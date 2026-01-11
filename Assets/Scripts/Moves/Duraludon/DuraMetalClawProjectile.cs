using Unity.Netcode;
using UnityEngine;

public class DuraMetalClawProjectile : NetworkBehaviour
{
    private float speed = 20f;
    private float range = 6.5f;
    private DamageInfo damage;
    private StatChange speedReduction = new StatChange(30, Stat.Speed, 2f, true, false, true, 0);

    private Vector3 direction;
    private Vector3 startPosition;

    private Team orangeTeam;
    private bool initialized = false;


    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 direction, Vector3 startPosition, Team orangeTeam, DamageInfo damageInfo)
    {
        this.orangeTeam = orangeTeam;
        this.direction = direction;
        transform.position = startPosition;
        transform.rotation = Quaternion.LookRotation(direction);
        this.startPosition = startPosition;
        damage = damageInfo;
        initialized = true;
    }

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

        if (Aim.Instance.CanPokemonBeTargeted(other.gameObject, AimTarget.NonAlly, orangeTeam, true) && other.TryGetComponent(out Pokemon pokemon))
        {
            pokemon.AddStatChange(speedReduction);
            pokemon.TakeDamageRPC(damage);
        }
    }
}
