using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OpenWorldEmotes", menuName = "Lobby/Open World Emote Catalog")]
public sealed class OpenWorldEmoteCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private string animationName;
        [SerializeField] private string displayName;
        [SerializeField, Min(0.01f)] private float speed = 1f;
        [SerializeField, HideInInspector] private AnimationClip clip;
        [SerializeField, HideInInspector] private string statePath;
        [SerializeField, HideInInspector] private int layer;
        [SerializeField, HideInInspector] private float stateSpeed = 1f;
        [SerializeField, HideInInspector] private string resolvedAnimationName;

        public string AnimationName => animationName;
        public string DisplayName => displayName;
        public float Speed => speed;
        public AnimationClip Clip => clip;
        public int StateHash => Animator.StringToHash(statePath ?? string.Empty);
        public string StatePath => statePath;
        public int Layer => layer;
        public float Duration => clip != null && stateSpeed > 0f && speed > 0f ? clip.length / stateSpeed / speed : 0f;

        internal bool IsPlayable => clip != null && !clip.empty && clip.length > 0f &&
            !string.IsNullOrEmpty(animationName) && animationName == resolvedAnimationName &&
            !string.IsNullOrEmpty(statePath) && layer >= 0 && Duration > 0f && float.IsFinite(Duration) && float.IsFinite(speed);

#if UNITY_EDITOR
        internal bool Resolve(AnimationClip resolvedClip, string path, int resolvedLayer, float authoredSpeed)
        {
            if (clip == resolvedClip && statePath == path && layer == resolvedLayer && stateSpeed == authoredSpeed &&
                resolvedAnimationName == animationName) return false;
            clip = resolvedClip;
            statePath = path;
            layer = resolvedLayer;
            stateSpeed = authoredSpeed;
            resolvedAnimationName = animationName;
            return true;
        }
#endif
    }

    [SerializeField] private Entry[] maleAnimations = Array.Empty<Entry>();
    [SerializeField] private Entry[] femaleAnimations = Array.Empty<Entry>();
    [SerializeField, HideInInspector] private RuntimeAnimatorController maleController;
    [SerializeField, HideInInspector] private RuntimeAnimatorController femaleController;
    private Entry[] combinedEntries;

    public IReadOnlyList<Entry> MaleAnimations => maleAnimations ?? Array.Empty<Entry>();
    public IReadOnlyList<Entry> FemaleAnimations => femaleAnimations ?? Array.Empty<Entry>();
    public IReadOnlyList<Entry> Entries
    {
        get
        {
            if (combinedEntries == null)
            {
                combinedEntries = new Entry[MaleAnimations.Count + FemaleAnimations.Count];
                for (int index = 0; index < MaleAnimations.Count; index++) combinedEntries[index] = MaleAnimations[index];
                for (int index = 0; index < FemaleAnimations.Count; index++) combinedEntries[MaleAnimations.Count + index] = FemaleAnimations[index];
            }
            return combinedEntries;
        }
    }

    private void OnEnable() => combinedEntries = null;

    public bool IsValidIndex(int index) => index >= 0 && index < Entries.Count;

    public bool TryGet(int index, bool isMale, out Entry entry)
    {
        entry = null;
        if (!IsValidIndex(index)) return false;
        var candidate = Entries[index];
        var controller = isMale ? maleController : femaleController;
        if (controller == null || candidate == null || !candidate.IsPlayable || (index < MaleAnimations.Count) != isMale) return false;
        entry = candidate;
        return true;
    }

    public bool TryGet(int index, Animator animator, out Entry entry)
    {
        entry = null;
        if (!IsValidIndex(index) || animator == null || animator.runtimeAnimatorController == null) return false;
        var candidate = Entries[index];
        var controller = index < MaleAnimations.Count ? maleController : femaleController;
        if (candidate == null || !candidate.IsPlayable || animator.runtimeAnimatorController != controller ||
            candidate.Layer >= animator.layerCount || !animator.HasState(candidate.Layer, candidate.StateHash)) return false;
        entry = candidate;
        return true;
    }

#if UNITY_EDITOR
    private struct ResolvedState
    {
        public AnimationClip Clip;
        public string Path;
        public int Layer;
        public float Speed;
    }

    private void OnValidate()
    {
        combinedEntries = null;
        UnityEditor.EditorApplication.update -= ResolveAfterValidation;
        UnityEditor.EditorApplication.update += ResolveAfterValidation;
    }

    private void ResolveAfterValidation()
    {
        UnityEditor.EditorApplication.update -= ResolveAfterValidation;
        if (this == null) return;
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            UnityEditor.EditorApplication.update += ResolveAfterValidation;
            return;
        }
        ResolveAnimations();
        if (UnityEditor.EditorUtility.IsPersistent(this)) UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
    }

    [ContextMenu("Refresh Animation Bindings")]
    public void ResolveAnimations()
    {
        combinedEntries = null;
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrainerModels/TrainerModel.prefab");
        var model = prefab != null ? prefab.GetComponent<TrainerModel>() : null;
        var serialized = model != null ? new UnityEditor.SerializedObject(model) : null;
        var male = serialized?.FindProperty("maleAnimator").objectReferenceValue as Animator;
        var female = serialized?.FindProperty("femaleAnimator").objectReferenceValue as Animator;
        var nextMale = male != null ? male.runtimeAnimatorController : null;
        var nextFemale = female != null ? female.runtimeAnimatorController : null;
        bool changed = maleController != nextMale || femaleController != nextFemale;
        maleController = nextMale;
        femaleController = nextFemale;
        changed |= ResolveList(MaleAnimations, maleController as UnityEditor.Animations.AnimatorController);
        changed |= ResolveList(FemaleAnimations, femaleController as UnityEditor.Animations.AnimatorController);
        if (changed && UnityEditor.EditorUtility.IsPersistent(this)) UnityEditor.EditorUtility.SetDirty(this);
    }

    private static bool ResolveList(IReadOnlyList<Entry> animations, UnityEditor.Animations.AnimatorController controller)
    {
        var states = new Dictionary<string, ResolvedState>(StringComparer.Ordinal);
        var clips = new Dictionary<string, ResolvedState>(StringComparer.Ordinal);
        if (controller != null)
            for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
                CollectStates(controller.layers[layerIndex].stateMachine, controller.layers[layerIndex].name, layerIndex, states, clips);
        bool changed = false;
        foreach (var entry in animations)
        {
            if (entry == null) continue;
            var name = entry.AnimationName ?? string.Empty;
            if (states.TryGetValue(name, out var state) || clips.TryGetValue(name, out state))
                changed |= entry.Resolve(state.Clip, state.Path, state.Layer, state.Speed);
            else changed |= entry.Resolve(null, null, 0, 1f);
        }
        return changed;
    }

    private static void CollectStates(UnityEditor.Animations.AnimatorStateMachine machine, string path, int layerIndex,
        Dictionary<string, ResolvedState> states, Dictionary<string, ResolvedState> clips)
    {
        foreach (var child in machine.states)
        {
            var state = child.state;
            if (!(state.motion is AnimationClip clip)) continue;
            var resolved = new ResolvedState { Clip = clip, Path = path + "." + state.name, Layer = layerIndex, Speed = state.speed };
            states.TryAdd(state.name, resolved);
            states.TryAdd(resolved.Path, resolved);
            clips.TryAdd(clip.name, resolved);
        }
        foreach (var child in machine.stateMachines)
            CollectStates(child.stateMachine, path + "." + child.stateMachine.name, layerIndex, states, clips);
    }
#endif
}
