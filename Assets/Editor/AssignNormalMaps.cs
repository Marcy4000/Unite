using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

// Renamed the class for clarity
public class ClothingMaterialConverter : EditorWindow
{
    private const string RootFolder = "Assets/Model/Clothing";
    private const string LodLevel = "Lv3";

    // You must create a preset asset for this to work!
    private const string PresetSearchString = "t:ShaderConversionPreset";

    // Menu item now opens an EditorWindow so the user can choose options
    [MenuItem("Tools/Clothing/Convert Clothing Materials to URP")]
    public static void ShowWindow()
    {
        var window = GetWindow<ClothingMaterialConverter>();
        window.titleContent = new GUIContent("Clothing Material Converter");
        window.Show();
    }

    // Instance field exposed in the EditorWindow to let the user toggle hair-only conversion
    private bool onlyConvertHair = false;

    // Draw the simple UI so users can toggle hair-only conversion and run the conversion
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Clothing Material Conversion", EditorStyles.boldLabel);
        onlyConvertHair = EditorGUILayout.Toggle("Only convert hair materials", onlyConvertHair);

        EditorGUILayout.Space();
        if (GUILayout.Button("Convert Now"))
        {
            // Run conversion with the selected option
            ConvertClothingMaterials(onlyConvertHair);
            Debug.Log($"Conversion finished. onlyConvertHair={onlyConvertHair}");
        }
    }

    // Keep a static converter entry point which accepts the option
    public static void ConvertClothingMaterials(bool onlyConvertHair)
    {
        // --- Find the Preset Asset ---
        string[] presetGuids = AssetDatabase.FindAssets(PresetSearchString);
        if (presetGuids.Length == 0)
        {
            Debug.LogError("Shader Conversion Preset not found! Please create one via Assets > Create > Clothing > Shader Conversion Preset.");
            return;
        }
        if (presetGuids.Length > 1)
        {
            Debug.LogWarning("Multiple Shader Conversion Presets found. Using the first one.");
        }

        // Try to find presets by filename/path match. If not found, fall back to the first preset found.
        string facePresetSearchName = "faceshaderpreset";
        string hairPresetSearchName = "hairshaderpreset";

        ShaderConversionPreset facePreset = null;
        ShaderConversionPreset hairPreset = null;

        foreach (var g in presetGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string pLower = p.ToLowerInvariant();
            string fileNameNoExt = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

            if (facePreset == null && (pLower.Contains(facePresetSearchName) || fileNameNoExt.Contains(facePresetSearchName)))
            {
            facePreset = AssetDatabase.LoadAssetAtPath<ShaderConversionPreset>(p);
            }

            if (hairPreset == null && (pLower.Contains(hairPresetSearchName) || fileNameNoExt.Contains(hairPresetSearchName)))
            {
            hairPreset = AssetDatabase.LoadAssetAtPath<ShaderConversionPreset>(p);
            }

            if (facePreset != null && hairPreset != null)
            break;
        }

        // Fallback: if no named face preset found, use the first preset found
        if (facePreset == null && presetGuids.Length > 0)
        {
            string facePresetPath = AssetDatabase.GUIDToAssetPath(presetGuids[0]);
            facePreset = AssetDatabase.LoadAssetAtPath<ShaderConversionPreset>(facePresetPath);
            Debug.LogWarning("Face shader preset not found by name; using the first Shader Conversion Preset found.");
        }

        if (facePreset == null)
        {
            Debug.LogError("Shader Conversion Preset not found! Please create one via Assets > Create > Clothing > Shader Conversion Preset.");
            return;
        }
        if (facePreset.targetShader == null)
        {
            Debug.LogError("The Target Shader in the face preset is not set!");
            return;
        }

        if (hairPreset != null && hairPreset.targetShader == null)
        {
            Debug.LogWarning("Hair preset found but its Target Shader is not set. Hair preset will be ignored.");
            hairPreset = null;
        }
        // -----------------------------

        string[] genderFolders = { "Male", "Female" };

        foreach (var genderFolder in genderFolders)
        {
            string genderPath = Path.Combine(RootFolder, genderFolder);
            if (!Directory.Exists(genderPath)) continue;

            foreach (var folderMapping in folderMappings)
            {
                // If user requested hair-only conversion, skip other folders
                if (onlyConvertHair && !string.Equals(folderMapping.Key, "Hair", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string typeFolderPath = Path.Combine(genderPath, folderMapping.Key, LodLevel);
                if (!Directory.Exists(typeFolderPath)) continue;

                ProcessModelsInFolder(typeFolderPath, facePreset, hairPreset, onlyConvertHair);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Material conversion for URP completed.");
    }

    private static void ProcessModelsInFolder(string folderPath, ShaderConversionPreset facePreset, ShaderConversionPreset hairPreset, bool onlyConvertHair)
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });

        foreach (var guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            string destinationPath = Path.Combine(Path.GetDirectoryName(modelPath), "ExtractedMaterials");

            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            ExtractMaterials(modelPath, destinationPath);

            // After extraction, we need to find the materials again from their new path
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { destinationPath });
            foreach (var matGuid in materialGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(matGuid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                    if (material != null)
                    {
                        // Decide which preset to use based on material name.
                        string matNameLower = (material.name ?? string.Empty).ToLower();
                        bool isHair = matNameLower.Contains("hair") || matNameLower.Contains("shadow");
                        // If user requested hair-only conversion and this material is not hair, skip it
                        if (onlyConvertHair && !isHair)
                        {
                            continue;
                        }
                        ShaderConversionPreset selected = isHair && hairPreset != null ? hairPreset : facePreset;
                        ConvertMaterialProperties(material, selected, isHair, matPath);
                    }
                }
            }
        }

    public static void ExtractMaterials(string assetPath, string destinationPath)
    {
        // This extraction logic is fine, no changes needed here.
        HashSet<string> hashSet = new HashSet<string>();
        IEnumerable<Object> enumerable = from x in AssetDatabase.LoadAllAssetsAtPath(assetPath)
                                         where x.GetType() == typeof(Material)
                                         select x;
        foreach (Object item in enumerable)
        {
            string path = Path.Combine(destinationPath, item.name) + ".mat";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            string value = AssetDatabase.ExtractAsset(item, path);
            if (string.IsNullOrEmpty(value))
            {
                hashSet.Add(assetPath);
            }
        }

        foreach (string item2 in hashSet)
        {
            AssetDatabase.WriteImportSettingsIfDirty(item2);
            AssetDatabase.ImportAsset(item2, ImportAssetOptions.ForceUpdate);
        }
    }

    // This is the core new function that handles the conversion.
    private static void ConvertMaterialProperties(Material material, ShaderConversionPreset preset, bool isHair = false, string materialAssetPath = null)
    {
        Debug.Log($"Converting Material: {material.name} (isHair: {isHair})");

        // 1. Store textures and key properties from the old shader
        // We check for both Standard and URP Lit property names
        Texture oldMainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        if (oldMainTex == null && material.HasProperty("_BaseMap")) oldMainTex = material.GetTexture("_BaseMap");

        Texture oldBumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
        Texture oldEmissionMap = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;
        float oldCutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;

        // 2. Change the shader to the one specified in the preset
        material.shader = preset.targetShader;

        // 3. Apply all values from the preset
        material.SetColor("_colorSkin", preset.colorSkin);
        material.SetFloat("_ShadowOffset", preset.shadowOffset);
        material.SetFloat("_ShadowPow", preset.shadowPow);
        material.SetFloat("_ShadowScale", preset.shadowScale);
        material.SetFloat("_RoughNessOffset", preset.roughNessOffset);
        material.SetColor("_SpecularColor", preset.specularColor);
        material.SetFloat("_MetallicOffset", preset.metallicOffset);
        material.SetFloat("_NormalScale", preset.normalScale);
        material.SetFloat("_MainlightAttenuation", 10f);
        material.SetFloat("_AtVector", (float)preset.attenuationVector);
        material.SetFloat("_UseObjectSpace", 1f);

        material.SetFloat("_SHType", (float)preset.shType);
        material.SetFloat("_SHScale", preset.shScale);
        material.SetColor("_SHTopColor", preset.shTopColor);
        material.SetColor("_SHBotColor", preset.shBotColor);
        material.SetColor("_SHColorScale", preset.shColorScale);

        material.SetColor("_OutlineColor", preset.outlineColor);
        material.SetFloat("_OutlineWidth", preset.outlineWidth);
        material.SetFloat("_Offset", preset.zOffset);
        material.SetVector("_lightDir", preset.lightDirection);

        material.SetFloat("_Emissive", preset.emissiveFactor);
        material.SetFloat("_EmisssionScale", preset.emissionScale);
        material.SetFloat("_Cutoff", preset.alphaCutoff); // We'll overwrite this with the old value later if it exists

        material.SetVector("_RimLightDir", preset.rimLightDirection);
        material.SetFloat("_RimlightScale", preset.rimlightScale);
        material.SetFloat("_RimlightScale2", preset.rimlightScale2);
        material.SetFloat("_RimlightShadowScale", preset.rimlightShadowScale);
        // Cap HDR intensity so the brightest RGB channel does not exceed 2
        Color rim = preset.rimlightColor;
        float maxChannel = Mathf.Max(rim.r, Mathf.Max(rim.g, rim.b));
        if (maxChannel > 2f)
        {
            float scale = 2f / maxChannel;
            rim.r *= scale;
            rim.g *= scale;
            rim.b *= scale;
        }
        material.SetColor("_RimlightColor", rim);
        material.SetFloat("_RimlightAttenuation", preset.rimlightAttenuation);

        material.SetVector("_AddLightDir", preset.addLightDirection);
        material.SetColor("_AddlightColor", preset.addlightColor);
        material.SetFloat("_AddlightLerp", preset.addlightLerp);
        material.SetFloat("_AddlightAttenuation", preset.addlightAttenuation);

        // Apply Render States
        material.renderQueue = (int)preset.renderQueue;
        material.SetInt("_Cull", (int)preset.cullMode);
        material.SetInt("_ZWrite", preset.zWrite ? 1 : 0);
        material.SetInt("_ZTest", (int)preset.zTest);
        material.SetInt("_SrcBlendFactor", (int)preset.srcBlend);
        material.SetInt("_DstBlendFactor", (int)preset.dstBlend);
        material.SetInt("_BlendOp", (int)preset.blendOp);

        // Apply keywords based on preset bools/enums
        SetKeyword(material, "_FACESHDW_SCALE_ON", preset.enableFaceShadowScale);
        if (preset.enableFaceShadowScale) material.SetFloat("_faceshadowScale", preset.faceShadowScaleValue);

        SetKeyword(material, "_VTEX_ON", preset.useVTex);
        SetKeyword(material, "_VCOLOR2N_ON", preset.useVColor2N);
        // ... add more keywords as needed ...

        // Hair-specific properties (apply when material identified as hair and preset contains hair fields)
        if (isHair)
        {
            // Safe-guard: only set if property exists on the material
            if (material.HasProperty("_color1")) material.SetColor("_color1", preset.color1);
            if (material.HasProperty("_color2")) material.SetColor("_color2", preset.color2);
            if (material.HasProperty("_color3")) material.SetColor("_color3", preset.color3);
            if (material.HasProperty("_specularColor1")) material.SetColor("_specularColor1", preset.specularColor1);
            if (material.HasProperty("_specularColor2")) material.SetColor("_specularColor2", preset.specularColor2);
            if (material.HasProperty("_glossiness_1X")) material.SetFloat("_glossiness_1X", preset.glossiness_1X);
            if (material.HasProperty("_glossiness_1Y")) material.SetFloat("_glossiness_1Y", preset.glossiness_1Y);
            if (material.HasProperty("_glossiness_2X")) material.SetFloat("_glossiness_2X", preset.glossiness_2X);
            if (material.HasProperty("_glossiness_2Y")) material.SetFloat("_glossiness_2Y", preset.glossiness_2Y);
        }

        // Apply JSON overrides if a JSON exists for this material (overrides preset defaults)
        if (!string.IsNullOrEmpty(materialAssetPath))
        {
            ApplyJsonOverrides(materialAssetPath, material);
        }

        // 4. Re-apply the stored textures and properties to the new shader slots (only if not set by JSON)
        if (oldMainTex != null && material.GetTexture("_MainTex") == null) material.SetTexture("_MainTex", oldMainTex);
        if (oldBumpMap != null && material.GetTexture("_BumpMapNr") == null) material.SetTexture("_BumpMapNr", oldBumpMap);

        // This is an assumption: We are putting the old emission map into the Green channel of the new _MixMap
        // This requires texture manipulation not possible here. A better approach is to assign it if the shader is simple.
        // For your complex shader, _MixMap is used for multiple things. We will assign the emission map here
        // as a placeholder. You may need a more advanced script to pack textures if that's your goal.
        if (oldEmissionMap != null && material.GetTexture("_MixMap") == null) material.SetTexture("_MixMap", oldEmissionMap);

        material.SetFloat("_Cutoff", oldCutoff); // Use the old cutoff value

        EditorUtility.SetDirty(material);
    }

    // Helper to enable/disable shader keywords
    private static void SetKeyword(Material mat, string keyword, bool enabled)
    {
        if (enabled)
        {
            mat.EnableKeyword(keyword);
        }
        else
        {
            mat.DisableKeyword(keyword);
        }
    }

    private static void ApplyJsonOverrides(string materialAssetPath, Material mat)
    {
        try
        {
            string dir = Path.GetDirectoryName(materialAssetPath);
            if (string.IsNullOrEmpty(dir)) return;
            string parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent)) return;
            string jsonDir = Path.Combine(parent, "Materials");
            string jsonFile = Path.Combine(jsonDir, mat.name + ".json");
            if (!File.Exists(jsonFile)) return;

            string jsonText = File.ReadAllText(jsonFile);
            JObject root = JObject.Parse(jsonText);
            var saved = root["m_SavedProperties"];
            if (saved == null) return;

            // Textures
            var texEnvs = saved["m_TexEnvs"] as JObject;
            if (texEnvs != null)
            {
                foreach (var prop in texEnvs.Properties())
                {
                    string propName = prop.Name;
                    var texToken = prop.Value["m_Texture"]?["Name"];
                    if (texToken != null)
                    {
                        string texName = texToken.Value<string>();
                        if (!string.IsNullOrEmpty(texName))
                        {
                            // Try to find texture asset by name
                            Texture found = null;
                            string[] guids = AssetDatabase.FindAssets(texName);
                            foreach (var g in guids)
                            {
                                string p = AssetDatabase.GUIDToAssetPath(g);
                                var t = AssetDatabase.LoadAssetAtPath<Texture>(p);
                                if (t != null && System.IO.Path.GetFileNameWithoutExtension(p).Equals(texName, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    found = t;
                                    break;
                                }
                            }
                            if (found == null && guids.Length > 0)
                            {
                                found = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(guids[0]));
                            }

                            if (found != null && mat.HasProperty(propName))
                            {
                                mat.SetTexture(propName, found);
                            }
                        }
                    }

                    // Scale / Offset
                    var scaleToken = prop.Value["m_Scale"];
                    if (scaleToken != null)
                    {
                        float sx = scaleToken["X"]?.Value<float>() ?? 1f;
                        float sy = scaleToken["Y"]?.Value<float>() ?? 1f;
                        if (mat.HasProperty(propName)) mat.SetTextureScale(propName, new Vector2(sx, sy));
                    }
                    var offsetToken = prop.Value["m_Offset"];
                    if (offsetToken != null)
                    {
                        float ox = offsetToken["X"]?.Value<float>() ?? 0f;
                        float oy = offsetToken["Y"]?.Value<float>() ?? 0f;
                        if (mat.HasProperty(propName)) mat.SetTextureOffset(propName, new Vector2(ox, oy));
                    }
                }
            }

            // Floats
            var floats = saved["m_Floats"] as JObject;
            if (floats != null)
            {
                foreach (var f in floats.Properties())
                {
                    string key = f.Name;
                    float val = f.Value.Value<float>();
                    // Try both exact key and underscore-prefixed variant if needed
                    if (mat.HasProperty(key))
                    {
                        mat.SetFloat(key, val);
                    }
                    else if (!key.StartsWith("_") && mat.HasProperty("_" + key))
                    {
                        mat.SetFloat("_" + key, val);
                    }
                }
            }

            // Colors
            var colors = saved["m_Colors"] as JObject;
            if (colors != null)
            {
                foreach (var c in colors.Properties())
                {
                    string key = c.Name;
                    var tok = c.Value;
                    if (tok != null && tok["r"] != null)
                    {
                        float r = tok["r"].Value<float>();
                        float g = tok["g"].Value<float>();
                        float b = tok["b"].Value<float>();
                        float a = tok["a"]?.Value<float>() ?? 1f;
                        Color col = new Color(r, g, b, a);
                        if (mat.HasProperty(key))
                        {
                            mat.SetColor(key, col);
                        }
                        else if (!key.StartsWith("_") && mat.HasProperty("_" + key))
                        {
                            mat.SetColor("_" + key, col);
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"ApplyJsonOverrides failed for {materialAssetPath}: {ex.Message}");
        }
    }

    // Folder mapping (unchanged)
    private static readonly Dictionary<string, (ClothingType type, string outputFolder)> folderMappings = new Dictionary<string, (ClothingType, string)>
    {
        { "Hair", (ClothingType.Hair, "Hair") },
        { "Outwear", (ClothingType.Overwear, "Overwear") },
        { "Bottom", (ClothingType.Pants, "Pants") },
        { "Hand", (ClothingType.Gloves, "Gloves") },
        { "Sock", (ClothingType.Socks, "Socks") },
        { "Eye", (ClothingType.Eyes, "Eyes") },
        { "Hat", (ClothingType.Hat, "Hats") },
        { "Top", (ClothingType.Shirt, "Shirt") },
        { "Foot", (ClothingType.Shoes, "Shoes") },
        { "Head", (ClothingType.Face, "Face") },
        { "Backpack", (ClothingType.Backpack, "Backpack") }
    };
}
