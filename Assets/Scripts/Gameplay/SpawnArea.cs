using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnArea : NetworkBehaviour
{
    private const float HEAL_INTERVAL = 0.5f;
    private const float HEAL_PERCENTAGE = 0.15f;

    [SerializeField] private Collider wallCollider;
    [SerializeField] private bool orangeTeam;

    private float healTimer = 0f;

    private List<PlayerManager> playersInSpawn = new List<PlayerManager>();

    public override void OnNetworkSpawn()
    {
        bool localPlayerTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";

        wallCollider.enabled = orangeTeam != localPlayerTeam;
        healTimer = HEAL_INTERVAL;
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            foreach (var player in playersInSpawn)
            {
                if (player.Pokemon.CurrentHp < player.Pokemon.GetMaxHp())
                {
                    player.Pokemon.HealDamage(Mathf.FloorToInt(player.Pokemon.GetMaxHp() * HEAL_PERCENTAGE));
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
            if (!playersInSpawn.Contains(player) && player.OrangeTeam == orangeTeam)
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
