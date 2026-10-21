using Unity.Netcode;
using UnityEngine;

public class CresseliaBasicAtk : BasicAttackBase
{
    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 4f;
        manager.HPBar.ShowGenericGuage(true);
    }
    public override void Perform(bool wildPriority)
    {
        var target = Aim.Instance.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null || !target.TryGetComponent(out NetworkObject network)) return;
        playerManager.MovesController.CresseliaBasicRPC(network.NetworkObjectId);
        playerManager.transform.rotation = Quaternion.LookRotation(CresseliaMoveRules.Direction(target.transform.position - playerManager.transform.position, playerManager.transform.forward));
        playerManager.StopMovementForTime(0.15f, true);
    }
    public override void Update()
    {
        playerManager.HPBar.UpdateGenericGuageValue(CresseliaMoveRules.BoostCooldown - playerManager.MovesController.CresseliaBoostRemaining,
            CresseliaMoveRules.BoostCooldown);
    }
}
