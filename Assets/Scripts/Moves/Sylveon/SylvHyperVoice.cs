using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SylvHyperVoice : MoveBase
{
    private DamageInfo closeDamage;
    private DamageInfo farDamage;
    private Vector3 direction;

    private string assetPath = "Moves/Sylveon/HypervoiceHitbox";

    private bool isScreaming = false;
    private float screamTimer = 2.5f;

    private HypervoiceHitbox hypervoiceHitbox;

    public SylvHyperVoice()
    {
        Name = "Hyper Voice";
        Cooldown = 7.0f;
        closeDamage = new DamageInfo(0, 0.32f, 6, 110, DamageType.Special);
        farDamage = new DamageInfo(0, 0.54f, 8, 184, DamageType.Special);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        closeDamage.attackerId = controller.Pokemon.NetworkObjectId;
        farDamage.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeHyperVoiceAim();
    }

    public override void Update()
    {
        if (isScreaming)
        {
            screamTimer -= Time.deltaTime;

            playerManager.HPBar.UpdateGenericGuageValue(screamTimer, 2.5f);

            hypervoiceHitbox.transform.position = playerManager.transform.position;
            playerManager.transform.rotation = Quaternion.LookRotation(direction);

            if (screamTimer <= 0f)
            {
                isScreaming = false;

                playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
                playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
                playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
                playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
                playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

                playerManager.HPBar.ShowGenericGuage(false);
                hypervoiceHitbox.DespawnRPC();
                hypervoiceHitbox = null;
            }
        }

        if (!IsActive)
        {
            return;
        }
        direction = Aim.Instance.SkillshotAim();
    }

    public override void Finish()
    {
        if (direction.magnitude != 0 && IsActive)
        {
            Vector2 direction = new Vector2(this.direction.x, this.direction.z);
            playerManager.MovesController.onObjectSpawned += (hyperVoiceHitbox) =>
            {
                hypervoiceHitbox = hyperVoiceHitbox.GetComponent<HypervoiceHitbox>();
                hypervoiceHitbox.TeamToIgnore = playerManager.OrangeTeam;
                hypervoiceHitbox.SetDamageRPC(closeDamage, farDamage);
                hypervoiceHitbox.transform.rotation = Quaternion.LookRotation(this.direction);
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

            playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
            playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

            playerManager.HPBar.ShowGenericGuage(true);

            screamTimer = 2.5f;

            isScreaming = true;
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideHyperVoiceAim();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideHyperVoiceAim();
    }
}
