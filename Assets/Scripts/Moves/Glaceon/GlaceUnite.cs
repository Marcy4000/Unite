using UnityEngine;

public class GlaceUnite : MoveBase
{
    private Vector3 direction;
    private DamageInfo damageInfo;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Glaceon/GlaceonUnite.prefab";

    private GlaceUniteArea uniteArea;
    private GlaceonPassive glaceonPassive;

    private StatChange[] uniteBuffs = new StatChange[2]
    {
        new StatChange(30, Stat.Speed, 6, true, true, true, 0),
        new StatChange(35, Stat.AtkSpeed, 6, true, true, true, 0),
    };

    public GlaceUnite()
    {
        Name = "Glacial Stage";
        Cooldown = 0f;
        damageInfo = new DamageInfo(0, 1.0f, 7, 320, DamageType.Special);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        glaceonPassive = playerManager.PassiveController.Passive as GlaceonPassive;
        Aim.Instance.InitializeGlaceonUniteAim();
    }

    public override void Update()
    {
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
            //playerManager.MovesController.LaunchHomingProjectileRpc(target.GetComponent<NetworkObject>().NetworkObjectId, damageInfo);
            playerManager.MovesController.onObjectSpawned += (uniteArea) =>
            {
                this.uniteArea = uniteArea.GetComponent<GlaceUniteArea>();
                this.uniteArea.InitializeRPC(playerManager.transform.position, Quaternion.LookRotation(direction), damageInfo, playerManager.CurrentTeam.Team, playerManager.NetworkObjectId);
                this.uniteArea.onGiveGlaceonSpears += () =>
                {
                    if (glaceonPassive != null)
                    {
                        glaceonPassive.UpdateIciclesCount((byte)Mathf.Clamp(glaceonPassive.IciclesCount + 1, 0, 8));
                    }
                };
                Debug.Log("Unite area spawned!");
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);
            playerManager.AnimationManager.PlayAnimation("ani_spellu_bat_0471");
            wasMoveSuccessful = true;
            foreach (StatChange buff in uniteBuffs)
            {
                playerManager.Pokemon.AddStatChange(buff);
            }
        }
        Aim.Instance.HideGlaceonUniteAim();
        Debug.Log("Finished glaceon unite!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideGlaceonUniteAim();
    }

    public override void ResetMove()
    {
    }
}
