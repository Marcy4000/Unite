using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pokemon : NetworkBehaviour
{
    [SerializeField] private PokemonBase baseStats;
    [SerializeField] private GameObject damagePrefab;
    private NetworkVariable<int> currentHp = new NetworkVariable<int>();
    private NetworkVariable<int> shieldHp = new NetworkVariable<int>();
    private NetworkVariable<int> currentLevel = new NetworkVariable<int>();
    private NetworkVariable<int> currentExp = new NetworkVariable<int>();
    private NetworkVariable<int> storedExp = new NetworkVariable<int>();

    private PokemonType type;

    private GameObject activeModel;

    public NetworkVariable<int> CurrentHp { get { return currentHp; } }
    public NetworkVariable<int> ShieldHp { get { return shieldHp; } }
    public NetworkVariable<int> CurrentLevel { get { return currentLevel; } }
    public NetworkVariable<int> CurrentExp { get { return currentExp; } }
    public NetworkVariable<int> StoredExp { get { return storedExp; } }

    public PokemonType Type { get { return type; } set { type = value; } }

    public PokemonBase BaseStats { get { return baseStats; } }

    public GameObject ActiveModel { get { return activeModel; } }

    public event Action OnHpOrShieldChange;
    public event Action OnLevelChange;
    public event Action OnExpChange;
    public event Action OnEvolution;
    public event Action<DamageInfo> OnDeath;

    public override void OnNetworkSpawn()
    {
        InitializePokemon();
    }

    public void GainPassiveExp(int amount)
    {
        if (baseStats.IsLevelBeforeEvo(currentLevel.Value))
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
        currentHp.Value = GetMaxHp();
        shieldHp.Value = 0;
        currentExp.Value = 0;
        currentHp.OnValueChanged += (previous, current) => OnHpOrShieldChange?.Invoke();
        shieldHp.OnValueChanged += (previous, current) => OnHpOrShieldChange?.Invoke();
        currentExp.OnValueChanged += (previous, current) => OnExpChange?.Invoke();
        currentLevel.OnValueChanged += (previous, current) => OnLevelChange?.Invoke();
        CheckEvolution();
    }

    public int GetMaxHp()
    {
        return baseStats.MaxHp[currentLevel.Value];
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
                atkStat = damage.attacker.baseStats.Attack[damage.attacker.CurrentLevel.Value];
                break;
            case DamageType.Special:
                atkStat = damage.attacker.baseStats.SpAttack[damage.attacker.CurrentLevel.Value];
                break;
            default:
                atkStat = 0;
                break;
        }

        int attackDamage = Mathf.FloorToInt(damage.ratio * atkStat + damage.slider * damage.attacker.CurrentLevel.Value + damage.baseDmg);
        int defStat;
        switch (damage.type)
        {
            case DamageType.Physical:
                defStat = baseStats.Defense[currentLevel.Value];
                break;
            case DamageType.Special:
                defStat = baseStats.SpDefense[currentLevel.Value];
                break;
            case DamageType.True:
            default:
                defStat = 0;
                break;
        }

        int actualDamage = Mathf.FloorToInt((float)attackDamage * 600 / (600 + defStat));

        if (shieldHp.Value > 0)
        {
            if (shieldHp.Value >= actualDamage)
            {
                RemoveShield(actualDamage);
            }
            else
            {
                actualDamage -= shieldHp.Value;
                RemoveShield(shieldHp.Value);
                if (IsServer) {
                    currentHp.Value -= actualDamage;
                } else {
                    SetCurrentHPServerRPC(currentHp.Value - actualDamage);
                }
            }
        }
        else
        {
            if (IsServer) {
                currentHp.Value -= actualDamage;
            } else {
                SetCurrentHPServerRPC(currentHp.Value - actualDamage);
            }
        }

        if (currentHp.Value <= 0)
        {
            if (IsServer) {
                currentHp.Value = 0;
            } else {
                SetCurrentHPServerRPC(0);
            }
            // Implement defeat logic if needed
            OnDeath?.Invoke(damage);
        }

        DamageIndicator indicator = Instantiate(damagePrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity, transform).GetComponent<DamageIndicator>();
        indicator.ShowDamage(actualDamage, damage.type);
    }

    public void HealDamage(int amount)
    {
        if (IsServer)
        {
            currentHp.Value = Mathf.Min(currentHp.Value + amount, GetMaxHp());
        }
        else
        {
            SetCurrentHPServerRPC(Mathf.Min(currentHp.Value + amount, GetMaxHp()));
        }
    }

    public void AddShield(int amount)
    {
        if (IsServer)
        {
            shieldHp.Value += amount;
        }
        else
        {
            SetShieldServerRPC(shieldHp.Value + amount);
        }
    }

    public void RemoveShield(int amount)
    {
        if (IsServer)
        {
            shieldHp.Value = Mathf.Max(shieldHp.Value - amount, 0);
        }
        else
        {
            SetShieldServerRPC(Mathf.Max(shieldHp.Value - amount, 0));
        }
    }

    public void GainExperience(int amount)
    {
        if (!IsOwner)
        {
            return;
        }

        // If there's stored experience, convert it first
        int currExp = currentExp.Value;

        if (storedExp.Value > 0)
        {
            int convertedExp = Mathf.Min(amount, storedExp.Value);
            if (IsServer) {
                storedExp.Value -= convertedExp;
            } else {
                SetStoredExpServerRPC(storedExp.Value - convertedExp);
            }
            amount -= convertedExp;
            currExp += convertedExp;
        }

        // Gain real experience
        SetCurrentEXPServerRPC(currExp + amount);

        // Check for level up
        while (currentExp.Value >= baseStats.GetExpForNextLevel(currentLevel.Value) && currentLevel.Value < 14)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsServer) {
            currentExp.Value -= baseStats.GetExpForNextLevel(currentLevel.Value);
            currentLevel.Value++;
        } else {
            SetCurrentEXPServerRPC(currentExp.Value - baseStats.GetExpForNextLevel(currentLevel.Value));
            SetCurrentLevelServerRPC(currentLevel.Value + 1);
        }
        PokemonEvolution evolution = baseStats.IsNewEvoLevel(currentLevel.Value);
        CheckEvolution();
        Debug.Log("Level Up! Current Level: " + currentLevel);
        // You can add additional logic here upon leveling up
    }

    [Rpc(SendTo.Server)]
    private void SetCurrentHPServerRPC(int amount)
    {
        currentHp.Value = amount;
    }

    [Rpc(SendTo.Server)]
    private void SetShieldServerRPC(int amount)
    {
        shieldHp.Value = amount;
    }

    [Rpc(SendTo.Server)]
    private void SetCurrentEXPServerRPC(int amount)
    {
        currentExp.Value = amount;
    }

    [Rpc(SendTo.Server)]
    private void SetCurrentLevelServerRPC(int amount)
    {
        currentLevel.Value = amount;
    }

    [Rpc(SendTo.Server)]
    private void SetStoredExpServerRPC(int amount)
    {
        storedExp.Value = amount;
    }

    public void StoreExperience(int amount)
    {
        storedExp.Value += amount;
    }

    private void CheckEvolution()
    {
        PokemonEvolution evolution = baseStats.IsNewEvoLevel(currentLevel.Value);
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
