using UnityEngine;
using Unity.Netcode;

public class MoveForwardProjectile : NetworkBehaviour
{
    public float speed = 30f;
    private float maxDistance; // Set the maximum distance the projectile should travel

    private DamageInfo damageInfo;
    private Vector3 direction;
    private float distanceTraveled = 0f;
    private Team orangeTeam;

    public void SetDirection(Vector2 direction, DamageInfo info, float maxDistance)
    {
        this.direction = new Vector3(direction.x, 0, direction.y);
        damageInfo = info;
        this.maxDistance = maxDistance;
        orangeTeam = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<PlayerManager>().CurrentTeam.Team;
    }

    void Update()
    {
        if (!IsOwner)
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
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner)
        {
            return;
        }

        if (!Aim.Instance.CanPokemonBeTargeted(other.gameObject, AimTarget.NonAlly, orangeTeam))
        {
            return;
        }

        if (other.gameObject.GetComponent<Pokemon>())
        {
            // Deal damage to the target
            other.GetComponent<Pokemon>().TakeDamage(damageInfo);
        }
    }
}