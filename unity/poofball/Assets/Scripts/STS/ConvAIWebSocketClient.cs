using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Handles the WebSocket connection to an ElevenLabs Conversational AI agent,
/// sending user audio chunks and receiving text/audio responses in near real-time.
/// </summary>
public class ConvAIWebSocketClient : MonoBehaviour
{
    [Header("ElevenLabs Agent Settings")]
    [Tooltip("Public agent ID or a signed URL for private agents.")]
    public string agentId = "YOUR_AGENT_ID";

    private const string BaseWSUrl = "wss://api.elevenlabs.io/v1/convai/conversation?agent_id=";

    // .NET ClientWebSocket (available in Unity 2021+ with .NET 4.x or .NET Standard 2.1)
    private ClientWebSocket webSocket;
    private CancellationTokenSource cancelSource;
    private bool isConnected = false;

    // We queue outbound messages to avoid blocking the main thread
    private ConcurrentQueue<string> outboundQueue = new ConcurrentQueue<string>();

    // Events for external scripts (mic capture, audio player, etc.)
    public event Action<string> OnAgentTextResponse;
    public event Action<byte[]> OnAgentAudioChunk;
    public event Action OnConnectionOpened;
    public event Action OnConnectionClosed;

    private void Awake()
    {
        Debug.Log($"[ConvAI] Awake: WebSocket client ready. Agent ID={agentId}");
    }

