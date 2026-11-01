using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using UnityEngine;

public sealed class CombatActor : IDisposable
{
    private sealed class ExecutionRecord
    {
        public CastSession Session;
        public CombatExecutionContext Context;
        public CombatExecution Execution;
        public bool RequiresTarget;
        public double PreparationDeadline;
        public double NextContinuousInput;
    }

    private sealed class PendingInput : IDisposable
    {
        public CombatMove Move;
        public int Revision;
        public int Slot;
        public long RequestId;
        public double QueryAt;
        public readonly CombatScope Scope = new CombatScope();
        public void Dispose() => Scope.Dispose();
    }

    private readonly struct Command
    {
        public readonly CastRequest Request;
        public readonly Vector3 Direction;
        public readonly ulong TargetId;
        public readonly long FollowUpCastId;
        public Command(CastRequest request, Vector3 direction, ulong targetId, long followUpCastId)
        { Request = request; Direction = direction; TargetId = targetId; FollowUpCastId = followUpCastId; }
    }

    private readonly PlayerManager actor;
    private readonly MovesController controller;
    private readonly Dictionary<long, ExecutionRecord> executions = new Dictionary<long, ExecutionRecord>();
    private readonly Dictionary<long, PendingInput> pending = new Dictionary<long, PendingInput>();
    private readonly Dictionary<long, bool> receipts = new Dictionary<long, bool>();
    private readonly HashSet<long> admittedRequests = new HashSet<long>();
    private readonly HashSet<int> releasedInputs = new HashSet<int>();
    private readonly Queue<long> receiptHistory = new Queue<long>();
    private readonly ActionBuffer<Command> buffer = new ActionBuffer<Command>();
    private readonly ActionBuffer<BufferedInput> inputBuffer = new ActionBuffer<BufferedInput>();
    private readonly int[] learnedAt = { -1, -1, -1 };
    private readonly CombatSlotSnapshot[] replicas = new CombatSlotSnapshot[3];
    private long nextRequestId;
    private double nextEnergyTick;
    private bool disposed;
    private int uniteMaximum;
    private int uniteEnergy;
    public CombatLoadout Loadout { get; } = new CombatLoadout();
    public CastCoordinator Coordinator { get; }
    public CombatCharacterRuntime Character { get; }
    public CombatEffectSpawner Spawner { get; } = new CombatEffectSpawner();
    public double Now => controller.NetworkManager != null && controller.IsSpawned ? controller.NetworkManager.ServerTime.Time : Time.timeAsDouble;
    public int UniteEnergy => controller.IsServer ? uniteEnergy : controller.ReplicatedCombatUniteEnergy;
    public bool IsDisposed => disposed;

    private readonly struct BufferedInput
    {
        public readonly CombatMove Move;
        public readonly bool Released;
        public BufferedInput(CombatMove move, bool released) { Move = move; Released = released; }
    }

    public IEnumerable<PrefabMoveEffect> PrefabEffects
    {
        get
        {
            foreach (ExecutionRecord record in executions.Values)
                if (record.Execution.Effect != null) yield return record.Execution.Effect;
        }
    }

    public CombatActor(PlayerManager actor)
    {
        this.actor = actor;
        controller = actor.MovesController;
        Character = CombatKitFactory.Create(actor);
        Coordinator = new CastCoordinator(Loadout, () => Now);
        Coordinator.Changed += OnSessionChanged;
        Loadout.Removing += OnRemovingAbility;
        actor.Pokemon.OnDeath += OnDeath;
        actor.Pokemon.OnStatusChange += OnStatusChange;
        if (controller.IsServer) actor.Pokemon.OnOtherPokemonKilled += OnKill;
        foreach (LearnableMove row in actor.Pokemon.BaseStats.LearnableMoves)
            if (row.moves != null)
                foreach (MoveAsset move in row.moves)
                    if (move != null && move.moveType == MoveType.UniteMove && !row.isUpgraded)
                        uniteMaximum = Math.Max(uniteMaximum, move.uniteEnergyCost);
        nextEnergyTick = Now + 1;
    }

