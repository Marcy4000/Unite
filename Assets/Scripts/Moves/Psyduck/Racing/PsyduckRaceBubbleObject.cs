using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceBubbleObject : NetworkBehaviour
{
    [SerializeField] private LayerMask layerMask;

    private GameObject pokemonToIgnore;
    private bool initialized = false;

    private float timer = 10f;
    private Tween travel;
    private Tween bob;

    private void Awake()
    {
        PsyduckRaceHit.PrepareTrigger(gameObject);
        if (transform.childCount > 0)
        {
            Transform visual = transform.GetChild(0);
            bob = visual.DOLocalMoveY(visual.localPosition.y + 0.3f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
    }

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong ignoreID, Vector3 startPos, Vector3 endPos)
    {
        if (initialized) return;
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(ignoreID, out var owner))
        {
            NetworkObject.Despawn(true);
            return;
        }
        pokemonToIgnore = owner.gameObject;
        transform.position = startPos;

        if (Physics.Linecast(startPos, endPos, out RaycastHit hit, layerMask))
        {
            endPos = hit.point;
        }

        travel = transform.DOMove(endPos, 1.25f).SetEase(Ease.InOutSine);

        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || !initialized)
        {
            return;
        }

        var playerManager = other.GetComponentInParent<PlayerManager>();
        if (playerManager == null || playerManager.gameObject == pokemonToIgnore)
        {
            return;
        }

        if (PsyduckRaceHit.Apply(playerManager, new StatusEffect(StatusType.Incapacitated, 1.2f, true, 0)))
            NetworkObject.Despawn(true);
    }

    public override void OnNetworkDespawn()
    {
        travel?.Kill();
        bob?.Kill();
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        travel?.Kill();
        bob?.Kill();
        base.OnDestroy();
    }
}
