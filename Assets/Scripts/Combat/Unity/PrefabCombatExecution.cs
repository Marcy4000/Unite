using System.Collections;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;

public sealed class PrefabCombatExecution : CombatExecution
{
    private readonly CombatActor combat;
    private GameObject prefab;
    private PrefabMoveEffect effect;
    private bool followUpReady;
    public PrefabMoveEffect Effect => effect;
    public override bool FollowUpReady => followUpReady;

    public PrefabCombatExecution(CombatExecutionContext context, CombatActor combat) : base(context) { this.combat = combat; }

    public override IEnumerator Prepare()
    {
        Prepared = false;
        yield return combat.Spawner.Load(Context.Definition.PrefabPath, Context.Scope, loaded => prefab = loaded);
        Prepared = prefab != null && prefab.GetComponent<NetworkObject>() != null && prefab.GetComponent<PrefabMoveEffect>() != null;
    }

    public override void Start()
    {
        Vector3 direction = Context.Direction.sqrMagnitude > 0.001f ? Context.Direction : Actor.transform.forward;
        GameObject instance = Object.Instantiate(prefab, Actor.transform.position + Vector3.up * 0.8f, Quaternion.LookRotation(direction));
        effect = instance.GetComponent<PrefabMoveEffect>();
        effect.MoveId = Context.MoveId;
        effect.CastSequence = unchecked((int)Context.CastId);
        combat.Character.ConfigureEffect(effect, Context);
        effect.Completed += OnCompleted;
        effect.RecastReady += OnRecastReady;
        instance.GetComponent<NetworkObject>().Spawn(true);
        effect.Initialize(Actor, Context.Direction, Context.Target, Context.Upgraded);
    }

    private void OnCompleted(PrefabMoveEffect completed) => Context.Finish(CastOutcome.Completed);
    private void OnRecastReady() { followUpReady = true; combat.PublishSlot(Context.Slot); }

    public override bool TryRecast(Vector3 direction, ulong targetId)
    {
        if (effect == null || !effect.IsSpawned || !effect.TryRecast(direction, targetId)) return false;
        followUpReady = false;
        return true;
    }

    public override void Stop(CastOutcome reason)
    {
        followUpReady = false;
        if (effect == null) return;
        effect.Completed -= OnCompleted;
        effect.RecastReady -= OnRecastReady;
        effect.Despawn();
        effect = null;
    }
}
