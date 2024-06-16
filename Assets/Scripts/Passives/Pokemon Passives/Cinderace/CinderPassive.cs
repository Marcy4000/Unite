using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CinderPassive : PassiveBase
{
    private StatChange critBuff = new StatChange(10, Stat.CritRate, 5, true, true, true, 2);
    private StatChange atkSpeedBuff = new StatChange(20, Stat.AtkSpeed, 5, true, true, true, 2);

    private float cooldown;

    override public void Start(PlayerManager playerManager)
    {
        base.Start(playerManager);
        IsActive = false;

        playerManager.Pokemon.OnHpOrShieldChange += CinderPassiveEffect;
    }

    override public void Update()
    {
        if (IsActive && cooldown > 0)
        {
            cooldown -= Time.deltaTime;
        }

        if (cooldown <= 0)
        {
            IsActive = false;
            cooldown = 1;
            Debug.Log("Cinder Passive Deactivated");
        }
    }

    private void CinderPassiveEffect()
    {
        if (playerManager.Pokemon.CurrentHp < playerManager.Pokemon.GetMaxHp()/2f && !IsActive)
        {
            IsActive = true;
            playerManager.Pokemon.AddStatChange(critBuff);
            playerManager.Pokemon.AddStatChange(atkSpeedBuff);
            cooldown = 30f;
            Debug.Log("Cinder Passive Activated");
        }
    }
}
