using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class SoldierPokemon : NetworkBehaviour
{
    private List<Vector3> targets = new List<Vector3>();

    private NavMeshAgent agent;
    private WildPokemon wildPokemon;
    private int currentTargetIndex = 0;
    private bool initialized;
    private bool navigationFailed;
    private int recoveryAttempts;
    private float nextRecovery;

    public TeamMember CurrentTeam => wildPokemon.Pokemon.TeamMember;
    public WildPokemon WildPokemon => wildPokemon;

    public void Awake()
    {
        wildPokemon = GetComponent<WildPokemon>();
        agent = GetComponent<NavMeshAgent>();

        Pokemon pokemon = GetComponent<Pokemon>();
        pokemon.OnEvolution += InitializeVision;
        pokemon.OnTeamChange += (team) => StartCoroutine(InitializeVisionCoroutine());
    }

    private void InitializeVision()
    {
        StartCoroutine(InitializeVisionCoroutine());
    }

    private IEnumerator InitializeVisionCoroutine()
    {
        yield return null;
        Vision vision = GetComponentInChildren<Vision>();
        vision.HasATeam = true;
        vision.CurrentTeam = CurrentTeam.Team;
        vision.IsVisiblyEligible = true;
        vision.SetVisibility(LobbyController.Instance.GetLocalPlayerTeam() == CurrentTeam.Team);

        if (IsServer)
            wildPokemon.AnimationManager.SetBool("Walking", true);
    }

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Team orangeTeam, AvailableWildPokemons pokemon, int laneID)
    {
        wildPokemon.Pokemon.UpdateTeamRPC(orangeTeam);

        wildPokemon.SetWildPokemonInfoRPC((short)pokemon, false);
        wildPokemon.Pokemon.OnLevelChange += UpdateSpeed;
        wildPokemon.Pokemon.OnPokemonInitialized += UpdateSpeed;
        wildPokemon.Pokemon.OnStatChange += UpdateSpeed;

        targets.Clear();
        currentTargetIndex = 0;
        initialized = true;

        Team rotomPathTeam = orangeTeam == Team.Orange ? Team.Blue : Team.Orange;

        Vector3[] positions = GameManager.Instance.GetRotomPath(rotomPathTeam, laneID);
        if (positions != null) foreach (var pos in positions)
        {
            targets.Add(pos);
        }

        if (EnsureNavigation()) MoveToNextTarget();

        InitializeClientsRPC();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void InitializeClientsRPC()
    {
        MinimapManager.Instance.CreateObjectiveIcon(wildPokemon);
    }

    private void UpdateSpeed(NetworkListEvent<StatChange> changeEvent)
    {
        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        agent.speed = wildPokemon.Pokemon.GetSpeed() / 1000f;
    }

    private void MoveToNextTarget()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && currentTargetIndex < targets.Count)
        {
            if (agent.SetDestination(targets[currentTargetIndex])) currentTargetIndex++;
        }
    }

    private bool EnsureNavigation()
    {
        if (agent == null || navigationFailed) return false;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) return true;
        if (Time.time < nextRecovery) return false;
        nextRecovery = Time.time + 0.5f;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.SamplePosition(transform.position, out var hit, 5, filter))
        {
            // Disabled agents cannot Warp. Place first, then bind to the mesh.
            agent.enabled = false;
            transform.position = hit.position;
            agent.enabled = true;
            if (agent.isOnNavMesh && agent.Warp(hit.position))
            {
                if (currentTargetIndex > 0) currentTargetIndex--;
                return true;
            }
        }
        if (++recoveryAttempts >= 6)
        {
            navigationFailed = true;
            agent.enabled = false;
            Debug.LogWarning($"{name} could not recover its NavMesh binding after 6 attempts; navigation disabled.", this);
        }
        return false;
    }

    private void Update()
    {
        if (!IsServer || !initialized || !EnsureNavigation())
        {
            return;
        }

        if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < 0.1f))
        {
            MoveToNextTarget();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.CompareTag("GoalZone"))
        {
            GoalZone goalZone = other.GetComponent<GoalZone>();
            if (CurrentTeam.IsOnSameTeam(goalZone.Team))
            {
                return;
            }

            goalZone.ScorePoints(CalculateScoreAmount(), NetworkObjectId);
            goalZone.WeaknenGoalZoneRPC(CalculateWeakenTime());
            wildPokemon.Pokemon.TakeDamageRPC(new DamageInfo(NetworkObjectId, 999f, 999, 9999, DamageType.True));
        }
    }

    private int CalculateScoreAmount()
    {
        float hpPercentage = (float)wildPokemon.Pokemon.CurrentHp / wildPokemon.Pokemon.GetMaxHp();
        int scoreAmount = wildPokemon.EnergyYield;

        if (hpPercentage <= 0.2f)
        {
            scoreAmount = (int)(scoreAmount / 2.5f);
        }
        else if (hpPercentage <= 0.4f)
        {
            scoreAmount = (int)(scoreAmount / 1.667f);
        }
        else if (hpPercentage <= 0.6f)
        {
            scoreAmount = (int)(scoreAmount / 1.25f);
        }

        return scoreAmount;
    }

    private float CalculateWeakenTime()
    {
        float hpPercentage = (float)wildPokemon.Pokemon.CurrentHp / wildPokemon.Pokemon.GetMaxHp();
        float weakenTime = 25f;

        if (hpPercentage <= 0.4f)
        {
            weakenTime = 15f;
        }
        else if (hpPercentage <= 0.6f)
        {
            weakenTime = 20f;
        }

        return weakenTime;
    }
}
