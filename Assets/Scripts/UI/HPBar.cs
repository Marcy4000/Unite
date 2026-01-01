using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unified HP bar that supports both Player and Wild Pokemon.
/// Uses a modular architecture for customizable UI components.
/// Maintains backward compatibility with existing code.
/// </summary>
public class HPBar : MonoBehaviour
{
    [Header("Core HP Bar Elements")]
    [SerializeField] private Image hpBar;
    [SerializeField] private Image shieldBar;
    [SerializeField] private Image damageBar;

    [Header("HP Bar Appearance")]
    [SerializeField] private Sprite[] hpBarColors; // 0: Player, 1: Enemy, 2: Ally

    [Header("HP Markers")]
    [SerializeField] private GameObject linePrefab;
    [SerializeField] private RectTransform linesHolder;
    [SerializeField] private int hpPerMarker = 1000;

    [Header("Player-Only Elements (Optional)")]
    [SerializeField] private Image expBar;
    [SerializeField] private Image storedExpBar;
    [SerializeField] private TMP_Text lvText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private GameObject eyeIcon;

    [Header("Energy Display")]
    [SerializeField] private Image energyBG;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private Sprite orangeEnergyBG;
    [SerializeField] private Sprite blueEnergyBG;

    [Header("Legacy Generic Gauge (Deprecated - Use Modules)")]
    [SerializeField] private GameObject generigGuage;
    [SerializeField] private Image genericGuageBar;

    [Header("Modular System")]
    [SerializeField] private RectTransform modulesContainer;
    [SerializeField] private List<MonoBehaviour> prefabModules = new List<MonoBehaviour>();

    [Header("Bar Size Configuration")]
    [SerializeField] private RectTransform barRectTransform;
    [SerializeField] private float wildBarWidth = 1f;
    [SerializeField] private float objectiveBarWidth = 2f;

    private Pokemon pokemon;
    private bool isOwner;
    private bool isInitialized;
    private Vision assignedVision;

    // Module system
    private List<IHPBarModule> activeModules = new List<IHPBarModule>();
    private Dictionary<string, IHPBarModule> moduleRegistry = new Dictionary<string, IHPBarModule>();
    private Dictionary<string, GaugeModule> dynamicGauges = new Dictionary<string, GaugeModule>();

    public Pokemon Pokemon => pokemon;
    public bool IsInitialized => isInitialized;
    public IReadOnlyList<IHPBarModule> ActiveModules => activeModules;

    #region Initialization

    /// <summary>
    /// Initialize HP bar for a Pokemon (unified method).
    /// </summary>
    public void SetPokemon(Pokemon pokemon, bool isOwner = false)
    {
        this.pokemon = pokemon;
        this.isOwner = isOwner;

        // Subscribe to Pokemon events
        pokemon.OnHpChange += UpdateHPUI;
        pokemon.OnShieldChange += UpdateShieldsUI;
        pokemon.OnLevelChange += UpdateLevel;
        pokemon.OnExpChange += UpdateExp;

        // Initialize UI
        UpdateHPUI();
        UpdateShieldsUI();
        UpdateLevel();
        UpdateExp();
        ShowGenericGuage(false);
        CreateHPMarkers(pokemon.GetMaxHp());

        // Initialize prefab modules
        foreach (var moduleBehaviour in prefabModules)
        {
            if (moduleBehaviour is IHPBarModule module && moduleBehaviour != null)
            {
                RegisterAndInitializeModule(module);
            }
        }

        isInitialized = true;
    }

    /// <summary>
    /// Legacy method - calls SetPokemon with isOwner = false.
    /// </summary>
    public void SetPokemon(Pokemon pokemon)
    {
        SetPokemon(pokemon, false);
    }

    /// <summary>
    /// Configure bar size for Wild/Objective Pokemon.
    /// </summary>
    public void ConfigureBarSize(PokemonType type)
    {
        if (barRectTransform == null) return;

        float width = type == PokemonType.Wild ? wildBarWidth : objectiveBarWidth;
        barRectTransform.sizeDelta = new Vector2(width, barRectTransform.sizeDelta.y);
    }

    #endregion

    #region Vision

    public void AssignVision(Vision vision)
    {
        if (assignedVision != null)
        {
            assignedVision.OnBushChanged -= UpdateEyeIcon;
        }

        assignedVision = vision;

        if (vision != null)
        {
            vision.OnBushChanged += UpdateEyeIcon;
        }
    }

    private void UpdateEyeIcon(GameObject currentBush)
    {
        if (eyeIcon != null)
        {
            eyeIcon.SetActive(currentBush != null);
        }
    }

    #endregion

    #region Player Name & Level

