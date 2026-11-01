using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class CombatArchitectureValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    [MenuItem("Tools/Combat/Validate Phase One")]
    public static void ValidateMenu()
    {
        List<string> errors = CollectErrors();
        if (errors.Count == 0) Debug.Log("Phase-one combat asset contracts are valid.");
        else foreach (string error in errors) Debug.LogError(error);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        List<string> errors = CollectErrors();
        if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
    }

    public static List<string> CollectErrors()
    {
        var errors = new List<string>();
        var checkedAssets = new HashSet<string>();
        UnityEngine.Object registryAsset = AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset");
        SerializedProperty registry = registryAsset != null ? new SerializedObject(registryAsset).FindProperty("List") : null;
        var registered = new HashSet<UnityEngine.Object>();
        if (registry == null) errors.Add("Combat network prefab registry is missing.");
        else for (int index = 0; index < registry.arraySize; index++)
            registered.Add(registry.GetArrayElementAtIndex(index).FindPropertyRelative("Prefab").objectReferenceValue);

        foreach (string guid in AssetDatabase.FindAssets("t:PokemonBase", new[] { "Assets/Pokemons/PlayableMons" }))
        {
            PokemonBase pokemon = AssetDatabase.LoadAssetAtPath<PokemonBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (!CombatKitFactory.SupportsDefinition(pokemon)) continue;
            if (pokemon.LearnableMoves == null) { errors.Add($"{pokemon.name}: missing learnset."); continue; }
            foreach (LearnableMove row in pokemon.LearnableMoves)
            {
                if (row.moves == null) { errors.Add($"{pokemon.name}: null learnset row."); continue; }
                foreach (MoveAsset asset in row.moves)
                {
                    if (asset == null) { errors.Add($"{pokemon.name}: null move asset."); continue; }
                    if (!(MoveDatabase.GetMove(asset.move) is CombatMove definition))
                    { errors.Add($"{pokemon.name}/{asset.move}: no migrated input/execution registration."); continue; }
                    if (asset.moveType == MoveType.All) errors.Add($"{pokemon.name}/{asset.move}: cannot install in All slots.");
                    if (!string.IsNullOrEmpty(definition.PrefabPath)) CheckPrefab(definition.PrefabPath, typeof(PrefabMoveEffect));
                }
            }
        }

        foreach (var contract in CombatKitFactory.RequiredAssets) CheckPrefab(contract.Path, contract.Component, contract.Networked);
        GameObject gauge = Resources.Load<GameObject>("HPBarModules/SegmentedGauge");
        if (gauge == null || gauge.GetComponent<SegmentedGaugeModule>() == null) errors.Add("Zoroark segmented gauge Resources contract is missing.");
        return errors;

        void CheckPrefab(string path, Type required, bool networked = true)
        {
            if (!checkedAssets.Add(path)) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { errors.Add($"Missing combat prefab: {path}"); return; }
            if (prefab.GetComponent(required) == null) errors.Add($"{path}: missing {required.Name}.");
            if (networked && prefab.GetComponent<Unity.Netcode.NetworkObject>() == null) errors.Add($"{path}: missing NetworkObject.");
            if (networked && !registered.Contains(prefab)) errors.Add($"{path}: absent from DefaultNetworkPrefabs.");
            string assetGuid = AssetDatabase.AssetPathToGUID(path);
            bool addressable = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings?.FindAssetEntry(assetGuid) != null;
            if (!addressable) errors.Add($"{path}: missing Addressables entry.");
        }
    }
}
