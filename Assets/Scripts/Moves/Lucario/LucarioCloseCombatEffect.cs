using System.Collections;
using UnityEngine;

public class LucarioCloseCombatEffect : LucarioMoveEffect
{
    public int Punches { get; private set; }
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); Unstoppable(LucarioMoveRules.CombatDuration);
        PlayAnimation("spell3a1", LucarioMoveRules.CombatWindup);
        Actor.MovesController.ShowLucarioAttachedVisualRPC("Fist", "Bip001 R Hand", LucarioMoveRules.CombatDuration);
        Actor.MovesController.ShowLucarioAttachedVisualRPC("Fist", "Bip001 L Hand", LucarioMoveRules.CombatDuration);
        double began = NetworkManager.ServerTime.Time;
        yield return CastPause(LucarioMoveRules.CombatWindup); if (CastInterrupted) yield break;
        PlayAnimation("spell3a2", LucarioMoveRules.CombatLastHit - LucarioMoveRules.CombatWindup); PhaseRPC(1);
        for (int i = 0; i < 11; i++)
        {
            double at = began + Mathf.Lerp(LucarioMoveRules.CombatWindup, LucarioMoveRules.CombatLastHit, i / 10f);
            yield return CastPause(Mathf.Max(0, (float)(at - NetworkManager.ServerTime.Time))); if (CastInterrupted) yield break;
            Punches++; int hits = 0;
            foreach (var target in Box(LucarioMoveRules.CombatWidth, LucarioMoveRules.CombatRange, LucarioMoveRules.CombatRange * 0.5f))
                if (CanHit(target) && target.ResolveDamage(LucarioMoveRules.Combat(Actor.NetworkObjectId, Upgraded)) > 0)
                { hits++; Visual("Impact", target.transform.position + Vector3.up, 0.4f, 0.12f); }
            Combat.Heal(LucarioMoveRules.CombatHeal(Actor.Pokemon.GetAttack(), hits));
        }
        Combat.ReadyKick(); PhaseRPC(2); PlayAnimation("spell3a3", LucarioMoveRules.CombatDuration - LucarioMoveRules.CombatLastHit);
        yield return CastPause(Mathf.Max(0, (float)(began + LucarioMoveRules.CombatDuration - NetworkManager.ServerTime.Time))); UnlockActions();
    }
}
