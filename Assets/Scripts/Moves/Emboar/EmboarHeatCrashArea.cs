using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EmboarHeatCrashArea : NetworkBehaviour
{
    [SerializeField] private ParticleSystem landParticles;
    [SerializeField] private GameObject maskObject;

    private DamageInfo firstWave;
    private DamageInfo secondWave;
    private DamageInfo thirdWave;

    private StatChange normalSlow = new StatChange(20, Stat.Speed, 0.5f, true, false, true, 0);
    private StatChange finalSlow = new StatChange(25, Stat.Speed, 3.2f, true, false, true, 0);

    private bool orangeTeam;
    private bool knockup;

    private PlayerManager emboar;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, Vector3 rotation, DamageInfo first, DamageInfo second, DamageInfo third, bool knockup)
    {
        emboar = NetworkManager.Singleton.SpawnManager.SpawnedObjects[first.attackerId].GetComponent<PlayerManager>();

        transform.position = position;
        transform.rotation = Quaternion.LookRotation(rotation);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        orangeTeam = emboar.OrangeTeam;
        firstWave = first;
        secondWave = second;
        thirdWave = third;
        this.knockup = knockup;

        StartCoroutine(EarthquakeWaves());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayParticlesRPC()
    {
        landParticles.Play();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateAreaPositionRPC()
    {
        maskObject.transform.localPosition += new Vector3(0f, 0f, 2f);
        landParticles.transform.localPosition = maskObject.transform.localPosition;
        maskObject.transform.localScale += new Vector3(1.5f, 0f, 0f);
    }

    private IEnumerator EarthquakeWaves()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.6f);
            PlayParticlesRPC();

            if (i > 0)
            {
                UpdateAreaPositionRPC();
            }

            Collider[] colliders = Physics.OverlapBox(maskObject.transform.position, maskObject.transform.localScale / 2, maskObject.transform.rotation);

            foreach (Collider col in colliders)
            {
                if (!Aim.Instance.CanPokemonBeTargeted(col.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    continue;
                }

                if (col.TryGetComponent(out Pokemon pokemon))
                {
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

                    if (pokemon.GetMaxHp() < emboar.Pokemon.GetMaxHp())
                    {
                        pokemon.AddStatChange(finalSlow);
                    }

                    if (knockup)
                    {
                        pokemon.ApplyKnockupRPC(1f, 0.4f);
                    }
                }
            }
        }

        yield return new WaitForSeconds(0.5f);
        NetworkObject.Despawn(true);
    }
}
