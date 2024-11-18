using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class FlareonTackle : MoveBase
{
    private Vector3 direction;
    private DamageInfo damageInfo = new DamageInfo(0, 0.8f, 3, 300, DamageType.Physical);

    private float range = 3f;
    private bool isDashing = false;

    private List<GameObject> hitTargets = new List<GameObject>();

    public FlareonTackle()
    {
        Name = "Tackle";
        Cooldown = 6.0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(range);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
    }

    public override void Update()
    {
        if (isDashing)
        {
            GameObject[] targets = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + new Vector3(0f, 0.6f, 0f), 0.75f, AimTarget.NonAlly);
            foreach (GameObject target in targets)
            {
                if (hitTargets.Contains(target))
                {
                    continue;
                }

                Pokemon pokemon = target.GetComponent<Pokemon>();
                if (pokemon != null)
                {
                    pokemon.TakeDamage(damageInfo);

                    hitTargets.Add(target);
                }
            }
        }

        if (!IsActive)
        {
            return;
        }
        direction = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.PlayerMovement.CanMove = false;
            isDashing = true;

            playerManager.transform.DOMove(playerManager.transform.position + direction * range, 0.2f).OnComplete(() =>
            {
                playerManager.PlayerMovement.CanMove = true;
                isDashing = false;
                hitTargets.Clear();
            });

            playerManager.AnimationManager.PlayAnimation($"ani_leafeonspell2_bat_0133");
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideDashAim();
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideDashAim();
    }

    public override void ResetMove()
    {
        direction = Vector3.zero;
        isDashing = false;
    }
}
