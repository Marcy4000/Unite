using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Core HP bar module that handles HP, Shield, and Damage bar animations.
/// </summary>
public class HPModule : HPBarModuleBase
{
    [Header("HP Bar References")]
    [SerializeField] private Image hpBar;
    [SerializeField] private Image shieldBar;
    [SerializeField] private Image damageBar;

    [Header("HP Bar Colors")]
    [SerializeField] private Sprite[] hpBarColors; // 0: Player, 1: Enemy, 2: Ally

    [Header("HP Markers")]
    [SerializeField] private GameObject linePrefab;
    [SerializeField] private RectTransform linesHolder;
    [SerializeField] private int hpPerMarker = 1000;

    [Header("Animation Settings")]
    [SerializeField] private float damageAnimationDelay = 0.4f;

    private Coroutine damageAnimationCoroutine;

    protected override void SubscribeToEvents()
    {
        if (pokemon != null)
        {
            pokemon.OnHpChange += UpdateUI;
            pokemon.OnShieldChange += UpdateShieldUI;
            pokemon.OnLevelChange += OnLevelChange;
        }
    }

    protected override void UnsubscribeFromEvents()
    {
        if (pokemon != null)
        {
            pokemon.OnHpChange -= UpdateUI;
            pokemon.OnShieldChange -= UpdateShieldUI;
            pokemon.OnLevelChange -= OnLevelChange;
        }
    }

    public override void UpdateUI()
    {
        if (pokemon == null || hpBar == null) return;

        float newHp = (float)pokemon.CurrentHp / pokemon.GetMaxHp();

        if (hpBar.fillAmount > newHp)
        {
            if (damageAnimationCoroutine != null)
            {
                StopCoroutine(damageAnimationCoroutine);
            }

            if (isActiveAndEnabled)
            {
                damageAnimationCoroutine = StartCoroutine(AnimateDamage(newHp));
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
            if (damageBar != null)
            {
                damageBar.fillAmount = newHp;
            }
        }
    }

    private void UpdateShieldUI()
    {
        if (pokemon == null || shieldBar == null) return;
        shieldBar.fillAmount = (float)pokemon.ShieldHp / pokemon.GetMaxHp();
    }

    private void OnLevelChange()
    {
        CreateHPMarkers(pokemon.GetMaxHp());
    }

    private IEnumerator AnimateDamage(float newHp)
    {
        float curHp = hpBar.fillAmount;
        float changeAmt = curHp - newHp;

        if (damageBar != null)
        {
            damageBar.fillAmount = curHp;
        }
        hpBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();

        yield return new WaitForSeconds(damageAnimationDelay);

        while (curHp - newHp > Mathf.Epsilon)
        {
            curHp -= changeAmt * Time.deltaTime;
            if (damageBar != null)
            {
                damageBar.fillAmount = curHp;
            }
            yield return null;
        }

        if (damageBar != null)
        {
            damageBar.fillAmount = newHp;
        }
    }

    /// <summary>
    /// Update HP bar color based on team relation.
    /// </summary>
    /// <param name="isEnemy">Whether this is an enemy.</param>
    /// <param name="isLocalPlayer">Whether this is the local player.</param>
    public void UpdateHpBarColor(bool isEnemy, bool isLocalPlayer)
    {
        if (hpBarColors == null || hpBarColors.Length < 3) return;

        if (isLocalPlayer)
        {
            hpBar.sprite = hpBarColors[0];
        }
        else if (isEnemy)
        {
            hpBar.sprite = hpBarColors[1];
        }
        else
        {
            hpBar.sprite = hpBarColors[2];
        }
    }

    /// <summary>
    /// Create HP markers (tick marks) every X HP.
    /// </summary>
    public void CreateHPMarkers(int maxHP)
    {
        if (linesHolder == null || linePrefab == null) return;

        foreach (Transform child in linesHolder.transform)
        {
            Destroy(child.gameObject);
        }

        int numberOfMarkers = maxHP / hpPerMarker;
        float healthBarWidth = linesHolder.rect.width;
        float spacing = (healthBarWidth * hpPerMarker) / maxHP;

        for (int i = 1; i <= numberOfMarkers; i++)
        {
            GameObject line = Instantiate(linePrefab, linesHolder.transform);
            RectTransform lineRectTransform = line.GetComponent<RectTransform>();
            lineRectTransform.anchoredPosition = new Vector2(spacing * i, 0);
        }
    }

    public override void Cleanup()
    {
        if (damageAnimationCoroutine != null)
        {
            StopCoroutine(damageAnimationCoroutine);
        }
        base.Cleanup();
    }
}
