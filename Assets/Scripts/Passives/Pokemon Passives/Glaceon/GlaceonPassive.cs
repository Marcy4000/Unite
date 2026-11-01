public class GlaceonPassive : PassiveBase
{
    private GlaceonCharacterRuntime Character => playerManager?.MovesController.Combat?.Character as GlaceonCharacterRuntime;
    public byte IciclesCount => Character?.Crystals ?? 0;
    public override void Start(PlayerManager controller) => base.Start(controller);
    public void ResetTimer() { if (playerManager != null && playerManager.IsServer) Character?.ResetCrystalTimer(); }
    public void UpdateIciclesCount(byte count) { if (playerManager != null && playerManager.IsServer) Character?.SetCrystals(count); }
    public override void Update()
    {
        if (playerManager == null || playerManager.HPBar == null) return;
        var character = Character;
        playerManager.HPBar.ShowGenericGuage(character != null && character.Evolved);
        playerManager.HPBar.UpdateGenericGuageValue((character?.CrystalRemaining ?? 0) / 5f);
    }
    public override void Dispose()
    {
        if (playerManager != null && playerManager.HPBar != null) playerManager.HPBar.ShowGenericGuage(false);
        playerManager = null;
    }
}
