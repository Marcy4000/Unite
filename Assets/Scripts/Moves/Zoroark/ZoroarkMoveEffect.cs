using System.Collections;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;

public class ZoroarkMoveEffect : PrefabMoveEffect
{
    [SerializeField] private AvailableMoves move;
    [SerializeField] private GameObject dashVisual, markVisual;
    private CombatActionLock phaseLock;
    private bool airSteering;
    private double airSteeringUntil, airSteeringPausedUntil;
    private float airSteeringDistance;

    public ZoroarkExecution Execution { get; private set; }
    public bool AerialWindow => Execution?.AerialWindow ?? false;
    public float RemainingAirTime => Execution?.RemainingAirTime ?? 0;
    public bool IsNight => move == AvailableMoves.ZoroarkNightSlash;
    public bool IsFeint => move == AvailableMoves.ZoroarkFeintAttack;
    public bool Finishing => Execution?.Finishing ?? false;
    public bool UniqueReady => Execution?.UniqueReady ?? false;
    public bool CanSpendAerialReposition => Execution?.CanSpendAerialReposition ?? false;

    internal void BindExecution(ZoroarkExecution execution) { Execution = execution; }
    internal void Finish(CastOutcome outcome) => CompleteExecution(outcome);
    internal void SignalCastSucceeded() => MarkCastSucceeded();
    internal bool ValidTarget(Pokemon target, bool acquire) => CanHit(target, acquire);
    internal void DriveTo(Vector3 destination, float duration) => Drive(destination, duration);
    internal void Face(Vector3 direction) => FaceRPC(direction);
    internal void Animate(string state, float duration = 0, float blendDuration = 0) => PlayAnimation(state, duration, blendDuration);
    internal void SetPhaseLock(int mode) => PhaseLockRPC(mode);
    internal void SetDashVisible(bool visible) => DashVisualRPC(visible);
    internal void SetMark(bool visible, Vector3 position) => MarkRPC(visible, position);
    internal void SetAirSteering(bool active, double endsAt) => AirSteeringRPC(active, endsAt);

    public override bool TryRecast(Vector3 direction, ulong targetId) => Execution?.TryRecast(direction, targetId) ?? false;
    public bool TrySpendAerialReposition() => Execution?.TrySpendAerialReposition() ?? false;
    public void Movement(Vector2 input) => Execution?.Movement(input);
    public void MoveMark(Vector3 origin) => Execution?.MoveMark(origin);
    public void ValidHit(bool basic) => Execution?.ValidHit(basic);
    public void UniqueAttackLanded(bool hit) => Execution?.UniqueAttackLanded(hit);

    protected override void BeginPresentation()
    {
        if (dashVisual != null) dashVisual.SetActive(false);
        if (markVisual != null) markVisual.SetActive(false);
    }

    protected override IEnumerator Execute()
    {
        if (Execution == null)
        {
            Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        yield return Execution.RunLifecycle();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PhaseLockRPC(int mode)
    {
        phaseLock?.Dispose();
        phaseLock = null;
        if (mode != 0 && Actor != null)
            phaseLock = new CombatActionLock(Actor, true, lockServer: mode != 2, lockBattleItems: mode != 3, unlockedMoveSlot: mode == 2 ? 0 : -1);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void DashVisualRPC(bool visible) { if (dashVisual != null) dashVisual.SetActive(visible); }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void MarkRPC(bool visible, Vector3 location)
    {
        if (markVisual == null) return;
        markVisual.SetActive(visible && (Actor.IsOwner || (LobbyController.Instance != null && Actor.CurrentTeam.IsOnSameTeam(LobbyController.Instance.GetLocalPlayerTeam()))));
        markVisual.transform.position = location + Vector3.up * 0.08f;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void AirSteeringRPC(bool active, double endsAt)
    {
        airSteering = active;
        airSteeringUntil = endsAt;
        if (active) { airSteeringDistance = 0; airSteeringPausedUntil = 0; }
    }

    public void PauseAirSteering(float duration)
    {
        if (IsServer) PauseAirSteeringRPC(NetworkManager.ServerTime.Time + duration);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PauseAirSteeringRPC(double until) { airSteeringPausedUntil = until; }

    protected override void Update()
    {
        base.Update();
        if (Actor != null && dashVisual != null) dashVisual.transform.SetPositionAndRotation(Actor.transform.position, Actor.transform.rotation);
        if (!airSteering || Actor == null || !Actor.IsSpawned || (!IsServer && !Actor.IsOwner) || Actor.Pokemon.CurrentHp <= 0) return;
        double now = NetworkManager.ServerTime.Time;
        if (now >= airSteeringUntil || now < airSteeringPausedUntil || airSteeringDistance >= ZoroarkMoveRules.NightAirSteerDistance) return;
        ZoroarkCharacterRuntime character = Actor.MovesController.Combat?.Character as ZoroarkCharacterRuntime;
        Vector2 input = Vector2.ClampMagnitude(Actor.IsOwner ? Actor.MovementInput : character?.ZoroarkMovementInput ?? Vector2.zero, 1);
        float step = Mathf.Min(ZoroarkMoveRules.NightAirSteerSpeed * Time.deltaTime,
            ZoroarkMoveRules.NightAirSteerDistance - airSteeringDistance);
        Vector3 delta = new Vector3(input.x, 0, input.y) * step;
        if (delta.sqrMagnitude > 0)
            airSteeringDistance += Actor.PlayerMovement.MoveWithWalls(delta).magnitude;
    }

    protected override void Cleanup()
    {
        phaseLock?.Dispose();
        phaseLock = null;
        airSteering = false;
    }
}
