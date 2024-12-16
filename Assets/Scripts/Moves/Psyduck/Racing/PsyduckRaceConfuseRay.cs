public class PsyduckRaceConfuseRay : MoveBase
{
    private string wallPrefabPath = "Assets/Prefabs/Objects/Moves/Psyduck/PsyduckRaceConfuseRay.prefab";

    public PsyduckRaceConfuseRay()
    {
        Name = "Confuse Ray";
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
            RaceLapCounter lapCounter = RaceManager.Instance.GetNextPlayer(playerManager.NetworkObjectId);

            if (lapCounter != null)
            {
                UnityEngine.Debug.Log("Target found");

                playerManager.MovesController.onObjectSpawned += (obj) =>
                {
                    if (obj.TryGetComponent(out PsyduckRaceHoamingProjectile hoamingProjectile))
                    {
                        hoamingProjectile.SetTargetRPC(lapCounter.AssignedPlayerID, playerManager.NetworkObjectId, playerManager.transform.position);
                    }
                };
                playerManager.MovesController.SpawnNetworkObjectFromStringRPC(wallPrefabPath);
            }
        }
        base.Finish();

        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }

    public override void ResetMove()
    {
    }
}

public class PsyduckRaceFreezeRay : MoveBase
{
    private string wallPrefabPath = "Assets/Prefabs/Objects/Moves/Psyduck/PsyduckRaceFreezeRay.prefab";

    public PsyduckRaceFreezeRay()
    {
        Name = "Freeze Ray";
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
            RaceLapCounter lapCounter = RaceManager.Instance.GetNextPlayer(playerManager.NetworkObjectId);

            if (lapCounter != null)
            {
                UnityEngine.Debug.Log("Target found");

                playerManager.MovesController.onObjectSpawned += (obj) =>
                {
                    if (obj.TryGetComponent(out PsyduckRaceHoamingProjectile hoamingProjectile))
                    {
                        hoamingProjectile.SetTargetRPC(lapCounter.AssignedPlayerID, playerManager.NetworkObjectId, playerManager.transform.position);
                    }
                };
                playerManager.MovesController.SpawnNetworkObjectFromStringRPC(wallPrefabPath);
            }
        }
        base.Finish();

        playerManager.MovesController.LearnMove(RaceManager.Instance.EmptyMove);
    }

    public override void ResetMove()
    {
    }
}