using System.Collections.Generic;
using UnityEngine;

public class JoltDischarge : MoveBase
{
    private DamageInfo hitDamage = new DamageInfo(0, 1.6f, 6, 300, DamageType.Physical);
    private DamageInfo areaDamage = new DamageInfo(0, 1.3f, 3, 150, DamageType.Physical);

    private Vector3 direction;
    private float initialRange;
    private float initialBoostedRange;

    private float range;

    private bool isRunning = false;
    private float travelDistance = 0f;

    private float travelSpeed = 2.5f;
    private float secondSpeed = 3.5f;

    private float secondUseCd = 3f;

    private bool hitAnything = false;
    private bool secondUse;

    DischargeHitbox dischargeHitbox;

    private List<GameObject> hitTargets = new List<GameObject>();

    private string prefabPath = "Assets/Prefabs/Objects/Moves/Jolteon/JolteonDischarge.prefab";

    public JoltDischarge()
    {
        Name = "Discharge";
        Cooldown = 11.0f;
        initialRange = 8f;
        initialBoostedRange = 10f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(initialRange);
        isRunning = false;
        hitAnything = false;
        hitDamage.attackerId = controller.NetworkObjectId;
        areaDamage.attackerId = controller.NetworkObjectId;
    }

    public override void Update()
    {
        if (isRunning)
        {
            float speed = secondUse ? secondSpeed : travelSpeed;

            playerManager.PlayerMovement.CharacterController.Move(speed * direction.normalized * (playerManager.Pokemon.GetSpeed()/1000f) * Time.deltaTime);
            travelDistance += speed * playerManager.Pokemon.GetSpeed()/1000f * Time.deltaTime;

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
                    pokemon.TakeDamage(hitDamage);
                    pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.35f, true, 0));

                    if (secondUse)
                    {
                        pokemon.AddStatChange(new StatChange(20, Stat.Speed, 2.5f, true, false, true, 0));
                    }

                    hitTargets.Add(target);
                    hitAnything = true;
                }
            }
        }

        float maxRange = secondUse ? range * 0.7f : range;

        if (secondUse && secondUseCd > 0)
        {
            secondUseCd -= Time.deltaTime;
        }

        if (secondUseCd <= 0 && secondUse && !isRunning)
        {
            secondUse = false;
            wasMoveSuccessful = true;
            secondUse = false;
            Finish();
        }

        if (isRunning && travelDistance >= maxRange)
        {
            if (dischargeHitbox != null)
            {
                dischargeHitbox.DespawnRPC();
                dischargeHitbox = null;
            }
            isRunning = false;
            playerManager.PlayerMovement.CanMove = true;

            playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

            hitTargets.Clear();

            if (!hitAnything || secondUse)
            {
                wasMoveSuccessful = true;
                secondUse = false;
                Finish();
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
            }
            else if (!secondUse)
            {
                secondUse = true;
                secondUseCd = 3f;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, secondUseCd);
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
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                dischargeHitbox = obj.GetComponent<DischargeHitbox>();
                dischargeHitbox.InitializeDischargeHitboxRpc(playerManager.transform.position, areaDamage, playerManager.NetworkObjectId);
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(prefabPath, playerManager.OwnerClientId);

            playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
            playerManager.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

            JoltPassive joltPassive = playerManager.PassiveController.Passive as JoltPassive;
            if (joltPassive.IsPassiveReady)
            {
                joltPassive.ResetPassiveCharge();
                range = initialBoostedRange;
            }
            else
            {
                range = initialRange;
            }

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
}
