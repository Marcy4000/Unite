using System;
using UnityEngine;

internal sealed class OpenWorldAnimationPoseBlend
{
    private struct Pose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public Pose(Transform bone)
        {
            Position = bone.localPosition;
            Rotation = bone.localRotation;
            Scale = bone.localScale;
        }
    }

    private Animator animator;
    private Transform[] bones;
    private Pose[] previousPose;
    private Pose[] sourcePose;
    private float duration;
    private float elapsed;

    public bool IsInitialized => animator != null;
    public bool IsBlending => elapsed < duration;

    public void Initialize(Animator target)
    {
        if (animator == target) return;
        Clear();
        animator = target;
        if (animator == null) return;
        bones = animator.GetComponentsInChildren<Transform>(true);
        previousPose = new Pose[bones.Length];
        sourcePose = new Pose[bones.Length];
        RememberPose();
    }

    public void Begin(float seconds)
    {
        duration = Mathf.Max(0f, seconds);
        elapsed = 0f;
        if (previousPose != null) Array.Copy(previousPose, sourcePose, previousPose.Length);
    }

    public void Apply(float deltaTime)
    {
        if (!IsInitialized || !IsBlending) return;
        elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0f, deltaTime));
        float weight = Mathf.SmoothStep(0f, 1f, elapsed / duration);
        for (int index = 0; index < bones.Length; index++)
        {
            var bone = bones[index];
            if (bone == null) continue;
            var source = sourcePose[index];
            bone.localPosition = Vector3.LerpUnclamped(source.Position, bone.localPosition, weight);
            bone.localRotation = Quaternion.SlerpUnclamped(source.Rotation, bone.localRotation, weight);
            bone.localScale = Vector3.LerpUnclamped(source.Scale, bone.localScale, weight);
        }
    }

    public void RememberPose()
    {
        if (bones == null) return;
        for (int index = 0; index < bones.Length; index++)
            if (bones[index] != null) previousPose[index] = new Pose(bones[index]);
    }

    public void Clear()
    {
        animator = null;
        bones = null;
        previousPose = null;
        sourcePose = null;
        duration = 0f;
        elapsed = 0f;
    }
}
