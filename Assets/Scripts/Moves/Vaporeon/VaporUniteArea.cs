using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class VaporUniteArea : NetworkBehaviour
{
    private DamageInfo damageInfo;
    private DamageInfo healAmount;

    private StatChange defDebuff = new StatChange(50, Stat.Defense, 5f, true, false, true, 0);
    private StatChange spDefDebuff = new StatChange(50, Stat.SpDefense, 5f, true, false, true, 0);

    private bool vaporeonTeam;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(DamageInfo damageInfo, DamageInfo healAmount, bool vaporeonTeam, Vector3 position)
    {
        transform.position = position;
        transform.localScale = new Vector3(0.01f, 1f, 0.01f);
        this.damageInfo = damageInfo;
        this.healAmount = healAmount;
        this.vaporeonTeam = vaporeonTeam;
    }

    [Rpc(SendTo.Server)]
    public void DoExplosionRPC()
    {
        transform.DOScale(Vector3.one, 0.6f).OnComplete(() =>
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 6f);

            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                Collider hit = colliders[i];

                if (hit == null)
                {
                    continue;
                }

                if (hit.TryGetComponent(out PlayerManager player))
                {
                    if (player.OrangeTeam == vaporeonTeam)
                    {
                        player.Pokemon.HealDamage(healAmount);
                    }
                    else
                    {
                        player.Pokemon.AddStatChange(defDebuff);
                        player.Pokemon.AddStatChange(spDefDebuff);
                        player.Pokemon.TakeDamage(damageInfo);
                    }
                }
                else if (hit.TryGetComponent(out Pokemon pokemon))
                {
                    pokemon.TakeDamage(damageInfo);
                }
            }

            NetworkObject.Despawn(true);
        });
    }
}
