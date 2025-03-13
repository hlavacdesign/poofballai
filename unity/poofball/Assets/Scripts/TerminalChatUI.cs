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
///   "conversation_answer": "...",
///   "audio_url": "...",
///   "media_urls": ["...", ...]
/// }
/// </summary>
[Serializable]
public class AgentResponse
{
    public string conversation_answer;
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

    [Header("Camera Animation")]
    [Tooltip("Reference to the CameraFOVAnimator for the dramatic reveal.")]
    public CameraFOVAnimator cameraFOV;

    [Header("Gallery")]
    [Tooltip("Prefab for the GalleryBox, which will display multiple images.")]
    public GameObject galleryBoxPrefab;

    // Internal store of whichever URL we decide to use:
    private string activeBackendUrl;

    // Reference to the TMP_InputField from the currently active input box
    private TMP_InputField currentInputField;

    // We'll add a reference to TyperSequential to handle streaming text
    private TyperSequential typer;

    private bool firstTime = true;

    public LoadingCrumbs crumbs;

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

        // Instead of creating the input box immediately,
        // do an opening sequence: "hello" -> LLM -> typed response -> audio
        StartCoroutine(OpeningSequence());
    }

    /// <summary>
    /// The opening sequence:
    /// 1) We call the server with "hello"
    /// 2) Show typed response & audio
    /// 3) Show input box once done
    /// </summary>
    private IEnumerator OpeningSequence()
    {
        yield return StartCoroutine(SendRequestToLLM("hello, who are you?", skipCreateNewInput: true));
        CreateNewInputBox();
    }


/// <summary>
/// Helper coroutine aggregator that starts all passed coroutines in parallel
/// and yields until they have ALL completed.
/// </summary>
private IEnumerator WaitForAllCoroutines(params IEnumerator[] routines)
{
    // Start each IEnumerator as its own Coroutine.
    List<Coroutine> runningCoroutines = new List<Coroutine>();
    foreach (var routine in routines)
    {
        if (routine == null) 
            continue;
        runningCoroutines.Add(StartCoroutine(routine));
    }

    // Wait for them all
    foreach (var coro in runningCoroutines)
    {
        yield return coro;
    }
}

