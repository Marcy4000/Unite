using UnityEngine;

public abstract class ServerBattleItem : BattleItemBase
{
    public AvailableBattleItems Kind { get; }

    protected ServerBattleItem(AvailableBattleItems kind)
    {
        Kind = kind;
        Name = BattleItemRules.Name(kind);
        Cooldown = BattleItemRules.Cooldown(kind);
    }

    public override void Update() { }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        Submit(Vector3.zero);
    }

    protected void Submit(Vector3 direction, ulong targetId = ulong.MaxValue)
    {
        IsActive = false;
        playerManager.MovesController.SubmitBattleItem(Kind, direction, targetId);
    }
}
