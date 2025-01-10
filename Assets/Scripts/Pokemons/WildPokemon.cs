using DG.Tweening;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class WildPokemon : NetworkBehaviour
{
    private Pokemon pokemon;
    private AnimationManager animationManager;
    private WildPokemonInfo wildPokemonInfo;
    [SerializeField] private HPBarWild hpBar;
    [SerializeField] private RedBlueBuffAura redBlueBuffAura;

    [SerializeField] private GameObject soldierPrefab;
    private AvailableWildPokemons soldierToSpawn;

    private Vision vision;
    private Rigidbody rb;

    private const string resourcePath = "Assets/Prefabs/Objects/Objects/AeosEnergy.prefab";
    private float healingTick;
    private ObjectiveType objectiveType;

    private AsyncOperationHandle<PokemonBase> pokemonLoadHandle;

    public Pokemon Pokemon => pokemon;
    public WildPokemonInfo WildPokemonInfo { get => wildPokemonInfo; set { wildPokemonInfo = value; } }
    public AnimationManager AnimationManager => animationManager;
    public int ExpYield { get => wildPokemonInfo.ExpYield[pokemon.CurrentLevel]; }
    public ushort EnergyYield { get => wildPokemonInfo.EnergyYield[pokemon.CurrentLevel]; }
    public Vision Vision => vision;

    public int SoldierLaneID { get; set; }

    public override void OnNetworkSpawn()
    {
        pokemon = GetComponent<Pokemon>();
        animationManager = GetComponent<AnimationManager>();
        vision = GetComponentInChildren<Vision>();
        rb = GetComponent<Rigidbody>();
        vision.HasATeam = false;
        vision.IsVisible = true;
        pokemon.Type = PokemonType.Wild;
        pokemon.OnEvolution += AssignVisionObjects;
        pokemon.OnLevelChange += () => hpBar.UpdateEnergyAmount(EnergyYield);
        NetworkObject.DestroyWithScene = true;

        if (IsOwner)
        {
            pokemon.OnKnockback += Knockback;
            pokemon.OnKnockup += Knockup;
        }

        if (IsServer)
        {
            pokemon.OnDeath += Die;
        }
    }

    private void AssignVisionObjects()
    {
        animationManager.AssignAnimator(pokemon.ActiveModel.GetComponentInChildren<Animator>());

        vision.ResetObjects();
        if (pokemon.Type != PokemonType.Objective)
        {
            vision.AddObject(pokemon.ActiveModel);
        }
        vision.AddObject(hpBar.gameObject);
        vision.AddObject(redBlueBuffAura.AuraHolder.gameObject);
        vision.SetVisibility(false);
    }

    private IEnumerator SetInitialLevel()
    {
        yield return new WaitUntil(() => pokemon.CurrentHp != 0);

        int totalLevelUps = CalculateTimeHits();

        int totalExp = 0;

        for (int i = 0; i < totalLevelUps; i++)
        {
            totalExp += pokemon.BaseStats.GetExpForNextLevel(pokemon.CurrentLevel + i);
        }

        pokemon.GainExperienceRPC(totalExp);
        GameManager.Instance.onFarmLevelUps += LevelUpWildMon;
    }

    private int CalculateTimeHits()
    {
        int firstLevelUpTime = 30; // Initial 10 seconds delay

        // Calculate the number of 30-second intervals that have passed
        int totalHits = Mathf.FloorToInt((GameManager.Instance.GameTime - firstLevelUpTime) / 30);

        // Ensure totalHits is not negative
        return Mathf.Max(totalHits, 0);
    }

    private void LevelUpWildMon()
    {
        pokemon.GainExperienceRPC(pokemon.BaseStats.GetExpForNextLevel(pokemon.CurrentLevel));
    }

    private void Die(DamageInfo info)
    {
        if (info.attackerId == NetworkObjectId)
        {
            StartCoroutine(DumbDespawn());
            return;
        }

        if (pokemon.Type == PokemonType.Objective)
        {
            ShowKillRpc(info);
            if (pokemonLoadHandle.IsValid())
            {
                HandleObjectiveBehaviour(objectiveType, info);
            }
        }
        else
        {
            pokemon.GiveExpRpc(info.attackerId, transform.position, ExpYield);
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].TryGetComponent(out PlayerManager player))
            {
                GiveAttackerEnergy(player);
                GivePlayerBuff(player);
            }

            StartCoroutine(DumbDespawn());
        }
    }

    private void GivePlayerBuff(PlayerManager player)
    {
        if (pokemon.HasStatusEffect(StatusType.BlueBuff))
        {
            StatusEffect buff = new StatusEffect();
            foreach (var status in pokemon.StatusEffects)
            {
                if (status.Type == StatusType.BlueBuff)
                {
                    buff = status;
                    break;
                }
            }

            buff.IsTimed = true;
            buff.Duration = 70f;

            player.Pokemon.AddStatusEffect(buff);
        }
        else if (pokemon.HasStatusEffect(StatusType.RedBuff))
        {
            StatusEffect buff = new StatusEffect();
            foreach (var status in pokemon.StatusEffects)
            {
                if (status.Type == StatusType.RedBuff)
                {
                    buff = status;
                    break;
                }
            }

            buff.IsTimed = true;
            buff.Duration = 70f;

            player.Pokemon.AddStatusEffect(buff);
        }
    }

    private void HandleObjectiveBehaviour(ObjectiveType objectiveType, DamageInfo info)
    {
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<Pokemon>();

        switch (objectiveType)
        {
            case ObjectiveType.Zapdos:
                HandleZapdos(attacker, info);
                break;
            case ObjectiveType.Drednaw:
                HandleDrednaw(attacker, info);
                break;
            case ObjectiveType.Rotom:
                HandleRotom(attacker, info);
                break;
            case ObjectiveType.Registeel:
                HandleRegisteel(attacker, info);
                break;
            default:
                break;
        }
    }

    private void HandleZapdos(Pokemon attacker, DamageInfo info)
    {
        Team teamToBuff = attacker.TeamMember.Team;
        if (attacker.TryGetComponent(out PlayerManager player))
        {
            GiveAttackerEnergy(player);
        }

        foreach (var playerManager in GameManager.Instance.Players)
        {
            if (playerManager != null && playerManager.CurrentTeam.IsOnSameTeam(teamToBuff) && playerManager.PlayerState != PlayerState.Dead)
            {
                playerManager.AddScoreBoostRPC(new ScoreBoost(0, ScoreSpeedFactor.Rayquaza, 25f, true));
            }
        }

        LaneManager opposingLane = GameManager.Instance.Lanes.ToList().Find(lane => lane.Team != teamToBuff);

        foreach (var goalZone in opposingLane.GoalZones)
        {
            goalZone.WeaknenGoalZoneRPC(20f);
        }

        pokemon.GiveExpRpc(info.attackerId, transform.position, ExpYield);
        StartCoroutine(DumbDespawn());
    }

    private void HandleDrednaw(Pokemon attacker, DamageInfo info)
    {
        Team teamToGiveExp = attacker.TeamMember.Team;
        if (attacker.TryGetComponent(out PlayerManager player))
        {
            GiveAttackerEnergy(player);
        }

        foreach (var playerManager in GameManager.Instance.Players)
        {
            if (playerManager != null && playerManager.CurrentTeam.IsOnSameTeam(teamToGiveExp))
            {
                playerManager.Pokemon.GainExperienceRPC(Mathf.RoundToInt(ExpYield * 0.50f));
                if (playerManager.PlayerState != PlayerState.Dead)
                {
                    playerManager.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(playerManager.Pokemon.GetMaxHp() * 0.08f), 9, 0, 60f, true));
                }
            }
        }

        StartCoroutine(DumbDespawn());
    }

    private void HandleRotom(Pokemon attacker, DamageInfo info)
    {
        if (attacker.TryGetComponent(out PlayerManager player))
        {
            SpawnSoldierRPC(player.CurrentTeam.Team);
            GiveAttackerEnergy(player);
        }

        pokemon.GiveExpRpc(info.attackerId, transform.position, ExpYield);
        StartCoroutine(DumbDespawn());
    }

    private void HandleRegisteel(Pokemon attacker, DamageInfo info)
    {
        Team teamToGiveExp = attacker.TeamMember.Team;
        if (attacker.TryGetComponent(out PlayerManager player))
        {
            GiveAttackerEnergy(player);
        }

        foreach (var playerManager in GameManager.Instance.Players)
        {
            if (playerManager != null && playerManager.CurrentTeam.IsOnSameTeam(teamToGiveExp))
            {
                playerManager.Pokemon.GainExperienceRPC(Mathf.RoundToInt(ExpYield * 0.60f));
                if (playerManager.PlayerState != PlayerState.Dead)
                {
                    playerManager.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(playerManager.Pokemon.GetMaxHp() * 0.08f), 9, 0, 30f, true));
                    playerManager.Pokemon.AddStatChange(new StatChange(15, Stat.Attack, 90f, true, true, true, 0));
                    playerManager.Pokemon.AddStatChange(new StatChange(15, Stat.SpAttack, 90f, true, true, true, 0));
                }
            }
        }

        StartCoroutine(DumbDespawn());
    }

    [Rpc(SendTo.Server)]
    private void SpawnSoldierRPC(Team orangeTeam)
    {
        SoldierPokemon soldier = Instantiate(soldierPrefab, transform.position, transform.rotation).GetComponent<SoldierPokemon>();
        soldier.GetComponent<NetworkObject>().Spawn(true);
        soldier.InitializeRPC(orangeTeam, soldierToSpawn, SoldierLaneID);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowKillRpc(DamageInfo info)
    {
        BattleUIManager.instance.ShowKill(info, pokemon);
    }

    private IEnumerator DumbDespawn()
    {
        yield return new WaitForSeconds(0.1f);
        gameObject.GetComponent<NetworkObject>().Despawn(true);
    }

    private void GiveAttackerEnergy(PlayerManager attacker)
    {
        if (attacker == null)
        {
            return;
        }

        if (attacker.AvailableEnergy() >= EnergyYield)
        {
            attacker.GainEnergyRPC(EnergyYield);
        }
        else
        {
            SpawnEnergy((short)(EnergyYield - attacker.AvailableEnergy()));
            attacker.GainEnergyRPC(attacker.AvailableEnergy());
        }
    }

    private void SpawnEnergy(short amount)
    {
        int numFives = amount / 5;
        int remainderOnes = amount % 5;

        for (int i = 0; i < numFives; i++)
        {
            SpawnEnergyRpc(true);
        }

        for (int i = 0; i < remainderOnes; i++)
        {
            SpawnEnergyRpc(false);
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnEnergyRpc(bool isBig)
    {
        Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
        Addressables.LoadAssetAsync<GameObject>(resourcePath).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                GameObject spawnedObject = Instantiate(prefab, transform.position + offset, Quaternion.identity);
                spawnedObject.GetComponent<AeosEnergy>().LocalBigEnergy = isBig;
                spawnedObject.GetComponent<NetworkObject>().Spawn(true);
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };
    }

    [Rpc(SendTo.Everyone)]
    public void SetWildPokemonInfoRPC(short infoID, bool isObjective = false)
    {
        wildPokemonInfo = CharactersList.Instance.WildPokemons[infoID];
        pokemon = GetComponent<Pokemon>();
        pokemonLoadHandle = Addressables.LoadAssetAsync<PokemonBase>(wildPokemonInfo.PokemonBase);
        pokemonLoadHandle.Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                pokemon.SetNewPokemon(handle.Result);
                hpBar.SetPokemon(pokemon);
                hpBar.InitializeEnergyUI(EnergyYield);

                if (isObjective)
                {
                    MinimapManager.Instance.CreateObjectiveIcon(this);
                    objectiveType = wildPokemonInfo.ObjectiveType;
                }

                if (IsServer)
                {
                    StartCoroutine(SetInitialLevel());
                }
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };

        pokemon.Type = isObjective ? PokemonType.Objective : PokemonType.Wild;
        soldierToSpawn = wildPokemonInfo.SoldierToSpawn;

        if (isObjective)
        {
            MinimapManager.Instance.CreateObjectiveIcon(this);
        }
    }

    public void Knockup(float force, float duration)
    {
        if (pokemon.Type == PokemonType.Objective)
        {
            return;
        }
        rb.velocity = Vector3.zero;

        transform.DOJump(transform.position, force, 1, duration);
    }

    public void Knockback(Vector3 direction, float force)
    {
        if (pokemon.Type == PokemonType.Objective)
        {
            return;
        }
        rb.AddForce(direction * force, ForceMode.Impulse);
    }

    public override void OnDestroy()
    {
        if (pokemonLoadHandle.IsValid())
        {
            Addressables.Release(pokemonLoadHandle);
        }
        GameManager.Instance.onFarmLevelUps -= LevelUpWildMon;
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (pokemon.Type == PokemonType.Objective)
        {
            if (pokemon.IsOutOfCombat && !pokemon.IsHPFull())
            {
                healingTick -= Time.deltaTime;
                if (healingTick <= 0)
                {
                    pokemon.HealDamageRPC(Mathf.FloorToInt(pokemon.GetMaxHp() * 0.05f));
                    healingTick = 0.5f;
                }
            }
        }
    }
}
