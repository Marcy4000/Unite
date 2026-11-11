using UnityEngine;

/// <summary>Unite-DB formulas; levels are zero-based. Estimated geometry/timing is deliberately tunable here.</summary>
public static class TrevenantMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Trevenant/";
    public const string Visuals = "Effects/Trevenant/";
    // Reference dimensions baked into the authored VFX/indicator assets.
    public const float AuthoredHammerRange = 4, AuthoredHammerWidth = 3, AuthoredHammerLength = 5, AuthoredCurseRadius = 3.5f;
    public static float BasicRange = 2.5f, BasicRadius = 1.25f, BasicOffset = 1.5f;
    public static float BasicWindup = 0.22f, BasicRecovery = 0.24f, BasicReferenceCooldown = 0.93333f;
    public static float WispRange = 5, WispRadius = 0.45f, WispSpacing = 0.25f, WispSpeed = 10, WispSpreadAngle = 25;
    public static float WispWindup = 0.2f, WispRecovery = 0.15f, BurnDuration = 3, BurnInterval = 1;
    public static float HammerRange = 4, HammerAngle = 180, HammerGatherOffset = 2;
    public static float HammerSecondRange = 5, HammerSecondWidth = 3, HammerRecastWindow = 5;
    public static float HammerWindup = 0.35f, HammerRecovery = 0.2f, HammerPullDuration = 0.25f;
    public static float HammerSweepContact = 0.36f, HammerSlamContact = 0.4f;
    public static float HammerHealthCost = 0.05f;
    public static float HammerSweepVisualScale = 1, HammerSlamVisualScale = 1.5f;
    public static float HammerImpactLifetime = 0.3f, HammerTrailLifetime = 0.16f;
    public static Vector3 HammerGripOffset = Vector3.zero, HammerGripEuler = new Vector3(0, 0, 90);
    public static float CurseRadius = 3.5f, CursePlusRadius = 4.3f, CurseDuration = 6, CurseInterval = 0.5f;
    public static float CurseHealthInterval = 1, CurseHealthCost = 0.02f, CurseWindup = 0.5f, CurseRecovery = 0.3f;
    public static int CurseEventsToFill = 20;
    public static float BranchRange = 3.5f, BranchRadius = 1, BranchWindup = 0.2f, BranchTravel = 0.2f, BranchRecovery = 0.1f;
    public static float HornRange = 7, HornWidth = 3, HornWindup = 0.2f, HornTravel = 0.9f, HornRecovery = 0.2f;
    public static float HornThrow = 0.75f, HornAttackReduction = 0.5f;
    public static float HornCarryOffset = 1.5f, HornCarryInterval = 0.1f, ThrowHeight = 1.5f;
    public static float PainRange = 6, PainBreakRange = 7.5f, PainDuration = 6, PainPlusDuration = 8.5f;
    public static float PainWindup = 0.2f, PainRecovery = 0.2f, PainInterval = 0.5f, PainHealRatio = 1.344f;
    public static float UniteRange = 8, UniteRadius = 4.5f, UniteWindup = 0.7f, UniteRecovery = 0.8f, UniteThrow = 0.5f;
    public static float UniteMarkDuration = 6, UniteMarkInterval = 0.5f, UniteShieldDuration = 6;
    public static float PassiveThreshold = 0.35f, PassiveDuration = 4, PassiveHealInterval = 0.5f;
    public static float PassiveCooldown = 100, PassiveReductionInterval = 0.5f;
    public const int UniteEnergy = 100000;
    public static float HpFraction(Pokemon p) => (float)p.CurrentHp / Mathf.Max(1, p.GetMaxHp());
    public static int HpTier(float fraction) => fraction < 0.4f ? 2 : fraction <= 0.7f ? 1 : 0;
    public static float HammerStun(float hp) => 1.1f + HpTier(hp) * 0.2f;
    public static short CurseSlow(float hp) => (short)(12 + HpTier(hp) * 5);
    public static float HornHeal(float hp) => 0.05f + HpTier(hp) * 0.01f; // Three ticks PER enemy.
    public static float PainRedirect(float hp) => 0.3f + HpTier(hp) * 0.1f;
    public static float PassiveHeal(int level) => (2 + 0.18f * level) / 100;
    public static float CurseArea(bool plus) => plus ? CursePlusRadius : CurseRadius;
    public static int HealthCost(int hp, int maximum, float fraction, bool fromMaximum) => Mathf.Clamp(Mathf.FloorToInt((fromMaximum ? maximum : hp) * fraction), 0, Mathf.Max(0, hp - 1));
    public static int RedirectAmount(int incoming, float hpFraction) => Mathf.Max(0, Mathf.FloorToInt(incoming * PainRedirect(hpFraction)));
    public static float GaugeFraction(int events) => Mathf.Clamp01((float)events / Mathf.Max(1, CurseEventsToFill));
    public static bool Living(Pokemon p) => p != null && p.IsSpawned && p.CurrentHp > 0;
    public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    public static Vector3 WispDirection(Vector3 aim, int index) => Quaternion.AngleAxis((index - 1) * WispSpreadAngle, Vector3.up) * Flat(aim).normalized;
    public static Vector3 WispPosition(Vector3 origin, Vector3 aim, int index, float travelled)
        => origin + Vector3.up * 0.8f + Vector3.Cross(Vector3.up, Flat(aim).normalized) * ((index - 1) * WispSpacing)
            + WispDirection(aim, index) * travelled;
    public static bool InRange(Vector3 a, Vector3 b, float radius) => Flat(a - b).sqrMagnitude <= radius * radius;
    public static bool Enemy(PlayerManager actor, Pokemon p) => actor != null && Living(p)
        && !p.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !p.HasStatusEffect(StatusType.Invincible) && !p.HasStatusEffect(StatusType.Untargetable);
    public static string Animation(PlayerManager actor, string clip) => "ani_" + clip + "_bat_" + (actor.Pokemon.CurrentLevel >= 4 ? "0709" : "0708");
    public static DamageInfo Damage(ulong source, float ratio, short slider, short value) => new DamageInfo(source, ratio, slider, value, DamageType.Physical);
    public static DamageInfo Wisp(ulong source, bool first) { var d = DamageInfo.Tuned("trevenant.wisp", Damage(source, 0.58f, 10, 190)); d.multiplier = first ? 1 : 0.5f; return d; }
    public static DamageInfo Burn(ulong source) => DamageInfo.Tuned("trevenant.burn", Damage(source, 0.145f, 3, 48));
    public static DamageInfo Hammer(ulong source) => DamageInfo.Tuned("trevenant.hammer", Damage(source, 1.1f, 22, 242));
    public static DamageInfo Curse(ulong source, bool detonate) => detonate ? DamageInfo.Tuned("trevenant.curse", Damage(source, 1.6f, 12, 120)) : DamageInfo.Tuned("trevenant.curse.2", Damage(source, 0.4f, 3, 30));
    public static DamageInfo Branch(ulong source) => DamageInfo.Tuned("trevenant.branch", Damage(source, 0.75f, 9, 280));
    public static DamageInfo Horn(ulong source) => DamageInfo.Tuned("trevenant.horn", Damage(source, 0.43f, 4, 200));
    public static DamageInfo HornBonus(ulong source) { var d = DamageInfo.Tuned("trevenant.hornbonus", Damage(source, 0.215f, 2, 100)); d.source = DamageSource.Secondary; d.suppressOnHitEffects = true; return d; }
    public static DamageInfo Pain(ulong source) => DamageInfo.Tuned("trevenant.pain", Damage(source, 0.29f, 2, 80));
    public static DamageInfo Unite(ulong source) { var d = DamageInfo.Tuned("trevenant.unite", Damage(source, 1.6f, 15, 700)); d.proprieties = DamageProprieties.IsUniteMove; return d; }
}
