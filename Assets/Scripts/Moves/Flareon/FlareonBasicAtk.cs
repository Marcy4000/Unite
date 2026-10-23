using UnityEngine;

public class FlareonBasicAtk : BasicAttackBase
{
    public override void Initialize(PlayerManager manager) { base.Initialize(manager); range = FlareonMoveRules.BasicRange; }
    public override void Perform(bool wildPriority)
    {
        GameObject target = Aim.Instance.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null || !target.TryGetComponent(out Pokemon pokemon)) return;
        Vector3 direction = FlareonMoveRules.Direction(target.transform.position - playerManager.transform.position, playerManager.transform.forward);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        FlareonMoveObject.PresentBasic(playerManager.NetworkObjectId, playerManager.MovesController.FlareonAttackCharge == 2, direction);
        playerManager.MovesController.FlareonBasicRPC(pokemon.NetworkObjectId);
        // Contact is at 0.1s. No missing Flareon attack clips are requested.
        playerManager.StopMovementForTime(0.14f, playerManager.Pokemon.CurrentLevel < 3);
    }
    public override void Update() { }
}
