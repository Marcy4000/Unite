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
        this.animator = animator;
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(parameter.name);
            }
        }
    }

    public void SetBool(string name, bool value)
    {
        if (IsAnimatorNull())
            return;
        SetBoolRpc(Animator.StringToHash(name), value);
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
        animator.Play(hash);
    }
}
