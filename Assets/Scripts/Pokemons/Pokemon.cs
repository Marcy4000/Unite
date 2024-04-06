using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pokemon : NetworkBehaviour
{
    [SerializeField] private GameObject damagePrefab;
    private PokemonBase baseStats;
    private NetworkVariable<int> currentHp = new NetworkVariable<int>();
    private NetworkVariable<int> shieldHp = new NetworkVariable<int>();
    private NetworkVariable<int> currentLevel = new NetworkVariable<int>();
    private NetworkVariable<int> currentExp = new NetworkVariable<int>();
    private NetworkVariable<int> storedExp = new NetworkVariable<int>();
    private int localExp;
    private int localStoredExp;
    private int localLevel;

    private PokemonType type;

    private GameObject activeModel;

    public NetworkVariable<int> CurrentHp { get { return currentHp; } }
    public NetworkVariable<int> ShieldHp { get { return shieldHp; } }
    public NetworkVariable<int> CurrentLevel { get { return currentLevel; } }
    public NetworkVariable<int> CurrentExp { get { return currentExp; } }
    public NetworkVariable<int> StoredExp { get { return storedExp; } }

    public int LocalExp { get { return localExp; } }
    public int LocalStoredExp { get { return localStoredExp; } }
    public int LocalLevel { get { return localLevel; } }

    public PokemonType Type { get { return type; } set { type = value; } }

    public PokemonBase BaseStats { get { return baseStats; } }

    public GameObject ActiveModel { get { return activeModel; } }

    public event Action OnHpOrShieldChange;
    public event Action OnLevelChange;
    public event Action OnExpChange;
    public event Action OnEvolution;
    public event Action<DamageInfo> OnDeath;
    public event Action<DamageInfo> OnDamageTaken;

    private DamageInfo lastHit;

    public void GainPassiveExp(int amount)
    {
        if (!IsOwner)
        {
            return;
        }

        if (baseStats.IsLevelBeforeEvo(localLevel))
        {
            StoreExperience(amount);
        }
        else
        {
            GainExperience(amount);
        }
    }

    public void SetNewPokemon(PokemonBase pokemonBase)
    {
        baseStats = pokemonBase;
        InitializePokemon();
    }

    public void InitializePokemon()
    {
        localExp = 0;
        if (IsServer) {
            currentHp.Value = GetMaxHp();
            shieldHp.Value = 0;
            currentExp.Value = 0;
        } else {
            SetCurrentHPServerRPC(GetMaxHp());
            SetCurrentEXPServerRPC(0);
            SetShieldServerRPC(0);
        }
        currentHp.OnValueChanged += CurrentHpChanged;
        shieldHp.OnValueChanged += (previous, current) => OnHpOrShieldChange?.Invoke();
        storedExp.OnValueChanged += StoredExpChanged;
        currentExp.OnValueChanged += CurrentExpValueChanged;
        currentLevel.OnValueChanged += CurrentLevelChanged;
        CheckEvolution();
    }

    private void CurrentHpChanged(int previous, int current)
    {
        if (current <= 0)
        {
            OnDeath?.Invoke(lastHit);
        }
        OnHpOrShieldChange?.Invoke();
    }

    private void CurrentExpValueChanged(int previous, int current)
    {
        localExp = current;
        OnExpChange?.Invoke();
    }

    private void CurrentLevelChanged(int previous, int current)
    {
        localLevel = current;
        CheckEvolution();
        OnLevelChange?.Invoke();
    }

    private void StoredExpChanged(int previous, int current)
    {
        localStoredExp = current;
        OnExpChange?.Invoke();
    }

    public int GetMaxHp()
    {
        return baseStats.MaxHp[currentLevel.Value];
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TakeDamage(new DamageInfo(NetworkObjectId, 3.45f, 32, 820, DamageType.Physical));
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
        lastHit = damage;
        int atkStat;
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[damage.attackerId].GetComponent<Pokemon>();

        switch (damage.type)
        {
            case DamageType.True:
            case DamageType.Physical:
                atkStat = attacker.baseStats.Attack[attacker.CurrentLevel.Value];
                break;
            case DamageType.Special:
                atkStat = attacker.baseStats.SpAttack[attacker.CurrentLevel.Value];
                break;
            default:
                atkStat = 0;
                break;
        }

        int attackDamage = Mathf.FloorToInt(damage.ratio * atkStat + damage.slider * attacker.CurrentLevel.Value + damage.baseDmg);
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
        }

        OnDamageTakenRpc(damage);
        ClientDamageRpc(actualDamage, damage);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ClientDamageRpc(int actualDamage, DamageInfo damage)
    {
        DamageIndicator indicator = Instantiate(damagePrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity, transform).GetComponent<DamageIndicator>();
        indicator.ShowDamage(actualDamage, damage.type);
    }

    [Rpc(SendTo.Owner)]
    private void OnDamageTakenRpc(DamageInfo damage)
    {
        OnDamageTaken?.Invoke(damage);
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
        if (localStoredExp > 0)
        {
            int convertedExp = Mathf.Min(amount, localStoredExp);
            localStoredExp -= convertedExp;
            amount -= convertedExp;
            localExp += convertedExp;
        }

        // Gain real experience
        
        localExp += amount;

        // Check for level up
        while (localExp >= baseStats.GetExpForNextLevel(localLevel) && localLevel < 14)
        {
            LevelUp();
        }

        if (IsServer) {
            currentExp.Value = localExp;
            storedExp.Value = localStoredExp;
            currentLevel.Value = localLevel;
        } else {
            SetCurrentEXPServerRPC(localExp);
            SetStoredExpServerRPC(localStoredExp);
            SetCurrentLevelServerRPC(localLevel);
        }
    }

    private void LevelUp()
    {
        if (!IsOwner)
        {
            return;
        }

        localExp -= baseStats.GetExpForNextLevel(localLevel);
        localLevel++;
        Debug.Log("Level Up! Current Level: " + currentLevel.Value);
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
        if (IsServer) {
            storedExp.Value += amount;
        } else {
            SetStoredExpServerRPC(storedExp.Value + amount);
        }
    }

    private void CheckEvolution()
    {
        PokemonEvolution evolution = baseStats.IsNewEvoLevel(localLevel);
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
