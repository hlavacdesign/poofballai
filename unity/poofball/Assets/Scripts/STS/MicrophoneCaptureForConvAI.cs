using UnityEngine;
using System;

[RequireComponent(typeof(ConvAIWebSocketClient))]
public class MicrophoneCaptureForConvAI : MonoBehaviour
{
    [Header("Microphone Settings")]
    [Tooltip("Sample rate in Hz for your mic capture.")]
    public int sampleRate = 16000;

    [Tooltip("Length of each chunk in seconds before sending to the server.")]
    public int chunkLengthSec = 1;

    [Tooltip("Set true to have the Microphone.Start() loop over the audio buffer.")]
    public bool loopRecording = true;

    private AudioClip micClip;
    private string micDevice;
    private int sampleCountPerChunk;
    private int lastSamplePos;

    private ConvAIWebSocketClient convAIClient;

    private void Awake()
    {
        Debug.Log("[MicCapture] Awake: preparing microphone capture script.");
    }

    void Start()
    {
        convAIClient = GetComponent<ConvAIWebSocketClient>();
        // Subscribe to connection events
        convAIClient.OnConnectionOpened += OnConnectionOpened;
        convAIClient.OnConnectionClosed += OnConnectionClosed;

        sampleCountPerChunk = sampleRate * chunkLengthSec;

        // Optional: if you want to auto-connect, you can do:
        // convAIClient.Connect();
        Debug.Log($"[MicCapture] Start: chunkLengthSec={chunkLengthSec}, sampleRate={sampleRate}, sampleCountPerChunk={sampleCountPerChunk}");

        // >>> ADD THIS LINE: <<<
        ConnectToAgent(); // automatically connect on Start
    }

    void OnDestroy()
    {
        convAIClient.OnConnectionOpened -= OnConnectionOpened;
        convAIClient.OnConnectionClosed -= OnConnectionClosed;
    }

    void Update()
    {
        if (micClip == null) return; // Not recording yet

        int currentPos = Microphone.GetPosition(micDevice);
        if (currentPos < lastSamplePos)
        {
            // Possibly wrapped if in loop mode
            Debug.Log("[MicCapture] Microphone position wrapped in loop mode.");
            lastSamplePos = 0;
        }

        int samplesAvailable = currentPos - lastSamplePos;
        if (samplesAvailable >= sampleCountPerChunk)
        {
            Debug.Log($"[MicCapture] We have {samplesAvailable} samples available. Will extract {sampleCountPerChunk} samples.");

            float[] samples = new float[sampleCountPerChunk];
            micClip.GetData(samples, lastSamplePos);

            // Convert floats -> 16-bit PCM
            byte[] pcmBytes = ConvertFloatsTo16BitPCM(samples);

            // Base64 encode
            string b64 = Convert.ToBase64String(pcmBytes);
            Debug.Log($"[MicCapture] Sending chunk to agent. PCM bytes={pcmBytes.Length}, base64 length={b64.Length}");

            // Send to agent
            convAIClient.SendAudioChunk(b64);

            lastSamplePos += sampleCountPerChunk;
        }
    }

    private void OnConnectionOpened()
    {
        Debug.Log("[MicCapture] OnConnectionOpened: Starting microphone capture...");
        StartMicrophone();
    }

    private void OnConnectionClosed()
    {
        Debug.Log("[MicCapture] OnConnectionClosed: stopping microphone...");
        StopMicrophone();
    }

    public void ConnectToAgent()
    {
        Debug.Log("[MicCapture] ConnectToAgent() called.");
        convAIClient.Connect();
    }

    public void DisconnectFromAgent()
    {
        Debug.Log("[MicCapture] DisconnectFromAgent() called.");
        convAIClient.Disconnect();
    }

    private void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[MicCapture] No microphones found on this system.");
            return;
        }

        micDevice = Microphone.devices[0];
        Debug.Log($"[MicCapture] Using mic device: {micDevice}. Starting at {sampleRate} Hz, loop={loopRecording}");
        micClip = Microphone.Start(micDevice, loopRecording, chunkLengthSec * 2, sampleRate);
        lastSamplePos = 0;
    }

    private void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
        {
            Debug.Log($"[MicCapture] Stopping mic device: {micDevice}");
            Microphone.End(micDevice);
        }
        micClip = null;
    }

    /// <summary>
    /// Convert float samples [-1..+1] to 16-bit PCM.
    /// </summary>
    private byte[] ConvertFloatsTo16BitPCM(float[] samples)
    {
        short[] intData = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            float f = Mathf.Clamp(samples[i], -1f, 1f);
            intData[i] = (short)(f * short.MaxValue);
        }

        byte[] bytes = new byte[intData.Length * 2];
        Buffer.BlockCopy(intData, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
