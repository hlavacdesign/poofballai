using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class ElevenLabsSpeechToSpeechAPI : MonoBehaviour
{
    [Header("ElevenLabs API Settings")]
    public string apiKey = "sk_9cb064675e99d8c7c4a4aa583ebb132571b4a04d890bc773";
    // The Voice ID for your custom voice from VoiceLab
    public string voiceId = "YOUR_CUSTOM_VOICE_ID";

    // For STS, e.g., "eleven_english_sts_v2" or "eleven_multilingual_sts_v2"
    public string modelId = "eleven_english_sts_v2";

    // Updated to request raw PCM output at 44.1 kHz
    public string outputFormat = "pcm_44100";

    // (Optional) For noise reduction
    public bool removeBackgroundNoise = false;

    // The base STS streaming endpoint
    private const string BaseUrl = "https://api.elevenlabs.io/v1/speech-to-speech";

    /// <summary>
    /// Sends a chunk of audio (e.g. WAV) to the ElevenLabs STS streaming endpoint,
    /// returns raw 16-bit PCM data via onComplete callback.
    /// </summary>
    public IEnumerator ConvertSpeechChunk(byte[] wavData, System.Action<byte[]> onComplete)
    {
        // Construct the full URL
        string url = $"{BaseUrl}/{voiceId}/stream";

        // Create the form data
        WWWForm form = new WWWForm();
        form.AddField("model_id", modelId);
        form.AddField("output_format", outputFormat);
        form.AddField("remove_background_noise", removeBackgroundNoise ? "true" : "false");
        // Add the audio file as "audio_file"
        form.AddBinaryData("audio_file", wavData, "input.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            // Add API key
            www.SetRequestHeader("xi-api-key", apiKey);

            // We'll receive binary data
            www.downloadHandler = new DownloadHandlerBuffer();

            // Send the request and wait
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"ElevenLabs STS error: {www.error}");
                onComplete?.Invoke(null);
            }
            else
            {
                // This is raw PCM 16-bit data at 44.1 kHz (single- or dual-channel, typically single)
                byte[] responseAudioBytes = www.downloadHandler.data;
                onComplete?.Invoke(responseAudioBytes);
            }
        }
    }
}
