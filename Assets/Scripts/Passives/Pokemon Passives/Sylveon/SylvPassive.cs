using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SylvPassive : PassiveBase
{
    // TODO: Implement Sylveon passive
    // Eevee: Every time Eevee deals or receives damage, increase Sp. Attack by 5% for 1.5s, stacking up to 4 times.
    // Sylveon: Every time Sylveon deals or receives damage, increase Sp. Atk and Sp. Defense by 2.5% for 1.5s, stacking up to 6 times.

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);

    }

    public override void Update()
    {
        
    }
}
