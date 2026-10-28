using System;
using UnityEngine;
using UnityEngine.AI;

// Authored in the gameplay scene: unlike hierarchy names, these bindings survive
// map-state changes and explicitly include the one-shot opening lane farm.
public class BotMapContext : MonoBehaviour
{
    public enum CampKind { Opening, Lane, Jungle, Center, Objective, FinalObjective }
    [Serializable] public class Camp
    {
        public WildPokemonSpawner spawner;
        public CampKind kind;
        public Team team;
        public string lane;
    }
    public Camp[] camps = new Camp[0];
    public GoalZone[] goals = new GoalZone[0];
    public static BotMapContext Instance { get; private set; }
    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public Vector3 LaneFront(Team team, string lane, Vector3 home)
    {
        GoalZone enemy = null;
        float nearest = float.PositiveInfinity;
        foreach (var goal in goals)
        {
            if (goal == null || !goal.gameObject.activeInHierarchy || !goal.IsActive || goal.Team == team ||
                goal.GoalStatus == GoalStatus.Destroyed || goal.GoalTier == 0 || (goal.transform.position.z >= 0) != (lane == "top")) continue;
            float distance = (goal.transform.position - home).sqrMagnitude;
            if (distance < nearest) { nearest = distance; enemy = goal; }
        }
        Vector3 desired = enemy != null ? enemy.transform.position + (home - enemy.transform.position).normalized * 7 :
            new Vector3(0, 0, lane == "top" ? 28 : -28);
        return Sample(desired, home);
    }

    public static Vector3 Sample(Vector3 desired, Vector3 fallback) =>
        NavMesh.SamplePosition(desired, out var hit, 4, NavMesh.AllAreas) ? hit.position : fallback;
}
