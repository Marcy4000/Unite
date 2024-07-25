using Unity.Netcode;
using UnityEngine;

public class FlygonDigWarning : NetworkBehaviour
{
    private bool orangeTeam;

    private DamageInfo damageInfo;
    private StatChange slow = new StatChange(15, Stat.Speed, 2f, true, false, true, 0);

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 startPos, bool orangeTeam, DamageInfo damage)
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
            if (hit.TryGetComponent(out PlayerManager player))
            {
                if (player.OrangeTeam == orangeTeam)
                {
                    continue;
                }

                player.Pokemon.TakeDamage(damageInfo);
                player.Pokemon.AddStatChange(slow);
            }
            else if (hit.TryGetComponent(out Pokemon pokemon))
            {
                pokemon.TakeDamage(damageInfo);
                pokemon.AddStatChange(slow);
            }
        }

        NetworkObject.Despawn(true);
    }
}
