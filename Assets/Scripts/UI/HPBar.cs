using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Image hpBar, shieldBar, damageBar, expBar, storedExpBar, energyBG, genericGuageBar;
    [SerializeField] private TMP_Text lvText, playerNameText, energyText;
    [SerializeField] private Pokemon pokemon;
    [SerializeField] private Sprite orangeEnergyBG, blueEnergyBG;
    [SerializeField] private GameObject generigGuage;

    public void SetPokemon(Pokemon pokemon)
    {
        this.pokemon = pokemon;
        pokemon.OnHpOrShieldChange += UpdateUI;
        pokemon.OnLevelChange += UpdateLevel;
        pokemon.OnExpChange += UpdateExp;
        UpdateUI();
        UpdateLevel();
        UpdateExp();
        ShowGenericGuage(false);
    }

    public void UpdatePlayerName(string playerName)
    {
        playerNameText.text = playerName;
    }

    public void InitializeEnergyUI(PokemonType type, bool orangeTeam=false, bool hideUI=false)
    {
        if (hideUI)
        {
            energyBG.gameObject.SetActive(false);
            energyText.gameObject.SetActive(false);
            return;
        }

        switch (type)
        {
            case PokemonType.Player:
                if (orangeTeam)
                {
                    energyBG.sprite = orangeEnergyBG;
                }
                else
                {
                    energyBG.sprite = blueEnergyBG;
                }
                energyText.text = "0";
                break;
            case PokemonType.Wild:
            case PokemonType.Objective:
                energyBG.gameObject.SetActive(false);
                energyText.gameObject.SetActive(false);
                break;
            default:
                break;
        }
    }

    public void ShowGenericGuage(bool show)
    {
        generigGuage.SetActive(show);
    }

    public void UpdateGenericGuageValue(float fillAmount)
    {
        genericGuageBar.fillAmount = Mathf.Clamp(fillAmount, 0f, 1f);
    }

    public void UpdateGenericGuageValue(float fillAmount, float maxValue)
    {
        genericGuageBar.fillAmount = Mathf.Clamp(fillAmount / maxValue, 0f, 1f);
    }

    public void UpdateEnergyAmount(short amount)
    {
        energyText.text = amount.ToString();
    }

    public void UpdateUI()
    {
        if (hpBar.fillAmount > (pokemon.CurrentHp / pokemon.GetMaxHp()))
        {
            StopAllCoroutines();
            if (isActiveAndEnabled)
            {
                StartCoroutine(UpdateHP(pokemon.CurrentHp / pokemon.GetMaxHp()));
            }
            else
            {
                hpBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();
                damageBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();
            }
        }
        else
        {
            hpBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();
        }

        shieldBar.fillAmount = (float)pokemon.ShieldHp / pokemon.GetMaxHp();
    }

    private void UpdateLevel()
    {
        lvText.text = $"{pokemon.CurrentLevel + 1}";
        UpdateExp();
    }

    private void UpdateExp()
    {
        float normalizedExp = (float)pokemon.LocalExp / pokemon.BaseStats.GetExpForNextLevel(pokemon.LocalLevel);
        float normalizedStoredExp = (float)pokemon.LocalStoredExp / pokemon.BaseStats.GetExpForNextLevel(pokemon.LocalLevel);
        normalizedStoredExp += normalizedExp;
        if (normalizedExp < 0)
        {
            expBar.fillAmount = 1;
        }
        else
        {
            expBar.fillAmount = normalizedExp;
        }
        storedExpBar.fillAmount = normalizedStoredExp;
    }

    private IEnumerator UpdateHP(float newHp)
    {
        float curHp = hpBar.fillAmount;
        float changeAmt = curHp - newHp;

        damageBar.fillAmount = curHp;
        hpBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();

        yield return new WaitForSeconds(0.4f);

        while (curHp - newHp > Mathf.Epsilon)
        {
            curHp -= changeAmt * Time.deltaTime;
            damageBar.fillAmount = curHp;

            yield return null;
        }

        damageBar.fillAmount = newHp;
    }
}
