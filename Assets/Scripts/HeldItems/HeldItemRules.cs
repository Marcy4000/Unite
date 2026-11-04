using UnityEngine;

public static class HeldItemRules
{
    public const ushort ScoreShieldId = 26000;
    public const ushort AssaultVestId = 26001;
    public const ushort BuddyBarrierId = 26002;
    public const ushort ResonantGuardId = 26003;

    public static bool Primary(DamageInfo damage) => damage.source == DamageSource.Direct && !damage.suppressOnHitEffects
        && !damage.proprieties.HasFlag(DamageProprieties.IsMuscleBand);
    public static bool Basic(DamageInfo damage) => damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack);
    public static int MuscleDamage(int remainingHp, bool player) => player
        ? Mathf.Min(360, Mathf.FloorToInt(remainingHp * 0.03f)) : Mathf.FloorToInt(remainingHp * 0.03f);
    public static int FocusHealing(int maximumHp, int currentHp) => Mathf.FloorToInt(Mathf.Max(0, maximumHp - currentHp) * 0.12f);
    public static int ShellHealing(int specialAttack) => 80 + Mathf.FloorToInt(specialAttack * 0.6f);
    public static int ScoreShield(int hp, int level) => 30 * Mathf.Clamp(level + 1, 1, 15) + Mathf.FloorToInt(hp * 0.1f);
    public static bool AttackBased(Pokemon attacker, DamageInfo damage) => damage.type == DamageType.Physical
        ? !attacker.SwitchAtkStat : damage.type == DamageType.Special && attacker.SwitchAtkStat;
}
