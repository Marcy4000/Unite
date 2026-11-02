using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class ChineseLobbyMaterialImporter
{
    public static readonly string[] ModelFolders =
    {
        "Assets/Model/Lobby/pref_sce_lobby_newcity_online_lv3",
        "Assets/Model/Lobby/pref_sce_lobby_newmaincity_01_lv4"
    };

    [MenuItem("Tools/Chinese Lobby/Restore Materials From JSON")]
    public static void RestoreAll()
    {
        var sources = new List<MaterialSource>();
        foreach (string folder in ModelFolders)
        {
            foreach (string path in Directory.GetFiles(folder + "/Materials", "*.json").OrderBy(path => path))
                sources.Add(ReadSource(folder, path));
        }
        int unnamedTextures = sources.Sum(source => ((JObject)source.Data["m_SavedProperties"]["m_TexEnvs"])
            .Properties().Count(entry => !(bool)entry.Value["m_Texture"]["IsNull"]
                && string.IsNullOrEmpty((string)entry.Value["m_Texture"]["Name"])
                && source.Textures[entry.Name] == null));
        if (unnamedTextures > 0)
            Debug.LogWarning($"Chinese Lobby: {unnamedTextures} original texture references have no exported name (reflection cubemaps). They remain unassigned; their original image data cannot be recovered from material JSON.");

        RestorePackedTextureImportSettings(sources);
        foreach (MaterialSource source in sources)
            RestoreMaterial(source);
        EnsureHologramStencilPass();
        AssetDatabase.SaveAssets();

        foreach (string folder in ModelFolders)
        {
            string modelPath = folder + "/" + Path.GetFileName(folder) + ".fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Model importer not found: " + modelPath);
            var mappings = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (MaterialSource source in sources.Where(source => source.Folder == folder))
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), source.Name);
                var material = AssetDatabase.LoadAssetAtPath<Material>(source.MaterialPath);
                if (!mappings.TryGetValue(identifier, out UnityEngine.Object previous) || previous != material)
                {
                    importer.AddRemap(identifier, material);
                    changed = true;
                }
            }
            if (changed)
                importer.SaveAndReimport();
        }
        Debug.Log($"Chinese Lobby: restored {sources.Count} materials and remapped both FBX models.");
    }

    private static void RestorePackedTextureImportSettings(IEnumerable<MaterialSource> sources)
    {
        var paths = new HashSet<string>();
        foreach (MaterialSource source in sources)
        {
            bool environment = source.Shader.name.StartsWith("==MAIN==/", StringComparison.Ordinal)
                || source.Shader.name.StartsWith("==PGAME==/BattleV3/", StringComparison.Ordinal);
            if (!environment)
                continue;
            foreach (var entry in source.Textures)
            {
                if (entry.Value != null && (entry.Key == "_NormalMap" || entry.Key == "_BumpMap"
                    || entry.Key == "_MixMap" || entry.Key == "_LightmapTex"))
                    paths.Add(AssetDatabase.GetAssetPath(entry.Value));
            }
        }
        foreach (string path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Packed texture importer not found: " + path);
            if (importer.textureType == TextureImporterType.Default && !importer.sRGBTexture && !importer.alphaIsTransparency)
                continue;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }
    }

    private static MaterialSource ReadSource(string folder, string path)
    {
        var data = JObject.Parse(File.ReadAllText(path));
        string name = (string)data["m_Name"];
        string shaderName = (string)data["m_Shader"]?["Name"];
        Shader shader = Shader.Find(shaderName);
        if (string.IsNullOrEmpty(name) || shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException($"Cannot restore {path}: missing name or shader '{shaderName}', or shader has compilation errors.");

        var textures = new Dictionary<string, Texture>();
        foreach (JProperty entry in ((JObject)data["m_SavedProperties"]["m_TexEnvs"]).Properties())
        {
            JToken reference = entry.Value["m_Texture"];
            string textureName = (string)reference["Name"];
            Texture texture = null;
            if (!(bool)reference["IsNull"] && !string.IsNullOrEmpty(textureName))
            {
                string[] candidates = Directory.GetFiles(folder, textureName + ".*")
                    .Where(candidate => Path.GetExtension(candidate) != ".meta")
                    .ToArray();
                foreach (string candidate in candidates)
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture>(candidate);
                    if (texture != null)
                        break;
                }
                if (texture == null)
                    throw new InvalidOperationException($"Missing local texture '{textureName}' for {path}.");
            }
            else if (!(bool)reference["IsNull"])
            {
                string overridePath = UnnamedTextureOverride(name, entry.Name);
                if (overridePath != null)
                {
                    texture = AssetDatabase.LoadAssetAtPath<Cubemap>(overridePath);
                    if (texture == null)
                        throw new InvalidOperationException($"Missing supplied cubemap '{overridePath}' for {path}.");
                }
            }
            textures.Add(entry.Name, texture);
        }
        return new MaterialSource
        {
            Folder = folder,
            Name = name,
            Shader = shader,
            Data = data,
            Textures = textures,
            MaterialPath = folder + "/Materials/" + name + ".mat"
        };
    }

    private static string UnnamedTextureOverride(string materialName, string propertyName)
    {
        if (propertyName != "_ReflectionCube")
            return null;
        switch (materialName)
        {
            case "mat_newcity_glass01_lv3":
            case "mat_newcity_glass01a_lv3":
            case "mat_newcity_glass02_lv3":
                return "Assets/Cubemaps/HDRI_newcity_techglass.dds";
            case "mat_newcity_water06_lv3":
                return "Assets/Cubemaps/HDRI_CartoonWater01.dds";
            default:
                return null;
        }
    }

    private static void RestoreMaterial(MaterialSource source)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(source.MaterialPath);
        if (material == null)
        {
            material = new Material(source.Shader);
            AssetDatabase.CreateAsset(material, source.MaterialPath);
        }
        material.shader = source.Shader;
        material.name = source.Name;
        material.shaderKeywords = Array.Empty<string>();
        var serialized = new SerializedObject(material);
        var saved = serialized.FindProperty("m_SavedProperties");
        var properties = (JObject)source.Data["m_SavedProperties"];
        RestoreValues(saved.FindPropertyRelative("m_Floats"), properties["m_Floats"], (target, value) => target.floatValue = (float)value);
        RestoreValues(saved.FindPropertyRelative("m_Ints"), properties["m_Ints"], (target, value) => target.intValue = (int)value);
        RestoreValues(saved.FindPropertyRelative("m_Colors"), properties["m_Colors"], (target, value) =>
            target.colorValue = new Color((float)value["r"], (float)value["g"], (float)value["b"], (float)value["a"]));
        RestoreValues(saved.FindPropertyRelative("m_TexEnvs"), properties["m_TexEnvs"], (target, value) =>
        {
            string propertyName = ((JProperty)value.Parent).Name;
            target.FindPropertyRelative("m_Texture").objectReferenceValue = source.Textures[propertyName];
            target.FindPropertyRelative("m_Scale").vector2Value = ReadVector(value["m_Scale"]);
            target.FindPropertyRelative("m_Offset").vector2Value = ReadVector(value["m_Offset"]);
        });
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (material.HasProperty("_ReflectionCubeAvailable"))
            material.SetFloat("_ReflectionCubeAvailable", source.Textures.TryGetValue("_ReflectionCube", out Texture cube) && cube != null ? 1f : 0f);
        ConfigureRendering(material, source.Data);
        EditorUtility.SetDirty(material);
    }

    private static void RestoreValues(SerializedProperty target, JToken source, Action<SerializedProperty, JToken> assign)
    {
        if (!(source is JObject values))
            return;
        foreach (JProperty entry in values.Properties())
        {
            SerializedProperty element = null;
            for (int index = 0; index < target.arraySize; index++)
            {
                var candidate = target.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("first").stringValue == entry.Name)
                {
                    element = candidate;
                    break;
                }
            }
            if (element == null)
            {
                int index = target.arraySize;
                target.InsertArrayElementAtIndex(index);
                element = target.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("first").stringValue = entry.Name;
            }
            assign(element.FindPropertyRelative("second"), entry.Value);
        }
    }

    private static Vector2 ReadVector(JToken value)
    {
        return new Vector2((float)value["X"], (float)value["Y"]);
    }

    private static void ConfigureRendering(Material material, JObject data)
    {
        JToken floats = data["m_SavedProperties"]["m_Floats"];
        bool environment = material.shader.name.StartsWith("==MAIN==/", StringComparison.Ordinal)
            || material.shader.name.Contains("MetalEmissColorLMV3");
        int queue = material.shader.renderQueue;
        if (environment)
        {
            int renderType = (int?)floats["_RenderType"] ?? queue;
            if (renderType >= 1000)
                queue = renderType;
            queue += (int?)floats["_RenderQueueOffset"] ?? 0;
        }
        queue += (int?)floats["_QueueOffset"] ?? 0;
        material.renderQueue = queue;
        bool alphaClip = ((float?)floats["_UseAlphaClip"] ?? 0f) > 0.5f
            || ((float?)floats["_AlphaClip"] ?? 0f) > 0.5f;
        bool transparent = environment ? ((int?)floats["_RenderType"] ?? queue) >= 3000 : queue >= 3000;
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : alphaClip ? "TransparentCutout" : "Opaque");
        SetKeyword(material, "_ALPHATEST_ON", alphaClip);
        SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
        SetKeyword(material, "_NORMALMAP", ((float?)floats["_UseNormalMap"] ?? 0f) > 0.5f);
        SetKeyword(material, "_EMISSION", SourceHasEmission(data));
        if (material.shader.name == "==PGAME==/Fx2/m_holographic2Pass")
        {
            foreach (string property in new[] { "_TEX", "_WSUV" })
            {
                bool enabled = ((float?)floats[property] ?? 0f) > 0.5f;
                SetKeyword(material, property + "_ON", enabled);
                SetKeyword(material, property + "_OFF", !enabled);
            }
        }
        material.SetShaderPassEnabled("ShadowCaster", ((float?)floats["_DisableShadowCaster"] ?? 0f) < 0.5f);
        material.doubleSidedGI = ((float?)floats["_Cull"] ?? 2f) == 0f;
        if (material.name == "mat_newcity_skybox02_lv3")
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
    }

    private static void EnsureHologramStencilPass()
    {
        const string featureName = "Chinese Lobby Hologram Stencil";
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == "Assets/Settings/URP-UIPreview-Renderer.asset")
                continue;
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            var feature = renderer.rendererFeatures.OfType<RenderObjects>().FirstOrDefault(candidate => candidate.name == featureName);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RenderObjects>();
                feature.name = featureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            feature.settings.passTag = featureName;
            feature.settings.Event = RenderPassEvent.BeforeRenderingTransparents;
            feature.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
            feature.settings.filterSettings.LayerMask = -1;
            feature.settings.filterSettings.PassNames = new[] { "FurPass1" };
            feature.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            feature.settings.overrideDepthState = false;
            feature.settings.stencilSettings.overrideStencilState = false;
            feature.SetActive(true);
            feature.Create();
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index], out string featureGuid, out long localId);
                map.GetArrayElementAtIndex(index).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
    }

    private static bool SourceHasEmission(JObject data)
    {
        JToken color = data["m_SavedProperties"]["m_Colors"]?["_EmissionColor"];
        return color != null && ((float)color["r"] > 0f || (float)color["g"] > 0f || (float)color["b"] > 0f);
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }

    private sealed class MaterialSource
    {
        public string Folder;
        public string Name;
        public string MaterialPath;
        public Shader Shader;
        public JObject Data;
        public Dictionary<string, Texture> Textures;
    }
}
