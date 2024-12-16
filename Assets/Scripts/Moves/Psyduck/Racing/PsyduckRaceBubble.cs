using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PsyduckRaceBubble : MoveBase
{
    private string wallPrefabPath = "Assets/Prefabs/Objects/Moves/Psyduck/PsyduckRaceBubble.prefab";

    public PsyduckRaceBubble()
    {
        Name = "Bubbles";
        Cooldown = 30.0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
    }

    public override void Update()
    {
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.StartCoroutine(SpawnBubbles());
        }

        base.Finish();

        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }

    private IEnumerator SpawnBubbles()
    {
        Vector3 startPos = playerManager.transform.position;

        Vector3[] bubbleEndPositions = new Vector3[3];

        bubbleEndPositions[0] = playerManager.transform.position + playerManager.transform.forward * 6.5f;
        bubbleEndPositions[1] = playerManager.transform.position + playerManager.transform.forward * 5.5f + playerManager.transform.right * Mathf.Tan(50 * Mathf.Deg2Rad) * 5.5f;
        bubbleEndPositions[2] = playerManager.transform.position + playerManager.transform.forward * 5.5f - playerManager.transform.right * Mathf.Tan(50 * Mathf.Deg2Rad) * 5.5f;

        for (int i = 0; i < 3; i++)
        {
            bool bubbleSpawned = false;
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                if (obj.TryGetComponent(out PsyduckRaceBubbleObject bubbleObject))
                {
                    bubbleObject.InitializeRPC(playerManager.NetworkObjectId, startPos, bubbleEndPositions[i]);
                    bubbleSpawned = true;
                }
            };
            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(wallPrefabPath, playerManager.OwnerClientId);

            yield return new WaitUntil(() => bubbleSpawned);
        }
    }

    public override void ResetMove()
    {
    }
}
