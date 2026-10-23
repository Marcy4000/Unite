using System.Collections.Generic;
using UnityEngine;

public enum MeowsticCast : byte { Scratch, Leer, Reflect, MagicCoat, Psychic, WonderRoom, Unite }
public enum MeowsticEffect : byte { Basic, Boosted, Scratch, Leer, Reflect, Detonation, MagicCoat, Reflection, Psychic, Pulse, WonderRoom, Unite, State, RoomAim, Inversion }

public static class MeowsticMoveRules
{
    public const string VisualFolder = "Assets/Prefabs/Objects/Moves/Meowstic/Male/Rework/";
    public const float PsychicRadius = 3.5f, RoomHalfSize = 3.25f, CoatRadius = 3f, UniteRadius = 5.5f;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public static int Slot(MeowsticCast kind) => kind == MeowsticCast.Unite ? 2 : kind == MeowsticCast.Leer || kind == MeowsticCast.Psychic || kind == MeowsticCast.WonderRoom ? 1 : 0;
    public static float Cooldown(MeowsticCast kind) => kind == MeowsticCast.Scratch ? 6 : kind == MeowsticCast.Leer || kind == MeowsticCast.Psychic ? 7 : kind == MeowsticCast.Reflect ? 8 : kind == MeowsticCast.MagicCoat ? 9 : kind == MeowsticCast.WonderRoom ? 11 : 120;
    public static float Range(MeowsticCast kind) => Support(kind) ? 7 : kind == MeowsticCast.Scratch ? 2.5f : kind == MeowsticCast.Leer ? 3 : kind == MeowsticCast.WonderRoom ? 3.5f : kind == MeowsticCast.Unite ? UniteRadius : CoatRadius;
    public static bool Support(MeowsticCast kind) => kind == MeowsticCast.Psychic || kind == MeowsticCast.Reflect;
    public static float Commitment(MeowsticCast kind) => kind == MeowsticCast.Unite ? 0.35f : kind == MeowsticCast.Scratch ? 0.3f : 0.2f;
    public static bool Upgraded(MeowsticCast kind, int level) => kind != MeowsticCast.Unite && level >= (Slot(kind) == 0 ? 9 : 11);
    public static int EnemyCdr(int count) => Mathf.Clamp(count, 0, 5) * 5;
    public static int ReflectCap(bool upgraded) => upgraded ? 3000 : 2500;
    public static int StoreReflect(int stored, int received, DamageType type, bool upgraded) => type == DamageType.Physical
        ? Mathf.Clamp(stored + Mathf.FloorToInt(Mathf.Max(0, received) * 0.35f), 0, ReflectCap(upgraded)) : stored;
    public static float CoatDuration(bool upgraded) => upgraded ? 5 : 4;
    public static short RoomDefense(bool upgraded) => (short)(upgraded ? 20 : 15);
    public static float PsychicHealingRatio(bool upgraded) => upgraded ? 0.45f : 0.35f;
    public static Vector3 BasicAttackOrigin(Pokemon actor, bool boosted)
    {
        GameObject model = actor.ActiveModel;
        if (model != null)
        {
            string socket = boosted ? "EffHeadCenter01" : "EffHand01";
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == socket) return bone.position;
            return model.transform.position + Vector3.up * (actor.CurrentLevel >= 3 ? 0.7f : 0.35f);
        }
        // Dedicated servers have no animated model. Keep their flight origin
        // close to the same body height, rather than a generic one-metre offset.
        return actor.transform.position + Vector3.up * (actor.CurrentLevel >= 3 ? 0.7f : 0.35f);
    }
    public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    public static Vector3 Direction(Vector3 v, Vector3 fallback)
    {
        v.y = 0; fallback.y = 0;
        return Finite(v) && v.sqrMagnitude > 0.001f ? v.normalized : fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
    }
    public static bool Living(Pokemon target) => target != null && target.IsSpawned && target.CurrentHp > 0;
    public static bool Ally(PlayerManager actor, Pokemon target) => Living(target) && target.Type == PokemonType.Player && target.TeamMember.IsOnSameTeam(actor.CurrentTeam);
    public static bool Enemy(PlayerManager actor, Pokemon target) => Living(target) && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam)
        && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
    public static bool VisibleEnemy(PlayerManager actor, Pokemon target)
    {
        if (!Enemy(actor, target) || target.HasStatusEffect(StatusType.Invisible)) return false;
        if (!target.TryGetComponent(out Vision vision)) return true;
        if (vision.TemporarilyRevealed) return true;
        // IsRendered is observer-local and is not a server visibility authority.
        // Check the casting team's controllers, including shared bush vision.
        foreach (var obj in actor.NetworkManager.SpawnManager.SpawnedObjects.Values)
            if (obj.TryGetComponent(out PlayerManager ally) && Living(ally.Pokemon) && ally.CurrentTeam.IsOnSameTeam(actor.CurrentTeam)
                && !ally.Pokemon.HasStatusEffect(StatusType.VisionObscuring) && ally.VisionController != null
                && ally.VisionController.ContainsVision(vision) && (!vision.IsInBush || (ally.Vision != null && ally.Vision.CurrentBush == vision.CurrentBush))) return true;
        return false;
    }
    public static List<Pokemon> Targets(Vector3 center, float radius)
    {
        return Targets(Physics.OverlapSphere(center, radius, LayerMask.GetMask("Players", "WildPokemons")));
    }
    public static List<Pokemon> Targets(Collider[] hits)
    {
        var unique = new HashSet<Pokemon>();
        foreach (Collider hit in hits)
        {
            Pokemon target = hit.GetComponentInParent<Pokemon>();
            if (Living(target)) unique.Add(target);
        }
        return new List<Pokemon>(unique);
    }
    public static bool InCone(Vector3 origin, Vector3 forward, Vector3 point, float range, float halfAngle = 45)
    {
        Vector3 delta = point - origin; delta.y = 0;
        return delta.sqrMagnitude <= range * range && (delta.sqrMagnitude < 0.01f || Vector3.Dot(Direction(forward, Vector3.forward), delta.normalized) >= Mathf.Cos(halfAngle * Mathf.Deg2Rad));
    }
    public static bool Reflectable(StatusEffect effect) => effect.IsTimed && effect.Duration > 0 && !effect.PeriodicDamage.Enabled
        && (effect.Type == StatusType.Immobilized || effect.Type == StatusType.Incapacitated || effect.Type == StatusType.Asleep || effect.Type == StatusType.Frozen || effect.Type == StatusType.VisionObscuring);
    public static AvailableMoves Move(MeowsticCast kind)
    {
        switch (kind)
        {
            case MeowsticCast.Scratch: return AvailableMoves.MeowsticMScratch;
            case MeowsticCast.Leer: return AvailableMoves.MeowsticMLeer;
            case MeowsticCast.Reflect: return AvailableMoves.MeowsticMReflect;
            case MeowsticCast.MagicCoat: return AvailableMoves.MeowsticMMagicCoat;
            case MeowsticCast.Psychic: return AvailableMoves.MeowsticMPsychic;
            case MeowsticCast.WonderRoom: return AvailableMoves.MeowsticMWonderRoom;
            default: return AvailableMoves.MeowsticMUnite;
        }
    }
}

