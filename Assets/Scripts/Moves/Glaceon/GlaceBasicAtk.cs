using Unity.Netcode;
using UnityEngine;

public class GlaceBasicAtk : BasicAttackBase
{
    private string attackPrefab;
    private byte charge = 0;
    private byte basicAtkType = 0; // 0 = normal, 1 = Ice Shard, 2 = Icy Wind

    private float cooldown;

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;

    private GlaceonPassive glaceonPassive;

    public byte Charge { get => charge; set => charge = value; }

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 7f;
        attackPrefab = "Assets/Prefabs/Objects/BasicAtk/GlaceonBasicAtk.prefab";
        normalDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 0.39f, 4, 70, DamageType.Special);
        boostedDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 0.47f, 5, 90, DamageType.Special);
        glaceonPassive = playerManager.PassiveController.Passive as GlaceonPassive;
    }

    public override void Perform(bool wildPriority)
    {
        PokemonType priority = wildPriority ? PokemonType.Wild : PokemonType.Player;
        switch (basicAtkType)
        {
            case 0:
                GameObject closestEnemy = Aim.Instance.AimInCircle(range, priority);

                // If an enemy is found, launch a homing projectile towards it
                if (closestEnemy != null)
                {
                    DamageInfo damage = charge == 2 ? boostedDmg : normalDmg;

                    playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, damage, attackPrefab);
                    string animation = playerManager.Pokemon.CurrentLevel >= 3 ? $"ani_atk{charge + 1}_bat_0471" : $"ani_atk{charge + 4}_bat_0133";
                    playerManager.AnimationManager.PlayAnimation(animation);
                    playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown());
                    playerManager.transform.LookAt(closestEnemy.transform);
                    playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
                    cooldown = 4.5f;

                    if (charge == 2)
                    {
                        glaceonPassive.UpdateIciclesCount((byte)Mathf.Clamp(glaceonPassive.IciclesCount + 2, 0, 8));
                    }

                    charge++;
                }

                if (charge > 2)
                {
                    charge = 0;
                }
                break;
            case 1:
                closestEnemy = Aim.Instance.AimInCircle(range, priority);

                if (closestEnemy != null)
                {
                    playerManager.AnimationManager.PlayAnimation("ani_spell1b_bat_0471");
                    playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown());
                    playerManager.transform.LookAt(closestEnemy.transform);
                    playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
                }
                break;
            default:
                break;
        }
    }

    public void ChangeBasicAtkType(byte type)
    {
        if (type > 2)
        {
            return;
        }

        basicAtkType = type;
    }

    public void FillChargeAmount()
    {
        cooldown = 4.5f;
        charge = 2;
    }

    public override void Update()
    {
        if (cooldown > 0)
        {
            cooldown -= Time.deltaTime;
        }

        if (cooldown <= 0 && charge > 0)
        {
            charge = 0;
        }
    }
}
