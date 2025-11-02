using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Renamed the class for clarity
public class ClothingMaterialConverter : EditorWindow
{
    private const string RootFolder = "Assets/Model/Clothing";
    private const string LodLevel = "Lv3";

    // You must create a preset asset for this to work!
    private const string PresetSearchString = "t:ShaderConversionPreset";

    // Renamed the menu item
    [MenuItem("Tools/Clothing/Convert Clothing Materials to URP")]
    public static void ConvertClothingMaterials()
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

        string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[0]);
        ShaderConversionPreset preset = AssetDatabase.LoadAssetAtPath<ShaderConversionPreset>(presetPath);

        if (preset.targetShader == null)
        {
            Debug.LogError("The Target Shader in the preset is not set!");
            return;
        }
        // -----------------------------

        string[] genderFolders = { "Male", "Female" };

        foreach (var genderFolder in genderFolders)
        {
            string genderPath = Path.Combine(RootFolder, genderFolder);
            if (!Directory.Exists(genderPath)) continue;

            foreach (var folderMapping in folderMappings)
            {
                string typeFolderPath = Path.Combine(genderPath, folderMapping.Key, LodLevel);
                if (!Directory.Exists(typeFolderPath)) continue;

                ProcessModelsInFolder(typeFolderPath, preset);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Material conversion for URP completed.");
    }

    private static void ProcessModelsInFolder(string folderPath, ShaderConversionPreset preset)
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
                    ConvertMaterialProperties(material, preset);
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
    private static void ConvertMaterialProperties(Material material, ShaderConversionPreset preset)
    {
        Debug.Log($"Converting Material: {material.name}");

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
        material.SetFloat("_MainlightAttenuation", preset.mainlightAttenuation);
        material.SetFloat("_AtVector", (float)preset.attenuationVector);

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
        material.SetColor("_RimlightColor", preset.rimlightColor);
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

        // 4. Re-apply the stored textures and properties to the new shader slots
        if (oldMainTex != null) material.SetTexture("_MainTex", oldMainTex);
        if (oldBumpMap != null) material.SetTexture("_BumpMapNr", oldBumpMap);

        // This is an assumption: We are putting the old emission map into the Green channel of the new _MixMap
        // This requires texture manipulation not possible here. A better approach is to assign it if the shader is simple.
        // For your complex shader, _MixMap is used for multiple things. We will assign the emission map here
        // as a placeholder. You may need a more advanced script to pack textures if that's your goal.
        if (oldEmissionMap != null) material.SetTexture("_MixMap", oldEmissionMap);

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