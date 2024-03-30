using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Image hpBar, shieldBar, damageBar, expBar, storedExpBar;
    [SerializeField] private TMP_Text lvText;
    [SerializeField] private Pokemon pokemon;

    private void Start()
    {
        SetPokemon(pokemon);
    }

    public void SetPokemon(Pokemon pokemon)
    {
        this.pokemon = pokemon;
        pokemon.OnHpOrShieldChange += UpdateUI;
        pokemon.OnLevelChange += UpdateLevel;
        pokemon.OnExpChange += UpdateExp;
        UpdateUI();
        UpdateLevel();
        UpdateExp();
    }

    public void UpdateUI()
    {
        if (hpBar.fillAmount > (pokemon.CurrentHp / pokemon.GetMaxHp()))
        {
            StopAllCoroutines();
            StartCoroutine(UpdateHP(pokemon.CurrentHp / pokemon.GetMaxHp()));
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
    }

    private void UpdateExp()
    {
        float normalizedExp = (float)pokemon.CurrentExp / pokemon.BaseStats.GetExpForNextLevel(pokemon.CurrentLevel);
        float normalizedStoredExp = (float)pokemon.StoredExp / pokemon.BaseStats.GetExpForNextLevel(pokemon.CurrentLevel);
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

        yield return new WaitForSeconds(1f);

        while (curHp - newHp > Mathf.Epsilon)
        {
            curHp -= changeAmt * Time.deltaTime;
            damageBar.fillAmount = curHp;

            yield return null;
        }

        damageBar.fillAmount = newHp;
    }
}
