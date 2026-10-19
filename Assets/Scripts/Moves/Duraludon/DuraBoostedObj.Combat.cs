using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Persistent attack/mark bookkeeping. Individual casts live on their own
// registered move prefabs, rather than on the boosted-attack object.
public partial class DuraBoostedObj
{
    // Secondary damage must not create another mark or recursively proc itself.
    private const DamageProprieties SecondaryDamage = DamageProprieties.Unused_2;
    private bool combatSubscribed, pulseEnabled, pulseUpgraded;
    private int attackSerial, bonusSerial = -1, pulseBonusAttacks;
    private float pulseBonusExpires;
    private readonly HashSet<ulong> bonusTargets = new HashSet<ulong>();
    private readonly Dictionary<ulong, PulseMark> pulseMarks = new Dictionary<ulong, PulseMark>();
    private readonly Dictionary<ulong, float> tailMarks = new Dictionary<ulong, float>();
    private static readonly Dictionary<ulong, DuraBoostedObj> combats = new Dictionary<ulong, DuraBoostedObj>();
    public int AttackSerial => attackSerial;
    public static bool TryGetCombat(ulong actorId, out DuraBoostedObj combat) => combats.TryGetValue(actorId, out combat) && combat != null;
    private ushort PulseMarkId => (ushort)(20000 + NetworkObjectId % 10000);
    private ushort TailMarkId => (ushort)(30000 + NetworkObjectId % 10000);

    private sealed class PulseMark
    {
        public int count;
        public float expires;
    }

    private void InitializeCombat()
    {
        if (!IsServer || combatSubscribed) return;
        combatSubscribed = true;
        combats[duraludon.NetworkObjectId] = this;
        duraludon.Pokemon.OnDamageDealt += OnCombatDamage;
        duraludon.Pokemon.OnDeath += OnCombatDeath;
    }

    private bool CanHit(Pokemon target)
    {
        return target != null && target.CurrentHp > 0
            && !target.TeamMember.IsOnSameTeam(duraludon.CurrentTeam)
            && !target.HasStatusEffect(StatusType.Invincible)
            && !target.HasStatusEffect(StatusType.Untargetable);
    }

    private List<Pokemon> TargetsAt(Vector3 position, float radius)
    {
        var found = new HashSet<Pokemon>();
        foreach (Collider hit in Physics.OverlapSphere(position, radius))
        {
            // Vision trigger colliders are children: only the actual Pokemon
            // collider counts, and multiple colliders cannot multiply damage.
            if (hit.TryGetComponent(out Pokemon pokemon) && CanHit(pokemon)) found.Add(pokemon);
        }
        return new List<Pokemon>(found);
    }

