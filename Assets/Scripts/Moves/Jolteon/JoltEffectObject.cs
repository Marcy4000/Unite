using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Authored presentation for every reworked move; Web and its marks also own
// their server-side damage. Driven dashes retain the game's owner-movement flow.
public class JoltEffectObject : NetworkBehaviour
{
    [SerializeField] private JoltEffect effect;
    [SerializeField] private GameObject graphics;
    [SerializeField] private Transform accent;
    [SerializeField] private BoxCollider webArea;
    private PlayerManager actor;
    private Pokemon target;
    private Vector3 direction;
    private float remaining, tickRemaining, age;
    private bool initialized, empowered, upgraded;
    private ushort markId;
    private AsyncOperationHandle<GameObject> assetHandle;
    private bool holdsAsset;
    private Action completed;
    private Vision visibility;
    private readonly HashSet<ulong> webHits = new HashSet<ulong>();
    private static readonly Dictionary<(ulong, ulong), JoltEffectObject> marks = new Dictionary<(ulong, ulong), JoltEffectObject>();
    private static readonly HashSet<ushort> activeMarkIds = new HashSet<ushort>();
    private static ushort nextMarkId = JoltMoveRules.WebMarkIdMin;
    private (ulong, ulong) markKey;

    public void HoldAsset(AsyncOperationHandle<GameObject> handle, Action callback)
    {
        assetHandle = handle;
        holdsAsset = true;
        completed = callback;
    }