    public void UpdatePlayerName(string playerName)
    {
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }
    }

    private void UpdateLevel()
    {
        if (pokemon == null) return;

        if (lvText != null)
        {
            lvText.text = $"{pokemon.CurrentLevel + 1}";
        }
        UpdateExp();
        CreateHPMarkers(pokemon.GetMaxHp());
    }

    #endregion

    #region HP Bar Colors

    public void UpdateHpBarColor(bool enemyTeam, bool isPlayer)
    {
        if (hpBarColors == null || hpBarColors.Length < 3 || hpBar == null) return;

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

    #endregion

    #region Energy UI

    public void InitializeEnergyUI(PokemonType type, Team team, bool hideUI = false)
    {
        if (hideUI || energyBG == null || energyText == null)
        {
            if (energyBG != null) energyBG.gameObject.SetActive(false);
            if (energyText != null) energyText.gameObject.SetActive(false);
            return;
        }

        switch (type)
        {
            case PokemonType.Player:
                if (team != LobbyController.Instance.GetLocalPlayerTeam())
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

    /// <summary>
    /// Initialize energy UI for wild Pokemon.
    /// </summary>
    public void InitializeEnergyUI(ushort energyAmount, bool hideUI = false)
    {
        if (hideUI || energyBG == null || energyText == null)
        {
            if (energyBG != null) energyBG.gameObject.SetActive(false);
            if (energyText != null) energyText.gameObject.SetActive(false);
            return;
        }

        UpdateEnergyAmount(energyAmount);
    }

    public void UpdateEnergyAmount(ushort amount)
    {
        if (energyText != null)
        {
            energyText.text = amount.ToString();
        }
    }

    #endregion

    #region HP/Shield/EXP Updates

    public void UpdateHPUI()
    {
        if (pokemon == null || hpBar == null) return;

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
                if (damageBar != null) damageBar.fillAmount = newHp;
            }
        }
        else
        {
            hpBar.fillAmount = newHp;
            if (damageBar != null) damageBar.fillAmount = newHp;
        }
    }

    private void UpdateShieldsUI()
    {
        if (pokemon == null || shieldBar == null) return;
        shieldBar.fillAmount = (float)pokemon.ShieldHp / pokemon.GetMaxHp();
    }

    private void UpdateExp()
    {
        if (pokemon == null || expBar == null) return;

        if (pokemon.LocalLevel == pokemon.LevelCap - 1)
        {
            expBar.fillAmount = 1;
            if (storedExpBar != null) storedExpBar.fillAmount = 0;
            return;
        }

        float expNeeded = pokemon.BaseStats.GetExpForNextLevel(pokemon.LocalLevel);
        float normalizedExp = pokemon.LocalExp / expNeeded;
        float normalizedStoredExp = pokemon.LocalStoredExp / expNeeded;
        normalizedStoredExp += normalizedExp;

        if (normalizedExp < 0)
        {
            expBar.fillAmount = 1;
        }
        else
        {
            expBar.fillAmount = normalizedExp;
        }

        if (storedExpBar != null)
        {
            storedExpBar.fillAmount = normalizedStoredExp;
        }
    }

    private IEnumerator UpdateHP(float newHp)
    {
        float curHp = hpBar.fillAmount;
        float changeAmt = curHp - newHp;

        if (damageBar != null) damageBar.fillAmount = curHp;
        hpBar.fillAmount = (float)pokemon.CurrentHp / pokemon.GetMaxHp();

        yield return new WaitForSeconds(0.4f);

        while (curHp - newHp > Mathf.Epsilon)
        {
            curHp -= changeAmt * Time.deltaTime;
            if (damageBar != null) damageBar.fillAmount = curHp;
            yield return null;
        }

        if (damageBar != null) damageBar.fillAmount = newHp;
    }

    #endregion

    #region HP Markers

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

    #endregion

    #region Legacy Generic Gauge (Backward Compatibility)

    /// <summary>
    /// [DEPRECATED] Use AddGauge() for new implementations.
    /// Shows/hides the legacy generic gauge.
    /// </summary>
    public void ShowGenericGuage(bool show)
    {
        if (generigGuage != null)
        {
            generigGuage.SetActive(show);
        }
    }

    /// <summary>
    /// [DEPRECATED] Use GetGauge().SetFillAmount() for new implementations.
    /// </summary>
    public void UpdateGenericGuageValue(float fillAmount)
    {
        if (genericGuageBar != null)
        {
            genericGuageBar.fillAmount = Mathf.Clamp(fillAmount, 0f, 1f);
        }
    }

    /// <summary>
    /// [DEPRECATED] Use GetGauge().SetValue() for new implementations.
    /// </summary>
    public void UpdateGenericGuageValue(float fillAmount, float maxValue)
    {
        if (genericGuageBar != null)
        {
            genericGuageBar.fillAmount = Mathf.Clamp(fillAmount / maxValue, 0f, 1f);
        }
    }

    #endregion

    #region Modular System

    /// <summary>
    /// Add a module at runtime.
    /// </summary>
    public void AddModule(IHPBarModule module)
    {
        if (module == null) return;

        RegisterAndInitializeModule(module);

        if (isInitialized && pokemon != null)
        {
            module.Initialize(pokemon, isOwner);
        }
    }

    /// <summary>
    /// Add a module by instantiating a prefab.
    /// </summary>
    public T AddModuleFromPrefab<T>(T prefab, RectTransform parent = null) where T : MonoBehaviour, IHPBarModule
    {
        if (prefab == null) return null;

        RectTransform targetParent = parent != null ? parent : modulesContainer;
        if (targetParent == null) targetParent = transform as RectTransform;

        T instance = Instantiate(prefab, targetParent);
        AddModule(instance);
        return instance;
    }

    /// <summary>
    /// Remove a module.
    /// </summary>
    public void RemoveModule(IHPBarModule module, bool destroyGameObject = true)
    {
        if (module == null) return;

        if (moduleRegistry.ContainsKey(module.ModuleId))
        {
            moduleRegistry.Remove(module.ModuleId);
        }
        activeModules.Remove(module);

        module.Cleanup();

        if (destroyGameObject && module is MonoBehaviour mb)
        {
            Destroy(mb.gameObject);
        }
    }

    /// <summary>
    /// Remove a module by ID.
    /// </summary>
    public void RemoveModule(string moduleId, bool destroyGameObject = true)
    {
        if (TryGetModule(moduleId, out IHPBarModule module))
        {
            RemoveModule(module, destroyGameObject);
        }
    }

    /// <summary>
    /// Get a module by ID.
    /// </summary>
    public IHPBarModule GetModule(string moduleId)
    {
        moduleRegistry.TryGetValue(moduleId, out IHPBarModule module);
        return module;
    }

    /// <summary>
    /// Try to get a module by ID.
    /// </summary>
    public bool TryGetModule(string moduleId, out IHPBarModule module)
    {
        return moduleRegistry.TryGetValue(moduleId, out module);
    }

    /// <summary>
    /// Get a module by type.
    /// </summary>
    public T GetModule<T>() where T : class, IHPBarModule
    {
        foreach (var module in activeModules)
        {
            if (module is T typedModule)
            {
                return typedModule;
            }
        }
        return null;
    }

    /// <summary>
    /// Get all modules of a specific type.
    /// </summary>
    public List<T> GetModules<T>() where T : class, IHPBarModule
    {
        List<T> result = new List<T>();
        foreach (var module in activeModules)
        {
            if (module is T typedModule)
            {
                result.Add(typedModule);
            }
        }
        return result;
    }

    /// <summary>
    /// Check if a module with the given ID exists.
    /// </summary>
    public bool HasModule(string moduleId)
    {
        return moduleRegistry.ContainsKey(moduleId);
    }

    private void RegisterAndInitializeModule(IHPBarModule module)
    {
        string id = module.ModuleId;

        // Handle duplicate IDs
        if (moduleRegistry.ContainsKey(id))
        {
            int counter = 1;
            while (moduleRegistry.ContainsKey($"{id}_{counter}"))
            {
                counter++;
            }
            id = $"{id}_{counter}";
        }

        moduleRegistry[id] = module;
        activeModules.Add(module);

        if (isInitialized && pokemon != null)
        {
            module.Initialize(pokemon, isOwner);
        }
    }

    #endregion

    #region Dynamic Gauge Management

    /// <summary>
    /// Create or get a named gauge for abilities.
    /// </summary>
    /// <param name="gaugeId">Unique identifier for this gauge.</param>
    /// <param name="gaugePrefab">Prefab to instantiate (if creating new).</param>
    /// <returns>The gauge module.</returns>
    public GaugeModule GetOrCreateGauge(string gaugeId, GaugeModule gaugePrefab = null)
    {
        if (dynamicGauges.TryGetValue(gaugeId, out GaugeModule existing))
        {
            return existing;
        }

        if (gaugePrefab != null)
        {
            GaugeModule newGauge = AddModuleFromPrefab(gaugePrefab);
            if (newGauge != null)
            {
                dynamicGauges[gaugeId] = newGauge;
            }
            return newGauge;
        }

        return null;
    }

    /// <summary>
    /// Remove a dynamic gauge by ID.
    /// </summary>
    public void RemoveGauge(string gaugeId)
    {
        if (dynamicGauges.TryGetValue(gaugeId, out GaugeModule gauge))
        {
            dynamicGauges.Remove(gaugeId);
            RemoveModule(gauge, true);
        }
    }

    /// <summary>
    /// Show/hide a dynamic gauge.
    /// </summary>
    public void SetGaugeVisibility(string gaugeId, bool visible)
    {
        if (dynamicGauges.TryGetValue(gaugeId, out GaugeModule gauge))
        {
            gauge.SetVisibility(visible);
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        // Unsubscribe from Pokemon events
        if (pokemon != null)
        {
            pokemon.OnHpChange -= UpdateHPUI;
            pokemon.OnShieldChange -= UpdateShieldsUI;
            pokemon.OnLevelChange -= UpdateLevel;
            pokemon.OnExpChange -= UpdateExp;
        }

        // Unsubscribe from Vision
        if (assignedVision != null)
        {
            assignedVision.OnBushChanged -= UpdateEyeIcon;
        }

        // Cleanup modules
        foreach (var module in activeModules)
        {
            module.Cleanup();
        }
        activeModules.Clear();
        moduleRegistry.Clear();
        dynamicGauges.Clear();
    }

    #endregion
}
