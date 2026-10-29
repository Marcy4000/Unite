using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NinetalesSnowGlobeEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject gathering, explosion;
    protected override void BeginPresentation()
    {
        transform.position = Actor.transform.position;
        if (gathering != null) { gathering.SetActive(true); gathering.transform.localScale = Vector3.one * NinetalesMoveRules.UniteRadius; }
        if (explosion != null) explosion.SetActive(false);
    }
    protected override IEnumerator Execute()
    {
        string animation = NinetalesMoveRules.Animation("spellg");
        LockActions(); PlayAnimation(animation, NinetalesMoveRules.UniteWindup + NinetalesMoveRules.UniteRecovery, NinetalesMoveRules.AnimationBlend);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, NinetalesMoveRules.UniteWindup + NinetalesMoveRules.UniteRecovery, true, NinetalesMoveRules.UniteImmuneId));
        yield return new WaitForSeconds(NinetalesMoveRules.UniteWindup);
        ExplodeRPC(); Physics.SyncTransforms();
        var combat = Actor.MovesController.NinetalesCombat;
        if (combat != null)
            foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, NinetalesMoveRules.UniteRadius))
            {
                bool frozen = target.HasStatusEffect(StatusType.Frozen);
                combat.Hit(target, NinetalesMoveRules.UniteDamage(Actor.NetworkObjectId, frozen));
            }
        NinetalesMoveRules.Refresh(Actor.Pokemon, new StatChange(30, Stat.Speed, NinetalesMoveRules.UniteBuffDuration, true, true, true, NinetalesMoveRules.UniteSpeedId));
        NinetalesMoveRules.Refresh(Actor.Pokemon, new StatChange(30, Stat.Cdr, NinetalesMoveRules.UniteBuffDuration, true, true, false, NinetalesMoveRules.UniteCdrId));
        Actor.Pokemon.RemoveShieldWithIDRPC(NinetalesMoveRules.UniteShieldId);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), NinetalesMoveRules.UniteShieldId, 0, NinetalesMoveRules.UniteBuffDuration, true));
        yield return new WaitForSeconds(NinetalesMoveRules.UniteRecovery);
        Actor.AnimationManager.RecoverAnimationRPC(animation, NinetalesMoveRules.Animation("idle"), NinetalesMoveRules.AnimationBlend);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ExplodeRPC()
    {
        if (gathering != null) gathering.SetActive(false);
        if (explosion != null) { explosion.SetActive(true); explosion.transform.localScale = Vector3.one * NinetalesMoveRules.UniteRadius; }
    }
    protected override void Cleanup()
    {
        if (IsServer && Actor != null && Actor.Pokemon.IsSpawned) Actor.Pokemon.RemoveStatusEffectWithID(NinetalesMoveRules.UniteImmuneId);
    }
}
