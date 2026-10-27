using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class EulerAnimationRepairTests
{
    private readonly List<Object> temporary = new List<Object>();
    private readonly List<string> temporaryFolders = new List<string>();

    [TearDown]
    public void Cleanup()
    {
        foreach (var item in temporary) if (item != null) Object.DestroyImmediate(item);
        temporary.Clear();
        foreach (string folder in temporaryFolders) AssetDatabase.DeleteAsset(folder);
        temporaryFolders.Clear();
    }

    private AnimationClip Source()
    {
        var clip = new AnimationClip { name = "EulerSource", frameRate = 30 };
        temporary.Add(clip);
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        float[] start = { -45, 90.00131f, 44.99759f };
        float[] end = { 61.334793f, 88.70972f, 148.49284f };
        for (int axis = 0; axis < 3; axis++)
        {
            bindings.Add(EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "localEulerAnglesRaw." + "xyz"[axis]));
            curves.Add(AnimationCurve.Linear(0, start[axis], 0.1f, end[axis]));
        }
        bindings.Add(EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "m_LocalPosition.x"));
        curves.Add(AnimationCurve.Linear(0, 0.25f, 0.1f, 0.5f));
        AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.startTime = 0;
        settings.stopTime = 0.1f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = 0.05f, functionName = "Footstep", intParameter = 7 } });
        return clip;
    }

    private AnimationClip Convert(AnimationClip clip)
    {
        var result = EulerAnimationRepair.BuildCorrectedClip(clip, EulerAnimationRepair.SourceOrder.XYZ);
        if (result != null) temporary.Add(result);
        return result;
    }

    private static Quaternion ReadRotation(AnimationClip clip, string path, float time)
    {
        var values = new float[4];
        for (int i = 0; i < 4; i++)
            values[i] = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[i])).Evaluate(time);
        return new Quaternion(values[0], values[1], values[2], values[3]).normalized;
    }

    [Test]
    public void XYZCompositionFixesTheDiagnosedRootAndZXYMatchesUnity()
    {
        var angles = new Vector3(-45, 90.00131f, 44.99759f);
        var xyz = EulerAnimationRepair.ToQuaternion(angles, EulerAnimationRepair.SourceOrder.XYZ);
        Assert.That(Quaternion.Angle(xyz, new Quaternion(-0.5f, 0.5f, 0.5f, 0.5f)), Is.LessThan(0.1f));
        Assert.That(Quaternion.Angle(xyz, Quaternion.Euler(angles)), Is.GreaterThan(60));
        Assert.That(Quaternion.Angle(EulerAnimationRepair.ToQuaternion(angles, EulerAnimationRepair.SourceOrder.ZXY), Quaternion.Euler(angles)), Is.LessThan(0.1f));
    }

    [Test]
    public void ConversionPreservesNonRotationDataSettingsEventsAndSource()
    {
        var source = Source();
        var originalBindings = AnimationUtility.GetCurveBindings(source);
        var corrected = Convert(source);
        Assert.That(corrected, Is.Not.Null);
        Assert.That(AnimationUtility.GetCurveBindings(corrected).Any(b => b.propertyName.StartsWith("localEulerAngles")), Is.False);
        Assert.That(AnimationUtility.GetCurveBindings(source).Length, Is.EqualTo(originalBindings.Length));
        Assert.That(AnimationUtility.GetCurveBindings(source).Any(b => b.propertyName == "localEulerAnglesRaw.x"), Is.True);
        var position = EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "m_LocalPosition.x");
        Assert.That(AnimationUtility.GetEditorCurve(corrected, position).keys, Is.EqualTo(AnimationUtility.GetEditorCurve(source, position).keys));
        Assert.That(corrected.length, Is.EqualTo(source.length).Within(0.00001f));
        Assert.That(corrected.frameRate, Is.EqualTo(source.frameRate));
        var settings = AnimationUtility.GetAnimationClipSettings(corrected);
        Assert.That(settings.loopTime, Is.True);
        Assert.That(settings.stopTime, Is.EqualTo(0.1f).Within(0.00001f));
        var animationEvent = AnimationUtility.GetAnimationEvents(corrected).Single();
        Assert.That(animationEvent.functionName, Is.EqualTo("Footstep"));
        Assert.That(animationEvent.intParameter, Is.EqualTo(7));
        Assert.That(animationEvent.time, Is.EqualTo(0.05f).Within(0.00001f));
    }

    [Test]
    public void TrimmedPlaybackRangeSurvivesEventCopying()
    {
        var source = Source();
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.stopTime = 0.05f; // Shorter than the stored curve range, as in extracted Vaporeon clips.
        AnimationUtility.SetAnimationClipSettings(source, settings);
        var corrected = Convert(source);
        Assert.That(AnimationUtility.GetAnimationClipSettings(corrected).stopTime, Is.EqualTo(0.05f).Within(0.00001f));
        Assert.That(corrected.length, Is.EqualTo(source.length).Within(0.00001f));
    }

    [Test]
    public void SourceSamplesBecomeContinuousNormalizedQuaternionKeysAndCannotBeDoubleCorrected()
    {
        var source = Source();
        var corrected = Convert(source);
        Quaternion previous = Quaternion.identity;
        for (int frame = 0; frame <= 3; frame++)
        {
            float time = frame / 30f;
            var angles = new Vector3();
            for (int axis = 0; axis < 3; axis++)
                angles[axis] = AnimationUtility.GetEditorCurve(source, EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "localEulerAnglesRaw." + "xyz"[axis])).Evaluate(time);
            var actual = ReadRotation(corrected, "Bip001", time);
            Assert.That(Quaternion.Angle(actual, EulerAnimationRepair.ToQuaternion(angles, EulerAnimationRepair.SourceOrder.XYZ)), Is.LessThan(0.1f));
            if (frame > 0) Assert.That(Quaternion.Dot(previous, actual), Is.GreaterThanOrEqualTo(0));
            previous = actual;
        }
        Assert.That(EulerAnimationRepair.RequiresEulerConversion(corrected), Is.False);
        var copied = Convert(corrected);
        Assert.That(copied, Is.Not.Null);
        Assert.That(copied, Is.Not.SameAs(corrected));
        foreach (var binding in AnimationUtility.GetCurveBindings(corrected))
            Assert.That(AnimationUtility.GetEditorCurve(copied, binding).keys, Is.EqualTo(AnimationUtility.GetEditorCurve(corrected, binding).keys));
    }

    [Test]
    public void QuaternionCopiesKeepFrameRateSettingsAndEventsDespiteDifferentBakeOptions()
    {
        var source = Convert(Source());
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.stopTime = 0.05f;
        AnimationUtility.SetAnimationClipSettings(source, settings);
        var copied = EulerAnimationRepair.BuildCorrectedClip(source, EulerAnimationRepair.SourceOrder.ZYX, 120);
        temporary.Add(copied);
        Assert.That(copied.frameRate, Is.EqualTo(source.frameRate));
        Assert.That(copied.length, Is.EqualTo(source.length));
        Assert.That(AnimationUtility.GetAnimationClipSettings(copied).stopTime, Is.EqualTo(0.05f).Within(0.00001f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(copied).loopTime, Is.True);
        Assert.That(AnimationUtility.GetAnimationEvents(copied).Single().functionName, Is.EqualTo("Footstep"));
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
            Assert.That(AnimationUtility.GetEditorCurve(copied, binding).keys, Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys));
    }

    [Test]
    public void QuaternionCopiesStillRepairNaNTangentsWithoutChangingSource()
    {
        var source = Convert(Source());
        var binding = EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "m_LocalPosition.y");
        AnimationUtility.SetEditorCurve(source, binding, new AnimationCurve(new Keyframe(0, 1, 0, 0), new Keyframe(0.1f, 1, float.NaN, 0)));
        var copied = Convert(source);
        Assert.That(AnimationUtility.GetEditorCurve(copied, binding).Evaluate(0.05f), Is.EqualTo(1));
        Assert.That(float.IsNaN(AnimationUtility.GetEditorCurve(source, binding).keys[1].inTangent), Is.True);
    }

    [Test]
    public void QuaternionCopyIsSavedAndLoggedAsSkippedConversion()
    {
        var source = Convert(Source());
        string folder = "Assets/__EulerAnimationRepairTests_" + Guid.NewGuid().ToString("N");
        temporaryFolders.Add(folder);
        LogAssert.Expect(LogType.Log, new Regex("Animation repair: skipped Euler conversion for 'EulerSource'.*Copied to '.*_Copy.anim'"));
        var copied = EulerAnimationRepair.CreateCorrectedCopy(source, folder, EulerAnimationRepair.SourceOrder.XYZ);
        Assert.That(copied, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(copied), Does.EndWith("/EulerSource_Copy.anim"));
        Assert.That(EulerAnimationRepair.RequiresEulerConversion(copied), Is.False);
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
            Assert.That(AnimationUtility.GetEditorCurve(copied, binding).keys, Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys));
    }

    [Test]
    public void ClipsWithoutRotationTracksAreCopiedToo()
    {
        var source = new AnimationClip { name = "PositionOnly", frameRate = 30 };
        temporary.Add(source);
        var binding = EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x");
        AnimationUtility.SetEditorCurve(source, binding, AnimationCurve.Linear(0, 1, 1, 2));
        var copied = Convert(source);
        Assert.That(copied, Is.Not.Null);
        Assert.That(copied, Is.Not.SameAs(source));
        Assert.That(AnimationUtility.GetEditorCurve(copied, binding).keys, Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys));
    }

    [TestCase(float.NaN)]
    [TestCase(-1f)]
    [TestCase(241f)]
    public void InvalidSampleRatesAreRejected(float rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EulerAnimationRepair.BuildCorrectedClip(Source(), EulerAnimationRepair.SourceOrder.XYZ, rate));
    }

    [TestCase("../Outside")]
    [TestCase("Packages/Output")]
    [TestCase("Assets/../Outside")]
    public void OutputCannotEscapeAssets(string folder)
    {
        Assert.Throws<ArgumentException>(() => EulerAnimationRepair.ValidateFolder(folder));
    }

    [Test]
    public void NaNPositionTangentsAreRepairedWithoutChangingSourceOrValidTangents()
    {
        var source = Source();
        var binding = EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "m_LocalPosition.y");
        var keys = new[] { new Keyframe(0, 0.25f, float.NaN, 0), new Keyframe(0.1f, 0.25f, float.NaN, 0) };
        AnimationUtility.SetEditorCurve(source, binding, new AnimationCurve(keys));
        Assert.That(float.IsNaN(AnimationUtility.GetEditorCurve(source, binding).Evaluate(0.05f)), Is.True);
        var corrected = Convert(source);
        var repaired = AnimationUtility.GetEditorCurve(corrected, binding);
        for (int i = 0; i <= 20; i++) Assert.That(repaired.Evaluate(i / 200f), Is.EqualTo(0.25f));
        Assert.That(float.IsNaN(AnimationUtility.GetEditorCurve(source, binding).keys[1].inTangent), Is.True);
        Assert.That(EulerAnimationRepair.RepairInvalidTangents(corrected), Is.Zero);
    }

    [Test]
    public void TangentRepairPreservesLegitimateInfiniteStepTangentsAndTiming()
    {
        var source = Source();
        var binding = EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "m_LocalPosition.y");
        var curve = new AnimationCurve(new Keyframe(0, 1, 0, float.PositiveInfinity), new Keyframe(0.1f, 2, float.PositiveInfinity, 0));
        AnimationUtility.SetEditorCurve(source, binding, curve);
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.stopTime = 0.05f;
        AnimationUtility.SetAnimationClipSettings(source, settings);
        Assert.That(EulerAnimationRepair.RepairInvalidTangents(source), Is.Zero);
        Assert.That(AnimationUtility.GetEditorCurve(source, binding).keys, Is.EqualTo(curve.keys));
        Assert.That(AnimationUtility.GetAnimationClipSettings(source).stopTime, Is.EqualTo(0.05f).Within(0.00001f));
    }

    [Test]
    public void TangentRepairUsesNeighbouringSlopeAndPreservesTrimmedRange()
    {
        var source = Source();
        var binding = EditorCurveBinding.FloatCurve("Bip001", typeof(Transform), "m_LocalPosition.y");
        AnimationUtility.SetEditorCurve(source, binding, new AnimationCurve(new Keyframe(0, 1, 0, float.NaN), new Keyframe(0.1f, 2, float.NaN, 0)));
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.stopTime = 0.05f;
        AnimationUtility.SetAnimationClipSettings(source, settings);
        Assert.That(EulerAnimationRepair.RepairInvalidTangents(source), Is.EqualTo(2));
        var repaired = AnimationUtility.GetEditorCurve(source, binding);
        Assert.That(repaired.keys[0].outTangent, Is.EqualTo(10).Within(0.00001f));
        Assert.That(repaired.keys[1].inTangent, Is.EqualTo(10).Within(0.00001f));
        Assert.That(repaired.Evaluate(0.05f), Is.EqualTo(1.5f).Within(0.00001f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(source).stopTime, Is.EqualTo(0.05f).Within(0.00001f));
    }

    [Test]
    public void ControllerReplacementTraversesNestedStatesAndBlendTrees()
    {
        var original = Source();
        var corrected = Convert(original);
        var controller = new AnimatorController();
        var machine = new AnimatorStateMachine();
        var nested = new AnimatorStateMachine();
        var direct = new AnimatorState { motion = original };
        var blended = new AnimatorState();
        var tree = new BlendTree { children = new[] { new ChildMotion { motion = original, timeScale = 1 } } };
        blended.motion = tree;
        machine.states = new[] { new ChildAnimatorState { state = direct } };
        nested.states = new[] { new ChildAnimatorState { state = blended } };
        machine.stateMachines = new[] { new ChildAnimatorStateMachine { stateMachine = nested } };
        controller.layers = new[] { new AnimatorControllerLayer { name = "Base Layer", stateMachine = machine } };
        temporary.AddRange(new Object[] { controller, machine, nested, direct, blended, tree });
        int count = EulerAnimationRepair.ReplaceControllerClips(controller, new Dictionary<AnimationClip, AnimationClip> { { original, corrected } });
        Assert.That(count, Is.EqualTo(2));
        Assert.That(direct.motion, Is.SameAs(corrected));
        Assert.That(tree.children[0].motion, Is.SameAs(corrected));
    }

    [TestCase("idle")]
    [TestCase("atk1")]
    public void VaporeonCorrectedClipsSampleAll47BonesAtTheModelInitialPose(string animation)
    {
        const string folder = "Assets/Model/Pokemons/Test/";
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Anims/ani_" + animation + "_bat_0134.anim");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "pref_char_bat_013400_vaporeon_lv3.fbx");
        if (source == null || model == null) Assert.Ignore("Optional test Vaporeon assets are not present.");
        var corrected = Convert(source);
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        try
        {
            instance = Object.Instantiate(model);
            SceneManager.MoveGameObjectToScene(instance, scene);
            var reference = instance.transform.Find("reference");
            corrected.SampleAnimation(reference.gameObject, 0);
            int bones = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(corrected).Where(b => b.propertyName == "m_LocalRotation.x"))
            {
                var expected = model.transform.Find("reference/" + binding.path);
                var actual = reference.Find(binding.path);
                Assert.That(actual, Is.Not.Null, binding.path);
                Assert.That(Quaternion.Angle(expected.localRotation, actual.localRotation), Is.LessThan(0.1f), binding.path);
                bones++;
            }
            Assert.That(bones, Is.EqualTo(47));
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void VaporeonStunHasFiniteBonePositionsAndVisibleMeshThroughoutTheTimeline()
    {
        const string folder = "Assets/Model/Pokemons/Test/";
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Anims/ani_stun_bat_0134.anim");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "pref_char_bat_013400_vaporeon_lv3.fbx");
        if (source == null || model == null) Assert.Ignore("Optional test Vaporeon assets are not present.");
        var corrected = Convert(source);
        var mesh = new Mesh();
        temporary.Add(mesh);
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        try
        {
            instance = Object.Instantiate(model);
            SceneManager.MoveGameObjectToScene(instance, scene);
            var reference = instance.transform.Find("reference");
            var bones = reference.GetComponentsInChildren<Transform>(true);
            var renderer = reference.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            // Include subframes and the formerly visible final three frames, not just stored key times.
            for (int frame = 0; frame <= Mathf.CeilToInt(corrected.length * 60); frame++)
            {
                float time = Mathf.Min(frame / 60f, corrected.length);
                corrected.SampleAnimation(reference.gameObject, time);
                foreach (var bone in bones) AssertFinite(bone.position, $"{bone.name} at {time}");
                renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices) AssertFinite(vertex, $"Mesh at {time}");
                Assert.That(mesh.bounds.size.sqrMagnitude, Is.GreaterThan(0.01f), $"Collapsed mesh at {time}");
            }
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void AssertFinite(Vector3 value, string context)
    {
        Assert.That(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
            || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z), Is.False, context);
    }
}
