using System.Collections;
using UnityEngine;

public class UmbreonMoonlightPranceEffect : UmbreonMoveEffect
{
    private ushort leap, buff, stolen;
    private double shieldDeadline;
    private bool absorbing;
    protected override IEnumerator Execute()
    {
        leap = UmbreonEffectIds.Acquire(); buff = UmbreonEffectIds.Acquire(); stolen = UmbreonEffectIds.Acquire(true);
        shieldDeadline = NetworkManager.ServerTime.Time + UmbreonMoveRules.UniteLeap + UmbreonMoveRules.UniteAuraDuration + UmbreonMoveRules.StolenShieldTail;
        LockActions(false, false);
        PlayAnimation(UmbreonMoveRules.Animation(Actor, "spellu"), UmbreonMoveRules.UniteLeap + 0.2f);
        Actor.Pokemon.AddStatChange(new StatChange((short)Mathf.RoundToInt((1 - UmbreonMoveRules.UniteMoveMultiplier) * 100), Stat.Speed,
            UmbreonMoveRules.UniteLeap, true, false, true, leap).WithSource(Actor.NetworkObjectId));
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, UmbreonMoveRules.UniteLeap, true, leap));
        int extracted = 0;
        foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, UmbreonMoveRules.UniteAuraRadius))
            if (CanHit(target)) extracted += target.ExtractEligibleShields(0.5f);
        Actor.Pokemon.AddStolenShield(extracted, stolen, shieldDeadline);
        yield return CastPause(UmbreonMoveRules.UniteLeap);
        if (CastInterrupted) yield break;
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(leap);
        foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, UmbreonMoveRules.UniteRadius))
            if (CanHit(target)) { target.ResolveDamage(UmbreonMoveRules.Unite(Actor.NetworkObjectId)); Stun(target, 1); }
        Visual("MoonBlast", Actor.transform.position, UmbreonMoveRules.UniteRadius, 0.6f);
        Actor.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, 6, true, true, true, buff));
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.1f), buff, 0, 6, true).WithSource(Actor.NetworkObjectId));
        UnlockActions();
        absorbing = true; Pokemon.BeforeShieldGrant += Absorb;
        double until = NetworkManager.ServerTime.Time + UmbreonMoveRules.UniteAuraDuration;
        shieldDeadline = until + UmbreonMoveRules.StolenShieldTail;
        while (NetworkManager.ServerTime.Time < until && UmbreonMoveRules.Living(Actor.Pokemon))
        {
            foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, UmbreonMoveRules.UniteAuraRadius))
                if (CanHit(target)) Slow(target, 30, 0.2f);
            yield return new WaitForSeconds(0.1f);
        }
    }
    private ShieldInfo Absorb(Pokemon recipient, ShieldInfo shield)
    {
        if (!absorbing || !UmbreonMoveRules.Living(Actor.Pokemon) || !CanHit(recipient) || !UmbreonMoveRules.Stealable(shield)
            || !UmbreonMoveRules.InCircle(Actor.transform.position, recipient.transform.position, UmbreonMoveRules.UniteAuraRadius)) return shield;
        int amount = Mathf.FloorToInt(shield.Amount * 0.5f); shield.Amount -= amount;
        Actor.Pokemon.AddStolenShield(amount, stolen, shieldDeadline);
        return shield;
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
            absorbing = false; Pokemon.BeforeShieldGrant -= Absorb;
            if (Actor != null && Actor.IsSpawned)
            { Actor.Pokemon.RemoveStatusEffectWithID(leap); Actor.Pokemon.RemoveAllStatChangeWithIDRPC(leap); }
            UmbreonEffectIds.Release(leap, 1); UmbreonEffectIds.Release(buff, 6);
            UmbreonEffectIds.Release(stolen, Mathf.Max(0, (float)(shieldDeadline - NetworkManager.ServerTime.Time)));
        }
        base.Cleanup();
    }
}