    /// <summary>
    /// Initiate a connection to the ElevenLabs agent via WebSocket.
    /// </summary>
    public async void Connect()
    {
        if (isConnected)
        {
            Debug.LogWarning("[ConvAI] Connect() called but already connected.");
            return;
        }

        // Construct the direct or signed URL
        string url = BaseWSUrl + agentId;
        Debug.Log($"[ConvAI] Attempting to connect to: {url}");

        webSocket = new ClientWebSocket();
        cancelSource = new CancellationTokenSource();

        try
        {
            Uri serverUri = new Uri(url);
            await webSocket.ConnectAsync(serverUri, cancelSource.Token);
            isConnected = true;
            OnConnectionOpened?.Invoke();
            Debug.Log("[ConvAI] WebSocket connected successfully.");

            // Optionally: send a conversation initiation message
            SendJsonMessage(new { type = "conversation_initiation_client_data" });
            Debug.Log("[ConvAI] Sent conversation_initiation_client_data event.");

            // Start background tasks
            _ = Task.Run(ReceiveLoop);
            _ = Task.Run(SendLoop);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ConvAI] Connection failed: {ex.Message}");
            isConnected = false;
            OnConnectionClosed?.Invoke();
        }
    }

    /// <summary>
    /// Disconnect from the agent (if connected).
    /// </summary>
    public async void Disconnect()
    {
        if (!isConnected)
        {
            Debug.LogWarning("[ConvAI] Disconnect() called but not connected.");
            return;
        }

        Debug.Log("[ConvAI] Disconnecting...");
        try
        {
            cancelSource.Cancel();
            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client Disconnect",
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ConvAI] Exception during disconnection: {ex.Message}");
        }
        finally
        {
            isConnected = false;
            OnConnectionClosed?.Invoke();
            Debug.Log("[ConvAI] WebSocket disconnected.");
        }
    }

    /// <summary>
    /// Enqueue a base64-encoded PCM audio chunk for sending to the server.
    /// </summary>
    public void SendAudioChunk(string base64Audio)
    {
        if (!isConnected)
        {
            Debug.LogWarning("[ConvAI] Attempting to send audio chunk but not connected.");
            return;
        }

        Debug.Log($"[ConvAI] Queuing audio chunk. base64 length={base64Audio.Length}");
        SendJsonMessage(new { user_audio_chunk = base64Audio });
    }

    /// <summary>
    /// General method for enqueuing any JSON object to the server.
    /// </summary>
    public void SendJsonMessage(object obj)
    {
        if (!isConnected)
        {
            Debug.LogWarning("[ConvAI] Attempting to send JSON but not connected.");
            return;
        }

        string json = JsonConvert.SerializeObject(obj);
        outboundQueue.Enqueue(json);
        Debug.Log($"[ConvAI] Enqueued JSON message: {json}");
    }

    /// <summary>
    /// Background task that continuously sends queued messages to the server.
    /// </summary>
    private async Task SendLoop()
    {
        Debug.Log("[ConvAI] SendLoop started.");
        try
        {
            while (isConnected && webSocket.State == WebSocketState.Open && !cancelSource.IsCancellationRequested)
            {
                if (outboundQueue.TryDequeue(out string message))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    var seg = new ArraySegment<byte>(bytes);

                    try
                    {
                        await webSocket.SendAsync(seg, WebSocketMessageType.Text, true, cancelSource.Token);
                        Debug.Log($"[ConvAI] Sent {bytes.Length} bytes to server.");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ConvAI] SendAsync exception: {ex.Message}");
                    }
                }
                else
                {
                    // No messages in the queue, small delay
                    await Task.Delay(10);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ConvAI] SendLoop exception: {e.Message}");
        }
        Debug.Log("[ConvAI] SendLoop exiting.");
    }

    /// <summary>
    /// Background task that continuously receives messages from the server.
    /// Handles multi-frame messages by accumulating them in a StringBuilder
    /// until 'EndOfMessage' is true, then parsing the entire JSON.
    /// </summary>
    private async Task ReceiveLoop()
    {
        Debug.Log("[ConvAI] ReceiveLoop started.");

        byte[] buffer = new byte[8192];
        var sb = new StringBuilder(); // accumulate message across frames

        try
        {
            while (isConnected && webSocket.State == WebSocketState.Open && !cancelSource.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result = null;

                try
                {
                    // Receive one frame
                    result = await webSocket.ReceiveAsync(segment, cancelSource.Token);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ConvAI] ReceiveAsync exception: {ex.Message}");
                    break;
                }

                // If server closes the connection, handle it
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("[ConvAI] Server closed the connection.");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", cancelSource.Token);
                    break;
                }

                // Convert this frame to string
                int count = result.Count;
                string partialMsg = Encoding.UTF8.GetString(buffer, 0, count);
                sb.Append(partialMsg);

                // If it's the last frame of this message, parse the entire message
                if (result.EndOfMessage)
                {
                    string fullMessage = sb.ToString();
                    sb.Clear(); // reset for next message

                    Debug.Log($"[ConvAI] Received full message ({fullMessage.Length} chars): {fullMessage}");
                    HandleServerJsonMessage(fullMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ConvAI] ReceiveLoop exception: {ex.Message}");
        }
        finally
        {
            Debug.Log("[ConvAI] ReceiveLoop ending. Disconnecting...");
            Disconnect();
        }
    }

    /// <summary>
    /// Parse and handle a complete JSON message from the server.
    /// </summary>
    /// <param name="msgStr">The entire JSON string from the server.</param>
    private void HandleServerJsonMessage(string msgStr)
    {
        try
        {
            var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(msgStr);
            if (root == null || !root.ContainsKey("type"))
            {
                Debug.LogWarning("[ConvAI] No 'type' in received JSON, ignoring.");
                return;
            }

            string eventType = root["type"].ToString();
            Debug.Log($"[ConvAI] Event type: {eventType}");

            switch (eventType)
            {
                case "ping":
                    var pingEvt = JsonConvert.DeserializeObject<PingEvent>(msgStr);
                    if (pingEvt != null)
                    {
                        // reply with pong
                        SendJsonMessage(new { type = "pong", event_id = pingEvt.ping_event.event_id });
                        Debug.Log($"[ConvAI] Replied with 'pong' for event_id={pingEvt.ping_event.event_id}");
                    }
                    break;

                case "agent_response":
                    var agentResp = JsonConvert.DeserializeObject<AgentResponseEvent>(msgStr);
                    if (agentResp?.agent_response_event?.agent_response != null)
                    {
                        string text = agentResp.agent_response_event.agent_response;
                        Debug.Log($"[ConvAI] Agent text: {text}");
                        OnAgentTextResponse?.Invoke(text);
                    }
                    break;

                case "audio":
                    var audioEvt = JsonConvert.DeserializeObject<AudioResponseEvent>(msgStr);
                    if (audioEvt != null && !string.IsNullOrEmpty(audioEvt.audio_event.audio_base_64))
                    {
                        Debug.Log("[ConvAI] Received agent audio chunk (base64).");
                        byte[] rawPcm = Convert.FromBase64String(audioEvt.audio_event.audio_base_64);
                        OnAgentAudioChunk?.Invoke(rawPcm);
                    }
                    else
                    {
                        Debug.LogWarning("[ConvAI] Audio event but data was empty!");
                    }
                    break;

                case "user_transcript":
                    Debug.Log("[ConvAI] Received user_transcript event.");
                    // If you want to do something with recognized user text, parse it here
                    break;

                case "conversation_initiation_metadata":
                    Debug.Log("[ConvAI] Received conversation_initiation_metadata event. This includes conversation_id and audio format info.");
                    // You can parse the content from msgStr if needed
                    break;

                case "interruption":
                    Debug.Log("[ConvAI] Received interruption event.");
                    break;

                default:
                    Debug.LogWarning($"[ConvAI] Unhandled event type: {eventType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ConvAI] Failed to parse JSON: {ex.Message}\nRaw data: {msgStr}");
        }
    }
}

// Minimal classes for recognized JSON event structures:

[Serializable]
public class PingEvent
{
    public string type;
    public PingPayload ping_event;
}

[Serializable]
public class PingPayload
{
    public int event_id;
    public int? ping_ms;
}

[Serializable]
public class AgentResponseEvent
{
    public string type;
    public AgentResponsePayload agent_response_event;
}

[Serializable]
public class AgentResponsePayload
{
    public string agent_response;
}

[Serializable]
public class AudioResponseEvent
{
    public string type;
    public AudioPayload audio_event;
}

[Serializable]
public class AudioPayload
{
    public string audio_base_64;
    public int event_id;
}
