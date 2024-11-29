using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CresseliaPsybeamProjectile : NetworkBehaviour
{
    private StatusEffect stun = new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0);

    private DamageInfo damageInfo;
    private Team orangeTeam;

    public event System.Action OnHit;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector2 direction, DamageInfo info)
    {
        transform.position = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].transform.position;
        transform.position += new Vector3(0, 1.58f, 0);

        transform.localScale = new Vector3(1f, 1f, 0f);
        transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y));
        transform.rotation = Quaternion.Euler(5, transform.rotation.eulerAngles.y, 0);

        transform.position += transform.forward * 1.73f;

        damageInfo = info;

        orangeTeam = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<PlayerManager>().CurrentTeam.Team;

        transform.DOScaleZ(1f, 0.25f).onComplete += () => {
            StartCoroutine(DestroyDelayed());
        };
    }

    private IEnumerator DestroyDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        NetworkObject.Despawn(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!Aim.Instance.CanPokemonBeTargeted(other.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                return;
            }

            pokemon.TakeDamage(damageInfo);
            pokemon.AddStatusEffect(stun);

            NotifyAboutHitRPC();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyAboutHitRPC()
    {
        OnHit?.Invoke();
    }
}
