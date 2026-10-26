using Unity.Netcode;
using UnityEngine;

public class AnimationManager : NetworkBehaviour
{
    private Animator animator;

    public Animator Animator => animator;

    public bool IsAnimatorNull()
    {
        return animator == null;
    }

    public void AssignAnimator(Animator animator)
    {
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

    public void PlayAnimation(string name)
    {
        PlayAnimationRpc(Animator.StringToHash(name));
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayAnimationRpc(int hash)
    {
        if (IsAnimatorNull())
            return;
        // A supported Mirror Shadow adapter may reuse a character-independent
        // effect prefab. Never request that character's missing model clip.
        if (!animator.HasState(0, hash) && TryGetComponent(out Pokemon pokemon)
            && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.MarshadowPassive)
            hash = Animator.StringToHash("pm0883_ba21_tokusyu02");
        animator.Play(hash);
    }
}
