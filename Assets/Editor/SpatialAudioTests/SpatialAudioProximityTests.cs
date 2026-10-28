using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class SpatialAudioProximityTests
{
    private static readonly Type ManagerType = Type.GetType("SpatialAudioManager, Assembly-CSharp", true);
    private static readonly MethodInfo GainMethod = ManagerType.GetMethod("GetProximityGain", BindingFlags.NonPublic | BindingFlags.Static);
    private Camera camera;

    [SetUp]
    public void SetUp()
    {
        camera = new GameObject("Proximity audio test camera").AddComponent<Camera>();
        camera.fieldOfView = 25f;
        camera.aspect = 16f / 9f;
        camera.transform.SetPositionAndRotation(new Vector3(0f, 18.85f, -18.6f), Quaternion.Euler(45f, 0f, 0f));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(camera.gameObject);
    }

    private float Gain(Vector3 position, float fadeWidth = 0.1f)
    {
        return (float)GainMethod.Invoke(null, new object[] { camera, position, fadeWidth });
    }

    [TestCase(0.5f, 0.5f)]
    [TestCase(0.1f, 0.5f)]
    [TestCase(0.9f, 0.5f)]
    [TestCase(0.5f, 0.1f)]
    [TestCase(0.5f, 0.9f)]
    [TestCase(0.2f, 0.8f)]
    public void VisibleSourcesRemainAtFullVolumeUntilTheEdgeBand(float horizontal, float vertical)
    {
        Vector3 position = camera.ViewportToWorldPoint(new Vector3(horizontal, vertical, 25f));
        Assert.That(Gain(position), Is.EqualTo(1f).Within(0.0001f));
    }

    [TestCase(0.05f, 0.5f)]
    [TestCase(0.95f, 0.5f)]
    [TestCase(0.5f, 0.05f)]
    [TestCase(0.5f, 0.95f)]
    [TestCase(0.05f, 0.05f)]
    public void OnlyTheNarrowScreenEdgeBandAttenuates(float horizontal, float vertical)
    {
        Vector3 position = camera.ViewportToWorldPoint(new Vector3(horizontal, vertical, 25f));
        Assert.That(Gain(position), Is.EqualTo(0.5f).Within(0.0001f));
    }

    [TestCase(0f, 0.5f)]
    [TestCase(1f, 0.5f)]
    [TestCase(0.5f, 0f)]
    [TestCase(0.5f, 1f)]
    [TestCase(-0.1f, 0.5f)]
    [TestCase(1.1f, 0.5f)]
    [TestCase(0.5f, -0.1f)]
    [TestCase(0.5f, 1.1f)]
    public void OffscreenSourcesAreSilent(float horizontal, float vertical)
    {
        Vector3 position = camera.ViewportToWorldPoint(new Vector3(horizontal, vertical, 25f));
        Assert.That(Gain(position), Is.EqualTo(0f).Within(0.0001f));
    }

    [TestCase(-10f)]
    [TestCase(0.1f)]
    [TestCase(1001f)]
    public void SourcesOutsideCameraDepthAreSilent(float depth)
    {
        Vector3 position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        Assert.That(Gain(position), Is.Zero);
    }

    [Test]
    public void MissingOrDisabledCameraDoesNotFallBackToGlobalPlayback()
    {
        Assert.That(GainMethod.Invoke(null, new object[] { null, Vector3.zero, 0.1f }), Is.EqualTo(0f));
        camera.enabled = false;
        Assert.That(Gain(Vector3.zero), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ZoomAndAspectRatioUseTheRenderedViewport(bool orthographic)
    {
        camera.orthographic = orthographic;
        camera.orthographicSize = 12f;
        camera.fieldOfView = 60f;
        camera.aspect = 9f / 16f;
        Vector3 edge = camera.ViewportToWorldPoint(new Vector3(0.95f, 0.5f, 25f));
        Assert.That(Gain(edge), Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void PanningAwayFromASourceMakesItSilent()
    {
        Vector3 position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 25f));
        Assert.That(Gain(position), Is.EqualTo(1f));
        camera.transform.position += camera.transform.right * 100f;
        Assert.That(Gain(position), Is.Zero);
    }

    [Test]
    public void FadeWidthIsConfigurable()
    {
        Vector3 position = camera.ViewportToWorldPoint(new Vector3(0.025f, 0.5f, 25f));
        Assert.That(Gain(position, 0.05f), Is.EqualTo(0.5f).Within(0.0001f));
    }
}
