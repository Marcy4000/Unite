using Unity.Netcode;
using UnityEngine;

public class MeowsticMBasicAtk : ChargedBasicAttackBase
{
    protected override int AttackCharge => playerManager.MovesController != null ? playerManager.MovesController.MeowsticAttackCharge : 0;
    public override void Initialize(PlayerManager manager) { base.Initialize(manager); range = 4f; }
    public override void Perform(bool wildPriority)
    {
        GameObject target = Aim.Instance.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null || !target.TryGetComponent(out NetworkObject network)) return;
        playerManager.MovesController.MeowsticBasicRPC(network.NetworkObjectId);
        playerManager.transform.rotation = Quaternion.LookRotation(MeowsticMoveRules.Direction(target.transform.position - playerManager.transform.position, playerManager.transform.forward));
        playerManager.MovesController.BeginMeowsticBasicRecovery();
    }
}