    public bool Equip(AvailableMoves moveId)
    {
        if (disposed || !(MoveDatabase.GetMove(moveId) is CombatMove definition)) return false;
        MoveAsset selected = null;
        int level = -1;
        bool upgraded = false;
        foreach (LearnableMove row in actor.Pokemon.BaseStats.LearnableMoves)
        {
            if (row.level > actor.Pokemon.CurrentLevel || row.moves == null) continue;
            foreach (MoveAsset asset in row.moves)
            {
                if (asset == null || asset.move != moveId || asset.moveType == MoveType.All) continue;
                if (row.isUpgraded) upgraded = true;
                else if (row.level >= level) { selected = asset; level = row.level; }
            }
        }
        if (selected == null) return false;
        int slot = (int)selected.moveType;
        if (slot < 0 || slot >= Loadout.SlotCount) return false;
        AbilityInstance previous = Loadout.Get(slot);
        if (previous != null && previous.Definition.Id != (int)moveId && level <= learnedAt[slot]) return false;
        learnedAt[slot] = Math.Max(learnedAt[slot], level);
        definition.IsUpgraded = upgraded;
        Loadout.Install(slot, new AbilityDefinition((int)moveId, definition.Name, definition.Cooldown,
            definition.CooldownOnEffectEnd, definition.MaxStoredUses, definition.IsStoredMove, definition.StoredUseInterval), upgraded);
        if (slot == 2) uniteMaximum = selected.uniteEnergyCost;
        PublishSlot(slot);
        return true;
    }

    public bool CanBeginInput(int slot, BattleActionStatus status)
    {
        if (disposed || status == null) return false;
        ActionStatusType blockers = status.GetStatus() & ~ActionStatusType.Cooldown;
        if (blockers != ActionStatusType.None) return false;
        return !status.HasStatus(ActionStatusType.Cooldown) || replicas[slot].FollowUpReady || Character.OverridesCooldown(slot);
    }

    public void HandleInput(CombatMove move, int slot, BattleActionStatus status, bool released)
    {
        if (released && move.StartsOnPress && move.IsActive) { move.Finish(); return; }
        AbilityInstance ability = Loadout.Get(slot);
        if (released && ability != null && inputBuffer.TryUpdate(slot, ability.Revision, Now,
            input => new BufferedInput(input.Move, true))) return;
        if (!CanBeginInput(slot, status))
        {
            if (!released && ability != null && CanBufferRecoveryInput(slot, status))
                inputBuffer.Put(slot, ability.Revision, new BufferedInput(move, false), Now, Character.MoveInputBufferDuration);
            else inputBuffer.Clear(slot);
            return;
        }
        if (controller.IsServerBasicAttackAnimating && controller.BasicAttack.MoveInputBufferDuration > 0)
        {
            if (ability != null) inputBuffer.Put(slot, ability.Revision, new BufferedInput(move, released), Now, controller.BasicAttack.MoveInputBufferDuration);
            return;
        }
        if (released) move.Finish();
        else move.Start(actor);
    }

    private bool CanBufferRecoveryInput(int slot, BattleActionStatus status)
    {
        if (disposed || status == null || !controller.IsOwner || !ActorCanAct()
            || Character.MoveInputBufferDuration <= 0 || !Character.CanBufferMoveInput) return false;
        ActionStatusType blockers = status.GetStatus() & ~ActionStatusType.Cooldown;
        return blockers == ActionStatusType.Busy
            && (!status.HasStatus(ActionStatusType.Cooldown) || replicas[slot].FollowUpReady || Character.OverridesCooldown(slot));
    }

    public void ClearInput() => inputBuffer.Clear();

    public bool IsFollowUpReady(int slot) => slot >= 0 && slot < replicas.Length && replicas[slot].FollowUpReady;

    public Pokemon GetFollowUpTarget(int slot)
    {
        if (!IsFollowUpReady(slot) || replicas[slot].FollowUpTargetId == ulong.MaxValue || controller.NetworkManager == null) return null;
        return controller.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(replicas[slot].FollowUpTargetId, out var target)
            ? target.GetComponent<Pokemon>() : null;
    }

    public void SetInputActive(CombatMove move, bool active)
    {
        if (!controller.IsOwner || !move.StartsOnPress || move.EndsOnRelease) return;
        int slot = (int)controller.GetMoveSlot(move);
        AbilityInstance ability = Loadout.Get(slot);
        if (ability == null || ability.Definition.Id != (int)move.MoveId) return;
        int revision = replicas[slot].MoveId == move.MoveId && replicas[slot].Revision > 0 ? replicas[slot].Revision : ability.Revision;
        foreach (PendingInput input in pending.Values)
            if (input.Slot == slot) { revision = input.Revision; break; }
        controller.SetCombatInputActiveRPC(slot, revision, active);
    }

