using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceDashPlatform : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            PlayerManager playerManager = other.gameObject.GetComponent<PlayerManager>();

            if (playerManager == null || !playerManager.IsOwner)
            {
                return;
            }

            PsyduckRacePassive playerPassive = playerManager.PassiveController.Passive as PsyduckRacePassive;
            playerPassive?.Dash(0.5f);
        }
    }
}
