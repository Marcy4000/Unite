using Unity.Netcode;
using UnityEngine;

public class CresseliaBasicAtk : BasicAttackBase
{
    private CresseliaCharacterRuntime Character => playerManager?.MovesController?.CresseliaRuntime;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => 0.2f;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 4f;
        _ = manager?.MovesController?.CresseliaRuntime;
        manager?.HPBar?.ShowGenericGuage(true);
    }

    public override void InitializeServer(PlayerManager manager)
    {
        base.InitializeServer(manager);
        range = 4f;
        _ = manager?.MovesController?.CresseliaRuntime;
    }

    public override void Perform(bool wildPriority)
    {
        var target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null || !target.TryGetComponent(out NetworkObject network)) return;
        playerManager.MovesController.RequestServerBasicAttack(network.NetworkObjectId);
    }

    public override void PerformOnServer(Pokemon target) => Character?.PerformBasicAttack(target);

    public override void Update()
    {
        if (playerManager == null || playerManager.HPBar == null || playerManager.MovesController == null) return;
        playerManager.HPBar.UpdateGenericGuageValue(
            CresseliaMoveRules.BoostCooldown - playerManager.MovesController.CresseliaBoostRemaining,
            CresseliaMoveRules.BoostCooldown);
    }
}