    public void ReceiveInputActive(int slot, int revision, bool active)
    {
        if (!controller.IsServer || disposed || Loadout.Get(slot)?.Revision != revision) return;
        if (!(MoveDatabase.GetMove((AvailableMoves)Loadout.Get(slot).Definition.Id) is CombatMove move)
            || !move.StartsOnPress || move.EndsOnRelease) return;
        if (active) releasedInputs.Remove(slot); else releasedInputs.Add(slot);
        foreach (ExecutionRecord record in new List<ExecutionRecord>(executions.Values))
            if (record.Session.Request.Slot == slot && record.Session.Request.Revision == revision)
                record.Execution.SetInputActive(active);
    }

    public void UpdateInput(CombatMove move, Vector3 direction)
    {
        if (!controller.IsOwner || !move.StartsOnPress) return;
        int slot = (int)controller.GetMoveSlot(move);
        CombatSlotSnapshot snapshot = replicas[slot];
        if (snapshot.MoveId == move.MoveId && snapshot.CastId != 0)
            controller.UpdateCombatInputRPC(slot, snapshot.Revision, snapshot.CastId, direction);
    }

    public void ReceiveInput(int slot, int revision, long castId, Vector3 direction)
    {
        if (!controller.IsServer || disposed || !CombatTargeting.Finite(direction)
            || !executions.TryGetValue(castId, out ExecutionRecord record) || record.Session.Request.Slot != slot
            || record.Session.Request.Revision != revision || !record.Session.Accepted || Now < record.NextContinuousInput
            || !(record.Context.Definition is CombatMove move) || !move.StartsOnPress) return;
        record.NextContinuousInput = Now + 0.025;
        record.Execution.TryRecast(direction, ulong.MaxValue);
    }

    public void Submit(CombatMove move, Vector3 direction, Pokemon target)
    {
        int slot = (int)controller.GetMoveSlot(move);
        AbilityInstance ability = Loadout.Get(slot);
        if (disposed || ability == null || ability.Definition.Id != (int)move.MoveId || !controller.IsOwner) return;
        foreach (PendingInput input in pending.Values)
            if (input.Slot == slot) { SetInputActive(move, true); return; }
        int revision = replicas[slot].MoveId == move.MoveId && replicas[slot].Revision > 0 ? replicas[slot].Revision : ability.Revision;
        long requestId = ++nextRequestId;
        var pendingInput = new PendingInput { Move = move, Slot = slot, Revision = revision, RequestId = requestId, QueryAt = Now + 3 };
        pendingInput.Scope.Own(controller.AcquireMoveStatus(slot, ActionStatusType.Busy));
        if (move.LockDuringLoad) pendingInput.Scope.Own(new CombatActionLock(actor, move.LockMovementDuringLoad, false));
        pending.Add(requestId, pendingInput);
        long followUp = replicas[slot].FollowUpReady ? replicas[slot].CastId : 0;
        controller.SubmitCombatActionRPC(requestId, slot, revision, move.MoveId, direction,
            target != null ? target.NetworkObjectId : ulong.MaxValue, followUp);
    }

    public void Receive(long requestId, int slot, int revision, AvailableMoves moveId, Vector3 direction, ulong targetId, long followUpCastId)
    {
        if (!controller.IsServer || disposed) return;
        if (receipts.TryGetValue(requestId, out bool accepted))
        {
            controller.CombatActionResultRPC(requestId, accepted, CastOutcome.None);
            return;
        }
        if (admittedRequests.Contains(requestId) || Coordinator.FindRequest(requestId) != null) { Replay(requestId); return; }
        if (!Coordinator.TryAdmit(requestId)) { Reject(requestId); return; }
        admittedRequests.Add(requestId);
        if (Loadout.Get(slot)?.Revision == revision) releasedInputs.Remove(slot);
        Process(new Command(new CastRequest(requestId, slot, revision, (int)moveId), direction, targetId, followUpCastId), false);
    }

