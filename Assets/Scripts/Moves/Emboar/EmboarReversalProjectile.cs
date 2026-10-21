using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Reversal owns its growing hitbox, initialization and lifetime independently.
public class EmboarReversalProjectile : NetworkBehaviour
{
    private readonly StatusEffect stun = new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0);
    private DamageInfo damageInfo;
    private Team team;
    private bool initialized;
    private Tween growth;
    private readonly System.Collections.Generic.HashSet<Pokemon> hit = new System.Collections.Generic.HashSet<Pokemon>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer) StartCoroutine(Watchdog());
    }

    private IEnumerator Watchdog()
    {
        yield return new WaitForSeconds(3f);
        if (IsSpawned) NetworkObject.Despawn(true);
    }

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector2 direction, DamageInfo info)
    {
        if (initialized || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(info.attackerId, out var actor)
            || !actor.TryGetComponent(out PlayerManager manager)) return;
        initialized = true;
        damageInfo = info;
        team = manager.CurrentTeam.Team;
        Vector3 forward = new Vector3(direction.x, 0, direction.y);
        if (forward.sqrMagnitude < 0.001f) forward = actor.transform.forward;
        transform.SetPositionAndRotation(actor.transform.position + Vector3.up * 1.58f,
            Quaternion.Euler(5, Quaternion.LookRotation(forward).eulerAngles.y, 0));
        transform.position += transform.forward * 1.73f;
        transform.localScale = new Vector3(1, 1, 0);
        growth = transform.DOScaleZ(1, 0.25f).OnComplete(() => StartCoroutine(Expire()));
    }

    private IEnumerator Expire()
    {
        yield return new WaitForSeconds(0.1f);
        if (IsSpawned) NetworkObject.Despawn(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !initialized || !other.TryGetComponent(out Pokemon target)
            || !Aim.Instance.CanPokemonBeTargeted(other.gameObject, AimTarget.NonAlly, team) || !hit.Add(target)) return;
        target.TakeDamageRPC(damageInfo);
        target.AddStatusEffect(stun);
    }

    public override void OnNetworkDespawn()
    {
        growth?.Kill();
        StopAllCoroutines();
        base.OnNetworkDespawn();
    }
}
