using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class DisplayPerformanceTests
{
    private RenderPipelineAsset previousPipeline;
    private RenderPipelineAsset firstPipeline;
    private RenderPipelineAsset secondPipeline;
    private object policy;
    private int previousTarget;
    private int previousVSync;

    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Invoke(object target, string name, params object[] arguments) => target.GetType()
        .GetMethod(name).Invoke(target, arguments);
    private static float Scale(RenderPipelineAsset pipeline) => (float)pipeline.GetType().GetProperty("renderScale").GetValue(pipeline);
    private static void Scale(RenderPipelineAsset pipeline, float value) => pipeline.GetType().GetProperty("renderScale").SetValue(pipeline, value);
    private static float Multiplier(float distance, float fieldOfView) => (float)RuntimeType("ZoomRenderScalePolicy")
        .GetMethod("CalculateMultiplier").Invoke(null, new object[] { distance, fieldOfView });

    [SetUp]
    public void SetUp()
    {
        previousPipeline = QualitySettings.renderPipeline;
        previousTarget = Application.targetFrameRate;
        previousVSync = QualitySettings.vSyncCount;
        var pipelineType = RuntimeType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
        firstPipeline = (RenderPipelineAsset)ScriptableObject.CreateInstance(pipelineType);
        secondPipeline = (RenderPipelineAsset)ScriptableObject.CreateInstance(pipelineType);
        policy = Activator.CreateInstance(RuntimeType("ZoomRenderScalePolicy"));
        QualitySettings.renderPipeline = firstPipeline;
    }

    [TearDown]
    public void TearDown()
    {
        Invoke(policy, "Restore");
        QualitySettings.renderPipeline = previousPipeline;
        Application.targetFrameRate = previousTarget;
        QualitySettings.vSyncCount = previousVSync;
        Object.DestroyImmediate(firstPipeline);
        Object.DestroyImmediate(secondPipeline);
    }

    [Test]
    public void ZoomScaleIsClampedAndTracksDistanceAndFieldOfView()
    {
        float normalDistance = new Vector3(0f, 18.85f, -18.6f).magnitude;
        float cannon = Multiplier(new Vector3(0f, 27.7f, -28.9f).magnitude, 25f);
        float jump = Multiplier(new Vector3(0f, 57.7f, -61f).magnitude, 30f);
        Assert.That(Multiplier(normalDistance, 25f), Is.EqualTo(1f));
        Assert.That(Multiplier(0f, 25f), Is.EqualTo(1f));
        Assert.That(cannon, Is.InRange(0.8f, 0.999f));
        Assert.That(jump, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(Multiplier(10000f, 179f), Is.EqualTo(0.8f));
        Assert.That(Multiplier(normalDistance, 60f), Is.LessThan(1f));
        Assert.That(jump, Is.LessThan(cannon));
    }

    [Test]
    public void ScalingNeverCompoundsAndNormalZoomRestoresUserQuality()
    {
        Scale(firstPipeline, 0.7f);
        for (int iteration = 0; iteration < 20; iteration++) Invoke(policy, "Apply", 100f, 30f);
        Assert.That(Scale(firstPipeline), Is.EqualTo(0.56f).Within(0.0001f));
        Invoke(policy, "Apply", 26.48f, 25f);
        Assert.That(Scale(firstPipeline), Is.EqualTo(0.7f).Within(0.0001f));
        Invoke(policy, "Apply", 100f, 30f);
        Invoke(policy, "Restore");
        Invoke(policy, "Restore");
        Assert.That(Scale(firstPipeline), Is.EqualTo(0.7f).Within(0.0001f));
    }

    [Test]
    public void QualityPipelineSwitchRestoresOldAssetAndUsesNewBaseline()
    {
        Scale(firstPipeline, 1f);
        Scale(secondPipeline, 0.75f);
        Invoke(policy, "Apply", 100f, 30f);
        QualitySettings.renderPipeline = secondPipeline;
        Invoke(policy, "Apply", 100f, 30f);
        Assert.That(Scale(firstPipeline), Is.EqualTo(1f));
        Assert.That(Scale(secondPipeline), Is.EqualTo(0.6f).Within(0.0001f));
        Invoke(policy, "Restore");
        Assert.That(Scale(secondPipeline), Is.EqualTo(0.75f));
    }

    [Test]
    public void ExternalRenderScaleChangesAreNotOverwrittenOnTeardown()
    {
        Invoke(policy, "Apply", 100f, 30f);
        Scale(firstPipeline, 0.9f);
        Invoke(policy, "Restore");
        Assert.That(Scale(firstPipeline), Is.EqualTo(0.9f));
        Invoke(policy, "Apply", 100f, 30f);
        Scale(firstPipeline, 0.7f);
        Invoke(policy, "Apply", 100f, 30f);
        Invoke(policy, "Restore");
        Assert.That(Scale(firstPipeline), Is.EqualTo(0.7f));
    }

    [Test]
    public void QuantizationCannotDropBelowTheMinimumMultiplier()
    {
        Scale(firstPipeline, 0.73f);
        Invoke(policy, "Apply", 100f, 30f);
        Assert.That(Scale(firstPipeline), Is.InRange(0.73f * 0.8f, 0.73f));
        Invoke(policy, "Restore");
        Assert.That(Scale(firstPipeline), Is.EqualTo(0.73f));
    }

    [Test]
    public void DisablingGameplayCameraRestoresRenderScale()
    {
        var root = new GameObject("Display performance camera test");
        try
        {
            var controller = root.AddComponent(RuntimeType("CameraController"));
            var cameraPolicy = controller.GetType().GetField("renderScalePolicy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Scale(firstPipeline, 0.85f);
            Invoke(cameraPolicy, "Apply", 100f, 30f);
            Assert.That(Scale(firstPipeline), Is.LessThan(0.85f));
            ((Behaviour)controller).enabled = false;
            controller.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            Assert.That(Scale(firstPipeline), Is.EqualTo(0.85f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(0d, 60)]
    [TestCase(double.NaN, 60)]
    [TestCase(double.PositiveInfinity, 60)]
    [TestCase(59.94d, 60)]
    [TestCase(90d, 90)]
    [TestCase(119.88d, 120)]
    [TestCase(144d, 144)]
    [TestCase(165d, 165)]
    public void MobileTargetUsesAdvertisedRefreshWithSafeFallback(double refresh, int expected)
    {
        Assert.That(RuntimeType("AndroidDisplaySettings").GetMethod("SelectTargetFrameRate")
            .Invoke(null, new object[] { refresh }), Is.EqualTo(expected));
    }

    [Test]
    public void MobilePolicyDoesNotChangeDesktopSettings()
    {
        Application.targetFrameRate = 47;
        QualitySettings.vSyncCount = 1;
        Assert.That(RuntimeType("AndroidDisplaySettings").GetMethod("ApplyMobileFrameRatePolicy").Invoke(null, null), Is.False);
        Assert.That(Application.targetFrameRate, Is.EqualTo(47));
        Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
    }
}
