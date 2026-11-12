using System.Collections;
using UnityEngine;

public class LucarioQuickAttackEffect : LucarioMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); PlayAnimation("spell1", LucarioMoveRules.DashWindup + LucarioMoveRules.QuickRange / 40 + LucarioMoveRules.DashRecovery);
        yield return CastPause(LucarioMoveRules.DashWindup); if (CastInterrupted) yield break;
        PhaseRPC(1);
        yield return Dash(Direction, LucarioMoveRules.QuickRange, 40, LucarioMoveRules.QuickWidth, target => target.ResolveDamage(LucarioMoveRules.Quick(Actor.NetworkObjectId)));
        if (CastInterrupted) yield break;
        PhaseRPC(2); yield return CastPause(LucarioMoveRules.DashRecovery); UnlockActions();
    }
}
