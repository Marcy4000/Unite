using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Image hpBar, shieldBar, damageBar, expBar, storedExpBar, energyBG, genericGuageBar;
    [SerializeField] private TMP_Text lvText, playerNameText, energyText;
    [SerializeField] private Pokemon pokemon;
    [SerializeField] private Sprite orangeEnergyBG, blueEnergyBG;
    [SerializeField] private GameObject generigGuage, eyeIcon;

    [SerializeField] private GameObject linePrefab;
    [SerializeField] private RectTransform linesHolder;

    [SerializeField] private Sprite[] hpBarColors;

    public void SetPokemon(Pokemon pokemon)
    {
        this.pokemon = pokemon;
        pokemon.OnHpChange += UpdateHPUI;
        pokemon.OnShieldChange += UpdateShieldsUI;
        pokemon.OnLevelChange += UpdateLevel;
        pokemon.OnExpChange += UpdateExp;
        UpdateHPUI();
        UpdateShieldsUI();
        UpdateLevel();
        UpdateExp();
        ShowGenericGuage(false);
        CreateHPMarkers(pokemon.GetMaxHp());
    }

    public void AssignVision(Vision vision)
    {
        vision.OnBushChanged += UpdateEyeIcon;
    }

    private void UpdateEyeIcon(GameObject currentBush)
    {
        eyeIcon.SetActive(currentBush != null);
    }

    public void UpdatePlayerName(string playerName)
    {
        playerNameText.text = playerName;
    }

    public void UpdateHpBarColor(bool enemyTeam, bool isPlayer)
    {
        if (isPlayer)
        {
            hpBar.sprite = hpBarColors[0];
            return;
        }

        if (enemyTeam)
        {
            hpBar.sprite = hpBarColors[1];
        }
        else
        {
            hpBar.sprite = hpBarColors[2];
        }
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

    public void UpdateEnergyAmount(ushort amount)
    {
        energyText.text = amount.ToString();
    }

    public void UpdateHPUI()
    {
        float newHp = (float)pokemon.CurrentHp / pokemon.GetMaxHp();
        if (hpBar.fillAmount > newHp)
        {
            StopAllCoroutines();
            if (isActiveAndEnabled)
            {
                StartCoroutine(UpdateHP(newHp));
            }
            else
            {
                hpBar.fillAmount = newHp;
                damageBar.fillAmount = newHp;
            }
        }
        else
        {
            hpBar.fillAmount = newHp;
            damageBar.fillAmount = newHp;
        }
    }

    private void UpdateShieldsUI()
    {
        shieldBar.fillAmount = (float)pokemon.ShieldHp / pokemon.GetMaxHp();
    }

    private void UpdateLevel()
    {
        lvText.text = $"{pokemon.CurrentLevel + 1}";
        UpdateExp();
        CreateHPMarkers(pokemon.GetMaxHp());
    }

    private void UpdateExp()
    {
        if (pokemon.LocalLevel == pokemon.LevelCap - 1)
        {
            expBar.fillAmount = 1;
            storedExpBar.fillAmount = 0;
            return;
        }

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

    public void CreateHPMarkers(int maxHP)
    {
        foreach (Transform child in linesHolder.transform)
        {
            Destroy(child.gameObject);
        }

        int numberOfMarkers = maxHP / 1000;

        float healthBarWidth = linesHolder.rect.width;

        float spacing = (healthBarWidth * 1000f) / maxHP;

        for (int i = 1; i <= numberOfMarkers; i++)
        {
            GameObject line = Instantiate(linePrefab, linesHolder.transform);
            RectTransform lineRectTransform = line.GetComponent<RectTransform>();
            lineRectTransform.anchoredPosition = new Vector2(spacing * i, 0);
        }
    }
}
