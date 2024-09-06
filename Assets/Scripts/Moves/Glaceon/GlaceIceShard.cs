using Unity.Netcode;
using UnityEngine;

public class GlaceIceShard : MoveBase
{
    private string attackPrefab;

    private DamageInfo damageInfo;
    private StatChange movementBuff;
    private StatChange atkSpeedBuff;
    private StatChange atkSpeedBuffUpgrade;

    private float range = 7f;

    private float timer = 0f;
    private bool subscribed = false;
    private bool isActivated = false;

    private GlaceonPassive glaceonPassive;
    private GlaceBasicAtk basicAtk;

    public GlaceIceShard()
    {
        Name = "Ice Shard";
        Cooldown = 8.5f;
        damageInfo = new DamageInfo(0, 0.52f, 6, 100, DamageType.Special, DamageProprieties.CanCrit);
        attackPrefab = "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab";
        movementBuff = new StatChange(40, Stat.Speed, 0.5f, true, true, true, 0);
        atkSpeedBuff = new StatChange(60, Stat.AtkSpeed, 2.5f, true, true, true, 0);
        atkSpeedBuffUpgrade = new StatChange(100, Stat.AtkSpeed, 2.5f, true, true, true, 0);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        glaceonPassive = playerManager.PassiveController.Passive as GlaceonPassive;
        basicAtk = playerManager.MovesController.BasicAttack as GlaceBasicAtk;
        if (!subscribed)
        {
            playerManager.MovesController.onBasicAttackPerformed += OnBasicAttack;
            subscribed = true;
        }
    }

    public override void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }

        if (timer <= 0 && isActivated)
        {
            isActivated = false;
            basicAtk.ChangeBasicAtkType(0);
        }
    }

    public override void Finish()
    {
        if (!IsActive)
        {
            return;
        }

        wasMoveSuccessful = true;
        isActivated = true;
        timer = 2.5f;

        basicAtk.ChangeBasicAtkType(1);

        playerManager.Pokemon.AddStatChange(movementBuff);
        playerManager.Pokemon.AddStatChange(IsUpgraded ? atkSpeedBuffUpgrade : atkSpeedBuff);

        playerManager.AnimationManager.PlayAnimation("ani_spell2a_bat_0471");

        base.Finish();
    }

    private void OnBasicAttack()
    {
        if (!isActivated)
        {
            return;
        }

        GameObject closestEnemy = Aim.Instance.AimInCircle(range);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, damageInfo, attackPrefab);
            glaceonPassive.UpdateIciclesCount((byte)Mathf.Clamp(glaceonPassive.IciclesCount + 2, 0, 8));
        }
    }

    public override void ResetMove()
    {
        if (isActivated)
        {
            timer = 0;
        }
    }
}
