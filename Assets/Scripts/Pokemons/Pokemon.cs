using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Pokemon : NetworkBehaviour
{
    [SerializeField] private GameObject damagePrefab;
    private PokemonBase baseStats;
    private NetworkVariable<int> currentHp = new NetworkVariable<int>();
    private NetworkVariable<int> currentLevel = new NetworkVariable<int>();
    private NetworkVariable<int> currentExp = new NetworkVariable<int>();
    private NetworkVariable<int> storedExp = new NetworkVariable<int>();

    private NetworkList<ShieldInfo> shields;
    private List<float> shieldTimers = new List<float>();
    private bool shieldChangePending;

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

    // Replicated offensive-scaling inversion; source ownership is tracked in Pokemon.Meowstic.
    private NetworkVariable<bool> flipAtkStat = new NetworkVariable<bool>();

    private float outOfCombatTimer = 0;
    private NetworkVariable<bool> isOutOfCombat = new NetworkVariable<bool>(true);

    private PokemonEvolution currentEvolution;

    private NetworkVariable<int> killStreak = new NetworkVariable<int>();
    private float killStreakTimer;

    private NetworkVariable<TeamMember> team = new NetworkVariable<TeamMember>();

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

    public PokemonEvolution CurrentEvolution { get { return currentEvolution; } }

    public int LevelCap { get { return GetLevelCap(); } }

    public int KillStreak { get { return killStreak.Value; } }

    public TeamMember TeamMember { get { return team.Value; } }

    public event Action OnHpChange;
    public event Action OnShieldChange;
    public event Action OnLevelChange;
    public event Action OnExpChange;
    public event Action OnEvolution;
    public event Action<DamageInfo> OnDeath;
    public event Action<DamageInfo> OnDamageTaken;
    public event Action OnPokemonInitialized;

    public event Action<ulong, DamageInfo> OnDamageDealt;
    // Server-only extension points. Block before shields/HP, and report real HP
    // lost after shields/overkill rather than re-evaluating a damage formula.
    public event Func<DamageInfo, bool> BeforeDamage;
    public event Action<Pokemon, DamageInfo, int> OnHealthDamageDealt;
    public event Action<ulong> OnOtherPokemonKilled;

    public event Action<NetworkListEvent<StatChange>> OnStatChange;
    public event Action<StatusEffect, bool> OnStatusChange;

    public event Action<Vector3, float> OnKnockback;
    public event Action<float, float> OnKnockup;

    public event Action<TeamMember> OnTeamChange;

    private DamageInfo lastHit;

    private void Awake()
    {
        statChanges = new NetworkList<StatChange>();
        statusEffects = new NetworkList<StatusEffect>();
        shields = new NetworkList<ShieldInfo>();
        statChanges.OnListChanged += OnStatListChanged;
        shields.OnListChanged += OnShieldListChanged;
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
            GainExperienceRPC(amount);
        }
    }

    public void SetNewPokemon(PokemonBase pokemonBase)
    {
        baseStats = pokemonBase;
        InitializePokemon();
    }

    public void InitializePokemon()
    {
        // Asset loading completes independently on each peer. A client's level can
        // still be stale here, so it must never reset the authoritative spawn HP
        // (or any other gameplay state) using its local copy of the stats.
        if (IsServer)
        {
            currentHp.Value = GetMaxHp();
            currentExp.Value = 0;
            shieldTimers.Clear();
            shields.Clear();
            flipAtkStat.Value = false;
        }

        // Replication may have arrived before the PokemonBase finished loading.
        localExp = currentExp.Value;
        localStoredExp = storedExp.Value;
        localLevel = currentLevel.Value;

        currentHp.OnValueChanged -= CurrentHpChanged;
        currentHp.OnValueChanged += CurrentHpChanged;
        storedExp.OnValueChanged -= StoredExpChanged;
        storedExp.OnValueChanged += StoredExpChanged;
        currentExp.OnValueChanged -= CurrentExpValueChanged;
        currentExp.OnValueChanged += CurrentExpValueChanged;
        currentLevel.OnValueChanged -= CurrentLevelChanged;
        currentLevel.OnValueChanged += CurrentLevelChanged;
        team.OnValueChanged -= OnTeamChanged;
        team.OnValueChanged += OnTeamChanged;
        CheckEvolution();

        OnPokemonInitialized?.Invoke();
    }

    private void OnTeamChanged(TeamMember previous, TeamMember current)
    {
        OnTeamChange?.Invoke(current);
    }

    private void CurrentHpChanged(int previous, int current)
    {
        if (previous > 0 && current <= 0)
        {
            if (IsServer)
            {
                RemoveShieldWithIDRPC(RayquazaBuff.ShieldId);
            }
            OnDeath?.Invoke(lastHit);
        }
        OnHpChange?.Invoke();
    }

    private void OnShieldListChanged(NetworkListEvent<ShieldInfo> change)
    {
        // Observe replicated changes, not an RPC's potentially obsolete list size.
        // Coalesce a burst into one notification after the list is consistent.
        shieldChangePending = true;
    }

    private void LateUpdate()
    {
        // Inactive recipients retain the dirty flag until they resume. No
        // coroutine can get stranded by disable/despawn or an obsolete count.
        if (!IsSpawned || !shieldChangePending) return;
        shieldChangePending = false;
        OnShieldChange?.Invoke();
    }

    private void CurrentExpValueChanged(int previous, int current)
    {
        localExp = current;
        OnExpChange?.Invoke();
    }

    private void CurrentLevelChanged(int previous, int current)
    {
        localLevel = current;
        OnLevelChange?.Invoke();
    }

    private void StoredExpChanged(int previous, int current)
    {
        localStoredExp = current;
        OnExpChange?.Invoke();
    }

    private void OnStatListChanged(NetworkListEvent<StatChange> changeEvent)
    {
        // A max-HP modifier disappearing must not leave overhealed HP behind.
        // Increasing the maximum does not grant free healing.
        bool clampedHp = IsServer && baseStats != null && CurrentHp > GetMaxHp();
        if (clampedHp)
            currentHp.Value = GetMaxHp();
        else if (changeEvent.Value.AffectedStat == Stat.MaxHp || changeEvent.PreviousValue.AffectedStat == Stat.MaxHp
            || changeEvent.Type == NetworkListEvent<StatChange>.EventType.Clear)
            OnHpChange?.Invoke();
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

    public float GetSpellVamp()
    {
        // Step 1: Sum together all flat modifiers
        int flatModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.SpellVamp && !change.Percentage)
            {
                flatModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 2: Sum together all percent modifiers
        float percentModifierSum = 0;
        foreach (StatChange change in statChanges)
        {
            if (change.AffectedStat == Stat.SpellVamp && change.Percentage)
            {
                percentModifierSum += change.IsBuff ? change.Amount : -change.Amount;
            }
        }

        // Step 3: Add the flat modifier to the Pok�mon's natural movement speed stat
        float trueLifeSteal = baseStats.SpellVamp[currentLevel.Value] + flatModifierSum;

        // Step 4: Multiply the resultant movement speed stat with 100% plus the net percentage modifier
        trueLifeSteal = trueLifeSteal * (1f + percentModifierSum / 100f);

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
        AddStatChangeRPC(change);
    }

    [Rpc(SendTo.Server)]
    private void AddStatChangeRPC(StatChange change)
    {
        if (!InterceptMeowsticStat(ref change)) return;
        // NetworkList invokes callbacks synchronously. Publish only after the
        // matching timer exists, so callbacks can safely remove/replace a stat.
        statTimers.Add(change.IsTimed ? change.Duration : -1);
        statChanges.Add(change);
    }

    private void RemoveStatAt(int index)
    {
        statTimers.RemoveAt(index);
        statChanges.RemoveAt(index);
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
                    RemoveStatAt(i);
                }
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveAllStatChangeWithIDRPC(ushort id)
    {
        foreach (var change in statChanges.AsNativeArray().ToArray())
            if (change.ID == id && change.CanBeRemoved) RemoveStatChangeRPC(change);
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
                    RemoveStatAt(i);
                }
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveAllStatChangesRPC()
    {
        foreach (var change in statChanges.AsNativeArray().ToArray())
            if (change.CanBeRemoved) RemoveStatChangeRPC(change);
    }

    [Rpc(SendTo.Server)]
    public void RemoveAllStatusEffectsRPC()
    {
        foreach (var effect in statusEffects.AsNativeArray().ToArray())
            RemoveStatusEffectRPC(effect);
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

    public bool HasStatChangeWithID(ushort id)
    {
        foreach (StatChange change in statChanges) if (change.ID == id) return true;
        return false;
    }

    public void AddStatusEffect(StatusEffect effect)
    {
        AddStatusEffectRPC(effect);
    }

    [Rpc(SendTo.Server)]
    private void AddStatusEffectRPC(StatusEffect effect)
    {
        if (!InterceptMeowsticStatus(effect)) return;
        if (effect.PeriodicDamage.Enabled)
        {
            if (!effect.PeriodicDamage.IsValid || !effect.IsTimed || !float.IsFinite(effect.Duration) || effect.Duration <= 0
                || CurrentHp <= 0 || HasStatusEffect(StatusType.Invincible)
                || !NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(effect.PeriodicDamage.Damage.attackerId)) return;
            for (int i = 0; i < statusEffects.Count; i++)
                if (statusEffects[i].SamePeriodicSource(effect))
                {
                    statusEffects[i] = effect; statusTimers[i] = effect.Duration;
                    RegisterPeriodicStatus(effect, true); return;
                }
            RegisterPeriodicStatus(effect);
        }
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
            foreach (var negative in statusEffects.AsNativeArray().ToArray())
                if (negative.IsNegativeStatus()) RemoveStatusEffectRPC(negative);
        }

        if (HasStatusEffect(effect))
        {
            statusTimers[statusEffects.IndexOf(effect)] += effect.Duration;
        }
        else
        {
            statusTimers.Add(effect.IsTimed ? effect.Duration : -1);
            statusEffects.Add(effect);
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
                    RemoveStatusAt(i);
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
                RemoveStatusAt(i);
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
                RemoveStatusAt(i);
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdateStatusEffectTimeRPC(StatusEffect status, float newTime)
    {
        if (!float.IsFinite(newTime)) return;
        foreach (var effect in statusEffects)
        {
            if (effect.Equals(status))
            {
                if (effect.PeriodicDamage.Enabled && newTime <= 0) { RemoveStatusEffectRPC(effect); return; }
                statusTimers[statusEffects.IndexOf(effect)] = newTime;
                if (effect.PeriodicDamage.Enabled) SetPeriodicTime(effect, newTime);
                return;
            }
        }
    }

    private void RemoveStatusAt(int index)
    {
        StatusEffect effect = statusEffects[index];
        statusTimers.RemoveAt(index);
        statusEffects.RemoveAt(index);
        // Gameplay callbacks see the completed removal on both server types.
        OnStatusListChangedRPC(effect, false);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void OnStatusListChangedRPC(StatusEffect effect, bool added)
    {
        OnStatusChange?.Invoke(effect, added);
    }

    [Rpc(SendTo.Server)]
    public void ClearStatusEffectsRPC()
    {
        RemoveAllStatusEffectsRPC();
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

    public bool HasStatusEffect(StatusEffect newEffect)
    {
        foreach (StatusEffect effect in statusEffects)
        {
            if (newEffect.Equals(effect))
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
        UpdatePeriodicPresentation();
        if (IsServer)
        {
            TickPeriodicStatuses();
            List<StatChange> expiredStats = null;
            for (int i = statChanges.Count; i > 0; i--)
            {
                int index = i - 1;
                StatChange change = statChanges[index];
                if (change.IsTimed)
                {
                    statTimers[index] -= Time.deltaTime;
                    if (statTimers[index] <= 0)
                    {
                        (expiredStats ??= new List<StatChange>()).Add(change);
                    }
                }
            }
            // Expiry callbacks can cleanse or replace other effects. Do not keep
            // iterating live indices across those callbacks.
            if (expiredStats != null)
                foreach (var change in expiredStats)
                {
                    int index = statChanges.IndexOf(change);
                    if (index >= 0 && statTimers[index] <= 0) RemoveStatAt(index);
                }

            List<StatusEffect> expiredStatuses = null;
            for (int i = statusEffects.Count; i > 0; i--)
            {
                int index = i - 1;
                StatusEffect effect = statusEffects[index];
                if (effect.IsTimed)
                {
                    statusTimers[index] = effect.PeriodicDamage.Enabled ? PeriodicTimeRemaining(effect) : statusTimers[index] - Time.deltaTime;
                    if (statusTimers[index] <= 0)
                    {
                        (expiredStatuses ??= new List<StatusEffect>()).Add(effect);
                    }
                }
            }
            if (expiredStatuses != null)
                foreach (var effect in expiredStatuses)
                {
                    int index = statusEffects.IndexOf(effect);
                    if (index >= 0 && statusTimers[index] <= 0) RemoveStatusAt(index);
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
                        shieldTimers.RemoveAt(index);
                        shields.RemoveAt(index);
                    }
                }
            }

            if (outOfCombatTimer > 0)
            {
                outOfCombatTimer -= Time.deltaTime;
            }

            isOutOfCombat.Value = outOfCombatTimer < 0;

            if (killStreakTimer > 0)
            {
                killStreakTimer -= Time.deltaTime;
            }
            else
            {
                killStreak.Value = 0;
            }
        }

        if (!IsOwner)
        {
            return;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TakeDamageRPC(new DamageInfo(NetworkObjectId, 3.45f, 32, 820, DamageType.Physical));
        }
        if (Keyboard.current.yKey.wasPressedThisFrame)
        {
            HealDamageRPC(300);
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
            if (type == PokemonType.Player)
                GainExperienceRPC(100);
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            AddStatChange(new StatChange(1000, Stat.Speed, 5, true, true, false, 0));
        }

        if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 3f, true, 0));
        }
#endif
    }

    [Rpc(SendTo.Server)]
    public void TakeDamageRPC(DamageInfo damage)
    {
        ResolveDamage(damage);
    }

    // Synchronous server result includes shield absorption, but not overkill.
    // Existing RPC callers retain exactly the same damage flow.
    public int ResolveDamage(DamageInfo damage)
    {
        if (!IsServer || HasStatusEffect(StatusType.Invincible) || currentHp.Value <= 0)
        {
            return 0;
        }

        lastHit = damage;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(damage.attackerId, out var attackerObject)
            || !attackerObject.TryGetComponent(out Pokemon attacker)) return 0;
        if (BeforeDamage != null)
            foreach (Func<DamageInfo, bool> block in BeforeDamage.GetInvocationList())
                if (block(damage)) return 0;

        int previousHp = currentHp.Value;
        int localHp = previousHp;
        List<ShieldInfo> localShields = shields.AsNativeArray().ToList();

        if (damage.proprieties.HasFlag(DamageProprieties.CanCrit))
        {
            if (UnityEngine.Random.Range(0, 100) < Mathf.Clamp(attacker.GetCritRate() + damage.bonusCritRate, 0, 100))
            {
                damage.proprieties |= DamageProprieties.WasCriticalHit;
            }
        }

        int actualDamage = CalculateDamage(damage, attacker);

        int damageRemainder;

        if (damage.type != DamageType.True)
        {
            List<ShieldInfo> remainingShields = TakeDamageFromShields(localShields.ToArray(), actualDamage, out damageRemainder);
            UpdateDamagedShields(remainingShields);
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

        OnResolvedDamage?.Invoke(damage, Mathf.Min(actualDamage, previousHp + (damage.type == DamageType.True ? 0 : localShields.Sum(shield => shield.Amount))));
        currentHp.Value = localHp;

        attacker.OnHealthDamageDealt?.Invoke(this, damage, Mathf.Max(0, previousHp - localHp));
        int resolved = Mathf.Min(actualDamage, previousHp + (damage.type == DamageType.True ? 0 : localShields.Sum(shield => shield.Amount)));
        attacker.OnDamageApplied?.Invoke(this, damage, resolved);
        ClientDamageRPC(actualDamage, damage, RayquazaBuff.ProtectsScoring(HasRayquazaShield, damage.type));
        attacker.OnDamageDealtRPC(NetworkObjectId, damage);
        return resolved;
    }

    public int CalculateDamage(DamageInfo damage)
    {
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[damage.attackerId].GetComponent<Pokemon>();

        return CalculateDamage(damage, attacker);
    }

    public int CalculateDamage(DamageInfo damage, Pokemon attacker)
    {
        int atkStat;

        switch (damage.type)
        {
            case DamageType.True:
            case DamageType.Physical:
                atkStat = damage.type == DamageType.Physical && attacker.SwitchAtkStat ? attacker.GetSpAttack() : attacker.GetAttack();
                break;
            case DamageType.Special:
                atkStat = attacker.SwitchAtkStat ? attacker.GetAttack() : attacker.GetSpAttack();
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

        float physicalReduction = damage.type == DamageType.Physical ? GetMeowsticPhysicalReduction() : 0;
        return Mathf.FloorToInt(finalDamage * (1f-GetDamageReduction()) * (1f-physicalReduction) * (damage.multiplier > 0 ? damage.multiplier : 1f)
            * GetRayquazaDamageMultiplier(attacker));
    }

    public List<ShieldInfo> TakeDamageFromShields(ShieldInfo[] shields, int damage, out int remainder)
    {
        // Copy the original shields to the result list
        List<ShieldInfo> resultShields = new List<ShieldInfo>(shields);

        // Sort a copy of the shields array by priority in descending order
        ShieldInfo[] sortedShields = shields.OrderByDescending(s => s.Priority)
            .ThenByDescending(s => s.ID == RayquazaBuff.ShieldId).ToArray();

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
    private void ClientDamageRPC(int actualDamage, DamageInfo damage, bool protectedScoring)
    {
        lastHit = damage;
        LastDamageProtectedScoring = protectedScoring;
        if (IsClient && damagePrefab != null)
        {
            Transform parent = activeModel == null ? transform : activeModel.transform;
            DamageIndicator indicator = Instantiate(damagePrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity, parent).GetComponent<DamageIndicator>();
            indicator.ShowDamage(actualDamage, damage.type, damage.proprieties.HasFlag(DamageProprieties.WasCriticalHit));
        }

        if (IsServer)
        {
            outOfCombatTimer = 5f;
        }

        OnDamageTaken?.Invoke(damage);
    }

    [Rpc(SendTo.Everyone)]
    private void ClientHealRpc(int actualHeal)
    {
        if (!IsClient || damagePrefab == null) return;
        Transform parent = activeModel == null ? transform : activeModel.transform;
        DamageIndicator indicator = Instantiate(damagePrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity, parent).GetComponent<DamageIndicator>();
        indicator.ShowHeal(actualHeal);
    }

    [Rpc(SendTo.Server)]
    public void HealDamageRPC(int amount)
    {
        ApplyHealing(amount);
    }

    private void ApplyHealing(int amount)
    {
        int oldHp = currentHp.Value;
        if (oldHp <= 0 || amount <= 0 || baseStats == null) return;
        int healAmount = Mathf.Min(amount, Mathf.Max(0, GetMaxHp() - oldHp));
        if (healAmount == 0) return;
        currentHp.Value = oldHp + healAmount;
        ClientHealRpc(healAmount);
    }

    // Only an explicit respawn/round reset may restore HP from zero. Keep the
    // existing owner-driven respawn flow while also allowing server authority.
    [Rpc(SendTo.Server)]
    public void RestoreHealthOnRespawnRPC(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if ((sender != OwnerClientId && sender != Unity.Netcode.NetworkManager.ServerClientId)
            || type != PokemonType.Player || baseStats == null) return;
        currentHp.Value = GetMaxHp();
    }

    [Rpc(SendTo.Server)]
    public void HealDamageRPC(DamageInfo healInfo)
    {
        if (CurrentHp <= 0 || baseStats == null
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(healInfo.attackerId, out var obj)
            || !obj.TryGetComponent(out Pokemon attacker) || attacker.BaseStats == null) return;
        int atkStat;

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

        ApplyHealing(healAmount);
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
                    shieldTimers[i] += info.Duration;
                    shields[i] = new ShieldInfo(info.MaxStacks == 0 ? shields[i].Amount + info.Amount : Mathf.Min(shields[i].Amount + info.Amount, info.MaxStacks),
                            info.ID, shields[i].Priority, shields[i].Duration, shields[i].IsTimed, info.MaxStacks);
                    return;
                }
            }
        }

        shieldTimers.Add(info.IsTimed ? info.Duration : -1);
        shields.Add(info);
    }

    [Rpc(SendTo.Server)]
    public void RemoveShieldWithIDRPC(ushort id)
    {
        for (int i = 0; i < shields.Count; i++)
        {
            if (shields[i].ID == id)
            {
                shieldTimers.RemoveAt(i);
                shields.RemoveAt(i);
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

    private int GetShieldsAsInt()
    {
        int shieldAmount = 0;

        foreach (ShieldInfo shield in shields)
        {
            shieldAmount += shield.Amount;
        }

        return shieldAmount;
    }

    private void UpdateDamagedShields(List<ShieldInfo> remainingShields)
    {
        // Damage changes amounts, not lifetimes. Keep each surviving shield's
        // timer and metadata instead of rebuilding it from its original duration.
        for (int i = remainingShields.Count - 1; i >= 0; i--)
        {
            if (remainingShields[i].Amount <= 0)
            {
                shieldTimers.RemoveAt(i);
                shields.RemoveAt(i);
            }
            else if (!shields[i].Equals(remainingShields[i]))
            {
                shields[i] = remainingShields[i];
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    public void OnDamageDealtRPC(ulong targetID, DamageInfo damage)
    {
        if (IsServer)
        {
            outOfCombatTimer = 5f;

            if (GetLifeSteal() > 0 && damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack) && !IsHPFull())
            {
                Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();

                HealDamageRPC(Mathf.FloorToInt(attackedPokemon.CalculateDamage(damage, this) * (float)(GetLifeSteal() / 100f)));
            }

            if (GetSpellVamp() > 0 && damage.type == DamageType.Special && !IsHPFull())
            {
                Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();

                HealDamageRPC(Mathf.FloorToInt(attackedPokemon.CalculateDamage(damage, this) * (float)(GetSpellVamp() / 100f)));
            }
        }
        OnDamageDealt?.Invoke(targetID, damage);
    }

    [Rpc(SendTo.Everyone)]
    public void OnKilledPokemonRPC(ulong targetID)
    {
        if (IsServer)
        {
            Pokemon killedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
            if (killedPokemon.type == PokemonType.Player)
            {
                killStreak.Value = killStreak.Value + 1;
                killStreakTimer = 5f;
                killStreakTimer = 5f;
            }
        }
        OnOtherPokemonKilled?.Invoke(targetID);
    }

    [Rpc(SendTo.Server)]
    public void GainExperienceRPC(int amount)
    {
        if (localStoredExp > 0)
        {
            int convertedExp = Mathf.Min(amount, localStoredExp);
            localStoredExp -= convertedExp;
            localExp += convertedExp;
        }
        
        localExp += amount;

        while (localExp >= baseStats.GetExpForNextLevel(localLevel) && localLevel < LevelCap - 1)
        {
            LevelUpRPC();
        }

        currentExp.Value = localExp;
        storedExp.Value = localStoredExp;
        currentLevel.Value = localLevel;
    }

    [Rpc(SendTo.Server)]
    private void LevelUpRPC()
    {
        float hpPercentage = (float)currentHp.Value / GetMaxHp(localLevel);

        localExp -= baseStats.GetExpForNextLevel(localLevel);
        localLevel++;
        if (type == PokemonType.Player)
            Debug.Log("Level Up! Current Level: " + localLevel);

        currentHp.Value = Mathf.RoundToInt(GetMaxHp(localLevel) * hpPercentage);

        CheckEvolution();
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

    [Rpc(SendTo.Server)]
    public void UpdateTeamRPC(Team team)
    {
        this.team.Value = new TeamMember(team);
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
            EvolveRPC(localLevel);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EvolveRPC(int evolutionLevel)
    {
        StartCoroutine(EvolveRoutine(evolutionLevel));
    }

    private IEnumerator EvolveRoutine(int evolutionLevel)
    {
        while (baseStats == null)
        {
            yield return null;
        }

        PokemonEvolution evolution = baseStats.IsNewEvoLevel(evolutionLevel);

        // Each peer can announce the initial evolution after loading its assets.
        // A delayed duplicate must not replace an already initialized model and
        // lose its Animator state (notably Psyduck's racing branch).
        if (evolution == null || (currentEvolution == evolution && activeModel != null))
        {
            yield break;
        }

        if (activeModel != null)
        {
            Destroy(activeModel);
        }
        currentEvolution = evolution;
        activeModel = Instantiate(evolution.newModel, transform.position, transform.rotation, transform);
        portrait = evolution.newSprite;
        OnEvolution?.Invoke();
    }

    // Calculate rewards once on the server. Broadcasting this would make every
    // peer send the same GainExperienceRPC requests back to the server.
    [Rpc(SendTo.Server)]
    public void GiveExpRpc(ulong attackerID, Vector3 deathPos, float baseExp)
    {
        Pokemon attacker = NetworkManager.SpawnManager.SpawnedObjects[attackerID].GetComponent<Pokemon>();

        Dictionary<int, (float koerExp, float proximityExp)> expDistribution = new Dictionary<int, (float, float)>
        {
            {1, (1.0f, 0.0f)},
            {2, (1.0f, 0.5f)},
            {3, (1.0f, 0.25f)},
            {4, (1.0f, 0.1667f)},
            {5, (1.0f, 0.125f)}
        };

        List<Pokemon> playersInProximity;

        if (attacker.TryGetComponent(out PlayerManager player))
        {
            playersInProximity = FindPlayersInProximity(deathPos, 6f, player.CurrentTeam.Team);
        }
        else
        {
            return;
        }

        DistributeExperience(attacker, playersInProximity, baseExp, expDistribution);
    }

    private List<Pokemon> FindPlayersInProximity(Vector3 koPosition, float range, Team team)
    {
        List<Pokemon> playersInProximity = new List<Pokemon>();

        foreach (var playerObject in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            PlayerManager player = playerObject.GetComponent<PlayerManager>();
            if (player != null && player.CurrentTeam.IsOnSameTeam(team) && Vector3.Distance(player.transform.position, koPosition) <= range)
            {
                playersInProximity.Add(player.Pokemon);
            }
        }

        return playersInProximity;
    }

    private void DistributeExperience(Pokemon attacker, List<Pokemon> playersInProximity, float baseExp, Dictionary<int, (float koerExp, float proximityExp)> expDistribution)
    {
        int playersCount = playersInProximity.Count;
        if (!expDistribution.TryGetValue(playersCount, out var expValues))
        {
            expValues = expDistribution[1];
        }

        float attackerExp = baseExp * expValues.koerExp;
        attacker.GainExperienceRPC(Mathf.FloorToInt(attackerExp));

        float proximityExp = baseExp * expValues.proximityExp;
        foreach (var player in playersInProximity)
        {
            if (player != attacker)
            {
                player.GainExperienceRPC(Mathf.FloorToInt(proximityExp));
            }
        }
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

    private int GetLevelCap()
    {
        switch (type)
        {
            case PokemonType.Player:
                return 15;
            case PokemonType.Wild:
            case PokemonType.Objective:
                return 22;
            default:
                return 15;
        }
    }
}
