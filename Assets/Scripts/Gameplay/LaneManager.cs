using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class LaneManager : NetworkBehaviour
{
    [SerializeField] private bool orangeTeam;
    [SerializeField] private int maxTier = 2;

    private List<GoalZone> goalZones = new List<GoalZone>();
    private List<FluxZone> fluxZones = new List<FluxZone>();

    public List<GoalZone> GoalZones => goalZones;
    public List<FluxZone> FluxZones => fluxZones;

    public bool OrangeTeam => orangeTeam;
    public int MaxTier => maxTier;

    public IEnumerator Start()
    {
        yield return new WaitForSeconds(0.2f);

        GetTeamGoalZones();
        GetTeamFluxZones();
        InitalizeGoalZones();
    }

    private void GetTeamFluxZones()
    {
        fluxZones = FindObjectsOfType<FluxZone>().ToList();

        for (int i = fluxZones.Count - 1; i >= 0; i--)
        {
            if (fluxZones[i].OrangeTeam != orangeTeam)
            {
                fluxZones.RemoveAt(i);
            }
        }
    }

    private void GetTeamGoalZones()
    {
        goalZones = FindObjectsOfType<GoalZone>().ToList();

        for (int i = goalZones.Count - 1; i >= 0; i--)
        {
            if (goalZones[i].OrangeTeam != orangeTeam)
            {
                goalZones.RemoveAt(i);
            }
            else
            {
                goalZones[i].onGoalZoneDestroyed += OnGoalZoneDestroyed;
            }
        }
    }

    private void InitalizeGoalZones()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (GoalZone goalZone in goalZones)
        {
            if (goalZone.GoalTier < maxTier)
            {
                goalZone.SetIsActive(false);
            }
            else
            {
                goalZone.SetIsActive(true);
            }
        }
    }

    private void OnGoalZoneDestroyed(int laneID, int tier)
    {
        BattleUIManager.instance.UpdateGoalState(orangeTeam);

        if (!IsServer)
        {
            return;
        }

        DisableFluxZone(laneID, tier);

        foreach (GoalZone goalZone in goalZones)
        {
            if (tier-1 == 0)
            {
                if (goalZone.GoalTier == 0)
                {
                    goalZone.SetIsActive(true);
                    break;
                }
            }

            if (goalZone.GoalLaneId == laneID && goalZone.GoalTier == tier-1)
            {
                goalZone.SetIsActive(true);
            }
        }
    }

    private void DisableFluxZone(int laneID, int tier)
    {
        foreach (FluxZone fluxZone in fluxZones)
        {
            if (fluxZone.LaneID == laneID && fluxZone.Tier == tier)
            {
                fluxZone.SetIsActive(false);
            }
        }
    }

    public Vector3[] GetRotomPositions(int rotomLane)
    {
        List<GoalZone> goals = goalZones.Where(x => x.GoalLaneId == rotomLane).ToList();

        goals.Add(goalZones.First(x => x.GoalLaneId == -1));
        goals.Sort((x, y) => x.GoalTier.CompareTo(y.GoalTier));

        goals.Reverse();

        List<Vector3> positions = new List<Vector3>();

        foreach (GoalZone goal in goals)
        {
            positions.Add(goal.transform.position);
        }

        return positions.ToArray();
    }
}
