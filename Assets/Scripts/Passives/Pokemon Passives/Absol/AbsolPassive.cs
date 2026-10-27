public class AbsolPassive : PassiveBase
{
    private StatChange luck;
    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        luck = new StatChange(15, Stat.CritRate, 0, false, true, false, AbsolMoveRules.LuckId, false);
        controller.Pokemon.SetPersistentStatRPC(luck, true);
    }
    public override void Update() { }
    public override void Dispose()
    {
        if (playerManager != null && playerManager.Pokemon != null && playerManager.Pokemon.IsSpawned)
            playerManager.Pokemon.SetPersistentStatRPC(luck, false);
        playerManager = null;
    }
}
