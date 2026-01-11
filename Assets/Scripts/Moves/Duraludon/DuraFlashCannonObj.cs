using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class DuraFlashCannonObj : NetworkBehaviour
{
    [SerializeField] private GameObject ruptureObject;
    [SerializeField] private BoxCollider ruptureCollider;

    private DamageInfo damage;
    private StatChange speedReduction = new StatChange(50, Stat.Speed, 1f, true, false, true, 0);
    private StatusEffect stunEffect = new StatusEffect(StatusType.Incapacitated, 1f, true, 0);

    private Team orangeTeam;
    private bool initialized = false;
    private bool isUpgraded = false;


    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 direction, Vector3 startPosition, Team orangeTeam, DamageInfo damageInfo, bool isUpgraded)
    {
        this.orangeTeam = orangeTeam;
        this.isUpgraded = isUpgraded;
        transform.position = startPosition;
        transform.rotation = Quaternion.LookRotation(direction);
        damage = damageInfo;
        initialized = true;

        DoAnimationRPC();
    }

    [Rpc(SendTo.Everyone)]
    private void DoAnimationRPC()
    {
        ruptureObject.transform.localScale = new Vector3(1f, 1f, 0f);

        ruptureObject.transform.DOScaleZ(1f, 0.35f).SetEase(Ease.Linear).onComplete += () =>
        {
            ruptureObject.transform.DOScaleZ(1f, 0.15f).SetEase(Ease.Linear).onComplete += () =>
            {
                ruptureObject.SetActive(false);
            };
        };

        if (IsServer)
        {
            StartCoroutine(CheckForEnemiesRoutine());
        }
    }

    private IEnumerator CheckForEnemiesRoutine()
    {
        if (!initialized || !IsServer)
        {
            yield break;
        }

        List<Pokemon> hitPokemons = new List<Pokemon>();

        for (int i = 0; i < 10; i++)
        {
            foreach (var hit in Physics.OverlapBox(ruptureCollider.bounds.center, ruptureCollider.bounds.extents, ruptureCollider.transform.rotation))
            {
                if (hit.TryGetComponent(out Pokemon pokemon))
                {
                    if (Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam, true) && !hitPokemons.Contains(pokemon))
                    {
                        hitPokemons.Add(pokemon);
                        pokemon.AddStatChange(speedReduction);
                        pokemon.TakeDamageRPC(damage);

                        if (isUpgraded)
                        {
                            pokemon.AddStatusEffect(stunEffect);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(0.035f);
        }
    }

    [Rpc(SendTo.Server)]
    public void DestroySelfRPC()
    {
        NetworkObject.Despawn(true);
    }
}
