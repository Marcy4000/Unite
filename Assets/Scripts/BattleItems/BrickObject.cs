using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class BrickObject : NetworkBehaviour
{
    [SerializeField] private Transform modelHolder;
    [SerializeField] private float rotationDuration = 2f;
    [SerializeField] private float delayBetweenRotations = 0.5f;

    private StatusEffect stun = new StatusEffect(StatusType.Incapacitated, 1.5f, true, 0);
    private DamageInfo damage;

    private Team teamToIgnore;
    private Vector3 landPosition;
    private bool initialized;
    private Tween flight;
    private Tween spin;
    private Tween delay;
    private AsyncOperationHandle<GameObject> assetHandle;

    public void HoldAsset(AsyncOperationHandle<GameObject> handle) => assetHandle = handle;

    private void Start()
    {
        StartRotatingBrick();
    }

    private void StartRotatingBrick()
    {
        if (modelHolder == null)
        {
            Debug.LogError("ModelHolder is not assigned!");
            return;
        }

        RotateRandomly();
    }

    private void RotateRandomly()
    {
        // Generate a random rotation
        Vector3 randomRotation = new Vector3(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );

        // Rotate the modelHolder to the random rotation over a specified duration
        spin = modelHolder.DORotate(randomRotation, rotationDuration, RotateMode.FastBeyond360)
                   .SetEase(Ease.InOutSine) // Smooth easing for a natural feel
                   .OnComplete(() =>
                   {
                       // Add a delay and then rotate again
                       delay = DOVirtual.DelayedCall(delayBetweenRotations, RotateRandomly);
                   });
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void InitializeRPC(Vector3 startPosition, Vector3 landPosition, Team teamToIgnore, DamageInfo damage)
    {
        if (initialized || !IsSpawned || !BattleItemRules.Finite(startPosition) || !BattleItemRules.Finite(landPosition)) return;
        initialized = true;
        this.teamToIgnore = teamToIgnore;
        this.landPosition = landPosition;
        this.damage = damage;
        transform.position = startPosition;

        flight = transform.DOJump(landPosition, 2f, 1, 1f)
                 .OnComplete(() =>
                 {
                     StartCoroutine(DoDamage());
                 });
    }

    private IEnumerator DoDamage()
    {
        foreach (Pokemon pokemon in CombatTargeting.SphereTargets(landPosition, 2f))
        {
            if (pokemon.CurrentHp > 0 && !pokemon.TeamMember.IsOnSameTeam(teamToIgnore)
                && !pokemon.HasStatusEffect(StatusType.Invincible) && !pokemon.HasStatusEffect(StatusType.Untargetable))
            {
                pokemon.TakeDamageRPC(damage);
                pokemon.AddStatusEffect(stun);
            }
        }

        yield return new WaitForSeconds(0.1f);

        NetworkObject.Despawn(true);
    }

    public override void OnNetworkDespawn()
    {
        flight?.Kill();
        spin?.Kill();
        delay?.Kill();
        StopAllCoroutines();
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        flight?.Kill();
        spin?.Kill();
        delay?.Kill();
        if (assetHandle.IsValid()) Addressables.Release(assetHandle);
        base.OnDestroy();
    }
}
