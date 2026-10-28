using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class AnimationManager : NetworkBehaviour
{
    private Animator animator;
    private bool timedPlayback;
    private int timedState;
    private float previousPlaybackSpeed;
    private readonly Dictionary<int, bool> bools = new();
    private readonly Dictionary<int, float> floats = new();
    private readonly Dictionary<int, int> ints = new();
    private bool presentationWasActive;
    private bool animatorNeedsInitialization;
    private bool hasPendingState;
    private int pendingState;
    private float pendingStateAt, pendingDuration;
    private bool hasPendingTrigger;
    private int pendingTrigger;
    private bool CanAnimate => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;

    public Animator Animator => animator;

    public bool IsAnimatorNull()
    {
        return animator == null;
    }

    public void AssignAnimator(Animator animator)
    {
        RestorePlaybackSpeed();
        bool animatorChanged = this.animator != animator;
        this.animator = animator;
        presentationWasActive = false;
        animatorNeedsInitialization = animatorChanged;
        if (animator == null)
            return;

        animator.keepAnimatorStateOnDisable = true;
        InitializePresentation();
    }

    private void InitializePresentation()
    {
        if (!CanAnimate || !animatorNeedsInitialization) return;
        animatorNeedsInitialization = false;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(parameter.name);
            }
        }

        // Racing is a persistent presentation mode, not a one-shot owner RPC.
        // Every peer must initialize it when its own model finishes loading.
        if (TryGetComponent(out Pokemon pokemon)
            && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.PsyduckRacePassive)
        {
            PsyduckRacePassive.InitializeAnimator(animator);
        }
    }

    public void SetBool(string name, bool value)
    {
        if (IsAnimatorNull())
            return;
        SetBoolRpc(Animator.StringToHash(name), value);
    }

    public void SetBool(int hash, bool value)
    {
        if (IsAnimatorNull())
            return;
        SetBoolRpc(hash, value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetBoolRpc(int hash, bool value)
    {
        bools[hash] = value;
        if (CanAnimate) animator.SetBool(hash, value);
        else presentationWasActive = false;
    }

    public void SetTrigger(string name)
    {
        SetTriggerRpc(Animator.StringToHash(name));
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SetTriggerRpc(int hash)
    {
        SetLocalTrigger(hash);
    }

    private void SetLocalTrigger(int hash)
    {
        hasPendingState = false;
        RestorePlaybackSpeed();
        pendingTrigger = hash;
        hasPendingTrigger = true;
        if (!CanAnimate)
        {
            presentationWasActive = false;
            return;
        }

        ApplyPendingTrigger();
    }

    private void ApplyPendingTrigger()
    {
        if (!hasPendingTrigger) return;
        int hash = pendingTrigger;
        hasPendingTrigger = false;
        foreach (var parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
        animator.ResetTrigger(hash);
        animator.SetTrigger(hash);
    }

    public void SetFloat(string name, float value)
    {
        SetFloatRpc(Animator.StringToHash(name), value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetFloatRpc(int hash, float value)
    {
        floats[hash] = value;
        if (CanAnimate) animator.SetFloat(hash, value);
        else presentationWasActive = false;
    }

    public void SetInt(string name, int value)
    {
        SetIntRpc(Animator.StringToHash(name), value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetIntRpc(int hash, int value)
    {
        ints[hash] = value;
        if (CanAnimate) animator.SetInteger(hash, value);
        else presentationWasActive = false;
    }

    public void PlayAnimation(string name, float blendDuration = 0)
    {
        PlayAnimationRpc(Animator.StringToHash(name), blendDuration);
    }

    // A delayed recovery must not overwrite a newer move, stun or death pose.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void RecoverAnimationRPC(string expectedState, string recoveryState, float blendDuration = 0)
    {
        RecoverLocalAnimation(expectedState, recoveryState, blendDuration);
    }

    public bool RecoverLocalAnimation(string expectedState, string recoveryState, float blendDuration = 0)
    {
        if (animator == null) return false;
        int expected = Animator.StringToHash(expectedState), recovery = Animator.StringToHash(recoveryState);
        if (!CanAnimate)
        {
            if (!hasPendingState || pendingState != expected) return false;
            QueueState(recovery, 0);
            return true;
        }
        // During a blend the destination, not the outgoing pose, owns recovery.
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != expected || !animator.HasState(0, recovery)) return false;
        PlayLocalAnimation(recoveryState, 0, blendDuration);
        return true;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void PlayTimedAnimationRPC(string state, float duration, float blendDuration = 0)
    {
        PlayLocalAnimation(state, duration, blendDuration);
    }

    // Opt-in duration matching for authored casts. Legacy playback is unchanged.
    // Restore the old speed on recovery, replacement or a controller transition.
    public void PlayLocalAnimation(string state, float duration = 0, float blendDuration = 0)
    {
        if (animator == null) return;
        int hash = Animator.StringToHash(state);
        QueueState(hash, duration);
        RestorePlaybackSpeed();
        if (!CanAnimate) return;
        if (!animator.HasState(0, hash)) return;
        hasPendingState = false;
        SetPlaybackDuration(hash, duration);
        StartAnimation(hash, blendDuration);
    }

    private void SetPlaybackDuration(int hash, float duration)
    {
        RestorePlaybackSpeed();
        if (!float.IsFinite(duration) || duration <= 0) return;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (Animator.StringToHash(clip.name) == hash && clip.length > 0)
            {
                previousPlaybackSpeed = animator.speed;
                timedState = hash;
                timedPlayback = true;
                animator.speed = clip.length / Mathf.Max(0.01f, duration);
                break;
            }
    }

    private void StartAnimation(int hash, float blendDuration)
    {
        if (float.IsFinite(blendDuration) && blendDuration > 0)
            // Animator.speed also scales fixed-time transitions. Compensate so
            // the requested blend stays in real seconds for duration-matched clips.
            animator.CrossFadeInFixedTime(hash, blendDuration * Mathf.Abs(animator.speed), 0, 0);
        else animator.Play(hash, 0, 0);
    }

    private void LateUpdate()
    {
        if (!CanAnimate) { presentationWasActive = false; RestorePlaybackSpeed(); return; }
        InitializePresentation();
        bool resumedState = false;
        if (!presentationWasActive)
        {
            presentationWasActive = true;
            foreach (var pair in bools) animator.SetBool(pair.Key, pair.Value);
            foreach (var pair in floats) animator.SetFloat(pair.Key, pair.Value);
            foreach (var pair in ints) animator.SetInteger(pair.Key, pair.Value);
            if (hasPendingState && animator.HasState(0, pendingState))
            {
                // Resume elapsed presentation rather than replaying an old cast.
                float length = pendingDuration;
                if (length <= 0)
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                        if (Animator.StringToHash(clip.name) == pendingState) { length = clip.length; break; }
                SetPlaybackDuration(pendingState, pendingDuration);
                animator.Play(pendingState, 0, length > 0 ? (Time.time - pendingStateAt) / length : 0);
                hasPendingState = false;
                resumedState = true;
            }
            ApplyPendingTrigger();
        }
        if (!timedPlayback || resumedState) return;
        if (animator == null) { RestorePlaybackSpeed(); return; }
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != timedState) RestorePlaybackSpeed();
    }

    private void RestorePlaybackSpeed()
    {
        if (timedPlayback && animator != null) animator.speed = previousPlaybackSpeed;
        timedPlayback = false;
    }

    private void QueueState(int hash, float duration)
    {
        if (!CanAnimate) presentationWasActive = false;
        hasPendingTrigger = false;
        hasPendingState = true;
        pendingState = hash;
        pendingStateAt = Time.time;
        pendingDuration = duration;
    }

    public bool TryGetBool(int hash, out bool value) => bools.TryGetValue(hash, out value);

    private void OnDisable()
    {
        presentationWasActive = false;
        RestorePlaybackSpeed();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayAnimationRpc(int hash, float blendDuration = 0)
    {
        if (IsAnimatorNull())
            return;
        QueueState(hash, 0);
        RestorePlaybackSpeed();
        if (!CanAnimate) return;
        // A supported Mirror Shadow adapter may reuse a character-independent
        // effect prefab. Never request that character's missing model clip.
        if (!animator.HasState(0, hash) && TryGetComponent(out Pokemon pokemon)
            && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.MarshadowPassive)
            hash = Animator.StringToHash("pm0883_ba21_tokusyu02");
        if (!animator.HasState(0, hash)) return;
        hasPendingState = false;
        if (float.IsFinite(blendDuration) && blendDuration > 0) StartAnimation(hash, blendDuration);
        else animator.Play(hash);
    }
}
