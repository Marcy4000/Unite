using UnityEngine;
using UnityEngine.AI;

// State is derived from replicated match objects on every peer. The navigation
// obstacle is carved only while the physical wall is present; no permanent bake
// or extra network object is needed for a removable map barrier.
public class DynamicMapBarrier : MonoBehaviour
{
    [SerializeField] private GoalZone opensWhenGoalActive;
    [SerializeField] private WildPokemonSpawner blocksWhileObjectiveAlive;
    [SerializeField] private Collider[] blockingColliders = new Collider[0];
    [SerializeField] private Renderer[] wallRenderers = new Renderer[0];
    [SerializeField] private NavMeshObstacle navigationObstacle;
    private bool initialized;
    public bool IsBlocking { get; private set; }

    private void Update()
    {
        bool blocked = true;
        if (opensWhenGoalActive != null && opensWhenGoalActive.IsSpawned) blocked = !opensWhenGoalActive.IsActive;
        if (blocksWhileObjectiveAlive != null && blocksWhileObjectiveAlive.IsSpawned) blocked = blocksWhileObjectiveAlive.HasLivingPokemon;
        SetBlocked(blocked);
    }

    public void SetBlocked(bool blocked)
    {
        if (initialized && IsBlocking == blocked) return;
        initialized = true;
        IsBlocking = blocked;
        foreach (var collider in blockingColliders)
            if (collider != null) collider.enabled = blocked;
        foreach (var renderer in wallRenderers)
            if (renderer != null) renderer.enabled = blocked;
        if (navigationObstacle != null) navigationObstacle.enabled = blocked;
    }
}
