using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class WildPokemon : NetworkBehaviour
{
    private Pokemon pokemon;
    [SerializeField] private WildPokemonInfo wildPokemonInfo;
    [SerializeField] private HPBarWild hpBar;
    private Vision vision;

    private string resourcePath = "Assets/Prefabs/Objects/Objects/AeosEnergy.prefab";

    public Pokemon Pokemon => pokemon;
    public WildPokemonInfo WildPokemonInfo { get => wildPokemonInfo; set { wildPokemonInfo = value; } }
    public int ExpYield { get => wildPokemonInfo.ExpYield; }
    public ushort EnergyYield { get => wildPokemonInfo.EnergyYield; }

    public override void OnNetworkSpawn()
    {
        pokemon = GetComponent<Pokemon>();
        vision = GetComponentInChildren<Vision>();
        vision.HasATeam = false;
        vision.IsVisible = true;
        //pokemon.SetNewPokemon(wildPokemonInfo.PokemonBase);
        pokemon.Type = PokemonType.Wild;
        pokemon.OnEvolution += AssignVisionObjects;
        NetworkObject.DestroyWithScene = true;
        if (IsServer)
        {
            pokemon.OnDeath += Die;
        }
    }

    private void AssignVisionObjects()
    {
        vision.ResetObjects();
        vision.AddObject(pokemon.ActiveModel);
        vision.AddObject(hpBar.gameObject);
        vision.SetVisibility(false);
    }

    private void Die(DamageInfo info)
    {
        GiveExpRpc(info.attackerId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void GiveExpRpc(ulong attackerID)
    {
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackerID].GetComponent<Pokemon>();

        float baseExp = wildPokemonInfo.ExpYield;

        Dictionary<int, (float koerExp, float proximityExp)> expDistribution = new Dictionary<int, (float, float)>
        {
            {1, (1.0f, 0.0f)},
            {2, (0.7f, 0.3f)},
            {3, (0.7f, 0.15f)},
            {4, (0.7f, 0.1f)},
            {5, (0.7f, 0.075f)}
        };

        List<Pokemon> playersInProximity;

        if (attacker.TryGetComponent(out PlayerManager player))
        {
            player.MovesController.IncrementUniteCharge(5000);

            playersInProximity = FindPlayersInProximity(transform.position, 6f, player.OrangeTeam);
        }
        else
        {
            playersInProximity = FindPlayersInProximity(transform.position, 6f, false);
        }

        DistributeExperience(attacker, playersInProximity, baseExp, expDistribution);

        if (IsServer)
        {
            GiveAttackerEnergy(attacker.GetComponent<PlayerManager>());
            StartCoroutine(DumbDespawn());
        }
    }

    private List<Pokemon> FindPlayersInProximity(Vector3 koPosition, float range, bool orangeTeam)
    {
        List<Pokemon> playersInProximity = new List<Pokemon>();

        foreach (var playerObject in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            PlayerManager player = playerObject.GetComponent<PlayerManager>();
            if (player != null && player.OrangeTeam == orangeTeam && Vector3.Distance(player.transform.position, koPosition) <= range)
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
        attacker.GainExperience(Mathf.FloorToInt(attackerExp));

        float proximityExp = baseExp * expValues.proximityExp;
        foreach (var player in playersInProximity)
        {
            if (player != attacker)
            {
                player.GainExperience(Mathf.FloorToInt(proximityExp));
            }
        }
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

        if (attacker.AvailableEnergy() >= wildPokemonInfo.EnergyYield)
        {
            attacker.GainEnergyRPC(wildPokemonInfo.EnergyYield);
        }
        else
        {
            SpawnEnergy((short)(wildPokemonInfo.EnergyYield - attacker.AvailableEnergy()));
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

    [Rpc(SendTo.ClientsAndHost)]
    public void SetWildPokemonInfoRPC(short infoID, bool isObjective = false)
    {
        wildPokemonInfo = CharactersList.instance.WildPokemons[infoID];
        pokemon = GetComponent<Pokemon>();
        pokemon.SetNewPokemon(wildPokemonInfo.PokemonBase);
        pokemon.Type = isObjective ? PokemonType.Objective : PokemonType.Wild;
        hpBar.SetPokemon(pokemon);
        hpBar.InitializeEnergyUI(EnergyYield);
    }
}
