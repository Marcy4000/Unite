using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CresseliaSafeguard : MoveBase
{
    private GameObject target;

    private float distance;
    private float angle;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Cresselia/SafeguardIndicator.prefab";

    private Coroutine moveCoroutine;

    public CresseliaSafeguard()
    {
        Name = "Safeguard";
        Cooldown = 8.5f;
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
        if (IsActive)
        {
            playerManager.AnimationManager.PlayAnimation("pm0488_kw32_happyB01");
            playerManager.StopMovementForTime(1f);
            moveCoroutine = playerManager.StartCoroutine(MoveLock());

            foreach (StatusEffect effect in playerManager.Pokemon.StatusEffects)
            {
                if (effect.IsNegativeStatus() && effect.IsTimed)
                {
                    playerManager.Pokemon.RemoveStatusEffectRPC(effect);
                }
            }

            foreach (StatChange statChange in playerManager.Pokemon.StatChanges)
            {
                if (!statChange.IsBuff && statChange.IsTimed)
                {
                    playerManager.Pokemon.RemoveStatChangeRPC(statChange);
                }
            }

            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                SafeguardIndicator indicator = obj.GetComponent<SafeguardIndicator>();
                indicator.InitializeRPC(playerManager.NetworkObjectId);
                if (target != null)
                {
                    playerManager.StartCoroutine(SpawnSecondIndicator());
                }
            };
            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

            if (target != null)
            {
                playerManager.transform.LookAt(target.transform);
                playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);

                Pokemon pokemon = target.GetComponent<Pokemon>();
                foreach (StatusEffect effect in pokemon.StatusEffects)
                {
                    if (effect.IsNegativeStatus() && effect.IsTimed)
                    {
                        pokemon.RemoveStatusEffectRPC(effect);
                    }
                }

                foreach (StatChange statChange in pokemon.StatChanges)
                {
                    if (!statChange.IsBuff && statChange.IsTimed)
                    {
                        pokemon.RemoveStatChangeRPC(statChange);
                    }
                }
            }

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        base.Finish();
    }

    private IEnumerator SpawnSecondIndicator()
    {
        yield return null;

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            SafeguardIndicator indicator = obj.GetComponent<SafeguardIndicator>();
            indicator.InitializeRPC(target.GetComponent<NetworkObject>().NetworkObjectId);
        };
        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);
    }

    private IEnumerator MoveLock()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(1f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }

    public override void ResetMove()
    {
        target = null;
        if (moveCoroutine != null)
        {
            playerManager.StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }
}
