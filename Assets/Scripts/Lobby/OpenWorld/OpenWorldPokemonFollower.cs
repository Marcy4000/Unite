using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class OpenWorldPokemonFollower : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float followDistance = 3f;
    [SerializeField, Min(0.1f)] private float stopDistance = 1.5f;
    [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;
    [SerializeField, Min(0.1f)] private float sampleRadius = 3f;
    [SerializeField, Min(1f)] private float catchUpDistance = 25f;

    private static readonly int Walking = Animator.StringToHash("Walking");
    private NavMeshAgent agent;
    private Transform target;
    private GameObject visual;
    private Animator[] animators;
    private float nextUpdate;

    public Transform Target => target;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
    }

    public void Initialize(Transform trainer, GameObject model)
    {
        target = trainer;
        agent.stoppingDistance = stopDistance;
        visual = Instantiate(model, transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var compatibleAnimators = new System.Collections.Generic.List<Animator>();
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == Walking && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    compatibleAnimators.Add(animator);
                    break;
                }
        }
        animators = compatibleAnimators.ToArray();
        if (!TryGetComponent<FakeMeshShadow>(out var fakeShadow))
            fakeShadow = gameObject.AddComponent<FakeMeshShadow>();
        fakeShadow.SetSource(visual.transform);
        visual.SetActive(false);
        TryPlaceNearTrainer();
    }

    private bool Sample(Vector3 position, out NavMeshHit hit)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.SamplePosition(position, out hit, sampleRadius, filter);
    }

    private bool TryPlaceNearTrainer()
    {
        if (target == null || !Sample(target.position - target.forward * stopDistance, out var hit))
        {
            agent.enabled = false;
            visual.SetActive(false);
            return false;
        }
        agent.enabled = false;
        transform.SetPositionAndRotation(hit.position, target.rotation);
        agent.enabled = true;
        bool placed = agent.isOnNavMesh && agent.Warp(hit.position);
        visual.SetActive(placed);
        if (!placed) agent.enabled = false;
        return placed;
    }

    private void Update()
    {
        if (target == null || visual == null) return;
        if (Time.time >= nextUpdate)
        {
            nextUpdate = Time.time + updateInterval;
            if (!agent.enabled || !agent.isOnNavMesh ||
                (transform.position - target.position).sqrMagnitude > catchUpDistance * catchUpDistance)
            {
                SetWalking(false);
                TryPlaceNearTrainer();
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > followDistance && Sample(target.position, out var hit)) agent.SetDestination(hit.position);
            else if (distance <= stopDistance && !agent.isOnOffMeshLink) agent.ResetPath();
        }
        SetWalking(agent.enabled && agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f);
    }

    private void SetWalking(bool walking)
    {
        if (animators == null) return;
        foreach (var animator in animators)
            if (animator != null && animator.isActiveAndEnabled) animator.SetBool(Walking, walking);
    }

    private void OnDisable()
    {
        SetWalking(false);
        if (agent != null) agent.enabled = false;
    }
}
