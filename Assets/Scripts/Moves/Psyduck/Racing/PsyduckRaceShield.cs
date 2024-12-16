using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PsyduckRaceShield : MoveBase
{
    private string wallPrefabPath = "Assets/Prefabs/Objects/Moves/Psyduck/PsyduckRaceShield.prefab";

    public PsyduckRaceShield()
    {
        Name = "Shield";
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
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                if (obj.TryGetComponent(out PsyduckRacingShieldObject barrierWall))
                {
                    barrierWall.Initialize(playerManager.NetworkObjectId);
                }
            };
            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(wallPrefabPath, playerManager.OwnerClientId);
        }
        base.Finish();

        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }

    public override void ResetMove()
    {
    }
}
