using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// Data model for the server's JSON response:
/// {
///   "long_response": "...",
///   "short_response": "...",
///   "audio_url": "...",
///   "media_urls": ["...", ...]
/// }
/// </summary>
[Serializable]
public class AgentResponse
{
    public string long_response;
    public string short_response;
    public string audio_url;
    public List<string> media_urls;
}

public class TerminalChatUI : MonoBehaviour
{
    [Header("UI Setup")]
    [Tooltip("Parent transform (Content) that holds all messages. Should NOT be destroyed.")]
    public Transform contentTransform;

    [Tooltip("Prefab that has a TMP_InputField (the prompt).")]
    public GameObject inputBoxPrefab;

    [Tooltip("Prefab that has a TMP_Text (for showing messages).")]
    public GameObject outputBoxPrefab;
    public GameObject outputBoxWithCaratPrefab;

    [Tooltip("Optional ScrollRect to keep chat scrolled to bottom.")]
    public ScrollRect scrollRect;

    [Header("Backend URLs")]
    [Tooltip("If true, use the local server. If false, use the hosted server URL.")]
    public bool useLocalServer = true;

    [Tooltip("Local development server endpoint.")]
    public string localBackendUrl = "http://127.0.0.1:5000/chat";

    [Tooltip("Hosted/production server endpoint.")]
    public string hostedBackendUrl = "https://poofballai.onrender.com/chat";

    [Header("Audio")]
    [Tooltip("AudioSource used to play TTS mp3 files.")]
    public AudioSource audioSource;

    // Internal store of whichever URL we decide to use:
    private string activeBackendUrl;

    // Reference to the TMP_InputField from the currently active input box
    private TMP_InputField currentInputField;

    void Start()
    {
        // Decide which backend URL to use:
        activeBackendUrl = useLocalServer ? localBackendUrl : hostedBackendUrl;

        // On scene start, create the first input box
        CreateNewInputBox();
    }

    /// <summary>
    /// Spawns a new input box at the bottom for the user to type.
    /// Then forcibly re-scrolls (both now & next frame) so it's visible.
    /// </summary>
    private void CreateNewInputBox()
    {
        GameObject inputObj = Instantiate(inputBoxPrefab, contentTransform);
        currentInputField = inputObj.GetComponentInChildren<TMP_InputField>();

        // Hook up Enter submission
        currentInputField.onSubmit.AddListener(OnSubmitInput);

        // Focus the field
        currentInputField.Select();
        currentInputField.ActivateInputField();

        // Scroll to bottom immediately & next frame
        ScrollToBottomNow();
        StartCoroutine(ScrollToBottomNextFrame());
    }

    /// <summary>
    /// Called when user hits Enter in the current input box.
    /// 1) Remove that input box
    /// 2) Scroll to bottom
    /// 3) If user typed something, create an output box & send to LLM
    /// 4) Otherwise, spawn a new input box
    /// </summary>
    private void OnSubmitInput(string userInput)
    {
        currentInputField.onSubmit.RemoveListener(OnSubmitInput);

        // Destroy just the input box object
        Destroy(currentInputField.gameObject);

        // Immediately & next frame re-scroll
        ScrollToBottomNow();
        StartCoroutine(ScrollToBottomNextFrame());

        // If user typed nothing, just create a new input
        if (string.IsNullOrWhiteSpace(userInput))
        {
            CreateNewInputBox();
            return;
        }

        // Display the user's text in an output box
        CreateOutputBoxWithCarat(userInput);

        // Send to LLM
        StartCoroutine(SendRequestToLLM(userInput));
    }

    /// <summary>
    /// Sends user message to LLM; once response arrives, create an output box.
    /// Then spawn a new input box. After each step, re-scroll.
    /// </summary>
    private IEnumerator SendRequestToLLM(string userMessage)
    {
        string jsonData = "{\"message\": \"" + userMessage.Replace("\"", "\\\"") + "\"}";
        using (UnityWebRequest www = new UnityWebRequest(activeBackendUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // "result" is the JSON from the server
                string result = www.downloadHandler.text;

                // Parse JSON into AgentResponse object
                AgentResponse agentResponse = null;
                try
                {
                    agentResponse = JsonUtility.FromJson<AgentResponse>(result);
                }
                catch
                {
                    CreateOutputBox("Received invalid JSON:\n" + result);
                }

                if (agentResponse != null)
                {
                    // Create an output box for the LLM's long_response
                    CreateOutputBox(agentResponse.short_response);

                    // If there's audio_url, download & play
                    if (!string.IsNullOrEmpty(agentResponse.audio_url))
                    {
                        StartCoroutine(DownloadAndPlayAudio(agentResponse.audio_url));
                    }
                }
            }
            else
            {
                // Show error
                CreateOutputBox("Error: " + www.error);
            }
        }

        // Then, spawn a new input box
        CreateNewInputBox();
    }

    /// <summary>
    /// Downloads an MP3 from audioUrl and plays it via the assigned AudioSource.
    /// </summary>
    private IEnumerator DownloadAndPlayAudio(string audioUrl)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null && audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
            else
            {
                CreateOutputBox("Audio download error: " + www.error);
            }
        }
    }

    /// <summary>
    /// Create an output box with the given text,
    /// then forcibly re-scroll.
    /// </summary>
    private void CreateOutputBox(string text)
    {
        GameObject outputObj = Instantiate(outputBoxPrefab, contentTransform);
        TMP_Text textComp = outputObj.GetComponentInChildren<TMP_Text>();
        textComp.text = text;

        // Immediately & next frame re-scroll
        ScrollToBottomNow();
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private void CreateOutputBoxWithCarat(string text)
    {
        GameObject outputObj = Instantiate(outputBoxWithCaratPrefab, contentTransform);
        TMP_Text textComp = outputObj.GetComponentInChildren<TMP_Text>();
        textComp.text = text;

        // Immediately & next frame re-scroll
        ScrollToBottomNow();
        StartCoroutine(ScrollToBottomNextFrame());
    }

    /// <summary>
    /// Immediately forces a layout rebuild and scrolls to bottom in the same frame.
    /// </summary>
    private void ScrollToBottomNow()
    {
        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            // Some setups might require (0,1) for top or (0,0) for bottom, depending on your UI arrangement
            scrollRect.normalizedPosition = new Vector2(0, 0);
        }
    }

    /// <summary>
    /// Waits one frame, then again forces a rebuild & scroll to bottom.
    /// This helps handle any late UI changes not caught by the immediate rebuild.
    /// </summary>
    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;

        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.normalizedPosition = new Vector2(0, 0);
        }
    }
}
