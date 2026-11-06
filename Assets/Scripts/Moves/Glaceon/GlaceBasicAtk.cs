using UnityEngine;

public class GlaceBasicAtk : ChargedBasicAttackBase
{
    protected override int AttackCharge => playerManager != null ? playerManager.MovesController.GlaceAttackCharge : 0;
    protected override bool ShowAttackCounter => playerManager != null && playerManager.MovesController.GlaceAttackMode == 0;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => GlaceonMoveRules.InputBuffer;
    public byte Charge => (byte)AttackCharge;
    public override void Initialize(PlayerManager manager) { range = GlaceonMoveRules.BasicRange; base.Initialize(manager); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = GlaceonMoveRules.BasicRange; }
    public override void Perform(bool wildPriority)
    {
        float actualRange = playerManager.MovesController.GlaceAttackMode == 2 ? GlaceonMoveRules.WindRange : range;
        var target = ActorAim.AimInCircle(actualRange, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null && target.TryGetComponent(out Pokemon pokemon)) playerManager.MovesController.RequestServerBasicAttack(pokemon.NetworkObjectId);
    }
    public override void PerformOnServer(Pokemon target) { playerManager.MovesController.GlaceCombat?.Basic(target); }
    public override void Update()
    {
        range = playerManager != null && playerManager.MovesController.GlaceAttackMode == 2 ? GlaceonMoveRules.WindRange : GlaceonMoveRules.BasicRange;
        base.Update();
    }
}
