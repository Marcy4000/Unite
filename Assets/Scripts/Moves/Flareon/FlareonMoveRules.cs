using System.Collections.Generic;
using UnityEngine;

public enum FlareonCast { Swift, Tackle, HeatWave, Takedown, Flamethrower, FlareBlitz, Unite, Slam, Basic, State }

public static class FlareonMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Flareon/";
    public const float BasicRange = 2.5f, HeatRadius = 3.7f, SwiftRadius = 2f, InputBuffer = 0.2f;
    public const ushort AttackBuff = 22100, EeveeBuff = 22101, SpeedBuff = 22102, CastProtection = 22103;
    public const ushort FlameShield = 22104, SlamShield = 22105, UniteShield = 22106, BurnId = 22107, LaunchId = 22108;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public static readonly Vector3 FlameCenter = new Vector3(0, 0.6f, 2.29f), FlameSize = new Vector3(1.2f, 1.2f, 4.42f);
    public static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    public static Vector3 Direction(Vector3 value, Vector3 fallback)
    {
        value.y = 0; fallback.y = 0;
        return value.sqrMagnitude > 0.001f ? value.normalized : fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
    }
    public static string Path(FlareonCast kind) => Folder + "Flareon" + kind + "Object.prefab";
    public static bool Targeted(FlareonCast kind) => kind == FlareonCast.Takedown || kind == FlareonCast.Slam || kind == FlareonCast.Unite;
    public static bool Driven(FlareonCast kind) => kind == FlareonCast.Tackle || kind == FlareonCast.FlareBlitz || Targeted(kind);
    public static int Slot(FlareonCast kind) => kind == FlareonCast.Unite ? 2 : kind == FlareonCast.Tackle || kind == FlareonCast.Flamethrower || kind == FlareonCast.FlareBlitz ? 1 : 0;
    public static float Range(FlareonCast kind)
    {
        switch (kind)
        {
            case FlareonCast.Tackle: return 3;
            case FlareonCast.FlareBlitz: return 6;
            case FlareonCast.Takedown: return 4.5f;
            case FlareonCast.Slam: return 5.5f;
            case FlareonCast.Unite: return 5;
            case FlareonCast.Flamethrower: return 4.42f;
            case FlareonCast.HeatWave: return HeatRadius;
            default: return SwiftRadius;
        }
    }
    public static float Cooldown(FlareonCast kind) => kind == FlareonCast.Tackle ? 6 : kind == FlareonCast.HeatWave ? 9 : kind == FlareonCast.Takedown ? 7 : kind == FlareonCast.Unite || kind == FlareonCast.Slam ? 0 : 7.5f;
    public static float Windup(FlareonCast kind) => kind == FlareonCast.Unite ? 0.2f : kind == FlareonCast.FlareBlitz ? 0.1f : 0;
    public static float DriveTime(FlareonCast kind) => kind == FlareonCast.Tackle ? 0.2f : kind == FlareonCast.FlareBlitz ? 0.42f : kind == FlareonCast.Takedown ? 0.3f : 0.3f;
    public static float Recovery(FlareonCast kind) => Driven(kind) ? Windup(kind) + DriveTime(kind) + 0.08f : kind == FlareonCast.Flamethrower ? 0.65f : 0.18f;
    public static float LaunchDuration(bool upgraded) => upgraded ? 1.4f : 1.2f;
    public static int HeatPulses(bool upgraded) => upgraded ? 10 : 8;
    public static float HeatInterval(bool upgraded) => upgraded ? 0.6f : 0.75f;
    public static bool Upgraded(FlareonCast kind, int level) => level >= (Slot(kind) == 0 ? 9 : 11);
    public static AvailableMoves Move(FlareonCast kind)
    {
        switch (kind)
        {
            case FlareonCast.Swift: return AvailableMoves.FlareonSwift;
            case FlareonCast.Tackle: return AvailableMoves.FlareonTackle;
            case FlareonCast.HeatWave: return AvailableMoves.FlareonHeatWave;
            case FlareonCast.Takedown: case FlareonCast.Slam: return AvailableMoves.FlareonTakedown;
            case FlareonCast.Flamethrower: return AvailableMoves.FlareonFlameThrower;
            case FlareonCast.FlareBlitz: return AvailableMoves.FlareonFlareBlitz;
            default: return AvailableMoves.FlareonUnite;
        }
    }
    public static DamageInfo Damage(ulong source, FlareonCast kind, bool upgraded = false, bool burned = false, bool boosted = false)
    {
        switch (kind)
        {
            case FlareonCast.Swift: return new DamageInfo(source, 1f, 4, 150, DamageType.Physical, DamageProprieties.CanCrit);
            case FlareonCast.Tackle: return new DamageInfo(source, 0.8f, 3, 300, DamageType.Physical);
            case FlareonCast.HeatWave: return new DamageInfo(source, burned ? 1.3f : 1.25f, (short)(burned ? 9 : 8), (short)(burned ? 250 : 200), DamageType.Physical, DamageProprieties.CanCrit);
            case FlareonCast.Takedown: return new DamageInfo(source, burned ? 2.1f : 1.7f, (short)(burned ? 7 : 6), (short)(burned ? 450 : 400), DamageType.Physical, DamageProprieties.CanCrit);
            case FlareonCast.Slam: return new DamageInfo(source, 1.7f, 6, 400, DamageType.Physical, DamageProprieties.CanCrit);
            case FlareonCast.Flamethrower: return new DamageInfo(source, upgraded ? 2f : 1.86f, 9, (short)(upgraded ? 500 : 480), DamageType.Physical, DamageProprieties.CanCrit);
            case FlareonCast.FlareBlitz: return new DamageInfo(source, 1.8f, 7, 410, DamageType.Physical);
            case FlareonCast.Unite: return new DamageInfo(source, 2.5f, 7, 510, DamageType.Physical, DamageProprieties.CanCrit | DamageProprieties.IsUniteMove);
            default: return new DamageInfo(source, boosted ? 1.3f : 1f, (short)(boosted ? 3 : 2), (short)(boosted ? 120 : 100), DamageType.Physical, DamageProprieties.CanCrit | DamageProprieties.IsBasicAttack);
        }
    }
    public static void Burn(Pokemon target, ulong source, float duration)
    {
        target.AddStatusEffect(StatusEffect.DamageOverTime(StatusType.Burned,
            new DamageInfo(source, 0.2f, 0, 0, DamageType.Physical), duration, 1, BurnId, 0.01f, 200));
    }
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && target != null && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
    public static bool CanControl(PlayerManager actor, Pokemon target) => CanHit(actor, target) && !target.HasStatusEffect(StatusType.Unstoppable);
    public static HashSet<Pokemon> Targets(Collider[] colliders)
    {
        var result = new HashSet<Pokemon>();
        foreach (Collider collider in colliders) if (collider.TryGetComponent(out Pokemon target)) result.Add(target);
        return result;
    }
}

public sealed class FlareonComboState
{
    public int AttackCharge { get; private set; }
    public int Stacks { get; private set; }
    public double AttackExpires { get; private set; }
    public double StacksExpire { get; private set; }
    public void Tick(double now)
    {
        if (now >= AttackExpires) AttackCharge = 0;
        if (now >= StacksExpire) Stacks = 0;
    }
    public bool ConfirmBasic(double now)
    {
        Tick(now); bool boosted = AttackCharge == 2;
        AttackCharge = boosted ? 0 : AttackCharge + 1; AttackExpires = now + 5; return boosted;
    }
    public void AddStack(double now) { Tick(now); Stacks = Mathf.Min(3, Stacks + 1); StacksExpire = now + 5; }
    public void Clear() { AttackCharge = Stacks = 0; AttackExpires = StacksExpire = 0; }
}
