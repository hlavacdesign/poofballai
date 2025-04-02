using UnityEngine;
using System.Collections;
using System.IO;

[RequireComponent(typeof(ElevenLabsSpeechToSpeechAPI))]
[RequireComponent(typeof(RealTimeAudioPlayer))]
public class MicrophoneCapture : MonoBehaviour
{
    [Header("Recording Settings")]
    public int sampleRate = 44100;
    public int chunkLengthSec = 2; 
    public bool loopRecording = false;

    private AudioClip microphoneClip;
    private string microphoneDevice;
    private int sampleCountPerChunk;
    private int lastSamplePosition;

    private ElevenLabsSpeechToSpeechAPI sttAPI;
    private RealTimeAudioPlayer audioPlayer;

    void Start()
    {
        sttAPI = GetComponent<ElevenLabsSpeechToSpeechAPI>();
        audioPlayer = GetComponent<RealTimeAudioPlayer>();

        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            sampleCountPerChunk = sampleRate * chunkLengthSec;

            // Start recording in loop mode
            microphoneClip = Microphone.Start(microphoneDevice, loopRecording, chunkLengthSec * 2, sampleRate);
        }
        else
        {
            Debug.LogError("No microphone device found.");
        }
    }

    void Update()
    {
        if (microphoneClip == null) return;

        int currentPosition = Microphone.GetPosition(microphoneDevice);

        // Check if we've wrapped around in loop mode
        if (currentPosition < lastSamplePosition)
        {
            lastSamplePosition = 0;
        }

        int samplesAvailable = currentPosition - lastSamplePosition;
        if (samplesAvailable >= sampleCountPerChunk)
        {
            // Extract the chunk from the audio clip
            float[] samples = new float[sampleCountPerChunk];
            microphoneClip.GetData(samples, lastSamplePosition);

            // Convert float[] samples -> WAV byte[]
            byte[] wavBytes = ConvertSamplesToWav(samples, sampleRate, 1);

            // Send to ElevenLabs STS
            StartCoroutine(sttAPI.ConvertSpeechChunk(wavBytes, (convertedPcmBytes) => {
                if (convertedPcmBytes != null && convertedPcmBytes.Length > 0)
                {
                    // Play the raw PCM data (44.1 kHz, 16-bit)
                    audioPlayer.PlayPcmData(convertedPcmBytes);
                }
            }));

            lastSamplePosition += sampleCountPerChunk;
        }
    }

    /// <summary>
    /// Utility to convert raw float samples (–1f to +1f) into a 16-bit mono WAV file in memory.
    /// </summary>
    private byte[] ConvertSamplesToWav(float[] samples, int sampleRate, int channels)
    {
        // Convert float -> 16-bit PCM
        short[] intData = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            float f = Mathf.Clamp(samples[i], -1f, 1f);
            intData[i] = (short)(f * short.MaxValue);
        }

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // RIFF header
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + intData.Length * 2); // File size - 8
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // AudioFormat (PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2); // Byte rate
            writer.Write((short)(channels * 2));     // Block align
            writer.Write((short)16);                 // Bits per sample

            // data chunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(intData.Length * 2);

            // PCM data
            foreach (short s in intData)
            {
                writer.Write(s);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
