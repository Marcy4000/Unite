using UnityEngine;

public abstract class UmbreonMoveEffect : PrefabMoveEffect
{
    protected UmbreonRuntime Combat => Actor != null ? Actor.MovesController.UmbreonCombat : null;
    private ushort slow, stun;
    protected ushort SlowId => slow == 0 ? slow = UmbreonEffectIds.Acquire() : slow;
    protected ushort StunId => stun == 0 ? stun = UmbreonEffectIds.Acquire() : stun;
    protected void Slow(Pokemon target, short amount, float duration)
    {
        if (!UmbreonMoveRules.Living(target) || target.HasStatusEffect(StatusType.Unstoppable) || target.HasStatusEffect(StatusType.Invincible)) return;
        RefreshStat(target, new StatChange(amount, Stat.Speed, duration, true, false, true, SlowId));
    }
    protected bool Stun(Pokemon target, float duration)
    {
        if (!UmbreonMoveRules.Living(target)) return false;
        target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, StunId).WithSource(Actor.NetworkObjectId));
        return target.HasStatusEffect(StunId);
    }
    protected void Visual(string name, Vector3 origin, float radius, float duration)
        => Actor.MovesController.ShowCombatVisualRPC(UmbreonMoveRules.Visuals + name, origin, Direction, Vector3.one * radius, duration);
    protected override void Cleanup()
    {
        if (slow != 0) UmbreonEffectIds.Release(slow, 3);
        if (stun != 0) UmbreonEffectIds.Release(stun, 2);
    }
}
