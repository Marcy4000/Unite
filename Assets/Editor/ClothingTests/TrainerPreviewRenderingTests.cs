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
}
