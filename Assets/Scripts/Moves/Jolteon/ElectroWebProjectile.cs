using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ElectroWebProjectile : NetworkBehaviour
{
    private float speed = 10f;
    private float range = 9f;
    private DamageInfo tickDamage;

    private Vector3 direction;
    private Vector3 startPosition;

    private bool orangeTeam;
    private bool initialized = false;

    private List<Pokemon> markedPokemons = new List<Pokemon>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 direction, Vector3 startPosition, bool orangeTeam, DamageInfo damageInfo)
    {
        this.orangeTeam = orangeTeam;
        this.direction = direction;
        transform.position = startPosition;
        transform.rotation = Quaternion.LookRotation(direction);
        this.startPosition = startPosition;
        tickDamage = damageInfo;
        initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        transform.position += direction * speed * Time.deltaTime;

        if (Vector3.Distance(startPosition, transform.position) >= range)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerManager player))
        {
            if (player.OrangeTeam == orangeTeam)
            {
                return;
            }

            if (!markedPokemons.Contains(player.Pokemon))
            {
                MarkPokemon(player.Pokemon);
            }
        }
        else if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!markedPokemons.Contains(pokemon))
            {
                MarkPokemon(pokemon);
            }
        }
    }

    private void MarkPokemon(Pokemon pokemon)
    {
        markedPokemons.Add(pokemon);
        Addressables.LoadAssetAsync<GameObject>("Assets/Prefabs/Objects/Moves/Jolteon/JoltElectroWebMark.prefab").Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                GameObject spawnedObject = Instantiate(prefab);
                spawnedObject.GetComponent<NetworkObject>().Spawn(true);

                ElectroWebMark mark = spawnedObject.GetComponent<ElectroWebMark>();
                mark.SetTargetServerRPC(pokemon.NetworkObjectId, tickDamage);
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };
    }
}
