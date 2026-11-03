using System;
using UnityEditor;

public sealed class OpenWorldEmoteCatalogImporter : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
    {
        foreach (var path in imported)
        {
            if (path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase) ||
                path == "Assets/Prefabs/TrainerModels/TrainerModel.prefab")
            {
                EditorApplication.update -= RefreshCatalogs;
                EditorApplication.update += RefreshCatalogs;
                return;
            }
        }
    }

    private static void RefreshCatalogs()
    {
        EditorApplication.update -= RefreshCatalogs;
        foreach (var guid in AssetDatabase.FindAssets("t:OpenWorldEmoteCatalog"))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OpenWorldEmoteCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (catalog == null) continue;
            catalog.ResolveAnimations();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
    }
}
