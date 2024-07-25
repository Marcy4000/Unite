using DG.Tweening;
using System.Collections;
using UnityEngine;

public class FlygonUnite : MoveBase
{
    private string assetPath = "Assets/Prefabs/Objects/Moves/Flygon/FlygonTornado.prefab";

    private float spinDuration = 1.7f;
    public float returnDuration = 0.3f;
    public float initialSpinSpeed = 4f;
    public float spinRadius = 2f;
    public float speedIncreaseRate = 2f;

    private DamageInfo tornadoDamage = new DamageInfo(0, 1.55f, 8, 200, DamageType.Physical);
    private DamageInfo tornadoLow = new DamageInfo(0, 1.3f, 6, 170, DamageType.Physical);
    private DamageInfo tornadoLower = new DamageInfo(0, 1.1f, 4, 145, DamageType.Physical);

    private TornadoHitbox tornadoHitbox;
    private bool isInTornado;

    private int powerPhase;

    private StatusEffect tornadoStatus = new StatusEffect(StatusType.Invincible, 0, false, 6);

    private StatChange flygonSpeed = new StatChange(25, Stat.Speed, 4f, true, true, true, 0);

    private StatChange initialSpeed = new StatChange(15, Stat.Speed, 0f, false, true, true, 12);
    private StatChange halfSpeed = new StatChange(20, Stat.Speed, 0f, false, false, true, 13);
    private StatChange quarterSpeed = new StatChange(40, Stat.Speed, 0f, false, false, true, 14);

    private float tornadoTimer;

    public FlygonUnite()
    {
        Name = "Cyclonic Sand Surge";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        tornadoDamage.attackerId = playerManager.NetworkObjectId; 
        tornadoLow.attackerId = playerManager.NetworkObjectId;
        tornadoLower.attackerId = playerManager.NetworkObjectId;
        if (!isInTornado)
        {
            Aim.Instance.InitializeSimpleCircle(4f);
        }
    }

    public override void Update()
    {
        if (isInTornado)
        {
            if (tornadoHitbox != null)
            {
                tornadoHitbox.transform.position = playerManager.transform.position;
            }

            tornadoTimer -= Time.deltaTime;

            playerManager.HPBar.UpdateGenericGuageValue(tornadoTimer, 6f);

            if (tornadoTimer > 3f)
            {
                UpdatePowerPhase(0);
            }
            else if (tornadoTimer < 3f && tornadoTimer > 2f)
            {
                UpdatePowerPhase(1);
            }
            else if (tornadoTimer < 2f && tornadoTimer > 1f)
            {
                UpdatePowerPhase(2);
            }
            else if (tornadoTimer <= 1f)
            {
                UpdatePowerPhase(3);
            }

            if (tornadoTimer <= 0)
            {
                tornadoHitbox.DespawnRPC();
                tornadoHitbox = null;
                isInTornado = false;
                playerManager.AnimationManager.PlayAnimation("Armature_pm0330_00_ba10_waitA01");
                playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
                playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
                playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
                playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

                playerManager.Pokemon.AddStatChange(flygonSpeed);
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(initialSpeed.ID);
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(halfSpeed.ID);
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(quarterSpeed.ID);

                playerManager.Pokemon.RemoveStatusEffectWithID(tornadoStatus.ID);

                playerManager.HPBar.ShowGenericGuage(false);

                IsActive = false;
                wasMoveSuccessful = true;
                Finish();
            }
        }
    }

    private void UpdatePowerPhase(int expectedPowerPhase)
    {
        if (powerPhase == expectedPowerPhase)
        {
            return;
        }

        powerPhase = expectedPowerPhase;

        switch (powerPhase)
        {
            case 0:
                playerManager.Pokemon.AddStatChange(initialSpeed);
                break;
            case 1:
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(initialSpeed.ID);
                break;
            case 2:
                playerManager.Pokemon.AddStatChange(halfSpeed);
                tornadoHitbox.UpdateDamageRPC(tornadoLow);
                break;
            case 3:
                tornadoHitbox.UpdateDamageRPC(tornadoLower);
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(halfSpeed.ID);
                playerManager.Pokemon.AddStatChange(quarterSpeed);
                break;
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            if (!isInTornado)
            {
                playerManager.StartCoroutine(SpawnTornado());
                powerPhase = -1;
                wasMoveSuccessful = false;
            }
            else
            {
                tornadoTimer = 0f;
            }

        }
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    private IEnumerator SpawnTornado()
    {
        playerManager.Pokemon.AddStatusEffect(tornadoStatus);

        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.SetBool("Walking", true);

        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        Vector3 centerPoint = playerManager.transform.position; // The center point is the player's position when the coroutine is called
        float elapsedTime = 0f;
        float currentAngle = 0f; // Start with an initial angle

        while (elapsedTime < spinDuration)
        {
            elapsedTime += Time.deltaTime;

            // Exponentially increase the angle
            currentAngle += initialSpinSpeed * Mathf.Pow(speedIncreaseRate, elapsedTime) * Time.deltaTime;

            // Calculate the new position around the center point
            float x = centerPoint.x + Mathf.Cos(currentAngle) * spinRadius;
            float z = centerPoint.z + Mathf.Sin(currentAngle) * spinRadius;
            Vector3 newPosition = new Vector3(x, playerManager.transform.position.y, z);

            // Make the player look towards the new position
            playerManager.transform.LookAt(newPosition);

            // Update the player's position
            playerManager.transform.position = newPosition;


            yield return null;
        }

        playerManager.transform.LookAt(centerPoint);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

        // Move back to the center point smoothly
        playerManager.transform.DOMove(centerPoint, returnDuration);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out TornadoHitbox tornado))
            {
                tornado.InitializeRPC(tornadoDamage, playerManager.OrangeTeam);
                tornadoHitbox = tornado;
                tornadoHitbox.transform.position = centerPoint;
                tornadoTimer = 6f;
            }
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

        yield return new WaitForSeconds(returnDuration);

        // Restore the original position if needed
        // (Optional) You can remove this part if you want the player to stay at the center point
        playerManager.transform.position = centerPoint;
        playerManager.PlayerMovement.CanMove = true;

        playerManager.AnimationManager.PlayAnimation("Tornado");

        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);

        playerManager.HPBar.ShowGenericGuage(true);

        isInTornado = true;
    }

    public override void Cancel()
    {
        Aim.Instance.HideSimpleCircle();
        base.Cancel();
    }

    public override void ResetMove()
    {
        if (tornadoHitbox != null)
        {
            tornadoHitbox.DespawnRPC();
            tornadoHitbox = null;
        }
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.Pokemon.RemoveStatusEffectWithID(tornadoStatus.ID);
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(initialSpeed.ID);
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(halfSpeed.ID);
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(quarterSpeed.ID);
        playerManager.HPBar.ShowGenericGuage(false);
        isInTornado = false;
    }
}
