using UnityEngine;

/// <summary>Unite-DB detailed formulas and agreed project-scale tuning. Levels are zero-based.</summary>
public static class LucarioMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Lucario/";
    public const string Visuals = "Effects/Lucario/";
    public const int UniteEnergy = 90000;
    public static float BasicRange = 2.5f, BoostedRange = 4, BasicWidth = 3, BasicLength = 4.25f;
    public static float BasicWindup = 0.18f, BasicRecovery = 0.22f, BoostedGap = 0.16f, BoostedRadius = 2.6f;
    public static float QuickRange = 5.5f, QuickWidth = 2, DashWindup = 0.04f, DashRecovery = 0.12f;
    public static float ExtremeRange = 5.5f, ExtremeWidth = 2.5f, ExtremeSpeed = 40, MarkerRange = 8, MarkerCooldown = 10;
    public static float ExtremeAttackWindow = 1, ExtremeAttackRadius = 2.6f, ExtremeBuff = 7.5f, ExtremeBuffDuration = 2;
    public static float PunchMinRange = 3, PunchMaxRange = 6, PunchWidth = 2.5f, PunchSpeed = 26;
    public static float PunchFullCharge = 1, PunchHoldLimit = 4, PunchCancelCooldown = 1.4f, PunchShove = 1;
    public static float MeteorRange = 5.5f, MeteorWidth = 3.5f, MeteorWindup = 0.4f, MeteorRecovery = 0.2f, MeteorShove = 2;
    public static float BoneRange = 6.5f, BoneSpeed = 18, BoneRadius = 2, BoneWindow = 3, BoneShove = 2;
    public static float BoneStrikeInterval = 0.11f, BoneCatchRadius = 2, BoneCatchSpeed = 30, BoneRecovery = 0.12f;
    public static Vector3 BoneNearSize = new Vector3(3, 2.5f, 2), BoneFarSize = new Vector3(3, 2.5f, 5);
    public static float CombatRange = 5.5f, CombatWidth = 3, CombatWindup = 0.4f, CombatLastHit = 1.2f, CombatDuration = 1.4f;
    public static float KickWindow = 5, KickRange = 4, KickTravel = 0.16f;
    public static float AuraRange = 40, AuraRadius = 2, AuraWindup = 0.6f, AuraStartSpeed = 4, AuraAcceleration = 30, AuraMaxSpeed = 40;
    public static float AuraTickInterval = 0.2f, AuraEmpowerMultiplier = 1.2f, UniteBuffDuration = 6;
    public static float PassiveThreshold = 0.5f, PassiveCooldown = 45, PassiveDuration = 5;
    public static string Animation(string clip) => "ani_" + clip + "_bat_0448";
    public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    public static bool Living(Pokemon p) => p != null && p.IsSpawned && p.CurrentHp > 0;
    public static bool Enemy(PlayerManager actor, Pokemon p) => actor != null && Living(p) && !p.TeamMember.IsOnSameTeam(actor.CurrentTeam)
        && !p.HasStatusEffect(StatusType.Invincible) && !p.HasStatusEffect(StatusType.Untargetable);
    public static float ChargeFraction(float held) => Mathf.Clamp01(held / Mathf.Max(0.01f, PunchFullCharge));
    public static float PunchRange(float held) => Mathf.Lerp(PunchMinRange, PunchMaxRange, ChargeFraction(held));
    public static int PassiveShield(float attack, int level) => Mathf.FloorToInt(2.5f * attack + 50 * level + 50);
    public static int ExtremeHeal(float attack, int level) => Mathf.FloorToInt(0.3375f * attack + level + 42);
    public static int CombatHeal(float attack, int targets) => Mathf.FloorToInt(0.115f * attack + 23) * Mathf.Max(0, targets);
    public static int ExecuteBonus(int missingHp, bool wild) => Mathf.Min(wild ? 1000 : int.MaxValue, Mathf.FloorToInt(Mathf.Max(0, missingHp) * 0.12f));
    public static DamageInfo Damage(ulong id, float ratio, short slider, short value) => new DamageInfo(id, ratio, slider, value, DamageType.Physical);
    public static DamageInfo Basic(ulong id, float ratio = 1, bool followup = false)
    {
        var damage = DamageInfo.Tuned("lucario.basic", new DamageInfo(id, ratio, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit));
        damage.suppressOnHitEffects = followup; return damage;
    }
    public static DamageInfo Quick(ulong id) => DamageInfo.Tuned("lucario.quickattack", Damage(id, 2.21f, 11, 240));
    public static DamageInfo Extreme(ulong id) => DamageInfo.Tuned("lucario.extremespeed", Damage(id, 2.08f, 6, 280));
    public static DamageInfo Punch(ulong id, float held, int level, int missing, bool wild, bool empowered)
    {
        float t = ChargeFraction(held);
        // The game's integer slider cannot express the interpolated level term, so bake it into base damage.
        var damage = DamageInfo.Tuned("lucario.poweruppunch", Damage(id, Mathf.Lerp(2.0916f, 3.818f, t), 0,
            (short)Mathf.FloorToInt(Mathf.Lerp(88 + 3 * level, 161 + 5 * level, t))));
        damage.baseDmg = (short)Mathf.Min(short.MaxValue, damage.baseDmg + ExecuteBonus(missing, wild));
        damage.multiplier = empowered ? AuraEmpowerMultiplier : 1; return damage;
    }
    public static DamageInfo Meteor(ulong id) => DamageInfo.Tuned("lucario.meteormash", Damage(id, 1.67f, 8, 180));
    public static DamageInfo Bone(ulong id) => DamageInfo.Tuned("lucario.bonerush", Damage(id, 0.625f, 1, 50));
    public static DamageInfo Combat(ulong id, bool plus) => DamageInfo.Tuned(plus ? "lucario.closecombat.plus" : "lucario.closecombat", Damage(id, plus ? 0.56f : 0.5f, 2, (short)(plus ? 69 : 60)));
    public static DamageInfo Aura(ulong id) { var d = DamageInfo.Tuned("lucario.auracannon", Damage(id, 1.74f, 4, 210)); d.proprieties = DamageProprieties.IsUniteMove; return d; }
}
