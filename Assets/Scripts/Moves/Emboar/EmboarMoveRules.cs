using System;
using System.Collections.Generic;
using UnityEngine;

public enum EmboarCast : byte { Ember, Smog, FirePledge, Flamethrower, HeatCrash, FlameCharge, Unite, Impact, Reckless }

// Distances are project world units; levels are zero-based, like Pokemon.CurrentLevel.
public static class EmboarMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Emboar/";
    public const float InputBuffer = 0.15f, PledgeDuration = 5, RecklessDuration = 15, RecklessRearm = 5;
    public const float CrashLeap = 0.45f, CrashRadius = 2.4f, ChargeDuration = 1.15f, ChargeRamp = 0.4f;
    public const float UniteWindup = 0.2f, UniteDrive = 0.3f, UniteRadius = 3.5f, UniteInnerRadius = 2.4f;
    public const ushort EarlyDefense = 64501, PledgeSpeed = 64502, PledgeAttackSpeed = 64503, UniteShield = 64504, Immunity = 64505, FlameShield = 64506;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public static int Slot(EmboarCast kind) => kind == EmboarCast.Unite ? 2 : kind == EmboarCast.Smog || kind == EmboarCast.HeatCrash || kind == EmboarCast.FlameCharge ? 1 : 0;
    public static bool Driven(EmboarCast kind) => kind == EmboarCast.Smog || kind == EmboarCast.HeatCrash || kind == EmboarCast.FlameCharge || kind == EmboarCast.Unite;
    public static bool ConsumesReckless(EmboarCast kind) => kind == EmboarCast.HeatCrash || kind == EmboarCast.FlameCharge;
    public static bool Upgraded(EmboarCast kind, int level) => kind >= EmboarCast.FirePledge && kind <= EmboarCast.FlameCharge && level >= (Slot(kind) == 0 ? 10 : 12);
    public static float Cooldown(EmboarCast kind, bool upgraded = false) => kind == EmboarCast.Ember ? 6.5f : kind == EmboarCast.Smog ? 8
        : kind == EmboarCast.FirePledge ? upgraded ? 7 : 8 : kind == EmboarCast.Flamethrower ? 7 : kind == EmboarCast.HeatCrash ? 8 : kind == EmboarCast.FlameCharge ? 9 : 0;
    public static float Range(EmboarCast kind) => kind == EmboarCast.Ember ? 5.5f : kind == EmboarCast.Smog ? 3.5f : kind == EmboarCast.Flamethrower ? 6
        : kind == EmboarCast.HeatCrash ? 3.5f : kind == EmboarCast.FlameCharge ? 10.5f : kind == EmboarCast.Unite ? 5 : 0;
    public static float Recovery(EmboarCast kind) => kind == EmboarCast.Ember ? 0.38f : kind == EmboarCast.Smog ? 0.35f : kind == EmboarCast.FirePledge ? 0.06f
        : kind == EmboarCast.Flamethrower ? 0.5f : kind == EmboarCast.HeatCrash ? 0.62f : kind == EmboarCast.Unite ? 0.78f : ChargeDuration + 0.08f;
    public static AvailableMoves Move(EmboarCast kind) => kind == EmboarCast.Ember ? AvailableMoves.EmboarEmber : kind == EmboarCast.Smog ? AvailableMoves.EmboarSmog
        : kind == EmboarCast.FirePledge ? AvailableMoves.EmboarFirePledge : kind == EmboarCast.Flamethrower ? AvailableMoves.EmboarReversal
        : kind == EmboarCast.HeatCrash ? AvailableMoves.EmboarHeatCrash : kind == EmboarCast.FlameCharge ? AvailableMoves.EmboarFlameCharge : AvailableMoves.EmboarUnite;
    public static string Path(EmboarCast kind) => Folder + "Emboar" + kind + "Object.prefab";
    public static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    public static Vector3 Direction(Vector3 value, Vector3 fallback)
    {
        value.y = 0; fallback.y = 0;
        return Finite(value) && value.sqrMagnitude > 0.001f ? value.normalized : fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
    }
    public static Vector3 Destination(Vector3 origin, Vector3 requested, float range)
    {
        if (!Finite(requested)) return origin;
        Vector3 delta = requested - origin; delta.y = 0;
        return origin + Vector3.ClampMagnitude(delta, range);
    }
    public static float BasicRange(int level) => level >= 6 ? 3.5f : level >= 4 ? 3 : 2.5f;
    public static float ChargeSpeed(float age, bool empowered) => Mathf.Lerp(5, 9, empowered ? 1 : Mathf.Clamp01(age / ChargeRamp));
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && target != null && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
    public static bool CanControl(PlayerManager actor, Pokemon target) => CanHit(actor, target) && target.Type != PokemonType.Objective && !target.HasStatusEffect(StatusType.Unstoppable);
    public static HashSet<Pokemon> Targets(Collider[] colliders)
    {
        var result = new HashSet<Pokemon>();
        foreach (Collider collider in colliders)
        {
            Pokemon target = collider.GetComponentInParent<Pokemon>();
            if (target != null) result.Add(target);
        }
        return result;
    }
    public static DamageInfo Damage(ulong actor, EmboarCast kind, int stage = 0)
    {
        switch (kind)
        {
            case EmboarCast.Ember: return new DamageInfo(actor, 1.2f, 7, 155, DamageType.Physical, DamageProprieties.CanCrit);
            case EmboarCast.Flamethrower: return new DamageInfo(actor, 1.86f, 9, 480, DamageType.Physical);
            case EmboarCast.HeatCrash: return new DamageInfo(actor, 1.2f + stage * 0.3f, (short)(6 + stage), (short)(210 + stage * 65), DamageType.Physical);
            case EmboarCast.FlameCharge: return new DamageInfo(actor, 1.8f, 6, 300, DamageType.Physical);
            case EmboarCast.Unite: return new DamageInfo(actor, stage == 0 ? 0.85f : 2.55f, 0, (short)(stage == 0 ? 160 : 490), DamageType.Physical, DamageProprieties.IsUniteMove);
            default: return new DamageInfo(actor, stage == 0 ? 0.4f : 0.6f, (short)(stage == 0 ? 5 : 7), (short)(stage == 0 ? 120 : 160), DamageType.Physical, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit);
        }
    }
}

