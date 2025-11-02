using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class WildPokemonSpawner : NetworkBehaviour
{
    public enum RespawnType { NoRespawn, TimedRespawn, SpecificTimesRespawn }
    public enum BuffToGive { None, RedBuff, BlueBuff }

    [SerializeField] private GameObject pokemonPrefab;
    [SerializeField] private AvailableWildPokemons wildPokemonID;
    [SerializeField] private bool usesTimeRemaining = true;
    [SerializeField] private float firstSpawnTime = 600f;
    [SerializeField] private float respawnCooldown;
    [SerializeField] private RespawnType respawnType;
    [SerializeField] private List<float> specificRespawnTimes;
    [SerializeField] private float despawnTime;
    [SerializeField] private bool isObjective;
    [SerializeField] private int soldierLaneID;
    [SerializeField] private BuffToGive buffToGive;
    [SerializeField] private WildPokemonAISettings aiSettings;

    private NetworkVariable<bool> isSpawned = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private float timer;
    private bool spawnedFirstTime = false;

    private WildPokemon wildPokemon;
    private Vision vision;

    public WildPokemon WildPokemon => wildPokemon;
    public float RespawnCooldown => respawnCooldown;
    public RespawnType PokemonRespawnType => respawnType;

    private StatusEffect redBuff = new StatusEffect(StatusType.RedBuff, 0f, false, 1);
    private StatusEffect blueBuff = new StatusEffect(StatusType.BlueBuff, 0f, false, 1);

    public event System.Action OnShouldDestroyIcon;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        vision = GetComponent<Vision>();
        if (vision == null)
        {
            Debug.LogError($"Vision component missing on {gameObject.name}", this);
            return;
        }

        vision.IsVisiblyEligible = true;
        vision.OnVisibilityChanged += HandleVisibilityChanged;
        isSpawned.OnValueChanged += HandleSpawnStateChanged;

        HandleSpawnStateChanged(isSpawned.Value, isSpawned.Value);

        StartCoroutine(FixVisionBug());


        if (IsServer)
        {
            // --- Server-side Time Calculation ---
            if (usesTimeRemaining)
            {
                StartCoroutine(InitializeTimers());
            }
            else
            {
                if (firstSpawnTime < 0) firstSpawnTime = Mathf.Infinity;
                if (despawnTime < 0) despawnTime = Mathf.Infinity;
            }


            if (aiSettings.homePosition == Vector2.zero)
            {
                aiSettings.homePosition = new Vector2(transform.position.x, transform.position.z);
            }
        }
    }

    private IEnumerator InitializeTimers()
    {
        yield return null;

        if (firstSpawnTime > 0)
        {
            firstSpawnTime = GameManager.Instance.MAX_GAME_TIME - firstSpawnTime;
        }
        else
        {
            firstSpawnTime = Mathf.Infinity; // Indicates never spawn automatically initially
        }

        if (despawnTime > 0f)
        {
            despawnTime = GameManager.Instance.MAX_GAME_TIME - despawnTime;
        }
        else
        {
            despawnTime = Mathf.Infinity; // Indicates never despawn automatically
        }

        for (int i = 0; i < specificRespawnTimes.Count; i++)
        {
            specificRespawnTimes[i] = GameManager.Instance.MAX_GAME_TIME - specificRespawnTimes[i];
        }
        specificRespawnTimes.Sort(); // Important for HandleSpecificTimesRespawn
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from events to prevent errors
        if (vision != null)
        {
            vision.OnVisibilityChanged -= HandleVisibilityChanged;
        }
        isSpawned.OnValueChanged -= HandleSpawnStateChanged;

        // Clean up potential icon if object is destroyed
        OnShouldDestroyIcon?.Invoke();

        base.OnNetworkDespawn();
    }

    private IEnumerator FixVisionBug()
    {
        yield return new WaitForSeconds(0.01f);
        vision.enabled = false;
        yield return new WaitForSeconds(0.01f);
        vision.enabled = true;
    }

    private void HandleSpawnStateChanged(bool previousValue, bool newValue)
    {
        if (!IsServer || IsHost)
        {
            if (newValue == true)
            {
                if (MinimapManager.Instance != null)
                {
                    MinimapManager.Instance.CreateWildPokemonIcon(this);
                }
            }
            else
            {
                if (vision != null && vision.IsRendered)
                {
                    OnShouldDestroyIcon?.Invoke();
                }
            }
        }
    }

    private void HandleVisibilityChanged(bool visible)
    {
        if ((!IsServer || IsHost) && visible)
        {
            if (!isSpawned.Value)
            {
                OnShouldDestroyIcon?.Invoke();
            }
        }
    }

    private void Update()
    {
        if (!IsServer || GameManager.Instance == null || GameManager.Instance.GameState != GameState.Playing)
        {
            return;
        }

        // --- Despawning ---
        if (despawnTime != Mathf.Infinity && GameManager.Instance.GameTime >= despawnTime)
        {
            if (wildPokemon != null)
            {
                DespawnPokemon(false);
            }
            despawnTime = Mathf.Infinity;
            firstSpawnTime = Mathf.Infinity;
            respawnType = RespawnType.NoRespawn;
            return;
        }

        // --- Initial Spawning ---
        if (!spawnedFirstTime && firstSpawnTime != Mathf.Infinity && GameManager.Instance.GameTime >= firstSpawnTime)
        {
            SpawnPokemon();
            spawnedFirstTime = true;
            firstSpawnTime = Mathf.Infinity;
        }

        // --- Respawning ---
        if (spawnedFirstTime)
        {
            switch (respawnType)
            {
                case RespawnType.NoRespawn:
                    // Do nothing
                    break;

                case RespawnType.TimedRespawn:
                    HandleTimedRespawn();
                    break;

                case RespawnType.SpecificTimesRespawn:
                    HandleSpecificTimesRespawn();
                    break;
            }
        }
    }

    private void HandleTimedRespawn()
    {
        if (timer <= 0 || wildPokemon != null) return;

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            SpawnPokemon();
        }
    }

    private void HandleSpecificTimesRespawn()
    {
        if (specificRespawnTimes.Count > 0)
        {
            float nextRespawnTime = specificRespawnTimes[0];
            if (GameManager.Instance.GameTime >= nextRespawnTime)
            {
                SpawnPokemon();
                specificRespawnTimes.RemoveAt(0);
            }
        }
        else
        {
            respawnType = RespawnType.NoRespawn;
        }
    }

    public void EnableSpawner()
    {
        if (!IsServer) return;
        firstSpawnTime = Mathf.Max(GameManager.Instance.GameTime, usesTimeRemaining ? GameManager.Instance.MAX_GAME_TIME - firstSpawnTime : firstSpawnTime);
        spawnedFirstTime = false;

        if (respawnType == RespawnType.TimedRespawn)
        {
            timer = 0;
        }
    }

    public void SpawnPokemon()
    {
        if (wildPokemon != null || !IsServer)
        {
            return;
        }

        GameObject pokemonInstance = Instantiate(pokemonPrefab, transform.position, transform.rotation, transform);
        wildPokemon = pokemonInstance.GetComponent<WildPokemon>();
        if (wildPokemon == null)
        {
            Debug.LogError($"Prefab {pokemonPrefab.name} is missing WildPokemon component!", this);
            Destroy(pokemonInstance);
            return;
        }

        NetworkObject instanceNetworkObject = wildPokemon.GetComponent<NetworkObject>();
        if (instanceNetworkObject == null)
        {
            Debug.LogError($"Prefab {pokemonPrefab.name} is missing NetworkObject component!", this);
            Destroy(pokemonInstance);
            wildPokemon = null;
            return;
        }
        instanceNetworkObject.Spawn();

        wildPokemon.SetWildPokemonInfoRPC((short)wildPokemonID, isObjective);
        wildPokemon.SoldierLaneID = soldierLaneID;
        wildPokemon.Pokemon.OnDeath += HandlePokemonDeath;
        wildPokemon.Pokemon.OnPokemonInitialized += InitializeAISettings;

        if (buffToGive != BuffToGive.None)
        {
            wildPokemon.Pokemon.AddStatusEffect(buffToGive == BuffToGive.RedBuff ? redBuff : blueBuff);
        }

        isSpawned.Value = true;
    }

    private void InitializeAISettings()
    {
        if (wildPokemon == null || !IsServer) return;

        if (wildPokemon.TryGetComponent(out WildPokemonAI wildPokemonAI))
        {
            wildPokemonAI.Initialize(aiSettings);
        }
        wildPokemon.Pokemon.OnPokemonInitialized -= InitializeAISettings;
    }

    public void DespawnPokemon(bool canRespawn)
    {
        if (wildPokemon == null || !IsServer)
        {
            return;
        }

        if (!canRespawn)
        {
            respawnType = RespawnType.NoRespawn;
            specificRespawnTimes.Clear();
        }

        wildPokemon.Pokemon.TakeDamageRPC(new DamageInfo(wildPokemon.NetworkObjectId, 999f, 999, 9999, DamageType.True));
    }

    private void HandlePokemonDeath(DamageInfo info)
    {
        if (!IsServer) return;

        if (wildPokemon != null)
        {
            wildPokemon.Pokemon.OnDeath -= HandlePokemonDeath;
            wildPokemon.Pokemon.OnPokemonInitialized -= InitializeAISettings;
            wildPokemon = null;
        }


        isSpawned.Value = false;

        if (respawnType == RespawnType.TimedRespawn)
        {
            timer = respawnCooldown;
        }

        ConfirmPokemonDeathClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ConfirmPokemonDeathClientRpc()
    {
        if (vision != null && vision.IsRendered)
        {
            OnShouldDestroyIcon?.Invoke();
        }
    }
}