    public override void OnNetworkSpawn()
    {
        graphics.SetActive(false);
        // Also bounds the lifetime of a spawned object whose initialization fails.
        remaining = 3f;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void InitializeRPC(ulong actorId, Vector3 origin, Vector3 castDirection, bool boosted, bool isUpgraded, bool second, ulong targetId)
    {
        if (initialized) return;
        if (!Bind(actorId, origin, castDirection, boosted, isUpgraded, second, targetId) || actor.Pokemon.CurrentHp <= 0)
        {
            Despawn();
            return;
        }
        actor.Pokemon.OnDeath += OnActorDeath;
        if (effect == JoltEffect.WebMark)
        {
            markKey = (actorId, targetId);
            if (marks.TryGetValue(markKey, out var previous) && previous != null) previous.Despawn();
            marks[markKey] = this;
            do { nextMarkId = nextMarkId >= JoltMoveRules.WebMarkIdMax ? JoltMoveRules.WebMarkIdMin : (ushort)(nextMarkId + 1); } while (activeMarkIds.Contains(nextMarkId));
            markId = nextMarkId;
            activeMarkIds.Add(markId);
            target.AddStatusEffect(new StatusEffect(StatusType.Scriptable, remaining, true, markId));
            actor.Pokemon.OnDamageDealt += OnDamageDealt;
            tickRemaining = JoltMoveRules.WebTick;
        }
        BeginPresentationRPC(actorId, origin, castDirection, boosted, isUpgraded, second, targetId);
    }

    private bool Bind(ulong actorId, Vector3 origin, Vector3 castDirection, bool boosted, bool isUpgraded, bool second, ulong targetId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId, out var obj)
            || !obj.TryGetComponent(out actor)) return false;
        if (effect == JoltEffect.WebMark)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var targetObject)
                || !targetObject.TryGetComponent(out target) || target.CurrentHp <= 0) return false;
        }
        empowered = boosted;
        upgraded = isUpgraded;
        direction = new Vector3(castDirection.x, 0, castDirection.z).normalized;
        if (direction.sqrMagnitude < 0.001f) direction = actor.transform.forward;
        transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        remaining = JoltMoveRules.EffectDuration(effect, boosted, isUpgraded);
        if (effect == JoltEffect.Discharge) graphics.transform.localScale = Vector3.one * (second ? JoltMoveRules.RecastScale : 1f);
        if (effect == JoltEffect.Electroweb)
        {
            float width = boosted ? JoltMoveRules.BoostedWebWidth : JoltMoveRules.WebWidth;
            webArea.size = new Vector3(width, 1.5f, 0.3f);
            graphics.transform.localScale = new Vector3(width / JoltMoveRules.WebWidth, 1, 1);
        }
        initialized = true;
        return true;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void BeginPresentationRPC(ulong actorId, Vector3 origin, Vector3 castDirection, bool boosted, bool isUpgraded, bool second, ulong targetId)
    {
        if (!IsServer && !Bind(actorId, origin, castDirection, boosted, isUpgraded, second, targetId)) return;
        // Persistent auras and target marks must not reveal hidden actors.
        // A detached travelling projectile remains visible like other projectiles.
        if (effect != JoltEffect.Electroweb)
        {
            visibility = effect == JoltEffect.WebMark ? target.GetComponent<Vision>() : actor.Vision;
            if (visibility != null) visibility.OnVisibilityChanged += SetVisible;
        }
        SetVisible(visibility == null || visibility.IsRendered);
    }

    private void SetVisible(bool visible) { if (graphics != null) graphics.SetActive(visible); }

    private void Update()
    {
        if (!initialized)
        {
            if (IsServer && (remaining -= Time.deltaTime) <= 0) Despawn();
            return;
        }
        if (actor == null || !actor.IsSpawned || actor.Pokemon.CurrentHp <= 0)
        {
            if (IsServer) Despawn();
            graphics.SetActive(false);
            return;
        }
        age += Time.deltaTime;
        if (effect == JoltEffect.WebMark)
        {
            if (target == null || target.CurrentHp <= 0 || (IsServer && !target.HasStatusEffect(markId)))
            {
                if (IsServer) Despawn();
                graphics.SetActive(false);
                return;
            }
            transform.position = target.transform.position;
            if (IsServer)
            {
                tickRemaining -= Time.deltaTime;
                if (tickRemaining <= 0)
                {
                    tickRemaining += JoltMoveRules.WebTick;
                    target.TakeDamageRPC(new DamageInfo(actor.NetworkObjectId, 0.6f, 4, 60, DamageType.Physical, JoltMoveRules.PeriodicDamage));
                }
            }
        }
        else if (effect == JoltEffect.Electroweb)
        {
            if (IsServer) AdvanceWeb();
        }
        else transform.SetPositionAndRotation(actor.transform.position, actor.transform.rotation);

        if (accent != null)
        {
            accent.localRotation = Quaternion.Euler(0, age * (empowered ? 210f : 140f), 0);
            float pulse = 1f + Mathf.Sin(age * 16f) * 0.07f;
            accent.localScale = Vector3.one * pulse;
        }
        if (effect == JoltEffect.Charge)
            graphics.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, age / JoltMoveRules.ChargeSlowDuration);
        else if (effect == JoltEffect.ThunderFang)
            graphics.transform.localScale = Vector3.one * (age < JoltMoveRules.FangStartup
                ? Mathf.Lerp(0.5f, 1f, age / JoltMoveRules.FangStartup)
                : Mathf.Lerp(1.2f, 1f, (age - JoltMoveRules.FangStartup) / JoltMoveRules.FangDuration));
        if (IsServer && (remaining -= Time.deltaTime) <= 0) Despawn();
    }

    private void LateUpdate()
    {
        if (!initialized || actor == null || effect == JoltEffect.Electroweb) return;
        if (effect == JoltEffect.WebMark) { if (target != null) transform.position = target.transform.position; }
        else transform.SetPositionAndRotation(actor.transform.position, actor.transform.rotation);
    }

    private void AdvanceWeb()
    {
        Vector3 step = direction * JoltMoveRules.WebSpeed * Mathf.Min(Time.deltaTime, remaining);
        Vector3 center = transform.TransformPoint(webArea.center) + step * 0.5f;
        Vector3 size = webArea.size + Vector3.forward * step.magnitude;
        foreach (Collider collider in Physics.OverlapBox(center, size * 0.5f, transform.rotation))
        {
            if (!collider.TryGetComponent(out Pokemon pokemon) || !JoltMoveRuntime.CanHit(actor, pokemon)
                || !webHits.Add(pokemon.NetworkObjectId)) continue;
            // Each load captures this target. A delayed mark cannot attach to a
            // different hit, or appear after the source dies and respawns.
            var request = new JoltEffectRequest(actor, null);
            request.Load(JoltEffect.WebMark, pokemon.transform.position, direction, empowered, upgraded, false, pokemon.NetworkObjectId);
        }
        transform.position += step;
    }

    private void OnDamageDealt(ulong targetId, DamageInfo damage)
    {
        if (target == null || targetId != target.NetworkObjectId || !JoltMoveRules.TriggersWebSpeed(damage.proprieties)) return;
        JoltMoveRuntime.RefreshStat(actor.Pokemon, new StatChange(30, Stat.Speed, upgraded ? 1.5f : 1f, true, true, true, JoltMoveRules.WebSpeedId));
    }

    private void OnActorDeath(DamageInfo damage) { Despawn(); }
    private void Despawn() { if (IsServer && IsSpawned) NetworkObject.Despawn(true); }

    public override void OnNetworkDespawn()
    {
        graphics.SetActive(false);
        if (visibility != null) visibility.OnVisibilityChanged -= SetVisible;
        visibility = null;
        if (IsServer)
        {
            if (actor != null)
            {
                actor.Pokemon.OnDeath -= OnActorDeath;
                actor.Pokemon.OnDamageDealt -= OnDamageDealt;
            }
            if (markId != 0)
            {
                if (target != null && target.IsSpawned) target.RemoveStatusEffectWithID(markId);
                activeMarkIds.Remove(markId);
                if (marks.TryGetValue(markKey, out var current) && current == this) marks.Remove(markKey);
            }
        }
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (visibility != null) visibility.OnVisibilityChanged -= SetVisible;
        if (holdsAsset && assetHandle.IsValid()) Addressables.Release(assetHandle);
        holdsAsset = false;
        completed?.Invoke();
        completed = null;
        base.OnDestroy();
    }
}
