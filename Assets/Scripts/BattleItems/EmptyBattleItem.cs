using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EmptyBattleItem : BattleItemBase
{
    override public void Start(PlayerManager controller)
    {
        Debug.Log("Empty battle item");

        base.Start(controller);
    }

    public override void Update()
    {
        
    }
}
