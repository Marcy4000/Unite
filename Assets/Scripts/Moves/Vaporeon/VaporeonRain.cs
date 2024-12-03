using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class VaporeonRain : NetworkBehaviour
{
    [SerializeField] CapsuleCollider capsuleCollider;

    private StatChange defBuff = new StatChange(55, Stat.Defense, 0f, false, true, true, 6);
    private StatChange spDefBuff = new StatChange(55, Stat.SpDefense, 0f, false, true, true, 7);

    private StatChange speedDebuff = new StatChange(20, Stat.Speed, 0f, false, false, true, 8);

    private DamageInfo allyHeal;

    private bool initialized;

    private float rainCooldown = 10f;
    private float healCooldown = 1.2f;

    private Team vaporeonTeam;

    private List<PlayerManager> playerList = new List<PlayerManager>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, Team orangeTeam, DamageInfo heal)
    {
        vaporeonTeam = orangeTeam;
        transform.position = position;
        capsuleCollider.enabled = true;
        allyHeal = heal;
        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        rainCooldown -= Time.deltaTime;
        healCooldown -= Time.deltaTime;

        if (healCooldown <= 0)
        {
            foreach (PlayerManager player in playerList)
            {
                if (player != null)
                {
                    if (player.CurrentTeam.IsOnSameTeam(vaporeonTeam))
                    {
                        player.Pokemon.HealDamageRPC(allyHeal);
                    }
                }
            }

            healCooldown = 1.2f;
        }

        if (rainCooldown <= 0)
        {
            Despawn();
        }
    }

    private void Despawn()
    {
        foreach (PlayerManager player in playerList)
        {
            if (player != null)
            {
                if (player.CurrentTeam.IsOnSameTeam(vaporeonTeam))
                {
                    player.Pokemon.RemoveStatChangeWithIDRPC(defBuff.ID);
                    player.Pokemon.RemoveStatChangeWithIDRPC(spDefBuff.ID);
                }
                else
                {
                    player.Pokemon.RemoveStatChangeWithIDRPC(speedDebuff.ID);
                }
            }
        }

        NetworkObject.Despawn(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerManager player))
        {
            if (player.CurrentTeam.IsOnSameTeam(vaporeonTeam))
            {
                player.Pokemon.AddStatChange(defBuff);
                player.Pokemon.AddStatChange(spDefBuff);
            }
            else
            {
                player.Pokemon.AddStatChange(speedDebuff);
            }

            if (!playerList.Contains(player))
            {
                playerList.Add(player);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerManager player))
        {
            if (player.CurrentTeam.IsOnSameTeam(vaporeonTeam))
            {
                player.Pokemon.RemoveStatChangeWithIDRPC(defBuff.ID);
                player.Pokemon.RemoveStatChangeWithIDRPC(spDefBuff.ID);
            }
            else
            {
                player.Pokemon.RemoveStatChangeWithIDRPC(speedDebuff.ID);
            }

            if (playerList.Contains(player))
            {
                playerList.Remove(player);
            }
        }
    }
}
