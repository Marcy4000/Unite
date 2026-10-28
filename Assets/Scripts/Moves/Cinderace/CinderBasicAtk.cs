using UnityEngine;

public class CinderBasicAtk : ChargedBasicAttackBase
{
    protected override int AttackCharge => playerManager.MovesController != null ? playerManager.MovesController.CinderaceAttackCharge : 0;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 7f;
    }
    public override void Perform(bool wildPriority)
    {
        var target = playerManager.Aim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null || !target.TryGetComponent(out Pokemon pokemon)) return;
        int charge = playerManager.MovesController.CinderaceAttackCharge;
        playerManager.MovesController.CinderaceBasicRPC(pokemon.NetworkObjectId);
        playerManager.AnimationManager.PlayAnimation($"ani_atk{charge + 1}_bat_0815");
        playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown());
        playerManager.transform.rotation = Quaternion.LookRotation(CinderMoveRules.Direction(target.transform.position - playerManager.transform.position, playerManager.transform.forward));
    }
}
