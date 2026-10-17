using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Keep the existing class/menu/API: other editor tools may still call it.
public class ClothingMaterialConverter : EditorWindow
{
    public const string RootFolder = "Assets/Model/Clothing";
    public const string FaceShader = "PGAME_URP/LobbyPlayer/m_lob_playerpbrSkin_lv3";
    public const string HairShader = "==PGAME==/LOBBYPLAYER/m_lob_PlayerPBRHairColor_lv3_URP";
    public const string PlayerShader = "==PGAME==/LobbyPlayer/m_lob_playerpbr_lv3";
    public const string EyeShader = "==PGAME==/LobbyPlayer/m_lob_PlayerPBRColorEye";

    public enum RimPolicy { SafeAdaptation, Disabled, PreserveSource }
    public enum Family { Unsupported, Skin, Hair, Player, Legacy, Bag, Sock, Color, HairFelt, Felt, Fur, Alpha, Eye, HairShadow }

    [Serializable]
    public sealed class ConversionOptions
    {
        public string rootFolder = RootFolder;
        public bool onlyConvertHair;
        public bool onlyLv3Folders = true;
        public bool allowApproximations;
        public bool dryRun = true;
        public bool showProgress = true;
        public RimPolicy rimPolicy = RimPolicy.SafeAdaptation;
        public float maxRimIntensity = 2;
    }

    [Serializable]
    public sealed class ConversionRecord
    {
        public string materialPath, sourcePath, sourceShader, targetShader, status, reason;
        public Family family;
        public bool approximation;
        public List<string> warnings = new List<string>();
        public List<string> ignoredProperties = new List<string>();
    }

    [Serializable]
    public sealed class ConversionReport
    {
        public ConversionOptions options;
        public string reportPath;
        public int converted, previewed, skipped, failed;
        public bool cancelled;
        public List<ConversionRecord> records = new List<ConversionRecord>();
    }

    public sealed class ConversionResult : IDisposable
    {
        public Material material;
        public ConversionRecord record = new ConversionRecord();
        public void Dispose() { if (material != null) Object.DestroyImmediate(material); }
    }

    [SerializeField] private ConversionOptions options = new ConversionOptions();
    private string lastReport;

    [MenuItem("Tools/Clothing/Convert Clothing Materials to URP")]
    public static void ShowWindow()
    {
        GetWindow<ClothingMaterialConverter>("Clothing Materials").Show();
    }

    private void OnGUI()
    {
        if (options == null) options = new ConversionOptions();
        EditorGUILayout.LabelField("Restore clothing materials from original dumps", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Source shader names choose the adapter, not material/folder names. " +
            "Preview first. Apply changes existing .mat assets in place, with backups and Undo. " +
            "No texture imports, models, or presets are changed.", MessageType.Info);
        options.rootFolder = EditorGUILayout.TextField("Material root", options.rootFolder);
        options.onlyLv3Folders = EditorGUILayout.Toggle("Only Lv3 folders", options.onlyLv3Folders);
        options.onlyConvertHair = EditorGUILayout.Toggle("Only hair shader families", options.onlyConvertHair);
        options.allowApproximations = EditorGUILayout.Toggle("Allow approximate adapters", options.allowApproximations);
        options.rimPolicy = (RimPolicy)EditorGUILayout.EnumPopup("Rim lighting policy", options.rimPolicy);
        options.maxRimIntensity = EditorGUILayout.Slider("Safe rim HDR maximum", options.maxRimIntensity, 0, 8);
        if (options.rimPolicy == RimPolicy.PreserveSource)
            EditorGUILayout.HelpBox("Original HDR rim colors can exceed 1024. Missing end thresholds and " +
                "legacy start=10 values are not safe with the recreated shaders.", MessageType.Warning);
        if (GUILayout.Button("Preview conversion (no material changes)"))
        {
            options.dryRun = true;
            lastReport = RunConversion(options).reportPath;
        }
        if (GUILayout.Button("Apply conversion with backups") && EditorUtility.DisplayDialog("Convert clothing materials?",
                "Update matching materials under " + options.rootFolder + "? Unsupported/ambiguous sources are skipped. " +
                "Original material files will be backed up under Library/ClothingMaterialConversion.", "Apply", "Cancel"))
        {
            options.dryRun = false;
            lastReport = RunConversion(options).reportPath;
        }
        if (!string.IsNullOrEmpty(lastReport) && GUILayout.Button("Show last report"))
            EditorUtility.RevealInFinder(lastReport);
    }

