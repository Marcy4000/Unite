using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class JoltSwiftProjectile : NetworkBehaviour
{
    private static readonly StatusType[] invulnerableStatuses = { StatusType.Invincible, StatusType.Untargetable, StatusType.Invisible };

    [SerializeField] private GameObject starImage;
    [SerializeField] private float maxDistance = 20f; // Maximum distance before destruction

    private bool isBigStar;

    private DamageInfo bigDamage;
    private DamageInfo smallDamage;

    private float speed = 10f;
    private Vector3 direction;

    private bool orangeTeam;

    private bool initialized = false;
    private ulong lastHit = 0;

    private float traveledDistance = 0f;

    private string path = "Moves/Jolteon/JoltSwift";
    private Rigidbody rb;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 startPos,Vector3 direction, bool isBigStar, DamageInfo bigDamage, DamageInfo smallDamage, bool orangeTeam, ulong lastHit=0)
    {
        transform.position = startPos;
        this.direction = new Vector3(direction.x, 0, direction.z).normalized; // Ensure the direction is on the xz plane
        this.isBigStar = isBigStar;
        this.bigDamage = bigDamage;
        this.smallDamage = smallDamage;
        this.orangeTeam = orangeTeam;
        this.lastHit = lastHit;

        starImage.transform.localScale = isBigStar ? new Vector3(1f, 1f, 1f) : new Vector3(0.5f, 0.5f, 1f);

        if (!isBigStar)
        {
            traveledDistance /= 1.5f;
        }

        initialized = true;
    }

    private void Start()
    {
        if (!IsServer)
        {
            return;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false; // Disable gravity
        rb.isKinematic = true; // Disable physics-based movement
    }

    void Update()
    {
        if (!initialized || !IsServer)
            return;

        // Move the projectile
        Vector3 movement = direction * speed * Time.deltaTime;
        transform.position += movement;
        traveledDistance += movement.magnitude;

        // Destroy the projectile if it has traveled the maximum distance
        if (traveledDistance >= maxDistance)
        {
            if (isBigStar)
            {
                Split(null);
            }

            NetworkObject.Despawn(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.CompareTag("Wall"))
        {
            // Perform a raycast to get the exact hit point and normal
            Ray ray = new Ray(transform.position, direction); // Slight offset backwards to ensure the ray starts outside the collider
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, speed * Time.deltaTime * 10f, LayerMask.GetMask("Default"))) // Adjust distance as needed
            {
                // Bounce off the wall
                direction = Vector3.Reflect(direction, hit.normal);
                direction = new Vector3(direction.x, 0, direction.z).normalized; // Ensure the direction is on the xz plane
            }
        }
        else if (other.TryGetComponent(out PlayerManager player))
        {
            if (player.OrangeTeam == orangeTeam || player.Pokemon.HasAnyStatusEffect(invulnerableStatuses) || player.NetworkObjectId == lastHit)
            {
                return;
            }

            OnStarHit(player.Pokemon);
        }
        else if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (pokemon.NetworkObjectId == lastHit)
            {
                return;
            }

            OnStarHit(pokemon);
        }
    }

    private void OnStarHit(Pokemon pokemon)
    {
        DamageInfo damageInfo = isBigStar ? bigDamage : smallDamage;

        pokemon.TakeDamage(damageInfo);

        if (isBigStar)
        {
            Split(pokemon);
        }

        NetworkObject.Despawn(gameObject);
    }

    private void Split(Pokemon pokemon)
    {
        // Calculate the directions for the smaller stars
        Vector3 direction1 = direction;
        Vector3 direction2 = Quaternion.Euler(0, 60, 0) * direction;
        Vector3 direction3 = Quaternion.Euler(0, -60, 0) * direction;

        // Spawn the first small star with the original direction
        SpawnStar(direction1, pokemon);

        // Spawn the second small star with +60 degrees direction
        SpawnStar(direction2, pokemon);

        // Spawn the third small star with -60 degrees direction
        SpawnStar(direction3, pokemon);
    }

    private void SpawnStar(Vector3 direction, Pokemon pokemon)
    {
        ulong clientId;

        if (pokemon != null)
        {
            clientId = pokemon.NetworkObjectId;
        }else
        {
            clientId = 0;
        }

        // Instantiate the small star
        GameObject spawnedObject = Instantiate(Resources.Load(path, typeof(GameObject))) as GameObject;
        spawnedObject.GetComponent<NetworkObject>().Spawn(true);
        spawnedObject.GetComponent<JoltSwiftProjectile>().InitializeRPC(transform.position, direction, false, bigDamage, smallDamage, orangeTeam, clientId);
    } 
}
