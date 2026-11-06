using UnityEngine;

// Owner presentation only. Resources and Snow Cloak are resolved by GlaceonRuntime.
public class GlaceonPassive : PassiveBase
{
    public byte IciclesCount => playerManager != null ? (byte)playerManager.MovesController.GlaceCrystalCount : (byte)0;
    public override void Start(PlayerManager controller) { base.Start(controller); }
    public override void Update()
    {
        if (playerManager.HPBar == null) return;
        bool show = playerManager.Pokemon.CurrentLevel >= 3 && playerManager.Pokemon.CurrentHp > 0;
        playerManager.HPBar.ShowGenericGuage(show);
        playerManager.HPBar.UpdateGenericGuageValue(show ? playerManager.MovesController.GlaceCrystalRemaining / GlaceonMoveRules.CrystalLifetime : 0);
    }
    public override void Dispose()
    {
        if (playerManager != null && playerManager.HPBar != null) { playerManager.HPBar.UpdateGenericGuageValue(0); playerManager.HPBar.ShowGenericGuage(false); }
        playerManager = null;
    }
}
