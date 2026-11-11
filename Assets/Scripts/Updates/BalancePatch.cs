using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unite.Updates
{
    [Serializable]
    public sealed class PokemonBalance
    {
        public string id;
        public string name;
        public int[] maxHp, attack, defense, spAttack, spDefense, critRate, cdr, lifeSteal, speed;
        public float[] atkSpeed, spellVamp;
    }
    [Serializable]
    public sealed class MoveBalance
    {
        public string id;
        public string name;
        public bool overrideCooldown;
        public float cooldown;
        public float upgradedCooldown;
        public int uniteEnergyCost;
    }
    [Serializable]
    public sealed class DamageBalance
    {
        public string id;
        [Tooltip("Authored Attack/Sp. Attack, per-level, and flat expressions, for reference.")]
        public string authoredFormula;
        public bool overrideAttack;
        public float attackCoefficient;
        public bool overrideLevel;
        public int levelDamage;
        public bool overrideBase;
        public int baseDamage;
    }
    [Serializable]
    public sealed class BalanceNumber
    {
        public string id;
        public float value;
    }
    [Serializable]
    public sealed class BalancePatch
    {
        public int schemaVersion = 1;
        public string compatibilityLine;
        public string patchVersion;
        public PokemonBalance[] pokemon = Array.Empty<PokemonBalance>();
        public MoveBalance[] moves = Array.Empty<MoveBalance>();
        public BalanceNumber[] numbers = Array.Empty<BalanceNumber>();
        public DamageBalance[] damage = Array.Empty<DamageBalance>();

        public void Validate(string line, string version)
        {
            if (schemaVersion != 1 || compatibilityLine != line || patchVersion != version)
                throw new InvalidOperationException("Incompatible balance data.");
            if (pokemon == null || moves == null || numbers == null || damage == null) throw new InvalidOperationException("Incomplete balance data.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in pokemon)
            {
                if (p == null) throw new InvalidOperationException("Null Pokémon balance entry.");
                Id(p.id, ids);
                Values(p.maxHp, 0, 10000000); Values(p.attack, 0, 1000000); Values(p.defense, 0, 1000000);
                Values(p.spAttack, 0, 1000000); Values(p.spDefense, 0, 1000000); Values(p.speed, 0, 1000000);
                Values(p.critRate, 0, 100); Values(p.cdr, 0, 99); Values(p.lifeSteal, 0, 100);
                Values(p.atkSpeed, 0, 10000); Values(p.spellVamp, 0, 100);
                int levels = p.maxHp.Length;
                if (p.attack.Length != levels || p.defense.Length != levels || p.spAttack.Length != levels || p.spDefense.Length != levels ||
                    p.critRate.Length != levels || p.cdr.Length != levels || p.lifeSteal.Length != levels || p.speed.Length != levels ||
                    p.atkSpeed.Length != levels || p.spellVamp.Length != levels)
                    throw new InvalidOperationException("Mismatched stat table lengths: " + p.id);
            }
            ids.Clear();
            foreach (var m in moves)
            {
                if (m == null) throw new InvalidOperationException("Null move balance entry.");
                Id(m.id, ids); Number(m.cooldown, 0, 86400); Number(m.upgradedCooldown, 0, 86400);
                Number(m.uniteEnergyCost, 0, 10000000);
            }
            ids.Clear();
            foreach (var d in damage)
            {
                if (d == null) throw new InvalidOperationException("Null damage balance entry.");
                Id(d.id, ids);
                Number(d.attackCoefficient, 0, 1000);
                Number(d.levelDamage, 0, short.MaxValue); Number(d.baseDamage, 0, short.MaxValue);
            }
            ids.Clear();
            foreach (var n in numbers)
            {
                if (n == null) throw new InvalidOperationException("Null numeric balance entry.");
                Id(n.id, ids); Number(n.value, 0, n.id.EndsWith(".ratio", StringComparison.Ordinal) ? 1 : 10000000);
            }
        }
        public void ValidateAgainst(BalancePatch baseline)
        {
            var knownPokemon = new Dictionary<string, PokemonBalance>(StringComparer.Ordinal);
            var knownMoves = new HashSet<string>(StringComparer.Ordinal);
            var knownDamage = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in baseline.pokemon) knownPokemon.Add(entry.id, entry);
            foreach (var entry in baseline.moves) knownMoves.Add(entry.id);
            foreach (var entry in baseline.damage) knownDamage.Add(entry.id);
            foreach (var entry in pokemon)
                if (!knownPokemon.TryGetValue(entry.id, out var authored) || entry.maxHp.Length != authored.maxHp.Length)
                    throw new InvalidOperationException("Balance stat ID/table differs from this player baseline: " + entry.id);
            foreach (var entry in moves)
                if (!knownMoves.Contains(entry.id))
                    throw new InvalidOperationException("Balance move ID is not supported by this player: " + entry.id);
            foreach (var entry in damage)
                if (!knownDamage.Contains(entry.id))
                    throw new InvalidOperationException("Balance damage ID is not supported by this player: " + entry.id);
        }

        private static void Id(string id, HashSet<string> ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidOperationException("Missing or duplicate balance ID: " + id);
        }
        private static void Values(int[] values, float min, float max)
        {
            if (values == null || (values.Length != 15 && values.Length != 22)) throw new InvalidOperationException("Stat tables must contain 15 playable or 22 wild levels.");
            foreach (var value in values) Number(value, min, max);
        }
        private static void Values(float[] values, float min, float max)
        {
            if (values == null || (values.Length != 15 && values.Length != 22)) throw new InvalidOperationException("Stat tables must contain 15 playable or 22 wild levels.");
            foreach (var value in values) Number(value, min, max);
        }
        private static void Number(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new InvalidOperationException("Balance value outside supported range.");
        }
    }

    // A snapshot is installed once at boot. No asset mutation, reflection or mid-match updates.
    public static class BalanceRuntime
    {
        private static Dictionary<string, PokemonBalance> pokemon = new Dictionary<string, PokemonBalance>(StringComparer.Ordinal);
        private static Dictionary<string, MoveBalance> moves = new Dictionary<string, MoveBalance>(StringComparer.Ordinal);
        private static Dictionary<string, float> numbers = new Dictionary<string, float>(StringComparer.Ordinal);
        private static Dictionary<string, DamageBalance> damage = new Dictionary<string, DamageBalance>(StringComparer.Ordinal);
        public static DamageBalance Damage(string id) => id != null && damage.TryGetValue(id, out var value) ? value : null;
        public static PokemonBalance Pokemon(string id) => id != null && pokemon.TryGetValue(id, out var value) ? value : null;
        public static MoveBalance Move(string id) => id != null && moves.TryGetValue(id, out var value) ? value : null;
        public static float Number(string id, float fallback) => numbers.TryGetValue(id, out var value) ? value : fallback;
        public static void Install(BalancePatch patch, string line, string version)
        {
            if (GameRelease.Ready) throw new InvalidOperationException("Balance is pinned until the next boot.");
            patch.Validate(line, version);
            // Detach from the caller so editing a deserialized patch cannot change the installed snapshot.
            var copy = JsonUtility.FromJson<BalancePatch>(JsonUtility.ToJson(patch));
            var p = new Dictionary<string, PokemonBalance>(StringComparer.Ordinal);
            var m = new Dictionary<string, MoveBalance>(StringComparer.Ordinal);
            var n = new Dictionary<string, float>(StringComparer.Ordinal);
            var d = new Dictionary<string, DamageBalance>(StringComparer.Ordinal);
            foreach (var entry in copy.damage) d.Add(entry.id, entry);
            foreach (var entry in copy.pokemon) p.Add(entry.id, entry);
            foreach (var entry in copy.moves) m.Add(entry.id, entry);
            foreach (var entry in copy.numbers) n.Add(entry.id, entry.value);
            pokemon = p; moves = m; numbers = n; damage = d;
        }
        public static void Reset() { pokemon.Clear(); moves.Clear(); numbers.Clear(); damage.Clear(); }
    }
}