// These state machines are also used directly by the tests, not duplicated test logic.
public sealed class EmboarRecklessState
{
    public int Charge { get; private set; }
    public double ReadyUntil { get; private set; }
    public double RearmUntil { get; private set; }
    public bool Ready(double now) => Charge == 100 && now < ReadyUntil;
    public void Tick(double now)
    {
        if (Charge == 100 && now >= ReadyUntil) { Charge = 0; RearmUntil = ReadyUntil + EmboarMoveRules.RecklessRearm; ReadyUntil = 0; }
    }
    public void Add(int amount, double now)
    {
        Tick(now);
        if (now < RearmUntil || Ready(now) || amount <= 0) return;
        Charge = Mathf.Clamp(Charge + amount, 0, 100);
        if (Charge == 100) ReadyUntil = now + EmboarMoveRules.RecklessDuration;
    }
    public bool Consume(EmboarCast kind, double now)
    {
        Tick(now);
        if (!EmboarMoveRules.ConsumesReckless(kind) || !Ready(now)) return false;
        Clear(); RearmUntil = now + EmboarMoveRules.RecklessRearm; return true;
    }
    public void Fill(double now) { Charge = 100; ReadyUntil = now + EmboarMoveRules.RecklessDuration; RearmUntil = 0; }
    public void Clear() { Charge = 0; ReadyUntil = RearmUntil = 0; }
}

public sealed class EmboarAttackState
{
    public int Charge { get; private set; }
    public int PledgeHits { get; private set; }
    public double PledgeUntil { get; private set; }
    public void Arm(double now) { PledgeHits = 3; PledgeUntil = now + EmboarMoveRules.PledgeDuration; }
    public void Tick(double now) { if (now >= PledgeUntil) { PledgeHits = 0; PledgeUntil = 0; } }
    public bool Boosted(double now) { Tick(now); return PledgeHits > 0 || Charge == 2; }
    public bool Launcher(double now) { Tick(now); return PledgeHits == 1; }
    public void Confirm(double now)
    {
        Tick(now);
        bool boosted = PledgeHits > 0 || Charge == 2;
        if (PledgeHits > 0) { PledgeHits--; if (PledgeHits == 0) PledgeUntil = 0; }
        Charge = boosted ? 0 : Charge + 1;
    }
    public void Clear() { Charge = PledgeHits = 0; PledgeUntil = 0; }
}

public static class EmboarEffectIds
{
    private static readonly Dictionary<ushort, float> reserved = new Dictionary<ushort, float>();
    public static ushort Acquire()
    {
        for (ushort id = 64000; id < 64500; id++)
            if (!reserved.TryGetValue(id, out float until) || Time.time >= until) { reserved[id] = float.PositiveInfinity; return id; }
        throw new InvalidOperationException("Emboar effect ID pool exhausted.");
    }
    public static void Release(ushort id) { if (id != 0) reserved[id] = Time.time + 6; }
}
