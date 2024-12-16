using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RaceItemBox : NetworkBehaviour
{
    [SerializeField] private GameObject model;
    [SerializeField] private List<RaceItemsSelection> items = new List<RaceItemsSelection>();

    private float timer = 0;
    private bool isActive = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }
        isActive = true;
        UpdateModelVisibilityRPC(true);
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
            RaceLapCounter lapCounter = RaceManager.Instance.PlayerLapCounters[playerController.NetworkObjectId];

            RaceItemsSelection selection = items[lapCounter.CurrentPlace - 1];

            LearnMoveRPC(lapCounter.CurrentPlace - 1, Random.Range(0, selection.moves.Count), playerController.NetworkObjectId, RpcTarget.Single(playerController.OwnerClientId, RpcTargetUse.Temp));

            isActive = false;
            timer = 2f;
            UpdateModelVisibilityRPC(false);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void LearnMoveRPC(int index1, int index2, ulong targetObject, RpcParams rpcParams = default)
    {
        PlayerManager playerController = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetObject].GetComponent<PlayerManager>();

        playerController.MovesController.LearnMove(items[index1].moves[index2]);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateModelVisibilityRPC(bool isVisible)
    {
        model.SetActive(isVisible);
    }
}

[System.Serializable]
public class RaceItemsSelection
{
    public List<MoveAsset> moves;
}