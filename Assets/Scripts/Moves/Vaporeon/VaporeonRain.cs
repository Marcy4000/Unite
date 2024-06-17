using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
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

    private bool vaporeonTeam;

    private List<PlayerManager> playerList = new List<PlayerManager>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, bool orangeTeam, DamageInfo heal)
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
                    if (player.OrangeTeam == vaporeonTeam)
                    {
                        player.Pokemon.HealDamage(allyHeal);
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
                if (player.OrangeTeam == vaporeonTeam)
                {
                    player.Pokemon.RemoveStatChangeWithID(defBuff.ID);
                    player.Pokemon.RemoveStatChangeWithID(spDefBuff.ID);
                }
                else
                {
                    player.Pokemon.RemoveStatChangeWithID(speedDebuff.ID);
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
            if (player.OrangeTeam == vaporeonTeam)
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
            if (player.OrangeTeam == vaporeonTeam)
            {
                player.Pokemon.RemoveStatChangeWithID(defBuff.ID);
                player.Pokemon.RemoveStatChangeWithID(spDefBuff.ID);
            }
            else
            {
                player.Pokemon.RemoveStatChangeWithID(speedDebuff.ID);
            }

            if (playerList.Contains(player))
            {
                playerList.Remove(player);
            }
        }
    }
}
