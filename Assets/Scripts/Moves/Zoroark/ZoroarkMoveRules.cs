using System.Collections.Generic;
using UnityEngine;

/// <summary>AGE geometry uses 1000 dump units per project unit. Formulas retain Unite-DB tuning (zero-based CurrentLevel).</summary>
public static class ZoroarkMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Zoroark/";
    public const string Visuals = "Effects/Zoroark/";
    public const float BasicRange = 2.5f, BasicAngle = 120, UniqueRange = 4.5f;
    public const float BasicWindup = 0.14f, BasicRecovery = 0.16f, InputBuffer = 0.18f;
    public const float SlashRange = 4, FuryRange = 3.5f, ClawRange = 4.5f, CutRange = 4.5f;
    // IndicatorRange is not the movement maximum: age_570_S11/S12 MMoveModeLineMax.
    public const float ClawDashDistance = 4.4f, CutDashDistance = 5;
    public const float DashRadius = 0.75f, DashDuration = 0.2f, Windup = 0.035f, Recovery = 0.06f;
    public const float ClawEndpointOffset = 2, ClawThrow = 0.5f, ClawThrowPlus = 0.6f;
    public const float NightDashRange = 4, NightWindow = 2.5f, NightAirTime = 1.2f, NightInvincible = 1.4f;
    public const float NightAirSteerSpeed = 1.5f, NightAirSteerDistance = 1.5f;
    public const int NightSteps = 6, FeintSteps = 4, UniteEnergy = 80000;
    public const float CrossLength = 8, CrossWidth = 2.5f;
    public static readonly Vector3 CrossBoxSize = new Vector3(CrossLength, 4, CrossWidth);
    public static readonly Vector3 BasicBoxSize = new Vector3(3, 10, 4);
    public const float BasicBoxOffset = 1.5f;
    public static readonly Vector3 ClawEndpointSize = new Vector3(2.5f, 2.5f, 3);
    public const float FeintRange = 7, FeintReturnRange = 5, FeintWindow = 3, FeintTargetRange = 7, FeintBounce = 4;
    public const float FeintFinalWindup = 0.15f;
    public const float FuryWindow = 4, IllusionCooldown = 15, IllusionDuration = 15, IllusionUnstoppable = 0.33f;
    public const float UniteDuration = 1.33f;
    public static readonly float[] UniteRadii = { 2.8f, 3.5f, 4.2f };
    public const ushort ClawStatus = 57101, NightStatus = 57102, FeintStatus = 57103, IllusionStatus = 57104;
    public const ushort UniteStatus = 57105, UniteSpeed = 57106, UniteShield = 57107;

    public static string Animation(PlayerManager actor, string clip)
        => "ani_" + clip + "_bat_" + (actor.Pokemon.CurrentLevel < 4 ? "057000" : "057101");
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && target != null && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible)
        && !target.HasStatusEffect(StatusType.Untargetable);
    public static DamageInfo Basic(ulong source, bool boosted)
        => DamageInfo.Tuned("zoroark.basic", new DamageInfo(source, boosted ? 1.3f : 1, 0, 0, DamageType.Physical, DamageProprieties.CanCrit | DamageProprieties.IsBasicAttack));
    public static DamageInfo Dash(ulong source, AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.ZoroarkSlash: return DamageInfo.Tuned("zoroark.zoroarkslash", new DamageInfo(source, 0.78f, 12, 230, DamageType.Physical));
            case AvailableMoves.ZoroarkFurySwipes: return DamageInfo.Tuned("zoroark.zoroarkfuryswipes", new DamageInfo(source, 0.94f, 8, 310, DamageType.Physical));
            case AvailableMoves.ZoroarkShadowClaw: return DamageInfo.Tuned("zoroark.zoroarkshadowclaw", new DamageInfo(source, 1, 12, 297, DamageType.Physical));
            case AvailableMoves.ZoroarkCut: return DamageInfo.Tuned("zoroark.zoroarkcut", new DamageInfo(source, 0.9775f, 13, 287, DamageType.Physical));
            case AvailableMoves.ZoroarkNightSlash: return DamageInfo.Tuned("zoroark.zoroarknightslash", new DamageInfo(source, 0.65f, 10, 225, DamageType.Physical));
            default: return DamageInfo.Tuned("zoroark.dash", new DamageInfo(source, 1, 10, 300, DamageType.Physical));
        }
    }
    public static int CutHeal(int attack, int level, bool plus) => Mathf.FloorToInt(attack * (plus ? 0.62f : 0.53f) + level * (plus ? 8 : 7) + (plus ? 185 : 158));
    public static int NightHeal(int attack, int level) => Mathf.FloorToInt(0.72f * attack + 11 * level + 228);
    public static int FinisherDamage(int attack, int level, int missingHp, bool feint, bool wild)
    {
        int value = Mathf.FloorToInt(attack * (feint ? 2.5f : 1.86f) + level * (feint ? 25 : 18) + (feint ? 750 : 600) + missingHp * (feint ? 0.15f : 0.1f));
        return wild ? Mathf.Min(value, feint ? 1200 : 1000) : value;
    }
    public static DamageInfo Finisher(PlayerManager actor, Pokemon target, bool feint, bool basic)
    {
        int total = FinisherDamage(actor.Pokemon.GetAttack(), actor.Pokemon.CurrentLevel, target.GetMissingHp(), feint, target.Type != PokemonType.Player);
        return DamageInfo.Tuned("zoroark.finisher", new DamageInfo(actor.NetworkObjectId, 0, 0, (short)Mathf.Min(short.MaxValue, total), DamageType.Physical,
            basic ? DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit : DamageProprieties.None));
    }
    public static float Range(AvailableMoves move) => move == AvailableMoves.ZoroarkSlash ? SlashRange : move == AvailableMoves.ZoroarkFurySwipes ? FuryRange
        : move == AvailableMoves.ZoroarkCut ? CutRange : move == AvailableMoves.ZoroarkShadowClaw ? ClawRange
        : move == AvailableMoves.ZoroarkFeintAttack ? FeintRange : NightDashRange;

    public static float DashDistance(AvailableMoves move) => move == AvailableMoves.ZoroarkCut ? CutDashDistance
        : move == AvailableMoves.ZoroarkShadowClaw ? ClawDashDistance : Range(move);
    public static float FeintAimRange(bool active, bool finisher) => finisher ? FeintTargetRange : active ? FeintReturnRange : FeintRange;

    // Full MSize dimensions from age_570_S1/S2/S11/S12/S22; not half extents.
    public static Vector3 DashBoxSize(AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.ZoroarkSlash: return new Vector3(2.5f, 2.5f, 3);
            case AvailableMoves.ZoroarkFurySwipes: return new Vector3(2, 10, 3.5f);
            case AvailableMoves.ZoroarkShadowClaw: return new Vector3(2.5f, 10, 5.4f);
            case AvailableMoves.ZoroarkCut: return new Vector3(2.5f, 10, 6);
            case AvailableMoves.ZoroarkFeintAttack: return new Vector3(2.5f, 10, 5);
            default: return Vector3.zero; // Missing Night Slash movement script: retain its existing collision.
        }
    }
    public static float DashBoxOffset(AvailableMoves move) => move == AvailableMoves.ZoroarkSlash ? 0.5f
        : move == AvailableMoves.ZoroarkShadowClaw ? 0.8f : move == AvailableMoves.ZoroarkFeintAttack ? -0.8f : 1;

    public static HashSet<Pokemon> BasicTargets(Vector3 origin, Vector3 direction, bool unique)
    {
        // A4 (Night Slash's special basic) is absent; do not extrapolate A1/A2/A3 to it.
        if (unique) return CombatTargeting.SectorTargets(origin, direction, UniqueRange, BasicAngle);
        return CombatTargeting.BoxTargets(origin + Vector3.up * 0.8f + direction * BasicBoxOffset,
            BasicBoxSize * 0.5f, Quaternion.LookRotation(direction));
    }

    public static HashSet<Pokemon> DashTargets(AvailableMoves move, Vector3 start, Vector3 end, Vector3 direction, bool returning)
    {
        Vector3 size = DashBoxSize(move);
        if (size == Vector3.zero || returning)
            return CombatTargeting.SweptSphereTargets(start, end, DashRadius);
        Vector3 offset = direction * DashBoxOffset(move);
        Quaternion rotation = Quaternion.LookRotation(direction);
        // Sweep the entire actual frame segment, including its endpoint, to avoid tunnelling.
        var result = new HashSet<Pokemon>();
        int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(start, end) / (Mathf.Min(size.x, size.z) * 0.5f)), 1, 128);
        for (int i = 0; i <= samples; i++)
            result.UnionWith(CombatTargeting.BoxTargets(Vector3.Lerp(start, end, i / (float)samples) + offset, size * 0.5f, rotation));
        return result;
    }
}
