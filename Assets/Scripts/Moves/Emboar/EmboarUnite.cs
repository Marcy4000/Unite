using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class EmboarUnite : MoveBase
{
    private EmboarBasicAtk basicAtk;
    private EmboarPassive passive;

    private const float EFFECT_DURATION = 10f;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Emboar/EmboarFirePledgeIndicator.prefab";

    private StatChange atkSpeedBuff = new StatChange(30, Stat.AtkSpeed, EFFECT_DURATION, true, true, true, 0);
    private StatChange cooldownReduction = new StatChange(30, Stat.Cdr, EFFECT_DURATION, true, true, false, 0);
    private StatChange atkBuff = new StatChange(20, Stat.Attack, EFFECT_DURATION, true, true, true, 0);

    private bool isFirePledgeActive;
    private float firePledgeTimer;

    private GameObject firePledgeIndicator;
    private Coroutine firePledgeCoroutine;

    public EmboarUnite()
    {
        Name = "Rentless Roast";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        basicAtk = playerManager.MovesController.BasicAttack as EmboarBasicAtk;
        passive = playerManager.PassiveController.Passive as EmboarPassive;
    }

    public override void Update()
    {
        if (firePledgeIndicator != null)
        {
            firePledgeIndicator.transform.position = playerManager.transform.position;
        }

        if (isFirePledgeActive)
        {
            passive?.SetRecklessCharge(100);

            firePledgeTimer -= Time.deltaTime;
            if (firePledgeTimer <= 0)
            {
                isFirePledgeActive = false;
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
                firePledgeCoroutine = playerManager.StartCoroutine(ApplyEffect());
            }
        }
        base.Finish();
    }

    private IEnumerator ApplyEffect()
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        playerManager.AnimationManager.PlayAnimation("Pet_refuse");

        yield return new WaitForSeconds(0.316f);

        firePledgeTimer = EFFECT_DURATION;
        isFirePledgeActive = true;
        basicAtk.SetCharge(2);

        playerManager.Pokemon.AddStatChange(cooldownReduction);
        playerManager.Pokemon.AddStatChange(atkSpeedBuff);
        playerManager.Pokemon.AddStatChange(atkBuff);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            firePledgeIndicator = obj;
        };
        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

        yield return new WaitForSeconds(0.4f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.AnimationManager.SetTrigger("Transition");
    }

    public override void ResetMove()
    {
        isFirePledgeActive = false;
        basicAtk.SetCharge(0);
        basicAtk.nextBoostedAttackKnocksUp = false;

        if (firePledgeIndicator != null)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(firePledgeIndicator.GetComponent<NetworkObject>().NetworkObjectId);
        }

        if (firePledgeCoroutine != null)
        {
            playerManager.StopCoroutine(firePledgeCoroutine);
            firePledgeCoroutine = null;
        }
    }
}
