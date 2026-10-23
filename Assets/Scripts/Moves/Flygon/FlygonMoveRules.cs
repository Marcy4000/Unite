using System;
using System.Collections.Generic;
using UnityEngine;

public enum FlygonCast : byte { Bite, Dig, Supersonic, Boomburst, Earthquake, Sandstorm, Unite }

// Project-scale tuning. Levels below are zero-based, like Pokemon.CurrentLevel.
public static class FlygonMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Flygon/";
    public const string UniteRecoveryAnimation = "Armature_pm0330_00_ba10_waitA01";
    public const float DigDuration = 4, DigEnter = 0.3f, DigExit = 0.35f, DigRadius = 1.5f;
    public const float WaveSpeed = 7, WaveRange = 7, WaveWidth = 1.6f;
    public const float QuakeRadius = 2.5f, QuakeLeap = 0.45f, QuakeInterval = 0.6f;
    public const float StormRadius = 4.5f, StormDuration = 5, StormInterval = 0.6f;
    public const float UniteFormation = 0.65f, UniteDuration = 6, UniteRadius = 4, UniteCore = 0.9f;
    public const float InputBuffer = 0.2f;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };

    // Sound moves replace Dig (MoveB); ground moves replace Bite (MoveA).
    // These must match MoveAsset.moveType, which also indexes the server's learned moves.
    public static int Slot(FlygonCast kind) => kind == FlygonCast.Unite ? 2 : kind == FlygonCast.Dig || kind == FlygonCast.Supersonic || kind == FlygonCast.Boomburst ? 1 : 0;
    public static bool Mode(FlygonCast kind) => kind == FlygonCast.Dig || kind == FlygonCast.Unite;
    public static bool GroundAim(FlygonCast kind) => kind == FlygonCast.Earthquake || kind == FlygonCast.Sandstorm;
    public static bool DirectionAim(FlygonCast kind) => kind == FlygonCast.Bite || kind == FlygonCast.Supersonic;
    public static float Range(FlygonCast kind) => kind == FlygonCast.Bite ? 3 : kind == FlygonCast.Supersonic ? WaveRange
        : kind == FlygonCast.Earthquake ? 3.5f : kind == FlygonCast.Sandstorm ? StormRadius : kind == FlygonCast.Dig ? DigRadius : kind == FlygonCast.Boomburst ? 5 : UniteRadius;
    public static float Cooldown(FlygonCast kind) => kind == FlygonCast.Bite ? 6 : kind == FlygonCast.Dig ? 8 : kind == FlygonCast.Supersonic ? 7
        : kind == FlygonCast.Boomburst ? 7.5f : kind == FlygonCast.Earthquake ? 9.5f : kind == FlygonCast.Sandstorm ? 11 : 0;
    public static float Recovery(FlygonCast kind) => kind == FlygonCast.Bite ? 0.5f : kind == FlygonCast.Supersonic ? 0.3f
        : kind == FlygonCast.Boomburst ? 0.7f : kind == FlygonCast.Sandstorm ? 0.35f : kind == FlygonCast.Earthquake ? 0.55f : 0;
    public static float Windup(FlygonCast kind) => kind == FlygonCast.Bite ? 0.23f : kind == FlygonCast.Supersonic ? 0.12f
        : kind == FlygonCast.Boomburst ? 0.35f : kind == FlygonCast.Sandstorm ? 0.2f : kind == FlygonCast.Unite ? UniteFormation : DigEnter;
    public static AvailableMoves Move(FlygonCast kind) => kind == FlygonCast.Bite ? AvailableMoves.FlygonBite : kind == FlygonCast.Dig ? AvailableMoves.FlygonDig
        : kind == FlygonCast.Supersonic ? AvailableMoves.FlygonSuperSonic : kind == FlygonCast.Boomburst ? AvailableMoves.FlygonBoomBurst
        : kind == FlygonCast.Earthquake ? AvailableMoves.FlygonEarthquake : kind == FlygonCast.Sandstorm ? AvailableMoves.FlygonSandstorm : AvailableMoves.FlygonUnite;
    public static string Path(FlygonCast kind) => Folder + (kind == FlygonCast.Bite ? "FlygonBiteObject" : kind == FlygonCast.Dig ? "FlygonDig"
        : kind == FlygonCast.Supersonic ? "FlygonSupersonic" : kind == FlygonCast.Boomburst ? "FlygonBoomburst"
        : kind == FlygonCast.Earthquake ? "EarthquakeObject" : kind == FlygonCast.Sandstorm ? "SandstormArea" : "FlygonTornado") + ".prefab";
    public static bool Upgraded(FlygonCast kind, int level) => !Mode(kind) && kind != FlygonCast.Bite && level >= (GroundAim(kind) ? 12 : 10);
    public static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    public static Vector3 Direction(Vector3 value, Vector3 fallback)
    {
        value.y = 0; fallback.y = 0;
        return Finite(value) && value.sqrMagnitude > 0.001f ? value.normalized : fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
    }
    public static Vector3 Destination(Vector3 origin, Vector3 requested, float range)
    {
        if (!Finite(requested)) return origin;
        Vector3 offset = requested - origin; offset.y = 0;
        return origin + Vector3.ClampMagnitude(offset, range);
    }
    public static int TornadoStage(float age) => age < 3 ? 0 : age < 4 ? 1 : age < 5 ? 2 : 3;
    public static float TornadoPower(int stage) => stage <= 0 ? 1 : stage == 1 ? 0.75f : stage == 2 ? 0.5f : 0.25f;
    public static float TornadoLift(int stage) => stage <= 0 ? 1 : stage == 1 ? 0.75f : stage == 2 ? 0.5f : 0.3f;
    public static short TornadoSpeed(int stage) => (short)(stage <= 0 ? 15 : stage == 1 ? 10 : stage == 2 ? 5 : 0);
    public static short SonicSlow(bool upgraded) => (short)(upgraded ? 25 : 20);
    public static short BurstReduction(float distance) => (short)(distance <= 2 ? 15 : distance < 4 ? 10 : 0);
    public static float BurstReductionDuration(bool upgraded) => upgraded ? 4 : 3;
    public static DamageInfo Damage(ulong actor, FlygonCast kind, int stage = 0)
    {
        switch (kind)
        {
            case FlygonCast.Bite: return new DamageInfo(actor, 0.8f, 6, 180, DamageType.Physical);
            case FlygonCast.Dig: return new DamageInfo(actor, 0.6f, 6, 140, DamageType.Physical);
            case FlygonCast.Boomburst: return stage == 0 ? new DamageInfo(actor, 1.2f, 8, 200, DamageType.Physical) : new DamageInfo(actor, 1, 7, 150, DamageType.Physical);
            case FlygonCast.Earthquake: return new DamageInfo(actor, 1.1f + stage * 0.15f, (short)(6 + stage), (short)(110 + stage * 15), DamageType.Physical);
            case FlygonCast.Sandstorm: return new DamageInfo(actor, 0.55f, 4, 50, DamageType.Physical);
            default: return stage == 0 ? new DamageInfo(actor, 1.55f, 8, 200, DamageType.Physical)
                : stage == 1 ? new DamageInfo(actor, 1.4f, 7, 185, DamageType.Physical)
                : stage == 2 ? new DamageInfo(actor, 1.3f, 6, 170, DamageType.Physical) : new DamageInfo(actor, 1.1f, 4, 145, DamageType.Physical);
        }
    }
}

// Released IDs stay reserved until lingering timed effects have expired.
public static class FlygonEffectIds
{
    private static readonly Dictionary<ushort, float> reserved = new Dictionary<ushort, float>();
    public static ushort Acquire()
    {
        for (ushort id = 44000; id < 48000; id++)
            if (!reserved.TryGetValue(id, out float until) || Time.time >= until) { reserved[id] = float.PositiveInfinity; return id; }
        throw new InvalidOperationException("Flygon effect ID pool exhausted.");
    }
    public static void Release(ushort id) { if (id != 0) reserved[id] = Time.time + 6; }
}
