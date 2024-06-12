using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MovesController : NetworkBehaviour
{
    [SerializeField] private MoveAsset lockedMove;
    public Transform projectileSpawnPoint;
    public GameObject homingProjectilePrefab;
    [SerializeField] private int uniteMoveCharge = 0;
    private int uniteMoveMaxCharge = 10000;

    private MoveBase[] moves = new MoveBase[2];
    private MoveStatus[] moveStatuses = new MoveStatus[2];

    private int prevLevel = -1;

    private MoveBase uniteMove;
    private BasicAttackBase basicAttack;

    private Pokemon pokemon;
    private PlayerControls controls;
    private PlayerManager playerManager;

    public PlayerControls Controls => controls;
    public Pokemon Pokemon => pokemon;
    public PlayerMovement PlayerMovement => playerManager.PlayerMovement;
    public int UniteMoveCharge => uniteMoveCharge;

    public BasicAttackBase BasicAttack => basicAttack;

    private float basicAttackCooldown = 0;

    public event Action onBasicAttackPerformed;
    public event Action<MoveBase> onMovePerformed;

    public event Action<GameObject> onObjectSpawned;

    private AttackSpeedCooldown[] cooldownTable = new AttackSpeedCooldown[]
{
        new AttackSpeedCooldown { threshold = 8.11f, cooldown = 0.93333f },
        new AttackSpeedCooldown { threshold = 16.42f, cooldown = 0.86667f },
        new AttackSpeedCooldown { threshold = 26.11f, cooldown = 0.8f },
        new AttackSpeedCooldown { threshold = 37.56f, cooldown = 0.73333f },
        new AttackSpeedCooldown { threshold = 51.29f, cooldown = 0.66667f },
        new AttackSpeedCooldown { threshold = 68.8f, cooldown = 0.6f },
        new AttackSpeedCooldown { threshold = 89.04f, cooldown = 0.53333f },
        new AttackSpeedCooldown { threshold = 115.99f, cooldown = 0.46667f },
        new AttackSpeedCooldown { threshold = 151.91f, cooldown = 0.4f },
        new AttackSpeedCooldown { threshold = 202.5f, cooldown = 0.33333f },
        new AttackSpeedCooldown { threshold = 285f, cooldown = 0.26667f },
};

    void Start()
    {
        controls = new PlayerControls();
        controls.asset.Enable();

        pokemon = GetComponent<Pokemon>();
        playerManager = GetComponent<PlayerManager>();

        if (!IsOwner)
        {
            return;
        }

        pokemon.OnLevelChange += CheckIfCanLearnMove;
        MoveLearnPanel.onSelectedMove += LearnMove;

        BattleUIManager.instance.ReferenceController(this);

        for (int i = 0; i < moves.Length; i++)
        {
            LearnMove(lockedMove);
            moveStatuses[i] = new MoveStatus(0);
        }

        CheckIfCanLearnMove();
        LearnBasicAttack(BasicAttacksDatabase.GetBasitAttack(pokemon.BaseStats.PokemonName));
        StartCoroutine(PassiveUniteCharge());
    }

    IEnumerator PassiveUniteCharge()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            IncrementUniteCharge(900);
        }
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        if (playerManager.PlayerState != PlayerState.Alive)
        {
            return;
        }   

        Aim.Instance.ShowBasicAtk(controls.Movement.BasicAttack.IsPressed(), basicAttack.range);

        if (controls.Movement.BasicAttack.IsPressed())
        {
            TryPerformingBasicAttack();
        }

        basicAttack.Update();

        if (basicAttackCooldown > 0)
        {
            basicAttackCooldown -= Time.deltaTime;
        }

        if (controls.Movement.MoveA.WasPressedThisFrame())
        {
            TryUsingMove(0);
        }
        if (controls.Movement.MoveB.WasPressedThisFrame())
        {
            TryUsingMove(1);
        }
        if (controls.Movement.UniteMove.WasPressedThisFrame())
        {
            TryUsingUniteMove();
        }


        moves[0].Update();
        moves[1].Update();
        uniteMove.Update();

        if (controls.Movement.CancelMove.WasPressedThisFrame())
        {
            moves[0].Cancel();
            moves[1].Cancel();
            uniteMove.Cancel();
        }

        if (controls.Movement.MoveA.WasReleasedThisFrame())
        {
            TryFinishingMove(0);
        }
        if (controls.Movement.MoveB.WasReleasedThisFrame())
        {
            TryFinishingMove(1);
        }
        if (controls.Movement.UniteMove.WasReleasedThisFrame())
        {
            TryFinishingUniteMove();
        }

        for (int i = 0; i < moveStatuses.Length; i++)
        {
            if (moveStatuses[i].Cooldown > 0)
            {
                moveStatuses[i].Cooldown -= Time.deltaTime;
            }
            else if (moveStatuses[i].StatusType == ActionStatusType.Cooldown)
            {
                moveStatuses[i].StatusType = ActionStatusType.Ready;
            }
        }
    }

    private void CheckIfCanLearnMove()
    {
        Debug.Log($"Checking if can learn move. Current level: {pokemon.CurrentLevel.Value}, Previous level: {prevLevel}");

        for (int i = prevLevel+1; i <= pokemon.CurrentLevel.Value; i++)
        {
            for (int j = 0; j < pokemon.BaseStats.LearnableMoves.Length; j++)
            {
                if (i == pokemon.BaseStats.LearnableMoves[j].level)
                {
                    BattleUIManager.instance.InitializeMoveLearnPanel(pokemon.BaseStats.LearnableMoves[j].moves);
                }
            }
        }

        prevLevel = pokemon.CurrentLevel.Value;
    }

    private void TryPerformingBasicAttack()
    {
        if (basicAttackCooldown > 0)
        {
            return;
        }
        basicAttack.Perform();
        basicAttackCooldown = GetAtkSpeedCooldown();
        onBasicAttackPerformed?.Invoke();
    }

    public void TryUsingMove(int index)
    {
        if (index > moves.Length)
        {
            return;
        }

        if (moveStatuses[index].StatusType != ActionStatusType.Ready)
            return;

        moves[index].Start(playerManager);
    }

    public void TryUsingUniteMove()
    {
        if (uniteMoveCharge < uniteMoveMaxCharge)
            return;

        uniteMove.Start(playerManager);
    }

    public void TryFinishingUniteMove()
    {
        if (uniteMoveCharge < uniteMoveMaxCharge)
            return;

        uniteMove.Finish();
    }

    public void TryFinishingMove(int index)
    {
        if (index > moves.Length)
        {
            return;
        }

        if (moveStatuses[index].StatusType != ActionStatusType.Ready)
            return;

        moves[index].Finish();
    }

    public void OnMoveOver(MoveBase move)
    {
        if (!move.wasMoveSuccessful)
        {
            return;
        }

        if (move.Cooldown > 0)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                if (moves[i] == move)
                {
                    moveStatuses[i].StatusType = ActionStatusType.Cooldown;
                    moveStatuses[i].Cooldown = moves[i].Cooldown;
                    moveStatuses[i].Cooldown -= moveStatuses[i].Cooldown * pokemon.GetCDR() / 100f;
                    UpdateMoveUI(i);
                }
            }
        }
        else
        {
            uniteMoveCharge = 0;
        }

        onMovePerformed?.Invoke(move);
    }

    public MoveBase GetMove(MoveType moveType)
    {
        switch (moveType)
        {
            case MoveType.MoveA:
                return moves[0];
            case MoveType.MoveB:
                return moves[1];
            case MoveType.UniteMove:
                return uniteMove;
            default:
                return null;
        }
    }

    private void UpdateMoveUI(int index)
    {
        switch (moveStatuses[index].StatusType)
        {
            case ActionStatusType.Ready:
                BattleUIManager.instance.SetMoveLock(index, false);
                break;
            case ActionStatusType.Cooldown:
                BattleUIManager.instance.ShowMoveCooldown(index, moveStatuses[index].Cooldown);
                BattleUIManager.instance.SetMoveLock(index, false);
                break;
            case ActionStatusType.Disabled:
                BattleUIManager.instance.SetMoveLock(index, true);
                break;
            default:
                break;
        }
    }

    public void LearnBasicAttack(BasicAttackBase basicAttack)
    {
        this.basicAttack = basicAttack;
        this.basicAttack.Initialize(playerManager);
    }

    public void LearnMove(MoveAsset move)
    {
        switch (move.moveType)
        {
            case MoveType.MoveA:
                moves[0] = MoveDatabase.GetMove(move.move);
                moves[0].onMoveOver += OnMoveOver;
                break;
            case MoveType.MoveB:
                moves[1] = MoveDatabase.GetMove(move.move);
                moves[1].onMoveOver += OnMoveOver;
                break;
            case MoveType.UniteMove:
                uniteMoveMaxCharge = move.uniteEnergyCost;
                uniteMove = MoveDatabase.GetMove(move.move);
                uniteMove.onMoveOver += OnMoveOver;
                break;
            case MoveType.All:
                for (int i = 0; i < moves.Length; i++)
                {
                    moves[i] = MoveDatabase.GetMove(move.move);
                }
                uniteMove = MoveDatabase.GetMove(move.move);
                break;
            default:
                break;
        }

        BattleUIManager.instance.InitializeMoveUI(move);
    }

    public void UpdateMoveStatus(int index, ActionStatusType statusType)
    {
        if (index < 0 || index > moveStatuses.Length)
        {
            return;
        }

        moveStatuses[index].StatusType = statusType;
        UpdateMoveUI(index);
    }

    public void RestoreMoveStatus(int index)
    {
        if (index < 0 || index > moveStatuses.Length)
        {
            return;
        }

        if (moveStatuses[index].Cooldown > 0f)
        {
            moveStatuses[index].StatusType = ActionStatusType.Cooldown;
            UpdateMoveUI(index);
        }
        else
        {
            moveStatuses[index].StatusType = ActionStatusType.Ready;
            UpdateMoveUI(index);
        }
    }

    [Rpc(SendTo.Server)]
    public void LaunchHomingProjectileRpc(ulong targetId, DamageInfo info)
    {
        // Instantiate homing projectile
        GameObject homingProjectile = Instantiate(homingProjectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        homingProjectile.GetComponent<NetworkObject>().Spawn();

        // Set target for homing projectile
        HomingProjectile homingScript = homingProjectile.GetComponent<HomingProjectile>();
        if (homingScript != null)
        {
            homingScript.SetTarget(targetId, info);
        }
    }

    public void LaunchProjectileFromPath(ulong targetId, DamageInfo info, string resourcePath)
    {
        LaunchHomingProjectileRpc(targetId, info, resourcePath);
    }

    [Rpc(SendTo.Server)]
    private void LaunchHomingProjectileRpc(ulong targetId, DamageInfo info, string resourcePath)
    {
        // Instantiate homing projectile
        GameObject homingProjectile = Instantiate(Resources.Load(resourcePath, typeof(GameObject)), projectileSpawnPoint.position, projectileSpawnPoint.rotation) as GameObject;
        homingProjectile.GetComponent<NetworkObject>().Spawn();

        // Set target for homing projectile
        HomingProjectile homingScript = homingProjectile.GetComponent<HomingProjectile>();
        if (homingScript != null)
        {
            homingScript.SetTarget(targetId, info);
        }
    }

    [Rpc(SendTo.Server)]
    public void LaunchMoveForwardProjRpc(Vector2 dir, DamageInfo info, float maxDistance, string resourcePath)
    {
        // Instantiate homing projectile
        GameObject moveForwardsProjectile = Instantiate(Resources.Load(resourcePath, typeof(GameObject)), projectileSpawnPoint.position, Quaternion.identity) as GameObject;
        moveForwardsProjectile.GetComponent<NetworkObject>().Spawn();

        // Set target for homing projectile
        MoveForwardProjectile forwardsScript = moveForwardsProjectile.GetComponent<MoveForwardProjectile>();
        if (forwardsScript != null)
        {
            forwardsScript.SetDirection(dir, info, maxDistance);
        }
    }

    [Rpc(SendTo.Server)]
    public void DespawnNetworkObjectRPC(ulong objectID)
    {
        NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectID, out NetworkObject networkObject);
        if (networkObject != null)
        {
            networkObject.Despawn(true);
        }
    }

    [Rpc(SendTo.Server)]
    public void SpawnNetworkObjectFromStringRPC(string path)
    {
        GameObject spawnedObject = GameObject.Instantiate(Resources.Load(path, typeof(GameObject))) as GameObject;
        spawnedObject.GetComponent<NetworkObject>().Spawn();

        NotifyAboutSpawnRPC(spawnedObject.GetComponent<NetworkObject>().NetworkObjectId);
    }

    [Rpc(SendTo.Server)]
    public void SpawnNetworkObjectFromStringRPC(string path, ulong cliendID)
    {
        GameObject spawnedObject = GameObject.Instantiate(Resources.Load(path, typeof(GameObject))) as GameObject;
        spawnedObject.GetComponent<NetworkObject>().SpawnWithOwnership(cliendID, true);

        NotifyAboutSpawnRPC(spawnedObject.GetComponent<NetworkObject>().NetworkObjectId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyAboutSpawnRPC(ulong objectID)
    {
        onObjectSpawned?.Invoke(NetworkManager.Singleton.SpawnManager.SpawnedObjects[objectID].gameObject);
        onObjectSpawned = null;
    }

    public void IncrementUniteCharge(int amount)
    {
        uniteMoveCharge = Mathf.Clamp(uniteMoveCharge + amount, 0, uniteMoveMaxCharge);
    }

    public float GetAtkSpeedCooldown()
    {
        float atkSpeed = pokemon.GetAtkSpeed();

        foreach (var entry in cooldownTable)
        {
            if (atkSpeed <= entry.threshold)
            {
                return entry.cooldown;
            }
        }

        // Default cooldown value if no threshold is matched
        return 1f;
    }
}

public struct AttackSpeedCooldown
{
    public float threshold;
    public float cooldown;
}