    private bool ActorCanAct()
    {
        return !disposed && controller.IsSpawned && actor.Pokemon.CurrentHp > 0 && actor.PlayerState == PlayerState.Alive
            && !actor.Pokemon.HasStatusEffect(StatusType.Incapacitated) && !actor.Pokemon.HasStatusEffect(StatusType.Frozen)
            && !actor.Pokemon.HasStatusEffect(StatusType.Asleep) && !actor.Pokemon.HasStatusEffect(StatusType.Bound)
            && (GameManager.Instance == null || GameManager.Instance.GameState == GameState.Playing);
    }

    private void Process(Command command, bool fromBuffer)
    {
        CastRequest request = command.Request;
        AbilityInstance ability = Loadout.Get(request.Slot);
        if (!ActorCanAct() || !CombatTargeting.Finite(command.Direction) || ability == null
            || ability.Revision != request.Revision || ability.Definition.Id != request.AbilityId)
        { Reject(request.RequestId); return; }
        if (controller.ServerActionLocks > 0)
        {
            if (!fromBuffer)
            {
                buffer.Clear(request.Slot, previous => Reject(previous.Request.RequestId));
                buffer.Put(request.Slot, request.Revision, command, Now, 0.18);
            }
            return;
        }
        if (command.FollowUpCastId != 0)
        {
            if (executions.TryGetValue(command.FollowUpCastId, out ExecutionRecord active)
                && ReferenceEquals(active.Session.Ability, ability) && active.Session.Accepted
                && active.Execution.TryRecast(command.Direction, command.TargetId))
            {
                Remember(request.RequestId, true);
                controller.CombatActionResultRPC(request.RequestId, true, CastOutcome.None);
                if (active.Context.Definition.IsOffensive) actor.Pokemon.NotifyOffensiveAction();
                PublishSlot(request.Slot);
            }
            else Reject(request.RequestId);
            return;
        }
        var definition = MoveDatabase.GetMove((AvailableMoves)request.AbilityId) as CombatMove;
        if (definition == null) { Reject(request.RequestId); return; }
        definition.IsUpgraded = ability.Upgraded;
        definition.playerManager = actor;
        Vector3 direction = command.Direction;
        direction.y = 0;
        direction = definition.AimStyle == MoveAimStyle.Ground ? Vector3.ClampMagnitude(direction, definition.Range)
            : direction.sqrMagnitude > 0.001f ? direction.normalized : actor.transform.forward;
        Pokemon target = null;
        bool requiresTarget = definition.TargetRelation != CombatTargetRelation.None
            && (!definition.TargetOptional || command.TargetId != ulong.MaxValue);
        if (requiresTarget)
        {
            if (!controller.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(command.TargetId, out var targetObject)
                || !targetObject.TryGetComponent(out target) || !ValidTarget(definition, target)) { Reject(request.RequestId); return; }
        }
        var context = new CombatExecutionContext { Actor = actor, Slot = request.Slot, MoveId = (AvailableMoves)request.AbilityId,
            Definition = definition, Direction = direction, Target = target, Upgraded = ability.Upgraded,
            InputActive = !releasedInputs.Contains(request.Slot) };
        if (!Character.PrepareCast(context) || (request.Slot == 2 && (uniteMaximum <= 0 || uniteEnergy < uniteMaximum)))
        { Reject(request.RequestId); return; }
        double cooldown = definition.EffectiveCooldown(ability.Upgraded) * Mathf.Max(0.01f, 1 - actor.Pokemon.GetCDR() / 100f);
        if (!Coordinator.TryPrepare(request, cooldown, context.IgnoreCooldown, out CastSession session, out _))
        { Reject(request.RequestId); return; }
        context.CastId = session.CastId;
        context.Scope = session.Scope.Own(new CombatScope());
        context.Finish = outcome => Coordinator.End(session, outcome);
        CombatExecution execution = Character.CreateExecution(context) ?? new PrefabCombatExecution(context, this);
        var record = new ExecutionRecord { Session = session, Context = context, Execution = execution,
            RequiresTarget = requiresTarget, PreparationDeadline = Now + 8 };
        executions.Add(session.CastId, record);
        session.Scope.Own(new CombatCallbackLease(() => execution.Stop(session.Outcome)));
        controller.StartCoroutine(Prepare(record));
        PublishSlot(request.Slot);
    }

