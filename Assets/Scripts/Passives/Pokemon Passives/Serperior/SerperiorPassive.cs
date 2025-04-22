using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SerperiorPassive : PassiveBase
{
    private StatChange atkBuff = new StatChange(10, Stat.Attack, 5f, true, true, true, 0);
    private StatChange speedBuff = new StatChange(15, Stat.Speed, 5f, true, true, true, 0);

    private bool isOnCooldown = false;
    private float cooldownTimer = 0f;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        controller.Pokemon.OnDamageTaken += OnDamageTaken;
        controller.Pokemon.OnStatChange += OnStatChange;
    }

    public override void Update()
    {
        if (isOnCooldown)
        {
            cooldownTimer += Time.deltaTime;
            if (cooldownTimer >= 15f)
            {
                isOnCooldown = false;
                cooldownTimer = 0f;
            }
        }
    }

    private void OnDamageTaken(DamageInfo damageInfo)
    {
        if (isOnCooldown) return;

        if (playerManager.Pokemon.CurrentHp < (playerManager.Pokemon.CurrentHp * 0.45f))
        {
            playerManager.Pokemon.AddStatChange(atkBuff);
            playerManager.Pokemon.AddStatChange(speedBuff);

            isOnCooldown = true;
        }
    }

    private void OnStatChange(NetworkListEvent<StatChange> changeEvent)
    {
        if (isOnCooldown) return;

        if (changeEvent.Type == NetworkListEvent<StatChange>.EventType.Add || changeEvent.Type == NetworkListEvent<StatChange>.EventType.Insert)
        {
            if (!changeEvent.Value.IsBuff && changeEvent.Value.ID != 1)
            {
                playerManager.Pokemon.RemoveStatChangeRPC(changeEvent.Value);

                StatChange newStat = changeEvent.Value;
                newStat.IsBuff = true;

                playerManager.Pokemon.AddStatChange(newStat);

                isOnCooldown = true;
            }
        }
    }
}
