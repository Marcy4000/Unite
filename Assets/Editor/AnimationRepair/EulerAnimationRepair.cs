using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Repairs extracted generic clips whose Euler values were labelled with the wrong order.</summary>
public static class EulerAnimationRepair
{
    // This is the order in which rotations are APPLIED, not quaternion multiplication order.
    public enum SourceOrder { XYZ, XZY, YXZ, YZX, ZXY, ZYX }

    private sealed class EulerTrack
    {
        public string Prefix;
        public readonly AnimationCurve[] Axes = new AnimationCurve[3];
    }

    public static Quaternion ToQuaternion(Vector3 angles, SourceOrder order)
    {
        Quaternion x = Quaternion.AngleAxis(angles.x, Vector3.right);
        Quaternion y = Quaternion.AngleAxis(angles.y, Vector3.up);
        Quaternion z = Quaternion.AngleAxis(angles.z, Vector3.forward);
        switch (order)
        {
            case SourceOrder.XYZ: return z * y * x;
            case SourceOrder.XZY: return y * z * x;
            case SourceOrder.YXZ: return z * x * y;
            case SourceOrder.YZX: return x * z * y;
            case SourceOrder.ZXY: return y * x * z;
            case SourceOrder.ZYX: return x * y * z;
            default: throw new ArgumentOutOfRangeException(nameof(order));
        }
    }

