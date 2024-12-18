using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class ExampleScript : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField inputField;
    public TMP_Text responseText;
    public Button sendButton;

    // The placeholder text object (assigned in Inspector)
    // This is the TMP_Text component used as a placeholder within the input field.
    public TMP_Text placeholderText;

    private string backendUrl = "http://127.0.0.1:5000/chat"; // Local dev URL

    void Start()
    {
        if (sendButton == null || inputField == null || responseText == null)
        {
            Debug.LogWarning("Please assign the sendButton, inputField, and responseText in the inspector.");
            return;
        }

        // Hook up the button click event
        sendButton.onClick.AddListener(SendMessageToBackend);

        // Hook up TMP_InputField events:
        // When the input field is selected (clicked into), hide placeholder.
        inputField.onSelect.AddListener(OnInputFieldSelected);
        // When the input field is deselected (clicked out), show placeholder if empty.
        inputField.onDeselect.AddListener(OnInputFieldDeselected);
        // When Enter is pressed (onSubmit event), trigger sending the message.
        inputField.onSubmit.AddListener(OnInputFieldSubmitted);
    }

    private void OnInputFieldSelected(string text)
    {
        // Hide placeholder when input field is selected
        if (placeholderText != null)
        {
            placeholderText.gameObject.SetActive(false);
        }
    }

    private void OnInputFieldDeselected(string text)
    {
        // If the input field is empty, show the placeholder again
        if (placeholderText != null && string.IsNullOrWhiteSpace(inputField.text))
        {
            placeholderText.gameObject.SetActive(true);
        }
    }

    private void OnInputFieldSubmitted(string submittedText)
    {
        // Pressing Enter inside the input field triggers submit, so send message
        SendMessageToBackend();
    }

    public void SendMessageToBackend()
    {
        string userMessage = inputField.text;
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            responseText.text = "Please enter a message first.";
            return;
        }

        // Optionally, clear the input field after sending
        inputField.text = "";
        if (placeholderText != null)
        {
            placeholderText.gameObject.SetActive(true);
        }

        StartCoroutine(SendRequest(userMessage));
    }

    IEnumerator SendRequest(string message)
    {
        string jsonData = "{\"message\": \"" + message.Replace("\"", "\\\"") + "\"}";

        using (UnityWebRequest www = new UnityWebRequest(backendUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string result = www.downloadHandler.text;
                string reply = ParseReplyFromJson(result);
                responseText.text = reply;
            }
            else
            {
                responseText.text = "Error: " + www.error;
            }
        }
    }

    private string ParseReplyFromJson(string json)
    {
        // Naive parsing: Expected format: {"reply":"some text"}
        int start = json.IndexOf(":") + 1;
        int end = json.LastIndexOf("}");
        string val = json.Substring(start, end - start).Trim().Trim('"');
        return val;
    }
}