    private IEnumerator Prepare(ExecutionRecord record)
    {
        IEnumerator routine = record.Execution.Prepare();
        yield return RunPreparation(routine, record);
        CastSession session = record.Session;
        if (session.Phase != CastPhase.Preparing) yield break;
        if (!record.Execution.Prepared || !ActorCanAct() || controller.ServerActionLocks > 0 || !Character.RevalidateCast(record.Context)
            || (record.RequiresTarget && !ValidTarget(record.Context.Definition, record.Context.Target))
            || (session.Request.Slot == 2 && uniteEnergy < uniteMaximum))
        { Coordinator.End(session, CastOutcome.PreparationFailed); yield break; }
        if (!Coordinator.TryCommit(session)) yield break;
        if (record.Context.Slot == 2)
        {
            record.Context.UniteRefund = new UniteCastRefund(uniteMaximum);
            uniteEnergy = 0;
            controller.SetCombatUniteEnergy(0);
        }
        try
        {
            Character.CommitCast(record.Context);
            if (record.Context.Definition.IsOffensive) actor.Pokemon.NotifyOffensiveAction();
            record.Execution.Start();
            if (!record.Context.Definition.UsesExplicitUniteCastCheckpoint) record.Context.MarkCastSucceeded();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Coordinator.End(session, CastOutcome.ExecutionFailed);
        }
        PublishSlot(record.Context.Slot);
    }

