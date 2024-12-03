using Unity.Netcode;
using UnityEngine;

public class FlygonDigWarning : NetworkBehaviour
{
    private Team orangeTeam;

    private DamageInfo damageInfo;
    private StatChange slow = new StatChange(15, Stat.Speed, 2f, true, false, true, 0);

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 startPos, Team orangeTeam, DamageInfo damage)
    {
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
    public void GiveDamageRPC()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);

        foreach (Collider hit in colliders)
        {
            if (!Aim.Instance.CanPokemonBeTargeted(hit.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                continue;
            }

            if (hit.TryGetComponent(out Pokemon pokemon))
            {
                pokemon.TakeDamageRPC(damageInfo);
                pokemon.AddStatChange(slow);
            }
        }

        NetworkObject.Despawn(true);
    }
}
