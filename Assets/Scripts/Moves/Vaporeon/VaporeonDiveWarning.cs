using Unity.Netcode;
using UnityEngine;

public class VaporeonDiveWarning : NetworkBehaviour
{
    [SerializeField] private GameObject warningImage;

    private bool orangeTeam;

    private DamageInfo damageInfo;
    private StatusEffect stunEffect = new StatusEffect(StatusType.Incapacitated, 0.35f, true, 0);

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 startPos, bool orangeTeam, DamageInfo damage)
    {
        warningImage.SetActive(true);
        transform.position = startPos;
        this.orangeTeam = orangeTeam;
        damageInfo = damage;
    }

    [Rpc(SendTo.Server)]
    public void DespawnRPC()
    {
        NetworkObject.Despawn(true);
    }

    [Rpc(SendTo.Server)]
    public void DoPushbackRPC()
    {
        warningImage.SetActive(false);

        Collider[] colliders = Physics.OverlapSphere(transform.position, 2f);

        foreach (Collider hit in colliders)
        {
            if (hit.TryGetComponent(out Pokemon pokemon))
            {
                if (!Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    continue;
                }

                pokemon.TakeDamage(damageInfo);
                pokemon.AddStatusEffect(stunEffect);
                Vector3 direction = (pokemon.transform.position - transform.position).normalized;
                pokemon.ApplyKnockbackRPC(direction, 25f);
            }
        }

        NetworkObject.Despawn(true);
    }
}
