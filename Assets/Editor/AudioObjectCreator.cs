using UnityEngine;
using UnityEditor;
using System.IO;
using JSAM;

public class AudioObjectCreator : MonoBehaviour
{
    [MenuItem("Tools/Automate Sound Effects")]
    public static void AutomateSoundEffects()
    {
        // Define paths
        string audioClipFolderPath = "Assets/Audio/SFX/PsyduckRacing"; // Path to your audio clips
        string scriptableObjectFolderPath = "Assets/Audio/SFX/PsyduckRacing"; // Path to save ScriptableObjects

        // Ensure the ScriptableObject folder exists
        if (!Directory.Exists(scriptableObjectFolderPath))
        {
            Directory.CreateDirectory(scriptableObjectFolderPath);
        }

        // Load all AudioClips from the folder
        string[] audioClipGUIDs = AssetDatabase.FindAssets("t:AudioClip", new[] { audioClipFolderPath });

        foreach (string guid in audioClipGUIDs)
        {
            string audioClipPath = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipPath);

            if (clip != null)
            {
                // Create a new ScriptableObject for each clip
                SoundFileObject soundEffect = ScriptableObject.CreateInstance<SoundFileObject>();
                // Set up fields (you may need to modify this according to your ScriptableObject structure)
                soundEffect.Files.Add(clip);
                soundEffect.pitchShift = 0f; // Default pitch

                // Save the ScriptableObject asset
                string assetName = Path.GetFileNameWithoutExtension(audioClipPath) + ".asset";
                string assetPath = Path.Combine(scriptableObjectFolderPath, assetName);
                AssetDatabase.CreateAsset(soundEffect, assetPath);
            }
        }

        // Save and refresh the AssetDatabase
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Sound effects automation completed.");
    }
}