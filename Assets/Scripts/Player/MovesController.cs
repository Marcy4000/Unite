using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MovesController : NetworkBehaviour
{
    [SerializeField] private MoveAsset lockedMove;
    public Transform projectileSpawnPoint; // Define the spawn point for the projectile
    public GameObject homingProjectilePrefab; // Reference to the homing projectile prefab
    public float attackRadius = 10f; // Radius to search for enemies
    [SerializeField]private int uniteMoveCharge = 0;
    private int uniteMoveMaxCharge = 10000;

    private MoveBase[] moves = new MoveBase[2];
    private float[] moveCooldowns = new float[2];

    private MoveBase uniteMove;

    private Pokemon pokemon;
    private PlayerControls controls;
    private PlayerManager playerManager;

    public PlayerControls Controls => controls;
    public Pokemon Pokemon => pokemon;
    public PlayerMovement PlayerMovement => playerManager.PlayerMovement;
    public int UniteMoveCharge => uniteMoveCharge;

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
            moveCooldowns[i] = 0;
        }

        CheckIfCanLearnMove();
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

        Aim.Instance.ShowBasicAtk(controls.Movement.BasicAttack.IsPressed(), attackRadius);

        if (controls.Movement.BasicAttack.WasPressedThisFrame())
        {
            PerformBasicAttack();
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

        if (controls.Movement.MoveA.IsPressed())
        {
            moves[0].Update();
        }
        if (controls.Movement.MoveB.IsPressed())
        {
            moves[1].Update();
        }
        if (controls.Movement.UniteMove.IsPressed())
        {
            uniteMove.Update();
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

        for (int i = 0; i < moveCooldowns.Length; i++)
        {
            if (moveCooldowns[i] > 0)
            {
                moveCooldowns[i] -= Time.deltaTime;
            }
        }
    }

    private void CheckIfCanLearnMove()
    {
        for (int i = 0; i < pokemon.BaseStats.LearnableMoves.Length; i++)
        {
            if (pokemon.CurrentLevel.Value == pokemon.BaseStats.LearnableMoves[i].level)
            {
                BattleUIManager.instance.InitializeMoveLearnPanel(pokemon.BaseStats.LearnableMoves[i].moves);
                //LearnMove(pokemon.BaseStats.LearnableMoves[i].moves[0]);
            }
        }
    }

    public void TryUsingMove(int index)
    {
        if (index > moves.Length)
        {
            return;
        }

        if (moveCooldowns[index] > 0)
            return;

        moves[index].Start(this);
    }

    public void TryUsingUniteMove()
    {
        if (uniteMoveCharge < uniteMoveMaxCharge)
            return;

        uniteMove.Start(this);
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

        if (moveCooldowns[index] > 0)
            return;

        moves[index].Finish();
    }

    public void OnMoveOver(MoveBase move)
    {
        if (!move.wasMoveSuccessful)
        {
            return;
        }

        if (move.cooldown > 0)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                if (moves[i] == move)
                {
                    moveCooldowns[i] = moves[i].cooldown;
                    BattleUIManager.instance.ShowMoveCooldown(i, moves[i].cooldown);
                }
            }
        }
        else
        {
            uniteMoveCharge = 0;
        }
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

    private void PerformBasicAttack()
    {
        GameObject closestEnemy = Aim.Instance.AimInCircle(attackRadius);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            LaunchHomingProjectileRpc(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, new DamageInfo(pokemon.NetworkObjectId, 1, 0, 0, DamageType.Physical));
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

    public void IncrementUniteCharge(int amount)
    {
        uniteMoveCharge = Mathf.Clamp(uniteMoveCharge + amount, 0, uniteMoveMaxCharge);
    }
}
