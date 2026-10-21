using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private readonly Dictionary<int, MarshadowVisualRequest> marshadowVisuals = new Dictionary<int, MarshadowVisualRequest>();
    private readonly Dictionary<int, MarshadowServerCast> marshadowCasts = new Dictionary<int, MarshadowServerCast>();

    public IDisposable AcquireMoveStatus(int slot, ActionStatusType status)
    {
        if (slot == 2)
        {
            var lease = uniteMoveStatus.AcquireStatus(status); UpdateUniteMoveUI();
            return new MarshadowStatusLease(lease, UpdateUniteMoveUI);
        }
        if (slot < 0 || slot >= moveStatuses.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        var result = moveStatuses[slot].AcquireStatus(status); UpdateMoveUI(slot);
        return new MarshadowStatusLease(result, () => UpdateMoveUI(slot));
    }

    private bool IsLivingMarshadow => pokemon != null && pokemon.CurrentHp > 0 && pokemon.BaseStats.Passive == AvailablePassives.MarshadowPassive;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void ShowMarshadowEffectRPC(MarshadowEffect effect, int id, Vector3 origin, Vector3 direction, float duration, ulong target, int stage)
    {
        if (!IsLivingMarshadow || !Enum.IsDefined(typeof(MarshadowEffect), effect)) return;
        PresentMarshadowEffectRPC(effect, id, origin, direction, Mathf.Clamp(duration, 0.1f, 15f), target, Mathf.Clamp(stage, 0, 3));
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PresentMarshadowEffectRPC(MarshadowEffect effect, int id, Vector3 origin, Vector3 direction, float duration, ulong target, int stage)
    {
        if (!IsClient || marshadowVisuals.ContainsKey(id)) return;
        var request = new MarshadowVisualRequest();
        marshadowVisuals.Add(id, request);
        float started = Time.time;
        Addressables.LoadAssetAsync<GameObject>(MarshadowMoveRules.EffectPath(effect)).Completed += handle =>
        {
            float remaining = duration - (Time.time - started);
            if (request.Cancelled || this == null || !IsSpawned || pokemon.CurrentHp <= 0 || remaining <= 0 || handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogWarning($"Marshadow visual unavailable: {effect}: {handle.OperationException}");
                Addressables.Release(handle);
                if (this != null) marshadowVisuals.Remove(id);
                return;
            }
            var go = Instantiate(handle.Result, origin, Quaternion.LookRotation(MarshadowMoveRules.Direction(direction, Vector3.forward)));
            request.Instance = go.GetComponent<MarshadowVisual>();
            request.Instance.Initialize(playerManager, effect, origin, direction, remaining, target, stage, Time.time - started, handle, () => marshadowVisuals.Remove(id));
        };
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void EndMarshadowEffectRPC(int id) { StopMarshadowEffectRPC(id); }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void StopMarshadowEffectRPC(int id)
    {
        if (!marshadowVisuals.TryGetValue(id, out var request)) return;
        request.Cancelled = true;
        if (request.Instance != null) Destroy(request.Instance.gameObject);
        marshadowVisuals.Remove(id);
    }

    private MarshadowServerCast ServerCast(int id)
    {
        var expired = new List<int>();
        foreach (var pair in marshadowCasts) if (Time.time - pair.Value.Started > 15f) expired.Add(pair.Key);
        foreach (int key in expired) marshadowCasts.Remove(key);
        if (!marshadowCasts.TryGetValue(id, out var cast))
        {
            cast = new MarshadowServerCast(); marshadowCasts.Add(id, cast);
        }
        return cast;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void MarshadowHitRPC(MarshadowHit hit, int id, int stage, Vector3 from, Vector3 to, Vector3 direction, bool upgraded, ulong targetId)
    {
        if (!IsLivingMarshadow || !Enum.IsDefined(typeof(MarshadowHit), hit) || stage < 0 || stage > 2
            || (from - transform.position).sqrMagnitude > 100f || (to - from).sqrMagnitude > 25f) return;
        direction = MarshadowMoveRules.Direction(direction, transform.forward);
        var cast = ServerCast(id);
        if (cast.Cancelled) return;
        float radius = hit == MarshadowHit.SpectralThief ? MarshadowMoveRules.SpectralRadius : 1.1f;
        Vector3 offset = hit == MarshadowHit.DrainPunch || hit == MarshadowHit.Kick ? direction * 1.4f : Vector3.zero;
        int healing = 0, wildHealing = 0;
        foreach (Pokemon target in MarshadowMoveRuntime.Sweep(playerManager, from + offset, to + offset, radius))
        {
            if (hit == MarshadowHit.AuraFollowup && target.NetworkObjectId != targetId) continue;
            if (!cast.Hits.Add(((int)hit, stage, target.NetworkObjectId))) continue;
            DamageInfo damage;
            switch (hit)
            {
                case MarshadowHit.DrainPunch: damage = new DamageInfo(NetworkObjectId, 1.15f, 6, 265, DamageType.Physical); break;
                case MarshadowHit.Rush: damage = new DamageInfo(NetworkObjectId, stage == 2 ? (upgraded ? 1.9f : 1.75f) : 1.15f, 5, (short)(stage == 2 ? 400 : 260), DamageType.Physical); break;
                case MarshadowHit.SpectralThief: damage = new DamageInfo(NetworkObjectId, 1.65f, 6, 510, DamageType.Physical); break;
                case MarshadowHit.Kick: damage = new DamageInfo(NetworkObjectId, 1.76f, 9, 520, DamageType.Physical); break;
                default: damage = new DamageInfo(NetworkObjectId, 0, 0, (short)MarshadowMoveRules.AuraExecute(target.GetMissingHp(), upgraded, target.TeamMember.Team == Team.Neutral), DamageType.Physical); break;
            }
            int before = target.CurrentHp;
            target.TakeDamageRPC(damage);
            int dealt = Mathf.Max(0, before - target.CurrentHp);
            if (hit == MarshadowHit.DrainPunch)
            {
                if (target.TeamMember.Team == Team.Neutral) wildHealing += Mathf.FloorToInt(dealt * 0.1f);
                else healing += Mathf.FloorToInt(dealt * 0.25f);
            }
            if (target.CurrentHp > 0)
            {
                if (hit == MarshadowHit.SpectralThief)
                {
                    target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));
                    SlowMarshadowTarget(target, 25, 1f);
                    if ((target.transform.position - to).sqrMagnitude <= MarshadowMoveRules.TheftRadius * MarshadowMoveRules.TheftRadius
                        && target.TransferMarshadowBuffs(pokemon, upgraded) > 0)
                        PresentMarshadowEffectRPC(MarshadowEffect.Theft, -MarshadowMoveRules.NextCast(), target.transform.position, direction, 0.5f, NetworkObjectId, 0);
                    if (!target.HasStatusEffect(StatusType.Unstoppable) && !target.HasStatusEffect(StatusType.HindranceResistance)) target.ApplyKnockupRPC(1.2f, 0.5f);
                }
                if (hit == MarshadowHit.Rush && stage == 2)
                {
                    target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.45f, true, 0));
                    if (!target.HasStatusEffect(StatusType.Unstoppable) && !target.HasStatusEffect(StatusType.HindranceResistance)) target.ApplyKnockupRPC(1.2f, 0.45f);
                    SlowMarshadowTarget(target, 30, 1f);
                }
                if (hit == MarshadowHit.Kick)
                {
                    target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.2f, true, 62011));
                    if (!target.HasStatusEffect(StatusType.Unstoppable) && !target.HasStatusEffect(StatusType.HindranceResistance))
                    {
                        if (target.TryGetComponent(out PlayerMovement movement)) movement.ApplyMarshadowWallKick(direction, upgraded ? 1f : 0.7f, NetworkObjectId);
                        else target.ApplyKnockbackRPC(direction, 12.5f);
                    }
                }
            }
            if (hit == MarshadowHit.AuraFollowup && !cast.CooldownRewarded)
            {
                cast.CooldownRewarded = true;
                MarshadowAuraRewardRPC(id);
            }
            if (hit == MarshadowHit.Rush && stage == 2 && upgraded && !cast.CooldownRewarded)
            {
                cast.CooldownRewarded = true;
                pokemon.RemoveAllStatChangeWithIDRPC(MarshadowMoveRules.FinisherSpeed);
                pokemon.AddStatChange(new StatChange(20, Stat.Speed, 2f, true, true, true, MarshadowMoveRules.FinisherSpeed));
            }
        }
        healing += Mathf.Min(wildHealing, Mathf.RoundToInt(pokemon.GetMaxHp() * 0.04f));
        if (healing > 0) pokemon.HealDamageRPC(healing);
    }

    private static void SlowMarshadowTarget(Pokemon target, short amount, float duration)
    {
        target.RemoveStatChangeWithIDRPC(MarshadowMoveRules.Slow);
        target.AddStatChange(new StatChange(amount, Stat.Speed, duration, true, false, true, MarshadowMoveRules.Slow));
    }

    public void MarshadowWallImpact(Vector3 position)
    {
        if (IsServer) PresentMarshadowEffectRPC(MarshadowEffect.WallImpact, -MarshadowMoveRules.NextCast(), position, transform.forward, 0.3f, 0, 0);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void MarshadowAuraRewardRPC(int id)
    {
        if (moves[0] is MarshadowAuraSphere aura && aura.LastCast == id)
            ReduceMoveCooldown(MoveType.MoveB, moves[1].Cooldown * 0.3f);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void LaunchMarshadowAuraRPC(int id, Vector3 origin, Vector3 direction)
    {
        if (!IsLivingMarshadow || (origin - transform.position).sqrMagnitude > 25f) return;
        var cast = ServerCast(id);
        if (cast.ProjectileStarted || cast.Cancelled) return;
        cast.ProjectileStarted = true;
        direction = MarshadowMoveRules.Direction(direction, transform.forward);
        PresentMarshadowEffectRPC(MarshadowEffect.AuraSphere, id, origin, direction, MarshadowMoveRules.AuraRange / MarshadowMoveRules.AuraSpeed, 0, 0);
        StartCoroutine(TravelMarshadowAura(id, origin, direction));
    }

    private IEnumerator TravelMarshadowAura(int id, Vector3 origin, Vector3 direction)
    {
        bool cancelled = false;
        Action<DamageInfo> onDeath = _ => cancelled = true;
        pokemon.OnDeath += onDeath;
        float distance = 0;
        Vector3 point = origin;
        try
        {
            while (!cancelled && !ServerCast(id).Cancelled && IsSpawned && pokemon.CurrentHp > 0 && distance < MarshadowMoveRules.AuraRange)
            {
                float step = Mathf.Min(MarshadowMoveRules.AuraRange - distance, MarshadowMoveRules.AuraSpeed * Time.deltaTime);
                Vector3 next = point + direction * step;
                Pokemon nearest = null;
                float nearestDistance = float.MaxValue;
                foreach (Pokemon target in MarshadowMoveRuntime.Sweep(playerManager, point, next, 0.45f))
                {
                    float d = (target.transform.position - point).sqrMagnitude;
                    if (d < nearestDistance) { nearest = target; nearestDistance = d; }
                }
                if (nearest != null)
                {
                    nearest.TakeDamageRPC(new DamageInfo(NetworkObjectId, 1.12f, 7, 210, DamageType.Physical));
                    MarshadowAuraResultRPC(id, nearest.CurrentHp > 0, nearest.NetworkObjectId);
                    yield break;
                }
                distance += step; point = next;
                yield return null;
            }
            if (!cancelled && !ServerCast(id).Cancelled && IsSpawned) MarshadowAuraResultRPC(id, false, 0);
        }
        finally
        {
            if (pokemon != null) pokemon.OnDeath -= onDeath;
            if (IsSpawned) StopMarshadowEffectRPC(id);
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void MarshadowAuraResultRPC(int id, bool hit, ulong target)
    {
        if (moves[0] is MarshadowAuraSphere aura) aura.ProjectileResult(id, hit, target);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelMarshadowCastRPC(int id)
    {
        var cast = ServerCast(id); cast.Cancelled = true;
        if (cast.Instance != null && cast.Instance.IsSpawned) cast.Instance.Despawn(true);
        StopMarshadowEffectRPC(id);
    }
}

internal sealed class MarshadowStatusLease : IDisposable
{
    private IDisposable lease;
    private Action refresh;
    public MarshadowStatusLease(IDisposable lease, Action refresh) { this.lease = lease; this.refresh = refresh; }
    public void Dispose()
    {
        if (lease == null) return;
        lease.Dispose(); lease = null;
        refresh?.Invoke(); refresh = null;
    }
}

internal sealed class MarshadowVisualRequest
{
    public bool Cancelled;
    public MarshadowVisual Instance;
}
internal sealed class MarshadowServerCast
{
    public readonly float Started = Time.time;
    public readonly HashSet<(int, int, ulong)> Hits = new HashSet<(int, int, ulong)>();
    public bool CooldownRewarded, ProjectileStarted, CopyStarted, Cancelled;
    public NetworkObject Instance;
}
