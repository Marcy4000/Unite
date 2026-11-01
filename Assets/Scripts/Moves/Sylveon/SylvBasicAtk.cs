using UnityEngine;

public class SylvBasicAtk : ChargedBasicAttackBase
{
    private byte charge = 0;
    private float cooldown = float.PositiveInfinity;
    private SylveonCharacterRuntime Character => playerManager?.MovesController?.EnsureSylveonCombatRuntime()?.Character as SylveonCharacterRuntime;
    protected override int AttackCharge => charge;
    public override bool ServerResolved => true;

    public byte Charge
    {
        get => playerManager != null && playerManager.IsServer ? Character?.Charge ?? charge : charge;
        set
        {
            charge = (byte)Mathf.Clamp(value, 0, 2);
            if (value == 2 && playerManager != null && playerManager.IsServer) Character?.FillCharge();
        }
    }

    public override void Initialize(PlayerManager manager)
    {
        range = SylvMoveRules.BasicRange;
        if (manager?.MovesController?.EnsureSylveonCombatRuntime()?.Character is SylveonCharacterRuntime runtime)
            charge = runtime.Charge;
        base.Initialize(manager);
    }

    public override void InitializeServer(PlayerManager manager)
    {
        base.InitializeServer(manager);
        range = SylvMoveRules.BasicRange;
        manager?.MovesController?.EnsureSylveonCombatRuntime();
    }

    public override void Perform(bool wildPriority)
    {
        GameObject closestEnemy = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (closestEnemy != null && closestEnemy.TryGetComponent(out Pokemon pokemon))
        {
            playerManager.MovesController?.EnsureSylveonCombatRuntime();
            playerManager.MovesController.RequestServerBasicAttack(pokemon.NetworkObjectId);
        }
    }

    public override void PerformOnServer(Pokemon target) => Character?.PerformBasicAttack(target);

    public override void ApplyServerState(int value, float empoweredUntil) => charge = (byte)Mathf.Clamp(value, 0, 2);

    public void ApplySnapshot(byte value)
    {
        charge = (byte)Mathf.Clamp(value, 0, 2);
        cooldown = float.PositiveInfinity;
    }

    public void FillChargeAmount()
    {
        if (playerManager != null && playerManager.IsServer) Character?.FillCharge();
    }

    public override void ResetAttack()
    {
        if (playerManager != null && playerManager.IsServer) Character?.ResetBasicCharge();
        charge = 0;
        cooldown = float.PositiveInfinity;
        base.ResetAttack();
    }

    public override void Update()
    {
        if (Character == null && !float.IsPositiveInfinity(cooldown))
        {
            if (cooldown > 0f) cooldown -= Time.deltaTime;
            if (cooldown <= 0f && charge > 0)
            {
                charge = 0;
                cooldown = float.PositiveInfinity;
            }
        }
        base.Update();
    }
}
