using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EarthquakeArea : NetworkBehaviour
{
    [SerializeField] private ParticleSystem landParticles;
    
    private float maxRadius = 2.5f;
    private DamageInfo firstWave;
    private DamageInfo secondWave;
    private DamageInfo thirdWave;

    private StatusEffect stun = new StatusEffect(StatusType.Incapacitated, 1.2f, true, 0);
    private StatChange normalSlow = new StatChange(20, Stat.Speed, 0.5f, true, false, true, 0);
    private StatChange finalSlow = new StatChange(25, Stat.Speed, 3.2f, true, false, true, 0);

    private bool orangeTeam;

    private Dictionary<Pokemon, int> stackAmounts = new Dictionary<Pokemon, int>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, DamageInfo first, DamageInfo second, DamageInfo third, bool orangeTeam)
    {
        transform.position = position;
        this.orangeTeam = orangeTeam;
        firstWave = first;
        secondWave = second;
        thirdWave = third;
        stackAmounts.Clear();

        StartCoroutine(EarthquakeWaves());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayParticlesRPC()
    {
        landParticles.Play();
    }

    private IEnumerator EarthquakeWaves()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.6f);
            PlayParticlesRPC();
            Collider[] colliders = Physics.OverlapSphere(transform.position, maxRadius);

            foreach (Collider col in colliders)
            {
                if (!Aim.Instance.CanPokemonBeTargeted(col.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    continue;
                }

                if (col.TryGetComponent(out Pokemon pokemon))
                {
                    if (stackAmounts.ContainsKey(pokemon))
                    {
                        stackAmounts[pokemon]++;
                    }
                    else
                    {
                        stackAmounts.Add(pokemon, 1);
                    }

                    DamageInfo damageInfo;
                    switch (i)
                    {
                        case 0:
                            damageInfo = firstWave;
                            break;
                        case 1:
                            damageInfo = secondWave;
                            break;
                        case 2:
                            damageInfo = thirdWave;
                            break;
                        default:
                            damageInfo = firstWave;
                            break;
                    }

                    pokemon.TakeDamage(damageInfo);
                    pokemon.AddStatChange(normalSlow);

                    if (stackAmounts.TryGetValue(pokemon, out int stack))
                    {
                        if (stack == 3)
                        {
                            pokemon.AddStatusEffect(stun);
                            pokemon.AddStatChange(finalSlow);
                        }
                    }
                }
            }
        }

        yield return new WaitForSeconds(0.5f);
        NetworkObject.Despawn(true);
    }
}
