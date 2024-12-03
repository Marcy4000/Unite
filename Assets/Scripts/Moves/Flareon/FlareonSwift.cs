using UnityEngine;

public class FlareonSwift : MoveBase
{
    private DamageInfo damageInfo = new DamageInfo(0, 1f, 4, 150, DamageType.Physical, DamageProprieties.CanCrit);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Flareon/FlareonSwiftArea.prefab";
    private FlareonSwiftArea swiftArea;

    public FlareonSwift()
    {
        Name = "Swift";
        Cooldown = 7.5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSimpleCircle(2.5f);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
    }

    public override void Update()
    {
        if (swiftArea != null)
        {
            swiftArea.transform.position = playerManager.transform.position + new Vector3(0, 0.75f, 0);
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                swiftArea = obj.GetComponent<FlareonSwiftArea>();
                swiftArea.InitializeRPC(damageInfo, playerManager.CurrentTeam.Team);
            };
            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSimpleCircle();
    }

    public override void ResetMove()
    {
        if (swiftArea != null)
        {
            swiftArea.DespawnRPC();
            swiftArea = null;
        }
    }
}
