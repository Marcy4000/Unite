using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VaporHelpingHand : MoveBase
{
    private StatChange atkBuff = new StatChange(35, Stat.Attack, 3.5f, true, true, true, 0);
    private StatChange spAtkBuff = new StatChange(35, Stat.SpAttack, 3.5f, true, true, true, 0);

    private GameObject target;

    private float distance;
    private float angle;

    public VaporHelpingHand()
    {
        Name = "Helping Hand";
        Cooldown = 9.0f;
        distance = 8f;
        angle = 60f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
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
        if (target != null && IsActive)
        {
            Pokemon pokemon = target.GetComponent<Pokemon>();
            pokemon.AddStatChange(atkBuff);
            pokemon.AddStatChange(spAtkBuff);

            playerManager.transform.LookAt(target.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);

            string animation = playerManager.Pokemon.CurrentLevel >= 3 ? "Armature_pm0134_00_kw32_happyA01_gfbanm" : "ani_leafeonspell1_bat_0133";

            playerManager.AnimationManager.PlayAnimation(animation);
            playerManager.StopMovementForTime(0.4f);

            wasMoveSuccessful = true;
            Debug.Log("Helping Hand Buffs Added!");
        }
        Aim.Instance.HideAutoAim();
        Debug.Log("Finished Helping Hand!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }
}
