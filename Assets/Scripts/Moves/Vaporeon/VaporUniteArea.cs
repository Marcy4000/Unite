using DG.Tweening;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class VaporUniteArea : NetworkBehaviour
{
    private DamageInfo damageInfo;
    private DamageInfo healAmount;

    // Legacy prefab compatibility; new casts use the server-side Vaporeon runtime.
    private StatChange defDebuff = new StatChange(35, Stat.Defense, 5f, true, false, true, 0);
    private StatChange spDefDebuff = new StatChange(35, Stat.SpDefense, 5f, true, false, true, 0);

    private Team vaporeonTeam;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(DamageInfo damageInfo, DamageInfo healAmount, Team vaporeonTeam, Vector3 position)
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
            var seen = new HashSet<Pokemon>();

            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                Collider hit = colliders[i];

                if (hit == null || !hit.TryGetComponent(out Pokemon target) || target.CurrentHp <= 0 || !seen.Add(target))
                {
                    continue;
                }

                if (Aim.Instance.CanPokemonBeTargeted(hit.gameObject, AimTarget.NonAlly, vaporeonTeam))
                {
                    if (hit.TryGetComponent(out Pokemon pokemon))
                    {
                        pokemon.TakeDamageRPC(damageInfo);
                        pokemon.AddStatChange(defDebuff);
                        pokemon.AddStatChange(spDefDebuff);
                    }
                }
                else if (hit.TryGetComponent(out Pokemon ally) && ally.Type == PokemonType.Player && ally.TeamMember.IsOnSameTeam(vaporeonTeam))
                {
                    ally.HealDamageRPC(healAmount);
                }
            }

            NetworkObject.Despawn(true);
        });
    }
}
