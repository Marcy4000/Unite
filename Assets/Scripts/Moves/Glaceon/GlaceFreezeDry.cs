using UnityEngine;

public class GlaceFreezeDry : MoveBase
{
    private DamageInfo damageInfo;
    private float distance;
    private Vector3 direction;

    private StatChange atkBuff = new StatChange(50, Stat.SpAttack, 3f, true, true, true, 0);

    private GlaceonPassive glaceonPassive;
    private FreezeDryProjectile freezeDryProjectile;

    public GlaceFreezeDry()
    {
        Name = "Freeze Dry";
        Cooldown = 8.5f;
        distance = 6f;
        damageInfo = new DamageInfo(0, 0.65f, 5, 210, DamageType.Special);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        glaceonPassive = playerManager.PassiveController.Passive as GlaceonPassive;
        Aim.Instance.InitializeSkillshotAim(distance);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        direction = Aim.Instance.SkillshotAim();
    }

    public override void Finish()
    {
        if (direction.magnitude != 0 && IsActive)
        {
            Vector2 direction = new Vector2(this.direction.x, this.direction.z);
            playerManager.MovesController.onObjectSpawned += (freezeDryProjectile) =>
            {
                this.freezeDryProjectile = freezeDryProjectile.GetComponent<FreezeDryProjectile>();
                this.freezeDryProjectile.SetDirection(playerManager.transform.position, direction, damageInfo, distance);
                this.freezeDryProjectile.OnMoveHit += OnProjectileHit;
                Debug.Log("Freeze dry projectile spawned!");
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC("Assets/Prefabs/Objects/Moves/Glaceon/FreezeDryProjectile.prefab", playerManager.OwnerClientId);
            playerManager.StopMovementForTime(0.35f);
            playerManager.AnimationManager.PlayAnimation($"ani_spell1b_bat_0471");
            playerManager.transform.rotation = Quaternion.LookRotation(this.direction);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    private void OnProjectileHit(int enemiesHit)
    {
        if (enemiesHit > 0)
        {
            playerManager.Pokemon.AddStatChange(atkBuff);
        }

        for (int i = 0; i < enemiesHit; i++)
        {
            glaceonPassive.UpdateIciclesCount((byte)Mathf.Clamp(glaceonPassive.IciclesCount + 2, 0, 8));
        }
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSkillshotAim();
    }
}
