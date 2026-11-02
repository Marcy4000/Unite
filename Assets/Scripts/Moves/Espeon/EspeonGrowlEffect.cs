using System.Collections;

public class EspeonGrowlEffect : EspeonMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions();
        FaceRPC(Target.transform.position - Actor.transform.position);
        PlayAnimation(EspeonMoveRules.Animation(Actor, "spell2"), 0.4f);
        yield return CastPause(0.15f);
        if (CastInterrupted) yield break;
        UnlockActions();
        yield return TravelTo(Target, 18, () => CanHit(Target));
        if (Arrived && Hit(Target, EspeonMoveRules.Growl(Actor.NetworkObjectId)) > 0) Slow(Target, 30, 2);
    }
}