    public static bool RequiresEulerConversion(AnimationClip clip)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        return AnimationUtility.GetCurveBindings(clip).Any(b => b.type == typeof(Transform) && IsEulerProperty(b.propertyName));
    }

    /// <summary>Returns a new, unsaved clip. Clips without Euler tracks are copied without rotation conversion.</summary>
    public static AnimationClip BuildCorrectedClip(AnimationClip source, SourceOrder order, float sampleRate = 0)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        float rate = sampleRate == 0 ? source.frameRate : sampleRate;
        if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0 || rate > 240)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be between 0 and 240 fps (0 uses the source rate).");

        var bindings = AnimationUtility.GetCurveBindings(source);
        var tracks = new Dictionary<string, EulerTrack>();
        var removals = new List<EditorCurveBinding>();
        var repairedBindings = new List<EditorCurveBinding>();
        var repairedCurves = new List<AnimationCurve>();
        float endTime = 0;
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            int repaired = RepairNaNTangents(curve);
            if (curve != null && curve.length > 0)
                endTime = Mathf.Max(endTime, curve.keys[curve.length - 1].time);
            if (binding.type != typeof(Transform) || !IsEulerProperty(binding.propertyName))
            {
                if (repaired > 0)
                {
                    repairedBindings.Add(binding);
                    repairedCurves.Add(curve);
                }
                continue;
            }

            string prefix = binding.propertyName.Substring(0, binding.propertyName.Length - 2);
            int axis = "xyz".IndexOf(binding.propertyName[binding.propertyName.Length - 1]);
            if (!tracks.TryGetValue(binding.path, out var track))
                tracks.Add(binding.path, track = new EulerTrack { Prefix = prefix });
            if (track.Prefix != prefix)
                throw new InvalidOperationException($"{source.name}: multiple Euler representations at '{binding.path}'. Resolve these before converting.");
            track.Axes[axis] = curve;
            removals.Add(binding);
        }
        if (tracks.Count > 0 && source.humanMotion)
            throw new InvalidOperationException($"{source.name}: humanoid clips are not supported. This tool repairs generic transform animation only.");
        foreach (var pair in tracks)
        {
            if (pair.Value.Axes.Any(c => c == null || c.length == 0))
                throw new InvalidOperationException($"{source.name}: incomplete XYZ Euler track at '{pair.Key}'. Missing axes cannot safely be guessed.");
        }
        foreach (var binding in bindings)
        {
            if (binding.type == typeof(Transform) && tracks.ContainsKey(binding.path) && binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                throw new InvalidOperationException($"{source.name}: both quaternion and Euler tracks at '{binding.path}'. Resolve these before converting.");
        }

        // Include the original data range, not clip.length (which can include a padded final frame).
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        endTime = Mathf.Max(endTime, settings.stopTime);
        if (tracks.Count > 0 && endTime * rate > 1000000)
            throw new InvalidOperationException($"{source.name}: too many samples requested.");

        var result = Object.Instantiate(source);
        result.name = source.name;
        try
        {
            var outputBindings = new List<EditorCurveBinding>(removals);
            var outputCurves = removals.Select(_ => (AnimationCurve)null).ToList();
            outputBindings.AddRange(repairedBindings);
            outputCurves.AddRange(repairedCurves);
            foreach (var pair in tracks)
            {
                var track = pair.Value;
                // Prefer exact original key times over nearby floating-point frame times. Preserve off-grid keys too.
                var times = new SortedSet<float>(track.Axes.SelectMany(c => c.keys).Select(k => k.time));
                for (int frame = 0; frame <= Mathf.CeilToInt(endTime * rate); frame++)
                {
                    float time = Mathf.Min(frame / rate, endTime);
                    if (times.GetViewBetween(time - 0.00001f, time + 0.00001f).Count == 0) times.Add(time);
                }
                var keys = new List<Keyframe>[4];
                for (int axis = 0; axis < 4; axis++) keys[axis] = new List<Keyframe>(times.Count);
                Quaternion previous = Quaternion.identity;
                bool first = true;
                foreach (float time in times)
                {
                    Vector3 euler = new Vector3(track.Axes[0].Evaluate(time), track.Axes[1].Evaluate(time), track.Axes[2].Evaluate(time));
                    Quaternion q = ToQuaternion(euler, order).normalized;
                    if (!IsFinite(q.x) || !IsFinite(q.y) || !IsFinite(q.z) || !IsFinite(q.w))
                        throw new InvalidOperationException($"{source.name}: non-finite rotation at '{pair.Key}', time {time}.");
                    // q and -q are the same orientation; use the same hemisphere for interpolation.
                    if (!first && Quaternion.Dot(previous, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    for (int axis = 0; axis < 4; axis++) keys[axis].Add(new Keyframe(time, q[axis]));
                    previous = q;
                    first = false;
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    var curve = new AnimationCurve(keys[axis].ToArray());
                    // Linear components avoid cubic overshoot; Unity normalizes the sampled rotation.
                    for (int key = 0; key < curve.length; key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                    }
                    outputBindings.Add(EditorCurveBinding.FloatCurve(pair.Key, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]));
                    outputCurves.Add(curve);
                }
            }
            if (outputBindings.Count > 0)
                AnimationUtility.SetEditorCurves(result, outputBindings.ToArray(), outputCurves.ToArray());
            if (tracks.Count > 0)
            {
                result.frameRate = rate;
                result.EnsureQuaternionContinuity();
                AnimationUtility.SetAnimationEvents(result, AnimationUtility.GetAnimationEvents(source));
            }
            // SetAnimationEvents resets the clip's trimmed range in Unity 6; restore settings LAST.
            AnimationUtility.SetAnimationClipSettings(result, settings);
            return result;
        }
        catch
        {
            Object.DestroyImmediate(result);
            throw;
        }
    }

    /// <summary>Repairs NaN tangents in-place without changing key values or legitimate infinite step tangents.</summary>
    public static int RepairInvalidTangents(AnimationClip clip)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        int count = 0;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            int repaired = RepairNaNTangents(curve);
            if (repaired == 0) continue;
            count += repaired;
            bindings.Add(binding);
            curves.Add(curve);
        }
        if (count > 0)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
        return count;
    }

    private static int RepairNaNTangents(AnimationCurve curve)
    {
        if (curve == null) return 0;
        var keys = curve.keys;
        int count = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            // Infinity is valid for stepped interpolation. NaN is never a valid tangent.
            if (float.IsNaN(key.inTangent))
            {
                key.inTangent = i == 0 ? 0 : Secant(keys[i - 1], key);
                count++;
            }
            if (float.IsNaN(key.outTangent))
            {
                key.outTangent = i == keys.Length - 1 ? 0 : Secant(key, keys[i + 1]);
                count++;
            }
            keys[i] = key;
        }
        if (count > 0) curve.keys = keys;
        return count;
    }

    private static float Secant(Keyframe left, Keyframe right)
    {
        float slope = (right.value - left.value) / (right.time - left.time);
        if (!IsFinite(slope)) throw new InvalidOperationException("Cannot repair tangent: neighbouring key times or values are invalid.");
        return slope;
    }

    public static AnimationClip CreateCorrectedCopy(AnimationClip source, string outputFolder, SourceOrder order, float sampleRate = 0)
    {
        ValidateFolder(outputFolder);
        bool convertEuler = RequiresEulerConversion(source);
        var corrected = BuildCorrectedClip(source, order, sampleRate);
        try
        {
            EnsureFolder(outputFolder);
            string name = source.name;
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            string suffix = convertEuler ? order + "Fixed" : "Copy";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{name}_{suffix}.anim");
            AssetDatabase.CreateAsset(corrected, path);
            if (!convertEuler)
            {
                string sourcePath = AssetDatabase.GetAssetPath(source);
                Debug.Log($"Animation repair: skipped Euler conversion for '{source.name}' ({(string.IsNullOrEmpty(sourcePath) ? "unsaved clip" : sourcePath)}): no Euler tracks. Copied to '{path}'; existing quaternion data preserved and NaN tangents checked.", source);
            }
            return corrected;
        }
        catch
        {
            if (!AssetDatabase.Contains(corrected)) Object.DestroyImmediate(corrected);
            throw;
        }
    }

    /// <summary>Updates only the specified controller and its referenced blend trees; supports Undo.</summary>
    public static int ReplaceControllerClips(AnimatorController controller, Dictionary<AnimationClip, AnimationClip> replacements)
    {
        if (controller == null) throw new ArgumentNullException(nameof(controller));
        int count = 0;
        var visited = new HashSet<BlendTree>();
        foreach (var layer in controller.layers) ReplaceInStateMachine(layer.stateMachine, replacements, visited, ref count);
        if (count > 0) EditorUtility.SetDirty(controller);
        return count;
    }

    private static void ReplaceInStateMachine(AnimatorStateMachine machine, Dictionary<AnimationClip, AnimationClip> replacements, HashSet<BlendTree> visited, ref int count)
    {
        foreach (var child in machine.states)
        {
            var state = child.state;
            if (state.motion is AnimationClip clip && replacements.TryGetValue(clip, out var replacement))
            {
                Undo.RecordObject(state, "Replace repaired animation");
                state.motion = replacement;
                EditorUtility.SetDirty(state);
                count++;
            }
            else if (state.motion is BlendTree tree) ReplaceInBlendTree(tree, replacements, visited, ref count);
        }
        foreach (var child in machine.stateMachines) ReplaceInStateMachine(child.stateMachine, replacements, visited, ref count);
    }

    private static void ReplaceInBlendTree(BlendTree tree, Dictionary<AnimationClip, AnimationClip> replacements, HashSet<BlendTree> visited, ref int count)
    {
        if (!visited.Add(tree)) return;
        var children = tree.children;
        bool changed = false;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion is AnimationClip clip && replacements.TryGetValue(clip, out var replacement))
            {
                children[i].motion = replacement;
                changed = true;
                count++;
            }
            else if (children[i].motion is BlendTree nested) ReplaceInBlendTree(nested, replacements, visited, ref count);
        }
        if (!changed) return;
        Undo.RecordObject(tree, "Replace repaired blend tree animations");
        tree.children = children;
        EditorUtility.SetDirty(tree);
    }

    public static void ValidateFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal)
            || folder.Contains('\\') || folder.Split('/').Any(p => p == ".." || p == "." || p.Length == 0))
            throw new ArgumentException("Choose an output folder below Assets, using forward slashes.", nameof(folder));
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = folder.Substring(0, folder.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
    }

    private static bool IsEulerProperty(string property)
    {
        return new[] { "localEulerAnglesRaw", "localEulerAnglesBaked", "localEulerAngles" }
            .Any(prefix => property.Length == prefix.Length + 2 && property.StartsWith(prefix + ".", StringComparison.Ordinal)
                && "xyz".IndexOf(property[property.Length - 1]) >= 0);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
