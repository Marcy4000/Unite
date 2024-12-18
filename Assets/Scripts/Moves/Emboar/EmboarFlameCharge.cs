using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EmboarFlameCharge : MoveBase
{
    private float[] dashSpeed = { 8f, 10f, 13f };
    private float dashDuration = 2.5f;
    private float dashLevelTick = 0.4f;
    private float sphereCastRadius = 1f; // Radius for the sphere cast
    private float turningFactor = 5f; // Factor for smoothing out direction changes

    private float dashTimeRemaining;
    private float dashLevelTickTimeRemaining;
    private bool isDashing;
    private Vector3 dashDirection;
    private Vector3 currentVelocity;

    private List<Pokemon> recentlyHitPokemon = new List<Pokemon>();
    private float invincibilityDuration = 0.7f; // Duration for which a Pokemon can't be hit again

    private GameObject trailObject;

    private string trailPath = "Assets/Prefabs/Objects/Moves/Emboar/EmboarTrail.prefab";

    RaycastHit[] hits = new RaycastHit[15];

    private DamageInfo dashDamage = new DamageInfo(0, 1.8f, 6, 300, DamageType.Physical);
    private StatChange speedDebuff = new StatChange(20, Stat.Speed, 2f, true, false, true, 0);
    private StatChange speedBuff = new StatChange(20, Stat.Speed, 2f, true, true, true, 0);

    private int dashLevel = 0;

    private EmboarPassive passive;

    public EmboarFlameCharge()
    {
        Name = "Flame Charge";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        dashDamage.attackerId = playerManager.NetworkObjectId;
        passive = playerManager.PassiveController.Passive as EmboarPassive;
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
        dashLevelTickTimeRemaining = dashLevelTick;
        dashLevel = passive.IsRecklessActive ? 2 : 0;
        playerManager.PlayerMovement.CanMove = false;

        playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.HindranceResistance, dashDuration, true, 0));
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

            if (dashLevelTickTimeRemaining > 0)
            {
                dashLevelTickTimeRemaining -= Time.deltaTime;
            }
            else if (dashLevel < 2)
            {
                dashLevel++;
                dashLevelTickTimeRemaining = dashLevelTick;
            }

            // Move Jolteon
            currentVelocity = dashDirection * dashSpeed[dashLevel];
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

            playerManager.MovesController.DespawnNetworkObjectRPC(trailObject.GetComponent<NetworkObject>().NetworkObjectId);
            trailObject = null;

            playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

            if (IsUpgraded)
            {
                playerManager.Pokemon.AddStatChange(speedBuff);
            }

            wasMoveSuccessful = true;
            Finish();
        }
    }

    void HandleCollisions()
    {
        if (dashLevel < 2)
        {
            return;
        }

        if (dashDirection.sqrMagnitude > 0.001f)
        {
            dashDirection.Normalize();
        }
        int hitCount = Physics.SphereCastNonAlloc(playerManager.transform.position + new Vector3(0f, 1f, 0f), sphereCastRadius, dashDirection, hits, currentVelocity.magnitude * Time.deltaTime);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider.TryGetComponent(out Pokemon pokemon))
            {
                if (!Aim.Instance.CanPokemonBeTargeted(hit.collider.gameObject, AimTarget.NonAlly, playerManager.CurrentTeam) || recentlyHitPokemon.Contains(pokemon))
                {
                    continue;
                }

                pokemon.TakeDamageRPC(dashDamage);
                pokemon.AddStatChange(speedDebuff);
                pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.7f, true, 0));
                pokemon.ApplyKnockupRPC(1.5f, 0.7f);
                recentlyHitPokemon.Add(pokemon);
                playerManager.StartCoroutine(RemoveFromRecentlyHit(pokemon));
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
