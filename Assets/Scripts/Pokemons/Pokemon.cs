using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pokemon : MonoBehaviour
{
    [SerializeField] private PokemonBase baseStats;
    [SerializeField] private GameObject damagePrefab;
    private int currentHp;
    private int shieldHp;
    private int currentLevel;
    private int currentExp;
    private int storedExp;

    private PokemonType type;

    private GameObject activeModel;

    public int CurrentHp { get { return currentHp; } }
    public int ShieldHp { get { return shieldHp; } }
    public int CurrentLevel { get { return currentLevel; } }
    public int CurrentExp { get { return currentExp; } }
    public int StoredExp { get { return storedExp; } }

    public PokemonType Type { get { return type; } set { type = value; } }

    public PokemonBase BaseStats { get { return baseStats; } }

    public GameObject ActiveModel { get { return activeModel; } }

    public event Action OnHpOrShieldChange;
    public event Action OnLevelChange;
    public event Action OnExpChange;
    public event Action OnEvolution;
    public event Action<DamageInfo> OnDeath;

    private void Awake()
    {
        InitializePokemon();
    }

    public void GainPassiveExp(int amount)
    {
        if (baseStats.IsLevelBeforeEvo(currentLevel))
        {
            StoreExperience(6);
        }
        else
        {
            GainExperience(6);
        }
    }

    public void InitializePokemon()
    {
        currentHp = GetMaxHp();
        shieldHp = 0;
        currentExp = 0;
        CheckEvolution();
    }

    public int GetMaxHp()
    {
        return baseStats.MaxHp[currentLevel];
    }

    private void Update()
    {
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TakeDamage(new DamageInfo(this, 3.45f, 32, 820, DamageType.Physical));
        }
        if (Keyboard.current.yKey.wasPressedThisFrame)
        {
            HealDamage(300);
        }

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            AddShield(300);
        }
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            RemoveShield(300);
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            GainExperience(100);
        }
    }

    public void TakeDamage(DamageInfo damage)
    {
        int atkStat;

        switch (damage.type)
        {
            case DamageType.True:
            case DamageType.Physical:
                atkStat = damage.attacker.baseStats.Attack[damage.attacker.CurrentLevel];
                break;
            case DamageType.Special:
                atkStat = damage.attacker.baseStats.SpAttack[damage.attacker.CurrentLevel];
                break;
            default:
                atkStat = 0;
                break;
        }

        int attackDamage = Mathf.FloorToInt(damage.ratio * atkStat + damage.slider * damage.attacker.CurrentLevel + damage.baseDmg);
        int defStat;
        switch (damage.type)
        {
            case DamageType.Physical:
                defStat = baseStats.Defense[currentLevel];
                break;
            case DamageType.Special:
                defStat = baseStats.SpDefense[currentLevel];
                break;
            case DamageType.True:
            default:
                defStat = 0;
                break;
        }

        int actualDamage = Mathf.FloorToInt((float)attackDamage * 600 / (600 + defStat));

        if (shieldHp > 0)
        {
            if (shieldHp >= actualDamage)
            {
                shieldHp -= actualDamage;
            }
            else
            {
                actualDamage -= shieldHp;
                shieldHp = 0;
                currentHp -= actualDamage;
            }
        }
        else
        {
            currentHp -= actualDamage;
        }

        if (currentHp <= 0)
        {
            currentHp = 0;
            // Implement defeat logic if needed
            OnDeath?.Invoke(damage);
        }

        DamageIndicator indicator = Instantiate(damagePrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity, transform).GetComponent<DamageIndicator>();
        indicator.ShowDamage(actualDamage, damage.type);
        OnHpOrShieldChange?.Invoke();
    }

    public void HealDamage(int amount)
    {
        currentHp = Mathf.Min(currentHp + amount, GetMaxHp());
        OnHpOrShieldChange?.Invoke();
    }

    public void AddShield(int amount)
    {
        shieldHp += amount;
        OnHpOrShieldChange?.Invoke();
    }

    public void RemoveShield(int amount)
    {
        shieldHp = Mathf.Max(shieldHp - amount, 0);
        OnHpOrShieldChange?.Invoke();
    }

    public void GainExperience(int amount)
    {
        // If there's stored experience, convert it first
        if (storedExp > 0)
        {
            int convertedExp = Mathf.Min(amount, storedExp);
            storedExp -= convertedExp;
            amount -= convertedExp;
            currentExp += convertedExp;
        }

        // Gain real experience
        currentExp += amount;

        // Check for level up
        while (currentExp >= baseStats.GetExpForNextLevel(currentLevel) && currentLevel < 14)
        {
            LevelUp();
        }

        OnExpChange?.Invoke();
    }

    private void LevelUp()
    {
        currentExp -= baseStats.GetExpForNextLevel(currentLevel);
        currentLevel++;
        PokemonEvolution evolution = baseStats.IsNewEvoLevel(currentLevel);
        CheckEvolution();
        Debug.Log("Level Up! Current Level: " + currentLevel);
        // You can add additional logic here upon leveling up
        OnLevelChange?.Invoke();
    }

    public void StoreExperience(int amount)
    {
        storedExp += amount;
        OnExpChange?.Invoke();
    }

    private void CheckEvolution()
    {
        PokemonEvolution evolution = baseStats.IsNewEvoLevel(currentLevel);
        if (evolution != null)
        {
            Evolve(evolution);
        }
    }

    private void Evolve(PokemonEvolution evolution)
    {
        if (activeModel != null)
        {
            Destroy(activeModel);
        }
        activeModel = Instantiate(evolution.newModel, transform.position, transform.rotation, transform);
        OnEvolution?.Invoke();
    }
}
