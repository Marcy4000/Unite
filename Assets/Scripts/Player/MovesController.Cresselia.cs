using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private readonly NetworkVariable<double> cresseliaBoostReadyAt = new NetworkVariable<double>();
    private readonly NetworkVariable<bool> cresseliaEscort = new NetworkVariable<bool>();
    private readonly float[] cresseliaNextCast = new float[2];
    private readonly List<CresseliaShieldLease> cresseliaShields = new List<CresseliaShieldLease>();
    private float cresseliaNextBasic, cresseliaNextEscortScan;
    private bool cresseliaBound;
    private ushort cresseliaAuroraDebuff, cresseliaMoonblastDebuff;
    public bool CresseliaEscorting => cresseliaEscort.Value;
    public float CresseliaBoostRemaining => (float)Math.Max(0, cresseliaBoostReadyAt.Value - CresseliaNow);
    private double CresseliaNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsLivingCresselia => pokemon != null && pokemon.BaseStats != null && pokemon.CurrentHp > 0
        && pokemon.BaseStats.Passive == AvailablePassives.CresseliaPassive;

    private void UpdateCresseliaServer()
    {
        if (!IsServer || pokemon == null || pokemon.BaseStats == null || pokemon.BaseStats.Passive != AvailablePassives.CresseliaPassive) return;
        if (!cresseliaBound)
        {
            cresseliaBound = true;
            cresseliaAuroraDebuff = CresseliaEffectIds.Acquire();
            cresseliaMoonblastDebuff = CresseliaEffectIds.Acquire();
            pokemon.OnDeath += OnCresseliaDeath;
        }
        if (Time.time < cresseliaNextEscortScan) return;
        cresseliaNextEscortScan = Time.time + 0.2f;
        bool escort = false;
        if (IsLivingCresselia)
            foreach (Pokemon ally in CresseliaMoveRuntime.Targets(Physics.OverlapSphere(transform.position, 6f, LayerMask.GetMask("Players"))))
                if (ally != pokemon && CresseliaMoveRuntime.Living(ally) && ally.TeamMember.IsOnSameTeam(playerManager.CurrentTeam)) { escort = true; break; }
        if (escort == cresseliaEscort.Value) return;
        cresseliaEscort.Value = escort;
        pokemon.RemoveAllStatChangeWithIDRPC(CresseliaMoveRules.EscortId);
        if (escort) pokemon.AddStatChange(new StatChange(20, Stat.Speed, 0, false, true, true, CresseliaMoveRules.EscortId));
    }

    private void OnCresseliaDeath(DamageInfo damage)
    {
        cresseliaEscort.Value = false;
        cresseliaNextEscortScan = 0;
        pokemon.RemoveAllStatChangeWithIDRPC(CresseliaMoveRules.EscortId);
        EndCresseliaUniteServer(true);
        ClearCresseliaBufferedInput();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastCresseliaRPC(CresseliaCast move, Vector3 direction, Vector3 destination, ulong targetId, bool hasTarget)
    {
        ExecuteCresseliaCast(move, direction, destination, targetId, hasTarget);
    }

    private void ExecuteCresseliaCast(CresseliaCast move, Vector3 direction, Vector3 destination, ulong targetId, bool hasTarget)
    {
        if (!IsServer) return;
        UpdateCresseliaServer();
        if (!IsLivingCresselia || !Enum.IsDefined(typeof(CresseliaCast), move) || cresseliaUniteAirborne
            || pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts)) return;
        bool starter = move == CresseliaCast.AuroraBeam || move == CresseliaCast.Safeguard;
        if (!starter && pokemon.CurrentLevel < (CresseliaMoveRules.Slot(move) == 0 ? 4 : 6)) return;
        int slot = CresseliaMoveRules.Slot(move);
        if (Time.time + 0.12f < cresseliaNextCast[slot]) return;
        cresseliaNextCast[slot] = Time.time + CresseliaMoveRules.Cooldown(move) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f);
        direction = CresseliaMoveRules.Direction(direction, transform.forward);
        bool upgraded = CresseliaMoveRules.Upgraded(move, pokemon.CurrentLevel);
        if (CresseliaMoveRules.IsSupport(move))
        {
            Pokemon ally = null;
            if (hasTarget && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
                && obj.TryGetComponent(out PlayerManager manager) && manager != playerManager
                && CresseliaMoveRuntime.Living(manager.Pokemon) && manager.CurrentTeam.IsOnSameTeam(playerManager.CurrentTeam)
                && (manager.transform.position - transform.position).sqrMagnitude <= 8.5f * 8.5f) ally = manager.Pokemon;
            ApplyCresseliaProtection(move, ally, upgraded);
        }
        else if (move == CresseliaCast.Moonlight)
        {
            if (!CresseliaMoveRules.Finite(destination)) destination = transform.position;
            Vector3 offset = destination - transform.position; offset.y = 0;
            destination = transform.position + Vector3.ClampMagnitude(offset, CresseliaMoveRules.Range(move));
            if (!playerManager.PlayerMovement.TryCresseliaLanding(destination, out destination)) destination = transform.position;
            StartCoroutine(CresseliaMoonlightField(destination, upgraded));
        }
        else if (move == CresseliaCast.Psybeam) CresseliaPsybeamHit(direction, upgraded);
        else StartCoroutine(CresseliaProjectile(move, direction, upgraded));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CresseliaBasicRPC(ulong targetId)
    {
        ExecuteCresseliaBasic(targetId);
    }

    private void ExecuteCresseliaBasic(ulong targetId)
    {
        if (!IsServer) return;
        if (!IsLivingCresselia || cresseliaUniteAirborne || pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts)
            || Time.time + 0.08f < cresseliaNextBasic || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
            || !obj.TryGetComponent(out Pokemon target) || !CresseliaMoveRuntime.CanHit(playerManager, target)
            || (target.transform.position - transform.position).sqrMagnitude > 4.75f * 4.75f) return;
        cresseliaNextBasic = Time.time + GetAtkSpeedCooldown();
        bool boosted = CresseliaBoostRemaining <= 0;
        if (boosted) cresseliaBoostReadyAt.Value = CresseliaNow + CresseliaMoveRules.BoostCooldown;
        playerManager.AnimationManager.SetFloat("CresseliaActionSpeed", 3f);
        playerManager.AnimationManager.PlayAnimation(boosted ? "pm0488_ba21_tokusyu02" : "pm0488_ba20_buturi01");
        StartCoroutine(CresseliaHomingAttack(target, boosted));
    }

    private IEnumerator CresseliaHomingAttack(Pokemon target, bool boosted)
    {
        Vector3 position = transform.position + Vector3.up * 0.8f;
        int visual = ShowCresseliaEffect(boosted ? CresseliaEffect.Boosted : CresseliaEffect.Basic,
            position, transform.forward, 3f, target.NetworkObjectId);
        float expires = Time.time + 3;
        while (IsSpawned && CresseliaMoveRuntime.CanHit(playerManager, target) && Time.time < expires)
        {
            Vector3 goal = target.transform.position + Vector3.up * 0.7f;
            Vector3 next = Vector3.MoveTowards(position, goal, CresseliaMoveRules.BasicSpeed * Time.deltaTime);
            if ((next - goal).sqrMagnitude <= 0.25f * 0.25f)
            {
                target.TakeDamageRPC(CresseliaMoveRules.BasicDamage(NetworkObjectId, boosted));
                if (boosted && CresseliaMoveRuntime.Living(target)) target.AddStatusEffect(new StatusEffect(StatusType.Asleep, 0.15f, true, 0));
                ShowCresseliaEffect(CresseliaEffect.MoonblastImpact, goal, Vector3.forward, 0.2f, 0, boosted ? 0.55f : 0.3f);
                break;
            }
            position = next;
            yield return null;
        }
        StopCresseliaEffectRPC(visual);
    }

    private void CresseliaPsybeamHit(Vector3 direction, bool upgraded)
    {
        float length = CresseliaMoveRules.Range(CresseliaCast.Psybeam), width = CresseliaMoveRules.PsybeamWidth(upgraded);
        Vector3 origin = transform.position;
        ShowCresseliaEffect(CresseliaEffect.Psybeam, origin, direction, 0.35f, 0, width);
        var targets = CresseliaMoveRuntime.Targets(Physics.OverlapBox(origin + Vector3.up * 0.7f + direction * (length * 0.5f),
            new Vector3(width * 0.5f, 1f, length * 0.5f), Quaternion.LookRotation(direction), LayerMask.GetMask("Players", "WildPokemons")));
        int enemyPlayers = 0;
        foreach (Pokemon target in targets)
        {
            if (!CresseliaMoveRuntime.CanHit(playerManager, target)) continue;
            if (target.Type == PokemonType.Player) enemyPlayers++;
            target.TakeDamageRPC(CresseliaMoveRules.Damage(CresseliaCast.Psybeam, NetworkObjectId));
            if (CresseliaMoveRuntime.Living(target)) target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));
        }
        if (enemyPlayers > 0 && IsLivingCresselia)
        {
            pokemon.HealDamageRPC(Mathf.FloorToInt(pokemon.GetMaxHp() * 0.05f) * enemyPlayers);
            AdvanceCresseliaRhythm();
            ShowCresseliaEffect(CresseliaEffect.BeamHeal, origin + direction * 3, -direction, 0.35f, NetworkObjectId);
        }
    }

    private IEnumerator CresseliaProjectile(CresseliaCast move, Vector3 direction, bool upgraded)
    {
        Vector3 origin = transform.position + Vector3.up * 0.7f;
        float range = CresseliaMoveRules.Range(move), travelled = 0;
        float radius = move == CresseliaCast.Moonblast ? 0.7f : 0.4f;
        int visual = ShowCresseliaEffect(move == CresseliaCast.Moonblast ? CresseliaEffect.Moonblast : CresseliaEffect.AuroraBeam,
            origin, direction, range / CresseliaMoveRules.BeamSpeed + 0.05f);
        while (IsSpawned && travelled < range)
        {
            float next = Mathf.Min(range, travelled + CresseliaMoveRules.BeamSpeed * Time.deltaTime);
            Vector3 from = origin + direction * travelled, to = origin + direction * next;
            Pokemon first = null; float closest = float.MaxValue;
            foreach (Pokemon target in CresseliaMoveRuntime.Targets(Physics.OverlapCapsule(from, to, radius, LayerMask.GetMask("Players", "WildPokemons"))))
            {
                if (!CresseliaMoveRuntime.CanHit(playerManager, target)) continue;
                float distance = Vector3.Dot(target.transform.position - origin, direction);
                if (distance < closest) { closest = distance; first = target; }
            }
            if (first != null)
            {
                if (move == CresseliaCast.Moonblast) CresseliaMoonblastHit(first, upgraded);
                else
                {
                    first.TakeDamageRPC(CresseliaMoveRules.Damage(move, NetworkObjectId));
                    RefreshCresseliaDebuffs(first, false);
                    if (first.Type == PokemonType.Player) AdvanceCresseliaRhythm();
                    ShowCresseliaEffect(CresseliaEffect.MoonblastImpact, first.transform.position, direction, 0.25f, 0, 0.6f);
                }
                break;
            }
            travelled = next;
            yield return null;
        }
        StopCresseliaEffectRPC(visual);
    }

    private void CresseliaMoonblastHit(Pokemon primary, bool upgraded)
    {
        float radius = CresseliaMoveRules.MoonblastRadius(upgraded);
        Vector3 center = primary.transform.position;
        ShowCresseliaEffect(CresseliaEffect.MoonblastImpact, center, Vector3.forward, 0.35f, 0, radius);
        var targets = CresseliaMoveRuntime.Targets(Physics.OverlapSphere(center, radius, LayerMask.GetMask("Players", "WildPokemons")));
        targets.Add(primary);
        bool rhythm = false;
        foreach (Pokemon target in targets)
        {
            if (!CresseliaMoveRuntime.CanHit(playerManager, target)) continue;
            target.TakeDamageRPC(CresseliaMoveRules.Damage(CresseliaCast.Moonblast, NetworkObjectId));
            RefreshCresseliaDebuffs(target, true);
            rhythm |= target.Type == PokemonType.Player;
        }
        if (rhythm) AdvanceCresseliaRhythm();
    }

    private void RefreshCresseliaDebuffs(Pokemon target, bool moonblast)
    {
        if (!CresseliaMoveRuntime.Living(target)) return;
        ushort id = moonblast ? cresseliaMoonblastDebuff : cresseliaAuroraDebuff;
        target.RemoveAllStatChangeWithIDRPC(id);
        target.AddStatChange(new StatChange(15, Stat.AtkSpeed, moonblast ? 5 : 3, true, false, true, id));
        if (!moonblast) return;
        target.AddStatChange(new StatChange(30, Stat.Attack, 5, true, false, true, id));
        target.AddStatChange(new StatChange(20, Stat.SpAttack, 5, true, false, true, id));
    }

    private void AdvanceCresseliaRhythm()
    {
        if (!IsLivingCresselia) return;
        cresseliaBoostReadyAt.Value = CresseliaNow + CresseliaMoveRules.ReduceBoost(CresseliaBoostRemaining);
    }

    private void ApplyCresseliaProtection(CresseliaCast move, Pokemon ally, bool upgraded)
    {
        CresseliaMoveRuntime.Cleanse(pokemon);
        ShowCresseliaEffect(CresseliaEffect.Cleanse, transform.position, transform.forward, 0.45f, NetworkObjectId);
        if (ally != null)
        {
            CresseliaMoveRuntime.Cleanse(ally);
            ShowCresseliaEffect(CresseliaEffect.Cleanse, ally.transform.position, transform.forward, 0.45f, ally.NetworkObjectId);
        }
        if (move == CresseliaCast.LunarBlessing)
        {
            int amount = Mathf.FloorToInt(pokemon.GetMaxHp() * CresseliaMoveRules.ShieldRatio(upgraded));
            StartCoroutine(CresseliaBlessing(pokemon, amount));
            if (ally != null) { StartCoroutine(CresseliaBlessing(ally, amount)); AdvanceCresseliaRhythm(); }
        }
    }

    private IEnumerator CresseliaBlessing(Pokemon target, int amount)
    {
        var lease = new CresseliaShieldLease(target, amount);
        cresseliaShields.Add(lease);
        int visual = ShowCresseliaEffect(CresseliaEffect.LunarShield, target.transform.position, transform.forward,
            CresseliaMoveRules.BlessingDuration, target.NetworkObjectId, amount, lease.Id);
        double expires = CresseliaNow + CresseliaMoveRules.BlessingDuration;
        while (lease.Intact && CresseliaNow < expires) yield return null;
        int heal = lease.Intact ? lease.Remaining : 0;
        lease.Dispose(); cresseliaShields.Remove(lease);
        StopCresseliaEffectRPC(visual);
        if (heal > 0 && CresseliaMoveRuntime.Living(target))
        {
            target.HealDamageRPC(heal);
            ShowCresseliaEffect(CresseliaEffect.ShieldHeal, target.transform.position, transform.forward, 0.45f, target.NetworkObjectId);
        }
        else if (CresseliaMoveRuntime.Living(target)) ShowCresseliaEffect(CresseliaEffect.ShieldBreak, target.transform.position, transform.forward, 0.25f, target.NetworkObjectId);
    }

    private IEnumerator CresseliaMoonlightField(Vector3 center, bool upgraded)
    {
        float radius = CresseliaMoveRules.MoonlightRadius(upgraded);
        ShowCresseliaEffect(CresseliaEffect.Moonlight, center, Vector3.forward, CresseliaMoveRules.MoonlightDuration, 0, radius);
        bool rhythm = false;
        double started = CresseliaNow;
        for (int pulse = 0; pulse < CresseliaMoveRules.MoonlightPulses; pulse++)
        {
            while (CresseliaNow < started + pulse) yield return null;
            ShowCresseliaEffect(CresseliaEffect.HealPulse, center, Vector3.forward, 0.35f, 0, radius);
            foreach (Pokemon ally in CresseliaMoveRuntime.Targets(Physics.OverlapSphere(center + Vector3.up * 0.5f, radius, LayerMask.GetMask("Players"))))
            {
                if (!CresseliaMoveRuntime.Living(ally) || !ally.TeamMember.IsOnSameTeam(playerManager.CurrentTeam)) continue;
                int amount = Mathf.Min(ally.GetMaxHp() - ally.CurrentHp, Mathf.FloorToInt(ally.GetMaxHp() * 0.08f));
                if (amount <= 0) continue;
                ally.HealDamageRPC(amount);
                if (!rhythm && ally != pokemon) { rhythm = true; AdvanceCresseliaRhythm(); }
            }
        }
    }

    private void DisposeCresseliaRuntime()
    {
        ClearCresseliaBufferedInput();
        if (cresseliaBound && pokemon != null) pokemon.OnDeath -= OnCresseliaDeath;
        EndCresseliaUniteServer(true);
        foreach (var shield in cresseliaShields) shield.Dispose();
        cresseliaShields.Clear();
        if (IsServer && cresseliaBound && NetworkManager != null && NetworkManager.SpawnManager != null)
            foreach (var obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
                if (obj.TryGetComponent(out Pokemon target))
                {
                    target.RemoveAllStatChangeWithIDRPC(cresseliaAuroraDebuff);
                    target.RemoveAllStatChangeWithIDRPC(cresseliaMoonblastDebuff);
                }
        CresseliaEffectIds.Release(cresseliaAuroraDebuff); CresseliaEffectIds.Release(cresseliaMoonblastDebuff);
        cresseliaAuroraDebuff = 0; cresseliaMoonblastDebuff = 0;
        cresseliaBound = false;
        cresseliaDecorationRequested = false;
        DisposeCresseliaPresentation();
    }

    public override void OnNetworkDespawn()
    {
        OnVaporeonNetworkDespawn();
        if (pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.CresseliaPassive)
        {
            StopAllCoroutines();
            if (IsOwner)
            {
                foreach (MoveBase move in moves) if (move is CresseliaMoveBase) move.ResetMove();
                if (uniteMove is CresseliaUnite unite) unite.ResetMove();
            }
            DisposeCresseliaRuntime();
        }
        base.OnNetworkDespawn();
    }
}

