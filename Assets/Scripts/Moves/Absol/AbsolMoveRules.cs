using System.Collections.Generic;
using UnityEngine;

/// <summary>All Absol tuning in project units. Formulas use zero-based CurrentLevel.</summary>
public static class AbsolMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Absol/";
    public const string Visuals = "Effects/Absol/";
    public const float BasicRange = 2.5f;
    // AGE MSize is the full box size, not the aim indicator or half-extents.
    // Distances use the same /1000 conversion as the active-skill dump.
    public static readonly Vector3 BasicHitboxSize = new Vector3(3, 10, 4);
    public static readonly Vector3 BoostedHitboxSize = new Vector3(3, 10, 4.5f);
    public static readonly Vector3 BoostedBonusHitboxSize = new Vector3(3.5f, 10, 7);
    public const float BasicHitboxOffset = 1.5f, BoostedBonusHitboxOffset = 3f, PursuitHitboxOffset = 1f;
    // Seconds at base attack speed. Recovery is AFTER the final damage tick.
    public const float BasicWindup = 0.20f, BasicRecovery = 0.26f;
    public const float BoostedWindup = 0.22f, BoostedHitDelay = 0.08f, BoostedRecovery = 0.26f;
    public const float BasicReferenceCooldown = 0.93333f, BasicInputBuffer = 0.18f;
    public const float PursuitAttackWindup = 0.04f, PursuitAttackRecovery = 0.14f;
    public const float BoostedDefenseDuration = 2f;
    public const float FeintCooldown = 8f, SlashCooldown = 6f, NightCooldown = 5.5f, PursuitCooldown = 5.5f, PsychoCooldown = 7f, SuckerCooldown = 5f;
    public const float DashDistance = 4f, DashBow = 1.5f, DashRadius = 0.75f, DashDuration = 0.3f;
    // Active-skill dump ranges describe aiming, not AGE-script dash travel.
    public const float DashIndicatorRange = 5f, PursuitIndicatorRange = 5f;
    public const float FeintWindup = 0.04f, FeintRecovery = 0.06f;
    public const float NightFirstWindup = 0.04f, NightFirstRecovery = 0.06f;
    public const float NightSecondWindup = 0.09f, NightSecondRecovery = 0.08f;
    public const float NightRecastWindow = 6f, NightRecastDelay = 0.15f;
    public const float SlashRange = 3f, SlashAngle = 120f, SlashWindup = 0.12f, SlashRecovery = 0.22f;
    public const float PursuitDistance = 5f, PursuitDuration = 0.2f, PursuitEmpowerDuration = 3f;
    public const float PursuitWindup = 0.035f, PursuitRecovery = 0.065f;
    public const float PursuitRange = 5.5f, PursuitLeapDuration = 0.16f, BackstabAngle = 120f;
    public const float PursuitSpeedDuration = 2f, PursuitCooldownReduction = 0.7f;
    public const float PsychoRange = 7f, PsychoSpeed = 18f, MarkDuration = 5f;
    public static readonly Vector3 PsychoHitboxSize = new Vector3(3, 10, 3);
    public const float PsychoAnimationDuration = 0.35f;
    public const float PsychoWindup = 0.10f, PsychoRecovery = 0.08f;
    public const float SuckerRange = 5f, SuckerBreakRange = 10f, SuckerStance = 1f, SuckerDashDuration = 0.2f;
    public const float SuckerRadius = 1.5f, SuckerBehindDistance = 1f, SuckerShoveDistance = 1.5f, SuckerShoveDuration = 0.2f;
    public const float SuckerRecovery = 0.08f;
    // S22 declares a backward line move capped at 2000, Length=150. Its
    // /1000 time mapping remains provisional; existing stance timing is kept.
    public const float SuckerRetreatDistance = 2f, SuckerRetreatDuration = 0.15f;
    public const float UniteWidth = 5f, UniteLength = 8f, UniteHitboxOffset = 4.5f, UniteWindup = 1f / 3f;
    public const float UniteIndicatorRange = 8f;
    public const float UniteFlurryDuration = 1.15f, UniteFinalRange = 10f, UniteFinalAngle = 70f, UniteFinalOffset = -0.5f;
    public const int UniteTicks = 5, UniteEnergy = 80000;
    public const float UniteFinalSpacing = 0.055f, UniteFinalDelay = 0.2f, UniteSecondAnimationDuration = 1.2f;
    public const float UniteRecovery = UniteSecondAnimationDuration - UniteFinalDelay - 3 * UniteFinalSpacing;
    public const float UniteFourHitRange = 2.5f, UniteThreeHitRange = 5f, UniteTwoHitRange = 7.5f;
    public const float UniteShoveDistance = 1.5f, UniteShoveDuration = 0.2f;

    public const ushort LuckId = 35900, DefenseId = 35901, AttackSpeedId = 35902, PursuitSpeedId = 35903;
    public const ushort PsychoSpeedId = 35904, SuckerSelfSlowId = 35905, SuckerTargetSlowId = 35906, SuckerStatusId = 35907;
    public const ushort UniteStatusId = 35908, UniteUntargetableId = 35909, UniteSpeedId = 35910, UniteShieldId = 35911;
    public const ushort SuckerAttackSpeedId = 35912;

    public const string Idle = "ani_idle_bat_0359_Copy";
    // Visual-only crossfades in seconds; never delay gameplay phases or damage.
    public const float BasicAnimationBlend = 0.06f, MoveAnimationBlend = 0.06f;
    public const float RecoveryAnimationBlend = 0.10f, UniteAnimationBlend = 0.10f;
    public const string FeintAnimation = "ani_spell21_bat_0359_XYZFixed";
    public const string NightFirstAnimation = "ani_spell21_bat_0359_XYZFixed", NightSecondAnimation = "ani_spell2a_bat_0359_XYZFixed";
    public const string PursuitAnimation = "ani_spell1b1_bat_0359_Copy", PursuitAttackAnimation = "ani_spell1b2_bat_0359_XYZFixed";
    public const string SlashAnimation = "ani_spell1_bat_0359_XYZFixed", PsychoAnimation = "ani_spell1a2_bat_0359_XYZFixed";
    public const string SuckerAnimation = "ani_spell2b1_bat_0359_XYZFixed", SuckerAttackAnimation = "ani_spell2b4_bat_0359_XYZFixed";
    public const string UniteFirstAnimation = "ani_spellu1_bat_0359_XYZFixed", UniteSecondAnimation = "ani_spellu2_bat_0359_XYZFixed";

    public static DamageInfo Damage(ulong source, float ratio, short slider, short baseDamage, float bonusCrit = 0)
        => new DamageInfo(source, ratio, slider, baseDamage, DamageType.Physical, DamageProprieties.CanCrit) { bonusCritRate = bonusCrit };
    public static DamageInfo BasicDamage(ulong source, bool boosted, bool isolated)
    {
        var damage = boosted ? DamageInfo.Tuned("absol.basicdamage", Damage(source, isolated ? 0.885f : 0.51f, (short)(isolated ? 6 : 5), (short)(isolated ? 120 : 100))) : DamageInfo.Tuned("absol.basicdamage.2", Damage(source, 1, 0, 0));
        damage.proprieties |= DamageProprieties.IsBasicAttack; return damage;
    }
    public static DamageInfo BoostedBonusDamage(ulong source)
    {
        var damage = DamageInfo.Tuned("absol.boostedbonusdamage", Damage(source, 0.375f, 1, 20));
        damage.suppressOnHitEffects = true;
        damage.proprieties |= DamageProprieties.IsBasicAttack; return damage;
    }
    public static float BasicTimingScale(float attackCooldown) => Mathf.Clamp(attackCooldown / BasicReferenceCooldown, 0.2f, 1f);
    // A3/A4 have AtkSpdNotAnim=1, but AtkSpdNotAtkInter=0. The shared attack
    // interval remains speed-dependent; only these animation phases stay fixed.
    public static float AttackTimingScale(float attackCooldown, bool boosted, bool pursuit)
        => boosted || pursuit ? 1 : BasicTimingScale(attackCooldown);
    public static DamageInfo DashDamage(ulong source, bool feint, bool second, float missingHp)
    {
        var damage = feint ? DamageInfo.Tuned("absol.dashdamage", Damage(source, 0.66f, 7, 130)) : second ? DamageInfo.Tuned("absol.dashdamage.2", Damage(source, 1.035f, 9, 210, NightCrit(missingHp))) : DamageInfo.Tuned("absol.dashdamage.3", Damage(source, 0.69f, 6, 140, NightCrit(missingHp)));
        if (feint) damage.type = DamageType.True;
        return damage;
    }
    public static float NightCrit(float missingHpFraction) => Mathf.Min(30, Mathf.Clamp01(missingHpFraction) * 80);
    public static DamageInfo PursuitDamage(ulong source, bool backstab)
    {
        var damage = backstab ? DamageInfo.Tuned("absol.pursuitdamage", Damage(source, 2.14f, 21, 259)) : DamageInfo.Tuned("absol.pursuitdamage.2", Damage(source, 1.43f, 14, 173));
        damage.proprieties |= DamageProprieties.IsBasicAttack; return damage;
    }
    public static DamageInfo MarkDamage(ulong source, int remaining)
    {
        var damage = remaining == 1 ? DamageInfo.Tuned("absol.markdamage", Damage(source, 1.08f, 7, 72, 15)) : DamageInfo.Tuned("absol.markdamage.2", Damage(source, 0.72f, 5, 48, 15));
        damage.source = DamageSource.Secondary; return damage;
    }
    public static DamageInfo SlashDamage(ulong source) => DamageInfo.Tuned("absol.slashdamage", Damage(source, 0.89f, 6, 200, 15));
    public static DamageInfo PsychoDamage(ulong source) => DamageInfo.Tuned("absol.psychodamage", Damage(source, 0.5f, 1, 65, 15));
    public static DamageInfo SuckerDamage(ulong source, bool counter) => counter ? DamageInfo.Tuned("absol.suckerdamage", Damage(source, 1.56f, 10, 250)) : DamageInfo.Tuned("absol.suckerdamage.2", Damage(source, 1.05f, 6, 165));
    public static DamageInfo UniteDamage(ulong source)
    {
        var damage = DamageInfo.Tuned("absol.unitedamage", Damage(source, 0.83f, 4, 184));
        damage.proprieties |= DamageProprieties.IsUniteMove; return damage;
    }
    public static float FinalPulseRadius(int pulse) => UniteFourHitRange * (Mathf.Clamp(pulse, 0, 3) + 1);
    // For stationary points measured from the finale's offset origin. Runtime
    // contact is queried again for every expanding pulse, never snapshotted.
    public static int FinalHitCount(float distance) => distance < 0 || distance > UniteFinalRange ? 0
        : distance <= UniteFourHitRange ? 4 : distance <= UniteThreeHitRange ? 3 : distance <= UniteTwoHitRange ? 2 : 1;
    public static HashSet<Pokemon> BoxTargets(Vector3 origin, Vector3 direction, Vector3 size, float forwardOffset)
    {
        direction.y = 0;
        if (!CombatTargeting.Finite(direction) || direction.sqrMagnitude < 0.001f) return new HashSet<Pokemon>();
        return CombatTargeting.BoxTargets(origin + Vector3.up * 0.8f + direction.normalized * forwardOffset,
            size * 0.5f, Quaternion.LookRotation(direction));
    }
    public static HashSet<Pokemon> PsychoTargets(Vector3 previous, Vector3 current, Vector3 direction)
    {
        // The projectile travels along its local Z axis. One elongated box is
        // the exact swept volume, including the final segment on a slow frame.
        Vector3 size = PsychoHitboxSize;
        size.z += Vector3.Distance(previous, current);
        return BoxTargets((previous + current) * 0.5f, direction, size, 0);
    }
    public static bool IsBackstab(Vector3 attacker, Vector3 victim, Vector3 forward)
    {
        Vector3 delta = attacker - victim; delta.y = 0; forward.y = 0;
        return delta.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f
            && Vector3.Dot(delta.normalized, -forward.normalized) >= Mathf.Cos(BackstabAngle * 0.5f * Mathf.Deg2Rad);
    }
    public static bool IsDirectDamage(DamageInfo damage) => damage.source == DamageSource.Direct
        && (damage.proprieties & (DamageProprieties.IsMuscleBand | DamageProprieties.IsHeal)) == 0;
    public static bool CanConsumeMark(DamageInfo damage) => IsDirectDamage(damage) && !damage.suppressOnHitEffects;
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && target != null && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
    public static void Refresh(Pokemon target, StatChange change) { target.RemoveAllStatChangeWithIDRPC(change.ID); target.AddStatChange(change); }
    public static void DashHaste(Pokemon actor) => Refresh(actor, new StatChange(60, Stat.AtkSpeed, 0.5f, true, true, false, AttackSpeedId));
    public static void Shove(Pokemon target, Vector3 direction, float distance, float duration)
    {
        if (target.TryGetComponent<PlayerMovement>(out var movement)) movement.DisplaceRPC(direction, distance, duration);
        else target.ApplyKnockbackRPC(direction, distance / duration);
    }
}
