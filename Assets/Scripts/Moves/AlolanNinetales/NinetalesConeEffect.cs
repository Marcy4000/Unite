using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NinetalesConeEffect : PrefabMoveEffect
{
    [SerializeField] private bool dazzlingGleam;
    [SerializeField] private GameObject cone;
    protected override void BeginPresentation()
    {
        transform.SetPositionAndRotation(Actor.transform.position, Quaternion.LookRotation(Direction));
        if (cone != null) { cone.SetActive(false); cone.transform.localScale = new Vector3(NinetalesMoveRules.ConeRange, 1, NinetalesMoveRules.ConeRange); }
    }
    protected override IEnumerator Execute()
    {
        string animation = NinetalesMoveRules.Animation(dazzlingGleam ? "spell1b" : "spell1", Actor.Pokemon.CurrentLevel >= 3);
        LockActions(); FaceRPC(Direction);
        PlayAnimation(animation, NinetalesMoveRules.ConeWindup + NinetalesMoveRules.ConeRecovery, NinetalesMoveRules.AnimationBlend);
        yield return CastPause(NinetalesMoveRules.ConeWindup);
        if (CastInterrupted) yield break;
        ShowConeRPC(); Physics.SyncTransforms();
        var combat = Actor.MovesController.NinetalesCombat;
        if (combat != null)
            foreach (var target in CombatTargeting.SectorTargets(Actor.transform.position, Direction, NinetalesMoveRules.ConeRange, NinetalesMoveRules.ConeAngle))
            {
                var damage = dazzlingGleam ? NinetalesMoveRules.GleamDamage(Actor.NetworkObjectId, Upgraded) : NinetalesMoveRules.PowderDamage(Actor.NetworkObjectId);
                if (combat.Hit(target, damage) <= 0) continue;
                if (dazzlingGleam && NinetalesMoveRules.Living(target)) target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, Upgraded ? 1 : 0.8f, true, 0));
                else if (!dazzlingGleam) combat.Slow(target, 50, 1);
            }
        yield return CastPause(NinetalesMoveRules.ConeRecovery);
        if (!CastInterrupted) Actor.AnimationManager.RecoverAnimationRPC(animation, NinetalesMoveRules.Animation("idle", Actor.Pokemon.CurrentLevel >= 3), NinetalesMoveRules.AnimationBlend);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowConeRPC() { if (cone != null) cone.SetActive(true); }
}
