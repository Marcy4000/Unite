using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BoomburstHitbox : NetworkBehaviour
{
    [SerializeField] private ParticleSystem particles;

    private DamageInfo closeDamage, farDamage;
    private float basePushForce = 20f;

    private Team orangeTeam;

    private StatChange defReductionClose = new StatChange(15, Stat.Defense, 3f, true, false, true, 0);
    private StatChange spDefReductionClose = new StatChange(15, Stat.Defense, 3f, true, false, true, 0);
    private StatChange defReductionFar = new StatChange(10, Stat.Defense, 3f, true, false, true, 0);
    private StatChange spDefReductionFar = new StatChange(10, Stat.Defense, 3f, true, false, true, 0);

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 startPos, Team orangeTeam, DamageInfo close, DamageInfo far)
    {
        transform.position = startPos;
        this.orangeTeam = orangeTeam;
        closeDamage = close;
        farDamage = far;
        
        StartCoroutine(DoPushback());
    }

    private IEnumerator DoPushback()
    {
        yield return new WaitForSeconds(0.6f);

        PlayParticlesRPC();

        Collider[] targets = Physics.OverlapSphere(transform.position, 5f);

        foreach (Collider enemy in targets)
        {
            if (enemy.TryGetComponent(out Pokemon pokemon))
            {
                if (!Aim.Instance.CanPokemonBeTargeted(enemy.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                float fallOffFactor = Mathf.Max(0, 1 - distance / 5f);
                float pushForce = basePushForce * fallOffFactor;

                Vector3 direction = (enemy.transform.position - transform.position).normalized;
                pokemon.ApplyKnockbackRPC(direction, pushForce);
                pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.2f, true, 0));

                if (distance > 3f)
                {
                    pokemon.TakeDamageRPC(farDamage);
                }
                else
                {
                    pokemon.TakeDamageRPC(closeDamage);
                }

                if (distance <= 2f)
                {
                    pokemon.AddStatChange(defReductionClose);
                    pokemon.AddStatChange(spDefReductionClose);
                }
                else if (distance > 2f && distance < 4f)
                {
                    pokemon.AddStatChange(defReductionFar);
                    pokemon.AddStatChange(spDefReductionFar);
                }
            }
        }

        yield return new WaitForSeconds(2f);
        NetworkObject.Despawn(true);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayParticlesRPC()
    {
        particles.Play();
    }
}
