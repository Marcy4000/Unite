using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pokemon : NetworkBehaviour
{
    [SerializeField] private GameObject damagePrefab;
    private PokemonBase baseStats;
    private NetworkVariable<int> currentHp = new NetworkVariable<int>();
    private NetworkVariable<int> currentLevel = new NetworkVariable<int>();
    private NetworkVariable<int> currentExp = new NetworkVariable<int>();
    private NetworkVariable<int> storedExp = new NetworkVariable<int>();

    private NetworkList<ShieldInfo> shields;
    private List<float> shieldTimers = new List<float>();

    private int localExp;
    private int localStoredExp;
    private int localLevel;

    private PokemonType type;

    private GameObject activeModel;

    private Sprite portrait;

    private NetworkList<StatChange> statChanges;
    private List<float> statTimers = new List<float>();

    private NetworkList<StatusEffect> statusEffects;
    private List<float> statusTimers = new List<float>();

    // This is so stupid, only implemented for meowstic's wonder room and likely has no other use otherwise
    private NetworkVariable<bool> flipAtkStat = new NetworkVariable<bool>();

    private float outOfCombatTimer = 0;
    private NetworkVariable<bool> isOutOfCombat = new NetworkVariable<bool>();

    public int CurrentHp { get { return currentHp.Value; } }
    public int ShieldHp { get { return GetShieldsAsInt(); } }
    public int CurrentLevel { get { return currentLevel.Value; } }
    public int CurrentExp { get { return currentExp.Value; } }
    public int StoredExp { get { return storedExp.Value; } }

    public NetworkList<ShieldInfo> Shields { get { return shields; } }

    public int LocalExp { get { return localExp; } }
    public int LocalStoredExp { get { return localStoredExp; } }
    public int LocalLevel { get { return localLevel; } }

    public bool SwitchAtkStat { get { return flipAtkStat.Value; } }

    public bool IsOutOfCombat { get { return isOutOfCombat.Value; } }

    public PokemonType Type { get { return type; } set { type = value; } }

    public PokemonBase BaseStats { get { return baseStats; } }

    public GameObject ActiveModel { get { return activeModel; } }

    public Sprite Portrait { get { return portrait; } }

    public NetworkList<StatChange> StatChanges { get { return statChanges; } }
    public NetworkList<StatusEffect> StatusEffects { get { return statusEffects; } }

    public event Action OnHpOrShieldChange;
    public event Action OnLevelChange;
    public event Action OnExpChange;
    public event Action OnEvolution;
    public event Action<DamageInfo> OnDeath;
    public event Action<DamageInfo> OnDamageTaken;
    public event Action OnPokemonInitialized;

    public event Action<ulong, DamageInfo> OnDamageDealt;
    public event Action<ulong> OnOtherPokemonKilled;

    public event Action<NetworkListEvent<StatChange>> OnStatChange;
    public event Action<StatusEffect, bool> OnStatusChange;

    public event Action<Vector3, float> OnKnockback;
    public event Action<float, float> OnKnockup;

    private DamageInfo lastHit;

    private void Awake()
    {
        statChanges = new NetworkList<StatChange>();
        statusEffects = new NetworkList<StatusEffect>();
        shields = new NetworkList<ShieldInfo>();
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
            currentExp.Value = 0;
            shields.Clear();
            shieldTimers.Clear();
        } else {
            SetCurrentHPServerRPC(GetMaxHp());
            SetCurrentEXPServerRPC(0);
            ClearShieldsRPC();
        }
        FlipAtkStatsRPC(false);

        currentHp.OnValueChanged += CurrentHpChanged;
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

    [Rpc(SendTo.Everyone)]
    private void OnShieldListChangedRPC()
    {
        StartCoroutine(ShieldListChanged());
    }

    private IEnumerator ShieldListChanged()
    {
        yield return null;
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

    private void OnStatListChanged(NetworkListEvent<StatChange> changeEvent)
    {
        OnStatChange?.Invoke(changeEvent);
    }

    #region StatsGetters

    public int GetMaxHp()
    {
        return GetMaxHp(currentLevel.Value);
    }

    public int GetMaxHp(int level)
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.MaxHp && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.MaxHp && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueHp = baseStats.MaxHp[level] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueHp = Mathf.RoundToInt(trueHp * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueHp, 0, 99999);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueAtk = baseStats.Attack[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueAtk = Mathf.RoundToInt(trueAtk * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueAtk, 0, 2000);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueDef = baseStats.Defense[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueDef = Mathf.RoundToInt(trueDef * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueDef, 0, 999999);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueSpAtk = baseStats.SpAttack[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueSpAtk = Mathf.RoundToInt(trueSpAtk * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueSpAtk, 0, 3000);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueSpDef = baseStats.SpDefense[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueSpDef = Mathf.RoundToInt(trueSpDef * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueSpDef, 0, 999999);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueCrit = baseStats.CritRate[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueCrit = Mathf.RoundToInt(trueCrit * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueCrit, 0, 100);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueCDR = baseStats.Cdr[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueCDR = Mathf.RoundToInt(trueCDR * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueCDR, 0, 60);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        int trueLifeSteal = baseStats.LifeSteal[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueLifeSteal = Mathf.RoundToInt(trueLifeSteal * (1f + percentModifierSum / 100f));

        return Mathf.Clamp(trueLifeSteal, 0, 100);
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        float trueAtkSpeed = baseStats.AtkSpeed[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueAtkSpeed = trueAtkSpeed * (1f + percentModifierSum / 100f);

        return trueAtkSpeed;
    }

    // Thanks to the unite mathcord for the publishing <3
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

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
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
        else if (trueSpeed > 5750)
        {
            trueSpeed = Mathf.RoundToInt((trueSpeed - 5750) * 0.5f + (5750 - 5000) * 0.8f + 5000);
        }

        // Cap Effective MS to 9000
        trueSpeed = Mathf.Clamp(trueSpeed, 1250, 9000);

        return trueSpeed;
    }

    public float GetDamageReduction()
    {
        // Step 1: Sum together all flat modifiers
        float flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.DamageReduction && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        flatModifierSum /= 100f;

        if (flatModifierSum > 1f)
        {
            flatModifierSum = 1f;
        }

        return flatModifierSum;
    }

    public int GetDefensePenetration()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.DefensePenetration && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        Mathf.Clamp(flatModifierSum, 0, 100);

        return flatModifierSum;
    }

    public float GetCritModifier()
    {
        // Step 1: Sum together all flat modifiers
        float flatModifierSum = 100;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.CritModifier && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        flatModifierSum = Mathf.Clamp(flatModifierSum, 0, 400);

        flatModifierSum /= 100f;

        return flatModifierSum;
    }

    #endregion

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
            NetworkListEvent<StatChange> networkListEvent = new NetworkListEvent<StatChange>();
            networkListEvent.PreviousValue = change;
            networkListEvent.Value = change;
            networkListEvent.Type = NetworkListEvent<StatChange>.EventType.Add;
            networkListEvent.Index = statChanges.Count - 1;
            OnStatChange?.Invoke(networkListEvent);
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

    [Rpc(SendTo.Server)]
    public void RemoveStatChangeWithIDRPC(ushort id)
    {
        for (int i = 0; i < statChanges.Count; i++)
        {
            if (statChanges[i].ID == id)
            {
                if (statChanges[i].CanBeRemoved)
                {
                    statChanges.RemoveAt(i);
                    statTimers.RemoveAt(i);
                }
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveAllStatChangeWithIDRPC(ushort id)
    {
        for (int i = statChanges.Count; i > 0; i--)
        {
            int index = i - 1;
            if (statChanges[index].ID == id)
            {
                if (statChanges[index].CanBeRemoved)
                {
                    statChanges.RemoveAt(index);
                    statTimers.RemoveAt(index);
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveStatChangeRPC(StatChange change)
    {
        for (int i = 0; i < statChanges.Count; i++)
        {
            if (statChanges[i].Equals(change))
            {
                if (statChanges[i].CanBeRemoved)
                {
                    statChanges.RemoveAt(i);
                    statTimers.RemoveAt(i);
                }
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveAllStatChangesRPC()
    {
        for (int i = statChanges.Count; i > 0; i--)
        {
            int index = i - 1;
            if (statChanges[index].CanBeRemoved)
            {
                statChanges.RemoveAt(index);
                statTimers.RemoveAt(index);
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveAllStatusEffectsRPC()
    {
        statusEffects.Clear();
        statusTimers.Clear();
    }

    public int GetStatChange(Stat affectedStat)
    {
        int result = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == affectedStat)
            {
                result += change.IsBuff ? change.Amount : -change.Amount;
            }
        }
        return result;
    }

    public void AddStatusEffect(StatusEffect effect)
    {
        AddStatusEffectRPC(effect);
    }

    [Rpc(SendTo.Server)]
    private void AddStatusEffectRPC(StatusEffect effect)
    {
        if (effect.IsNegativeStatus())
        {
            if (HasStatusEffect(StatusType.Unstoppable) || HasStatusEffect(StatusType.Invincible))
            {
                return;
            }
            else if (HasStatusEffect(StatusType.HindranceResistance))
            {
                AddStatChange(new StatChange(20, Stat.Speed, 3, true, false, true, 0));
                return;
            }
        }
        else if (effect.Type == StatusType.Unstoppable)
        {
            foreach (var loopEffect in statusEffects)
            {
                if (loopEffect.IsNegativeStatus())
                {
                    statusTimers.RemoveAt(statusEffects.IndexOf(loopEffect));
                    statusEffects.Remove(loopEffect);
                }
            }
        }

        if (HasStatusEffect(effect.Type))
        {
            statusTimers[statusEffects.IndexOf(effect)] += effect.Duration;
        }
        else
        {
            statusEffects.Add(effect);
            if (effect.IsTimed)
            {
                statusTimers.Add(effect.Duration);
            }
            else
            {
                statusTimers.Add(-1);
            }
            OnStatusListChangedRPC(effect, true);
        }
    }

    public void RemoveStatusEffectWithID(ushort id)
    {
        if (IsServer)
        {
            for (int i = 0; i < statusEffects.Count; i++)
            {
                if (statusEffects[i].ID == id)
                {
                    OnStatusListChangedRPC(statusEffects[i], false);
                    statusEffects.RemoveAt(i);
                    statusTimers.RemoveAt(i);
                    return;
                }
            }
        }
        else
        {
            RemoveStatusEffectWithIDRPC(id);
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveStatusEffectRPC(StatusEffect effect)
    {
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffects[i].Equals(effect))
            {
                OnStatusListChangedRPC(effect, false);
                statusEffects.RemoveAt(i);
                statusTimers.RemoveAt(i);
                return;
            }
        }
    }

        [Rpc(SendTo.Server)]
    private void RemoveStatusEffectWithIDRPC(ushort id)
    {
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffects[i].ID == id)
            {
                OnStatusListChangedRPC(statusEffects[i], false);
                statusEffects.RemoveAt(i);
                statusTimers.RemoveAt(i);
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdateStatusEffectTimeRPC(StatusEffect status, float newTime)
    {
        foreach (var effect in statusEffects)
        {
            if (effect.Equals(status))
            {
                statusTimers[statusEffects.IndexOf(effect)] = newTime;
                return;
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnStatusListChangedRPC(StatusEffect effect, bool added)
    {
        OnStatusChange?.Invoke(effect, added);
    }

    [Rpc(SendTo.Server)]
    public void ClearStatusEffectsRPC()
    {
        statusEffects.Clear();
        statusTimers.Clear();
    }

    public bool HasStatusEffect(StatusType type)
    {
        foreach (StatusEffect effect in statusEffects)
        {
            if (effect.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasStatusEffect(ushort id)
    {

        foreach (StatusEffect effect in statusEffects)
        {
            if (effect.ID == id)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasAnyStatusEffect(StatusType[] types)
    {
        foreach (StatusEffect effect in statusEffects)
        {
            foreach (var type in types)
            {
                if (effect.Type == type)
                {
                    return true;
                }
            }
        }

        return false;
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

            for (int i = statusEffects.Count; i > 0; i--)
            {
                int index = i - 1;
                StatusEffect effect = statusEffects[index];
                if (effect.IsTimed)
                {
                    statusTimers[index] -= Time.deltaTime;
                    if (statusTimers[index] <= 0)
                    {
                        statusEffects.RemoveAt(index);
                        statusTimers.RemoveAt(index);
                        OnStatusListChangedRPC(effect, false);
                    }
                }
            }

            for (int i = shields.Count; i > 0; i--)
            {
                int index = i - 1;
                ShieldInfo shield = shields[index];
                if (shield.IsTimed)
                {
                    shieldTimers[index] -= Time.deltaTime;
                    if (shieldTimers[index] <= 0)
                    {
                        shields.RemoveAt(index);
                        shieldTimers.RemoveAt(index);
                        OnShieldListChangedRPC();
                    }
                }
            }

            if (outOfCombatTimer > 0)
            {
                outOfCombatTimer -= Time.deltaTime;
            }

            isOutOfCombat.Value = outOfCombatTimer < 0;
        }

        if (!IsOwner)
        {
            return;
        }

        // TODO: remove all this debug stuff once ready for release
        // Update: Only available in debug builds now
#if DEVELOPMENT_BUILD || UNITY_EDITOR
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
            AddShieldRPC(new ShieldInfo(300, 6969, 0, 3, true));
        }
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            RemoveShieldWithIDRPC(6969);
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            GainExperience(100);
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            AddStatChange(new StatChange(1000, Stat.Speed, 5, true, false, false, 0));
        }

        if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            AddStatusEffect(new StatusEffect(StatusType.VisionObscuring, 3f, true, 0));
        }
#endif
    }

    public void TakeDamage(DamageInfo damage)
    {
        if (HasStatusEffect(StatusType.Invincible) || currentHp.Value <= 0)
        {
            return;
        }

        lastHit = damage;

        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[damage.attackerId].GetComponent<Pokemon>();

        int localHp = currentHp.Value;
        List<ShieldInfo> localShields = GetShieldsAsList();

        if (damage.proprieties.HasFlag(DamageProprieties.CanCrit))
        {
            if (UnityEngine.Random.Range(0, 100) < attacker.GetCritRate())
            {
                damage.proprieties |= DamageProprieties.WasCriticalHit;
            }
        }

        int actualDamage = CalculateDamage(damage, attacker);

        int damageRemainder;

        if (damage.type != DamageType.True)
        {
            List<ShieldInfo> remainingShields = TakeDamageFromShields(localShields.ToArray(), actualDamage, out damageRemainder);
            UpdateShieldListRPC(remainingShields.ToArray());
        }
        else
        {
            damageRemainder = actualDamage;
        }

        if (damageRemainder > 0)
        {
            localHp -= damageRemainder;
        }

        if (localHp <= 0)
        {
            localHp = 0;
            attacker.OnKilledPokemonRPC(NetworkObjectId);
        }

        if (IsServer)
        {
            currentHp.Value = localHp;
        }
        else
        {
            SetCurrentHPServerRPC(localHp);
        }

        ClientDamageRpc(actualDamage, damage);
        attacker.OnDamageDealtRPC(NetworkObjectId, damage);
    }

    public int CalculateDamage(DamageInfo damage)
    {
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[damage.attackerId].GetComponent<Pokemon>();

        return CalculateDamage(damage, attacker);
    }

    public int CalculateDamage(DamageInfo damage, Pokemon attacker)
    {
        int atkStat;

        if (attacker.SwitchAtkStat)
        {
            switch (damage.type)
            {
                case DamageType.Physical:
                    damage.type = DamageType.Special;
                    break;
                case DamageType.Special:
                    damage.type = DamageType.Physical;
                    break;
                default:
                    break;
            }
        }

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

        int attackDamage = Mathf.FloorToInt(damage.ratio * atkStat + damage.slider * attacker.CurrentLevel + damage.baseDmg);

        if (damage.proprieties.HasFlag(DamageProprieties.WasCriticalHit))
        {
            attackDamage += Mathf.FloorToInt(attackDamage * attacker.GetCritModifier());
        }

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

        defStat = Mathf.Max(Mathf.FloorToInt(defStat * (1 - (attacker.GetDefensePenetration() / 100f))), 0);

        float finalDamage = (float)attackDamage * 600 / (600 + defStat);

        return Mathf.FloorToInt(finalDamage * (1f-GetDamageReduction()));
    }

    public List<ShieldInfo> TakeDamageFromShields(ShieldInfo[] shields, int damage, out int remainder)
    {
        // Copy the original shields to the result list
        List<ShieldInfo> resultShields = new List<ShieldInfo>(shields);

        // Sort a copy of the shields array by priority in descending order
        ShieldInfo[] sortedShields = shields.OrderByDescending(s => s.Priority).ToArray();

        // Iterate over the sorted shields and apply damage
        int remainingDamage = damage;
        foreach (var shield in sortedShields)
        {
            for (int i = 0; i < resultShields.Count; i++)
            {
                if (resultShields[i].Equals(shield))
                {
                    if (remainingDamage > 0)
                    {
                        ShieldInfo updatedShield = resultShields[i];
                        if (updatedShield.Amount > remainingDamage)
                        {
                            updatedShield.Amount -= remainingDamage;
                            remainingDamage = 0;
                        }
                        else
                        {
                            remainingDamage -= updatedShield.Amount;
                            updatedShield.Amount = 0;
                        }
                        resultShields[i] = updatedShield;
                    }
                }
            }
            if (remainingDamage <= 0)
            {
                break;
            }
        }

        // Set the remainder to the remaining damage after applying to all shields
        remainder = remainingDamage;

        // Return the updated list of shields
        return resultShields;
    }

    [Rpc(SendTo.Everyone)]
    private void ClientDamageRpc(int actualDamage, DamageInfo damage)
    {
        lastHit = damage;
        DamageIndicator indicator = Instantiate(damagePrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity, transform).GetComponent<DamageIndicator>();
        indicator.ShowDamage(actualDamage, damage.type, damage.proprieties.HasFlag(DamageProprieties.WasCriticalHit));

        if (IsServer)
        {
            outOfCombatTimer = 5f;
        }

        OnDamageTaken?.Invoke(damage);
    }

    [Rpc(SendTo.Everyone)]
    private void ClientHealRpc(int actualHeal)
    {
        DamageIndicator indicator = Instantiate(damagePrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity, transform).GetComponent<DamageIndicator>();
        indicator.ShowHeal(actualHeal);
    }

    public void HealDamage(int amount)
    {
        int oldHp = currentHp.Value;
        int newHp = Mathf.Min(oldHp + amount, GetMaxHp());

        if (IsServer)
        {
            currentHp.Value = newHp;
        }
        else
        {
            SetCurrentHPServerRPC(newHp);
        }

        int healAmount = newHp - oldHp;

        ClientHealRpc(healAmount);
    }

    public void HealDamage(DamageInfo healInfo)
    {
        int atkStat;
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[healInfo.attackerId].GetComponent<Pokemon>();
        int localHp = currentHp.Value;

        switch (healInfo.type)
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

        int healAmount = Mathf.FloorToInt(healInfo.ratio * atkStat + healInfo.slider * attacker.CurrentLevel + healInfo.baseDmg);

        localHp = Mathf.Min(localHp + healAmount, GetMaxHp());

        if (IsServer)
        {
            currentHp.Value = localHp;
        }
        else
        {
            SetCurrentHPServerRPC(localHp);
        }

        ClientHealRpc(healAmount);
    }

    [Rpc(SendTo.Server)]
    public void AddShieldRPC(ShieldInfo info)
    {
        if (HasShieldWithID(info.ID))
        {
            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i].ID == info.ID)
                {
                    shields[i] = new ShieldInfo(shields[i].Amount+info.Amount, info.ID, shields[i].Priority, shields[i].Duration, shields[i].IsTimed);
                    shieldTimers[i] += info.Duration;
                    OnShieldListChangedRPC();
                    return;
                }
            }
        }

        shields.Add(info);
        if (info.IsTimed)
        {
            shieldTimers.Add(info.Duration);
        }
        else
        {
            shieldTimers.Add(-1);
        }
        OnShieldListChangedRPC();
    }

    [Rpc(SendTo.Server)]
    public void RemoveShieldWithIDRPC(ushort id)
    {
        for (int i = 0; i < shields.Count; i++)
        {
            if (shields[i].ID == id)
            {
                shields.RemoveAt(i);
                shieldTimers.RemoveAt(i);
                OnShieldListChangedRPC();
                return;
            }
        }
    }

    public bool HasShieldWithID(ushort id)
    {
        foreach (ShieldInfo shield in shields)
        {
            if (shield.ID == id)
            {
                return true;
            }
        }

        return false;
    }

    public ShieldInfo? GetShieldWithID(ushort id)
    {
        foreach (ShieldInfo shield in shields)
        {
            if (shield.ID == id)
            {
                return shield;
            }
        }

        return null;
    }

    private List<ShieldInfo> GetShieldsAsList()
    {
        List<ShieldInfo> shieldList = new List<ShieldInfo>();

        foreach (ShieldInfo shield in shields)
        {
            shieldList.Add(shield);
        }

        return shieldList;
    }

    private int GetShieldsAsInt()
    {
        int shieldAmount = 0;

        foreach (ShieldInfo shield in shields)
        {
            shieldAmount += shield.Amount;
        }

        return shieldAmount;
    }

    [Rpc(SendTo.Server)]
    private void UpdateShieldListRPC(ShieldInfo[] newShields)
    {
        List<ShieldInfo> oldShields = GetShieldsAsList();

        List<ShieldInfo> filteredShields = new List<ShieldInfo>(newShields);
        filteredShields.RemoveAll(shield => shield.Amount == 0);

        shields.Clear();
        shieldTimers.Clear();

        foreach (var shield in filteredShields)
        {
            shields.Add(shield);
            shieldTimers.Add(shield.Duration);
        }

        if (oldShields.Count != filteredShields.Count)
        {
            OnShieldListChangedRPC();
        }
    }

    [Rpc(SendTo.Everyone)]
    public void OnDamageDealtRPC(ulong targetID, DamageInfo damage)
    {
        if (IsServer)
        {
            outOfCombatTimer = 5f;
        }
        OnDamageDealt?.Invoke(targetID, damage);
    }

    [Rpc(SendTo.Everyone)]
    public void OnKilledPokemonRPC(ulong targetID)
    {
        OnOtherPokemonKilled?.Invoke(targetID);
    }

    public void GainExperience(int amount)
    {
        if (!IsOwner)
        {
            return;
        }

        if (localStoredExp > 0)
        {
            int convertedExp = Mathf.Min(amount, localStoredExp);
            localStoredExp -= convertedExp;
            localExp += convertedExp;
        }
        
        localExp += amount;

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
            currentHp.Value = Mathf.RoundToInt(GetMaxHp(localLevel) * hpPercentage);
        }
        else
        {
            SetCurrentHPServerRPC(Mathf.RoundToInt(GetMaxHp(localLevel) * hpPercentage));
        }
    }

    [Rpc(SendTo.Server)]
    private void ClearShieldsRPC()
    {
        shields.Clear();
        shieldTimers.Clear();
    }

    [Rpc(SendTo.Server)]
    private void SetCurrentHPServerRPC(int amount)
    {
        currentHp.Value = amount;
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

    [Rpc(SendTo.Server)]
    public void FlipAtkStatsRPC(bool value)
    {
        flipAtkStat.Value = value;
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

    [Rpc(SendTo.Owner)]
    public void ApplyKnockbackRPC(Vector3 direction, float force)
    {
        OnKnockback?.Invoke(direction, force);
    }

    [Rpc(SendTo.Owner)]
    public void ApplyKnockupRPC(float force, float duration)
    {
        OnKnockup?.Invoke(force, duration);
    }

    public bool IsHPFull()
    {
        return currentHp.Value == GetMaxHp();
    }

    public int GetMissingHp()
    {
        return GetMaxHp() - currentHp.Value;
    }
}
