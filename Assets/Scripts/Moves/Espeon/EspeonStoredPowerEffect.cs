using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EspeonStoredPowerEffect : EspeonMoveEffect
{
    [SerializeField] private Transform[] projectiles;
    [SerializeField] private SphereCollider acquisitionArea;
    private readonly Dictionary<Pokemon, int> hits = new Dictionary<Pokemon, int>();
    private Pokemon[] recipients;
    private bool[] flying;
    private float[] launched;
    private int active;
    private bool enhanced;
    protected override void BeginPresentation()
    {
        recipients = new Pokemon[projectiles.Length]; flying = new bool[projectiles.Length]; launched = new float[projectiles.Length];
        foreach (var bolt in projectiles) bolt.gameObject.SetActive(false);
    }
    public static List<Pokemon> TargetPool(PlayerManager actor, Pokemon marked)
    {
        var players = new List<Pokemon>(); var wild = new List<Pokemon>();
        float squared = EspeonMoveRules.StoredRange * EspeonMoveRules.StoredRange;
        if (EspeonMoveRules.CanHit(actor, marked) && EspeonMoveRules.Flat(marked.transform.position - actor.transform.position).sqrMagnitude <= squared)
            return new List<Pokemon> { marked };
        foreach (var target in CombatTargeting.SphereTargets(actor.transform.position + Vector3.up * 0.8f, EspeonMoveRules.StoredRange))
        {
            if (!EspeonMoveRules.CanHit(actor, target, true) || EspeonMoveRules.Flat(target.transform.position - actor.transform.position).sqrMagnitude > squared) continue;
            (target.Type == PokemonType.Player ? players : wild).Add(target);
        }
        return players.Count > 0 ? players : wild;
    }
    public static bool HasTargets(PlayerManager actor) => actor != null
        && TargetPool(actor, actor.MovesController.EspeonMarkedTarget).Count > 0;
    protected override IEnumerator Execute()
    {
        // Snapshot the highest-priority pool. A player absorbing the cast never
        // spills its remaining blasts onto wild Pokémon if it dies mid-volley.
        // Also guard direct spawns and targets lost while the prefab was loading.
        List<Pokemon> pool = TargetPool(Actor, Combat.MarkedTarget);
        if (pool.Count == 0) yield break;
        LockActions(false);
        PlayAnimation(EspeonMoveRules.Animation(Actor, "spell2b"), 0.45f);
        PrepareRPC(Empowered);
        if (acquisitionArea != null) acquisitionArea.radius = EspeonMoveRules.StoredRange;
        Combat.GrantStoredHaste();
        int count = Mathf.Min(BlastCount, projectiles.Length);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            pool.RemoveAll(p => !EspeonMoveRules.CanHit(Actor, p));
            if (pool.Count == 0) break;
            Pokemon recipient = pool[Random.Range(0, pool.Count)];
            LaunchRPC(i, recipient.NetworkObjectId, Actor.transform.position + Vector3.up * 1.1f);
            yield return new WaitForSeconds(EspeonMoveRules.StoredInterval);
        }
        UnlockActions();
        while (active > 0) yield return null;
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PrepareRPC(bool empowered) { enhanced = empowered; }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LaunchRPC(int index, ulong targetId, Vector3 origin)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)) return;
        recipients[index] = obj.GetComponent<Pokemon>(); flying[index] = true; launched[index] = Time.time;
        projectiles[index].position = origin; projectiles[index].gameObject.SetActive(true); active++;
    }
    protected override void Update()
    {
        base.Update();
        if (Actor == null || flying == null) return;
        for (int i = 0; i < flying.Length; i++)
        {
            if (!flying[i]) continue;
            Pokemon recipient = recipients[i];
            if (!EspeonMoveRules.CanHit(Actor, recipient) || Time.time - launched[i] > 3)
            { if (IsServer) StopRPC(i); continue; }
            Vector3 goal = recipient.transform.position + Vector3.up * 0.8f;
            projectiles[i].position = Vector3.MoveTowards(projectiles[i].position, goal, EspeonMoveRules.StoredSpeed * Time.deltaTime);
            if (!IsServer || (projectiles[i].position - goal).sqrMagnitude > 0.04f) continue;
            hits.TryGetValue(recipient, out int count);
            bool low = recipient.CurrentHp * 2 < recipient.GetMaxHp();
            DamageInfo damage = EspeonMoveRules.Stored(Actor.NetworkObjectId, count > 0);
            if (Upgraded && low) { damage.ratio += 0.26f; damage.slider += 5; damage.baseDmg += 135; }
            if (Hit(recipient, damage, true) > 0)
            {
                hits[recipient] = ++count;
                Slow(recipient, Mathf.Min(50, count * 10), 2);
                if (enhanced && EspeonMoveRules.Living(Actor.Pokemon)) Actor.Pokemon.HealDamageRPC(150, Actor.NetworkObjectId);
            }
            StopRPC(i);
        }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void StopRPC(int index) { if (flying[index]) active--; flying[index] = false; projectiles[index].gameObject.SetActive(false); }
}
