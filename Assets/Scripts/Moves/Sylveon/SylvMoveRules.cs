using UnityEngine;

public static class SylvMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Sylveon/";
    public const ushort FireDebuff = 1400, KissSlow = 1401, EyesAttack = 1402, EyesSlow = 1403;
    public const ushort CalmSpeed = 1404, CalmAttack = 1405, CalmDefense = 1406, CalmShield = 1407;
    public const ushort PassiveAttack = 1408, PassiveDefense = 1409, FrolicInvincible = 1410;
    public const ushort UniteSpeed = 1411, UniteCdr = 1412, UniteShield = 1413;
    public const ushort BoostedSpeed = 1414;
    public const float FireRange = 5, FireLifetime = 5, FireInterval = 0.5f, FireSpeed = 12;
    public const float KissRange = 6, KissBreakRange = 9, KissSpeed = 12;
    public static int FlameCount(bool upgraded) => upgraded ? 5 : 4;
    public static DamageInfo FireDamage(ulong actor, bool repeat)
    {
        var damage = new DamageInfo(actor, 0.8f, 13, 482, DamageType.Special);
        damage.multiplier = repeat ? 0.28f : 1f;
        return damage;
    }
    public static DamageInfo KissDamage(ulong actor) => new DamageInfo(actor, 0.26f, 4, 160, DamageType.Special);
    public static DamageInfo KissHeal(ulong actor, bool upgraded) => new DamageInfo(actor, upgraded ? 0.345f : 0.23f,
        0, (short)(upgraded ? 285 : 190), DamageType.Special, DamageProprieties.IsHeal);
    public static int CalmShieldAmount(int spAttack) => Mathf.FloorToInt(0.9f * spAttack + 100);
    public static bool CanBlock(DamageProprieties flags) =>
        (flags & (DamageProprieties.IsBasicAttack | DamageProprieties.IsHeal | DamageProprieties.IsMuscleBand)) == 0;
}
