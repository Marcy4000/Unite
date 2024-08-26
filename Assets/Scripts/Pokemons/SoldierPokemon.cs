using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class SoldierPokemon : NetworkBehaviour
{
    private List<Vector3> targets = new List<Vector3>();

    private NavMeshAgent agent;
    private WildPokemon wildPokemon;
    private AnimationManager animationManager;

    private bool orangeTeam;

    private int currentTargetIndex = 0;

    public bool OrangeTeam => orangeTeam;
    public WildPokemon WildPokemon => wildPokemon;

    public override void OnNetworkSpawn()
    {
        wildPokemon = GetComponent<WildPokemon>();
        agent = GetComponent<NavMeshAgent>();
        animationManager = GetComponent<AnimationManager>();
        GetComponent<Pokemon>().OnEvolution += InitializeVision;
    }

    private void InitializeVision()
    {
        Vision vision = GetComponentInChildren<Vision>();
        vision.HasATeam = true;
        vision.CurrentTeam = orangeTeam;
        vision.IsVisible = true;
        vision.SetVisibility(LobbyController.Instance.GetLocalPlayerTeam() == orangeTeam);
    }

    [Rpc(SendTo.Server)]
    public void InitializeRPC(bool orangeTeam, AvailableWildPokemons pokemon, int laneID)
    {
        this.orangeTeam = orangeTeam;

        if (IsServer)
        {
            wildPokemon.SetWildPokemonInfoRPC((short)pokemon, false);
            wildPokemon.Pokemon.OnLevelChange += UpdateSpeed;
            wildPokemon.Pokemon.OnPokemonInitialized += UpdateSpeed;
            wildPokemon.Pokemon.OnStatChange += UpdateSpeed;

            targets.Add(transform.position);

            Vector3[] positions = GameManager.Instance.GetRotomPath(!orangeTeam, laneID);
            foreach (var pos in positions)
            {
                targets.Add(pos);
            }

            animationManager.SetBool("Walking", true);

            MoveToNextTarget();
        }

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
            if (goalZone.OrangeTeam == orangeTeam)
            {
                return;
            }

            goalZone.OnScore(CalculateScoreAmount());
            goalZone.WeaknenGoalZoneRPC(CalculateWeakenTime());
            wildPokemon.Pokemon.TakeDamage(new DamageInfo(NetworkObjectId, 999f, 999, 9999, DamageType.True));
        }
    }

    private int CalculateScoreAmount()
    {
        float hpPercentage = (float)wildPokemon.Pokemon.CurrentHp / wildPokemon.Pokemon.GetMaxHp();
        int scoreAmount = wildPokemon.WildPokemonInfo.EnergyYield;

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
