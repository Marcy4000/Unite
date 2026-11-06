using System.Collections;
using UnityEngine;

public class GlaceMoveEffect : PrefabMoveEffect
{
    protected override IEnumerator Execute()
    {
        var combat = Actor.MovesController.GlaceCombat;
        if (combat == null) yield break;
        switch (MoveId)
        {
            case AvailableMoves.GlaceSwift:
                combat.Swift();
                break;
            case AvailableMoves.GlaceTailWhip:
                LockActions(); FaceRPC(GlaceonMoveRules.Direction(Target.transform.position - Actor.transform.position, Direction));
                PlayAnimation(GlaceonMoveRules.Evolved(Actor) ? "ani_spell2_bat_0471" : "ani_spell2G_bat_0133", 0.4f);
                Actor.MovesController.LaunchGlaceProjectile(Target, GlaceonShot.Tail);
                combat.TailReady();
                yield return CastPause(0.4f);
                break;
            case AvailableMoves.GlaceIceShard:
                combat.IceShard(Upgraded);
                PlayAnimation("ani_spell2a_bat_0471", 0.2f);
                break;
            case AvailableMoves.GlaceIcyWind:
                LockActions(); FaceRPC(Direction);
                PlayAnimation("ani_spell2b_bat_0471", 0.25f);
                if (Actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) yield break;
                Drive(Actor.transform.position + Direction * 4, 0.2f);
                yield return CastPause(0.2f);
                if (!CastInterrupted) combat.IcyWind();
                break;
        }
    }
}
