using UnityEngine;

public class CresseliaMoonlight : MoveBase
{
    private float maxDistance = 5f;

    private Vector3 spawnLocation;
    private string assetPath = "Assets/Prefabs/Objects/Moves/Cresselia/CresseliaMoonlightArea.prefab";

    private float healPercentage = 0.08f;

    public CresseliaMoonlight()
    {
        Name = "Moonlight";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeCircleAreaIndicator(maxDistance);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        spawnLocation = Aim.Instance.CircleAreaAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                if (obj.TryGetComponent(out MoonlightArea moonlightArea))
                {
                    moonlightArea.InitializeRPC(spawnLocation, playerManager.CurrentTeam.Team, healPercentage);
                }
            };
            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);
            playerManager.AnimationManager.PlayAnimation("pm0488_kw36_mad01");
            playerManager.StopMovementForTime(0.7f);
            playerManager.transform.LookAt(spawnLocation);
            playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideCircleAreaIndicator();
    }

    public override void ResetMove()
    {

    }
}