    private IEnumerator RunPreparation(IEnumerator routine, ExecutionRecord record)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0 && record.Session.Phase == CastPhase.Preparing)
        {
            IEnumerator current = stack.Peek();
            bool advanced;
            try { advanced = current.MoveNext(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Coordinator.End(record.Session, CastOutcome.PreparationFailed);
                yield break;
            }
            if (!advanced) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; }
            if (current.Current is IEnumerator nested) stack.Push(nested);
            else yield return current.Current;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
    }

    private bool ValidTarget(PrefabMove definition, Pokemon target)
    {
        if (definition is CombatMove move && move.TargetRelation == CombatTargetRelation.Ally)
            return target != null && target != actor.Pokemon && target.IsSpawned && target.CurrentHp > 0
                && target.Type == PokemonType.Player && target.TeamMember.IsOnSameTeam(actor.CurrentTeam)
                && definition.IsTargetInCastRange(actor.transform.position, target.transform.position);
        return target != null && target.IsSpawned && target.CurrentHp > 0
            && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible)
            && !target.HasStatusEffect(StatusType.Untargetable) && !target.HasStatusEffect(StatusType.Invisible)
            && definition.IsTargetInCastRange(actor.transform.position, target.transform.position);
    }

    private void OnSessionChanged(CastSession session)
    {
        if (!controller.IsServer || !controller.IsSpawned) return;
        if (session.Accepted && session.Phase == CastPhase.Executing)
        {
            Remember(session.Request.RequestId, true);
            controller.CombatActionResultRPC(session.Request.RequestId, true, CastOutcome.None);
        }
        if (session.Phase == CastPhase.Ended)
        {
            if (executions.TryGetValue(session.CastId, out ExecutionRecord record)
                && record.Context.UniteRefund != null
                && record.Context.UniteRefund.TryRefund(session.Outcome, out int minimumEnergy))
            {
                uniteEnergy = Math.Min(uniteMaximum, Math.Max(uniteEnergy, minimumEnergy));
                controller.SetCombatUniteEnergy(uniteEnergy);
            }
            executions.Remove(session.CastId);
            if (!session.Accepted) Reject(session.Request.RequestId);
            foreach (Exception exception in session.Scope.CleanupErrors) Debug.LogException(exception);
        }
        PublishSlot(session.Request.Slot);
    }

    private void Remember(long requestId, bool accepted)
    {
        admittedRequests.Remove(requestId);
        Coordinator.DiscardAdmission(requestId);
        if (receipts.ContainsKey(requestId)) return;
        receipts.Add(requestId, accepted);
        receiptHistory.Enqueue(requestId);
        while (receiptHistory.Count > 256) receipts.Remove(receiptHistory.Dequeue());
    }

    private void Reject(long requestId)
    {
        Remember(requestId, false);
        controller.CombatActionResultRPC(requestId, false, CastOutcome.Rejected);
    }

    public void Replay(long requestId)
    {
        if (receipts.TryGetValue(requestId, out bool accepted)) controller.CombatActionResultRPC(requestId, accepted, CastOutcome.None);
        else
        {
            CastSession session = Coordinator.FindRequest(requestId);
            if (session == null && admittedRequests.Contains(requestId)) return;
            if (session == null || session.Phase == CastPhase.Ended) Reject(requestId);
            else if (session.Accepted) controller.CombatActionResultRPC(requestId, true, CastOutcome.None);
        }
    }

    public void ReceiveResult(long requestId, bool accepted)
    {
        if (!pending.TryGetValue(requestId, out PendingInput input)) return;
        pending.Remove(requestId);
        input.Dispose();
        if (accepted && ReferenceEquals(controller.GetMove((MoveType)input.Slot), input.Move)) input.Move.Accept();
        else if (!accepted) input.Move.EndInput();
    }

    public void ApplySnapshot(int slot, CombatSlotSnapshot snapshot)
    {
        CombatSlotSnapshot previous = replicas[slot];
        replicas[slot] = snapshot;
        if (controller.IsOwner)
        {
            controller.ApplyCombatSlot(slot, snapshot, Math.Max(0, snapshot.CooldownUntil - Now));
            if (previous.CastId != 0 && snapshot.CastId == 0 && controller.GetMove((MoveType)slot) is CombatMove move && move.StartsOnPress)
                move.EndInput();
        }
    }

    public void PublishSlot(int slot)
    {
        if (!controller.IsServer || !controller.IsSpawned || slot < 0 || slot >= Loadout.SlotCount) return;
        AbilityInstance ability = Loadout.Get(slot);
        if (ability == null) return;
        AbilityAvailability available = ability.Resources.Read(Now);
        var snapshot = new CombatSlotSnapshot { Revision = ability.Revision, MoveId = (AvailableMoves)ability.Definition.Id,
            Charges = available.Charges, CooldownUntil = available.Remaining > 0 ? Math.Round(Now + available.Remaining, 4) : 0,
            Reserved = available.Reserved, AwaitingEnd = available.AwaitingExecutionEnd, FollowUpTargetId = ulong.MaxValue };
        foreach (ExecutionRecord record in executions.Values)
        {
            if (!ReferenceEquals(record.Session.Ability, ability) || !record.Session.Accepted) continue;
            bool held = record.Context.Definition is CombatMove input && input.StartsOnPress;
            if (held || record.Execution.FollowUpReady)
            {
                snapshot.FollowUpReady = record.Execution.FollowUpReady;
                snapshot.CastId = record.Session.CastId;
                snapshot.FollowUpTargetId = record.Execution.FollowUpTarget != null ? record.Execution.FollowUpTarget.NetworkObjectId : ulong.MaxValue;
                break;
            }
        }
        controller.SetCombatSlot(slot, snapshot);
    }

    public void Tick()
    {
        if (disposed) return;
        Character.Tick();
        if (controller.IsServer)
        {
            buffer.Expire(Now, command => Reject(command.Request.RequestId));
            if (!ActorCanAct())
            {
                buffer.Clear(command => Reject(command.Request.RequestId));
                foreach (ExecutionRecord record in new List<ExecutionRecord>(executions.Values))
                    if (record.Session.Phase == CastPhase.Preparing || actor.Pokemon.CurrentHp <= 0)
                        Coordinator.End(record.Session, CastOutcome.Interrupted);
            }
            foreach (ExecutionRecord record in new List<ExecutionRecord>(executions.Values))
                if (record.Session.Phase == CastPhase.Preparing && Now >= record.PreparationDeadline) Coordinator.End(record.Session, CastOutcome.PreparationFailed);
            for (int slot = 0; slot < Loadout.SlotCount; slot++)
            {
                AbilityInstance ability = Loadout.Get(slot);
                if (ability == null) continue;
                if (controller.ServerActionLocks == 0 && buffer.TryTake(slot, ability.Revision, Now, out Command command)) Process(command, true);
                PublishSlot(slot);
            }
            if (Now >= nextEnergyTick)
            {
                nextEnergyTick = Now + 1;
                if (GameManager.Instance == null || GameManager.Instance.GameState == GameState.Playing) GrantUniteEnergy(900);
            }
            controller.SetCombatCharacterState(Character.CapturePresentationState());
        }
        if (controller.IsOwner)
        {
            for (int slot = 0; slot < replicas.Length; slot++) ApplySnapshot(slot, replicas[slot]);
            inputBuffer.Expire(Now, null);
            if (!ActorCanAct()) inputBuffer.Clear();
            if (!controller.IsServerBasicAttackAnimating)
            {
                for (int slot = 0; slot < Loadout.SlotCount; slot++)
                {
                    AbilityInstance ability = Loadout.Get(slot);
                    BattleActionStatus status = controller.CombatMoveStatus(slot);
                    if (ability == null || !CanBeginInput(slot, status)
                        || !inputBuffer.TryTake(slot, ability.Revision, Now, out BufferedInput input)) continue;
                    if (!ReferenceEquals(controller.GetMove((MoveType)slot), input.Move)) continue;
                    input.Move.Start(actor);
                    if (input.Released) input.Move.Finish();
                }
            }
            foreach (PendingInput input in new List<PendingInput>(pending.Values))
                if (Now >= input.QueryAt) { input.QueryAt = Now + 2; controller.QueryCombatRequestRPC(input.RequestId); }
        }
    }

    public void GrantUniteEnergy(int amount)
    {
        if (!controller.IsServer || amount <= 0 || uniteMaximum <= 0) return;
        uniteEnergy = (int)Math.Min(uniteMaximum, (long)uniteEnergy + amount);
        controller.SetCombatUniteEnergy(uniteEnergy);
    }

    private void OnKill(ulong targetId)
    {
        if (controller.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var target)
            && target.TryGetComponent(out Pokemon pokemon) && pokemon.Type == PokemonType.Wild) GrantUniteEnergy(5000);
    }

    private void OnDeath(DamageInfo damage)
    {
        buffer.Clear(command => Reject(command.Request.RequestId));
        inputBuffer.Clear();
        if (controller.IsServer) Coordinator.InterruptAll(CastOutcome.Interrupted);
        foreach (PendingInput input in pending.Values) input.Dispose();
        pending.Clear();
    }

    private void OnRemovingAbility(AbilityInstance ability)
    {
        releasedInputs.Remove(ability.Slot);
        buffer.Clear(ability.Slot, command => Reject(command.Request.RequestId));
        inputBuffer.Clear(ability.Slot);
        foreach (PendingInput input in new List<PendingInput>(pending.Values))
            if (input.Slot == ability.Slot) { pending.Remove(input.RequestId); input.Dispose(); }
    }

    public void Cancel(CombatMove move)
    {
        int slot = (int)controller.GetMoveSlot(move);
        AbilityInstance ability = Loadout.Get(slot);
        if (ability != null && ability.Definition.Id == (int)move.MoveId) controller.CancelCombatSlotRPC(slot, ability.Revision);
    }

    public void CancelSlot(int slot, int revision)
    {
        if (!controller.IsServer || Loadout.Get(slot)?.Revision != revision) return;
        buffer.Clear(slot, command => Reject(command.Request.RequestId));
        foreach (ExecutionRecord record in new List<ExecutionRecord>(executions.Values))
            if (record.Session.Request.Slot == slot && record.Session.Request.Revision == revision) Coordinator.End(record.Session, CastOutcome.Cancelled);
    }

    public void ReduceCooldown(int slot, double amount, double fraction = 0)
    {
        if (!controller.IsServer) return;
        AbilityInstance ability = Loadout.Get(slot);
        if (ability == null) return;
        if (fraction > 0) ability.Resources.ReduceCooldownByFraction(fraction, Now);
        else ability.Resources.ReduceCooldown(amount, Now);
        PublishSlot(slot);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        actor.Pokemon.OnDeath -= OnDeath;
        actor.Pokemon.OnStatusChange -= OnStatusChange;
        actor.Pokemon.OnOtherPokemonKilled -= OnKill;
        Coordinator.Dispose();
        Character.Dispose();
        Loadout.Removing -= OnRemovingAbility;
        foreach (PendingInput input in pending.Values) input.Dispose();
        pending.Clear();
        executions.Clear();
        buffer.Clear();
        inputBuffer.Clear();
    }

    private void OnStatusChange(StatusEffect effect, bool added)
    {
        if (!added) return;
        if (effect.Type != StatusType.Incapacitated && effect.Type != StatusType.Frozen
            && effect.Type != StatusType.Asleep && effect.Type != StatusType.Bound) return;
        inputBuffer.Clear();
        if (!controller.IsServer) return;
        buffer.Clear(command => Reject(command.Request.RequestId));
        foreach (ExecutionRecord record in new List<ExecutionRecord>(executions.Values))
            if (record.Session.Phase == CastPhase.Preparing) Coordinator.End(record.Session, CastOutcome.Interrupted);
    }
}
