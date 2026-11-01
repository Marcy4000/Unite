using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SylvFairyFrolicEffect : PrefabMoveEffect
{
    [SerializeField] private SphereCollider landingArea;
    [SerializeField] private GameObject airborne, landing, buff;
    [SerializeField] private GameObject landingWarning;
    private bool flyingActive, healing;
    private bool landingDamage;

    public event Action Landed;

    protected override void BeginPresentation()
    {
        transform.position = Actor.transform.position;
        landingWarning.SetActive(true);
        airborne.SetActive(true);
        landing.SetActive(false);
        buff.SetActive(false);
        if ((IsServer || Actor.IsOwner) && Actor.PlayerMovement != null)
        {
            flyingActive = true;
            Actor.PlayerMovement.IsFlying = true;
        }
    }

    protected override IEnumerator Execute()
    {
        LockActions(movement: false);
        PlayAnimation(SylvMoveRules.UniteAnimation);
        var negative = new List<StatusEffect>();
        foreach (StatusEffect status in Actor.Pokemon.StatusEffects)
            if (status.IsNegativeStatus() && status.IsTimed) negative.Add(status);
        foreach (StatusEffect status in negative) Actor.Pokemon.RemoveStatusEffectRPC(status);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, SylvMoveRules.UniteAirborneDuration, true, SylvMoveRules.FrolicInvincible));
        yield return new WaitForSeconds(SylvMoveRules.UniteAirborneDuration);
        Actor.Pokemon.RemoveStatusEffectWithID(SylvMoveRules.FrolicInvincible);
        UnlockActions();
        LandRPC();
        Landed?.Invoke();
        healing = true;
        Actor.Pokemon.OnHealthDamageDealt += HealFromDamage;
        RefreshStat(Actor.Pokemon, new StatChange(30, Stat.Speed, SylvMoveRules.UniteBuffDuration, true, true, true, SylvMoveRules.UniteSpeed));
        RefreshStat(Actor.Pokemon, new StatChange(30, Stat.Cdr, SylvMoveRules.UniteBuffDuration, true, true, false, SylvMoveRules.UniteCdr));
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), SylvMoveRules.UniteShield, 0, SylvMoveRules.UniteBuffDuration, true));
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
        yield return new WaitForSeconds(SylvMoveRules.UniteLifestealDuration);
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
        airborne.SetActive(false);
        landing.SetActive(true);
        buff.SetActive(true);
        ReleaseFlight();
        StartCoroutine(HideLanding());
    }

    private IEnumerator HideLanding()
    {
        yield return new WaitForSeconds(0.4f);
        if (landing != null) landing.SetActive(false);
    }

    private void ReleaseFlight()
    {
        if (!flyingActive || Actor == null) return;
        flyingActive = false;
        if (Actor.PlayerMovement != null)
            Actor.PlayerMovement.IsFlying = false;
    }

    protected override void Update()
    {
        base.Update();
        if (Actor != null) transform.position = Actor.transform.position;
    }

    protected override void Cleanup()
    {
        Landed = null;
        if (landingWarning != null) landingWarning.SetActive(false);
        ReleaseFlight();
        if (IsServer && Actor != null && Actor.Pokemon != null)
        {
            Actor.Pokemon.OnHealthDamageDealt -= HealFromDamage;
            if (Actor.Pokemon.IsSpawned)
                Actor.Pokemon.RemoveStatusEffectWithID(SylvMoveRules.FrolicInvincible);
        }
    }
}
