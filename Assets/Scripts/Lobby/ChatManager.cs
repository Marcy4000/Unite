using System.Collections.Generic;
using TMPro;
using Unite.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ChatManager : MonoBehaviour
{
    public enum ChatChannel { Global, Private }

    public struct ChatMessage : INetworkSerializable
    {
        public string senderID;
        public string recipientID;
        public string lobbyID;
        public string message;

        public bool IsPrivate => !string.IsNullOrEmpty(recipientID);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref senderID);
            serializer.SerializeValue(ref recipientID);
            serializer.SerializeValue(ref lobbyID);
            serializer.SerializeValue(ref message);
        }
    }

    [SerializeField] private GameObject messageRecieverPrefab;
    [SerializeField] private GameObject chatMessagePrefabOther, chatMessagePrefabSelf;
    [SerializeField] private Transform chatMessageContainer;
    [SerializeField] private GameObject chatContainer;
    [SerializeField] private TMP_InputField chatInputField;

    [Header("Channels")]
    [SerializeField] private Toggle globalChatToggle;
    [SerializeField] private Toggle privateChatToggle;
    [SerializeField] private TMP_Dropdown privatePlayerDropdown;

    [Header("Unread Messages")]
    [SerializeField] private GameObject unreadBadge;
    [SerializeField] private TMP_Text unreadCountText;
    [SerializeField] private GameObject messageNotification;
    [SerializeField] private TMP_Text notificationSenderText;
    [SerializeField] private TMP_Text notificationMessageText;
    [SerializeField, Min(0.1f)] private float notificationDuration = 6f;

    private ChatMessageReciever chatMessageReciever;
    private GameSessionView currentLobby;
    private float notificationHideTime;
    private ChatMessage? notificationMessage;
    private readonly List<ChatMessageUI> chatMessages = new List<ChatMessageUI>();
    private readonly List<ChatMessage> messageHistory = new List<ChatMessage>();
    private readonly HashSet<int> unreadMessages = new HashSet<int>();
    private readonly List<string> privatePlayerIds = new List<string>();

    public int UnreadMessageCount => unreadMessages.Count;
    public ChatChannel CurrentChannel { get; private set; }
    public string SelectedPrivatePlayerId { get; private set; }

    private void OnEnable()
    {
        if (globalChatToggle != null) globalChatToggle.onValueChanged.AddListener(OnGlobalChatChanged);
        if (privateChatToggle != null) privateChatToggle.onValueChanged.AddListener(OnPrivateChatChanged);
        if (privatePlayerDropdown != null) privatePlayerDropdown.onValueChanged.AddListener(SelectPrivatePlayer);
        SyncLobby();
        RefreshConversation();
        CloseChat();
    }

    private void OnDisable()
    {
        if (globalChatToggle != null) globalChatToggle.onValueChanged.RemoveListener(OnGlobalChatChanged);
        if (privateChatToggle != null) privateChatToggle.onValueChanged.RemoveListener(OnPrivateChatChanged);
        if (privatePlayerDropdown != null) privatePlayerDropdown.onValueChanged.RemoveListener(SelectPrivatePlayer);
        BindReceiver(null);
        CloseChat();
    }

    private void OnDestroy()
    {
        BindReceiver(null);
    }

    private void Update()
    {
        SyncLobby();
        if (messageNotification != null && messageNotification.activeSelf && Time.unscaledTime >= notificationHideTime)
            messageNotification.SetActive(false);

        var networkManager = NetworkManager.Singleton;
        if (currentLobby == null || networkManager == null || !networkManager.IsListening)
        {
            BindReceiver(null);
            return;
        }
        if (chatMessageReciever != null && chatMessageReciever.IsSpawned &&
            chatMessageReciever.NetworkManager == networkManager) return;

        var receiver = System.Array.Find(FindObjectsByType<ChatMessageReciever>(FindObjectsSortMode.None),
            candidate => candidate.IsSpawned && candidate.NetworkManager == networkManager);
        if (receiver == null && networkManager.IsServer && messageRecieverPrefab != null)
        {
            receiver = Instantiate(messageRecieverPrefab).GetComponent<ChatMessageReciever>();
            receiver.NetworkObject.Spawn();
        }
        BindReceiver(receiver);
    }

    private void BindReceiver(ChatMessageReciever receiver)
    {
        if (ReferenceEquals(chatMessageReciever, receiver)) return;
        if (!ReferenceEquals(chatMessageReciever, null)) chatMessageReciever.OnMessageRecieved -= RecieveMessageRPC;
        chatMessageReciever = receiver;
        if (chatMessageReciever != null) chatMessageReciever.OnMessageRecieved += RecieveMessageRPC;
    }

    public void OpenChat()
    {
        SyncLobby();
        if (messageNotification != null && messageNotification.activeSelf && notificationMessage.HasValue)
        {
            var message = notificationMessage.Value;
            if (message.IsPrivate)
            {
                OpenPrivateChat(message.senderID);
                return;
            }
            CurrentChannel = ChatChannel.Global;
        }
        ShowChat();
    }

    public void OpenGlobalChat()
    {
        SyncLobby();
        CurrentChannel = ChatChannel.Global;
        ShowChat();
    }

    public void OpenPrivateChat(string playerId)
    {
        SyncLobby();
        if (!privatePlayerIds.Contains(playerId)) return;
        SelectedPrivatePlayerId = playerId;
        CurrentChannel = ChatChannel.Private;
        ShowChat();
    }

    private void ShowChat()
    {
        if (chatContainer != null) chatContainer.SetActive(true);
        RefreshConversation();
    }

    public void SetChatChannel(ChatChannel channel)
    {
        SyncLobby();
        CurrentChannel = channel;
        RefreshConversation();
    }

    private void OnGlobalChatChanged(bool isOn)
    {
        if (isOn) SetChatChannel(ChatChannel.Global);
    }

    private void OnPrivateChatChanged(bool isOn)
    {
        if (isOn) SetChatChannel(ChatChannel.Private);
    }

    public void SelectPrivatePlayer(int index)
    {
        if (index < 0 || index >= privatePlayerIds.Count) return;
        SelectedPrivatePlayerId = privatePlayerIds[index];
        RefreshConversation();
    }

    public void CloseChat()
    {
        if (chatContainer != null) chatContainer.SetActive(false);
        if (messageNotification != null) messageNotification.SetActive(false);
    }

    public void ClearChat()
    {
        foreach (var chatMessage in chatMessages)
            if (chatMessage != null) Destroy(chatMessage.gameObject);
        chatMessages.Clear();
        messageHistory.Clear();
        unreadMessages.Clear();
        notificationMessage = null;
        UpdateUnreadBadge();
        if (messageNotification != null) messageNotification.SetActive(false);
    }

    public void SendChatMessage()
    {
        SyncLobby();
        if (chatMessageReciever == null || !chatMessageReciever.IsSpawned || currentLobby == null ||
            LobbyController.Instance?.Player == null || chatInputField == null ||
            string.IsNullOrWhiteSpace(chatInputField.text)) return;
        if (CurrentChannel == ChatChannel.Private && !privatePlayerIds.Contains(SelectedPrivatePlayerId)) return;

        var message = new ChatMessage
        {
            senderID = LobbyController.Instance.Player.Id,
            recipientID = CurrentChannel == ChatChannel.Private ? SelectedPrivatePlayerId : string.Empty,
            lobbyID = currentLobby.Id,
            message = chatInputField.text
        };
        chatMessageReciever.RecieveMessageRPC(message);
        chatInputField.text = string.Empty;
    }

    public void RecieveMessageRPC(ChatMessage message)
    {
        SyncLobby();
        var localPlayerId = LobbyController.Instance?.Player?.Id;
        if (currentLobby == null || localPlayerId == null || string.IsNullOrWhiteSpace(message.message)) return;
        if (!string.IsNullOrEmpty(message.lobbyID) && message.lobbyID != currentLobby.Id) return;
        if (message.IsPrivate && message.senderID != localPlayerId && message.recipientID != localPlayerId) return;

        bool isOwnMessage = message.senderID == localPlayerId;
        var chatMessageUI = Instantiate(isOwnMessage ? chatMessagePrefabSelf : chatMessagePrefabOther, chatMessageContainer)
            .GetComponent<ChatMessageUI>();
        chatMessageUI.SetMessage(message);
        chatMessages.Add(chatMessageUI);
        messageHistory.Add(message);
        bool isVisible = IsInCurrentConversation(message);
        chatMessageUI.gameObject.SetActive(isVisible);

        if (isOwnMessage || (isVisible && chatContainer != null && chatContainer.activeInHierarchy)) return;

        unreadMessages.Add(messageHistory.Count - 1);
        UpdateUnreadBadge();
        notificationMessage = message;
        var sender = currentLobby.Players.Find(player => player.Id == message.senderID);
        if (notificationSenderText != null)
            notificationSenderText.text = GetPlayerName(sender);
        if (notificationMessageText != null) notificationMessageText.text = message.message;
        notificationHideTime = Time.unscaledTime + notificationDuration;
        if (messageNotification != null) messageNotification.SetActive(isActiveAndEnabled);
    }

    private bool IsInCurrentConversation(ChatMessage message)
    {
        if (CurrentChannel == ChatChannel.Global) return !message.IsPrivate;
        if (!message.IsPrivate || SelectedPrivatePlayerId == null) return false;
        var localPlayerId = LobbyController.Instance?.Player?.Id;
        return (message.senderID == localPlayerId && message.recipientID == SelectedPrivatePlayerId) ||
            (message.senderID == SelectedPrivatePlayerId && message.recipientID == localPlayerId);
    }

    private void RefreshConversation()
    {
        if (globalChatToggle != null) globalChatToggle.SetIsOnWithoutNotify(CurrentChannel == ChatChannel.Global);
        if (privateChatToggle != null) privateChatToggle.SetIsOnWithoutNotify(CurrentChannel == ChatChannel.Private);
        if (privatePlayerDropdown != null)
        {
            privatePlayerDropdown.gameObject.SetActive(CurrentChannel == ChatChannel.Private && privatePlayerIds.Count > 0);
            if (!privatePlayerDropdown.interactable) privatePlayerDropdown.Hide();
            privatePlayerDropdown.SetValueWithoutNotify(Mathf.Max(0, privatePlayerIds.IndexOf(SelectedPrivatePlayerId)));
            privatePlayerDropdown.RefreshShownValue();
        }
        for (int index = 0; index < chatMessages.Count; index++)
            if (chatMessages[index] != null)
                chatMessages[index].gameObject.SetActive(IsInCurrentConversation(messageHistory[index]));
        if (chatContainer != null && chatContainer.activeInHierarchy) MarkMessagesRead();
    }

    private void SyncLobby()
    {
        var lobby = LobbyController.Instance?.ChatLobby;
        if (!ReferenceEquals(currentLobby, lobby))
        {
            BindReceiver(null);
            currentLobby = lobby;
            CurrentChannel = ChatChannel.Global;
            SelectedPrivatePlayerId = null;
            privatePlayerIds.Clear();
            ClearChat();
            CloseChat();
        }
        RefreshPrivatePlayers();
    }

    private void RefreshPrivatePlayers()
    {
        var localPlayerId = LobbyController.Instance?.Player?.Id;
        int count = 0;
        bool changed = false;
        if (currentLobby != null)
            foreach (var player in currentLobby.Players)
            {
                if (player.IsBot || player.Id == localPlayerId) continue;
                if (count >= privatePlayerIds.Count || privatePlayerIds[count] != player.Id ||
                    (privatePlayerDropdown != null && (count >= privatePlayerDropdown.options.Count ||
                        privatePlayerDropdown.options[count].text != GetPlayerName(player)))) changed = true;
                count++;
            }
        if (!changed && count == privatePlayerIds.Count &&
            (privatePlayerDropdown == null || privatePlayerDropdown.options.Count == count)) return;

        privatePlayerIds.Clear();
        var options = new List<string>();
        if (currentLobby != null)
            foreach (var player in currentLobby.Players)
            {
                if (player.IsBot || player.Id == localPlayerId) continue;
                privatePlayerIds.Add(player.Id);
                options.Add(GetPlayerName(player));
            }
        if (!privatePlayerIds.Contains(SelectedPrivatePlayerId))
            SelectedPrivatePlayerId = privatePlayerIds.Count > 0 ? privatePlayerIds[0] : null;
        if (privatePlayerDropdown != null)
        {
            privatePlayerDropdown.Hide();
            privatePlayerDropdown.ClearOptions();
            privatePlayerDropdown.AddOptions(options);
        }
        RefreshConversation();
    }

    private static string GetPlayerName(GameSessionPlayer player) =>
        player != null && player.Data.TryGetValue("PlayerName", out var name) ? name.Value : "Party member";

    private void MarkMessagesRead()
    {
        unreadMessages.RemoveWhere(index => IsInCurrentConversation(messageHistory[index]));
        UpdateUnreadBadge();
        if (messageNotification != null) messageNotification.SetActive(false);
    }

    private void UpdateUnreadBadge()
    {
        if (unreadBadge != null) unreadBadge.SetActive(UnreadMessageCount > 0);
        if (unreadCountText != null)
            unreadCountText.text = UnreadMessageCount > 99 ? "99+" : UnreadMessageCount.ToString();
    }
}
