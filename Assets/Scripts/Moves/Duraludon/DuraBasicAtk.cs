using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DuraBasicAtk : BasicAttackBase
{
    private string attackPrefab = "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab";
    private string boostedAttackPrefab = "Assets/Prefabs/Objects/BasicAtk/DuraludonBoostedAtk.prefab";
    private byte charge = 0;

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;
    private DamageInfo cannonDmg;

    private DuraBoostedObj boostedObjInstance;

    private bool isInCannonMode = false;
    private float cannonModeStartTime;
    private StatChange attackSpeedBuff = new StatChange(100, Stat.AtkSpeed, 5f, true, true, true, 0);
    private float cannonTimer = 2.5f;
    private Coroutine cannonModeCoroutine;

    private float baseRange = 7f;
    private float cannonRange = 11.5f;

    public bool IsInCannonMode => isInCannonMode;
    public event System.Action onExitCannonMode;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 7f;
        DamageProprieties proprieties = DamageProprieties.IsBasicAttack;
        proprieties |= DamageProprieties.CanCrit;
        normalDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1, 0, 0, DamageType.Physical, proprieties);
        boostedDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1.35f, 0, 0, DamageType.Physical, proprieties);
        cannonDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1.05f, 3, 120, DamageType.Physical, DamageProprieties.CanCrit);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out DuraBoostedObj projectile))
            {
                projectile.InitializeRPC(boostedDmg, playerManager.NetworkObjectId);
                boostedObjInstance = projectile;
            }
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(boostedAttackPrefab, playerManager.OwnerClientId);
        playerManager.Pokemon.OnDamageDealt += OnDamageDealt;
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damage)
    {
        if ((damage.proprieties & DamageProprieties.IsBasicAttack) != 0 && (damage.proprieties & DamageProprieties.IsMuscleBand) == 0)
        {
            if (isInCannonMode)
            {
                Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();
                target.TakeDamageRPC(cannonDmg);
            }
        }
    }

    public override void Perform(bool wildPriority)
    {
        range = isInCannonMode ? cannonRange : baseRange;
        PokemonType priority = wildPriority ? PokemonType.Wild : PokemonType.Player;
        GameObject closestEnemy = Aim.Instance.AimInCircle(range, priority);

        if (closestEnemy != null)
        {
            if (isInCannonMode)
            {
                cannonTimer = 2.5f;
            }
            string animationName = charge == 2 ? "ani_atk2_bat_0884" : "ani_atk1_bat_0884";

            if (isInCannonMode)
            {
                animationName = charge == 2 ? "ani_spell1a2atk_bat_0884" : "ani_spell1a2_bat_0884";
            }

            playerManager.AnimationManager.PlayAnimation(animationName);
            if (!isInCannonMode)
                playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), true);
            
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);

            DamageInfo currentDmg = charge == 2 ? boostedDmg : normalDmg;

            if (charge == 2)
            {
                boostedObjInstance.CastRPC(isInCannonMode);
            }
            else
            {
                playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, currentDmg, attackPrefab);
            }
            charge++;
        }

        if (charge > 2)
        {
            charge = 0;
        }
    }

    public void GainBoostedAttack()
    {
        charge = 2;
    }

    public override void Update()
    {
    }

    public void EnterCannonMode()
    {
        isInCannonMode = true;
        cannonModeStartTime = Time.time;
        cannonTimer = 2.5f;
        playerManager.Pokemon.AddStatChange(attackSpeedBuff);
        playerManager.PlayerMovement.AddMovementRestriction();
        cannonModeCoroutine = playerManager.StartCoroutine(CannonModeRoutine());
        playerManager.AnimationManager.PlayAnimation("ani_spell1a1_bat_0884");

        playerManager.HPBar.ShowGenericGuage(true);

        CameraController.Instance.ZoomCamera(new Vector3(0f, 27.7f, -28.9f));
    }

    private IEnumerator CannonModeRoutine()
    {
        float totalTime = 0f;
        while (totalTime < 6f && cannonTimer > 0f)
        {
            cannonTimer -= Time.deltaTime;
            totalTime += Time.deltaTime;
            playerManager.HPBar.UpdateGenericGuageValue(cannonTimer, 2.5f);
            yield return null;
        }
        ExitCannonMode();
    }

    public void ExitCannonMode()
    {
        isInCannonMode = false;
        if (Time.time < cannonModeStartTime + 5f)
        {
            playerManager.Pokemon.RemoveStatChangeRPC(attackSpeedBuff);
        }
        playerManager.PlayerMovement.RemoveMovementRestriction();   
        if (cannonModeCoroutine != null)
        {
            playerManager.StopCoroutine(cannonModeCoroutine);
            cannonModeCoroutine = null;
        }
        playerManager.AnimationManager.SetTrigger("EndCannon");
        playerManager.HPBar.ShowGenericGuage(false);
        CameraController.Instance.ResetZoom();
        onExitCannonMode?.Invoke();
    }
}
