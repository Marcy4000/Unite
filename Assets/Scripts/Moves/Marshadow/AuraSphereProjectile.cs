using System;
using Unity.Netcode;
using UnityEngine;

public class AuraSphereProjectile : NetworkBehaviour
{
    public float speed = 30f;
    private float maxDistance; // Set the maximum distance the projectile should travel

    private DamageInfo damageInfo;
    private Vector3 direction;
    private float distanceTraveled = 0f;
    private Team orangeTeam;

    private bool canMove = false;

    public event Action<ulong> OnMoveHit;

    public void SetDirection(Vector3 startingPosition, Vector2 direction, DamageInfo info, float maxDistance)
    {
        transform.position = startingPosition;
        this.direction = new Vector3(direction.x, 0, direction.y);
        damageInfo = info;
        this.maxDistance = maxDistance;
        orangeTeam = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<PlayerManager>().CurrentTeam.Team;
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
        if (distanceTraveled >= maxDistance)
        {
            OnMoveHit?.Invoke(69696969);
            DespawnObjectRPC();
        }
    }

    [Rpc(SendTo.Server)]
    private void DespawnObjectRPC()
    {
        NetworkObject.Despawn(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                pokemon.TakeDamageRPC(damageInfo);
                OnMoveHit?.Invoke(pokemon.NetworkObjectId);
                DespawnObjectRPC();
            }
        }
    }
}
