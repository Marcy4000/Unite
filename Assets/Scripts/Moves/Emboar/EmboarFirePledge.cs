using UnityEngine;
using Unity.Netcode;

public class EmboarFirePledge : MoveBase
{
    private EmboarBasicAtk basicAtk;

    private const float EFFECT_DURATION = 5f;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Emboar/EmboarFirePledgeIndicator.prefab";

    private StatChange speedBuff = new StatChange(25, Stat.Speed, EFFECT_DURATION, true, true, true, 0);
    private StatChange atkSpeedBuff = new StatChange(20, Stat.AtkSpeed, EFFECT_DURATION, true, true, true, 0);

    private bool isFirePledgeActive;
    private float firePledgeTimer;
    private int boostedCount;

    private bool subscribedToBasicAtk;

    private GameObject firePledgeIndicator;

    public EmboarFirePledge()
    {
        Name = "Fire Pledge";
        Cooldown = 8f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        basicAtk = playerManager.MovesController.BasicAttack as EmboarBasicAtk;

        if (!subscribedToBasicAtk)
        {
            playerManager.MovesController.onBasicAttackPerformed += OnBasicAttackPerformed;
            subscribedToBasicAtk = true;
        }
    }

    private void OnBasicAttackPerformed()
    {
        if (isFirePledgeActive && boostedCount < 2)
        {
            basicAtk.SetCharge(2);
            boostedCount++;

            if (boostedCount == 2)
            {
                basicAtk.nextBoostedAttackKnocksUp = true;
            }
        }
    }

    public override void Update()
    {
        if (firePledgeIndicator != null)
        {
            firePledgeIndicator.transform.position = playerManager.transform.position;
        }

        if (isFirePledgeActive)
        {
            firePledgeTimer -= Time.deltaTime;
            if (firePledgeTimer <= 0)
            {
                isFirePledgeActive = false;
                playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Busy);
                wasMoveSuccessful = true;
                playerManager.MovesController.DespawnNetworkObjectRPC(firePledgeIndicator.GetComponent<NetworkObject>().NetworkObjectId);
                Finish();
            }
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            if (!isFirePledgeActive)
            {
                firePledgeTimer = EFFECT_DURATION;
                isFirePledgeActive = true;
                basicAtk.SetCharge(2);
                boostedCount = 0;

                playerManager.Pokemon.AddStatChange(speedBuff);
                playerManager.Pokemon.AddStatChange(atkSpeedBuff);

                playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Busy);

                playerManager.MovesController.onObjectSpawned += (obj) =>
                {
                    firePledgeIndicator = obj;
                };
                playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);
            }
        }
        base.Finish();
    }

    public override void ResetMove()
    {
        isFirePledgeActive = false;
        if (basicAtk != null)
        {
            basicAtk.SetCharge(0);
            basicAtk.nextBoostedAttackKnocksUp = false;
        }
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Busy);

        if (firePledgeIndicator != null)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(firePledgeIndicator.GetComponent<NetworkObject>().NetworkObjectId);
        }
    }
}
