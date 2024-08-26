using Unity.Netcode;
using UnityEngine;

public class AuroraBeamProjectile : NetworkBehaviour
{
    public float speed = 30f;
    private float maxDistance; // Set the maximum distance the projectile should travel

    private StatChange atkSpdDebuff = new StatChange(15, Stat.AtkSpeed, 3f, true, false, true, 0);

    private DamageInfo damageInfo;
    private Vector3 direction;
    private float distanceTraveled = 0f;
    private bool orangeTeam;

    [Rpc(SendTo.Server)]
    public void SetDirectionRPC(Vector2 direction, DamageInfo info, float maxDistance)
    {
        transform.position = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].transform.position;
        this.direction = new Vector3(direction.x, 0, direction.y);
        transform.rotation = Quaternion.LookRotation(this.direction);
        damageInfo = info;
        this.maxDistance = maxDistance;
        orangeTeam = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<PlayerManager>().OrangeTeam;
    }

    void Update()
    {
        if (!IsServer)
        {
            return;
        }

        // Move the projectile forward
        Vector3 moveVector = new Vector3(direction.x * speed * Time.deltaTime, direction.y * speed * Time.deltaTime, direction.z * speed * Time.deltaTime);
        transform.Translate(moveVector, Space.World);

        // Update the distance traveled
        distanceTraveled += speed * Time.deltaTime;

        // Check if the projectile has traveled the maximum distance
        if (distanceTraveled >= maxDistance)
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

        if (!Aim.Instance.CanPokemonBeTargeted(other.gameObject, AimTarget.NonAlly, orangeTeam))
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            pokemon.TakeDamage(damageInfo);
            pokemon.AddStatChange(atkSpdDebuff);
            NetworkObject.Despawn(true);
        }
    }
}
