using UnityEngine;

public class MeowsticMPsychic : MoveBase
{
    private float range;
    private float angle;

    private GameObject target;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Meowstic/Male/MeowsticMPsychic.prefab";

    public MeowsticMPsychic()
    {
        Name = "Psychic";
        Cooldown = 7f;
        range = 7f;
        angle = 60f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeAutoAim(range, angle, AimTarget.Ally);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        target = Aim.Instance.SureHitAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.StopMovementForTime(0.6f);
            playerManager.AnimationManager.PlayAnimation("Armature_pm0734_00_ba20_buturi01");
            if (target != null)
            {
                playerManager.transform.LookAt(target.transform.position);
                playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);
                ApplyShield(target);
            }
            else
            {
                ApplyShield(playerManager.gameObject);
            }
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideAutoAim();
        base.Cancel();
    }

    private void ApplyShield(GameObject target)
    {
        if (target.TryGetComponent(out PlayerManager player))
        {
            player.Pokemon.AddStatChange(new StatChange(35, Stat.DamageReduction, 4f, true, true, false, 0));
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                MeowsticMPsychicArea shield = obj.GetComponent<MeowsticMPsychicArea>();
                shield.InitializeRPC(player.NetworkObjectId, playerManager.NetworkObjectId);
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);
        }
    }

    public override void ResetMove()
    {
    }
}
