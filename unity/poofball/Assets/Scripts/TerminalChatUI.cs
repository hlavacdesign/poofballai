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

    // We'll add a reference to TyperSequential to handle streaming text
    private TyperSequential typer;

    void Start()
    {
        // Decide which backend URL to use:
        activeBackendUrl = useLocalServer ? localBackendUrl : hostedBackendUrl;

        // If there's no TyperSequential attached, add it
        typer = GetComponent<TyperSequential>();
        if (typer == null)
        {
            typer = gameObject.AddComponent<TyperSequential>();
        }

        // On scene start, create the first input box
        CreateNewInputBox();
    }

    /// <summary>
    /// Spawns a new input box at the bottom for the user to type.
    /// Then forcibly re-scrolls so it's visible.
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

        // Scroll to bottom
        ScrollToBottomNow();
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

        // Re-scroll
        ScrollToBottomNow();

        // If user typed nothing, just create a new input
        if (string.IsNullOrWhiteSpace(userInput))
        {
            CreateNewInputBox();
            return;
        }

        // Display the user's text in an output box
        CreateOutputBoxWithCarat(userInput);

        // Optionally create a blank line
        CreateNewLine();

        // Send to LLM
        StartCoroutine(SendRequestToLLM(userInput));
    }

    /// <summary>
    /// Sends user message to LLM; once response arrives,
    /// we type the text in parallel with audio fetch + playback.
    /// We wait until BOTH are done, then allow new input.
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
                    // Remove last line if blank
                    RemoveLastLine();

                    // Start text typing
                    Coroutine typingCoro = StartCoroutine(CreateStreamingOutputBox(agentResponse.short_response));

                    // Start audio load + playback if there's an audio_url
                    Coroutine audioCoro = null;
                    if (!string.IsNullOrEmpty(agentResponse.audio_url))
                    {
                        audioCoro = StartCoroutine(DownloadAndPlayAudio(agentResponse.audio_url));
                    }

                    // Now wait for BOTH to finish:
                    yield return typingCoro;         // text typed
                    if (audioCoro != null) 
                        yield return audioCoro;      // audio done
                }
            }
            else
            {
                // Show error
                RemoveLastLine(); 
                CreateOutputBox("Error: " + www.error);
            }
        }

        // Only AFTER text typed and audio done, spawn a new input box
        CreateNewInputBox();
    }

    /// <summary>
    /// Creates an output box and types the text in a streaming style.
    /// </summary>
    private IEnumerator CreateStreamingOutputBox(string fullText)
    {
        // Instantiate the same prefab as normal
        GameObject outputObj = Instantiate(outputBoxPrefab, contentTransform);
        TMP_Text textComp = outputObj.GetComponentInChildren<TMP_Text>();

        // Start typed animation from TyperSequential
        yield return StartCoroutine(typer.TypeTextCoroutine(textComp, fullText, () =>
        {
            // Once fully typed, we can scroll
            ScrollToBottomNow();
        }));
    }

    /// <summary>
    /// Downloads an MP3 from audioUrl and plays it via the assigned AudioSource.
    /// Waits until the AudioSource finishes playing (if you want).
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

                    // Optionally wait until it finishes playing
                    while (audioSource.isPlaying)
                    {
                        yield return null;
                    }
                }
            }
            else
            {
                RemoveLastLine();
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

        ScrollToBottomNow();
    }

    private void CreateOutputBoxWithCarat(string text)
    {
        GameObject outputObj = Instantiate(outputBoxWithCaratPrefab, contentTransform);
        TMP_Text textComp = outputObj.GetComponentInChildren<TMP_Text>();
        textComp.text = text;

        ScrollToBottomNow();
    }

    /// <summary>
    /// Creates a purely blank line under the content. 
    /// (Call this wherever you want a line break.)
    /// </summary>
    private void CreateNewLine()
    {
        GameObject lineObj = Instantiate(outputBoxPrefab, contentTransform);
        TMP_Text textComp = lineObj.GetComponentInChildren<TMP_Text>();
        textComp.text = "\n";

        ScrollToBottomNow();
    }

    /// <summary>
    /// Removes the last line if it's blank (i.e., an empty outputBox).
    /// We do this before printing the LLM’s message to remove an extra blank line.
    /// </summary>
    private void RemoveLastLine()
    {
        if (contentTransform.childCount == 0) return;

        Transform lastChild = contentTransform.GetChild(contentTransform.childCount - 1);
        TMP_Text textComp = lastChild.GetComponentInChildren<TMP_Text>();
        if (textComp != null)
        {
            Destroy(lastChild.gameObject);
        }
        ScrollToBottomNow();
    }

    /// <summary>
    /// Immediately forces a layout rebuild and scrolls to bottom in the same frame.
    /// </summary>
    private void ScrollToBottomNow()
    {
        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            // Some setups might require (0,1) for top or (0,0) for bottom
            scrollRect.normalizedPosition = new Vector2(0, 0);
        }
    }
}
