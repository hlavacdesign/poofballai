using UnityEngine;
using System;

[RequireComponent(typeof(AudioSource))]
public class RealTimeAudioPlayer : MonoBehaviour
{
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Takes the raw 16-bit PCM data from ElevenLabs (sampleRate=44100, channels=1 or 2)
    /// and plays it immediately as an AudioClip.
    /// </summary>
    /// <param name="pcmData">Raw PCM bytes in 16-bit little-endian format.</param>
    public void PlayPcmData(byte[] pcmData)
    {
        if (pcmData == null || pcmData.Length == 0)
        {
            Debug.LogWarning("No PCM data to play.");
            return;
        }

        // For this example, we'll assume it's MONO 16-bit at 44100 Hz from ElevenLabs
        // If you want to handle stereo or other rates, you need to adapt accordingly.
        int sampleRate = 44100;
        int channels = 1;
        int bytesPerSample = 2; // 16-bit
        int totalSamples = pcmData.Length / bytesPerSample;

        // Convert 16-bit PCM -> float[]
        float[] floatData = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            // Little-endian
            short sampleShort = BitConverter.ToInt16(pcmData, i * bytesPerSample);
            // Convert to float in range -1 to 1
            floatData[i] = sampleShort / (float)short.MaxValue;
        }

        // Create AudioClip
        AudioClip clip = AudioClip.Create(
            "ElevenLabsOutput",
            totalSamples,
            channels,
            sampleRate,
            false // no stream
        );
        clip.SetData(floatData, 0);

        // Play
        audioSource.PlayOneShot(clip);
    }
}
