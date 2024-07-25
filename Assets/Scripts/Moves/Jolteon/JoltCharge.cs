using Unity.Netcode;
using UnityEngine;

public class JoltCharge : MoveBase
{
    private StatChange atkNormal = new StatChange(25, Stat.Attack, 5f, true, true, true, 0);
    private StatChange defNormal = new StatChange(30, Stat.Defense, 5f, true, true, true, 0);
    private StatChange spDefNormal = new StatChange(30, Stat.SpDefense, 5f, true, true, true, 0);
    private StatChange speedNormal = new StatChange(30, Stat.Speed, 2.5f, true, true, true, 0);

    private StatChange atkBoosted = new StatChange(35, Stat.Attack, 6f, true, true, true, 0);
    private StatChange defBoosted = new StatChange(40, Stat.Defense, 6f, true, true, true, 0);
    private StatChange spDefBoosted = new StatChange(40, Stat.SpDefense, 6f, true, true, true, 0);
    private StatChange speedBoosted = new StatChange(35, Stat.Speed, 5f, true, true, true, 0);

    private JoltPassive joltPassive;
    private GameObject indicator;

    private float timer;
    private bool startTimer;

    public JoltCharge()
    {
        Name = "Charge";
        Cooldown = 9.0f;
    }

    public override void Update()
    {
        if (!startTimer)
        {
            return;
        }

        if (indicator != null)
        {
            indicator.transform.position = playerManager.transform.position;
        }

        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }

        if (timer <= 0)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(indicator.GetComponent<NetworkObject>().NetworkObjectId);
            indicator = null;
            startTimer = false;
        }
    }

    public override void Finish()
    {
        joltPassive = playerManager.PassiveController.Passive as JoltPassive;

        if (IsActive)
        {
            bool boosted = false;

            if (joltPassive.IsPassiveReady)
            {
                playerManager.Pokemon.AddStatChange(atkBoosted);
                playerManager.Pokemon.AddStatChange(defBoosted);
                playerManager.Pokemon.AddStatChange(spDefBoosted);
                playerManager.Pokemon.AddStatChange(speedBoosted);
                joltPassive.ReducePassiveCharge(15f);
                boosted = true;
            }
            else
            {
                playerManager.Pokemon.AddStatChange(atkNormal);
                playerManager.Pokemon.AddStatChange(defNormal);
                playerManager.Pokemon.AddStatChange(spDefNormal);
                playerManager.Pokemon.AddStatChange(speedNormal);
            }
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                indicator = obj;
                startTimer = true;
                timer = boosted ? 6f : 5f;
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC("Assets/Prefabs/Objects/Moves/Jolteon/JolteonChargeIndicator.prefab", playerManager.OwnerClientId);

            playerManager.StopMovementForTime(1f);
            playerManager.AnimationManager.PlayAnimation("pm0135_00_ba21_tokusyu02");
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    public override void ResetMove()
    {
        if (indicator != null)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(indicator.GetComponent<NetworkObject>().NetworkObjectId);
            indicator = null;
        }
        startTimer = false;
    }
}
