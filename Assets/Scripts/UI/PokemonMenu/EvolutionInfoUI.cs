using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EvolutionInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text evolutionName;
    [SerializeField] private TMP_Text evolutionLevel;
    [SerializeField] private Image evolutionIcon;

    public void SetEvolutionInfo(PokemonEvolution evolution)
    {
        evolutionName.text = evolution.EvolutionName;
        evolutionLevel.text = $"<size=60%>L.<size=100%>{evolution.level+1}";
        evolutionIcon.sprite = evolution.newSprite;
    }
}
