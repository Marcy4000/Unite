using System;
using System.Collections;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;

public class CombatCharacterRuntime : IDisposable
{
    protected PlayerManager Actor { get; }
    protected MovesController Controller => Actor.MovesController;
    protected Pokemon Pokemon => Actor.Pokemon;
    protected bool IsServer => Controller.IsServer;
    protected bool IsOwner => Controller.IsOwner;
    protected bool IsSpawned => Controller.IsSpawned;
    protected Transform transform => Actor.transform;
    protected NetworkManager NetworkManager => Controller.NetworkManager;

    public CombatCharacterRuntime(PlayerManager actor) { Actor = actor; }
    public virtual bool CanStartBasicAttack => true;
    public virtual void Tick() { }
    public virtual void LateTick() { }
    public virtual bool HandleMovement(Vector2 input) => false;
    public virtual bool OverridesCooldown(int slot) => false;
    public virtual bool PrepareCast(CombatExecutionContext context) => true;
    public virtual bool RevalidateCast(CombatExecutionContext context) => true;
    public virtual void CommitCast(CombatExecutionContext context) { }
    public virtual void ConfigureEffect(PrefabMoveEffect effect, CombatExecutionContext context) { }
    public virtual CombatExecution CreateExecution(CombatExecutionContext context) => null;
    public virtual string CapturePresentationState() => string.Empty;
    public virtual void ApplyPresentationState(string state) { }
    public virtual void Dispose() { }
}

public sealed class CombatExecutionContext
{
    public PlayerManager Actor;
    public int Slot;
    public AvailableMoves MoveId;
    public PrefabMove Definition;
    public Vector3 Direction;
    public Pokemon Target;
    public bool Upgraded;
    public bool IgnoreCooldown;
    public bool InputActive = true;
    public object CharacterData;
    public long CastId;
    public CombatScope Scope;
    public Action<CastOutcome> Finish;
}

public abstract class CombatExecution
{
    protected CombatExecutionContext Context { get; }
    protected PlayerManager Actor => Context.Actor;
    protected MovesController Controller => Actor.MovesController;
    public bool Prepared { get; protected set; } = true;
    public virtual bool FollowUpReady => false;
    public CombatExecution(CombatExecutionContext context) { Context = context; }
    public virtual IEnumerator Prepare() { yield break; }
    public abstract void Start();
    public virtual bool TryRecast(Vector3 direction, ulong targetId) => false;
    public virtual void SetInputActive(bool active) { Context.InputActive = active; }
    public abstract void Stop(CastOutcome reason);
}

public sealed class CombatCallbackLease : IDisposable
{
    private Action release;
    public CombatCallbackLease(Action release) { this.release = release; }
    public void Dispose() { Action previous = release; release = null; previous?.Invoke(); }
}
