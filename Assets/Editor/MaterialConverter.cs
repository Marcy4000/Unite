using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class MaterialConverter : MonoBehaviour
{
    [System.Serializable]
    public class JsonMaterial
    {
        public ShaderData m_Shader;
        public SavedProperties m_SavedProperties;
        public string m_Name;

        [System.Serializable]
        public class ShaderData
        {
            public string Name;
        }

        [System.Serializable]
        public class SavedProperties
        {
            public Dictionary<string, TexEnv> m_TexEnvs;
            public Dictionary<string, float> m_Floats;
            public Dictionary<string, Color> m_Colors;

            [System.Serializable]
            public class TexEnv
            {
                public TextureData m_Texture;
                public Vector2 m_Scale;
                public Vector2 m_Offset;

                [System.Serializable]
                public class TextureData
                {
                    public string Name;
                }
            }
        }
    }

    [MenuItem("Tools/Convert JSON Materials in Folder")]
    public static void ConvertJsonMaterialsInFolder()
    {
        // Define input, output, and texture folders (edit these paths as needed)
        string inputFolder = "Assets/Model/Map/PsyduckRacing/pref_sce_EBeach_PsyDuck_01_lv3/Materials/json/";
        string outputFolder = "Assets/Model/Map/PsyduckRacing/pref_sce_EBeach_PsyDuck_01_lv3/Materials/json/converted";
        string textureFolder = "Assets/Model/Map/PsyduckRacing/pref_sce_EBeach_PsyDuck_01_lv3/";

        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string[] folders = outputFolder.Split('/');
            string currentPath = "";
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(currentPath))
                {
                    currentPath = folder;
                }
                else
                {
                    string newFolder = $"{currentPath}/{folder}";
                    if (!AssetDatabase.IsValidFolder(newFolder))
                    {
                        AssetDatabase.CreateFolder(currentPath, folder);
                    }
                    currentPath = newFolder;
                }
            }
        }

        // Get all JSON files in the input folder
        string[] jsonFiles = Directory.GetFiles(inputFolder, "*.json");
        foreach (string jsonFile in jsonFiles)
        {
            // Read JSON
            string json = File.ReadAllText(jsonFile);

            // Parse JSON
            JsonMaterial jsonMaterial = JsonConvert.DeserializeObject<JsonMaterial>(json);

            // Create a new URP material
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = jsonMaterial.m_Name;

            // Set Textures
            foreach (var texEnv in jsonMaterial.m_SavedProperties.m_TexEnvs)
            {
                if (!string.IsNullOrEmpty(texEnv.Value.m_Texture.Name))
                {
                    // Search for the texture in the texture folder
                    string texturePath = Path.Combine(textureFolder, texEnv.Value.m_Texture.Name + ".png");
                    Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);

                    if (texture != null)
                    {
                        material.SetTexture(texEnv.Key, texture);
                        material.SetTextureScale(texEnv.Key, texEnv.Value.m_Scale);
                        material.SetTextureOffset(texEnv.Key, texEnv.Value.m_Offset);
                    }
                    else
                    {
                        Debug.LogWarning($"Texture {texEnv.Value.m_Texture.Name} not found in {textureFolder}");
                    }
                }
            }

            // Set Floats
            foreach (var floatProp in jsonMaterial.m_SavedProperties.m_Floats)
            {
                material.SetFloat(floatProp.Key, floatProp.Value);
            }

            // Set Colors
            foreach (var colorProp in jsonMaterial.m_SavedProperties.m_Colors)
            {
                material.SetColor(colorProp.Key, colorProp.Value);
            }

            // Save Material
            string materialPath = Path.Combine(outputFolder, $"{jsonMaterial.m_Name}.mat");
            AssetDatabase.CreateAsset(material, materialPath);
            Debug.Log($"Material {jsonMaterial.m_Name} created and saved at {materialPath}.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("All JSON files have been converted to materials.");
    }
}