/// <summary>
/// Overload for SendRequestToLLM that can skip the "create new input" step
/// so we can do the opening sequence without showing the box early.
///
/// Now we start text-typing and audio coroutines in parallel.
/// The image loading (if any) also begins immediately in parallel.
/// </summary>
private IEnumerator SendRequestToLLM(string userMessage, bool skipCreateNewInput)
{
    // Optionally show loading crumbs
    if (skipCreateNewInput && crumbs != null) crumbs.StartCrumbs();

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
            stopCrumbs();

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

                // If there are any media_urls, create a gallery box immediately
                // (images will load in parallel via their own coroutines).
                if (agentResponse.media_urls != null && agentResponse.media_urls.Count > 0)
                {
                    // CreateNewLine(); // optional line break
                    CreateGalleryBox(agentResponse.media_urls);
                }

                // Prepare coroutines for text-typing and audio
                IEnumerator textTypingRoutine = CreateStreamingOutputBox(agentResponse.conversation_answer);
                IEnumerator audioRoutine = null;
                if (!string.IsNullOrEmpty(agentResponse.audio_url))
                {
                    audioRoutine = DownloadAndPlayAudio(agentResponse.audio_url);
                }

                // Now start them in parallel and wait until both complete (if audio is present)
                yield return StartCoroutine(WaitForAllCoroutines(textTypingRoutine, audioRoutine));
            }
        }
        else
        {
            stopCrumbs();

            // Show error
            RemoveLastLine();
            CreateOutputBox("Error: " + www.error);
        }
    }

    // Only AFTER text typed & audio done, optionally create input box
    if (!skipCreateNewInput)
    {
        CreateNewInputBox();
    }
}

    // / <summary>
    // / Overload for SendRequestToLLM that can skip the "create new input" step
    // / so we can do the opening sequence without showing the box early.
    // / </summary>
    // private IEnumerator SendRequestToLLM(string userMessage, bool skipCreateNewInput)
    // {
    //     // Optionally show loading crumbs
    //     if (crumbs != null) crumbs.StartCrumbs();

    //     string jsonData = "{\"message\": \"" + userMessage.Replace("\"", "\\\"") + "\"}";
    //     using (UnityWebRequest www = new UnityWebRequest(activeBackendUrl, "POST"))
    //     {
    //         byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
    //         www.uploadHandler = new UploadHandlerRaw(bodyRaw);
    //         www.downloadHandler = new DownloadHandlerBuffer();
    //         www.SetRequestHeader("Content-Type", "application/json");

    //         yield return www.SendWebRequest();

    //         if (www.result == UnityWebRequest.Result.Success)
    //         {
    //             stopCrumbs();

    //             // "result" is the JSON from the server
    //             string result = www.downloadHandler.text;

    //             // Parse JSON into AgentResponse object
    //             AgentResponse agentResponse = null;
    //             try
    //             {
    //                 agentResponse = JsonUtility.FromJson<AgentResponse>(result);
    //             }
    //             catch
    //             {
    //                 CreateOutputBox("Received invalid JSON:\n" + result);
    //             }

    //             if (agentResponse != null)
    //             {
    //                 // Remove last line if blank
    //                 RemoveLastLine();

    //                 // Now, if there are any media_urls, create a gallery box
    //                 if (agentResponse.media_urls != null && agentResponse.media_urls.Count > 0)
    //                 {
    //                     CreateNewLine(); // optional: create a line break

    //                     CreateGalleryBox(agentResponse.media_urls);
    //                 }

    //                 // Start text typing for conversation_answer
    //                 Coroutine typingCoro = StartCoroutine(
    //                     CreateStreamingOutputBox(agentResponse.conversation_answer)
    //                 );

    //                 // Start audio load + playback if there's an audio_url
    //                 Coroutine audioCoro = null;
    //                 if (!string.IsNullOrEmpty(agentResponse.audio_url))
    //                 {
    //                     audioCoro = StartCoroutine(DownloadAndPlayAudio(agentResponse.audio_url));
    //                 }

    //                 // Wait for text typed AND audio done (if any)
    //                 yield return typingCoro;
    //                 if (audioCoro != null)
    //                     yield return audioCoro;

    //             }
    //         }
    //         else
    //         {
    //             stopCrumbs();

    //             // Show error
    //             RemoveLastLine();
    //             CreateOutputBox("Error: " + www.error);
    //         }
    //     }

    //     // Only AFTER text typed and audio done, optionally create input box
    //     if (!skipCreateNewInput)
    //     {
    //         CreateNewInputBox();
    //     }
    // }

    /// <summary>
    /// Normal usage (the user typed something). 
    /// This calls the new overload with skipCreateNewInput=false.
    /// </summary>
    private IEnumerator SendRequestToLLM(string userMessage)
    {
        yield return StartCoroutine(SendRequestToLLM(userMessage, skipCreateNewInput: false));
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
    /// Once it starts playing, we call cameraFOV.FlyIn().
    /// Waits until the AudioSource finishes playing.
    /// </summary>
    private IEnumerator DownloadAndPlayAudio(string audioUrl)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            stopCrumbs();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null && audioSource != null)
                {
                    // Assign the clip and play
                    audioSource.clip = clip;
                    audioSource.Play();

                    // Kick off the camera FlyIn (only on the very first audio)
                    if (cameraFOV != null && firstTime)
                    {
                        cameraFOV.FlyIn(1.0f);
                        firstTime = false;
                    }

                    // Wait until it finishes playing
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
    /// Creates a new input box at the bottom for user typing,
    /// then forcibly scrolls so it's visible.
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
    /// Create an output box with the given text, forcibly re-scroll.
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

        stopCrumbs();

        Transform lastChild = contentTransform.GetChild(contentTransform.childCount - 1);
        TMP_Text textComp = lastChild.GetComponentInChildren<TMP_Text>();
        if (textComp != null)
        {
            Destroy(lastChild.gameObject);
        }
        ScrollToBottomNow();
    }

    private void stopCrumbs()
    {
        // stop the loading crumbs
        if (crumbs != null)
        {
            crumbs.StopCrumbs();
        }
    }

    /// <summary>
    /// Immediately forces a layout rebuild and scrolls to bottom in the same frame.
    /// </summary>
    private void ScrollToBottomNow()
    {
        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.normalizedPosition = new Vector2(0, 0);
        }
    }

    /// <summary>
    /// Instantiates a GalleryBox and initializes it with the given image URLs.
    /// </summary>
    private void CreateGalleryBox(List<string> imageUrls)
    {
        if (galleryBoxPrefab == null)
        {
            Debug.LogWarning("No GalleryBox prefab assigned!");
            return;
        }

        // Instantiate the gallery box as a child of contentTransform
        GameObject galleryObj = Instantiate(galleryBoxPrefab, contentTransform);

        // Scroll after creation
        ScrollToBottomNow();

        // Initialize the gallery with the provided URLs
        GalleryBox gallery = galleryObj.GetComponent<GalleryBox>();
        if (gallery != null)
        {
            gallery.Initialize(imageUrls);
        }
        else
        {
            Debug.LogWarning("GalleryBox component not found on prefab.");
        }
    }
}
