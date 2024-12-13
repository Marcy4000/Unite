using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GlobalVisionInitializer : NetworkBehaviour
{
    [SerializeField] private VisionController visionController;
    [SerializeField] private SphereCollider visionCollider;
    [SerializeField] private Team team;

    public override void OnNetworkSpawn()
    {
        visionCollider.enabled = false;
        visionController.enabled = false;
        GameManager.Instance.onGameStateChanged += OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            visionController.enabled = true;
            visionController.TeamToIgnore = team;
            visionController.IsEnabled = team == LobbyController.Instance.GetLocalPlayerTeam();
            visionController.transform.parent = null;
            visionCollider.enabled = true;
        }
    }
}
