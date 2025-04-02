using UnityEngine;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>
/// Receives raw PCM (16-bit) chunks from a ConvAIWebSocketClient,
/// but plays them sequentially (one after another) without overlap.
/// </summary>
[RequireComponent(typeof(ConvAIWebSocketClient))]
[RequireComponent(typeof(AudioSource))]
public class RealTimeAudioPlayerForConvAI : MonoBehaviour
{
    [Header("Agent Audio Format")]
    [Tooltip("Sample rate of the raw PCM returned by the agent (e.g. 16000).")]
    public int agentSampleRate = 16000;

    [Tooltip("Number of channels in the agent's PCM data (usually 1).")]
    public int agentChannels = 1;

    // A thread-safe queue for incoming PCM from the background
    private ConcurrentQueue<byte[]> backgroundPcmQueue = new ConcurrentQueue<byte[]>();

    // We'll move items from backgroundPcmQueue into this main-thread queue,
    // then a coroutine will process them *sequentially* to avoid overlap.
    private Queue<byte[]> playbackQueue = new Queue<byte[]>();

    private ConvAIWebSocketClient convAIClient;
    private AudioSource audioSource;

    // Reference to our "PlayInSequence" coroutine
    private Coroutine playbackCoroutine = null;

    private void Awake()
    {
        convAIClient = GetComponent<ConvAIWebSocketClient>();
        audioSource = GetComponent<AudioSource>();

        // Instead of playing immediately, we store PCM chunks in backgroundPcmQueue
        convAIClient.OnAgentAudioChunk += (byte[] pcmData) =>
        {
            backgroundPcmQueue.Enqueue(pcmData);
        };

        Debug.Log("[AudioPlayer] Awake: will play PCM chunks in strict sequence.");
    }

    private void Update()
    {
        // Move any PCM from the background queue into our main-thread "playbackQueue"
        while (backgroundPcmQueue.TryDequeue(out byte[] pcmData))
        {
            playbackQueue.Enqueue(pcmData);
        }

        // If we have pending audio chunks and the coroutine is not running, start it
        if (playbackQueue.Count > 0 && playbackCoroutine == null)
        {
            playbackCoroutine = StartCoroutine(PlayInSequence());
        }
    }

    /// <summary>
    /// Coroutine that continuously plays queued PCM chunks,
    /// one after another, until the queue is empty.
    /// </summary>
    private IEnumerator PlayInSequence()
    {
        Debug.Log("[AudioPlayer] PlayInSequence started.");

        while (playbackQueue.Count > 0)
        {
            // Dequeue the next chunk
            byte[] pcmData = playbackQueue.Dequeue();
            if (pcmData == null || pcmData.Length == 0)
            {
                Debug.LogWarning("[AudioPlayer] Encountered empty PCM data. Skipping...");
                continue;
            }

            // Convert to float samples
            float[] samples = Convert16BitPCMToFloats(pcmData, agentChannels);
            Debug.Log($"[AudioPlayer] Next chunk: {pcmData.Length} bytes => {samples.Length} float samples.");

            // Create AudioClip
            AudioClip clip = AudioClip.Create(
                "AgentReplyClip",
                samples.Length / agentChannels,
                agentChannels,
                agentSampleRate,
                false
            );
            clip.SetData(samples, 0);

            // Play the clip (no overlap)
            audioSource.Stop(); // ensure we don't have leftover playback
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log("[AudioPlayer] Playing clip sequentially.");

            // Wait for the current clip to finish
            // If the clip length is X, we wait X seconds.
            // That means we do not begin next chunk until this is done.
            yield return new WaitForSeconds(clip.length);
        }

        // Done with all queued chunks
        Debug.Log("[AudioPlayer] All queued chunks played. Stopping coroutine.");
        playbackCoroutine = null;
    }

    /// <summary>
    /// Convert raw 16-bit little-endian PCM to float[] in [-1..1].
    /// </summary>
    private float[] Convert16BitPCMToFloats(byte[] pcmBytes, int channels)
    {
        int totalSamples = pcmBytes.Length / 2; // 2 bytes per sample
        float[] floats = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            short val = BitConverter.ToInt16(pcmBytes, i * 2);
            floats[i] = val / (float)short.MaxValue;
        }
        return floats;
    }
}
