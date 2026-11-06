using System;
using Unity.Netcode;
using UnityEngine;

public sealed partial class OpenWorldTrainerAvatar
{
    private struct IdleVariation : INetworkSerializable, IEquatable<IdleVariation>
    {
        public uint Sequence;
        public bool Second;
        public double StartedAt;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Second);
            serializer.SerializeValue(ref StartedAt);
        }

        public bool Equals(IdleVariation other) => Sequence == other.Sequence &&
            Second == other.Second && StartedAt.Equals(other.StartedAt);
    }

    // The owner chooses once; observers use the same variant and network clock.
    private readonly NetworkVariable<IdleVariation> idleVariation = new NetworkVariable<IdleVariation>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private double nextIdleVariationAt;
    private uint appliedIdleVariation;

    private void UpdateIdleVariation()
    {
        var animator = AnimationAnimator;
        bool available = animator != null && animator.isActiveAndEnabled && !isRunning && !IsSeated &&
            (emotes == null || !emotes.IsPlaying) &&
            (interactionAnimation == null || !interactionAnimation.IsPlaying);
        double now = NetworkManager.ServerTime.Time;
        if (IsOwner)
        {
            bool idle = available && !animator.IsInTransition(0) &&
                (animator.GetCurrentAnimatorStateInfo(0).IsName("ani_idle_lob_female") ||
                 animator.GetCurrentAnimatorStateInfo(0).IsName("ani_idle_lob_male") ||
                 animator.GetCurrentAnimatorStateInfo(0).IsName("ani_idle_40350_lob_male"));
            if (!idle)
                nextIdleVariationAt = 0d;
            else if (nextIdleVariationAt == 0d)
                nextIdleVariationAt = now + UnityEngine.Random.Range(8f, 20f);
            else if (now >= nextIdleVariationAt)
            {
                uint sequence = idleVariation.Value.Sequence + 1;
                if (sequence == 0) sequence = 1;
                idleVariation.Value = new IdleVariation
                {
                    Sequence = sequence,
                    Second = UnityEngine.Random.Range(0, 2) == 1,
                    StartedAt = now
                };
                nextIdleVariationAt = 0d;
            }
        }

        var variation = idleVariation.Value;
        if (variation.Sequence == 0 || variation.Sequence == appliedIdleVariation || animator == null) return;
        appliedIdleVariation = variation.Sequence;
        if (!available) return;
        string stateName = $"ani_specialidle{(variation.Second ? "02" : "")}_lob_{(trainerModel.IsMale ? "male" : "female")}";
        var clip = Array.Find(animator.runtimeAnimatorController.animationClips, candidate => candidate.name == stateName);
        double elapsed = Math.Max(0d, now - variation.StartedAt);
        // Late joiners catch up to an active variation, but never replay an old one.
        if (clip == null || elapsed >= clip.length || !animator.HasState(0, Animator.StringToHash(stateName))) return;
        animator.ResetTrigger(StartRun);
        animator.ResetTrigger(StopRun);
        animator.ResetTrigger(ResetAnim);
        animator.SetBool(UseAltIdle, false);
        animator.CrossFadeInFixedTime(Animator.StringToHash(stateName), 0.15f, 0, (float)elapsed);
        // The controller's exit-time transition returns to idle naturally.
    }
}
