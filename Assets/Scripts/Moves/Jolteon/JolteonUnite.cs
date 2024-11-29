using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class JolteonUnite : MoveBase
{
    public float dashSpeed = 25f;
    public float dashDuration = 3.5f;
    public float bounceFactor = 0.8f; // Factor for bouncing off obstacles
    public float sphereCastRadius = 1f; // Radius for the sphere cast
    public float turningFactor = 5f; // Factor for smoothing out direction changes

    private float dashTimeRemaining;
    private bool isDashing;
    private Vector3 dashDirection;
    private Vector3 currentVelocity;

    private List<Pokemon> recentlyHitPokemon = new List<Pokemon>();
    private float invincibilityDuration = 0.7f; // Duration for which a Pokemon can't be hit again

    private GameObject trailObject;

    private string trailPath = "Assets/Prefabs/Objects/Moves/Jolteon/JolteonTrail.prefab";

    RaycastHit[] hits = new RaycastHit[15];

    private DamageInfo dashDamage = new DamageInfo(0, 1.8f, 6, 300, DamageType.Physical);
    private StatChange speedDebuff = new StatChange(20, Stat.Speed, 2f, true, false, true, 0);

    public JolteonUnite()
    {
        Name = "Lightning Blitz";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        dashDamage.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeDashAim(5f);
    }

    public override void Update()
    {
        if (isDashing)
        {
            DashUpdate();
        }

        if (!IsActive)
        {
            return;
        }

        dashDirection = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (IsActive && dashDirection.magnitude != 0)
        {
            StartDash();
        }
        Aim.Instance.HideDashAim();
        base.Finish();
    }

    void StartDash()
    {
        isDashing = true;
        dashTimeRemaining = dashDuration;
        playerManager.PlayerMovement.CanMove = false;

        playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 3.5f, true, 0));
        playerManager.AnimationManager.SetBool("Walking", true);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            trailObject = obj;
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(trailPath, playerManager.OwnerClientId);

        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        // If no direction is input, use forward direction
        if (dashDirection == Vector3.zero)
        {
            dashDirection = playerManager.transform.forward;
        }
    }

    void DashUpdate()
    {
        if (dashTimeRemaining > 0)
        {
            dashTimeRemaining -= Time.deltaTime;

            // Move Jolteon
            currentVelocity = dashDirection * dashSpeed;
            playerManager.PlayerMovement.CharacterController.Move(currentVelocity * Time.deltaTime);

            float adjustDirectionX = playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>().x;
            float adjustDirectionZ = playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>().y;
            Vector3 adjustDirection = new Vector3(adjustDirectionX, 0, adjustDirectionZ).normalized;
            if (adjustDirection != Vector3.zero)
            {
                dashDirection = Vector3.Lerp(dashDirection, adjustDirection, Time.deltaTime * turningFactor); // Smoothly adjust direction
            }

            if (trailObject != null)
            {
                trailObject.transform.position = playerManager.transform.position + new Vector3(0, 1f, 0);
            }

            playerManager.transform.rotation = Quaternion.LookRotation(dashDirection);

            // Check for collisions
            HandleCollisions();
        }
        else
        {
            // End dash
            isDashing = false;
            playerManager.PlayerMovement.CanMove = true;

            playerManager.Pokemon.AddStatChange(new StatChange(80, Stat.Speed, 6f, true, true, true, 0));
            playerManager.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.20f), 0, 0, 6f, true));

            playerManager.MovesController.DespawnNetworkObjectRPC(trailObject.GetComponent<NetworkObject>().NetworkObjectId);
            trailObject = null;

            JoltPassive passive = playerManager.PassiveController.Passive as JoltPassive;
            if (passive != null)
            {
                passive.UniteFillCharge(5f);
            }

            playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

            wasMoveSuccessful = true;
            Finish();
        }
    }

    void HandleCollisions()
    {
        if (dashDirection.sqrMagnitude > 0.001f)
        {
            dashDirection.Normalize();
        }
        int hitCount = Physics.SphereCastNonAlloc(playerManager.transform.position + new Vector3(0f, 1f, 0f), sphereCastRadius, dashDirection, hits, currentVelocity.magnitude * Time.deltaTime);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider.CompareTag("Wall"))
            {
                // Handle bouncing off obstacles
                Vector3 reflectDir = Vector3.Reflect(dashDirection, hit.normal);
                reflectDir = new Vector3(reflectDir.x, 0, reflectDir.z).normalized;
                dashDirection = reflectDir * bounceFactor;
                continue;
            }

            if (Aim.Instance.CanPokemonBeTargeted(hit.collider.gameObject, AimTarget.NonAlly, playerManager.CurrentTeam))
            {
                if (hit.collider.TryGetComponent(out Pokemon pokemon))
                {
                    if (recentlyHitPokemon.Contains(pokemon))
                    {
                        continue;
                    }

                    pokemon.TakeDamage(dashDamage);
                    pokemon.AddStatChange(speedDebuff);
                    recentlyHitPokemon.Add(pokemon);
                    playerManager.StartCoroutine(RemoveFromRecentlyHit(pokemon));
                }
            }
        }
    }

    private IEnumerator RemoveFromRecentlyHit(Pokemon pokemon)
    {
        yield return new WaitForSeconds(invincibilityDuration);
        recentlyHitPokemon.Remove(pokemon);
    }

    public override void ResetMove()
    {
        if (trailObject != null)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(trailObject.GetComponent<NetworkObject>().NetworkObjectId);
            trailObject = null;
        }
        isDashing = false;
        recentlyHitPokemon.Clear();

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
