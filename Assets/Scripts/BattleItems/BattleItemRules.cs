using UnityEngine;

public static class BattleItemRules
{
    public const float EjectRange = 5f;
    public const float SmokeRadius = 3f;
    public const float TargetRange = 6f;
    public const ushort XSpeedId = 64800;
    public const ushort SmokeId = 64801;
    public const ushort DollId = 64802;
    public const ushort TailId = 64803;
    public const ushort AttackId = 64804;
    public const ushort FullHealId = 64805;

    public static string Name(AvailableBattleItems kind)
    {
        switch (kind)
        {
            case AvailableBattleItems.Eject: return "Eject Button";
            case AvailableBattleItems.XSpeed: return "X Speed";
            case AvailableBattleItems.XAttack: return "X Attack";
            case AvailableBattleItems.FullHeal: return "Full Heal";
            case AvailableBattleItems.GoalGetter: return "Goal Getter";
            case AvailableBattleItems.Potion: return "Potion";
            case AvailableBattleItems.SlowSmoke: return "Slow Smoke";
            case AvailableBattleItems.ShedinjaDoll: return "Shedinja Doll";
            case AvailableBattleItems.FluffyTail: return "Fluffy Tail";
            case AvailableBattleItems.GoalHacker: return "Goal Hacker";
            case AvailableBattleItems.Brick: return "Brick";
            default: return "None";
        }
    }

    public static float Cooldown(AvailableBattleItems kind)
    {
        switch (kind)
        {
            case AvailableBattleItems.Eject: return 80;
            case AvailableBattleItems.XSpeed: return 50;
            case AvailableBattleItems.XAttack: return 40;
            case AvailableBattleItems.FullHeal: return 60;
            case AvailableBattleItems.GoalGetter: return 60;
            case AvailableBattleItems.Potion: return 30;
            case AvailableBattleItems.SlowSmoke: return 35;
            case AvailableBattleItems.ShedinjaDoll: return 45;
            case AvailableBattleItems.FluffyTail: return 22;
            case AvailableBattleItems.GoalHacker: return 80;
            case AvailableBattleItems.Brick: return 55;
            default: return 0;
        }
    }

    public static int PotionHealing(int maxHp) => Mathf.RoundToInt(160 + Mathf.Max(0, maxHp) * 0.2f);
    public static int TailDamage(int attack, int specialAttack, int level) => Mathf.FloorToInt(100 + attack + 0.6f * specialAttack + 10 * Mathf.Max(0, level - 1));
    public static bool Finite(Vector3 direction) => float.IsFinite(direction.x) && float.IsFinite(direction.y) && float.IsFinite(direction.z);
    public static bool IsSlow(StatChange change) => change.AffectedStat == Stat.Speed && !change.IsBuff;
}
