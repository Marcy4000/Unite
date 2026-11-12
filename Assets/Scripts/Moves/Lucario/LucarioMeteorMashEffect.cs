using System.Collections;
using UnityEngine;

public class LucarioMeteorMashEffect : LucarioMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); PlayAnimation("spell3", LucarioMoveRules.MeteorWindup + LucarioMoveRules.MeteorRecovery);
        Actor.MovesController.ShowLucarioAttachedVisualRPC("Fist", "Bip001 R Hand", LucarioMoveRules.MeteorWindup + LucarioMoveRules.MeteorRecovery);
        yield return CastPause(LucarioMoveRules.MeteorWindup); if (CastInterrupted) yield break;
        PhaseRPC(1);
        foreach (var target in Box(LucarioMoveRules.MeteorWidth, LucarioMoveRules.MeteorRange, LucarioMoveRules.MeteorRange * 0.5f))
            if (CanHit(target) && target.ResolveDamage(LucarioMoveRules.Meteor(Actor.NetworkObjectId)) > 0)
            { Shove(target, Direction, LucarioMoveRules.MeteorShove, 0.4f); Visual("Impact", target.transform.position, 1); }
        yield return CastPause(LucarioMoveRules.MeteorRecovery); UnlockActions();
    }
}
