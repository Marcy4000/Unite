using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarshadowCloseCombat : MoveBase
{
    private int dashCount = 0;
    private bool isDashing;

    private float range = 4.5f;

    private Vector3 initialDashDirection;
    private Vector3 dashDirection;

    private DamageInfo normalDamage = new DamageInfo(0, 1.45f, 5, 320, DamageType.Physical);
    private DamageInfo finalDamage = new DamageInfo(0, 1.75f, 6, 400, DamageType.Physical);

    Collider[] hits = new Collider[15];
    private List<Pokemon> recentlyHitPokemon = new List<Pokemon>();

    private string[] layers = { "Players", "WildPokemons" };

    private Coroutine dashRoutine;

    public MarshadowCloseCombat()
    {
        Name = "Close Combat";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        normalDamage.attackerId = playerManager.NetworkObjectId;
        finalDamage.attackerId = playerManager.NetworkObjectId;
        if (!isDashing)
        {
            Aim.Instance.InitializeDashAim(range);
        }
    }

    public override void Update()
    {
        if (isDashing)
        {
            HandleCollisions();
        }

        if (!IsActive)
        {
            return;
        }

        if (!isDashing)
        {
            initialDashDirection = Aim.Instance.DashAim();
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            wasMoveSuccessful = true;
            Aim.Instance.HideDashAim();
            if (!isDashing)
            {
                dashRoutine = playerManager.StartCoroutine(DashRoutine());
            }
        }
        base.Finish();
    }

    override public void Cancel()
    {
        Aim.Instance.HideDashAim();
        base.Cancel();
    }

    private IEnumerator DashRoutine()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        dashDirection = initialDashDirection;
        playerManager.PlayerMovement.CanMove = false;

        isDashing = true;

        for (dashCount = 0; dashCount < 3; dashCount++)
        {
            recentlyHitPokemon.Clear();

            playerManager.AnimationManager.SetBool("Walking", true);

            playerManager.transform.rotation = Quaternion.LookRotation(dashDirection);
            yield return playerManager.transform.DOMove(playerManager.transform.position + dashDirection * range, 0.4f).WaitForCompletion();

            Vector2 playerInput = playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>();
            if (playerInput.magnitude > 0.1f)
            {
                dashDirection = new Vector3(playerInput.x, 0, playerInput.y);
            }
            else
            {
                dashDirection = playerManager.transform.forward;
            }

            yield return new WaitForSeconds(0.2f);
        }

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

        playerManager.PlayerMovement.CanMove = true;
        isDashing = false;
    }

    private void HandleCollisions()
    {
        if (dashDirection.sqrMagnitude > 0.001f)
        {
            dashDirection.Normalize();
        }

        LayerMask mask = LayerMask.GetMask(layers);
        int hitCount = Physics.OverlapSphereNonAlloc(playerManager.transform.position + new Vector3(0f, 1f, 0f), 1f, hits, mask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];

            if (hit.gameObject.TryGetComponent(out Pokemon pokemon))
            {
                if (recentlyHitPokemon.Contains(pokemon) || !Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, playerManager.CurrentTeam))
                {
                    continue;
                }

                recentlyHitPokemon.Add(pokemon);

                if (dashCount < 2)
                {
                    pokemon.TakeDamageRPC(normalDamage);
                }
                else
                {
                    pokemon.TakeDamageRPC(finalDamage);
                    pokemon.ApplyKnockupRPC(2f, 0.5f);
                    pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));
                }
            }
        }
    }

    public override void ResetMove()
    {
        if (dashRoutine != null)
        {
            playerManager.StopCoroutine(dashRoutine);
        }
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.transform.DOKill();
        isDashing = false;
    }
}
