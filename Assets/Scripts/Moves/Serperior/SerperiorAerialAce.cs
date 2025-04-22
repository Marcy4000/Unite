using DG.Tweening;
using UnityEngine;

public class SerperiorAerialAce : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo = new DamageInfo(0, 1.2f, 4, 290, DamageType.Physical, DamageProprieties.CanCrit);
    private StatChange speedBuff = new StatChange(10, Stat.Speed, 2f, true, true, true, 0);

    private float range = 3f;

    public SerperiorAerialAce()
    {
        Name = "Aerial Ace";
        Cooldown = 6.0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeAutoAim(range, 30f, AimTarget.NonAlly);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
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
        if (IsActive && target != null)
        {
            playerManager.PlayerMovement.CanMove = false;

            playerManager.transform.DOMove(target.transform.position, 0.23f).OnComplete(() =>
            {
                target.GetComponent<Pokemon>().TakeDamageRPC(damageInfo);
                playerManager.Pokemon.AddStatChange(speedBuff);
                playerManager.PlayerMovement.CanMove = true;
            });

            playerManager.AnimationManager.PlayAnimation($"Fight_release");
            playerManager.transform.rotation = Quaternion.LookRotation(target.transform.position - playerManager.transform.position);
            playerManager.transform.rotation = Quaternion.Euler(0f, playerManager.transform.rotation.eulerAngles.y, 0f);
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

    public override void ResetMove()
    {
        target = null;
    }
}
