using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class SafeguardIndicator : NetworkBehaviour
{
    [SerializeField] private GameObject cylinder;
    private GameObject target;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong targetID)
    {
        target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].gameObject;
        PlayAnimationRPC();
    }

    private void Update()
    {
        if (target != null && IsServer)
        {
            transform.position = target.transform.position;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayAnimationRPC()
    {
        cylinder.transform.DOLocalMoveY(0, 0.75f).onComplete += () =>
        {
            if (IsServer)
            {
                NetworkObject.Despawn(transform);
            }
        };
    }
}
