using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MarshadowAuraSphere : MoveBase
{
    private float range = 5.5f;

    private Vector3 direction;

    private DamageInfo sphereDamage = new DamageInfo(0, 1.12f, 7, 110, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Marshadow/AuraSphere.prefab";

    private Pokemon target;
    private Coroutine jumpRoutine;
    private Coroutine cancelRoutine;
    TweenerCore<Vector3, Vector3, VectorOptions> sequence;

    public MarshadowAuraSphere()
    {
        Name = "Aura Sphere";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        sphereDamage.attackerId = playerManager.NetworkObjectId;
        if (target == null)
        {
            Aim.Instance.InitializeSkillshotAim(range);
        }
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        if (target == null)
        {
            direction = Aim.Instance.SkillshotAim();
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            if (target == null)
            {
                playerManager.StartCoroutine(SpawnSphere());
            }
            else
            {
                jumpRoutine = playerManager.StartCoroutine(JumpToTarget());
                playerManager.StopCoroutine(cancelRoutine);
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
                wasMoveSuccessful = true;
            }

        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }

    private IEnumerator SpawnSphere()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.PlayAnimation("pm0883_ba21_tokusyu01");
        playerManager.transform.rotation = Quaternion.LookRotation(this.direction);

        yield return new WaitForSeconds(0.416f);

        Vector2 direction = new Vector2(this.direction.x, this.direction.z);
        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            AuraSphereProjectile auraSphereProjectile = obj.GetComponent<AuraSphereProjectile>();
            auraSphereProjectile.SetDirection(playerManager.transform.position, direction, sphereDamage, range);
            auraSphereProjectile.OnMoveHit += OnProjectileHit;
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

        yield return new WaitForSeconds(1.164f);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.MovesController.UnlockEveryAction();

        playerManager.PlayerMovement.CanMove = true;
    }

    public override void Cancel()
    {
        Aim.Instance.HideSkillshotAim();
        base.Cancel();
    }

    private IEnumerator JumpToTarget()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.LookAt(target.transform);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

        sequence = playerManager.transform.DOMove(target.transform.position, 0.6f);

        yield return sequence.WaitForCompletion();

        playerManager.PlayerMovement.CanMove = true;
        if (Aim.Instance.CanPokemonBeTargeted(target.gameObject, AimTarget.NonAlly, playerManager.OrangeTeam))
        {
            target.TakeDamage(new DamageInfo(playerManager.NetworkObjectId, 0f, 0, (short)Mathf.RoundToInt(150 + (target.GetMissingHp()*0.15f)), DamageType.Physical));
        }
        target = null;

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    private void CancelJump(DamageInfo info)
    {
        playerManager.PlayerMovement.CanMove = true;
        sequence.Kill();
        playerManager.StopCoroutine(jumpRoutine);
        try
        {
            target.OnDeath -= CancelJump;
        }
        catch (System.Exception)
        {

        }
        target = null;
    }

    private void OnProjectileHit(ulong hitId)
    {
        try
        {
            target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[hitId].gameObject.GetComponent<Pokemon>();
            target.OnDeath += CancelJump;
            cancelRoutine = playerManager.StartCoroutine(SecondHitCooldown());
        }
        catch (System.Exception)
        {
            wasMoveSuccessful = true;
            target = null;
            Finish();
        }
    }

    private IEnumerator SecondHitCooldown()
    {
        BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 5f);
        yield return new WaitForSeconds(5f);
        wasMoveSuccessful = true;
        BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
        target = null;
        Finish();
    }

    public override void ResetMove()
    {
        if (jumpRoutine != null)
        {
            playerManager.StopCoroutine(jumpRoutine);
        }
        if (cancelRoutine != null)
        {
            playerManager.StopCoroutine(cancelRoutine);
        }
        if (sequence != null)
        {
            sequence.Kill();
        }
        target = null;
    }
}
