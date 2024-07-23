using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MarshadowSpectralThief : MoveBase
{
    private float range = 4f;

    private Vector3 dashDirection;

    private DamageInfo damage = new DamageInfo(0, 1.2f, 4, 110, DamageType.Physical);
    private StatusEffect stun = new StatusEffect(StatusType.Incapacitated, 0.6f, true, 0);
    private string assetPath = "Assets/Prefabs/Objects/Moves/Marshadow/MarshSpectralThiefArea.prefab";

    private GameObject warningObject;

    public MarshadowSpectralThief()
    {
        Name = "Spectral Thief";
        Cooldown = 8f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(range);
        damage.attackerId = playerManager.NetworkObjectId;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        dashDirection = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            wasMoveSuccessful = true;
            playerManager.PlayerMovement.CanMove = false;
            playerManager.transform.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
            playerManager.AnimationManager.PlayAnimation("pm0883_kw36_mad01");
            playerManager.MovesController.LockEveryAction();
            playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
            playerManager.transform.DOMove(playerManager.transform.position + dashDirection * range, 0.4f).OnComplete(() =>
            {
                playerManager.AnimationManager.SetTrigger("Transition");
                playerManager.StartCoroutine(AttackRoutine());
            });

            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                warningObject = obj;

                Vector3 pos = playerManager.transform.position + dashDirection * range;
                pos.y = 1.5f;
                obj.transform.position = pos;
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);
            Aim.Instance.HideDashAim();
        }
        base.Finish();
    }

    private IEnumerator AttackRoutine()
    {
        GameObject[] enemiesHit = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position, 1.5f, AimTarget.NonAlly, playerManager.OrangeTeam);

        foreach (GameObject enemy in enemiesHit)
        {
            if (enemy.TryGetComponent(out Pokemon pokemon))
            {
                pokemon.TakeDamage(damage);
                pokemon.AddStatusEffect(stun);
                pokemon.ApplyKnockupRPC(2f, 0.6f);

                if (Vector3.Distance(playerManager.transform.position, enemy.transform.position) < 0.6f)
                {
                    List<StatChange> buffsToSteal = new List<StatChange>();

                    foreach (var change in pokemon.StatChanges)
                    {
                        if (change.IsBuff && change.IsTimed)
                        {
                            buffsToSteal.Add(change);
                        }
                    }

                    foreach (var buff in buffsToSteal)
                    {
                        pokemon.RemoveStatChangeRPC(buff);
                        playerManager.Pokemon.AddStatChange(buff);
                    }
                }
            }
        }

        yield return new WaitForSeconds(0.75f);
        playerManager.PlayerMovement.CanMove = true;

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

        playerManager.MovesController.DespawnNetworkObjectRPC(warningObject.GetComponent<NetworkObject>().NetworkObjectId);
        warningObject = null;
    }

    public override void Cancel()
    {
        Aim.Instance.HideDashAim();
        base.Cancel();
    }
}
