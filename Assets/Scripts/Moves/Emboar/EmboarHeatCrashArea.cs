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

    private Team orangeTeam;
    private bool knockup;
    private bool upgraded;

    private PlayerManager emboar;

    private float waveDuration = 0.6f;

    private Vector3 startingPosition;
    private Vector3 startingScale;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, Vector3 rotation, DamageInfo first, DamageInfo second, DamageInfo third, bool knockup, bool upgraded)
    {
        emboar = NetworkManager.Singleton.SpawnManager.SpawnedObjects[first.attackerId].GetComponent<PlayerManager>();

        transform.position = position;
        transform.rotation = Quaternion.LookRotation(rotation);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        orangeTeam = emboar.CurrentTeam.Team;
        firstWave = first;
        secondWave = second;
        thirdWave = third;
        this.knockup = knockup;
        this.upgraded = upgraded;

        startingPosition = maskObject.transform.localPosition;
        startingScale = maskObject.transform.localScale;

        waveDuration = upgraded ? 0.4f : 0.6f;

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

    [Rpc(SendTo.ClientsAndHost)]
    private void ResetAreaPositionRPC()
    {
        maskObject.transform.localPosition = startingPosition;
        maskObject.transform.localScale = startingScale;
        landParticles.transform.localPosition = maskObject.transform.localPosition;
    }

    private IEnumerator EarthquakeWaves()
    {
        int waveNumber = upgraded ? 6 : 3;

        for (int i = 0; i < waveNumber; i++)
        {
            yield return new WaitForSeconds(waveDuration);
            PlayParticlesRPC();

            if (i > 0)
            {
                if (i % 3 == 0)
                {
                    ResetAreaPositionRPC();
                }
                else
                {
                    UpdateAreaPositionRPC();
                }
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
                    switch (i % 3)
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
                        pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.4f, true, 0));
                        pokemon.ApplyKnockupRPC(1f, 0.4f);
                    }
                }
            }
        }

        yield return new WaitForSeconds(0.5f);
        NetworkObject.Despawn(true);
    }
}
