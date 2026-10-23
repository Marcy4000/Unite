using UnityEngine;

public class EmboarBasicAtk : BasicAttackBase
{
    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = EmboarMoveRules.BasicRange(manager.Pokemon.CurrentLevel);
    }
    public override void Perform(bool wildPriority)
    {
        range = EmboarMoveRules.BasicRange(playerManager.Pokemon.CurrentLevel);
        GameObject target = Aim.Instance.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null || !target.TryGetComponent(out Pokemon pokemon)) return;
        Vector3 direction = EmboarMoveRules.Direction(target.transform.position - playerManager.transform.position, playerManager.transform.forward);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        playerManager.MovesController.EmboarBasicRPC(pokemon.NetworkObjectId, direction);
        // No basic-attack animation: the model does not currently provide one.
        playerManager.StopMovementForTime(0.22f * playerManager.MovesController.GetAtkSpeedCooldown(), false);
    }
    public override void Update() { range = EmboarMoveRules.BasicRange(playerManager.Pokemon.CurrentLevel); }
}
