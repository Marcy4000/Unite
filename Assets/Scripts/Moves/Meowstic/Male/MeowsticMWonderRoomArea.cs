using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MeowsticMWonderRoomArea : NetworkBehaviour
{
    private bool orangeTeam;

    private bool initialized = false;

    private float activeTime = 6f;

    private List<PlayerManager> playersInArea = new List<PlayerManager>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, Vector3 playerPos, bool orangeTeam)
    {
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(playerPos - position, Vector3.up);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        this.orangeTeam = orangeTeam;

        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        activeTime -= Time.deltaTime;

        if (activeTime <= 0)
        {
            foreach (PlayerManager player in playersInArea)
            {
                if (player != null)
                {
                    if (player.OrangeTeam != orangeTeam)
                    {
                        player.Pokemon.FlipAtkStatsRPC(false);
                    }
                    else
                    {
                        player.Pokemon.RemoveStatChangeWithIDRPC(15);
                        player.Pokemon.RemoveStatChangeWithIDRPC(16);
                    }
                }
            }
            NetworkObject.Despawn(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        PlayerManager player = other.GetComponent<PlayerManager>();

        if (player != null && !playersInArea.Contains(player))
        {
            playersInArea.Add(player);
            if (player.OrangeTeam != orangeTeam)
            {
                // This is stupid
                player.Pokemon.FlipAtkStatsRPC(true);
            }
            else
            {
                player.Pokemon.AddStatChange(new StatChange(15, Stat.Defense, 0f, false, true, true, 15));
                player.Pokemon.AddStatChange(new StatChange(15, Stat.SpDefense, 0f, false, true, true, 16));
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        PlayerManager player = other.GetComponent<PlayerManager>();

        if (player != null && playersInArea.Contains(player))
        {
            if (player.OrangeTeam != orangeTeam)
            {
                player.Pokemon.FlipAtkStatsRPC(false);
            }
            else
            {
                player.Pokemon.RemoveStatChangeWithIDRPC(15);
                player.Pokemon.RemoveStatChangeWithIDRPC(16);
            }
            playersInArea.Remove(player);
        }
    }
}
