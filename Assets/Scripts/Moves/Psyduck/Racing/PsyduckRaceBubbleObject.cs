using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceBubbleObject : NetworkBehaviour
{
    [SerializeField] private LayerMask layerMask;

    private GameObject pokemonToIgnore;
    private bool initialized = false;

    private float timer = 10f;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong ignoreID, Vector3 startPos, Vector3 endPos)
    {
        pokemonToIgnore = NetworkManager.Singleton.SpawnManager.SpawnedObjects[ignoreID].gameObject;
        transform.position = startPos;

        if (Physics.Linecast(startPos, endPos, out RaycastHit hit, layerMask))
        {
            endPos = hit.point;
        }

        Sequence bubbleSequence = DOTween.Sequence();
        bubbleSequence.Append(transform.DOMove(endPos, 1.25f).SetEase(Ease.InOutSine));
        bubbleSequence.Join(transform.DOLocalMoveY(0.3f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));

        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
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
        if (!IsServer || !initialized)
        {
            return;
        }

        if (other.gameObject == pokemonToIgnore)
        {
            return;
        }

        if (other.gameObject.CompareTag("Player"))
        {
            if (other.gameObject.TryGetComponent(out PlayerManager playerManager))
            {
                playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 1.2f, true, 0));
                NetworkObject.Despawn(true);
            }
        }
    }
}
