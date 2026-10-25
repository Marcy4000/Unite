using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class TrainerPreviewRenderingTests
{
    private const string PreviewRendererPath = "Assets/Settings/URP-UIPreview-Renderer.asset";
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // UIObject3D lives in Assembly-CSharp, which this test asmdef cannot reference.
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("Performant")]
    [TestCase("Balanced")]
    [TestCase("HighFidelity")]
    public void EveryQualityLevelReservesRendererSlotOneForPreviews(string quality)
    {
        var pipeline = new SerializedObject(AssetDatabase.LoadMainAssetAtPath($"Assets/Settings/URP-{quality}.asset"));
        var renderers = pipeline.FindProperty("m_RendererDataList");
        Assert.That(pipeline.FindProperty("m_DefaultRendererIndex").intValue, Is.Zero,
            "Gameplay cameras must continue using their original renderer.");
        Assert.That(renderers.arraySize, Is.GreaterThanOrEqualTo(2));
        Assert.That(renderers.GetArrayElementAtIndex(1).objectReferenceValue,
            Is.EqualTo(AssetDatabase.LoadMainAssetAtPath(PreviewRendererPath)));
    }

    [Test]
    public void PreviewRendererKeepsOutlinesWithoutGameplayEffects()
    {
        var renderer = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(PreviewRendererPath));
        var features = renderer.FindProperty("m_RendererFeatures");
        Assert.That(features.arraySize, Is.EqualTo(1));
        var outline = features.GetArrayElementAtIndex(0).objectReferenceValue;
        Assert.That(outline.GetType().Name, Is.EqualTo("TrainerOutlineRendererFeature"));
        Assert.That(new SerializedObject(outline).FindProperty("m_Active").boolValue, Is.True);

        var gameplay = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/URP-HighFidelity-Renderer.asset"));
        var gameplayFeatures = gameplay.FindProperty("m_RendererFeatures");
        Assert.That(Enumerable.Range(0, gameplayFeatures.arraySize).Any(index =>
            gameplayFeatures.GetArrayElementAtIndex(index).objectReferenceValue.GetType().Name == "DecalRendererFeature"),
            Is.True, "The fix must not remove gameplay decals.");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void UIObject3DCameraRendersWithThePreviewRendererAtEveryQuality(int quality)
    {
        int previousQuality = QualitySettings.GetQualityLevel();
        var root = new GameObject("Preview rendering test", typeof(RectTransform));
        root.SetActive(false);
        var container = new GameObject("Preview camera test container");
        var type = GameType("UI.ThreeDimensional.UIObject3D");
        Component preview = null;
        try
        {
            QualitySettings.SetQualityLevel(quality);
            preview = root.AddComponent(type);
            // Avoid creating a real trainer or touching the scene's shared preview containers.
            type.GetField("_ObjectPrefab", InstanceMembers).SetValue(preview, container.transform);
            type.GetField("_container", InstanceMembers).SetValue(preview, container.transform);
            type.GetField("_textureSize", InstanceMembers).SetValue(preview, new Vector2(32, 32));
            type.GetMethod("SetupTargetCamera", InstanceMembers).Invoke(preview, null);

            var camera = (Camera)type.GetField("_targetCamera", InstanceMembers).GetValue(preview);
            var cameraData = camera.GetComponent(GameType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData"));
            Assert.That(new SerializedObject(cameraData).FindProperty("m_RendererIndex").intValue, Is.EqualTo(1));
            type.GetMethod("Render", InstanceMembers).Invoke(preview, new object[] { true });
            Assert.That(camera.targetTexture.IsCreated(), Is.True);
            Assert.That(camera.targetTexture.depth, Is.GreaterThanOrEqualTo(24),
                "D16 causes face/nose submeshes to fight at longer camera distances.");
            var pipeline = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(
                $"Assets/Settings/URP-{new[] { "Performant", "Balanced", "HighFidelity" }[quality]}.asset"));
            var descriptor = camera.targetTexture.descriptor;
            descriptor.msaaSamples = pipeline.FindProperty("m_MSAA").intValue;
            Assert.That(camera.targetTexture.antiAliasing,
                Is.EqualTo(SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor)));
            // Changing quality must update the same preview's MSAA, not just new cameras.
            QualitySettings.SetQualityLevel((quality + 1) % 3);
            type.GetMethod("Render", InstanceMembers).Invoke(preview, new object[] { true });
            QualitySettings.SetQualityLevel(quality);
            type.GetMethod("Render", InstanceMembers).Invoke(preview, new object[] { true });
            Assert.That(camera.targetTexture.antiAliasing,
                Is.EqualTo(SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor)));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (preview != null) type.GetMethod("DestroyResources", InstanceMembers).Invoke(preview, null);
            Object.DestroyImmediate(root);
            if (container != null) Object.DestroyImmediate(container);
            QualitySettings.SetQualityLevel(previousQuality);
        }
    }

    [Test]
    public void DistantFaceMaterialsMatchHighPrecisionDepthAndKeepAntialiasedEdges()
    {
        int previousQuality = QualitySettings.GetQualityLevel();
        var previousActive = RenderTexture.active;
        var root = new GameObject("Face precision test", typeof(RectTransform));
        root.SetActive(false);
        var container = new GameObject("Face precision camera");
        container.transform.position = new Vector3(500, 500, 500);
        var type = GameType("UI.ThreeDimensional.UIObject3D");
        Component preview = null;
        RenderTexture reference = null;
        RenderTexture resolvedReference = null;
        try
        {
            QualitySettings.SetQualityLevel(2);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Model/Clothing/Male/Head/Lv3/pref_lob_40000head_lv3/pref_lob_40000head_lv3.fbx");
            Assert.That(asset, Is.Not.Null);
            var head = Object.Instantiate(asset, container.transform, false);
            preview = root.AddComponent(type);
            type.GetField("_ObjectPrefab", InstanceMembers).SetValue(preview, head.transform);
            type.GetField("_container", InstanceMembers).SetValue(preview, container.transform);
            type.GetField("_textureSize", InstanceMembers).SetValue(preview, new Vector2(384, 384));
            type.GetMethod("SetupTargetCamera", InstanceMembers).Invoke(preview, null);
            var camera = (Camera)type.GetField("_targetCamera", InstanceMembers).GetValue(preview);
            foreach (var transform in head.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = camera.gameObject.layer;
            foreach (var renderer in head.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.updateWhenOffscreen = true;
            camera.transform.position = container.transform.position + new Vector3(0, 1.53f, 10);
            camera.transform.LookAt(container.transform.position + new Vector3(0, 1.53f, 0));
            camera.fieldOfView = 2 * Mathf.Atan(0.36f / 10) * Mathf.Rad2Deg;
            type.GetMethod("Render", InstanceMembers).Invoke(preview, new object[] { true });
            var target = camera.targetTexture;
            var source = target.antiAliasing > 1
                ? (RenderTexture)type.GetField("_resolvedRenderTexture", InstanceMembers).GetValue(preview)
                : target;
            var actual = ReadPixels(source);

            var descriptor = target.descriptor;
            descriptor.depthBufferBits = 32;
            reference = new RenderTexture(descriptor);
            camera.targetTexture = reference;
            camera.Render();
            var referenceSource = reference;
            if (reference.antiAliasing > 1)
            {
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                resolvedReference = new RenderTexture(descriptor);
                resolvedReference.Create();
                reference.ResolveAntiAliasedSurface(resolvedReference);
                referenceSource = resolvedReference;
            }
            var expected = ReadPixels(referenceSource);
            int differentPixels = actual.Zip(expected, (a, b) =>
                Mathf.Abs(a.r - b.r) > 2 || Mathf.Abs(a.g - b.g) > 2 || Mathf.Abs(a.b - b.b) > 2).Count(different => different);
            Assert.That(differentPixels, Is.LessThan(20),
                "The overlapping nose/face materials must not acquire depth-fighting seams.");
            Assert.That(actual.Count(pixel => pixel.a > 0), Is.GreaterThan(1000),
                "This regression test must render a real face, not an empty camera.");
            if (target.antiAliasing > 1)
                Assert.That(actual.Count(pixel => pixel.a > 0 && pixel.a < 255), Is.GreaterThan(0),
                    "MSAA coverage must survive the resolve, including transparent silhouette edges.");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (preview != null) type.GetMethod("DestroyResources", InstanceMembers).Invoke(preview, null);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(container);
            if (reference != null) Object.DestroyImmediate(reference);
            if (resolvedReference != null) Object.DestroyImmediate(resolvedReference);
            QualitySettings.SetQualityLevel(previousQuality);
        }
    }

    private static Color32[] ReadPixels(RenderTexture source)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = source;
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            return texture.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(texture);
        }
    }
}
