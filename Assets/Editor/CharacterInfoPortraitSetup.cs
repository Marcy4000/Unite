using System.Linq;
using UnityEditor;
using UnityEngine;

// Resolve gameplay assets only while authoring. Character select uses the saved
// Sprite reference and never loads PokemonBase (or its model/move dependencies).
public static class CharacterInfoPortraitSetup
{
    [MenuItem("Tools/Character Select/Populate Missing Circle Portraits")]
    public static void PopulateCirclePortraits()
    {
        Debug.Log($"Assigned {PopulateMissingCirclePortraits()} character-select circle portraits.");
    }

    public static int PopulateMissingCirclePortraits()
    {
        int updated = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterInfo", new[] { "Assets" }))
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterInfo>(AssetDatabase.GUIDToAssetPath(guid));
            if (character.circleIcon != null) continue; // Preserve manually chosen portraits.
            var sprite = FindHighestEvolutionCirclePortrait(character);
            if (sprite == null)
            {
                Debug.LogWarning($"No highest-evolution circle portrait found for {character.name}; using its normal icon until one is assigned.", character);
                continue;
            }
            character.circleIcon = sprite;
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssetIfDirty(character);
            updated++;
        }
        return updated;
    }

    public static Sprite FindHighestEvolutionCirclePortrait(CharacterInfo character)
    {
        if (character == null || character.pokemon == null || string.IsNullOrEmpty(character.pokemon.AssetGUID)) return null;
        var pokemon = AssetDatabase.LoadAssetAtPath<PokemonBase>(AssetDatabase.GUIDToAssetPath(character.pokemon.AssetGUID));
        var evolution = pokemon != null && pokemon.Evolutions != null
            ? pokemon.Evolutions.Where(item => item != null).OrderByDescending(item => item.level).FirstOrDefault()
            : null;
        return evolution != null ? evolution.newSprite : null;
    }
}
