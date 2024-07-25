using System.Collections;
using UnityEngine;

public class SylvSwift : MoveBase
{
    private DamageInfo damageInfo;
    private float distance;
    private Vector3 direction;

    private bool launching = false;

    private Coroutine swiftRoutine;

    public SylvSwift()
    {
        Name = "Swift";
        Cooldown = 7.0f;
        distance = 5f;
        damageInfo = new DamageInfo(0, 0.32f, 6, 190, DamageType.Special);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeSkillshotAim(distance);
    }

    public override void Update()
    {
        if (launching)
        {
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
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
            launching = true;
            swiftRoutine = playerManager.StartCoroutine(LaunchSwift());

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    private IEnumerator LaunchSwift()
    {
        Vector2 direction = new Vector2(this.direction.x, this.direction.z);

        playerManager.AnimationManager.PlayAnimation($"ani_spell1a_bat_0133");
        playerManager.transform.rotation = Quaternion.LookRotation(this.direction);

        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(0.25f);

            playerManager.MovesController.onObjectSpawned += (sylvSwiftProjectile) =>
            {
                sylvSwiftProjectile.GetComponent<SylvSwiftProjectile>().SetDirection(playerManager.transform.position, direction, damageInfo, distance);
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC("Assets/Prefabs/Objects/Moves/Sylveon/SylvSwift.prefab", playerManager.OwnerClientId);
        }

        launching = false;
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSkillshotAim();
    }

    public override void ResetMove()
    {
        if (swiftRoutine != null)
        {
            playerManager.StopCoroutine(swiftRoutine);
        }
        launching = false;
    }
}
