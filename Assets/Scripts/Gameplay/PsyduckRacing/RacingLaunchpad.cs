using UnityEngine;
using DG.Tweening;

public class Launchpad : MonoBehaviour
{
    public Transform endPoint; // The endpoint the player will jump to
    public float jumpDuration = 1f; // Duration of the jump
    private readonly System.Collections.Generic.HashSet<ulong> launching = new();

    void OnTriggerEnter(Collider other)
    {
        // Check if the player is on the launchpad
        if (other.CompareTag("Player"))
        {
            PlayerManager temp = other.GetComponent<PlayerManager>();

            if (!temp.IsOwner)
            {
                return;
            }

            if (temp != null && launching.Add(temp.NetworkObjectId))
            {
                StartLaunchSequence(temp);
            }
        }
    }

    void StartLaunchSequence(PlayerManager playerManager)
    {
        // Stop player movement
        playerManager.PlayerMovement.CanMove = false;
        if (playerManager.PassiveController.Passive is PsyduckRacePassive racer) { racer.CanMove = false; racer.CurrentVelocity = Vector3.zero; }

        // Make the player jump to the endpoint
        Vector3 startPosition = playerManager.transform.position;
        playerManager.transform.DOJump(endPoint.position, 5f, 1, jumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (playerManager != null && playerManager.IsSpawned)
                {
                    bool finished = RaceManager.Instance != null && RaceManager.Instance.PlayerLapCounters.TryGetValue(playerManager.NetworkObjectId, out var counter) && counter.RaceFinished;
                    if (!finished)
                    {
                        playerManager.PlayerMovement.CanMove = true;
                        if (playerManager.PassiveController.Passive is PsyduckRacePassive passive) passive.CanMove = true;
                    }
                    launching.Remove(playerManager.NetworkObjectId);
                }
            }).OnKill(() => { if (playerManager != null) launching.Remove(playerManager.NetworkObjectId); });
    }
}
