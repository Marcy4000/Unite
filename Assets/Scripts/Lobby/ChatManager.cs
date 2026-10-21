using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ChatManager : MonoBehaviour
{
    public struct ChatMessage : INetworkSerializable
    {
        public string senderID;
        public string message;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref senderID);
            serializer.SerializeValue(ref message);
        }
    }

    [SerializeField] private GameObject messageRecieverPrefab;
    [SerializeField] private GameObject chatMessagePrefabOther, chatMessagePrefabSelf;
    [SerializeField] private Transform chatMessageContainer;
    [SerializeField] private GameObject chatContainer;
    [SerializeField] private TMP_InputField chatInputField;

    [Header("Unread Messages")]
    [SerializeField] private GameObject unreadBadge;
    [SerializeField] private TMP_Text unreadCountText;
    [SerializeField] private GameObject messageNotification;
    [SerializeField] private TMP_Text notificationSenderText;
    [SerializeField] private TMP_Text notificationMessageText;
    [SerializeField, Min(0.1f)] private float notificationDuration = 6f;

    private ChatMessageReciever chatMessageReciever;
    private Unite.Multiplayer.GameSessionView currentLobby;
    private float notificationHideTime;

    public int UnreadMessageCount { get; private set; }

    private List<ChatMessageUI> chatMessages = new List<ChatMessageUI>();

    private void OnEnable()
    {
        SyncLobby();
        CloseChat();
    }

    private void OnDisable()
    {
        CloseChat();
    }

    private void OnDestroy()
    {
        if (chatMessageReciever != null)
            chatMessageReciever.OnMessageRecieved -= RecieveMessageRPC;
    }

    private void Update()
    {
        SyncLobby();
        if (messageNotification != null && messageNotification.activeSelf && Time.unscaledTime >= notificationHideTime)
            messageNotification.SetActive(false);

        var networkManager = NetworkManager.Singleton;
        if (currentLobby == null || networkManager == null) return;

        if (networkManager.IsServer)
        {
            if (chatMessageReciever == null)
            {
                chatMessageReciever = Instantiate(messageRecieverPrefab, transform).GetComponent<ChatMessageReciever>();
                chatMessageReciever.NetworkObject.Spawn();
                chatMessageReciever.OnMessageRecieved += RecieveMessageRPC;
            }
        }
        else if (networkManager.IsClient)
        {
            if (chatMessageReciever == null)
            {
                ChatMessageReciever messageReciever = FindFirstObjectByType<ChatMessageReciever>();

                if (messageReciever != null)
                {
                    chatMessageReciever = messageReciever;
                    chatMessageReciever.OnMessageRecieved += RecieveMessageRPC;
                }
            }
        }
    }

    public void OpenChat()
    {
        SyncLobby();
        chatContainer.SetActive(true);
        MarkMessagesRead();
    }

    public void CloseChat()
    {
        if (chatContainer != null) chatContainer.SetActive(false);
        if (messageNotification != null) messageNotification.SetActive(false);
    }

    public void ClearChat()
    {
        foreach (ChatMessageUI chatMessage in chatMessages)
        {
            Destroy(chatMessage.gameObject);
        }
        chatMessages.Clear();
        MarkMessagesRead();
    }

    public void SendChatMessage()
    {
        if (chatMessageReciever != null && chatMessageReciever.IsSpawned &&
            LobbyController.Instance?.Lobby != null && LobbyController.Instance.Player != null &&
            !string.IsNullOrWhiteSpace(chatInputField.text))
        {
            ChatMessage message = new ChatMessage
            {
                senderID = LobbyController.Instance.Player.Id,
                message = chatInputField.text
            };
            chatMessageReciever.RecieveMessageRPC(message);
            chatInputField.text = "";
        }
    }

    public void RecieveMessageRPC(ChatMessage message)
    {
        SyncLobby();
        if (currentLobby == null || LobbyController.Instance.Player == null || string.IsNullOrWhiteSpace(message.message)) return;

        bool isOwnMessage = message.senderID == LobbyController.Instance.Player.Id;
        ChatMessageUI chatMessageUI = Instantiate(isOwnMessage ? chatMessagePrefabSelf : chatMessagePrefabOther, chatMessageContainer).GetComponent<ChatMessageUI>();
        chatMessageUI.SetMessage(message);
        chatMessages.Add(chatMessageUI);

        if (isOwnMessage || chatContainer.activeInHierarchy) return;

        UnreadMessageCount++;
        if (unreadBadge != null) unreadBadge.SetActive(true);
        if (unreadCountText != null)
            unreadCountText.text = UnreadMessageCount > 99 ? "99+" : UnreadMessageCount.ToString();

        var sender = currentLobby.Players.Find(player => player.Id == message.senderID);
        if (notificationSenderText != null)
            notificationSenderText.text = sender != null && sender.Data.TryGetValue("PlayerName", out var name)
                ? name.Value : "Party member";
        if (notificationMessageText != null) notificationMessageText.text = message.message;
        notificationHideTime = Time.unscaledTime + notificationDuration;
        if (messageNotification != null) messageNotification.SetActive(isActiveAndEnabled);
    }

    private void SyncLobby()
    {
        var lobby = LobbyController.Instance?.Lobby;
        if (ReferenceEquals(currentLobby, lobby)) return;
        currentLobby = lobby;
        ClearChat();
        CloseChat();
    }

    private void MarkMessagesRead()
    {
        UnreadMessageCount = 0;
        if (unreadBadge != null) unreadBadge.SetActive(false);
        if (unreadCountText != null) unreadCountText.text = "0";
        if (messageNotification != null) messageNotification.SetActive(false);
    }
}
