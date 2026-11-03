public class FluffyTail : ServerBattleItem
{
    public FluffyTail() : base(AvailableBattleItems.FluffyTail) { }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Aim.InitializeAutoAim(BattleItemRules.TargetRange, 90, AimTarget.Wild);
    }

    public override void Update()
    {
        if (IsActive) playerManager.Aim.SureHitAim();
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        var target = playerManager.Aim.SureHitAim();
        playerManager.Aim.HideAutoAim();
        if (target == null || !target.TryGetComponent(out Pokemon pokemon)) { base.Cancel(); return; }
        Submit(UnityEngine.Vector3.zero, pokemon.NetworkObjectId);
    }

    public override void Cancel()
    {
        if (IsActive && playerManager != null && playerManager.Aim != null) playerManager.Aim.HideAutoAim();
        base.Cancel();
    }
}
