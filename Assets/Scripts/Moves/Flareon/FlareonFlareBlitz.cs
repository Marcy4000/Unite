using System.Collections.Generic;
using UnityEngine;

public class FlareonFlareBlitz : MoveBase
{
    private DamageInfo hitDamage = new DamageInfo(0, 1.8f, 7, 410, DamageType.Physical);

    private Vector3 direction;
    private readonly float initialRange = 6f;
    private readonly float travelSpeed = 3f;

    private float range;

    private bool isRunning = false;
    private float travelDistance = 0f;

    private bool hitAnything = false;

    private List<GameObject> hitTargets = new List<GameObject>();

    public FlareonFlareBlitz()
    {
        Name = "Flare Blitz";
        Cooldown = 7.5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(initialRange);
        isRunning = false;
        hitAnything = false;
        hitDamage.attackerId = controller.NetworkObjectId;
    }

    public override void Update()
    {
        if (isRunning)
        {
            float speed = travelSpeed;

            playerManager.PlayerMovement.CharacterController.Move(speed * direction.normalized * (playerManager.Pokemon.GetSpeed() / 1000f) * Time.deltaTime);
            travelDistance += speed * playerManager.Pokemon.GetSpeed() / 1000f * Time.deltaTime;

            GameObject[] targets = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + new Vector3(0f, 0.6f, 0f), 0.75f, AimTarget.NonAlly);
            foreach (GameObject target in targets)
            {
                if (hitTargets.Contains(target))
                {
                    continue;
                }

                Pokemon pokemon = target.GetComponent<Pokemon>();
                if (pokemon != null)
                {
                    pokemon.TakeDamageRPC(hitDamage);
                    pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.35f, true, 0));
                    pokemon.AddStatusEffect(new StatusEffect(StatusType.Burned, 2f, true, 0));
                    pokemon.ApplyKnockupRPC(1.2f, 0.35f);

                    hitTargets.Add(target);
                    hitAnything = true;
                }
            }
        }

        float maxRange = range;

        if (isRunning && travelDistance >= maxRange)
        {
            isRunning = false;
            playerManager.PlayerMovement.CanMove = true;

            playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

            hitTargets.Clear();

            wasMoveSuccessful = true;
            Finish();

            if (hitAnything)
            {
                playerManager.MovesController.ReduceMoveCooldown(MoveType.MoveB, 3f);
            }
        }

        if (!IsActive)
        {
            return;
        }

        direction = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (direction.magnitude != 0 && IsActive && !isRunning)
        {
            playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
            playerManager.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

            range = initialRange;

            travelDistance = 0f;
            playerManager.PlayerMovement.CanMove = false;
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            playerManager.AnimationManager.SetBool("Walking", true);
            isRunning = true;
        }
        Aim.Instance.HideDashAim();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideDashAim();
        base.Cancel();
    }

    public override void ResetMove()
    {
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        isRunning = false;
        travelDistance = 0f;
        hitAnything = false;
    }
}
