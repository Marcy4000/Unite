using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HPBarWild : MonoBehaviour
{
    [SerializeField] private Image hpBar, shieldBar, damageBar, energyBG;
    [SerializeField] private RectTransform bar;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private Pokemon pokemon;

    public void SetPokemon(Pokemon pokemon)
    {
        this.pokemon = pokemon;
        pokemon.OnHpChange += UpdateHPUI;
        pokemon.OnShieldChange += UpdateShieldsUI;
        float width = pokemon.Type == PokemonType.Wild ? 1f : 2f;
        bar.sizeDelta = new Vector2(width, bar.sizeDelta.y);
        UpdateHPUI();
        UpdateShieldsUI();
    }

    public void InitializeEnergyUI(ushort energyAmount, bool hideUI = false)
    {
        if (hideUI)
        {
            energyBG.gameObject.SetActive(false);
            energyText.gameObject.SetActive(false);
            return;
        }

        UpdateEnergyAmount(energyAmount);
    }

    public void UpdateEnergyAmount(ushort amount)
    {
        energyText.text = amount.ToString();
    }

    public void UpdateHPUI()
    {
        if (hpBar.fillAmount > (pokemon.CurrentHp / pokemon.GetMaxHp()))
        {
            if (isActiveAndEnabled)
            {
                StopAllCoroutines();
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
            damageBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();
        }
    }

    public void UpdateShieldsUI()
    {
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
