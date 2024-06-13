using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class FreezeDryProjectile : NetworkBehaviour
{
    public float speed = 30f;
    private float maxDistance; // Set the maximum distance the projectile should travel

    private DamageInfo damageInfo;
    private Vector3 direction;
    private float distanceTraveled = 0f;
    private bool orangeTeam;

    private bool canMove = false;
    private bool gaveDamage = false;

    private List<Pokemon> pokemonList = new List<Pokemon>();

    public event Action<int> OnMoveHit;

    public void SetDirection(Vector3 startingPosition, Vector2 direction, DamageInfo info, float maxDistance)
    {
        transform.position = startingPosition;
        this.direction = new Vector3(direction.x, 0, direction.y);
        damageInfo = info;
        this.maxDistance = maxDistance;
        orangeTeam = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<PlayerManager>().OrangeTeam;
        canMove = true;
    }

    void Update()
    {
        if (!IsOwner || !canMove)
        {
            return;
        }

        // Move the projectile forward
        Vector3 moveVector = new Vector3(direction.x * speed * Time.deltaTime, direction.y * speed * Time.deltaTime, direction.z * speed * Time.deltaTime);
        transform.Translate(moveVector);

        // Update the distance traveled
        distanceTraveled += speed * Time.deltaTime;

        // Check if the projectile has traveled the maximum distance
        if (distanceTraveled >= maxDistance && !gaveDamage)
        {
            GiveDamage();
            DespawnObjectRPC();
            gaveDamage = true;
        }
    }

    [Rpc(SendTo.Server)]
    private void DespawnObjectRPC()
    {
        NetworkObject.Despawn(true);
    }

    private void GiveDamage()
    {
        OnMoveHit?.Invoke(pokemonList.Count);

        for (int i = pokemonList.Count; i > 0; i--)
        {
            if (pokemonList[i - 1] == null)
            {
                pokemonList.RemoveAt(i - 1);
                continue;
            }

            pokemonList[i - 1].TakeDamage(damageInfo);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner)
        {
            return;
        }

        PlayerManager playerManager;
        if (other.TryGetComponent(out playerManager))
        {
            if (playerManager.OrangeTeam == orangeTeam)
            {
                return;
            }
        }

        Pokemon pokemon;
        if (other.TryGetComponent(out pokemon))
        {
            // Deal damage to the target
            if (!pokemonList.Contains(pokemon))
            {
                pokemonList.Add(pokemon);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner)
        {
            return;
        }

        Pokemon pokemon;
        if (other.TryGetComponent(out pokemon))
        {
            // Deal damage to the target
            if (pokemonList.Contains(pokemon))
            {
                pokemonList.Remove(pokemon);
            }
        }
    }
}
