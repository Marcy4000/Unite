using UnityEngine;

/// <summary>Project-unit tuning; formulas use zero-based CurrentLevel. Unite-DB detailed RSB, Oct 2026.</summary>
public static class ZoroarkMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Zoroark/";
    public const string Visuals = "Effects/Zoroark/";
    public const float BasicRange = 2.5f, BasicAngle = 120, UniqueRange = 4.5f;
    public const float BasicWindup = 0.14f, BasicRecovery = 0.16f, InputBuffer = 0.18f;
    public const float SlashRange = 4, FuryRange = 3.5f, ClawRange = 4.5f, CutRange = 4.5f;
    public const float DashRadius = 0.75f, DashDuration = 0.2f, Windup = 0.035f, Recovery = 0.06f;
    public const float ClawEndpointLength = 1, ClawThrow = 0.5f, ClawThrowPlus = 0.6f;
    public const float NightDashRange = 4, NightWindow = 2.5f, NightAirTime = 1.2f, NightInvincible = 1.4f;
    public const float NightAirSteerSpeed = 1.5f, NightAirSteerDistance = 1.5f;
    public const int NightSteps = 6, FeintSteps = 4, UniteEnergy = 80000;
    public const float CrossLength = 6, CrossWidth = 1.5f;
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
        => new DamageInfo(source, boosted ? 1.3f : 1, 0, 0, DamageType.Physical, DamageProprieties.CanCrit | DamageProprieties.IsBasicAttack);
    public static DamageInfo Dash(ulong source, AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.ZoroarkSlash: return new DamageInfo(source, 0.78f, 12, 230, DamageType.Physical);
            case AvailableMoves.ZoroarkFurySwipes: return new DamageInfo(source, 0.94f, 8, 310, DamageType.Physical);
            case AvailableMoves.ZoroarkShadowClaw: return new DamageInfo(source, 1, 12, 297, DamageType.Physical);
            case AvailableMoves.ZoroarkCut: return new DamageInfo(source, 0.9775f, 13, 287, DamageType.Physical);
            case AvailableMoves.ZoroarkNightSlash: return new DamageInfo(source, 0.65f, 10, 225, DamageType.Physical);
            default: return new DamageInfo(source, 1, 10, 300, DamageType.Physical);
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
        return new DamageInfo(actor.NetworkObjectId, 0, 0, (short)Mathf.Min(short.MaxValue, total), DamageType.Physical,
            basic ? DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit : DamageProprieties.None);
    }
    public static float Range(AvailableMoves move) => move == AvailableMoves.ZoroarkSlash ? SlashRange : move == AvailableMoves.ZoroarkFurySwipes ? FuryRange
        : move == AvailableMoves.ZoroarkCut ? CutRange : move == AvailableMoves.ZoroarkShadowClaw ? ClawRange
        : move == AvailableMoves.ZoroarkFeintAttack ? FeintRange : NightDashRange;
}
