using System;
using Unite.Combat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private CombatActor combatRuntime;
    private bool combatTornDown;
    private readonly IDisposable[] combatSlotLocks = new IDisposable[3];
    private readonly NetworkVariable<CombatSlotSnapshot> combatSlotA = new NetworkVariable<CombatSlotSnapshot>();
    private readonly NetworkVariable<CombatSlotSnapshot> combatSlotB = new NetworkVariable<CombatSlotSnapshot>();
    private readonly NetworkVariable<CombatSlotSnapshot> combatUniteSlot = new NetworkVariable<CombatSlotSnapshot>();
    private readonly NetworkVariable<int> combatUniteEnergy = new NetworkVariable<int>();
    private readonly NetworkVariable<FixedString4096Bytes> combatCharacterState = new NetworkVariable<FixedString4096Bytes>();
    public CombatActor Combat
    {
        get
        {
            if (combatTornDown) return null;
            if (combatRuntime == null)
            {
                PlayerManager actor = playerManager != null ? playerManager : GetComponent<PlayerManager>();
                if (actor != null && CombatKitFactory.Supports(actor.Pokemon))
                {
                    combatRuntime = new CombatActor(actor);
                    ApplyCombatSnapshots();
                }
            }
            return combatRuntime;
        }
    }
    internal int ReplicatedCombatUniteEnergy => combatUniteEnergy.Value;
    internal BattleActionStatus CombatMoveStatus(int slot) => slot == 2 ? uniteMoveStatus : moveStatuses[slot];

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        combatTornDown = false;
        combatSlotA.OnValueChanged += OnCombatSlotA;
        combatSlotB.OnValueChanged += OnCombatSlotB;
        combatUniteSlot.OnValueChanged += OnCombatUniteSlot;
        combatUniteEnergy.OnValueChanged += OnCombatEnergy;
        combatCharacterState.OnValueChanged += OnCombatCharacterState;
        ApplyCombatSnapshots();
    }

    private void ApplyCombatSnapshots()
    {
        CombatActor runtime = Combat;
        if (runtime == null) return;
        runtime.ApplySnapshot(0, combatSlotA.Value);
        runtime.ApplySnapshot(1, combatSlotB.Value);
        runtime.ApplySnapshot(2, combatUniteSlot.Value);
        if (!IsServer) runtime.Character.ApplyPresentationState(combatCharacterState.Value.ToString());
    }

    private void OnCombatSlotA(CombatSlotSnapshot previous, CombatSlotSnapshot current) => Combat?.ApplySnapshot(0, current);
    private void OnCombatSlotB(CombatSlotSnapshot previous, CombatSlotSnapshot current) => Combat?.ApplySnapshot(1, current);
    private void OnCombatUniteSlot(CombatSlotSnapshot previous, CombatSlotSnapshot current) => Combat?.ApplySnapshot(2, current);
    private void OnCombatEnergy(int previous, int current) { if (IsOwner) { uniteMoveCharge = current; RefreshCombatUnite(); } }
    private void OnCombatCharacterState(FixedString4096Bytes previous, FixedString4096Bytes current)
    { if (!IsServer) Combat?.Character.ApplyPresentationState(current.ToString()); }

    internal void SetCombatSlot(int slot, CombatSlotSnapshot snapshot)
    {
        if (!IsServer || !IsSpawned) return;
        switch (slot)
        {
            case 0: combatSlotA.Value = snapshot; break;
            case 1: combatSlotB.Value = snapshot; break;
            case 2: combatUniteSlot.Value = snapshot; break;
        }
        Combat?.ApplySnapshot(slot, snapshot);
    }

    internal void SetCombatUniteEnergy(int energy)
    {
        if (IsServer && IsSpawned) combatUniteEnergy.Value = energy;
    }

    internal void SetCombatCharacterState(string state)
    {
        if (!IsServer || !IsSpawned) return;
        if (string.IsNullOrEmpty(state)) return;
        if (System.Text.Encoding.UTF8.GetByteCount(state) > 4090)
        { Debug.LogError("Combat presentation snapshot exceeded its bounded transport capacity."); return; }
        combatCharacterState.Value = new FixedString4096Bytes(state);
    }

    internal void ApplyCombatSlot(int slot, CombatSlotSnapshot snapshot, double remaining)
    {
        if (!IsOwner || !(GetMove((MoveType)slot) is CombatMove move) || move.MoveId != snapshot.MoveId) return;
        BattleActionStatus status = slot == 2 ? uniteMoveStatus : moveStatuses[slot];
        if (status == null) return;
        bool blocked = snapshot.Reserved || (snapshot.AwaitingEnd && !snapshot.FollowUpReady);
        if (blocked && combatSlotLocks[slot] == null) combatSlotLocks[slot] = AcquireMoveStatus(slot, ActionStatusType.Busy);
        else if (!blocked && combatSlotLocks[slot] != null) { combatSlotLocks[slot].Dispose(); combatSlotLocks[slot] = null; }
        if (slot == 2) { RefreshCombatUnite(); return; }
        status.Cooldown = (float)remaining;
        if (remaining > 0 && !status.HasStatus(ActionStatusType.Cooldown)) status.AddStatus(ActionStatusType.Cooldown);
        else if (remaining <= 0 && status.HasStatus(ActionStatusType.Cooldown)) status.RemoveStatus(ActionStatusType.Cooldown);
        UpdateMoveUI(slot);
    }

    private void RefreshCombatUnite()
    {
        if (!IsOwner || !(uniteMove is CombatMove)) return;
        uniteMoveCharge = combatUniteEnergy.Value;
        bool cooling = uniteMoveMaxCharge <= 0 || uniteMoveCharge < uniteMoveMaxCharge;
        if (cooling && !uniteMoveStatus.HasStatus(ActionStatusType.Cooldown)) uniteMoveStatus.AddStatus(ActionStatusType.Cooldown);
        else if (!cooling && uniteMoveStatus.HasStatus(ActionStatusType.Cooldown)) uniteMoveStatus.RemoveStatus(ActionStatusType.Cooldown);
        UpdateUniteMoveUI();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    internal void SubmitCombatActionRPC(long requestId, int slot, int revision, AvailableMoves moveId, Vector3 direction, ulong targetId, long followUpCastId)
        => Combat?.Receive(requestId, slot, revision, moveId, direction, targetId, followUpCastId);

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    internal void CombatActionResultRPC(long requestId, bool accepted, CastOutcome outcome) => Combat?.ReceiveResult(requestId, accepted);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    internal void QueryCombatRequestRPC(long requestId) => Combat?.Replay(requestId);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    internal void CancelCombatSlotRPC(int slot, int revision) => Combat?.CancelSlot(slot, revision);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    internal void UpdateCombatInputRPC(int slot, int revision, long castId, Vector3 direction) => Combat?.ReceiveInput(slot, revision, castId, direction);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    internal void SetCombatInputActiveRPC(int slot, int revision, bool active) => Combat?.ReceiveInputActive(slot, revision, active);

    public bool TryHandleCombatMovement(Vector2 input) => Combat?.Character.HandleMovement(input) ?? false;
    private void LateUpdate() => combatRuntime?.Character.LateTick();

    private void DisposeCombat()
    {
        combatTornDown = true;
        combatRuntime?.Dispose();
        combatRuntime = null;
        for (int slot = 0; slot < combatSlotLocks.Length; slot++) { combatSlotLocks[slot]?.Dispose(); combatSlotLocks[slot] = null; }
    }

    public override void OnNetworkDespawn()
    {
        DisposeLegacyRuntimesOnDespawn();
        combatSlotA.OnValueChanged -= OnCombatSlotA;
        combatSlotB.OnValueChanged -= OnCombatSlotB;
        combatUniteSlot.OnValueChanged -= OnCombatUniteSlot;
        combatUniteEnergy.OnValueChanged -= OnCombatEnergy;
        combatCharacterState.OnValueChanged -= OnCombatCharacterState;
        DisposeCombat();
        base.OnNetworkDespawn();
    }
}
