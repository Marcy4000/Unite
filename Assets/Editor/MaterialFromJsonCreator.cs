using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json; // Richiede il package com.unity.nuget.newtonsoft-json


public class MaterialFromJsonCreator
{
    // Definizioni delle classi per mappare la struttura JSON
    [System.Serializable]
    public class ShaderInfo
    {
        public string Name;
        public bool IsNull;
    }

    [System.Serializable]
    public class JsonMaterialData
    {
        public ShaderInfo m_Shader; // Modificato per usare ShaderInfo
        public SavedProperties m_SavedProperties;
        public string m_Name;
    }

    [System.Serializable]
    public class SavedProperties
    {
        public Dictionary<string, TexEnv> m_TexEnvs;
        public Dictionary<string, float> m_Floats;
        public Dictionary<string, JsonColor> m_Colors;
    }

    [System.Serializable]
    public class TexEnv
    {
        public TextureInfo m_Texture;
        public JsonVector2 m_Scale;
        public JsonVector2 m_Offset;
    }

    [System.Serializable]
    public class TextureInfo
    {
        public string Name; // Usato per trovare l'asset
        public bool IsNull;
    }

    [System.Serializable]
    public class JsonVector2
    {
        public float X;
        public float Y;
    }

    [System.Serializable]
    public class JsonColor
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }

    private const string JsonFolderPath = "Assets/Test/fun/Material";
    private const string TextureBaseFolderPath = "Assets/Test/fun/Texture2D"; // Cartella base per le texture
    private const string OutputMaterialFolderPath = "Assets/Test/fun/Materials_Generated_From_Json";
    private const string FallbackShaderName = "UI/UI3D_goldcard"; 

    [MenuItem("Tools/Create Materials from JSONs")]
    public static void CreateMaterials()
    {
        if (!Directory.Exists(JsonFolderPath))
        {
            Debug.LogError($"Cartella JSON non trovata: {JsonFolderPath}");
            return;
        }

        // Shader shader = Shader.Find(ShaderName); // Rimossa riga
        // if (shader == null) // Rimossa riga
        // { // Rimossa riga
        // Debug.LogError($"Shader non trovato: {ShaderName}. Assicurati che sia nel progetto e non abbia errori di compilazione."); // Rimossa riga
        // return; // Rimossa riga
        // } // Rimossa riga

        if (!Directory.Exists(OutputMaterialFolderPath))
        {
            Directory.CreateDirectory(OutputMaterialFolderPath);
            AssetDatabase.Refresh();
        }

        string[] jsonFiles = Directory.GetFiles(JsonFolderPath, "*.json");

        foreach (string filePath in jsonFiles)
        {
            string jsonContent = File.ReadAllText(filePath);
            JsonMaterialData materialData = null;
            try
            {
                materialData = JsonConvert.DeserializeObject<JsonMaterialData>(jsonContent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Errore durante la deserializzazione del JSON {Path.GetFileName(filePath)}: {e.Message}");
                continue;
            }


            if (materialData == null || string.IsNullOrEmpty(materialData.m_Name))
            {
                Debug.LogWarning($"Dati del materiale o nome mancanti nel file JSON: {Path.GetFileName(filePath)}");
                continue;
            }

            // Usa fallback shader se m_Shader è null, IsNull true, o Name vuoto
            string shaderNameFromJson = FallbackShaderName;
            if (materialData.m_Shader != null && !materialData.m_Shader.IsNull && !string.IsNullOrEmpty(materialData.m_Shader.Name))
            {
                shaderNameFromJson = materialData.m_Shader.Name;
            }
            else
            {
                Debug.LogWarning($"Informazioni shader mancanti o non valide nel file JSON: {Path.GetFileName(filePath)}. Verrà usato lo shader di fallback '{FallbackShaderName}'.");
            }
            
            Shader shader = Shader.Find(shaderNameFromJson);
            if (shader == null)
            {
                Debug.LogError($"Shader '{shaderNameFromJson}' specificato nel JSON '{Path.GetFileName(filePath)}' non trovato. Assicurati che sia nel progetto e non abbia errori di compilazione.");
                continue;
            }

            string materialName = materialData.m_Name;
            string materialPath = Path.Combine(OutputMaterialFolderPath, $"{materialName}.mat");

            Material material = new Material(shader);

            // Imposta Texture
            if (materialData.m_SavedProperties.m_TexEnvs != null)
            {
                foreach (var texEntry in materialData.m_SavedProperties.m_TexEnvs)
                {
                    if (texEntry.Value.m_Texture == null || texEntry.Value.m_Texture.IsNull || string.IsNullOrEmpty(texEntry.Value.m_Texture.Name))
                        continue;

                    Texture2D texture = FindTexture(texEntry.Value.m_Texture.Name);
                    if (texture != null)
                    {
                        material.SetTexture(texEntry.Key, texture);
                        if (texEntry.Value.m_Scale != null)
                            material.SetTextureScale(texEntry.Key, new Vector2(texEntry.Value.m_Scale.X, texEntry.Value.m_Scale.Y));
                        if (texEntry.Value.m_Offset != null)
                            material.SetTextureOffset(texEntry.Key, new Vector2(texEntry.Value.m_Offset.X, texEntry.Value.m_Offset.Y));
                    }
                    else
                    {
                        Debug.LogWarning($"Texture '{texEntry.Value.m_Texture.Name}' non trovata per la proprietà '{texEntry.Key}' nel materiale '{materialName}'.");
                    }
                }
            }

            // Imposta Float e Keywords
            if (materialData.m_SavedProperties.m_Floats != null)
            {
                foreach (var floatEntry in materialData.m_SavedProperties.m_Floats)
                {
                    material.SetFloat(floatEntry.Key, floatEntry.Value);

                    // Gestione Keywords
                    if (floatEntry.Key == "_MODEL2")
                    {
                        if (Mathf.Approximately(floatEntry.Value, 1.0f)) // ADD
                            material.EnableKeyword("_MODEL2_ADD");
                        else // BLEND (o qualsiasi altro valore)
                            material.DisableKeyword("_MODEL2_ADD");
                    }
                    else if (floatEntry.Key == "_MODEL3")
                    {
                        if (Mathf.Approximately(floatEntry.Value, 1.0f)) // ADD
                            material.EnableKeyword("_MODEL3_ADD");
                        else // BLEND
                            material.DisableKeyword("_MODEL3_ADD");
                    }
                    else if (floatEntry.Key == "_UseUIAlphaClip")
                    {
                        if (floatEntry.Value > 0.5f) // Tipicamente 1.0 per abilitato
                            material.EnableKeyword("UNITY_UI_ALPHACLIP");
                        else
                            material.DisableKeyword("UNITY_UI_ALPHACLIP");
                    }
                }
            }

            // Imposta Color/Vector
            if (materialData.m_SavedProperties.m_Colors != null)
            {
                foreach (var colorEntry in materialData.m_SavedProperties.m_Colors)
                {
                    Color color = new Color(colorEntry.Value.r, colorEntry.Value.g, colorEntry.Value.b, colorEntry.Value.a);
                    material.SetColor(colorEntry.Key, color);
                }
            }

            AssetDatabase.CreateAsset(material, materialPath);
            Debug.Log($"Materiale creato: {materialPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Creazione materiali completata.");
    }

    private static Texture2D FindTexture(string textureName)
    {
        // Cerca la texture per nome nella cartella specificata e sottocartelle
        string[] guids = AssetDatabase.FindAssets($"{textureName} t:Texture2D", new[] { TextureBaseFolderPath });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (guids.Length > 1)
            {
                Debug.LogWarning($"Trovate multiple texture con nome '{textureName}' in '{TextureBaseFolderPath}'. Verrà usata la prima: {path}");
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Fallback: cerca in tutto il progetto se non trovata nella cartella specifica
        guids = AssetDatabase.FindAssets($"{textureName} t:Texture2D");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Debug.LogWarning($"Texture '{textureName}' non trovata in '{TextureBaseFolderPath}', ma trovata in un'altra parte del progetto: {path}. Verrà usata questa.");
            if (guids.Length > 1 && !path.StartsWith(TextureBaseFolderPath)) // Evita doppio warning se la prima ricerca ha trovato multiple corrispondenze
            {
                Debug.LogWarning($"Trovate multiple texture con nome '{textureName}' nel progetto. Verrà usata la prima: {path}");
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }
}