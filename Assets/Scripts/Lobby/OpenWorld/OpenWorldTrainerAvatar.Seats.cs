using System;
using Unity.Netcode;
using UnityEngine;

public sealed partial class OpenWorldTrainerAvatar
{
    // Owner-authoritative, like the lobby trainer's transform. 0 = free, 1 = sitting, 2 = standing up.
    private readonly NetworkVariable<byte> seatPhase = new NetworkVariable<byte>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<double> seatStartedAt = new NetworkVariable<double>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> seatReserved = new NetworkVariable<bool>();
    private readonly NetworkVariable<Vector3> reservedSeatPosition = new NetworkVariable<Vector3>();
    private OpenWorldNpc pendingSeat;
    private uint seatRequestId;
    private OpenWorldNpc activeSeat;
    private Animator seatAnimator;
    private byte appliedSeatPhase;
    private bool seatStateEntered;

    public bool IsSeated => IsSpawned && seatPhase.Value != 0;
    public bool CanLeaveSeat => IsSeated && seatPhase.Value == 1 && !IsInputBlocked();
    public bool IsUsingSeat(OpenWorldNpc seat) => IsSeated && activeSeat == seat;

    public bool IsSeatAvailable(OpenWorldNpc seat)
    {
        if (seat == null || !IsSpawned) return false;
        foreach (var spawnedObject in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            var avatar = spawnedObject.GetComponent<OpenWorldTrainerAvatar>();
            if (avatar != null && avatar.seatReserved.Value &&
                (avatar.reservedSeatPosition.Value - seat.SeatPosition).sqrMagnitude < 0.0001f) return false;
        }
        return true;
    }

    public bool TryUseSeat(OpenWorldNpc seat)
    {
        if (seat == null || !seat.isActiveAndEnabled || !IsSpawned || !IsOwner || !IsClient ||
            LocalAvatar != this || pendingSeat != null || !CanStartEmote || AnimationAnimator == null ||
            !IsSeatAvailable(seat)) return false;
        var animator = AnimationAnimator;
        if (!animator.HasState(0, SeatStateHash(false)) || !animator.HasState(0, SeatStateHash(true))) return false;
        pendingSeat = seat;
        RequestSeatRpc(seat.SeatPosition, ++seatRequestId);
        return true;
    }

    private bool TryReserveSeat(Vector3 position)
    {
        if (!IsServer || seatReserved.Value) return false;
        foreach (var seat in FindObjectsByType<OpenWorldNpc>(FindObjectsSortMode.None))
        {
            if (seat.Interaction != OpenWorldNpcInteraction.Seat ||
                (seat.SeatPosition - position).sqrMagnitude >= 0.0001f || !IsSeatAvailable(seat)) continue;
            reservedSeatPosition.Value = seat.SeatPosition;
            seatReserved.Value = true;
            return true;
        }
        return false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestSeatRpc(Vector3 position, uint requestId)
    {
        SeatResultRpc(TryReserveSeat(position), requestId);
    }

    [Rpc(SendTo.Owner)]
    private void SeatResultRpc(bool accepted, uint requestId)
    {
        if (requestId != seatRequestId) return;
        var seat = pendingSeat;
        pendingSeat = null;
        if (!accepted) return;
        if (seat == null || !seat.isActiveAndEnabled || !isActiveAndEnabled || !CanStartEmote || AnimationAnimator == null)
        {
            ReleaseSeatRpc();
            return;
        }
        emotes?.CancelForMovement();
        interactionAnimation?.StopPlayback();
        activeSeat = seat;
        SetPose(seat.SeatPosition, seat.SeatYaw, false);
        seatStartedAt.Value = NetworkManager.ServerTime.Time;
        seatPhase.Value = 1;
        UpdateSeatAnimation();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReleaseSeatRpc()
    {
        seatReserved.Value = false;
    }

    public bool TryLeaveSeat()
    {
        if (!IsOwner || LocalAvatar != this || !CanLeaveSeat) return false;
        seatStartedAt.Value = NetworkManager.ServerTime.Time;
        seatPhase.Value = 2;
        UpdateSeatAnimation();
        return true;
    }

    private string SeatStateName(bool standing) =>
        $"ani_{(standing ? "standup" : "sitdown")}_lob_{(trainerModel != null && trainerModel.IsMale ? "male" : "female")}";
    private int SeatStateHash(bool standing) => Animator.StringToHash(SeatStateName(standing));

    private void UpdateSeatAnimation()
    {
        if (!IsSeated)
        {
            if (seatAnimator != null && seatAnimator.isActiveAndEnabled) seatAnimator.SetTrigger(ResetAnim);
            seatAnimator = null;
            appliedSeatPhase = 0;
            activeSeat = null;
            return;
        }
        if (IsOwner && (activeSeat == null || !activeSeat.isActiveAndEnabled))
        {
            ClearSeat();
            return;
        }
        var animator = AnimationAnimator;
        if (animator == null || !animator.isActiveAndEnabled) return;
        bool standing = seatPhase.Value == 2;
        int stateHash = SeatStateHash(standing);
        if (seatAnimator != animator || appliedSeatPhase != seatPhase.Value)
        {
            seatAnimator = animator;
            appliedSeatPhase = seatPhase.Value;
            seatStateEntered = false;
            animator.ResetTrigger(StartRun);
            animator.ResetTrigger(StopRun);
            animator.ResetTrigger(ResetAnim);
            animator.SetBool(UseAltIdle, false);
            animator.speed = 1f;
            string stateName = SeatStateName(standing);
            var clip = Array.Find(animator.runtimeAnimatorController.animationClips, animation => animation.name == stateName);
            float elapsed = (float)Math.Max(0d, NetworkManager.ServerTime.Time - seatStartedAt.Value);
            // Late joiners enter the existing seated idle instead of replaying the sit-down.
            if (!standing && clip != null && elapsed >= clip.length)
            {
                string idle = trainerModel.IsMale ? "ani_sit01_lob_male" : "ani_sit_lob_female";
                animator.Play(Animator.StringToHash(idle), 0, 0f);
            }
            else animator.Play(stateHash, 0, clip != null ? Mathf.Min(elapsed / clip.length, 0.99f) : 0f);
            return;
        }
        if (!standing || !IsOwner) return;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == stateHash) seatStateEntered = true;
        // Wait for the controller's transition to finish before restoring movement/gravity.
        if (seatStateEntered && state.shortNameHash != stateHash && !animator.IsInTransition(0))
        {
            ClearSeat();
        }
    }

    public void ClearSeat()
    {
        ++seatRequestId;
        pendingSeat = null;
        if (IsSpawned && IsOwner)
        {
            seatPhase.Value = 0;
            ReleaseSeatRpc();
        }
        else if (IsSpawned && IsServer) seatReserved.Value = false;
        activeSeat = null;
        appliedSeatPhase = 0;
        if (seatAnimator != null && seatAnimator.isActiveAndEnabled) seatAnimator.SetTrigger(ResetAnim);
        seatAnimator = null;
        verticalSpeed = 0f;
    }
}
