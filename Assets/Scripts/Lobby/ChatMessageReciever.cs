using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static ChatManager;

public class ChatMessageReciever : NetworkBehaviour
{
    public event System.Action<ChatMessage> OnMessageRecieved;

    public override void OnNetworkSpawn()
    {
        NetworkObject.DestroyWithScene = true;
    }

    [Rpc(SendTo.Everyone)]
    public void RecieveMessageRPC(ChatMessage message)
    {
        OnMessageRecieved?.Invoke(message);
    }
}
