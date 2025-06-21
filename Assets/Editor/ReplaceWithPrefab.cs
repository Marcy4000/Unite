using UnityEditor;
using UnityEngine;

public class ReplaceWithPrefab : MonoBehaviour
{
    [MenuItem("Tools/Replace WildPokemonSpawners with Prefab")]
    private static void ReplaceWildPokemonSpawners()
    {
        // Select your prefab in the Project window
        GameObject prefab = Selection.activeObject as GameObject;

        if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab))
        {
            Debug.LogError("Please select a prefab in the Project window before running this script.");
            return;
        }

        // Find all WildPokemonSpawner objects in the scene
        WildPokemonSpawner[] spawners = FindObjectsByType<WildPokemonSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            // Record the transform and assigned values
            Transform t = spawner.transform;

            // Instantiate the prefab and set its transform
            GameObject newObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.parent);
            newObject.transform.position = t.position;
            newObject.transform.rotation = t.rotation;
            newObject.transform.localScale = t.localScale;
            newObject.name = t.name;

            // Copy field values from the old spawner
            WildPokemonSpawner newSpawner = newObject.GetComponent<WildPokemonSpawner>();
            EditorUtility.CopySerializedManagedFieldsOnly(spawner, newSpawner);

            // Delete the old object
            DestroyImmediate(spawner.gameObject);
        }

        Debug.Log("Replaced all WildPokemonSpawner objects with the selected prefab.");
    }
}