using UnityEngine;

public partial class BotBrain
{
    private float nextItem;
    private void TryBattleItem(float health, PlayerManager enemy)
    {
        if (Time.time < nextItem || player.PlayerState != PlayerState.Alive || player.BotIsRecalling) return;
        nextItem = Time.time + 0.5f;
        var moves = player.MovesController;
        if (moves.BattleItem == null || !moves.BattleItemStatus.HasStatus(ActionStatusType.None)) return;
        bool use = moves.BattleItem is Potion ? health < 0.65f :
            moves.BattleItem is FullHeal ? player.Pokemon.HasAnyStatusEffect(new[] { StatusType.Incapacitated, StatusType.Frozen, StatusType.Asleep, StatusType.Bound, StatusType.Immobilized }) :
            moves.BattleItem is XSpeed ? State == BotState.Retreat || (enemy != null && FlatDistance(enemy.transform.position, transform.position) > 4) :
            moves.BattleItem is EjectButton ? State != BotState.Recover && health < 0.22f && enemy != null :
            moves.BattleItem is GoalGetter ? State == BotState.Score && player.CurrentEnergy >= 15 :
            moves.BattleItem is XAttack || moves.BattleItem is BrickLol ? enemy != null && FlatDistance(enemy.transform.position, transform.position) < 6 : false;
        if (!use) return;
        player.Aim.SetBotAim(moves.BattleItem is EjectButton ? null : enemy?.Pokemon,
            moves.BattleItem is EjectButton ? transform.position + (transform.position - enemy.transform.position).normalized * 5 : destination);
        moves.TryUsingBattleItem();
        moves.BattleItem.Update();
        moves.TryFinishingBattleItem();
    }
}
