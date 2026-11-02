using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ChineseLobbyMaterialTests
{
    [Test]
    public void SkyDomeRendersItsInwardFaces()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Model/Lobby/pref_sce_lobby_newcity_online_lv3/Materials/mat_newcity_skybox02_lv3.mat");
        Assert.That(material, Is.Not.Null);
        Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Front));
        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
    }

    private static readonly string[] ModelFolders =
    {
        "Assets/Model/Lobby/pref_sce_lobby_newcity_online_lv3",
        "Assets/Model/Lobby/pref_sce_lobby_newmaincity_01_lv4"
    };

    private static IEnumerable<string> MaterialJsonPaths()
    {
        return ModelFolders.SelectMany(folder => Directory.GetFiles(folder + "/Materials", "*.json")).OrderBy(path => path);
    }

    [TestCaseSource(nameof(MaterialJsonPaths))]
    public void MaterialPreservesOriginalShaderAndSavedProperties(string jsonPath)
    {
        var data = JObject.Parse(File.ReadAllText(jsonPath));
        var material = AssetDatabase.LoadAssetAtPath<Material>(Path.ChangeExtension(jsonPath, ".mat"));
        Assert.That(material, Is.Not.Null, jsonPath);
        Assert.That(material.shader.name, Is.EqualTo((string)data["m_Shader"]["Name"]));
        Assert.That(material.shader.isSupported, Is.True, material.shader.name);
        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, material.shader.name);
        var saved = new SerializedObject(material).FindProperty("m_SavedProperties");
        var properties = (JObject)data["m_SavedProperties"];
        foreach (JProperty entry in ((JObject)properties["m_Floats"]).Properties())
            Assert.That(FindValue(saved, "m_Floats", entry.Name).floatValue, Is.EqualTo((float)entry.Value).Within(0.00001f), entry.Name);
        foreach (JProperty entry in ((JObject)properties["m_Colors"]).Properties())
        {
            Color actual = FindValue(saved, "m_Colors", entry.Name).colorValue;
            Assert.That(actual.r, Is.EqualTo((float)entry.Value["r"]).Within(0.00001f), entry.Name);
            Assert.That(actual.g, Is.EqualTo((float)entry.Value["g"]).Within(0.00001f), entry.Name);
            Assert.That(actual.b, Is.EqualTo((float)entry.Value["b"]).Within(0.00001f), entry.Name);
            Assert.That(actual.a, Is.EqualTo((float)entry.Value["a"]).Within(0.00001f), entry.Name);
        }
        foreach (JProperty entry in ((JObject)properties["m_TexEnvs"]).Properties())
        {
            SerializedProperty actual = FindValue(saved, "m_TexEnvs", entry.Name);
            var texture = actual.FindPropertyRelative("m_Texture").objectReferenceValue;
            if ((bool)entry.Value["m_Texture"]["IsNull"])
                Assert.That(texture, Is.Null, entry.Name);
            else if (string.IsNullOrEmpty((string)entry.Value["m_Texture"]["Name"]))
            {
                Assert.That(entry.Name, Is.EqualTo("_ReflectionCube"));
                Assert.That(texture, Is.TypeOf<Cubemap>());
                string cubeName = material.name == "mat_newcity_water06_lv3" ? "HDRI_CartoonWater01" : "HDRI_newcity_techglass";
                Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo("Assets/Cubemaps/" + cubeName + ".dds"));
                Assert.That(material.GetFloat("_ReflectionCubeAvailable"), Is.EqualTo(1f));
            }
            else
            {
                Assert.That(texture, Is.Not.Null, entry.Name);
                Assert.That(texture.name, Is.EqualTo((string)entry.Value["m_Texture"]["Name"]), entry.Name);
                string modelFolder = Path.GetDirectoryName(Path.GetDirectoryName(jsonPath)).Replace('\\', '/');
                Assert.That(AssetDatabase.GetAssetPath(texture), Does.StartWith(modelFolder + "/"));
                bool environment = material.shader.name.StartsWith("==MAIN==/") || material.shader.name.StartsWith("==PGAME==/BattleV3/");
                if (environment && new[] { "_NormalMap", "_BumpMap", "_MixMap", "_LightmapTex" }.Contains(entry.Name))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                    Assert.That(importer.sRGBTexture, Is.False, texture.name);
                    Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default), texture.name);
                    Assert.That(importer.alphaIsTransparency, Is.False, texture.name);
                }
            }
            Assert.That(actual.FindPropertyRelative("m_Scale").vector2Value,
                Is.EqualTo(new Vector2((float)entry.Value["m_Scale"]["X"], (float)entry.Value["m_Scale"]["Y"])), entry.Name);
            Assert.That(actual.FindPropertyRelative("m_Offset").vector2Value,
                Is.EqualTo(new Vector2((float)entry.Value["m_Offset"]["X"], (float)entry.Value["m_Offset"]["Y"])), entry.Name);
        }
        if (material.shader.name.StartsWith("==MAIN==/"))
        {
            int expectedQueue = (int)properties["m_Floats"]["_RenderType"]
                + ((int?)properties["m_Floats"]["_RenderQueueOffset"] ?? 0)
                + ((int?)properties["m_Floats"]["_QueueOffset"] ?? 0);
            Assert.That(material.renderQueue, Is.EqualTo(expectedQueue));
        }
        if (material.shader.name == "==PGAME==/Fx2/m_holographic2Pass")
        {
            foreach (string property in new[] { "_TEX", "_WSUV" })
            {
                bool enabled = (float)properties["m_Floats"][property] > 0.5f;
                Assert.That(material.IsKeywordEnabled(property + "_ON"), Is.EqualTo(enabled));
                Assert.That(material.IsKeywordEnabled(property + "_OFF"), Is.EqualTo(!enabled));
            }
        }
    }

    [Test]
    public void HologramStencilPassRunsBeforeTransparentColor()
    {
        string[] renderers = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" })
            .Where(guid => AssetDatabase.GUIDToAssetPath(guid) != "Assets/Settings/URP-UIPreview-Renderer.asset").ToArray();
        Assert.That(renderers.Length, Is.GreaterThan(0));
        foreach (string guid in renderers)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            var features = renderer.rendererFeatures.OfType<RenderObjects>()
                .Where(feature => feature.name == "Chinese Lobby Hologram Stencil").ToArray();
            Assert.That(features.Length, Is.EqualTo(1), renderer.name);
            var feature = features[0];
            Assert.That(feature.isActive, Is.True);
            Assert.That(feature.settings.Event, Is.EqualTo(RenderPassEvent.BeforeRenderingTransparents));
            Assert.That(feature.settings.filterSettings.PassNames, Is.EqualTo(new[] { "FurPass1" }));
            Assert.That(feature.settings.filterSettings.RenderQueueType, Is.EqualTo(RenderQueueType.Transparent));
            Assert.That((int)feature.settings.filterSettings.LayerMask, Is.EqualTo(-1));
            Assert.That(feature.settings.overrideDepthState, Is.False);
            Assert.That(feature.settings.stencilSettings.overrideStencilState, Is.False);
            var map = new SerializedObject(renderer).FindProperty("m_RendererFeatureMap");
            Assert.That(map.arraySize, Is.EqualTo(renderer.rendererFeatures.Count));
            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index], out string featureGuid, out long localId);
                Assert.That(map.GetArrayElementAtIndex(index).longValue, Is.EqualTo(localId));
            }
        }
    }

    [TestCase("Assets/Model/Lobby/pref_sce_lobby_newcity_online_lv3")]
    [TestCase("Assets/Model/Lobby/pref_sce_lobby_newmaincity_01_lv4")]
    public void EveryModelMaterialUsesItsExternalJsonMaterial(string folder)
    {
        string modelPath = folder + "/" + Path.GetFileName(folder) + ".fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Assert.That(model, Is.Not.Null);
        var materials = model.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).ToArray();
        Assert.That(materials.Length, Is.GreaterThan(0));
        foreach (Material material in materials)
        {
            Assert.That(material, Is.Not.Null, modelPath);
            string expectedPath = folder + "/Materials/" + material.name + ".mat";
            Assert.That(File.Exists(Path.ChangeExtension(expectedPath, ".json")), Is.True, material.name);
            Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(expectedPath), material.name);
        }
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        var mappings = importer.GetExternalObjectMap();
        foreach (string jsonPath in Directory.GetFiles(folder + "/Materials", "*.json"))
        {
            string name = (string)JObject.Parse(File.ReadAllText(jsonPath))["m_Name"];
            var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
            Assert.That(mappings.ContainsKey(identifier), Is.True, name);
            Assert.That(AssetDatabase.GetAssetPath(mappings[identifier]), Is.EqualTo(Path.ChangeExtension(jsonPath, ".mat")), name);
        }
    }

    private static SerializedProperty FindValue(SerializedProperty saved, string collection, string name)
    {
        SerializedProperty entries = saved.FindPropertyRelative(collection);
        for (int index = 0; index < entries.arraySize; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            if (entry.FindPropertyRelative("first").stringValue == name)
                return entry.FindPropertyRelative("second");
        }
        Assert.Fail("Missing saved property: " + name);
        return null;
    }
}
