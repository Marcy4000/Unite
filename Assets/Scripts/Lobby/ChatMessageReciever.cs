using System.Collections.Generic;
using Unite.Multiplayer;
using Unity.Netcode;
using static ChatManager;

public class ChatMessageReciever : NetworkBehaviour
{
    public event System.Action<ChatMessage> OnMessageRecieved;

    public override void OnNetworkSpawn()
    {
        NetworkObject.DestroyWithScene = true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RecieveMessageRPC(ChatMessage message, RpcParams rpcParams = default)
    {
        if (!TryGetMessageTargets(LobbyController.Instance?.ChatLobby, message, rpcParams.Receive.SenderClientId,
            out var targetClientIds)) return;

        if (message.IsPrivate && targetClientIds.Exists(clientId => !NetworkManager.ConnectedClients.ContainsKey(clientId)))
            return;
        targetClientIds.RemoveAll(clientId => !NetworkManager.ConnectedClients.ContainsKey(clientId));
        if (targetClientIds.Count == 0) return;
        DeliverMessageRPC(message, RpcTarget.Group(targetClientIds, RpcTargetUse.Temp));
    }

    private static bool TryGetMessageTargets(GameSessionView lobby, ChatMessage message, ulong senderClientId,
        out List<ulong> targetClientIds)
    {
        targetClientIds = new List<ulong>();
        if (lobby == null || message.lobbyID != lobby.Id || string.IsNullOrWhiteSpace(message.message)) return false;
        var sender = lobby.Players.Find(player => player.Id == message.senderID);
        if (!TryGetClientId(sender, out var ownerClientId) || ownerClientId != senderClientId) return false;

        targetClientIds.Add(senderClientId);
        if (message.IsPrivate)
        {
            var recipient = lobby.Players.Find(player => player.Id == message.recipientID);
            if (message.recipientID == message.senderID || !TryGetClientId(recipient, out var recipientClientId) ||
                recipientClientId == senderClientId) return false;
            targetClientIds.Add(recipientClientId);
        }
        else
        {
            foreach (var player in lobby.Players)
                if (TryGetClientId(player, out var clientId) && !targetClientIds.Contains(clientId))
                    targetClientIds.Add(clientId);
        }
        return true;
    }

    private static bool TryGetClientId(GameSessionPlayer player, out ulong clientId)
    {
        clientId = 0;
        return player != null && !player.IsBot && player.Data.TryGetValue("OwnerID", out var owner) &&
            ulong.TryParse(owner.Value, out clientId);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void DeliverMessageRPC(ChatMessage message, RpcParams rpcParams = default)
    {
        ShowCityChatBubble(message);
        OnMessageRecieved?.Invoke(message);
    }

    private void ShowCityChatBubble(ChatMessage message)
    {
        var city = LobbyController.Instance?.OpenWorldLobby;
        if (message.IsPrivate || string.IsNullOrWhiteSpace(message.message) || city == null || !city.IsActive ||
            message.lobbyID != city.ChatLobby?.Id || !IsClient || NetworkManager.SpawnManager == null) return;

        foreach (var spawned in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            var avatar = spawned.GetComponent<OpenWorldTrainerAvatar>();
            if (avatar == null || !avatar.IsSpawned || avatar.PlayerId != message.senderID) continue;
            avatar.GetComponent<OpenWorldTrainerChatBubble>()?.Show(message.message);
            return;
        }
    }
}
