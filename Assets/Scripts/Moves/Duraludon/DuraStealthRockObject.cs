using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DuraStealthRockObject : DuraMoveObject
{
    [SerializeField] private SphereCollider rockArea;
    [SerializeField] private GameObject projectile, hiddenField, triggeredField;
    [SerializeField] private float projectileSpeed = 18f;
    [SerializeField] private float fieldLifetime = 60f;
    [SerializeField] private float tickInterval = 0.5f;
    private static readonly Dictionary<ulong, List<DuraStealthRockObject>> fields = new Dictionary<ulong, List<DuraStealthRockObject>>();
    private int phase;
    private ulong actorId;

    protected override void BeginPresentation() { SetVisibility(); }

    private void LateUpdate()
    {
        if (IsClient && Actor != null) SetVisibility();
    }

    private void SetVisibility()
    {
        bool visible = phase == 2 || (Aim.Instance != null && Aim.Instance.TeamToIgnore == Actor.CurrentTeam.Team);
        projectile.SetActive(phase == 0);
        hiddenField.SetActive(phase == 1 && visible);
        triggeredField.SetActive(phase == 2);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void SetPhaseRPC(int nextPhase, Vector3 position)
    {
        phase = nextPhase;
        transform.position = position;
        if (IsClient) SetVisibility();
    }

    protected override IEnumerator Execute()
    {
        actorId = Actor.NetworkObjectId;
        Vector3 destination = Origin + Vector3.ClampMagnitude(Destination - Origin, 7f);
        while ((transform.position - destination).sqrMagnitude > 0.001f)
        {
            Vector3 current = transform.position;
            Vector3 next = Vector3.MoveTowards(current, destination, projectileSpeed * Time.deltaTime);
            Vector3 travel = next - current;
            Pokemon intercepted = null;
            float closest = travel.magnitude;
            foreach (RaycastHit hit in Physics.SphereCastAll(current + Vector3.up, 0.35f, travel.normalized, travel.magnitude))
            {
                if (hit.collider.TryGetComponent(out Pokemon target) && CanHit(target) && hit.distance <= closest)
                {
                    closest = hit.distance;
                    intercepted = target;
                }
            }
            transform.position = intercepted != null ? intercepted.transform.position : next;
            if (intercepted != null) break;
            yield return null;
        }

        if (!fields.TryGetValue(actorId, out var active)) fields[actorId] = active = new List<DuraStealthRockObject>();
        if (active.Count >= 2) active[0].Despawn();
        active.Add(this);
        SetPhaseRPC(1, transform.position);
        float expires = Time.time + fieldLifetime;
        while (Time.time < expires && TargetsIn(rockArea).Count == 0) yield return new WaitForSeconds(0.1f);
        if (Time.time >= expires) yield break;

        SetPhaseRPC(2, transform.position);
        int shield = Mathf.RoundToInt(Actor.Pokemon.GetAttack() * 0.6f + 250);
        AddRefreshingShield(8843, shield, shield * 5, 3f);
        var hits = new Dictionary<ulong, int>();
        for (int tick = 0; tick < 4; tick++)
        {
            foreach (Pokemon target in TargetsIn(rockArea))
            {
                hits.TryGetValue(target.NetworkObjectId, out int count);
                hits[target.NetworkObjectId] = ++count;
                target.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 0.28f, 2, 80, DamageType.Physical));
                Slow(target, (short)Mathf.Min(100, count * 25), 2f, 8843);
                if (count == 2) target.AddStatusEffect(new StatusEffect(StatusType.Immobilized, 1f, true, 0));
            }
            yield return new WaitForSeconds(tickInterval);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && fields.TryGetValue(actorId, out var active))
        {
            active.Remove(this);
            if (active.Count == 0) fields.Remove(actorId);
        }
        base.OnNetworkDespawn();
    }
}
