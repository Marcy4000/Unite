using System;
using System.Collections.Generic;
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

    private Sprite portrait;

    private NetworkList<StatChange> statChanges;
    private List<float> statTimers = new List<float>();

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

    public Sprite Portrait { get { return portrait; } }

    public NetworkList<StatChange> StatChanges { get { return statChanges; } }

    public event Action OnHpOrShieldChange;
    public event Action OnLevelChange;
    public event Action OnExpChange;
    public event Action OnEvolution;
    public event Action<DamageInfo> OnDeath;
    public event Action<DamageInfo> OnDamageTaken;
    public event Action OnPokemonInitialized;

    public event Action OnStatChange;

    private DamageInfo lastHit;

    private void Awake()
    {
        statChanges = new NetworkList<StatChange>();
    }

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
        statChanges.OnListChanged += OnStatListChanged;
        CheckEvolution();
        OnPokemonInitialized?.Invoke();
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

    public int GetAttack()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Attack && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Attack && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueAtk = baseStats.Attack[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueAtk = Mathf.RoundToInt(trueAtk * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueAtk, 0, 9999);
    }

    public int GetDefense()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Defense && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Defense && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueDef = baseStats.Defense[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueDef = Mathf.RoundToInt(trueDef * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueDef, 0, 9999);
    }

    public int GetSpAttack()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.SpAttack && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.SpAttack && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueSpAtk = baseStats.SpAttack[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueSpAtk = Mathf.RoundToInt(trueSpAtk * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueSpAtk, 0, 9999);
    }

    public int GetSpDefense()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.SpDefense && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.SpDefense && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueSpDef = baseStats.SpDefense[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueSpDef = Mathf.RoundToInt(trueSpDef * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueSpDef, 0, 9999);
    }

    public int GetCritRate()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.CritRate && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.CritRate && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueCrit = baseStats.CritRate[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueCrit = Mathf.RoundToInt(trueCrit * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueCrit, 0, 9999);
    }

    public int GetCDR()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Cdr && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Cdr && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueCDR = baseStats.Cdr[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueCDR = Mathf.RoundToInt(trueCDR * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueCDR, 0, 9999);
    }

    public int GetLifeSteal()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.LifeSteal && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.LifeSteal && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueLifeSteal = baseStats.LifeSteal[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueLifeSteal = Mathf.RoundToInt(trueLifeSteal * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueLifeSteal, 0, 9999);
    }

    public float GetAtkSpeed()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.AtkSpeed && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.AtkSpeed && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        float trueAtkSpeed = baseStats.AtkSpeed[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueAtkSpeed = trueAtkSpeed * (1f + percentModifierSum / 100f);

        return trueAtkSpeed;
    }

    public int GetSpeed()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Speed && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.Speed && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pokémon's natural movement speed stat
        int trueSpeed = baseStats.Speed[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueSpeed = Mathf.RoundToInt(trueSpeed * (1f + percentModifierSum / 100f));

        // Movement speed efficacy tax
        if (trueSpeed < 0)
        {
            trueSpeed = 0;
        }
        else if (trueSpeed < 2500)
        {
            trueSpeed = Mathf.RoundToInt(trueSpeed * 0.5f + 1250);
        }
        else if (trueSpeed > 5000 && trueSpeed <= 5750) // No taxation for speeds between 2500 and 5000
        {
            trueSpeed = Mathf.RoundToInt((trueSpeed - 5000) * 0.8f + 5000);
        }
        else // trueSpeed > 5750
        {
            trueSpeed = Mathf.RoundToInt((trueSpeed - 5750) * 0.5f + (5750 - 5000) * 0.8f + 5000);
        }

        // Cap Effective MS to 9000
        trueSpeed = Mathf.Clamp(trueSpeed, 1250, 9000);

        return trueSpeed;
    }

    public void AddStatChange(StatChange change)
    {
        if (IsServer)
        {
            statChanges.Add(change);
            if (change.IsTimed)
            {
                statTimers.Add(change.Duration);
            }
            else
            {
                statTimers.Add(-1);
            }
            OnStatChange?.Invoke();
        }
        else
        {
            AddStatChangeRPC(change);
        }
    }

    [Rpc(SendTo.Server)]
    private void AddStatChangeRPC(StatChange change)
    {
        statChanges.Add(change);
        if (change.IsTimed)
        {
            statTimers.Add(change.Duration);
        }
        else
        {
            statTimers.Add(-1);
        }
    }

    public void RemoveStatChangeWithID(short id)
    {
        if (IsServer)
        {
            for (int i = 0; i < statChanges.Count; i++)
            {
                if (statChanges[i].ID == id)
                {
                    statChanges.RemoveAt(i);
                    statTimers.RemoveAt(i);
                    return;
                }
            }
        }
        else
        {
            RemoveStatChangeWithIDRPC(id);
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveStatChangeWithIDRPC(short id)
    {
        for (int i = 0; i < statChanges.Count; i++)
        {
            if (statChanges[i].ID == id)
            {
                statChanges.RemoveAt(i);
                statTimers.RemoveAt(i);
                return;
            }
        }
    }

    private void OnStatListChanged(NetworkListEvent<StatChange> changeEvent)
    {
        OnStatChange?.Invoke();
    }

    private void Update()
    {
        if (IsServer)
        {
            for (int i = statChanges.Count; i > 0; i--)
            {
                int index = i - 1;
                StatChange change = statChanges[index];
                if (change.IsTimed)
                {
                    statTimers[index] -= Time.deltaTime;
                    if (statTimers[index] <= 0)
                    {
                        statChanges.RemoveAt(index);
                        statTimers.RemoveAt(index);
                    }
                }
            }
        }

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

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            AddStatChange(new StatChange(1000, Stat.Speed, 5, true, false, false, 0));
        }
    }

    public void TakeDamage(DamageInfo damage)
    {
        lastHit = damage;
        int atkStat;
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[damage.attackerId].GetComponent<Pokemon>();
        int localHp = currentHp.Value;
        int localShield = shieldHp.Value;

        switch (damage.type)
        {
            case DamageType.True:
            case DamageType.Physical:
                atkStat = attacker.GetAttack();
                break;
            case DamageType.Special:
                atkStat = attacker.GetSpAttack();
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
                defStat = GetDefense();
                break;
            case DamageType.Special:
                defStat = GetSpDefense();
                break;
            case DamageType.True:
            default:
                defStat = 0;
                break;
        }

        int actualDamage = Mathf.FloorToInt((float)attackDamage * 600 / (600 + defStat));

        if (localShield > 0)
        {
            if (localShield >= actualDamage)
            {
                localShield -= actualDamage;
            }
            else
            {
                actualDamage -= localShield;
                localShield = 0;
                localHp -= actualDamage;
            }
        }
        else
        {
            localHp -= actualDamage;
        }

        if (localHp <= 0)
        {
            localHp = 0;
        }

        if (IsServer)
        {
            currentHp.Value = localHp;
            shieldHp.Value = localShield;
        }
        else
        {
            SetCurrentHPServerRPC(localHp);
            SetShieldServerRPC(localShield);
        }

        OnDamageTakenRpc(damage);
        ClientDamageRpc(actualDamage, damage);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ClientDamageRpc(int actualDamage, DamageInfo damage)
    {
        lastHit = damage;
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

    public void SetShield(int amount)
    {
        if (amount < 0)
        {
            return;
        }

        if (IsServer)
        {
            shieldHp.Value = amount;
        }
        else
        {
            SetShieldServerRPC(amount);
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

        float hpPercentage = (float)currentHp.Value / GetMaxHp();

        localExp -= baseStats.GetExpForNextLevel(localLevel);
        localLevel++;
        Debug.Log("Level Up! Current Level: " + localLevel);

        if (IsServer)
        {
            currentHp.Value = Mathf.RoundToInt(baseStats.MaxHp[localLevel] * hpPercentage);
        }
        else
        {
            SetCurrentHPServerRPC(Mathf.RoundToInt(baseStats.MaxHp[localLevel] * hpPercentage));
        }
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
        portrait = evolution.newSprite;
        OnEvolution?.Invoke();
    }
}
