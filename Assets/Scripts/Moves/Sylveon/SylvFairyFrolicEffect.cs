using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SylvFairyFrolicEffect : PrefabMoveEffect
{
    [SerializeField] private SphereCollider landingArea;
    [SerializeField] private GameObject airborne, landing, buff;
    [SerializeField] private GameObject landingWarning;
    private bool ownerLocked, healing;
    private bool landingDamage;

    protected override void BeginPresentation()
    {
        transform.position = Actor.transform.position;
        landingWarning.SetActive(true);
        airborne.SetActive(true); landing.SetActive(false); buff.SetActive(false);
        if (Actor.IsOwner)
        {
            ownerLocked = true;
            Actor.MovesController.LockEveryAction();
            Actor.ScoreStatus.AddStatus(ActionStatusType.Busy);
            Actor.PlayerMovement.IsFlying = true;
            Actor.AnimationManager.PlayAnimation("ani_spellu_bat_0700");
        }
    }

    protected override IEnumerator Execute()
    {
        var negative = new List<StatusEffect>();
        foreach (StatusEffect status in Actor.Pokemon.StatusEffects)
            if (status.IsNegativeStatus() && status.IsTimed) negative.Add(status);
        foreach (StatusEffect status in negative) Actor.Pokemon.RemoveStatusEffectRPC(status);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 1.333f, true, SylvMoveRules.FrolicInvincible));
        yield return new WaitForSeconds(1.333f);
        Actor.Pokemon.RemoveStatusEffectWithID(SylvMoveRules.FrolicInvincible);
        LandRPC();
        healing = true;
        Actor.Pokemon.OnHealthDamageDealt += HealFromDamage;
        RefreshStat(Actor.Pokemon, new StatChange(30, Stat.Speed, 6, true, true, true, SylvMoveRules.UniteSpeed));
        RefreshStat(Actor.Pokemon, new StatChange(30, Stat.Cdr, 6, true, true, false, SylvMoveRules.UniteCdr));
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), SylvMoveRules.UniteShield, 0, 6, true));
        var targets = new HashSet<Pokemon>();
        Vector3 center = transform.TransformPoint(landingArea.center);
        foreach (Collider collider in Physics.OverlapSphere(center, landingArea.radius))
            if (collider.TryGetComponent(out Pokemon target) && CanHit(target)) targets.Add(target);
        landingDamage = true;
        foreach (Pokemon target in targets)
        {
            var damage = new DamageInfo(Actor.Pokemon.NetworkObjectId, 1.3f, 13, 750, DamageType.Special, DamageProprieties.IsUniteMove);
            int calculated = target.CalculateDamage(damage, Actor.Pokemon);
            if (target.Type != PokemonType.Player && calculated > 2000) damage.multiplier = 2000f / calculated;
            target.TakeDamageRPC(damage);
        }
        landingDamage = false;
        yield return new WaitForSeconds(10);
        healing = false;
    }

    private void HealFromDamage(Pokemon target, DamageInfo damage, int hpDamage)
    {
        if (!healing || hpDamage <= 0 || Actor.Pokemon.CurrentHp <= 0) return;
        Actor.Pokemon.HealDamageRPC(Mathf.FloorToInt(hpDamage * (landingDamage ? 0.6f : 0.5f)));
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LandRPC()
    {
        landingWarning.SetActive(false);
        airborne.SetActive(false); landing.SetActive(true); buff.SetActive(true);
        ReleaseOwner();
        StartCoroutine(HideLanding());
    }
    private IEnumerator HideLanding() { yield return new WaitForSeconds(0.4f); landing.SetActive(false); }
    private void ReleaseOwner()
    {
        if (!ownerLocked || Actor == null) return;
        ownerLocked = false;
        Actor.PlayerMovement.IsFlying = false;
        Actor.MovesController.UnlockEveryAction();
        Actor.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
    protected override void Update()
    {
        base.Update();
        if (Actor != null) transform.position = Actor.transform.position;
    }
    protected override void Cleanup()
    {
        if (landingWarning != null) landingWarning.SetActive(false);
        ReleaseOwner();
        if (IsServer && Actor != null)
        {
            Actor.Pokemon.OnHealthDamageDealt -= HealFromDamage;
            Actor.Pokemon.RemoveStatusEffectWithID(SylvMoveRules.FrolicInvincible);
        }
    }
}
