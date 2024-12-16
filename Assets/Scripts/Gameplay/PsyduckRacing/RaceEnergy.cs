using Unity.Netcode;
using UnityEngine;

public class RaceEnergy : NetworkBehaviour
{
    [SerializeField] private GameObject model;

    private float timer = 0;
    private bool isActive = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }
        isActive = false;
        timer = 20f;
        UpdateModelVisibilityRPC(false);
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (!isActive && timer > 0)
        {
            timer -= Time.deltaTime;

            if (timer <= 0)
            {
                isActive = true;
                UpdateModelVisibilityRPC(true);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (isActive && other.gameObject.CompareTag("Player"))
        {
            PlayerManager playerController = other.gameObject.GetComponent<PlayerManager>();
            ReduceCooldownRPC(playerController.NetworkObjectId, RpcTarget.Single(playerController.OwnerClientId, RpcTargetUse.Temp));
            isActive = false;
            timer = 20f;
            UpdateModelVisibilityRPC(false);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReduceCooldownRPC(ulong targetObject, RpcParams rpcParams = default)
    {
        PlayerManager playerController = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetObject].GetComponent<PlayerManager>();

        playerController.MovesController.ReduceMoveCooldown(MoveType.MoveA, 30f);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateModelVisibilityRPC(bool isVisible)
    {
        model.SetActive(isVisible);
    }
}
