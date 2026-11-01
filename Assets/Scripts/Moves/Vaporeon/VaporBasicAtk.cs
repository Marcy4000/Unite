using Unity.Netcode;
using UnityEngine;

public class VaporBasicAtk : ChargedBasicAttackBase
{
    private VaporeonCharacterRuntime Character => playerManager?.MovesController?.Combat?.Character as VaporeonCharacterRuntime;
    protected override int AttackCharge => Character?.Charge ?? 0;
    protected override bool ShowAttackCounter => Character == null || !Character.Diving;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => 0.22f;

    public override void Initialize(PlayerManager manager) { base.Initialize(manager); range = VaporMoveRules.BasicRange; }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = VaporMoveRules.BasicRange; }

    public override void Perform(bool wildPriority)
    {
        GameObject target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null && target.TryGetComponent(out NetworkObject network))
            playerManager.MovesController.RequestServerBasicAttack(network.NetworkObjectId);
    }

    public override void PerformOnServer(Pokemon target) => Character?.PerformBasicAttack(target);
}
