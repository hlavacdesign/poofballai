using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class TerminalChatUI : MonoBehaviour
{
    [Header("UI Setup")]
    [Tooltip("Parent transform (Content) that holds all messages. Should NOT be destroyed.")]
    public Transform contentTransform;

    [Tooltip("Prefab that has a TMP_InputField (the prompt).")]
    public GameObject inputBoxPrefab;

    [Tooltip("Prefab that has a TMP_Text (for showing messages).")]
    public GameObject outputBoxPrefab;

    [Tooltip("Optional ScrollRect to keep chat scrolled to bottom.")]
    public ScrollRect scrollRect;

    [Header("Backend")]
    [Tooltip("Endpoint for the LLM POST request.")]
    public string backendUrl = "https://poofballai.onrender.com/chat";

    // Reference to the TMP_InputField from the currently active input box
    private TMP_InputField currentInputField;

    void Start()
    {
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
        CreateOutputBox(userInput);

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
        using (UnityWebRequest www = new UnityWebRequest(backendUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // Parse JSON
                string result = www.downloadHandler.text;
                string reply = ParseReplyFromJson(result);

                // Create an output box for the LLM reply
                CreateOutputBox(reply);
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

    /// <summary>
    /// Parses JSON like {"reply":"Hello!"}.
    /// Adjust for your actual JSON structure if needed.
    /// </summary>
    private string ParseReplyFromJson(string json)
    {
        int startIdx = json.IndexOf(":") + 1;
        int endIdx = json.LastIndexOf("}");
        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx) return json;
        return json.Substring(startIdx, endIdx - startIdx).Trim().Trim('"');
    }

    /// <summary>
    /// 1) Immediately forces a layout rebuild
    /// 2) Scrolls to bottom in the same frame
    /// </summary>
    private void ScrollToBottomNow()
    {
        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            // In a vertical ScrollRect, (0,0) is the bottom
            scrollRect.normalizedPosition = new Vector2(0, 1);
        }
    }

    /// <summary>
    /// Waits one frame, then again forces a rebuild & scroll to bottom.
    /// This handles any late UI changes not caught by the immediate rebuild.
    /// </summary>
    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;

        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.normalizedPosition = new Vector2(0, 1);
        }
    }
}
