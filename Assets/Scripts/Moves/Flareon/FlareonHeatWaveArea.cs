using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class FlareonHeatWaveArea : NetworkBehaviour
{
    [SerializeField] private ParticleSystem particles;

    private DamageInfo damage;
    private DamageInfo burnedDamage;

    private Team orangeTeam;
    private bool initialized;
    private bool isUpgraded;

    private GameObject player;

    private float timeToWait = 0.75f;
    private float duration = 6f;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong flareonID, Team orangeTeam, DamageInfo damage, DamageInfo burnedDamage, bool isUpgraded)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(flareonID, out NetworkObject playerNetworkObj))
        {
            player = playerNetworkObj.gameObject;
            transform.position = player.transform.position;
        }
        this.orangeTeam = orangeTeam;
        this.damage = damage;
        this.isUpgraded = isUpgraded;
        this.burnedDamage = burnedDamage;

        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        transform.position = player.transform.position + new Vector3(0, 0.4f, 0);

        timeToWait -= Time.deltaTime;
        duration -= Time.deltaTime;

        if (timeToWait <= 0)
        {
            timeToWait = isUpgraded ? 0.6f : 0.75f;
            StartCoroutine(DoWave());
        }

        if (duration <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private IEnumerator DoWave()
    {
        PlayParticlesRPC();

        yield return new WaitForSeconds(0.1f);

        GameObject[] enemies = Aim.Instance.AimInCircleAtPosition(transform.position, 3.7f, AimTarget.NonAlly, orangeTeam);

        foreach (GameObject enemy in enemies)
        {
            if (enemy.TryGetComponent(out Pokemon pokemon))
            {
                if (pokemon.HasStatusEffect(StatusType.Burned))
                {
                    pokemon.TakeDamageRPC(burnedDamage);
                }
                else
                {
                    pokemon.TakeDamageRPC(damage);
                }
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayParticlesRPC()
    {
        particles.Play();
    }
}
