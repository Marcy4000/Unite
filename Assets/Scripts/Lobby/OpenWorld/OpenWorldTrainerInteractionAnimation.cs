using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-25)]
public sealed class OpenWorldTrainerInteractionAnimation : MonoBehaviour
{
    private Animator animator;
    private AnimationClip clip;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private float elapsed;
    private float blendDuration;
    private bool blendingOut;
    private readonly OpenWorldAnimationPoseBlend poseBlend = new OpenWorldAnimationPoseBlend();

    public bool IsPlaying => graph.IsValid() || blendingOut;
    public bool IsBlendingOut => blendingOut;
    public AnimationClip CurrentClip => IsPlaying ? clip : null;

    public bool Play(Animator target, AnimationClip animation, float transitionDuration = 0.2f)
    {
        if (target == null || !target.isActiveAndEnabled || animation == null || animation.legacy || animation.length <= 0f)
            return false;
        if (animator != target) StopPlayback();
        else if (graph.IsValid()) graph.Destroy();
        animator = target;
        poseBlend.Initialize(animator);
        blendDuration = Mathf.Max(0f, transitionDuration);
        poseBlend.Begin(blendDuration);
        blendingOut = false;
        ResetController();
        clip = animation;
        elapsed = 0f;
        graph = PlayableGraph.Create($"Trainer NPC interaction {name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(graph, "Trainer Interaction", animator);
        output.SetSourcePlayable(playable);
        graph.Play();
        return true;
    }

    private void LateUpdate()
    {
        if (!IsPlaying) return;
        if (animator == null || !animator.isActiveAndEnabled)
        {
            StopPlayback();
            return;
        }
        if (!blendingOut)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= clip.length) BeginReturn();
            else
            {
                playable.SetTime(elapsed);
                graph.Evaluate(0f);
            }
        }
        poseBlend.Apply(Time.deltaTime);
        poseBlend.RememberPose();
        if (blendingOut && !poseBlend.IsBlending) StopPlayback();
    }

    public void CancelForMovement()
    {
        if (IsPlaying && !blendingOut) BeginReturn();
    }

    private void BeginReturn()
    {
        if (blendDuration <= 0f)
        {
            StopPlayback();
            return;
        }
        if (graph.IsValid()) graph.Destroy();
        blendingOut = true;
        poseBlend.Begin(blendDuration);
    }

    private void ResetController()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    if (parameter.name == "startRun" || parameter.name == "stopRun") animator.ResetTrigger(parameter.nameHash);
                    else if (parameter.name == "ResetAnim") animator.SetTrigger(parameter.nameHash);
                }
                else if (parameter.name == "useAltIdle" && parameter.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(parameter.nameHash, false);
            }
    }

    public void StopPlayback()
    {
        if (graph.IsValid()) graph.Destroy();
        blendingOut = false;
        poseBlend.Clear();
        animator = null;
        clip = null;
    }

    private void OnDisable() => StopPlayback();
    private void OnDestroy() => StopPlayback();
}
