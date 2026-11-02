using Unity.Netcode;
using UnityEngine;

public abstract class EspeonMoveEffect : PrefabMoveEffect
{
    public bool Empowered { get; set; }
    public int BlastCount { get; set; }
    protected EspeonRuntime Combat => Actor != null ? Actor.MovesController.EspeonCombat : null;
    private ushort slowId, stunId;
    private float slowUntil, stunUntil;
    protected ushort StunId => stunId == 0 ? stunId = EspeonEffectIds.Acquire() : stunId;
    protected int Hit(Pokemon target, DamageInfo damage, bool reduceFuture = false) => Combat?.Hit(target, damage, reduceFuture) ?? 0;
    protected void Slow(Pokemon target, int amount, float duration)
    {
        if (Actor == null || !EspeonMoveRules.Living(Actor.Pokemon) || !EspeonMoveRules.Living(target) || target.HasStatusEffect(StatusType.Unstoppable)
            || target.HasStatusEffect(StatusType.Invincible)) return;
        if (slowId == 0) slowId = EspeonEffectIds.Acquire();
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
        target.RemoveAllStatChangeWithIDRPC(slowId);
        target.AddStatChange(new StatChange((short)amount, Stat.Speed, duration, true, false, true, slowId).WithSource(Actor.NetworkObjectId));
    }
    protected bool Stun(Pokemon target, float duration)
    {
        if (Actor == null || !EspeonMoveRules.Living(Actor.Pokemon) || !EspeonMoveRules.Living(target)) return false;
        stunUntil = Mathf.Max(stunUntil, Time.time + duration);
        target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, StunId).WithSource(Actor.NetworkObjectId));
        return target.HasStatusEffect(StunId);
    }
    protected DamageInfo AddFlat(DamageInfo damage, float amount)
    {
        damage.baseDmg = (short)Mathf.Clamp(Mathf.FloorToInt(damage.baseDmg + amount), 0, short.MaxValue);
        return damage;
    }
    protected void Visual(string name, Vector3 origin, Vector3 direction, Vector3 scale, float duration)
        => Actor.MovesController.ShowCombatVisualRPC(EspeonMoveRules.Visuals + name, origin, direction, scale, duration);
    protected void BeamVisual(Vector3 from, Vector3 to, float width, float duration = 0.3f)
        => Visual("Beam", from, to - from, new Vector3(width, width, (to - from).magnitude), duration);
    protected override void Cleanup()
    {
        if (slowId != 0) EspeonEffectIds.ReleaseAfter(slowId, Mathf.Max(0.01f, slowUntil - Time.time));
        if (stunId != 0) EspeonEffectIds.ReleaseAfter(stunId, Mathf.Max(0.01f, stunUntil - Time.time));
    }
}
