using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HPBarWild : MonoBehaviour
{
    [SerializeField] private Image hpBar, shieldBar, damageBar, energyBG;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private Pokemon pokemon;

    public void SetPokemon(Pokemon pokemon)
    {
        this.pokemon = pokemon;
        pokemon.OnHpOrShieldChange += UpdateUI;
        UpdateUI();
    }

    public void InitializeEnergyUI(short energyAmount, bool hideUI = false)
    {
        if (hideUI)
        {
            energyBG.gameObject.SetActive(false);
            energyText.gameObject.SetActive(false);
            return;
        }

        UpdateEnergyAmount(energyAmount);
    }

    public void UpdateEnergyAmount(short amount)
    {
        energyText.text = amount.ToString();
    }

    public void UpdateUI()
    {
        if (hpBar.fillAmount > (pokemon.CurrentHp / pokemon.GetMaxHp()))
        {
            if (isActiveAndEnabled)
            {
                StopAllCoroutines();
                StartCoroutine(UpdateHP(pokemon.CurrentHp / pokemon.GetMaxHp()));
            }
        }
        else
        {
            hpBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();
        }
        shieldBar.fillAmount = (float)pokemon.ShieldHp / pokemon.GetMaxHp();
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
