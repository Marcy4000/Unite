using System.Collections;
using UnityEngine;

public class LucarioExtremeSpeedEffect : LucarioMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); Combat.Tick(); bool reset = false;
        if (Upgraded) Combat.ExtremeBuff();
        PlayAnimation("spell1b", LucarioMoveRules.DashWindup + LucarioMoveRules.ExtremeRange / LucarioMoveRules.ExtremeSpeed + LucarioMoveRules.DashRecovery);
        yield return CastPause(LucarioMoveRules.DashWindup); if (CastInterrupted) yield break;
        PhaseRPC(1);
        yield return Dash(Direction, LucarioMoveRules.ExtremeRange, LucarioMoveRules.ExtremeSpeed, LucarioMoveRules.ExtremeWidth, target =>
        {
            if (target.ResolveDamage(LucarioMoveRules.Extreme(Actor.NetworkObjectId)) <= 0) return;
            Combat.Heal(LucarioMoveRules.ExtremeHeal(Actor.Pokemon.GetAttack(), Actor.Pokemon.CurrentLevel));
            if (!reset && Combat.ConsumeMarker(target)) reset = true;
        });
        if (CastInterrupted) yield break;
        Combat.ReadySpin(); PhaseRPC(2); yield return CastPause(LucarioMoveRules.DashRecovery); UnlockActions();
    }
}
