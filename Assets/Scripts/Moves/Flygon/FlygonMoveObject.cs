using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Existing prefab/script GUIDs are retained. Each instance owns its cast, queries and asset handle.
public abstract class FlygonMoveObject : NetworkBehaviour
{
    [SerializeField] private GameObject graphics;
    [SerializeField] private SphereCollider area;
    private readonly NetworkVariable<ulong> actorId = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<Vector3> aim = new NetworkVariable<Vector3>(Vector3.forward);
    private readonly NetworkVariable<byte> phase = new NetworkVariable<byte>();
    private readonly NetworkVariable<double> phaseStarted = new NetworkVariable<double>();
    protected PlayerManager Actor { get; private set; }
    protected Vector3 Direction { get; private set; }
    protected Vector3 Destination { get; set; }
    protected Vector3 Origin { get; private set; }
    protected bool Upgraded { get; private set; }
    protected ushort EffectId { get; private set; }
    protected bool EndRequested { get; private set; }
    protected bool Live => Actor != null && Actor.IsSpawned && Actor.Pokemon.CurrentHp > 0 && IsSpawned;
    public abstract FlygonCast Kind { get; }
    public byte Phase => phase.Value;
    public float PhaseAge => (float)((NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble) - phaseStarted.Value);
    public PlayerManager PresentationActor => Actor;
    private MovesController combat;
    private int cast;
    private bool initialized, cleaned, holdsAsset;
    private byte shownPhase = byte.MaxValue;
    private AsyncOperationHandle<GameObject> handle;

    public void HoldAsset(AsyncOperationHandle<GameObject> asset) { handle = asset; holdsAsset = true; }
    public override void OnNetworkSpawn()
    {
        if (graphics != null) graphics.SetActive(false);
        if (IsServer) StartCoroutine(Watchdog());
    }
    private IEnumerator Watchdog() { yield return new WaitForSeconds(13); Despawn(); }
    public void Initialize(MovesController owner, PlayerManager player, int id, Vector3 direction, Vector3 destination, bool upgraded)
    {
        if (!IsServer || initialized) return;
        initialized = true; combat = owner; Actor = player; cast = id; Direction = direction; Destination = destination; Origin = player.transform.position;
        Upgraded = upgraded; EffectId = FlygonEffectIds.Acquire();
        aim.Value = direction; actorId.Value = player.NetworkObjectId; phaseStarted.Value = NetworkManager.ServerTime.Time;
        player.Pokemon.OnDeath += OnDeath;
        StartCoroutine(Run());
    }
    private IEnumerator Run() { yield return Execute(); Despawn(); }
    protected abstract IEnumerator Execute();
    protected virtual void Cleanup() { }
    protected virtual void PresentPhase(byte value) { }
    protected void SetPhase(byte value)
    {
        phaseStarted.Value = NetworkManager.ServerTime.Time; phase.Value = value;
        combat.NotifyFlygonPhase(Kind, cast, value);
    }
    public void RequestEnd() { if (IsServer && Phase >= 1 && (Kind == FlygonCast.Dig ? Phase == 1 : Kind == FlygonCast.Unite && Phase <= 4)) EndRequested = true; }
    protected IEnumerator Wait(float seconds, bool interruptible = false)
    {
        float until = Time.time + seconds;
        while (Live && Time.time < until)
        {
            if (interruptible && Actor.Pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts)) { Despawn(); yield break; }
            yield return null;
        }
    }
    protected HashSet<Pokemon> Targets(Vector3 center, float radius)
    {
        var result = new HashSet<Pokemon>();
        foreach (var collider in Physics.OverlapSphere(center + Vector3.up * 0.5f, radius))
        {
            Pokemon target = collider.GetComponentInParent<Pokemon>();
            if (CanHit(target)) result.Add(target);
        }
        return result;
    }
    protected HashSet<Pokemon> TargetsInArea()
    {
        return area != null ? Targets(area.transform.TransformPoint(area.center), area.radius * Mathf.Max(Mathf.Abs(area.transform.lossyScale.x), Mathf.Abs(area.transform.lossyScale.z)))
            : new HashSet<Pokemon>();
    }
    protected bool CanHit(Pokemon target) => Live && target != null && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(Actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
    protected bool CanControl(Pokemon target) => CanHit(target) && target.Type != PokemonType.Objective && !target.HasStatusEffect(StatusType.Unstoppable);
    protected void RefreshStat(Pokemon target, Stat stat, short amount, float duration, bool buff = false)
    {
        // One source-specific entry per stat; refresh instead of stacking every wave/tick.
        var old = new List<StatChange>();
        foreach (var change in target.StatChanges) if (change.ID == EffectId && change.AffectedStat == stat) old.Add(change);
        foreach (var change in old) target.RemoveStatChangeRPC(change);
        target.AddStatChange(new StatChange(amount, stat, duration, duration > 0, buff, true, EffectId));
    }
    protected virtual void Update()
    {
        if (IsServer && initialized && !Live) { Despawn(); return; }
        if (!IsClient || actorId.Value == ulong.MaxValue) return;
        if (Actor == null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId.Value, out var actor)) Actor = actor.GetComponent<PlayerManager>();
        bool attached = Kind == FlygonCast.Dig || Kind == FlygonCast.Unite;
        bool visible = !attached || Actor == null || Actor.Vision == null || Actor.Vision.IsRendered;
        if (graphics != null && graphics.activeSelf != visible) graphics.SetActive(visible);
        // Also covers prefabs whose old NetworkTransform did not replicate rotation.
        if (graphics != null) graphics.transform.rotation = Quaternion.LookRotation(FlygonMoveRules.Direction(aim.Value, Vector3.forward));
        if (shownPhase != phase.Value) { shownPhase = phase.Value; PresentPhase(shownPhase); }
    }
    private void OnDeath(DamageInfo damage) { Despawn(); }
    public void Despawn() { if (IsServer && IsSpawned) NetworkObject.Despawn(true); }
    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        if (!cleaned)
        {
            cleaned = true; Cleanup();
            if (Actor != null) Actor.Pokemon.OnDeath -= OnDeath;
            if (IsServer)
            {
                FlygonEffectIds.Release(EffectId);
                if (combat != null) combat.FlygonEffectEnded(Kind, cast);
            }
        }
        base.OnNetworkDespawn();
    }
    public override void OnDestroy()
    {
        if (holdsAsset && handle.IsValid()) Addressables.Release(handle);
        holdsAsset = false; base.OnDestroy();
    }
}
