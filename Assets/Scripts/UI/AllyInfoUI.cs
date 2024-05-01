using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AllyInfoUI : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Image hpBar;

    private Pokemon allyPokemon;

    public void InitializeUI(Pokemon pokemon)
    {
        allyPokemon = pokemon;
        portrait.sprite = pokemon.Portrait;
        hpBar.fillAmount = (float)pokemon.CurrentHp.Value / pokemon.GetMaxHp();
        pokemon.CurrentHp.OnValueChanged += UpdateHpBar;
        pokemon.OnEvolution += UpdatePortrait;
    }

    public void UpdateHpBar(int previous, int current)
    {
        hpBar.fillAmount = (float)current / allyPokemon.GetMaxHp();
    }

    public void UpdatePortrait()
    {
        portrait.sprite = allyPokemon.Portrait;
    }
}
