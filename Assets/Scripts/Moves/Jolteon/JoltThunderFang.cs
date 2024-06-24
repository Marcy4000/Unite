using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoltThunderFang : MoveBase
{
    private DamageInfo biteDamage = new DamageInfo(0, 1.72f, 9, 300, DamageType.Physical);
    private DamageInfo biteDamageBoosted = new DamageInfo(0, 1.9f, 10, 350, DamageType.Physical);
    private StatusEffect stun = new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0);
    private StatChange speedBoost = new StatChange(60, Stat.Speed, 1.5f, true, true, true, 0);

    private float distance = 4f;
    private Vector3 direction;

    private float secondUseCd;

    private bool isMoving = false;
    private bool hitAnything = false;
    private bool passiveBoost;

    private JoltPassive joltPassive;

    private List<GameObject> hitTargets = new List<GameObject>();

    public JoltThunderFang()
    {
        Name = "Thunder Fang";
        Cooldown = 7.0f;
    }

    override public void Start(PlayerManager controller)
    {
        base.Start(controller);
        biteDamage.attackerId = playerManager.NetworkObjectId;
        biteDamageBoosted.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeDashAim(distance);
    }

    public override void Update()
    {
        if (isMoving)
        {
            GameObject[] targets = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (playerManager.transform.forward * 1.3f), 1f, AimTarget.NonAlly);

            foreach (var target in targets)
            {
                if (hitTargets.Contains(target))
                {
                    continue;
                }

                Pokemon targetPokemon = target.GetComponent<Pokemon>();

                if (targetPokemon == null)
                {
                    continue;
                }

                DamageInfo damageInfo = passiveBoost ? biteDamageBoosted : biteDamage;

                targetPokemon.TakeDamage(damageInfo);
                targetPokemon.AddStatusEffect(stun);
                hitTargets.Add(target);
                hitAnything = true;
            }
        }

        if (secondUseCd > 0 && passiveBoost)
        {
            secondUseCd -= Time.deltaTime;
        }

        if (secondUseCd < 0 && passiveBoost) {
            wasMoveSuccessful = true;
            BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
            Finish();
            passiveBoost = false;
        }

        if (!IsActive)
        {
            return;
        }

        direction = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        joltPassive = playerManager.PassiveController.Passive as JoltPassive;

        if (direction.magnitude != 0 && IsActive)
        {
            if (joltPassive.IsPassiveReady)
            {
                joltPassive.ResetPassiveCharge();
                secondUseCd = 3.875f;
                passiveBoost = true;
                wasMoveSuccessful = false;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, secondUseCd);
            }
            else
            {
                passiveBoost = false;
                wasMoveSuccessful = true;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
            }

            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            playerManager.StartCoroutine(BiteRoutine(1));
        }
        Aim.Instance.HideDashAim();
        base.Finish();
    }

    private IEnumerator BiteRoutine(int biteAmounts)
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.PlayAnimation($"pm0135_00_ba20_thunderFang");

        hitTargets.Clear();
        hitAnything = false;

        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(0.675f);
        isMoving = true;
        var movement = playerManager.transform.DOJump(playerManager.transform.position + (direction.normalized * distance), 0.5f, 1, 0.2f).onComplete += () =>
        {
            if (hitAnything)
            {
                playerManager.Pokemon.AddStatChange(speedBoost);
            }
            playerManager.PlayerMovement.CanMove = true;

            playerManager.PlayerMovement.CanMove = true;
            playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

            isMoving = false;
        };
    }

    public override void Cancel()
    {
        Aim.Instance.HideDashAim();
        base.Cancel();
    }
}
