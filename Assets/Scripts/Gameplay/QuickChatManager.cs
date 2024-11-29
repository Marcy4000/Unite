using Unity.Netcode;
using UnityEngine;

public class QuickChatManager : NetworkBehaviour
{
    public static QuickChatManager Instance { get; private set; }

    [SerializeField] private QuickChatUI quickChatUI;

    private void Awake()
    {
        Instance = this;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SendQuickChatMessageRPC(QuickChatMessage message)
    {
        if (message.sendingTeam != LobbyController.Instance.GetLocalPlayerTeam())
            return;

        quickChatUI.EnqueueMessage(message);
    }
}
