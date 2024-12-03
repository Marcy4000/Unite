using Unity.Netcode;
using UnityEngine;

public class MoonblastProjectile : NetworkBehaviour
{
    [SerializeField] private LayerMask playerMask;

    public float speed = 30f;
    private float maxDistance; // Set the maximum distance the projectile should travel

    private StatChange atkSpdDebuff = new StatChange(15, Stat.AtkSpeed, 5f, true, false, true, 0);
    private StatChange atkDebuff = new StatChange(30, Stat.Attack, 5f, true, false, true, 0);
    private StatChange spAtkDebuff = new StatChange(20, Stat.SpAttack, 5f, true, false, true, 0);

    private DamageInfo damageInfo;
    private Vector3 direction;
    private float distanceTraveled = 0f;
    private Team orangeTeam;

    [Rpc(SendTo.Server)]
    public void SetDirectionRPC(Vector2 direction, DamageInfo info, float maxDistance)
    {
        transform.position = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].transform.position;
        transform.position += new Vector3(0, 0.5f, 0);

        this.direction = new Vector3(direction.x, 0, direction.y);
        this.maxDistance = maxDistance;
        damageInfo = info;

        orangeTeam = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<PlayerManager>().CurrentTeam.Team;
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

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!Aim.Instance.CanPokemonBeTargeted(other.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                return;
            }

            Explode(other.transform.position);
            
            NetworkObject.Despawn(true);
        }
    }

    private void Explode(Vector3 explosionPoint)
    {
        Collider[] hitColliders = Physics.OverlapSphere(explosionPoint, 1.2f, playerMask);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.TryGetComponent(out Pokemon pokemon))
            {
                if (!Aim.Instance.CanPokemonBeTargeted(hitCollider.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    continue;
                }

                pokemon.TakeDamageRPC(damageInfo);
                pokemon.AddStatChange(atkSpdDebuff);
                pokemon.AddStatChange(atkDebuff);
                pokemon.AddStatChange(spAtkDebuff);
            }
        }
    }
}
