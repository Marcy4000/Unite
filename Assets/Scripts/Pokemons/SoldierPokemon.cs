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
        vision.IsVisible = true;
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

        targets.Add(transform.position);

        Team rotomPathTeam = orangeTeam == Team.Orange ? Team.Blue : Team.Orange;

        Vector3[] positions = GameManager.Instance.GetRotomPath(rotomPathTeam, laneID);
        foreach (var pos in positions)
        {
            targets.Add(pos);
        }

        MoveToNextTarget();

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
        if (currentTargetIndex < targets.Count)
        {
            agent.SetDestination(targets[currentTargetIndex]);
            currentTargetIndex++;
        }
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (agent.remainingDistance < 0.1f)
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
