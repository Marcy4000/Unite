using Unity.Netcode;
using UnityEngine;

public class AnimationManager : NetworkBehaviour
{
    private Animator animator;
    private bool timedPlayback;
    private int timedState;
    private float previousPlaybackSpeed;

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
        if (animator == null)
            return;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(parameter.name);
            }
        }

        // Racing is a persistent presentation mode, not a one-shot owner RPC.
        // Every peer must initialize it when its own model finishes loading.
        if (animatorChanged && TryGetComponent(out Pokemon pokemon)
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
        if (IsAnimatorNull())
            return;
        animator.SetBool(hash, value);
    }

    public void SetTrigger(string name)
    {
        SetTriggerRpc(Animator.StringToHash(name));
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SetTriggerRpc(int hash)
    {
        if (IsAnimatorNull())
            return;

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
        if (IsAnimatorNull())
            return;
        animator.SetFloat(hash, value);
    }

    public void SetInt(string name, int value)
    {
        SetIntRpc(Animator.StringToHash(name), value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetIntRpc(int hash, int value)
    {
        if (IsAnimatorNull())
            return;
        animator.SetInteger(hash, value);
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
        if (!animator.HasState(0, hash)) return;
        RestorePlaybackSpeed();
        if (float.IsFinite(duration) && duration > 0 && animator.runtimeAnimatorController != null)
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == state && clip.length > 0)
                {
                    previousPlaybackSpeed = animator.speed; timedState = hash; timedPlayback = true;
                    animator.speed = clip.length / Mathf.Max(0.01f, duration); break;
                }
        StartAnimation(hash, blendDuration);
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
        if (!timedPlayback) return;
        if (animator == null) { RestorePlaybackSpeed(); return; }
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != timedState) RestorePlaybackSpeed();
    }

    private void RestorePlaybackSpeed()
    {
        if (timedPlayback && animator != null) animator.speed = previousPlaybackSpeed;
        timedPlayback = false;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayAnimationRpc(int hash, float blendDuration = 0)
    {
        if (IsAnimatorNull())
            return;
        RestorePlaybackSpeed();
        // A supported Mirror Shadow adapter may reuse a character-independent
        // effect prefab. Never request that character's missing model clip.
        if (!animator.HasState(0, hash) && TryGetComponent(out Pokemon pokemon)
            && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.MarshadowPassive)
            hash = Animator.StringToHash("pm0883_ba21_tokusyu02");
        if (float.IsFinite(blendDuration) && blendDuration > 0) StartAnimation(hash, blendDuration);
        else animator.Play(hash);
    }
}