    private bool TryPokemon(ulong id, out Pokemon pokemon)
    {
        pokemon = null;
        return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj)
            && obj.TryGetComponent(out pokemon);
    }

    private void RefreshSlow(Pokemon target, short amount, float duration, ushort id)
    {
        target.RemoveStatChangeWithIDRPC(id);
        target.AddStatChange(new StatChange(amount, Stat.Speed, duration, true, false, true, id));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void EnableDragonPulseRPC(bool upgraded)
    {
        pulseEnabled = true;
        pulseUpgraded = upgraded;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void BeginBasicAttackRPC()
    {
        attackSerial++;
        bonusTargets.Clear();
    }

    private void OnCombatDamage(ulong targetId, DamageInfo info)
    {
        if (!initialized || !IsServer || (info.proprieties & (SecondaryDamage | DamageProprieties.IsMuscleBand | DamageProprieties.IsHeal)) != 0
            || !TryPokemon(targetId, out Pokemon target)) return;

        // Pulse itself must not immediately replace the marks it just consumed,
        // but it is still eligible to consume Dragon Tail's separate mark.
        if (pulseEnabled && (info.proprieties & DamageProprieties.Unused_1) == 0 && CanHit(target))
        {
            int count = GetMarkCount(target) + 1;
            pulseMarks[targetId] = new PulseMark { count = Mathf.Min(count, DuraMoveRules.MaxMarks), expires = Time.time + DuraMoveRules.MarkDuration };
            var effect = new StatusEffect(StatusType.Scriptable, DuraMoveRules.MarkDuration, true, PulseMarkId);
            if (target.HasStatusEffect(PulseMarkId)) target.UpdateStatusEffectTimeRPC(effect, DuraMoveRules.MarkDuration);
            else target.AddStatusEffect(effect);
            ShowPulseMarksRPC(targetId, pulseMarks[targetId].count);
        }

        if (tailMarks.TryGetValue(targetId, out float expires))
        {
            tailMarks.Remove(targetId);
            if (expires > Time.time && target.HasStatusEffect(TailMarkId))
            {
                target.RemoveStatusEffectWithID(TailMarkId);
                target.TakeDamageRPC(new DamageInfo(duraludon.NetworkObjectId, 0.5f, 0, 0, DamageType.Physical, SecondaryDamage));
            }
        }

        // A normal Cannon attack splashes once around its impact, whereas its
        // boosted beam already hits every target along the line.
        if ((info.proprieties & DamageProprieties.IsBasicAttack) != 0
            && Mathf.Approximately(info.ratio, 1.16f) && info.slider == 3 && info.baseDmg == 132)
        {
            DamageInfo splash = info;
            splash.proprieties |= SecondaryDamage;
            foreach (Pokemon nearby in TargetsAt(target.transform.position, 1.5f))
                if (nearby != target) nearby.TakeDamageRPC(splash);
        }

        if ((info.proprieties & DamageProprieties.IsBasicAttack) == 0 || Time.time >= pulseBonusExpires) return;
        if (bonusSerial != attackSerial)
        {
            if (pulseBonusAttacks <= 0) return;
            pulseBonusAttacks--;
            bonusSerial = attackSerial;
            ReducePulseCooldownRPC(1.5f);
        }
        if (bonusTargets.Add(targetId))
            target.TakeDamageRPC(new DamageInfo(duraludon.NetworkObjectId, 0.15f, 1, 27, DamageType.Physical, SecondaryDamage));
    }

    private int GetMarkCount(Pokemon target)
    {
        if (pulseMarks.TryGetValue(target.NetworkObjectId, out PulseMark mark)
            && mark.expires > Time.time && target.HasStatusEffect(PulseMarkId)) return mark.count;
        pulseMarks.Remove(target.NetworkObjectId);
        return 0;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DragonPulseRPC(float charge)
    {
        if (!initialized || !pulseEnabled) return;
        SpawnMovePrefab(DuraMoveEffect.DragonPulse, duraludon.transform.forward, charge, pulseUpgraded);
    }

    public int ConsumePulseMarks(Pokemon target)
    {
        if (!IsServer) return 0;
        int marks = GetMarkCount(target);
        pulseMarks.Remove(target.NetworkObjectId);
        target.RemoveStatusEffectWithID(PulseMarkId);
        ShowPulseMarksRPC(target.NetworkObjectId, 0);
        return marks;
    }

    public void GrantPulseKnockoutBonus(bool upgraded, int playersKnockedOut)
    {
        if (!IsServer) return;
        pulseBonusAttacks = 3;
        pulseBonusExpires = Time.time + 8f;
        bonusSerial = -1;
        ReducePulseCooldownRPC(DuraMoveRules.PulseCooldown * (1f - duraludon.Pokemon.GetCDR() / 100f) * 0.3f);
        if (upgraded && playersKnockedOut > 0)
            duraludon.Pokemon.HealDamageRPC(Mathf.RoundToInt(duraludon.Pokemon.GetMaxHp() * 0.08f * playersKnockedOut));
    }

    [Rpc(SendTo.Owner)]
    private void ReducePulseCooldownRPC(float amount)
    {
        // On a host the result can arrive inside Finish, before onMoveOver has
        // started the cooldown. Apply on the following frame in either topology.
        StartCoroutine(ReducePulseCooldownNextFrame(amount));
    }

    private IEnumerator ReducePulseCooldownNextFrame(float amount)
    {
        yield return null;
        if (duraludon != null && duraludon.MovesController.GetMove(MoveType.MoveA) is DuraDragonPulse)
            duraludon.MovesController.ReduceMoveCooldown(MoveType.MoveA, amount);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DragonTailRPC(Vector3 direction, bool upgraded)
    {
        SpawnMovePrefab(DuraMoveEffect.DragonTail, direction, upgraded: upgraded);
    }

    public void ApplyTailMark(Pokemon target)
    {
        if (!IsServer) return;
        tailMarks[target.NetworkObjectId] = Time.time + 5f;
        target.RemoveStatusEffectWithID(TailMarkId);
        target.AddStatusEffect(new StatusEffect(StatusType.Scriptable, 5f, true, TailMarkId));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void StealthRockRPC(Vector3 destination)
    {
        if (!initialized) return;
        Vector3 origin = duraludon.transform.position;
        Vector3 offset = destination - origin;
        offset.y = 0;
        destination = origin + Vector3.ClampMagnitude(offset, 7f);
        SpawnMovePrefab(DuraMoveEffect.StealthRock, duraludon.transform.forward, destination: destination);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RevolvingRuinRPC()
    {
        if (!initialized) return;
        SpawnMovePrefab(DuraMoveEffect.RevolvingRuin, duraludon.transform.forward);
    }

    private void OnCombatDeath(DamageInfo info)
    {
        ClearCombat();
    }

    private void ClearCombat()
    {
        prefabGeneration++;
        StopAllCoroutines();
        foreach (var pair in pulseMarks)
        {
            if (TryPokemon(pair.Key, out Pokemon target)) target.RemoveStatusEffectWithID(PulseMarkId);
        }
        pulseMarks.Clear();
        foreach (var pair in tailMarks)
        {
            if (TryPokemon(pair.Key, out Pokemon target)) target.RemoveStatusEffectWithID(TailMarkId);
        }
        tailMarks.Clear();
        pulseBonusAttacks = 0;
        isCasting = false;
        ClearCombatVisualsRPC();
    }

    public override void OnNetworkDespawn()
    {
        prefabGeneration++;
        if (combatSubscribed && duraludon != null)
        {
            duraludon.Pokemon.OnDamageDealt -= OnCombatDamage;
            duraludon.Pokemon.OnDeath -= OnCombatDeath;
            if (combats.TryGetValue(duraludon.NetworkObjectId, out var registered) && registered == this)
                combats.Remove(duraludon.NetworkObjectId);
        }
        DestroyCombatVisuals();
        base.OnNetworkDespawn();
    }
}
