using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class SylvUnite : MoveBase
{
    private DamageInfo damageInfo = new DamageInfo(0, 1.3f, 13, 750, DamageType.Special);
    private float distance = 5f;

    private StatusEffect invulnerable = new StatusEffect(StatusType.Invincible, 1.333f, true, 0);
    private StatusEffect fairyFrolic = new StatusEffect(StatusType.Scriptable, 10.3f, true, 10);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Sylveon/SylveonUnite.prefab";
    private SylveonUniteArea uniteArea;

    private bool SubscribedToEvents = false;

    private Coroutine moveCoroutine;

    public SylvUnite()
    {
        Name = "Fairy Frolic";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.NetworkObjectId;
        Aim.Instance.InitializeSimpleCircle(distance);
        if (!SubscribedToEvents)
        {
            SubscribedToEvents = true;
            playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
        }
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damage)
    {
        if (!playerManager.Pokemon.HasStatusEffect(fairyFrolic.ID))
        {
            return;
        }

        Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
        int amount = target.CalculateDamage(damage, playerManager.Pokemon);

        playerManager.Pokemon.HealDamageRPC(amount / 2);
    }

    public override void Update()
    {
        if (uniteArea != null)
        {
            uniteArea.transform.position = playerManager.transform.position;
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            moveCoroutine = playerManager.StartCoroutine(JumpInAir());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSimpleCircle();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    private IEnumerator JumpInAir()
    {
        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            uniteArea = obj.GetComponent<SylveonUniteArea>();
            uniteArea.InitializeRPC(damageInfo, playerManager.CurrentTeam.Team);
        };
        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

        playerManager.AnimationManager.PlayAnimation("ani_spellu_bat_0700");
        playerManager.Pokemon.AddStatusEffect(invulnerable);
        foreach (var status in playerManager.Pokemon.StatusEffects)
        {
            if (status.IsNegativeStatus() && status.IsTimed)
            {
                playerManager.Pokemon.RemoveStatusEffectRPC(status);
            }
        }

        yield return new WaitForSeconds(1.033f);

        playerManager.Pokemon.AddStatusEffect(fairyFrolic);

        yield return new WaitForSeconds(0.3f);

        uniteArea.DoDamageRPC();
        uniteArea = null;

        moveCoroutine = null;
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSimpleCircle();
    }

    public override void ResetMove()
    {
        if (moveCoroutine != null)
        {
            playerManager.StopCoroutine(moveCoroutine);
        }
    }
}
