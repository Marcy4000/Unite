using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SylvMysticalFireEffect : PrefabMoveEffect
{
    [SerializeField] private Transform[] flames;
    private readonly Pokemon[] recipients = new Pokemon[5];
    private readonly bool[] launched = new bool[5], spent = new bool[5];
    private readonly HashSet<ulong> hitTargets = new HashSet<ulong>();
    private int count;
    private bool ready;

    protected override void BeginPresentation()
    {
        count = SylvMoveRules.FlameCount(Upgraded);
        for (int i = 0; i < flames.Length; i++) flames[i].gameObject.SetActive(i < count);
        ready = true;
    }

    protected override IEnumerator Execute()
    {
        yield return new WaitForSeconds(0.25f);
        float until = Time.time + SylvMoveRules.FireLifetime;
        for (int next = 0; next < count && Time.time < until;)
        {
            Pokemon target = FindTarget();
            if (target == null) { yield return new WaitForSeconds(0.1f); continue; }
            LaunchRPC(next, target.NetworkObjectId, flames[next].position);
            next++;
            yield return new WaitForSeconds(SylvMoveRules.FireInterval);
        }
        // Allow launched flames to finish after the orbiting flames expire.
        for (int i = 0; i < count; i++) if (!launched[i]) SpendRPC(i);
        float flightUntil = Time.time + 3;
        while (Time.time < flightUntil)
        {
            bool flying = false;
            for (int i = 0; i < count; i++) flying |= !spent[i];
            if (!flying) yield break;
            yield return null;
        }
    }

    private Pokemon FindTarget()
    {
        Pokemon closest = null;
        float best = float.PositiveInfinity;
        foreach (Collider collider in Physics.OverlapSphere(Actor.transform.position, SylvMoveRules.FireRange))
        {
            if (!collider.TryGetComponent(out Pokemon candidate) || !CanHit(candidate, true)) continue;
            float distance = (candidate.transform.position - Actor.transform.position).sqrMagnitude;
            bool player = candidate.Type == PokemonType.Player;
            bool previousPlayer = closest != null && closest.Type == PokemonType.Player;
            if (closest == null || (player && !previousPlayer) || (player == previousPlayer && distance < best))
            { closest = candidate; best = distance; }
        }
        return closest;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LaunchRPC(int index, ulong targetId, Vector3 origin)
    {
        launched[index] = true;
        flames[index].position = origin;
        recipients[index] = NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var target)
            ? target.GetComponent<Pokemon>() : null;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void SpendRPC(int index) { spent[index] = true; flames[index].gameObject.SetActive(false); }

    protected override void Update()
    {
        base.Update();
        if (!ready || Actor == null || !IsSpawned || Actor.Pokemon.CurrentHp <= 0) return;
        for (int i = 0; i < count; i++)
        {
            if (spent[i]) continue;
            if (!launched[i])
            {
                float angle = Time.time * 2 + i * Mathf.PI * 2 / count;
                flames[i].position = Actor.transform.position + new Vector3(Mathf.Sin(angle), 1.1f, Mathf.Cos(angle));
                continue;
            }
            Pokemon target = recipients[i];
            if (!CanHit(target)) { if (IsServer) SpendRPC(i); continue; }
            Vector3 goal = target.transform.position + Vector3.up * 0.8f;
            flames[i].position = Vector3.MoveTowards(flames[i].position, goal, SylvMoveRules.FireSpeed * Time.deltaTime);
            if (!IsServer || (flames[i].position - goal).sqrMagnitude > 0.04f) continue;
            target.TakeDamageRPC(SylvMoveRules.FireDamage(Actor.Pokemon.NetworkObjectId, !hitTargets.Add(target.NetworkObjectId)));
            if (target.CurrentHp > 0)
            {
                short amount = 15;
                foreach (StatChange stat in target.StatChanges)
                    if (stat.ID == SylvMoveRules.FireDebuff) amount += stat.Amount;
                RefreshStat(target, new StatChange((short)Mathf.Min(60, amount), Stat.SpAttack, 1, true, false, true, SylvMoveRules.FireDebuff));
            }
            Actor.MovesController.ReduceMoveCooldownByFraction(MoveType.MoveA, 0.12f);
            SpendRPC(i);
        }
    }
}
