using Unity.Netcode;
using UnityEngine;

public class SpawnArea : NetworkBehaviour
{
    private const float HEAL_INTERVAL = 0.5f;
    private const float HEAL_PERCENTAGE = 0.15f;

    [SerializeField] private Collider wallCollider;
    [SerializeField] private Team team;

    private float healTimer = 0f;

    private Collider healingCollider;

    public void AllowControlledPlayerTraversal(PlayerManager player)
    {
        if (IsServer && player != null && player.IsOwner && player.CurrentTeam.Team == team
            && wallCollider != null && player.PlayerMovement != null && player.PlayerMovement.CharacterController != null)
            Physics.IgnoreCollision(player.PlayerMovement.CharacterController, wallCollider);
    }

    public override void OnNetworkSpawn()
    {
        healingCollider = GetComponent<Collider>();
        if (wallCollider != null)
            wallCollider.enabled = IsServer || LobbyController.Instance == null
                || team != LobbyController.Instance.GetLocalPlayerTeam();
        if (IsServer && GameManager.Instance != null)
            foreach (var player in GameManager.Instance.Players) AllowControlledPlayerTraversal(player);
        healTimer = HEAL_INTERVAL;
    }

    private void Update()
    {
        if (!IsServer || GameManager.Instance == null || GameManager.Instance.GameState != GameState.Playing)
        {
            return;
        }

        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            foreach (var player in GameManager.Instance.Players)
            {
                if (CanHealPlayer(player))
                {
                    player.Pokemon.HealEnvironmentRPC(Mathf.FloorToInt(player.Pokemon.GetMaxHp() * HEAL_PERCENTAGE));
                }
            }

            healTimer = HEAL_INTERVAL;
        }
    }

    private bool CanHealPlayer(PlayerManager player)
    {
        return healingCollider != null && healingCollider.enabled && healingCollider.isTrigger
            && healingCollider.gameObject.activeInHierarchy && player != null && player.IsSpawned
            && player.Pokemon != null && player.Pokemon.IsSpawned && player.Pokemon.BaseStats != null
            && player.CurrentTeam.IsOnSameTeam(team) && player.PlayerState != PlayerState.Dead
            && player.Pokemon.CurrentHp > 0 && player.Pokemon.CurrentHp < player.Pokemon.GetMaxHp()
            && healingCollider.ClosestPoint(player.transform.position) == player.transform.position;
    }
}
