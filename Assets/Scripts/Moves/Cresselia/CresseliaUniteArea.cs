using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CresseliaUniteArea : NetworkBehaviour
{
    [SerializeField] private LayerMask pokemonLayer;
    private DamageInfo damageInfo;
    private Team orangeTeam;

    private StatusEffect sleep = new StatusEffect(StatusType.Asleep, 1.2f, true, 0);

    [Rpc(SendTo.Everyone)]
    public void InitializeRPC(DamageInfo damageInfo, Team orangeTeam)
    {
        this.damageInfo = damageInfo;
        this.orangeTeam = orangeTeam;
    }

    [Rpc(SendTo.Server)]
    public void DoDamageRPC()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 5.5f, pokemonLayer);
        foreach (Collider collider in colliders)
        {
            if (!Aim.Instance.CanPokemonBeTargeted(collider.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                continue;
            }
            Pokemon pokemon = collider.GetComponent<Pokemon>();

            pokemon.TakeDamage(damageInfo);
            pokemon.AddStatusEffect(sleep);
        }

        StartCoroutine(DespawnDelayed());
    }

    private IEnumerator DespawnDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        NetworkObject.Despawn(true);
    }

    [Rpc(SendTo.Server)]
    public void DespawnRPC()
    {
        NetworkObject.Despawn(true);
    }
}
