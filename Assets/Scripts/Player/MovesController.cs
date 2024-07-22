using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class MovesController : NetworkBehaviour
{
    [SerializeField] private MoveAsset lockedMove;
    public Transform projectileSpawnPoint;
    public GameObject homingProjectilePrefab;

    private MoveBase[] moves = new MoveBase[2];
    private BattleActionStatus[] moveStatuses = new BattleActionStatus[2];

    private int prevLevel = -1;

    private MoveBase uniteMove;
    private BattleActionStatus uniteMoveStatus = new BattleActionStatus(0); // Cooldown float goes unused cause unite move charges with ints
    private int uniteMoveCharge; // Should probably unify this with the BattleActionStatus class but eh
    private int uniteMoveMaxCharge;

    private BasicAttackBase basicAttack;
    private BattleActionStatus basicAttackStatus = new BattleActionStatus(0);

    private BattleItemBase battleItem;
    private BattleActionStatus battleItemStatus = new BattleActionStatus(0);

    private Pokemon pokemon;
    private PlayerControls controls;
    private PlayerManager playerManager;

    public PlayerControls Controls => controls;
    public Pokemon Pokemon => pokemon;
    public PlayerMovement PlayerMovement => playerManager.PlayerMovement;
    public int UniteMoveCharge => uniteMoveCharge;

    public BasicAttackBase BasicAttack => basicAttack;
    public BattleActionStatus BasicAttackStatus => basicAttackStatus;

    public BattleItemBase BattleItem => battleItem;
    public BattleActionStatus BattleItemStatus => battleItemStatus;

    public event Action onBasicAttackPerformed;
    public event Action<MoveBase> onMovePerformed;

    public event Action<GameObject> onObjectSpawned;

    private Vector3 objectSpawnPos = new Vector3(0, -20, 0);

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

        SelectBattleItem();
        battleItemStatus.OnStatusChange += UpdateBattleItemUI;

        LearnMove(lockedMove);

        for (int i = 0; i < moves.Length; i++)
        {
            moveStatuses[i] = new BattleActionStatus(0);
        }

        uniteMoveCharge = 0;
        uniteMoveStatus.AddStatus(ActionStatusType.Cooldown);

        LockEveryAction();
        LearnBasicAttack(BasicAttacksDatabase.GetBasicAttack(pokemon.BaseStats.PokemonName));
        StartCoroutine(PassiveUniteCharge());
    }

    public void GameStarted()
    {
        UnlockEveryAction();
        CheckIfCanLearnMove();
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

        if (basicAttackStatus.Cooldown > 0)
        {
            basicAttackStatus.Cooldown -= Time.deltaTime;
        }

        if (basicAttackStatus.HasStatus(ActionStatusType.Cooldown) && basicAttackStatus.Cooldown <= 0)
        {
            BasicAttackStatus.RemoveStatus(ActionStatusType.Cooldown);
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

        if (controls.Movement.BattleItem.WasPressedThisFrame())
        {
            TryUsingBattleItem();
        }


        moves[0].Update();
        moves[1].Update();
        uniteMove.Update();

        battleItem.Update();

        if (controls.Movement.CancelMove.WasPressedThisFrame())
        {
            moves[0].Cancel();
            moves[1].Cancel();
            uniteMove.Cancel();
            battleItem.Cancel();
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

        if (controls.Movement.BattleItem.WasReleasedThisFrame())
        {
            TryFinishingBattleItem();
        }

        for (int i = 0; i < moveStatuses.Length; i++)
        {
            if (moveStatuses[i].Cooldown > 0)
            {
                moveStatuses[i].Cooldown -= Time.deltaTime;
            }
            else if (moveStatuses[i].HasStatus(ActionStatusType.Cooldown))
            {
                moveStatuses[i].RemoveStatus(ActionStatusType.Cooldown);
            }
        }

        if (battleItemStatus.Cooldown > 0)
        {
            battleItemStatus.Cooldown -= Time.deltaTime;
        }
        else if (battleItemStatus.HasStatus(ActionStatusType.Cooldown))
        {
            battleItemStatus.RemoveStatus(ActionStatusType.Cooldown);
        }

        UpdateUniteMoveUI();
    }

    private void CheckIfCanLearnMove()
    {
        Debug.Log($"Checking if can learn move. Current level: {pokemon.CurrentLevel}, Previous level: {prevLevel}");

        for (int i = prevLevel+1; i <= pokemon.CurrentLevel; i++)
        {
            for (int j = 0; j < pokemon.BaseStats.LearnableMoves.Length; j++)
            {
                if (i == pokemon.BaseStats.LearnableMoves[j].level)
                {
                    LearnableMove learnableMove = RemoveAlreadyLearnedMoves(pokemon.BaseStats.LearnableMoves[j]);
                    BattleUIManager.instance.InitializeMoveLearnPanel(learnableMove.moves);
                }
            }
        }

        prevLevel = pokemon.CurrentLevel;
    }

    private LearnableMove RemoveAlreadyLearnedMoves(LearnableMove learnableMoves)
    {
        LearnableMove newLearnableMove = new LearnableMove();
        newLearnableMove.level = learnableMoves.level;
        newLearnableMove.isUpgraded = learnableMoves.isUpgraded;
        List<MoveAsset> newMoves = new List<MoveAsset>();
        foreach (MoveAsset move in learnableMoves.moves)
        {
            move.isUpgraded = learnableMoves.isUpgraded;
            MoveBase actualMove = MoveDatabase.GetMove(move.move);
            actualMove.IsUpgraded = move.isUpgraded;
            if (IsMatchingMove(move.moveType, actualMove.GetType(), move.isUpgraded))
            {
                newMoves.Add(move);
            }
        }

        newLearnableMove.moves = newMoves.ToArray();
        return newLearnableMove;
    }

    private bool IsMatchingMove(MoveType moveType, Type actualMoveType, bool isUpgraded)
    {
        Dictionary<MoveType, Type> moveTypeToClassType = new Dictionary<MoveType, Type>
        {
            { MoveType.MoveA, moves[0].GetType() },
            { MoveType.MoveB, moves[1].GetType() },
            { MoveType.UniteMove, uniteMove.GetType() }
        };

        if (moveType == MoveType.All)
        {
            return isUpgraded
                ? actualMoveType == moves[0].GetType() && actualMoveType == moves[1].GetType() && actualMoveType == uniteMove.GetType()
                : actualMoveType != moves[0].GetType() && actualMoveType != moves[1].GetType() && actualMoveType != uniteMove.GetType();
        }

        return isUpgraded
            ? actualMoveType == moveTypeToClassType[moveType]
            : actualMoveType != moveTypeToClassType[moveType];
    }

    private void TryPerformingBasicAttack()
    {
        if (!basicAttackStatus.HasStatus(ActionStatusType.None))
        {
            return;
        }
        basicAttack.Perform();
        basicAttackStatus.Cooldown = GetAtkSpeedCooldown();
        BasicAttackStatus.AddStatus(ActionStatusType.Cooldown);
        onBasicAttackPerformed?.Invoke();
    }

    public void TryUsingMove(int index)
    {
        if (index > moves.Length)
        {
            return;
        }

        if (!moveStatuses[index].HasStatus(ActionStatusType.None))
            return;

        moves[index].Start(playerManager);
    }

    public void TryUsingUniteMove()
    {
        if (!uniteMoveStatus.HasStatus(ActionStatusType.None))
            return;

        uniteMove.Start(playerManager);
    }

    public void TryUsingBattleItem()
    {
        if (!battleItemStatus.HasStatus(ActionStatusType.None))
            return;

        battleItem.Start(playerManager);
    }

    public void TryFinishingUniteMove()
    {
        if (!uniteMoveStatus.HasStatus(ActionStatusType.None))
            return;

        uniteMove.Finish();
    }

    public void TryFinishingMove(int index)
    {
        if (index > moves.Length)
        {
            return;
        }

        if (!moveStatuses[index].HasStatus(ActionStatusType.None))
            return;

        moves[index].Finish();
    }

    public void TryFinishingBattleItem()
    {
        if (!battleItemStatus.HasStatus(ActionStatusType.None))
            return;

        battleItem.Finish();
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
                    moveStatuses[i].AddStatus(ActionStatusType.Cooldown);
                    moveStatuses[i].Cooldown = moves[i].Cooldown;
                    moveStatuses[i].Cooldown -= moveStatuses[i].Cooldown * pokemon.GetCDR() / 100f;
                    UpdateMoveUI(i);
                }
            }
        }
        else
        {
            uniteMoveStatus.AddStatus(ActionStatusType.Cooldown);
            uniteMoveCharge = 0;
        }

        onMovePerformed?.Invoke(move);
    }

    private void OnBattleItemOver()
    {
        if (!battleItem.wasUseSuccessful)
        {
            return;
        }

        battleItemStatus.AddStatus(ActionStatusType.Cooldown);
        battleItemStatus.Cooldown = battleItem.Cooldown;
        UpdateBattleItemUI();
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
        bool showLock = moveStatuses[index].HasStatus(ActionStatusType.Disabled) || moveStatuses[index].HasStatus(ActionStatusType.Stunned);
        BattleUIManager.instance.SetMoveLock(index, showLock);

        if (moveStatuses[index].HasStatus(ActionStatusType.Cooldown))
        {
            BattleUIManager.instance.ShowMoveCooldown(index, moveStatuses[index].Cooldown);
        }
    }

    public void LockEveryAction()
    {
        for (int i = 0; i < moveStatuses.Length; i++)
        {
            moveStatuses[i].AddStatus(ActionStatusType.Disabled);
            UpdateMoveUI(i);
        }

        battleItemStatus.AddStatus(ActionStatusType.Disabled);
        UpdateBattleItemUI();

        uniteMoveStatus.AddStatus(ActionStatusType.Disabled);
        UpdateUniteMoveUI();

        basicAttackStatus.AddStatus(ActionStatusType.Disabled);
    }

    public void UnlockEveryAction()
    {
        for (int i = 0; i < moveStatuses.Length; i++)
        {
            moveStatuses[i].RemoveStatus(ActionStatusType.Disabled);
            UpdateMoveUI(i);
        }

        battleItemStatus.RemoveStatus(ActionStatusType.Disabled);
        UpdateBattleItemUI();

        uniteMoveStatus.RemoveStatus(ActionStatusType.Disabled);
        UpdateUniteMoveUI();

        basicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
    }

    private void UpdateBattleItemUI()
    {
        bool showLock = battleItemStatus.HasStatus(ActionStatusType.Disabled) || battleItemStatus.HasStatus(ActionStatusType.Stunned);
        BattleUIManager.instance.SetBattleItemLock(showLock);

        if (battleItemStatus.HasStatus(ActionStatusType.Cooldown))
        {
            BattleUIManager.instance.ShowBattleItemCooldown(battleItemStatus.Cooldown);
        }
    }

    private void UpdateUniteMoveUI()
    {
        bool showLock = uniteMoveStatus.HasStatus(ActionStatusType.Disabled) || uniteMoveStatus.HasStatus(ActionStatusType.Stunned);
        BattleUIManager.instance.SetUniteMoveDisabledLock(showLock);

        BattleUIManager.instance.UpdateUniteMoveCooldown(uniteMoveCharge, uniteMoveMaxCharge);
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
                moves[0].IsUpgraded = move.isUpgraded;
                break;
            case MoveType.MoveB:
                moves[1] = MoveDatabase.GetMove(move.move);
                moves[1].onMoveOver += OnMoveOver;
                moves[1].IsUpgraded = move.isUpgraded;
                break;
            case MoveType.UniteMove:
                uniteMoveMaxCharge = move.uniteEnergyCost;
                uniteMove = MoveDatabase.GetMove(move.move);
                uniteMove.onMoveOver += OnMoveOver;
                uniteMove.IsUpgraded = move.isUpgraded;
                break;
            case MoveType.All:
                for (int i = 0; i < moves.Length; i++)
                {
                    moves[i] = MoveDatabase.GetMove(move.move);
                }
                uniteMove = MoveDatabase.GetMove(move.move);
                uniteMoveMaxCharge = move.uniteEnergyCost;
                break;
            default:
                break;
        }

        BattleUIManager.instance.InitializeMoveUI(move);
    }

    private void SelectBattleItem()
    {
        BattleItemAsset selectedBattleItem = CharactersList.instance.GetBattleItemByID(int.Parse(LobbyController.Instance.Player.Data["BattleItem"].Value));

        battleItem = BattleItemDatabase.GetBattleItem(selectedBattleItem.battleItemType);

        BattleUIManager.instance.InitializeBattleItemUI(selectedBattleItem);

        battleItem.onBattleItemOver += OnBattleItemOver;
    }

    public void CancelAllMoves()
    {
        moves[0].Cancel();
        moves[1].Cancel();
        uniteMove.Cancel();
    }

    public void AddMoveStatus(int index, ActionStatusType statusType)
    {
        // If index is equal to the length of the moveStatuses array, it means we're adding a status to the unite move
        if (index == moveStatuses.Length)
        {
            uniteMoveStatus.AddStatus(statusType);
            return;
        }
        else if (index < 0 || index > moveStatuses.Length)
        {
            return;
        }

        moveStatuses[index].AddStatus(statusType);
        UpdateMoveUI(index);
    }

    public void RemoveMoveStatus(int index, ActionStatusType statusType)
    {
        // If index is equal to the length of the moveStatuses array, it means we're removing a status from the unite move
        if (index == moveStatuses.Length)
        {
            uniteMoveStatus.RemoveStatus(statusType);
            return;
        }
        else if (index < 0 || index > moveStatuses.Length)
        {
            return;
        }

        moveStatuses[index].RemoveStatus(statusType);
        UpdateMoveUI(index);
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
    private void LaunchHomingProjectileRpc(ulong targetId, DamageInfo info, string addressableKey)
    {
        Addressables.LoadAssetAsync<GameObject>(addressableKey).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                GameObject homingProjectile = Instantiate(prefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
                homingProjectile.GetComponent<NetworkObject>().Spawn();

                HomingProjectile homingScript = homingProjectile.GetComponent<HomingProjectile>();
                if (homingScript != null)
                {
                    homingScript.SetTarget(targetId, info);
                }
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };
    }

    [Rpc(SendTo.Server)]
    public void LaunchMoveForwardProjRpc(Vector2 dir, DamageInfo info, float maxDistance, string addressableKey)
    {
        Addressables.LoadAssetAsync<GameObject>(addressableKey).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                GameObject moveForwardsProjectile = Instantiate(prefab, projectileSpawnPoint.position, Quaternion.identity);
                moveForwardsProjectile.GetComponent<NetworkObject>().Spawn();

                MoveForwardProjectile forwardsScript = moveForwardsProjectile.GetComponent<MoveForwardProjectile>();
                if (forwardsScript != null)
                {
                    forwardsScript.SetDirection(dir, info, maxDistance);
                }
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };
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
    public void SpawnNetworkObjectFromStringRPC(string addressableKey)
    {
        Addressables.LoadAssetAsync<GameObject>(addressableKey).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                GameObject spawnedObject = Instantiate(prefab, objectSpawnPos, Quaternion.identity);
                spawnedObject.GetComponent<NetworkObject>().Spawn(true);

                NotifyAboutSpawnRPC(spawnedObject.GetComponent<NetworkObject>().NetworkObjectId);
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };
    }

    [Rpc(SendTo.Server)]
    public void SpawnNetworkObjectFromStringRPC(string addressableKey, ulong cliendID)
    {
        Addressables.LoadAssetAsync<GameObject>(addressableKey).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                GameObject spawnedObject = Instantiate(prefab, objectSpawnPos, Quaternion.identity);
                spawnedObject.GetComponent<NetworkObject>().SpawnWithOwnership(cliendID, true);

                NotifyAboutSpawnRPC(spawnedObject.GetComponent<NetworkObject>().NetworkObjectId);
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };
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
        if (uniteMoveCharge == uniteMoveMaxCharge && uniteMoveStatus.HasStatus(ActionStatusType.Cooldown))
        {
            uniteMoveStatus.RemoveStatus(ActionStatusType.Cooldown);
        }
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