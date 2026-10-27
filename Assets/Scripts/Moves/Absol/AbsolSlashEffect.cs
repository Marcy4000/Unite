using System.Collections;
using UnityEngine;

public class AbsolSlashEffect : PrefabMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); PlayAnimation(AbsolMoveRules.SlashAnimation, AbsolMoveRules.SlashWindup + AbsolMoveRules.SlashRecovery, AbsolMoveRules.MoveAnimationBlend);
        yield return new WaitForSeconds(AbsolMoveRules.SlashWindup);
        if (Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) yield break;
        Physics.SyncTransforms();
        foreach (var target in CombatTargeting.SectorTargets(Actor.transform.position, Direction, AbsolMoveRules.SlashRange, AbsolMoveRules.SlashAngle))
            if (CanHit(target)) target.ResolveDamage(AbsolMoveRules.SlashDamage(Actor.NetworkObjectId));
        Actor.MovesController.ShowCombatVisualRPC(AbsolMoveRules.Visuals + "MeleeSlash", Actor.transform.position, Direction, Vector3.one * AbsolMoveRules.SlashRange, 0.2f);
        yield return new WaitForSeconds(AbsolMoveRules.SlashRecovery);
        if (!Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) PlayAnimation(AbsolMoveRules.Idle, blendDuration: AbsolMoveRules.RecoveryAnimationBlend);
    }
}
