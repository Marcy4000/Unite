using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(OpenWorldTrainerAvatar))]
[DefaultExecutionOrder(-50)]
public sealed class OpenWorldTrainerEmotes : NetworkBehaviour
{
    [SerializeField] private OpenWorldEmoteCatalog catalog;
    [SerializeField, Min(0f)] private float blendDuration = 0.2f;

    private struct Playback : INetworkSerializable, IEquatable<Playback>
    {
        public int Index;
        public uint Sequence;
        public double StartedAt;
        public float Duration;
        public bool Loop;
        public uint RequestId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Index);
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref StartedAt);
            serializer.SerializeValue(ref Duration);
            serializer.SerializeValue(ref Loop);
            serializer.SerializeValue(ref RequestId);
        }

        public bool Equals(Playback other) => Index == other.Index && Sequence == other.Sequence &&
            StartedAt.Equals(other.StartedAt) && Duration.Equals(other.Duration) && Loop == other.Loop && RequestId == other.RequestId;
    }

    private readonly NetworkVariable<Playback> playback = new NetworkVariable<Playback>();
    private static readonly int ResetAnim = Animator.StringToHash("ResetAnim");
    private static readonly int StartRun = Animator.StringToHash("startRun");
    private static readonly int StopRun = Animator.StringToHash("stopRun");
    private static readonly int UseAltIdle = Animator.StringToHash("useAltIdle");
    private const float IdleBlendDuration = 0.2f;
    private OpenWorldTrainerAvatar avatar;
    private Animator playbackAnimator;
    private uint appliedSequence;
    private bool returningToIdle;
    private float previousAnimatorSpeed = 1f;
    private string currentClothing;
    private uint localRequestId;
    private uint cancelledThroughRequest;
    private bool hasCancellation;
    private bool requestedPlayback;

    public OpenWorldEmoteCatalog Catalog => catalog;
    public Animator ActiveAnimator => avatar != null ? avatar.AnimationAnimator : null;
    public int CurrentEmoteIndex => IsPlaying ? playback.Value.Index : -1;
    public bool IsPlaying => IsSpawned && playback.Value.Sequence != 0 && playback.Value.Index >= 0 &&
        !(IsOwner && hasCancellation && !IsNewer(playback.Value.RequestId, cancelledThroughRequest)) &&
        (playback.Value.Loop || NetworkManager.ServerTime.Time < playback.Value.StartedAt + playback.Value.Duration + IdleBlendDuration);
    public bool IsReady => IsSpawned && IsOwner && IsClient && avatar != null &&
        ActiveAnimator != null && ActiveAnimator.isActiveAndEnabled;
    public bool CanPlay => IsReady && avatar.CanStartEmote;

    private void Awake() => avatar = GetComponent<OpenWorldTrainerAvatar>();
    private void OnDisable() => ReturnToIdle();

    public override void OnNetworkSpawn()
    {
        currentClothing = avatar.Clothing;
        avatar.IdentityChanged += OnIdentityChanged;
    }

    public override void OnNetworkDespawn()
    {
        avatar.IdentityChanged -= OnIdentityChanged;
        ReturnToIdle();
        playbackAnimator = null;
        appliedSequence = 0;
        localRequestId = 0;
        hasCancellation = false;
        requestedPlayback = false;
    }

    public bool TryPlay(int index)
    {
        if (!CanPlay || catalog == null || !catalog.TryGet(index, ActiveAnimator, out _)) return false;
        requestedPlayback = true;
        PlayEmoteRpc(index, NextRequestId());
        return true;
    }

    public void CancelForMovement()
    {
        if (!IsSpawned || !IsOwner || !IsClient || (!IsPlaying && !requestedPlayback)) return;
        cancelledThroughRequest = NextRequestId();
        hasCancellation = true;
        requestedPlayback = false;
        ReturnToIdle();
        CancelEmoteRpc(cancelledThroughRequest);
    }

    private static bool IsNewer(uint candidate, uint previous) => unchecked((int)(candidate - previous)) > 0;

    private uint NextRequestId()
    {
        if (IsNewer(playback.Value.RequestId, localRequestId)) localRequestId = playback.Value.RequestId;
        localRequestId++;
        if (localRequestId == 0) localRequestId = 1;
        return localRequestId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void PlayEmoteRpc(int index, uint requestId)
    {
        if (!IsNewer(requestId, playback.Value.RequestId) || catalog == null || !catalog.TryGet(index,
            PlayerClothesInfo.Deserialize(avatar.Clothing).IsMale, out var entry)) return;
        uint sequence = playback.Value.Sequence + 1;
        if (sequence == 0) sequence = 1;
        playback.Value = new Playback
        {
            Index = index,
            Sequence = sequence,
            StartedAt = NetworkManager.ServerTime.Time,
            Duration = entry.Duration,
            Loop = entry.Clip.isLooping,
            RequestId = requestId
        };
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void CancelEmoteRpc(uint requestId)
    {
        if (IsNewer(requestId, playback.Value.RequestId)) ClearPlayback(requestId);
    }

    private void OnIdentityChanged(OpenWorldTrainerAvatar changed)
    {
        if (currentClothing == changed.Clothing) return;
        currentClothing = changed.Clothing;
        if (IsServer) ClearPlayback();
        ReturnToIdle();
    }

    private void ClearPlayback() => ClearPlayback(playback.Value.RequestId);

    private void ClearPlayback(uint requestId)
    {
        var current = playback.Value;
        if (current.Index < 0 && current.RequestId == requestId) return;
        current.Index = -1;
        current.RequestId = requestId;
        playback.Value = current;
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        if (!IsPlaying)
        {
            ReturnToIdle();
            if (IsServer) ClearPlayback();
            return;
        }

        double elapsed = Math.Max(0d, NetworkManager.ServerTime.Time - playback.Value.StartedAt);
        var animator = ActiveAnimator;
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (playbackAnimator != animator || appliedSequence != playback.Value.Sequence)
        {
            ReturnToIdle();
            playbackAnimator = animator;
            appliedSequence = playback.Value.Sequence;
            returningToIdle = false;
            previousAnimatorSpeed = animator.speed;
            if (catalog == null || !catalog.TryGet(playback.Value.Index, animator, out var entry))
            {
                ReturnToIdle();
                return;
            }
            animator.ResetTrigger(StartRun);
            animator.ResetTrigger(StopRun);
            animator.ResetTrigger(ResetAnim);
            animator.SetBool(UseAltIdle, false);
            animator.speed = entry.Speed;
            if (playback.Value.Loop || elapsed < playback.Value.Duration)
                animator.CrossFadeInFixedTime(entry.StateHash, blendDuration * entry.Speed, entry.Layer,
                    (float)(playback.Value.Loop ? elapsed % playback.Value.Duration : elapsed) * entry.Clip.length / entry.Duration);
        }
        if (!playback.Value.Loop && elapsed >= playback.Value.Duration) ReturnToIdle();
    }

    private void ReturnToIdle()
    {
        if (playbackAnimator == null) return;
        if (!returningToIdle) playbackAnimator.speed = previousAnimatorSpeed;
        if (!playbackAnimator.isActiveAndEnabled) return;
        if (returningToIdle)
        {
            if (!playbackAnimator.IsInTransition(0)) playbackAnimator.ResetTrigger(ResetAnim);
            return;
        }
        playbackAnimator.ResetTrigger(StartRun);
        playbackAnimator.ResetTrigger(StopRun);
        playbackAnimator.SetBool(UseAltIdle, false);
        playbackAnimator.SetTrigger(ResetAnim);
        returningToIdle = true;
    }
}
