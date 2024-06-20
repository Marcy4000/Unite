using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FullHeal : BattleItemBase
{
    public FullHeal()
    {
        Name = "Full Heal";
        Cooldown = 40;
    }

    public override void Update()
    {
        
    }

    public override void Finish()
    {
        if (IsActive)
        {
            wasUseSuccessful = true;
        }
        base.Finish();
    }
}
