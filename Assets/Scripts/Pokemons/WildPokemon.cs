using System.Collections;
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
    public short EnergyYield { get => wildPokemonInfo.EnergyYield; }

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

        attacker.GainExperience(wildPokemonInfo.ExpYield);
        if (attacker.GetComponent<PlayerManager>())
        {
            attacker.GetComponent<PlayerManager>().MovesController.IncrementUniteCharge(5000);
        }

        if (IsServer)
        {
            GiveAttackerEnergy(attacker.GetComponent<PlayerManager>());
            StartCoroutine(DumbDespawn());
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
