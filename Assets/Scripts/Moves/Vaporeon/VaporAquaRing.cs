using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class VaporAquaRing : MoveBase
{
    private StatChange atkBuff = new StatChange(45, Stat.Attack, 4.5f, true, true, true, 0);
    private StatChange spAtkBuff = new StatChange(45, Stat.SpAttack, 4.5f, true, true, true, 0);

    private DamageInfo healAmount = new DamageInfo(0, 0.51f, 6, 300, DamageType.Special);

    private string assetPath = "Moves/Vaporeon/VaporeonAquaRing";

    private GameObject target;

    private float distance;
    private float angle;

    public VaporAquaRing()
    {
        Name = "Aqua Ring";
        Cooldown = 12.0f;
        distance = 8f;
        angle = 60f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        healAmount.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeAutoAim(distance, angle, AimTarget.Ally);
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
            if (target != null)
            {
                Pokemon pokemon = target.GetComponent<Pokemon>();
                pokemon.AddStatChange(atkBuff);
                pokemon.AddStatChange(spAtkBuff);

                playerManager.transform.LookAt(target.transform);
                playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);

                playerManager.MovesController.onObjectSpawned += (obj) =>
                {
                    AquaRingObject ring = obj.GetComponent<AquaRingObject>();
                    ring.InitializeRPC(target.GetComponent<NetworkObject>().NetworkObjectId, healAmount);
                };
            }
            else
            {
                playerManager.Pokemon.AddStatChange(atkBuff);
                playerManager.Pokemon.AddStatChange(spAtkBuff);

                playerManager.MovesController.onObjectSpawned += (obj) =>
                {
                    AquaRingObject ring = obj.GetComponent<AquaRingObject>();
                    ring.InitializeRPC(playerManager.NetworkObjectId, healAmount);
                };
            }



            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

            playerManager.AnimationManager.PlayAnimation("Armature_pm0134_00_kw32_happyB01_gfbanm");
            playerManager.StopMovementForTime(0.4f);

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }
}
