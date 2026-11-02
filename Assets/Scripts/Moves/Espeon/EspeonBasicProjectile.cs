using System.Collections;

public class EspeonBasicProjectile : PrefabMoveEffect
{
    protected override IEnumerator Execute()
    {
        yield return TravelTo(Target, 20, () => CanHit(Target));
        if (Arrived && CanHit(Target)) Actor.MovesController.EspeonCombat?.Hit(Target,
            EspeonMoveRules.Basic(Actor.NetworkObjectId, Upgraded, false), true);
    }
}
