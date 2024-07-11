using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MeowsticMPsychicArea : NetworkBehaviour
{
    [SerializeField] private LayerMask pokemonMask;
    private PlayerManager meowstic;
    private PlayerManager target;

    private bool initialized = false;

    private float activeTime = 3.5f;

    private DamageInfo damage = new DamageInfo(0, 0.45f, 3, 55, DamageType.Special);
    private DamageInfo heal = new DamageInfo(0, 0.35f, 2, 45, DamageType.Special);

    private StatChange defBoost = new StatChange(15, Stat.Defense, 3.5f, true, true, true, 0);
    private StatChange spDefBoost = new StatChange(15, Stat.Defense, 3.5f, true, true, true, 0);

    private StatChange atkDebuff = new StatChange(20, Stat.Attack, 3f, true, false, true, 0);
    private StatChange spAtkDebuff = new StatChange(20, Stat.SpAttack, 3f, true, false, true, 0);

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong targetID, ulong meowsticID)
    {
        meowstic = NetworkManager.Singleton.SpawnManager.SpawnedObjects[meowsticID].GetComponent<PlayerManager>();
        target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<PlayerManager>();
        damage.attackerId = meowsticID;
        heal.attackerId = meowsticID;

        target.Pokemon.AddStatChange(defBoost);
        target.Pokemon.AddStatChange(spDefBoost);

        initialized = true;

        StartCoroutine(DoDamage());
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        transform.position = target.transform.position;

        activeTime -= Time.deltaTime;

        if (activeTime <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private IEnumerator DoDamage()
    {
        while (activeTime > 0)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 3.5f, pokemonMask);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider != null && hitCollider.TryGetComponent(out Pokemon pokemon))
                {
                    if (Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, meowstic.OrangeTeam))
                    {
                        pokemon.TakeDamage(damage);
                        if (pokemon.GetStatChange(Stat.Attack) > -60)
                        {
                            pokemon.AddStatChange(atkDebuff);
                            pokemon.AddStatChange(spAtkDebuff);
                        }
                    }
                }
            }
            target.Pokemon.HealDamage(heal);
            yield return new WaitForSeconds(0.8f);
        }
    }
}