// Source-specific IDs are leased, not derived by truncating NetworkObjectIds.
// Duplicate Cresselias can protect the same ally without merging conversions.
public static class CresseliaEffectIds
{
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    public static ushort Acquire()
    {
        for (ushort id = 48000; id < 50000; id++) if (used.Add(id)) return id;
        throw new InvalidOperationException("Cresselia effect ID pool exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
}

public sealed class CresseliaShieldLease : IDisposable
{
    private readonly Pokemon target;
    private bool dead, disposed;
    public ushort Id { get; }
    public int Remaining => !disposed && !dead && CresseliaMoveRuntime.Living(target) ? target.GetShieldWithID(Id)?.Amount ?? 0 : 0;
    public bool Intact => Remaining > 0;
    public CresseliaShieldLease(Pokemon target, int amount)
    {
        this.target = target; Id = CresseliaEffectIds.Acquire();
        target.OnDeath += OnDeath;
        // Conversion is the sole expiry owner, independent of VFX and shield timer ordering.
        target.AddShieldRPC(new ShieldInfo(amount, Id, 0, CresseliaMoveRules.BlessingDuration, false));
    }
    private void OnDeath(DamageInfo damage) { dead = true; Dispose(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (target != null)
        {
            target.OnDeath -= OnDeath;
            if (target.IsSpawned && target.IsServer) target.RemoveShieldWithIDRPC(Id);
        }
        CresseliaEffectIds.Release(Id);
    }
}
