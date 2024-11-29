using TMPro;
using UnityEngine;

public class PkmnListSideInfo : MonoBehaviour
{
    [SerializeField] private TMP_Text pokemonName;
    [SerializeField] private TMP_Text pokemonDifficulty;
    [SerializeField] private TMP_Text atkLabelText;
    [SerializeField] private TMP_Text rangeLabel, classLabel;
    [SerializeField] private TMP_Text pointsLabel;

    public void SetPokemonInfo(CharacterInfo characterInfo)
    {
        pokemonName.text = characterInfo.pokemonName;
        pokemonDifficulty.text = $"Difficulty: {characterInfo.Difficulty.ToString()}";
        rangeLabel.text = characterInfo.Range.ToString();
        classLabel.text = characterInfo.pClass.ToString();
        pointsLabel.text = "0";

        switch (characterInfo.DamageType)
        {
            case DamageType.Special:
                atkLabelText.text = "Sp. Atk";
                break;
            case DamageType.Physical:
            case DamageType.True:
                atkLabelText.text = "Attack";
                break;
        }
    }
}
