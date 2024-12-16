using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

public class AutoAssignNetworkPrefabs : EditorWindow
{
    private List<string> blacklistedFolders = new List<string>();
    private Vector2 scrollPosition;
    private NetworkPrefabsList prefabListAsset;

    [MenuItem("Tools/Auto Assign Network Prefabs")]
    private static void ShowWindow()
    {
        GetWindow<AutoAssignNetworkPrefabs>("Auto Assign Network Prefabs");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Network Prefab Auto-Assign Tool", EditorStyles.boldLabel);

        prefabListAsset = (NetworkPrefabsList)EditorGUILayout.ObjectField(
            "Prefab List Asset",
            prefabListAsset,
            typeof(NetworkPrefabsList),
            false
        );

        if (GUILayout.Button("Assign Network Prefabs"))
        {
            if (prefabListAsset == null)
            {
                Debug.LogError("Please assign a NetworkPrefabList asset.");
            }
            else
            {
                AssignNetworkPrefabs();
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Blacklisted Folders", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

        for (int i = 0; i < blacklistedFolders.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            blacklistedFolders[i] = EditorGUILayout.TextField(blacklistedFolders[i]);
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                blacklistedFolders.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Add Blacklisted Folder"))
        {
            blacklistedFolders.Add("");
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Save Blacklist"))
        {
            SaveBlacklist();
        }

        if (GUILayout.Button("Load Blacklist"))
        {
            LoadBlacklist();
        }
    }

    private void AssignNetworkPrefabs()
    {
        string[] allPrefabPaths = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !IsPathBlacklisted(path))
            .ToArray();

        foreach (string prefabPath in allPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab.TryGetComponent(out NetworkObject networkObject))
            {
                if (!prefabListAsset.PrefabList.Any(p => p.Prefab == prefab))
                {
                    Debug.Log($"Added {prefab.name} to prefab list.");
                    prefabListAsset.Add(new NetworkPrefab { Prefab = prefab });
                }
            }
        }

        EditorUtility.SetDirty(prefabListAsset);
        Debug.Log("Network prefabs updated in the specified asset.");
    }

    private bool IsPathBlacklisted(string path)
    {
        return blacklistedFolders.Any(blacklistedFolder =>
            path.StartsWith(blacklistedFolder, System.StringComparison.OrdinalIgnoreCase));
    }

    private void SaveBlacklist()
    {
        string savePath = Path.Combine(Application.dataPath, "blacklist.json");
        File.WriteAllText(savePath, JsonUtility.ToJson(new BlacklistData { Folders = blacklistedFolders }));
        Debug.Log($"Blacklist saved to {savePath}");
    }

    private void LoadBlacklist()
    {
        string loadPath = Path.Combine(Application.dataPath, "blacklist.json");
        if (File.Exists(loadPath))
        {
            string json = File.ReadAllText(loadPath);
            blacklistedFolders = JsonUtility.FromJson<BlacklistData>(json)?.Folders ?? new List<string>();
            Debug.Log($"Blacklist loaded from {loadPath}");
        }
        else
        {
            Debug.LogWarning("Blacklist file not found.");
        }
    }

    [System.Serializable]
    private class BlacklistData
    {
        public List<string> Folders;
    }
}
