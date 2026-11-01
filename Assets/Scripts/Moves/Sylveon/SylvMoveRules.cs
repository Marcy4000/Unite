using UnityEngine;

public static class SylvMoveRules
{
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public const string Folder = "Assets/Prefabs/Objects/Moves/Sylveon/";
    public const string BasicAttackPath = Folder + "SylvBasicAttack.prefab";
    public const string BoostedAttackPath = Folder + "SylvBoostedAttack.prefab";
    public const string SwiftPath = Folder + "SylvSwift.prefab";
    public const string BabyDollEyesPath = Folder + "SylvBabyDollEyes.prefab";
    public const string HyperVoicePath = Folder + "HypervoiceHitbox.prefab";
    public const string MysticalFirePath = Folder + "SylvMysticalFire.prefab";
    public const string DrainingKissPath = Folder + "SylvDrainingKiss.prefab";
    public const string CalmMindPath = Folder + "SylvCalmMind.prefab";
    public const string UnitePath = Folder + "SylvFairyFrolic.prefab";

    public const ushort FireDebuff = 1400, KissSlow = 1401, EyesAttack = 1402, EyesSlow = 1403;
    public const ushort CalmSpeed = 1404, CalmAttack = 1405, CalmDefense = 1406, CalmShield = 1407;
    public const ushort PassiveAttack = 1408, PassiveDefense = 1409, FrolicInvincible = 1410;
    public const ushort UniteSpeed = 1411, UniteCdr = 1412, UniteShield = 1413;
    public const ushort BoostedSpeed = 1414;

    public const float BasicRange = 5f, BasicExpiry = 4.5f, BasicRecoverySeconds = 0.25f, ProjectileMaxLifetime = 3f;
    public const float SwiftRange = 5f, SwiftCooldown = 7f, SwiftShotInterval = 0.25f, SwiftLifetime = 0.25f;
    public const int SwiftShotCount = 4;
    public const float BabyDollEyesRange = 6f, BabyDollEyesCooldown = 5f, BabyDollEyesCastLock = 0.25f;
    public const float HyperVoiceRange = 5f, HyperVoiceCooldown = 7f, HyperVoiceDuration = 2.5f, HyperVoiceFarDistance = 3.5f;
    public const int HyperVoiceWavesNormal = 6, HyperVoiceWavesUpgraded = 7;
    public const float FireDashRange = 3f, FireDashDuration = 0.25f, FireCooldown = 7f;
    public const float FireRange = 5, FireLifetime = 5, FireInterval = 0.5f, FireSpeed = 12;
    public const float KissRange = 6, KissBreakRange = 9, KissSpeed = 12, KissCooldown = 9f, KissCastLock = 0.2f;
    public const float CalmCooldown = 8f, CalmDuration = 3f;
    public const float UniteRange = 5f, UniteAirborneDuration = 1.333f, UniteBuffDuration = 6f, UniteLifestealDuration = 10f;
    public const int PassiveMaxStacks = 4;
    public const float PassiveStackDuration = 1.5f;
    public const short PassiveBonusPerStack = 5;

    public static string IdleAnimation(bool evolved) => evolved ? "ani_idle_bat_0700" : "ani_idle_bat_0133";
    public static string BasicAnimation(int step, bool evolved) =>
        evolved ? $"ani_atk{Mathf.Clamp(step, 0, 2) + 1}_bat_0700" : $"ani_atk{Mathf.Clamp(step, 0, 2) + 4}_bat_0133";

    public const string SwiftAnimation = "ani_spell1a_bat_0133";
    public const string BabyDollEyesAnimation = "ani_spell2_bat_0133";
    public const string MysticalFireAnimation = "ani_spell1a_bat_0700";
    public const string HyperVoiceAnimation = "ani_spell1b_1_bat_0700";
    public const string DrainingKissAnimation = "ani_spell2_bat_0700";
    public const string CalmMindAnimation = "ani_spell2b_bat_0700";
    public const string UniteAnimation = "ani_spellu_bat_0700";

    public static int FlameCount(bool upgraded) => upgraded ? 5 : 4;
    public static int HyperVoiceWaveCount(bool upgraded) => upgraded ? HyperVoiceWavesUpgraded : HyperVoiceWavesNormal;

    public static DamageInfo BasicDamage(ulong actor, bool boosted) =>
        boosted
            ? new DamageInfo(actor, 0.35f, 10, 180, DamageType.Special, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit)
            : new DamageInfo(actor, 1f, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit);

    public static DamageInfo SwiftDamage(ulong actor) => new DamageInfo(actor, 0.32f, 6, 190, DamageType.Special);
    public static DamageInfo BabyDollEyesDamage(ulong actor) => new DamageInfo(actor, 0.63f, 18, 330, DamageType.Special);
    public static DamageInfo HyperVoiceCloseDamage(ulong actor) => new DamageInfo(actor, 0.32f, 6, 110, DamageType.Special);
    public static DamageInfo HyperVoiceFarDamage(ulong actor) => new DamageInfo(actor, 0.54f, 8, 184, DamageType.Special);

    public static DamageInfo FireDamage(ulong actor, bool repeat)
    {
        var damage = new DamageInfo(actor, 0.8f, 13, 482, DamageType.Special);
        damage.multiplier = repeat ? 0.28f : 1f;
        return damage;
    }

    public static DamageInfo KissDamage(ulong actor) => new DamageInfo(actor, 0.26f, 4, 160, DamageType.Special);
    public static DamageInfo KissHeal(ulong actor, bool upgraded) => new DamageInfo(actor, upgraded ? 0.345f : 0.23f,
        0, (short)(upgraded ? 285 : 190), DamageType.Special, DamageProprieties.IsHeal);

    public static DamageInfo UniteLandingDamage(ulong actor, Pokemon target)
    {
        var damage = new DamageInfo(actor, 1.3f, 13, 750, DamageType.Special, DamageProprieties.IsUniteMove);
        if (target != null && target.Type == PokemonType.Wild && target.CalculateDamage(damage) > 2000)
            damage = new DamageInfo(actor, 0f, 0, 2000, DamageType.True, DamageProprieties.IsUniteMove);
        return damage;
    }

    public static int CalmShieldAmount(int spAttack) => Mathf.FloorToInt(0.9f * spAttack + 100);
    public static bool CanBlock(DamageProprieties flags) =>
        (flags & (DamageProprieties.IsBasicAttack | DamageProprieties.IsHeal | DamageProprieties.IsMuscleBand)) == 0;

    public static bool CountsForPassive(DamageInfo damage) => (damage.proprieties & DamageProprieties.IsHeal) == 0;

    public static bool CanHit(PlayerManager actor, Pokemon target) =>
        actor != null && target != null && target != actor.Pokemon && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam)
        && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
}
