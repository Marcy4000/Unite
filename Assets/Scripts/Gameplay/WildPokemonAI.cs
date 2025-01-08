using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class WildPokemonAI : NetworkBehaviour
{
    public enum WildPokemonState
    {
        Idle,
        MovingToPosition,
        Chasing,
        Attacking,
        Stunned
    }

    [SerializeField] private WildPokemon wildPokemon;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private NavMeshAgent agent;

    private WildPokemonAISettings aiSettings;
    private Vector3 targetPosition;
    private WildPokemonState state;

    private BattleActionStatus basicAttackStatus;
    private WildPokemonBasicAtk basicAttack = BasicAttacksDatabase.GetBasicAttack("wildpokemon") as WildPokemonBasicAtk;
    private BattleActionStatus[] moveStatuses = new BattleActionStatus[0];
    private MoveBase[] moves = new MoveBase[0];

    //private int currentMoveIndex = -1;
    private float stunnedTimer;
    private Transform currentTarget; // Keeps track of the current aggro target

    public WildPokemon WildPokemon => wildPokemon;
    public WildPokemonAISettings AISettings => aiSettings;
    public WildPokemonState State => state;
    public AnimationManager AnimationManager => animationManager;

    public void Initialize(WildPokemonAISettings settings)
    {
        aiSettings = settings;
        state = WildPokemonState.Idle;
        basicAttackStatus = new BattleActionStatus(0);

        basicAttack.Initialize(this, aiSettings.basicAttackRange);

        if (wildPokemon.Pokemon.BaseStats.LearnableMoves.Length > 0)
        {
            LearnableMove learnableMove = wildPokemon.Pokemon.BaseStats.LearnableMoves[0];

            moves = new MoveBase[learnableMove.moves.Length];
            moveStatuses = new BattleActionStatus[learnableMove.moves.Length];

            for (int i = 0; i < learnableMove.moves.Length; i++)
            {
                moves[i] = MoveDatabase.GetMove(learnableMove.moves[i].move);
                moveStatuses[i] = new BattleActionStatus(0);
            }
        }

        wildPokemon.Pokemon.OnLevelChange += UpdateSpeed;
        wildPokemon.Pokemon.OnPokemonInitialized += UpdateSpeed;
        wildPokemon.Pokemon.OnStatChange += UpdateSpeed;

        if (settings.movementType == WildPokemonAISettings.MovementType.MoveToPointUponAttack || settings.movementType == WildPokemonAISettings.MovementType.ChasePlayer)
        {
            wildPokemon.Pokemon.OnDamageTaken += OnPokemonDamage;
        }

        UpdateSpeed();
    }

    private void OnPokemonDamage(DamageInfo damage)
    {
        if (aiSettings.movementType == WildPokemonAISettings.MovementType.MoveToPointUponAttack)
        {
            state = WildPokemonState.MovingToPosition;
            targetPosition = new Vector3(aiSettings.positionToMoveTo.x, 0, aiSettings.positionToMoveTo.y);
            agent.SetDestination(targetPosition);
        }
        else if (aiSettings.movementType == WildPokemonAISettings.MovementType.ChasePlayer)
        {
            Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[damage.attackerId].GetComponent<Pokemon>();

            if (attacker == null || attacker.Type != PokemonType.Player)
            {
                return;
            }

            state = WildPokemonState.Chasing;
            currentTarget = attacker.transform;
            agent.SetDestination(attacker.transform.position);
        }
    }

    private void UpdateSpeed(NetworkListEvent<StatChange> changeEvent)
    {
        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        agent.speed = wildPokemon.Pokemon.GetSpeed() / 1000f;
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        switch (state)
        {
            case WildPokemonState.Idle:
                HandleIdleState();
                break;

            case WildPokemonState.MovingToPosition:
                HandleMovingToPositionState();
                break;

            case WildPokemonState.Chasing:
                HandleChasingState();
                break;

            case WildPokemonState.Attacking:
                HandleAttackingState();
                break;

            case WildPokemonState.Stunned:
                HandleStunnedState();
                break;
        }

        UpdateCooldowns();
        HandleAnimations();
    }

    private void UpdateCooldowns()
    {
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

        if (basicAttackStatus.Cooldown > 0)
        {
            basicAttackStatus.Cooldown -= Time.deltaTime;
        }
        else if (basicAttackStatus.HasStatus(ActionStatusType.Cooldown))
        {
            basicAttackStatus.RemoveStatus(ActionStatusType.Cooldown);
        }
    }

    private void HandleIdleState()
    {
        if (aiSettings.aggressionType == WildPokemonAISettings.AggressionType.Passive)
        {
            return;
        }

        GameObject[] detected = Aim.Instance.AimInCircleAtPosition(transform.position, aiSettings.noticeRadius, AimTarget.Enemy, Team.Neutral);

        if (detected.Length > 0)
        {
            currentTarget = GetClosestPlayer(detected);
            if (aiSettings.movementType == WildPokemonAISettings.MovementType.ChasePlayer)
            {
                state = WildPokemonState.Chasing;
                agent.SetDestination(currentTarget.position);
            }
            else if (aiSettings.movementType == WildPokemonAISettings.MovementType.Stationary)
            {
                state = WildPokemonState.Attacking;
            }
        }
    }

    private void HandleMovingToPositionState()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            state = WildPokemonState.Idle;
        }
    }

    private void HandleChasingState()
    {
        if (currentTarget == null)
        {
            // Transition to Idle and return to home position
            state = WildPokemonState.Idle;
            agent.SetDestination(new Vector3(aiSettings.homePosition.x, 0, aiSettings.homePosition.y));
            return;
        }

        Vector3 homePosition = new Vector3(aiSettings.homePosition.x, 0, aiSettings.homePosition.y);

        // If the Pokémon moves out of its home radius, reset to home
        if (Vector3.Distance(transform.position, homePosition) > aiSettings.homeRadius)
        {
            currentTarget = null;
            state = WildPokemonState.Idle;
            agent.SetDestination(homePosition);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        // Switch to attacking if close enough
        if (distanceToTarget <= aiSettings.basicAttackRange && aiSettings.aggressionType == WildPokemonAISettings.AggressionType.Aggressive)
        {
            state = WildPokemonState.Attacking;
            agent.ResetPath();
        }
        else
        {
            agent.SetDestination(currentTarget.position);
        }
    }

    private void HandleAttackingState()
    {
        if (currentTarget == null)
        {
            state = WildPokemonState.Idle;
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        // Switch back to chasing if target is out of range
        if (distanceToTarget > aiSettings.basicAttackRange)
        {
            if (aiSettings.movementType != WildPokemonAISettings.MovementType.Stationary)
            {
                state = WildPokemonState.Chasing;
                agent.SetDestination(currentTarget.position);
            }
            else
            {
                state = WildPokemonState.Idle;
            }
            return;
        }

        if (basicAttackStatus.HasStatus(ActionStatusType.None))
        {
            basicAttack.Perform(false);
            basicAttackStatus.AddStatus(ActionStatusType.Cooldown);
            basicAttackStatus.Cooldown = 1f;
        }
    }

    private void HandleStunnedState()
    {
        stunnedTimer -= Time.deltaTime;
        if (stunnedTimer <= 0)
        {
            state = WildPokemonState.Idle;
        }
    }

    private Transform GetClosestPlayer(GameObject[] players)
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closest = player.transform;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private void HandleAnimations()
    {
        if (animationManager.IsAnimatorNull())
        {
            return;
        }

        bool isMoving = agent.remainingDistance > agent.stoppingDistance && agent.velocity.magnitude > 0.1f;
        if (isMoving != animationManager.Animator.GetBool("Walking"))
        {
            UpdateAnimations(isMoving);
        }
    }

    private void UpdateAnimations(bool isMoving)
    {
        if (animationManager.IsAnimatorNull())
        {
            return;
        }

        animationManager.SetBool("Walking", isMoving);
    }
}

[System.Serializable]
public struct WildPokemonAISettings : INetworkSerializable
{
    public enum MovementType : byte
    {
        Stationary,
        MoveToPointUponAttack,
        ChasePlayer
    }

    public enum AggressionType : byte
    {
        Passive,
        Aggressive
    }

    public MovementType movementType;
    public AggressionType aggressionType;
    public Vector2 positionToMoveTo;
    public Vector2 homePosition;
    public float homeRadius;
    public float noticeRadius;
    public float basicAttackRange;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref movementType);
        serializer.SerializeValue(ref aggressionType);
        serializer.SerializeValue(ref positionToMoveTo);
        serializer.SerializeValue(ref homePosition);
        serializer.SerializeValue(ref homeRadius);
        serializer.SerializeValue(ref noticeRadius);
        serializer.SerializeValue(ref basicAttackRange);
    }
}