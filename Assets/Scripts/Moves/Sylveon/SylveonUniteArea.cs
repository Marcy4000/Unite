using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SylveonUniteArea : NetworkBehaviour
{
    [SerializeField] private LayerMask pokemonLayer;
    private DamageInfo damageInfo;
    private bool orangeTeam;

    [Rpc(SendTo.Everyone)]
    public void InitializeRPC(DamageInfo damageInfo, bool orangeTeam)
    {
        this.damageInfo = damageInfo;
        this.orangeTeam = orangeTeam;
    }

    [Rpc(SendTo.Server)]
    public void DoDamageRPC()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 5f, pokemonLayer);
        foreach (Collider collider in colliders)
        {
            if (!Aim.Instance.CanPokemonBeTargeted(collider.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                continue;
            }

            collider.GetComponent<Pokemon>().TakeDamage(damageInfo);
        }

        StartCoroutine(Despawn());
    }

    private IEnumerator Despawn()
    {
        yield return new WaitForSeconds(0.1f);
        NetworkObject.Despawn(true);
    }
}
