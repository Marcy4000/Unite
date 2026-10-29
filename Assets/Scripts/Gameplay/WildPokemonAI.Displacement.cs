using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public partial class WildPokemonAI
{
    private Coroutine displacementRoutine;
    private bool displacing;

    // Opt-in distance-based shove for agent-driven wild Pokémon. A physics impulse
    // cannot move their normally kinematic Rigidbody, and disabling the agent alone
    // would let its stale nextPosition pull the Pokémon back after the shove.
    public bool TryDisplace(Vector3 direction, float distance, float duration)
    {
        var pokemon = wildPokemon != null ? wildPokemon.Pokemon : null;
        if (!IsServer || !IsSpawned || pokemon == null || pokemon.CurrentHp <= 0 || pokemon.Type == PokemonType.Objective
            || !CombatTargeting.Finite(direction) || !float.IsFinite(distance) || !float.IsFinite(duration)
            || distance <= 0 || duration <= 0 || agent == null || ShoveImmune(pokemon)) return false;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.001f) return false;
        if (displacementRoutine != null) StopCoroutine(displacementRoutine);
        displacing = true;
        displacementRoutine = StartCoroutine(DisplaceWild(pokemon, direction.normalized, distance, duration));
        return true;
    }

    private static bool ShoveImmune(Pokemon pokemon) => pokemon.HasStatusEffect(StatusType.Unstoppable)
        || pokemon.HasStatusEffect(StatusType.Invincible) || pokemon.HasStatusEffect(StatusType.HindranceResistance);

    private IEnumerator DisplaceWild(Pokemon pokemon, Vector3 direction, float distance, float duration)
    {
        bool stopped = agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isStopped;
        var body = GetComponent<CapsuleCollider>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
        if (rb != null)
        {
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        try
        {
            float remaining = distance, speed = distance / duration;
            while (IsSpawned && pokemon.IsSpawned && pokemon.CurrentHp > 0 && remaining > 0.001f && !ShoveImmune(pokemon))
            {
                float step = Mathf.Min(remaining, speed * Time.deltaTime);
                Vector3 before = transform.position;
                if (!NavMesh.SamplePosition(before, out var start, 0.5f, filter)) yield break;
                Vector3 next = start.position + direction * step;
                if (NavMesh.Raycast(start.position, next, out var edge, filter)) next = edge.position;
                // Carving can lag a frame. Physical walls must stop the shove immediately.
                float bodyRadius = body != null ? body.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) : 0;
                float radius = Mathf.Max(0.05f, agent.radius, bodyRadius), height = Mathf.Max(agent.height, radius * 2);
                Physics.SyncTransforms();
                if (Physics.CapsuleCast(before + Vector3.up * radius, before + Vector3.up * (height - radius), radius,
                    direction, out var contact, step, LayerMask.GetMask("Wall"), QueryTriggerInteraction.Ignore))
                {
                    float allowed = Mathf.Max(0, contact.distance - 0.01f);
                    next = start.position + direction * Mathf.Min(allowed, Vector3.Distance(start.position, next));
                }
                if (!NavMesh.SamplePosition(next, out var ground, 0.1f, filter)) yield break;
                next = ground.position;
                float moved = Vector3.Dot(next - before, direction);
                if (moved <= 0.001f) yield break;
                if (rb != null) rb.position = next;
                transform.position = next;
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.nextPosition = next;
                remaining -= moved;
                if (moved < step * 0.2f) yield break;
                yield return null;
            }
        }
        finally
        {
            displacing = false; displacementRoutine = null;
            // A freeze that began before/during the shove still owns agent disablement.
            // A stationary target resumes at its displaced location, rather than
            // pathing back to its old standing point when the freeze ends.
            if (activeStuns > 0 && (previousState == WildPokemonState.Idle || previousState == WildPokemonState.Attacking))
                previousTargetPosition = transform.position;
            if (rb != null) rb.isKinematic = activeStuns == 0;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = stopped;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (displacementRoutine != null) StopCoroutine(displacementRoutine);
        displacementRoutine = null; displacing = false;
        base.OnNetworkDespawn();
    }
}
