using JSAM;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class QuickChatUI : MonoBehaviour
{
    [SerializeField] private GameObject holder;
    [SerializeField] private PlayerHeadUI playerHeadUI;
    [SerializeField] private Image playerPokemonIcon;
    [SerializeField] private TMP_Text messageText;

    private Queue<QuickChatMessage> messageQueue = new Queue<QuickChatMessage>();

    private bool isShowingMessage;

    private void Start()
    {
        holder.SetActive(false);
    }

    public void EnqueueMessage(QuickChatMessage message)
    {
        messageQueue.Enqueue(message);
    }

    private void ShowMessage(QuickChatMessage message)
    {
        holder.SetActive(true);

        PlayerManager player = NetworkManager.Singleton.SpawnManager.SpawnedObjects[message.senderId].GetComponent<PlayerManager>();

        playerHeadUI.InitializeHead(PlayerClothesInfo.Deserialize(player.LobbyPlayer.Data["ClothingInfo"].Value));
        playerPokemonIcon.sprite = player.Pokemon.Portrait;
        messageText.text = message.message;

        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_Signal);

        StartCoroutine(HideMessage());
    }

    private void Update()
    {
        if (!isShowingMessage)
        {
            if (messageQueue.Count > 0)
            {
                ShowMessage(messageQueue.Dequeue());
            }
        }
    }

    private IEnumerator HideMessage()
    {
        isShowingMessage = true;
        yield return new WaitForSeconds(2.5f);
        holder.SetActive(false);
        isShowingMessage = false;
    }
}

public struct QuickChatMessage : INetworkSerializable
{
    public string message;
    public ulong senderId;
    public Team sendingTeam;

    public QuickChatMessage(string message, ulong senderId, Team sendingTeam)
    {
        this.message = message;
        this.senderId = senderId;
        this.sendingTeam = sendingTeam;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref message);
        serializer.SerializeValue(ref senderId);
        serializer.SerializeValue(ref sendingTeam);
    }
}