using UnityEngine;

// The server owns Reckless. This adapter only presents its replicated state to the owner.
public class EmboarPassive : PassiveBase
{
    public bool IsRecklessActive => playerManager != null && playerManager.MovesController.EmboarRecklessReady;
    public override void Update()
    {
        if (playerManager == null || playerManager.HPBar == null) return;
        var combat = playerManager.MovesController;
        bool show = playerManager.Pokemon.CurrentLevel >= 6 && playerManager.Pokemon.CurrentHp > 0;
        playerManager.HPBar.ShowGenericGuage(show);
        if (!show) return;
        float remaining = combat.EmboarRecklessRearm;
        playerManager.HPBar.UpdateGenericGuageValue(combat.EmboarRecklessReady ? 1 : remaining > 0 ? 1 - remaining / EmboarMoveRules.RecklessRearm : combat.EmboarRecklessCharge / 100f);
    }
    public override void Dispose()
    {
        if (playerManager != null && playerManager.HPBar != null) playerManager.HPBar.ShowGenericGuage(false);
    }
}
