using System;
using System.Collections.Generic;
using UnityEngine;

public enum CinderCast { Ember, LowSweep, PyroBall, BlazeKick, FlameCharge, Feint, Unite, Basic, Cinders, Burn, Blaze }

// Unite-DB detailed notes, retrieved October 23, 2026. Levels are zero-based.
public static class CinderMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Cinderace/";
    public const float MarkDuration = 5f, DashDistance = 5f, DashDuration = 0.2f;
    public const float KickApproach = 0.25f, KickRebound = 0.25f, ReboundDistance = 3f;
    public const ushort BlazeCrit = 8150, BlazeSpeed = 8151, FeintSpeed = 8152, FeintInvincible = 8153;
    public const ushort KickImmune = 8154, KickSpeed = 8155, UniteImmune = 8156, UniteSpeed = 8157;
    public const ushort UniteAttackSpeed = 8158, UniteShield = 8159, UniteHitSpeed = 8160, FlameSlow = 8161;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public static string Path(CinderCast kind) => Folder + "Cinder" + kind + "Object.prefab";
    public static bool Targeted(CinderCast kind) => kind == CinderCast.Ember || kind == CinderCast.BlazeKick || kind == CinderCast.Unite;
    public static bool Dash(CinderCast kind) => kind == CinderCast.LowSweep || kind == CinderCast.FlameCharge;
    public static int Slot(CinderCast kind) => kind == CinderCast.Unite ? 2 : kind == CinderCast.LowSweep || kind == CinderCast.FlameCharge || kind == CinderCast.Feint ? 1 : 0;
    public static float Range(CinderCast kind) => kind == CinderCast.Unite ? 11f : Dash(kind) ? DashDistance : kind == CinderCast.BlazeKick ? 7f : 8f;
    public static float Cooldown(CinderCast kind)
    {
        switch (kind)
        {
            case CinderCast.Ember: return 6f;
            case CinderCast.LowSweep: return 7f;
            case CinderCast.PyroBall: return 4.5f;
            case CinderCast.BlazeKick: case CinderCast.Feint: return 9.5f;
            case CinderCast.FlameCharge: return 5f;
            default: return 0f;
        }
    }
    public static float Windup(CinderCast kind) => kind == CinderCast.PyroBall ? 0.4667f : kind == CinderCast.Unite ? 0.65f : kind == CinderCast.Ember ? 0.16f : 0f;
    public static float Recovery(CinderCast kind) => kind == CinderCast.PyroBall ? 1.1f : kind == CinderCast.Unite ? 1f : kind == CinderCast.Feint ? 0.73f : 0.4f;
    public static bool Upgraded(CinderCast kind, int level) => level >= (Slot(kind) == 0 ? 10 : 12);
    public static AvailableMoves Move(CinderCast kind)
    {
        switch (kind)
        {
            case CinderCast.Ember: return AvailableMoves.CinderEmber;
            case CinderCast.LowSweep: return AvailableMoves.CinderLowSweep;
            case CinderCast.PyroBall: return AvailableMoves.CinderPyroball;
            case CinderCast.BlazeKick: return AvailableMoves.CinderBlazeKick;
            case CinderCast.FlameCharge: return AvailableMoves.CinderFlameCharge;
            case CinderCast.Feint: return AvailableMoves.CinderFeint;
            default: return AvailableMoves.BlazingBycicleKick;
        }
    }
    public static DamageInfo Damage(ulong actor, CinderCast kind, bool boosted = false)
    {
        var flags = DamageProprieties.Unused_1; // Direct move or boosted basic: two cinders.
        switch (kind)
        {
            case CinderCast.Ember: return new DamageInfo(actor, 1.15f, 8, 155, DamageType.Physical, flags);
            case CinderCast.LowSweep: return new DamageInfo(actor, 0.36f, 3, 100, DamageType.Physical, flags);
            case CinderCast.PyroBall: return new DamageInfo(actor, 3.45f, 32, 820, DamageType.Physical, flags);
            case CinderCast.BlazeKick: return new DamageInfo(actor, 0.48f, 4, 120, DamageType.Physical, flags | DamageProprieties.WasCriticalHit);
            case CinderCast.FlameCharge: return new DamageInfo(actor, 0.47f, 3, 130, DamageType.Physical, flags);
            case CinderCast.Unite: return new DamageInfo(actor, 2.47f, 14, 670, DamageType.Physical, flags | DamageProprieties.IsUniteMove);
            default: return new DamageInfo(actor, boosted ? 1.4f : 1f, 0, 0, DamageType.Physical,
                DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit | (boosted ? flags : DamageProprieties.None));
        }
    }
    public static int FlareDamage(int attack, int level, int maxHp, bool wild)
    {
        int damage = Mathf.FloorToInt(0.9f * attack + 25 + 0.007f * Mathf.Max(0, level) * Mathf.Max(0, maxHp));
        return wild ? Mathf.Min(500, damage) : damage;
    }
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && target != null && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
    public static Vector3 Direction(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : fallback;
    }
    // Reserved pool: managed burns bypass PlayerManager's legacy generic DoT.
    public static bool ManagedBurn(StatusEffect effect) => effect.Type == StatusType.Burned && effect.ID >= 15000 && effect.ID < 20000;
}

public sealed class CinderStackState
{
    public int Count { get; private set; }
    public float Expires { get; private set; }
    public int Get(float now) => now < Expires ? Count : 0;
    public bool Add(int amount, float now)
    {
        Count = Get(now) + Mathf.Clamp(amount, 1, 2);
        Expires = now + CinderMoveRules.MarkDuration;
        if (Count < 5) return false;
        Count = 0; Expires = 0; return true;
    }
    public void Clear() { Count = 0; Expires = 0; }
}

public static class CinderEffectIds
{
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    public static ushort Acquire()
    {
        for (ushort id = 15000; id < 20000; id++) if (used.Add(id)) return id;
        throw new InvalidOperationException("Cinderace effect ID pool exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
}
