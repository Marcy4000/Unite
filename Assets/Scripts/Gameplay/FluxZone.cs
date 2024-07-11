using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class FluxZone : NetworkBehaviour
{
    [SerializeField] private bool orangeTeam;
    [SerializeField] private int laneID;
    [SerializeField] private int tier;
    [SerializeField] private GameObject graphic;

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>();
    private List<PlayerManager> playerManagerList = new List<PlayerManager>();

    public bool OrangeTeam => orangeTeam;
    public int LaneID => laneID;
    public int Tier => tier;
    public bool IsActive => isActive.Value;

    public override void OnNetworkSpawn()
    {
        isActive.OnValueChanged += OnActiveValueChanged;
        if (IsServer)
        {
            isActive.Value = true;
        }
    }

    public void OnActiveValueChanged(bool oldValue, bool newValue)
    {
        graphic.SetActive(newValue);
        if (!newValue)
        {
            foreach (var player in playerManagerList)
            {
                player.Pokemon.RemoveStatChangeWithID(1);
            }
            playerManagerList.Clear();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsActive || !IsServer)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManagerList.Add(playerManager);
            int value = (playerManager.OrangeTeam == orangeTeam) ? 60 : 50;
            StatChange statChange = new StatChange((short)value, Stat.Speed, 0, false, playerManager.OrangeTeam == orangeTeam, true, 1);
            playerManager.Pokemon.AddStatChange(statChange);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsActive || !IsServer)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManagerList.Remove(playerManager);
            playerManager.Pokemon.RemoveStatChangeWithID(1);
        }
    }

    public void SetIsActive(bool value)
    {
        if (IsServer)
        {
            isActive.Value = value;
        }
        else
        {
            SetIsActiveRPC(value);
        }
    }

    [Rpc(SendTo.Server)]
    private void SetIsActiveRPC(bool value)
    {
        isActive.Value = value;
    }
}
