using UnityEngine;
using DG.Tweening;

public class Launchpad : MonoBehaviour
{
    public Transform endPoint; // The endpoint the player will jump to
    public float jumpDuration = 1f; // Duration of the jump
    private PlayerManager playerManager; // Reference to the PlayerManager script

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

            playerManager = temp;
            if (playerManager != null)
            {
                StartLaunchSequence();
            }
        }
    }

    void StartLaunchSequence()
    {
        // Stop player movement
        playerManager.PlayerMovement.CanMove = false;

        // Make the player jump to the endpoint
        Vector3 startPosition = playerManager.transform.position;
        playerManager.transform.DOJump(endPoint.position, 5f, 1, jumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => playerManager.PlayerMovement.CanMove = true);
    }
}
