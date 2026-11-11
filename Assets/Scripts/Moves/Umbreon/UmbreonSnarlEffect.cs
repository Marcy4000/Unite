using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UmbreonSnarlEffect : UmbreonMoveEffect
{
    private ushort self, final;
    private bool registeredLock;
    protected override int UnlockedMoveSlot => Actor != null && Actor.MovesController.HasUmbreonMove(AvailableMoves.UmbreonFoulPlay) ? 0 : -1;
    private readonly Dictionary<Pokemon, List<ushort>> stacks = new Dictionary<Pokemon, List<ushort>>();
    protected override IEnumerator Execute()
    {
        self = UmbreonEffectIds.Acquire(); final = UmbreonEffectIds.Acquire();
        LockActions(false, false);
        Actor.MovesController.UmbreonSnarlLocks++;
        registeredLock = true;
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.HindranceResistance, UmbreonMoveRules.SnarlDuration, true, self));
        Actor.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, UmbreonMoveRules.SnarlDuration, true, false, true, self).WithSource(Actor.NetworkObjectId));
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), self, 0, 3, true).WithSource(Actor.NetworkObjectId));
        PlayAnimation(UmbreonMoveRules.Animation(Actor, "spell2b2"), UmbreonMoveRules.SnarlDuration);
        float began = Time.time;
        for (int i = 0; i < UmbreonMoveRules.SnarlTicks; i++)
        {
            yield return CastPause(Mathf.Max(0, began + i * UmbreonMoveRules.SnarlDuration / UmbreonMoveRules.SnarlTicks - Time.time));
            if (CastInterrupted) yield break;
            foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, UmbreonMoveRules.SnarlRadius))
            {
                if (!CanHit(target)) continue;
                target.ResolveDamage(UmbreonMoveRules.Snarl(Actor.NetworkObjectId, false)); Slow(target, 15, 2);
                if (!stacks.TryGetValue(target, out var list)) { list = new List<ushort>(); stacks[target] = list; }
                if (list.Count < 6)
                {
                    ushort id = UmbreonEffectIds.Acquire(); list.Add(id);
                    target.AddStatChange(new StatChange(3, Stat.Attack, 1.5f, true, false, true, id).WithSource(Actor.NetworkObjectId));
                    target.AddStatChange(new StatChange(3, Stat.SpAttack, 1.5f, true, false, true, id).WithSource(Actor.NetworkObjectId));
                }
            }
            Visual("SnarlPulse", Actor.transform.position, UmbreonMoveRules.SnarlRadius, 0.18f);
        }
        yield return CastPause(Mathf.Max(0, began + UmbreonMoveRules.SnarlDuration - Time.time));
        if (CastInterrupted) yield break;
        PlayAnimation(UmbreonMoveRules.Animation(Actor, "spell2b4"), 0.2f);
        foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, UmbreonMoveRules.SnarlRadius))
        {
            if (!CanHit(target)) continue;
            target.ResolveDamage(UmbreonMoveRules.Snarl(Actor.NetworkObjectId, true)); Stun(target, Upgraded ? 1 : 0.7f);
            target.AddStatChange(new StatChange(15, Stat.Attack, 3, true, false, true, final).WithSource(Actor.NetworkObjectId));
            target.AddStatChange(new StatChange(15, Stat.SpAttack, 3, true, false, true, final).WithSource(Actor.NetworkObjectId));
        }
        Visual("SnarlBurst", Actor.transform.position, UmbreonMoveRules.SnarlRadius, 0.4f);
        UnlockActions();
        ReleaseSnarlLock();
    }
    private void ReleaseSnarlLock()
    {
        if (!registeredLock) return;
        if (Actor != null) Actor.MovesController.UmbreonSnarlLocks--;
        registeredLock = false;
    }
    protected override void Update()
    {
        base.Update();
        if (Actor != null) transform.position = Actor.transform.position;
    }
    protected override void Cleanup()
    {
        if (IsServer)
        {
            ReleaseSnarlLock();
            if (Actor != null && Actor.IsSpawned)
            { Actor.Pokemon.RemoveStatusEffectWithID(self); Actor.Pokemon.RemoveAllStatChangeWithIDRPC(self); }
            foreach (var list in stacks.Values) foreach (var id in list) UmbreonEffectIds.Release(id, 1.5f);
            UmbreonEffectIds.Release(self, 3); UmbreonEffectIds.Release(final, 3);
        }
        base.Cleanup();
    }
}
