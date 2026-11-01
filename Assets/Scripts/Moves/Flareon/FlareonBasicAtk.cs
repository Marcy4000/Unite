using UnityEngine;

public class FlareonBasicAtk : ChargedBasicAttackBase
{
    private int charge;
    private FlareonCharacterRuntime Character => playerManager?.MovesController?.FlareonRuntime;
    protected override int AttackCharge => charge;
    public override bool ServerResolved => true;

    public override void Initialize(PlayerManager manager)
    {
        range = FlareonMoveRules.BasicRange;
        base.Initialize(manager);
        ApplySnapshot(Character?.AttackCharge ?? 0);
    }

    public override void InitializeServer(PlayerManager manager)
    {
        base.InitializeServer(manager);
        range = FlareonMoveRules.BasicRange;
    }

    public override void Perform(bool wildPriority)
    {
        GameObject target = ActorAim?.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null && target.TryGetComponent(out Pokemon pokemon))
            playerManager.MovesController.RequestServerBasicAttack(pokemon.NetworkObjectId);
    }

    public override void PerformOnServer(Pokemon target) => Character?.PerformBasicAttack(target);
    public override void ApplyServerState(int value, float empoweredUntil) => ApplySnapshot(value);
    public void ApplySnapshot(int value) => charge = Mathf.Clamp(value, 0, 2);

    public override void ResetAttack()
    {
        if (playerManager != null && playerManager.IsServer) Character?.ResetBasicAttack();
        charge = 0;
        base.ResetAttack();
    }
}
