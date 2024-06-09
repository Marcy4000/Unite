using System.Collections;
using System.Collections.Generic;
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
        SetBoolRpc(name, value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetBoolRpc(string name, bool value)
    {
        if (IsAnimatorNull())
            return;
        animator.SetBool(name, value);
    }

    public void SetTrigger(string name)
    {
        SetTriggerRpc(name);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SetTriggerRpc(string name)
    {
        if (IsAnimatorNull())
            return;

        animator.SetTrigger(name);
    }

    public void SetFloat(string name, float value)
    {
        SetFloatRpc(name, value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetFloatRpc(string name, float value)
    {
        if (IsAnimatorNull())
            return;
        animator.SetFloat(name, value);
    }

    public void SetInt(string name, int value)
    {
        SetIntRpc(name, value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetIntRpc(string name, int value)
    {
        if (IsAnimatorNull())
            return;
        animator.SetInteger(name, value);
    }

    public void PlayAnimation(string name)
    {
        PlayAnimationRpc(name);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayAnimationRpc(string name)
    {
        if (IsAnimatorNull())
            return;
        animator.Play(name);
    }
}
