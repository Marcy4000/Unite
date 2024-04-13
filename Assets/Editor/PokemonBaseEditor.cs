using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(PokemonBase))]
public class PokemonBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PokemonBase pokemonBase = (PokemonBase)target;

        if (GUILayout.Button("Fill Arrays From File"))
        {
            FillArraysFromFile(pokemonBase);
        }
    }

    void FillArraysFromFile(PokemonBase pokemonBase)
    {
        string filePath = EditorUtility.OpenFilePanel("Select Data File", "", "txt");
        if (string.IsNullOrEmpty(filePath))
            return;

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split('\t');

            if (i-1 >= pokemonBase.MaxHp.Length)
            {
                Debug.LogWarning("Data file contains more entries than array size. Additional entries will be ignored.");
                break;
            }

            pokemonBase.MaxHp[i-1] = int.Parse(values[1]);
            pokemonBase.Attack[i-1] = int.Parse(values[2]);
            pokemonBase.Defense[i-1] = int.Parse(values[3]);
            pokemonBase.SpAttack[i-1] = int.Parse(values[4]);
            pokemonBase.SpDefense[i-1] = int.Parse(values[5]);
            pokemonBase.CritRate[i-1] = int.Parse(values[6]);
            pokemonBase.Cdr[i-1] = int.Parse(values[7]);
            pokemonBase.LifeSteal[i-1] = int.Parse(values[8]);
            pokemonBase.AtkSpeed[i-1] = float.Parse(values[9]);
            pokemonBase.Speed[i-1] = int.Parse(values[10]);

            pokemonBase.AtkSpeed[i-1] /= 100f;
        }

        EditorUtility.SetDirty(pokemonBase);
    }
}