    // Compatibility with the former programmatic entry point (explicitly applies).
    public static void ConvertClothingMaterials(bool onlyConvertHair)
    {
        RunConversion(new ConversionOptions { onlyConvertHair = onlyConvertHair, dryRun = false });
    }

    public static ConversionReport RunConversion(ConversionOptions settings)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before converting material assets.");
        settings = settings ?? new ConversionOptions();
        string root = Normalize(settings.rootFolder).TrimEnd('/');
        if (!root.StartsWith("Assets/", StringComparison.Ordinal) || root.Split('/').Contains("..") || !AssetDatabase.IsValidFolder(root))
            throw new ArgumentException("Choose an existing folder under Assets.");
        textureIndex = null;
        textureHashes.Clear();
        string runDirectory = "Library/ClothingMaterialConversion/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(runDirectory);
        var report = new ConversionReport { options = settings, reportPath = runDirectory + "/report.json" };
        var paths = AssetDatabase.FindAssets("t:Material", new[] { root }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) &&
                        (!settings.onlyLv3Folders || Normalize(p).Contains("/Lv3/")))
            .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
        int undoGroup = -1;
        if (!settings.dryRun)
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore clothing materials from source dumps");
        }
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (settings.showProgress && EditorUtility.DisplayCancelableProgressBar("Clothing material conversion", path, (float)i / paths.Length))
                {
                    report.cancelled = true;
                    break;
                }
                var record = new ConversionRecord { materialPath = path };
                report.records.Add(record);
                try
                {
                    var current = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (current == null) throw new InvalidDataException("Cannot load material.");
                    string source = FindSource(path, current.name, root);
                    if (source == null)
                    {
                        record.status = "Skipped"; record.reason = "No matching Materials/<name>.json or .txt source.";
                        report.skipped++;
                        continue;
                    }
                    using (var result = ConvertCopy(current, File.ReadAllText(source), Normalize(Path.GetDirectoryName(Path.GetDirectoryName(source))), settings))
                    {
                        result.record.materialPath = path;
                        result.record.sourcePath = source;
                        report.records[report.records.Count - 1] = record = result.record;
                        if (result.material == null) { report.skipped++; continue; }
                        if (settings.dryRun)
                        {
                            record.status = "Preview";
                            report.previewed++;
                            continue;
                        }
                        // Do not persist unrelated, unsaved material edits as a side effect.
                        if (EditorUtility.IsDirty(current)) throw new InvalidOperationException("Material has unsaved edits; save/review it before applying.");
                        string backup = runDirectory + "/backups/" + AssetDatabase.AssetPathToGUID(path) + ".mat";
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.Copy(path, backup, false);
                        Undo.RecordObject(current, "Restore " + current.name);
                        EditorUtility.CopySerialized(result.material, current);
                        EditorUtility.SetDirty(current);
                        AssetDatabase.SaveAssetIfDirty(current);
                        record.status = "Converted";
                        report.converted++;
                    }
                }
                catch (Exception exception)
                {
                    record.status = "Failed"; record.reason = exception.Message;
                    report.failed++;
                }
            }
        }
        finally
        {
            if (settings.showProgress) EditorUtility.ClearProgressBar();
            if (undoGroup >= 0) Undo.CollapseUndoOperations(undoGroup);
            File.WriteAllText(report.reportPath, JsonConvert.SerializeObject(report, Formatting.Indented, new StringEnumConverter()));
        }
        Debug.Log($"Clothing conversion: {report.previewed} previewed, {report.converted} converted, " +
                  $"{report.skipped} skipped, {report.failed} failed. Report: {report.reportPath}");
        return report;
    }

    /// <summary>Builds a temporary material; never modifies the input or any asset/importer.</summary>
    public static ConversionResult ConvertCopy(Material current, string sourceJson, string modelFolder, ConversionOptions settings = null)
    {
        settings = settings ?? new ConversionOptions();
        if (current == null) throw new ArgumentNullException(nameof(current));
        if (!Finite(settings.maxRimIntensity) || settings.maxRimIntensity < 0)
            throw new ArgumentException("Rim maximum must be finite and nonnegative.");
        SourceMaterial source = SourceMaterial.Parse(sourceJson);
        var result = new ConversionResult();
        var record = result.record;
        record.sourceShader = source.shader;
        record.family = Classify(source.shader);
        string target = TargetShader(record.family);
        record.approximation = IsApproximation(record.family);
        if (settings.onlyConvertHair && record.family != Family.Hair && record.family != Family.HairFelt)
            return Skip(result, "Not a hair shader family (the folder/material name is not authoritative).");
        if (target == null) return Skip(result, "No compatible adapter for this source shader; left unchanged.");
        if (record.approximation && !settings.allowApproximations)
            return Skip(result, "Approximate adapter requires 'Allow approximate adapters'.");
        Shader shader = Shader.Find(target);
        if (shader == null || !shader.isSupported) return Skip(result, "Target shader is unavailable or unsupported: " + target);
        record.targetShader = target;
        if (record.approximation) record.warnings.Add(ApproximationNote(record.family));
        // A fresh material prevents old conversion/preset values from leaking through.
        result.material = new Material(shader) { name = current.name, hideFlags = current.hideFlags,
            enableInstancing = current.enableInstancing, doubleSidedGI = current.doubleSidedGI,
            globalIlluminationFlags = current.globalIlluminationFlags };
        try
        {
            Material material = result.material;
            SetFloat(material, "_Emissive", 0);
            SetFloat(material, "_EmisssionScale", 0);
            foreach (var value in source.numbers)
            {
                if (material.HasProperty(value.Key)) SetFloat(material, value.Key, value.Value);
                else record.ignoredProperties.Add(value.Key);
            }
            foreach (var value in source.colors)
            {
                if (!material.HasProperty(value.Key)) { record.ignoredProperties.Add(value.Key); continue; }
                int index = shader.FindPropertyIndex(value.Key);
                if (shader.GetPropertyType(index) == ShaderPropertyType.Vector) material.SetVector(value.Key, value.Value);
                else material.SetColor(value.Key, value.Value);
            }
            foreach (var entry in source.textures)
            {
                string property = TextureAlias(entry.Key, record.family, source);
                if (property == null || !material.HasProperty(property)) { record.ignoredProperties.Add(entry.Key); continue; }
                var texture = entry.Value;
                material.SetTexture(property, texture.isNull ? null : ResolveTexture(texture.name, modelFolder));
                material.SetTextureScale(property, texture.scale);
                material.SetTextureOffset(property, texture.offset);
            }
            AdaptProperties(material, source, record);
            ConfigureEncoding(material, "_BumpMapNr", "_PackedNormalEncoding", record);
            ConfigureEncoding(material, "_BumpMap2", "_DetailNormalEncoding", record);
            ApplyRimPolicy(material, source, settings, record);
            ConfigureRenderState(material, source, record);
            record.status = "Ready";
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static ConversionResult Skip(ConversionResult result, string reason)
    {
        result.record.status = "Skipped"; result.record.reason = reason;
        return result;
    }

    public static Family Classify(string shaderName)
    {
        // Exact source names: never reinterpret arbitrary FX, glass, or "shadow" names as hair/skin.
        string name = (shaderName ?? "").ToLowerInvariant();
        const string player = "==pgame==/lobbyplayer/";
        if (name == player + "m_lob_playerpbrskin_lv3" || name == player + "m_lob_playerpbrskin_lv1" ||
            name == player + "m_lob_playerpbrskinnoshadow_lv3") return Family.Skin;
        if (name == player + "m_lob_playerpbrhaircolor_lv3") return Family.Hair;
        if (name == player + "m_lob_playerpbr_lv3" || name == player + "m_lob_playerpbr_nooutline_lv3") return Family.Player;
        if (name == "hidden/m_lob_playerpbrlengcy_lv3" || name == "hidden/m_lob_playerpbrlegacy_lv3") return Family.Legacy;
        if (name == player + "m_lob_playerpbrbag_lv3" || name == player + "m_lob_playerpbrbag_lv1") return Family.Bag;
        if (name == player + "m_lob_playerpbrsock_lv3" || name == player + "m_lob_playerpbrsock_lv1") return Family.Sock;
        if (name == player + "训练家变色材质m_lob_playerpbrcolor_lv3") return Family.Color;
        if (name == player + "m_lob_playerpbrhaircolorfelt_lv3") return Family.HairFelt;
        if (name == "hidden/m_lob_playerpbrfelt_lv3") return Family.Felt;
        if (name == player + "m_lob_playerpbr_fur_lv3") return Family.Fur;
        if (name == "hidden/m_lob_playerpbralpha_lv3") return Family.Alpha;
        if (name == player + "m_lob_playerpbrcoloreye") return Family.Eye;
        if (name == "==pgame==/lobby/m_lob_hairshadow") return Family.HairShadow;
        return Family.Unsupported;
    }

    private static string TargetShader(Family family)
    {
        switch (family)
        {
            case Family.Skin: return FaceShader;
            case Family.Hair: case Family.HairFelt: return HairShader;
            case Family.Player: case Family.Legacy: case Family.Bag: case Family.Sock:
            case Family.Color: case Family.Felt: case Family.Fur: case Family.Alpha: return PlayerShader;
            case Family.Eye: return EyeShader;
            case Family.HairShadow: return "Universal Render Pipeline/Unlit";
            default: return null;
        }
    }

    private static bool IsApproximation(Family family)
    {
        return family != Family.Skin && family != Family.Hair && family != Family.Player &&
               family != Family.Eye && family != Family.HairShadow && family != Family.Unsupported;
    }

    private static string ApproximationNote(Family family)
    {
        switch (family)
        {
            case Family.Legacy: case Family.Bag: case Family.Sock:
                return "Packed PBR channels and squared roughness preserved; legacy BRDF/height/outline behavior is approximated.";
            case Family.HairFelt: return "Hair color/highlights retained; felt detail normal/AO/Fresnel are not reproduced.";
            case Family.Felt: return "Base PBR retained; masked felt normal/AO/Fresnel are not reproduced.";
            case Family.Fur: return "Base surface only: fur shells/geometry are not reproduced.";
            case Family.Color: return "Base PBR only: recoloring, hue/inversion and extra color textures are not reproduced.";
            default: return "Base PBR only: original alpha/stencil/ramp behavior is not an exact reconstruction.";
        }
    }

    private static string TextureAlias(string property, Family family, SourceMaterial source)
    {
        if (family == Family.HairShadow) return null;
        bool shoe = family == Family.Sock && source.Number("_SHOEMODE", 0) > 0.5f;
        if (family == Family.Sock)
        {
            if (property.EndsWith("Shoe", StringComparison.Ordinal)) return shoe ? property.Substring(0, property.Length - 4) : null;
            if (shoe && (property == "_MainTex" || property == "_BumpMapNr" || property == "_MixMap")) return null;
        }
        if (property == "_BaseMap") return source.textures.ContainsKey("_MainTex") ? null : "_MainTex";
        if (property == "_BumpMap") return source.textures.ContainsKey("_BumpMapNr") ? null : "_BumpMapNr";
        // Do NOT assign emission maps or recolor masks into the unrelated _MixMap layout.
        return property;
    }

    private static void AdaptProperties(Material material, SourceMaterial source, ConversionRecord record)
    {
        if (material.HasProperty("_AddLightDir") && source.colors.ContainsKey("_AddlightColor") && !source.colors.ContainsKey("_AddLightDir"))
        {
            // Many dumps contain a large HDR color but no runtime-supplied direction.
            // Do not activate it using the recreated shader/preset's arbitrary +Y default.
            material.SetVector("_AddLightDir", Vector4.zero);
            record.warnings.Add("Custom light direction was not saved; boost disabled by zero direction, HDR color retained for manual setup.");
        }
        bool hasMask = Assigned(material, "_MixMap");
        if (!hasMask)
        {
            SetFloat(material, "_Mask", 0); SetFloat(material, "_MaskOn", 0);
            SetFloat(material, "_Emissive", 0); SetFloat(material, "_Emiss", 0);
        }
        if (!Assigned(material, "_BumpMapNr")) SetFloat(material, "_BumpOn", 0);
        if (record.targetShader == PlayerShader)
        {
            bool legacy = record.family != Family.Player && record.family != Family.Color;
            SetFloat(material, "_RoughnessAlphaSquared", legacy ? 1 : 0);
            if (!source.numbers.ContainsKey("_MaskOn")) SetFloat(material, "_MaskOn", hasMask ? 1 : 0);
            if (record.family == Family.Sock && source.Number("_ALPHAMODE", 0) < 0.5f)
                SetFloat(material, "_MaskOn", 0);
            if (!source.numbers.ContainsKey("_Emiss"))
                SetFloat(material, "_Emiss", source.Number("_Emissive", 0) * source.Number("_EmisssionScale", 0));
            if (record.family == Family.Bag) SetFloat(material, "_IsBag", 1);
            if (source.shader.IndexOf("NoOutline", StringComparison.OrdinalIgnoreCase) >= 0) SetFloat(material, "_OutlineEnabled", 0);
        }
        if (source.shader.IndexOf("SkinNoShadow", StringComparison.OrdinalIgnoreCase) >= 0) SetFloat(material, "_ReceiveShadows", 0);
        if (record.family == Family.Hair || record.family == Family.HairFelt)
        {
            // Legacy coefficient 0.004629... maps to the hair shader's multiplier units.
            SetFloat(material, "_OutlineWidth", source.Number("_OutlineWidth", 1f / 216f) * 216f);
        }
        if (record.family == Family.HairShadow)
        {
            material.SetColor("_BaseColor", source.Color("_ShadowColor", Color.black));
            material.SetTexture("_BaseMap", null);
        }
        foreach (string property in new[] { "_MixMap", "_HairMap", "_OutlineMap" })
        {
            var importer = Assigned(material, property) ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture(property))) as TextureImporter : null;
            if (importer != null && importer.sRGBTexture)
                record.warnings.Add(property + " is imported as sRGB; data-channel fidelity needs a linear import (not changed).");
        }
    }

    private static void ConfigureEncoding(Material material, string textureProperty, string encodingProperty, ConversionRecord record)
    {
        if (!material.HasProperty(encodingProperty) || !Assigned(material, textureProperty)) return;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture(textureProperty))) as TextureImporter;
        int encoding = importer != null && importer.textureType == TextureImporterType.NormalMap ? 1 :
                       importer != null && importer.sRGBTexture ? 2 : 0;
        SetFloat(material, encodingProperty, encoding);
        if (encoding == 1)
            record.warnings.Add(textureProperty + ": Unity normal-map compatibility selected; packed alpha may be lost. Importer unchanged.");
    }

    private static void ApplyRimPolicy(Material material, SourceMaterial source, ConversionOptions settings, ConversionRecord record)
    {
        if (!material.HasProperty("_RimlightColor")) return;
        bool hasColor = source.colors.ContainsKey("_RimlightColor");
        Color rim = source.Color("_RimlightColor", Color.black);
        float start = source.Number("_RimlightScale", 1);
        float end = source.Number("_RimlightScale2", 1);
        Vector4 direction = source.Color("_RimLightDir", new Color(0,0,0,0));
        bool enabled = hasColor && Mathf.Max(rim.r, rim.g, rim.b) > 0 &&
                       new Vector3(direction.x, direction.y, direction.z).sqrMagnitude > 0.000001f;
        if (settings.rimPolicy == RimPolicy.Disabled) enabled = false;
        if (settings.rimPolicy == RimPolicy.SafeAdaptation)
        {
            if (settings.maxRimIntensity == 0) enabled = false;
            // The old start=10 sentinel must NOT become a reversed smoothstep against
            // the recreated shader's 1/1.5 default end. Such a ramp illuminates everything.
            if (start < 0 || start >= 1 || end <= start)
            {
                enabled = false;
                if (hasColor) record.warnings.Add("Unsafe/disabled legacy rim ramp suppressed (start=" + start + ", end=" + end + ").");
            }
            rim.r = Mathf.Max(0, rim.r); rim.g = Mathf.Max(0, rim.g); rim.b = Mathf.Max(0, rim.b);
            float maximum = Mathf.Max(rim.r, rim.g, rim.b);
            if (maximum > settings.maxRimIntensity)
            {
                float scale = settings.maxRimIntensity / maximum;
                rim.r *= scale; rim.g *= scale; rim.b *= scale;
                record.warnings.Add("Rim HDR capped AFTER source restoration, preserving hue (original maximum=" + maximum + ").");
            }
            rim.a = Mathf.Clamp01(rim.a);
        }
        if (!enabled) rim = new Color(0,0,0,rim.a);
        SetFloat(material, "_RimEnabled", enabled ? 1 : 0);
        SetFloat(material, "_RimlightScale", enabled ? start : 1);
        SetFloat(material, "_RimlightScale2", enabled ? end : 2);
        material.SetColor("_RimlightColor", rim);
        if (settings.rimPolicy == RimPolicy.PreserveSource && hasColor)
            record.warnings.Add("Unbounded source rim requested; original globals/end threshold may be missing.");
    }

    private static void ConfigureRenderState(Material material, SourceMaterial source, ConversionRecord record)
    {
        int mode = Mathf.RoundToInt(source.Number("_BlendMode", 0));
        // The player's _MaskOn also controls ramp/emission; it is NOT an alpha-test switch.
        bool mask = Assigned(material, "_MixMap") && material.HasProperty("_Mask") && material.GetFloat("_Mask") > 0.5f;
        bool cutout = mode == 5 || source.Number("_AlphaClip", 0) > 0.5f || mask ||
                      source.renderQueue == 2450 || source.Number("_RenderType", 0) == 2450;
        bool transparent = mode >= 1 && mode <= 4 || record.family == Family.Alpha || record.family == Family.HairShadow;
        if (!source.numbers.ContainsKey("_SrcBlendFactor")) SetFloat(material, "_SrcBlendFactor", transparent ? 5 : 1);
        if (!source.numbers.ContainsKey("_DstBlendFactor")) SetFloat(material, "_DstBlendFactor", transparent ? 10 : 0);
        if (!source.numbers.ContainsKey("_ZWrite")) SetFloat(material, "_ZWrite", transparent ? 0 : 1);
        int queue = transparent ? 3000 : cutout ? 2450 : 2000;
        int explicitQueue = source.renderQueue;
        float renderType = source.Number("_RenderType", 0);
        if (explicitQueue < 0 && renderType >= 1000) explicitQueue = Mathf.RoundToInt(renderType);
        if (explicitQueue >= 0) queue = explicitQueue;
        queue += Mathf.RoundToInt(source.Number("_QueueOffset", 0));
        material.renderQueue = Mathf.Clamp(queue, 0, 5000);
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
        SetFloat(material, "_Queue", material.renderQueue);
        SetFloat(material, "_AlphaClip", cutout ? 1 : 0);
        if (material.HasProperty("_AlphaClip")) SetKeyword(material, "_ALPHATEST_ON", cutout);
        if (record.targetShader == PlayerShader) SetFloat(material, "_BlendMode", transparent ? 1 : 0);
        if (record.family == Family.HairShadow)
        {
            SetFloat(material, "_Surface", 1);
            SetFloat(material, "_SrcBlend", source.Number("_SrcBlendFactor", 5));
            SetFloat(material, "_DstBlend", source.Number("_DstBlendFactor", 10));
            SetFloat(material, "_SrcBlendAlpha", source.Number("_SrcBlendFactorAlpha", 5));
            SetFloat(material, "_DstBlendAlpha", source.Number("_DstBlendFactorAlpha", 10));
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
        }
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }
    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
    }
    private static bool Assigned(Material material, string property)
    {
        if (!material.HasProperty(property) || material.GetTexture(property) == null) return false;
        string path = AssetDatabase.GetAssetPath(material.GetTexture(property));
        return path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static string Normalize(string path) => (path ?? "").Replace('\\', '/');

    private static string FindSource(string materialPath, string materialName, string root)
    {
        string directory = Normalize(Path.GetDirectoryName(materialPath));
        while (directory == root || directory.StartsWith(root + "/", StringComparison.Ordinal))
        {
            string sourceDirectory = directory + "/Materials";
            if (Directory.Exists(sourceDirectory))
            {
                var candidates = new[] { materialName, Path.GetFileNameWithoutExtension(materialPath) }.Distinct()
                    .SelectMany(name => new[] { sourceDirectory + "/" + name + ".json", sourceDirectory + "/" + name + ".txt" })
                    .Where(File.Exists).Distinct().ToArray();
                if (candidates.Length > 1) throw new InvalidDataException("Multiple matching source dumps; resolve ambiguity: " + string.Join(", ", candidates));
                if (candidates.Length == 1) return candidates[0];
            }
            directory = Normalize(Path.GetDirectoryName(directory));
        }
        return null;
    }

    private static Dictionary<string, List<string>> textureIndex;
    private static readonly Dictionary<string, string> textureHashes = new Dictionary<string, string>();

    private static Texture ResolveTexture(string name, string modelFolder)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Non-null source texture has no name.");
        string scope = Normalize(modelFolder).TrimEnd('/');
        string[] local = AssetDatabase.FindAssets("t:Texture " + name, new[] { scope }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase))
            .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (local.Length == 1) return AssetDatabase.LoadAssetAtPath<Texture>(local[0]);
        if (local.Length > 1) return ResolveEquivalent(name, local);
        if (textureIndex == null)
        {
            textureIndex = AssetDatabase.FindAssets("t:Texture", new[] { RootFolder }).Select(AssetDatabase.GUIDToAssetPath)
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList(), StringComparer.OrdinalIgnoreCase);
        }
        if (!textureIndex.TryGetValue(name, out var matches) || matches.Count == 0)
            throw new InvalidDataException("Missing source texture '" + name + "' (not replaced with an unrelated search result).");
        return ResolveEquivalent(name, matches.ToArray());
    }

    private static Texture ResolveEquivalent(string name, string[] paths)
    {
        if (paths.Length > 1 && paths.Select(TextureHash).Distinct().Count() != 1)
            throw new InvalidDataException("Ambiguous shared texture '" + name + "': " + string.Join(", ", paths));
        return AssetDatabase.LoadAssetAtPath<Texture>(paths[0]);
    }

    private static string TextureHash(string path)
    {
        if (!textureHashes.TryGetValue(path, out string hash))
        {
            // Include importer configuration: byte-identical PNGs imported differently are not interchangeable.
            using (var sha = SHA256.Create())
            {
                hash = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
                if (File.Exists(path + ".meta"))
                {
                    string metadata = File.ReadAllText(path + ".meta");
                    metadata = System.Text.RegularExpressions.Regex.Replace(metadata, @"(?m)^guid:.*$", "");
                    hash += Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(metadata)));
                }
            }
            textureHashes[path] = hash;
        }
        return hash;
    }

    private sealed class SourceTexture
    {
        public string name;
        public bool isNull;
        public Vector2 scale = Vector2.one, offset = Vector2.zero;
    }

    private sealed class SourceMaterial
    {
        public string shader;
        public int renderQueue = -1;
        public readonly Dictionary<string, float> numbers = new Dictionary<string, float>(StringComparer.Ordinal);
        public readonly Dictionary<string, Color> colors = new Dictionary<string, Color>(StringComparer.Ordinal);
        public readonly Dictionary<string, SourceTexture> textures = new Dictionary<string, SourceTexture>(StringComparer.Ordinal);
        public float Number(string key, float fallback) => numbers.TryGetValue(key, out float value) ? value : fallback;
        public Color Color(string key, Color fallback) => colors.TryGetValue(key, out Color value) ? value : fallback;

        public static SourceMaterial Parse(string json)
        {
            var root = JObject.Parse(json);
            var source = new SourceMaterial { shader = (string)root["m_Shader"]?["Name"] };
            if (string.IsNullOrEmpty(source.shader) || (bool?)root["m_Shader"]?["IsNull"] == true)
                throw new InvalidDataException("Missing original shader name; cannot infer it from already-converted material names.");
            source.renderQueue = (int?)root["m_CustomRenderQueue"] ?? (int?)root["m_RenderQueue"] ?? -1;
            var saved = root["m_SavedProperties"] as JObject;
            if (saved == null) throw new InvalidDataException("Missing m_SavedProperties in source JSON/text dump.");
            foreach (string category in new[] { "m_Floats", "m_Ints" })
                foreach (var entry in Entries(saved[category])) source.numbers[Key(entry.Key)] = Scalar(entry.Value);
            foreach (var entry in Entries(saved["m_Colors"]))
            {
                JToken value = entry.Value;
                source.colors[Key(entry.Key)] = new Color(Component(value,"r","x",0), Component(value,"g","y",0),
                    Component(value,"b","z",0), Component(value,"a","w",1));
            }
            foreach (var entry in Entries(saved["m_TexEnvs"]))
            {
                var value = entry.Value;
                var reference = value["m_Texture"];
                source.textures[Key(entry.Key)] = new SourceTexture
                {
                    name = (string)reference?["Name"],
                    isNull = reference == null || reference.Type == JTokenType.Null || (bool?)reference["IsNull"] == true,
                    scale = new Vector2(Component(value["m_Scale"],"X","x",1), Component(value["m_Scale"],"Y","y",1)),
                    offset = new Vector2(Component(value["m_Offset"],"X","x",0), Component(value["m_Offset"],"Y","y",0))
                };
            }
            return source;
        }

        private static string Key(string name) => name.StartsWith("_", StringComparison.Ordinal) ? name : "_" + name;
        private static float Scalar(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) throw new InvalidDataException("Null numeric source property.");
            // JValue.ToString() follows the editor's locale (e.g. 0,05 in Italian).
            // Numeric JSON tokens are already parsed: do not round-trip through text.
            float number = value.Type == JTokenType.Integer || value.Type == JTokenType.Float || value.Type == JTokenType.Boolean
                ? Convert.ToSingle(((JValue)value).Value, CultureInfo.InvariantCulture)
                : float.Parse(value.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture);
            if (!Finite(number)) throw new InvalidDataException("Non-finite source property value.");
            return number;
        }
        private static float Component(JToken value, string key, string alternate, float fallback)
        {
            JToken component = value?[key] ?? value?[alternate];
            return component == null ? fallback : Scalar(component);
        }
        private static IEnumerable<KeyValuePair<string, JToken>> Entries(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) yield break;
            if (token is JObject map)
            {
                foreach (var property in map.Properties()) yield return new KeyValuePair<string,JToken>(property.Name,property.Value);
                yield break;
            }
            if (token is JArray array)
            {
                foreach (var entry in array)
                {
                    string key = (string)(entry["Key"] ?? entry["first"]);
                    if (key != null) yield return new KeyValuePair<string,JToken>(key,entry["Value"] ?? entry["second"]);
                    else if (entry is JObject single && single.Count == 1)
                    {
                        var property = single.Properties().Single();
                        yield return new KeyValuePair<string,JToken>(property.Name,property.Value);
                    }
                    else throw new InvalidDataException("Unrecognized saved-property entry.");
                }
                yield break;
            }
            throw new InvalidDataException("Saved properties must be an object or key/value array.");
        }
    }

    // Explicit extraction only. Conversion never extracts/remaps models or generates duplicate .mat assets.
    public static void ExtractMaterials(string assetPath, string destinationPath)
    {
        if (!AssetDatabase.IsValidFolder(destinationPath)) throw new ArgumentException("Create the destination asset folder first.");
        foreach (Material material in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Material>())
        {
            string path = Normalize(destinationPath) + "/" + material.name + ".mat";
            if (File.Exists(path)) continue;
            string error = AssetDatabase.ExtractAsset(material, path);
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning(error);
        }
    }
}

// Historical name mentioned by older editor scripts.
public static class ClothingMaterialUpdater
{
    public static void ConvertClothingMaterials(bool onlyConvertHair = false) => ClothingMaterialConverter.ConvertClothingMaterials(onlyConvertHair);
}
