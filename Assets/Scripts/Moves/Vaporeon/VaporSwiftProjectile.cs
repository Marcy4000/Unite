using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class VaporSwiftProjectile : NetworkBehaviour
{
    private DamageInfo damageInfo;

    private bool orangeTeam;
    private bool isReady;

    private float starCooldown = 0.7f;
    private float duration = 3.5f;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(DamageInfo damageInfo, bool orangeTeam, Vector3 position)
    {
        this.damageInfo = damageInfo;
        this.orangeTeam = orangeTeam;
        transform.position = position;
        isReady = true;
    }

    private void Update()
    {
        if (!isReady || !IsServer)
        {
            return;
        }

        starCooldown -= Time.deltaTime;
        duration -= Time.deltaTime;
        
        if (starCooldown <= 0)
        {
            starCooldown = 0.7f;
            DoDamage();
        }

        if (duration <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void DoDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 1f);

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out PlayerManager playerManager))
            {
                if (playerManager.OrangeTeam == orangeTeam)
                {
                    return;
                }
            }

            if (hit.TryGetComponent(out Pokemon pokemon))
            {
                pokemon.TakeDamage(damageInfo);
            }
        }
    }
}
