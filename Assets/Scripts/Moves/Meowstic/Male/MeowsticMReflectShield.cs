using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MeowsticMReflectShield : NetworkBehaviour
{
    [SerializeField] private LayerMask pokemonMask;

    private PlayerManager target;
    private ulong meowsticID;

    private bool initialized = false;
    private bool isExploding = false;

    private float activeTime = 4f;
    private int storedDamage = 0;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong targetID, ulong meowsticID)
    {
        target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<PlayerManager>();
        target.Pokemon.OnDamageTaken += OnDamageTaken;
        target.Pokemon.OnDeath += OnTargetDeath;
        this.meowsticID = meowsticID;
        initialized = true;
    }

    private void OnDamageTaken(DamageInfo damage)
    {
        storedDamage += Mathf.FloorToInt(target.Pokemon.CalculateDamage(damage) * 0.35f);
        storedDamage = Mathf.Clamp(storedDamage, 0, 2500);
    }

    private void OnTargetDeath(DamageInfo info)
    {
        StartCoroutine(Explode());
        isExploding = true;
        initialized = false;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        transform.position = target.transform.position;

        activeTime -= Time.deltaTime;

        if (activeTime <= 0 && !isExploding)
        {
            StartCoroutine(Explode());
            isExploding = true;
        }
    }

    private IEnumerator Explode()
    {
        target.Pokemon.OnDamageTaken -= OnDamageTaken;
        target.Pokemon.OnDeath -= OnTargetDeath;

        Collider[] colliders = Physics.OverlapSphere(transform.position, 4f, pokemonMask);

        foreach (var col in colliders)
        {
            if (Aim.Instance.CanPokemonBeTargeted(col.gameObject, AimTarget.NonAlly, target.CurrentTeam))
            {
                col.GetComponent<Pokemon>().TakeDamageRPC(new DamageInfo(meowsticID, 0f, 0, (short)storedDamage, DamageType.True));
            }
        }

        yield return new WaitForSeconds(0.5f);

        NetworkObject.Despawn(true);
    }
}
