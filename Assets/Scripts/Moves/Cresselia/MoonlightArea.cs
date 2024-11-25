using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MoonlightArea : NetworkBehaviour
{
    [SerializeField] CapsuleCollider capsuleCollider;

    private float healPercentage;

    private bool initialized;

    private float moveCooldown = 6f;
    private float healCooldown = 1f;

    private bool vaporeonTeam;

    private List<PlayerManager> playerList = new List<PlayerManager>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, bool orangeTeam, float healPercentage)
    {
        vaporeonTeam = orangeTeam;
        transform.position = position;
        capsuleCollider.enabled = true;
        this.healPercentage = healPercentage;
        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        moveCooldown -= Time.deltaTime;
        healCooldown -= Time.deltaTime;

        if (healCooldown <= 0)
        {
            foreach (PlayerManager player in playerList)
            {
                if (player != null)
                {
                    if (player.OrangeTeam == vaporeonTeam)
                    {
                        player.Pokemon.HealDamage(Mathf.FloorToInt(player.Pokemon.GetMaxHp() * healPercentage));
                    }
                }
            }

            healCooldown = 1f;
        }

        if (moveCooldown <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerManager player))
        {
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
            if (playerList.Contains(player))
            {
                playerList.Remove(player);
            }
        }
    }
}