// Both gameplay and presentation start at their launch point on the first
// frame. Time.deltaTime includes time before the projectile was even created;
// using elapsed flight time avoids a large first-frame leap on slow frames.
public sealed class MeowsticProjectileMotion
{
    public Vector3 Position { get; private set; }
    private float updatedAt;
    public MeowsticProjectileMotion(Vector3 origin, float now) { Position = origin; updatedAt = now; }
    public Vector3 Step(Vector3 target, float now)
    {
        float elapsed = Mathf.Max(0, now - updatedAt);
        updatedAt = Mathf.Max(updatedAt, now);
        Position = Vector3.MoveTowards(Position, target, 20 * elapsed);
        return Position;
    }
}

public sealed class MeowsticEspurrState
{
    private double ready, reducingUntil;
    public StatChange? Filter(StatChange change, double now, bool evolved)
    {
        if (evolved || change.IsBuff || !change.IsTimed || !change.CanBeRemoved) return change;
        if (now >= ready) { ready = now + 23; reducingUntil = now + 3; return null; }
        if (now < reducingUntil) change.Amount = (short)Mathf.RoundToInt(change.Amount * 0.4f);
        return change;
    }
}

public sealed class MeowsticPsychicStacks
{
    private short amount;
    private double expires;
    public short Amount(double now) => now < expires ? amount : (short)0;
    public short Pulse(double now) { amount = (short)Mathf.Min(60, Amount(now) + 20); expires = now + 3; return amount; }
}

public static class MeowsticReflection
{
    private static int depth;
    public static bool Active => depth > 0;
    public static void Run(System.Action reflect)
    {
        depth++;
        try { reflect(); } finally { depth--; }
    }
}
