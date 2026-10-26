using Unity.Netcode;
using UnityEngine;

public class VaporBasicAtk : ChargedBasicAttackBase
{
    protected override int AttackCharge => playerManager.MovesController != null ? playerManager.MovesController.VaporeonAttackCharge : 0;
    protected override bool ShowAttackCounter => playerManager.MovesController == null || !playerManager.MovesController.VaporeonDiving;
    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager); range = VaporMoveRules.BasicRange;
    }
    public override void Perform(bool wildPriority)
    {
        var target = Aim.Instance.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null || !target.TryGetComponent(out NetworkObject network)) return;
        playerManager.MovesController.VaporeonBasicRPC(network.NetworkObjectId);
        playerManager.transform.rotation = Quaternion.LookRotation(VaporMoveRules.Direction(target.transform.position - playerManager.transform.position, playerManager.transform.forward));
        // Short firing commitment, then kite freely. Animation is chosen by the server.
        playerManager.StopMovementForTime(Mathf.Min(0.15f, playerManager.MovesController.GetAtkSpeedCooldown() * 0.25f));
    }
}
