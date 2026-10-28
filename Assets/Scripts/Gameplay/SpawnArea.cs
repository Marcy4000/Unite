using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnArea : NetworkBehaviour
{
    private const float HEAL_INTERVAL = 0.5f;
    private const float HEAL_PERCENTAGE = 0.15f;

    [SerializeField] private Collider wallCollider;
    [SerializeField] private Team team;

    private float healTimer = 0f;

    private List<PlayerManager> playersInSpawn = new List<PlayerManager>();

    public void AllowControlledPlayerTraversal(PlayerManager player)
    {
        if (IsServer && player.IsOwner && player.CurrentTeam.Team == team && wallCollider != null)
            Physics.IgnoreCollision(player.PlayerMovement.CharacterController, wallCollider);
    }

    public override void OnNetworkSpawn()
    {
        Team localPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();

        // A host now simulates both teams. Keep both barriers in its physics
        // world, ignoring each controlled participant only against its own base.
        wallCollider.enabled = IsServer || team != localPlayerTeam;
        healTimer = HEAL_INTERVAL;
    }

    private void Update()
    {
        if (!IsServer || GameManager.Instance.GameState != GameState.Playing)
        {
            return;
        }

        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            foreach (var player in playersInSpawn)
            {
                if (player == null)
                {
                    continue;
                }

                if (player.Pokemon.CurrentHp < player.Pokemon.GetMaxHp())
                {
                    player.Pokemon.HealEnvironmentRPC(Mathf.FloorToInt(player.Pokemon.GetMaxHp() * HEAL_PERCENTAGE));
                }
            }

            healTimer = HEAL_INTERVAL;
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
            if (!playersInSpawn.Contains(player) && player.CurrentTeam.IsOnSameTeam(team))
            {
                playersInSpawn.Add(player);
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
            if (playersInSpawn.Contains(player))
            {
                playersInSpawn.Remove(player);
            }
        }
    }
}
