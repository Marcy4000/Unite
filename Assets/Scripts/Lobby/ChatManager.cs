using System.Collections;
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

    private ChatMessageReciever chatMessageReciever;

    private List<ChatMessageUI> chatMessages = new List<ChatMessageUI>();

    private void Start()
    {
        CloseChat();

        chatMessages.Clear();
    }

    private void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (chatMessageReciever == null)
            {
                chatMessageReciever = Instantiate(messageRecieverPrefab, transform).GetComponent<ChatMessageReciever>();
                chatMessageReciever.NetworkObject.Spawn();
                chatMessageReciever.OnMessageRecieved += RecieveMessageRPC;
            }
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            if (chatMessageReciever == null)
            {
                ChatMessageReciever messageReciever = FindObjectOfType<ChatMessageReciever>();

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
        chatContainer.SetActive(true);
    }

    public void CloseChat()
    {
        chatContainer.SetActive(false);
    }

    public void ClearChat()
    {
        foreach (ChatMessageUI chatMessage in chatMessages)
        {
            Destroy(chatMessage.gameObject);
        }
        chatMessages.Clear();
    }

    public void SendChatMessage()
    {
        if (chatInputField.text.Length > 0)
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
        ChatMessageUI chatMessageUI = Instantiate(message.senderID == LobbyController.Instance.Player.Id ? chatMessagePrefabSelf : chatMessagePrefabOther, chatMessageContainer).GetComponent<ChatMessageUI>();
        chatMessageUI.SetMessage(message);
        chatMessages.Add(chatMessageUI);
    }
}
