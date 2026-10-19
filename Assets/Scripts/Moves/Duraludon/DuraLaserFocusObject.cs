using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DuraLaserFocusObject : DuraMoveObject
{
    [SerializeField] private GameObject shielding, focus;
    private bool empowered;
    private int remaining = 3, lastAttack = -1;
    private readonly HashSet<ulong> hitTargets = new HashSet<ulong>();
    private static readonly Dictionary<ulong, DuraLaserFocusObject> activeFocus = new Dictionary<ulong, DuraLaserFocusObject>();
    private ulong actorId;

    protected override void BeginPresentation()
    {
        shielding.SetActive(true);
        focus.SetActive(false);
    }

    private void LateUpdate()
    {
        if (Actor != null) transform.position = Actor.transform.position;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowFocusRPC()
    {
        if (!IsClient) return;
        shielding.SetActive(false);
        focus.SetActive(true);
    }

    protected override IEnumerator Execute()
    {
        actorId = Actor.NetworkObjectId;
        if (activeFocus.TryGetValue(actorId, out var previous) && previous != null && previous != this) previous.Despawn();
        activeFocus[actorId] = this;
        Actor.Pokemon.AddStatChange(new StatChange(20, Stat.DamageReduction, 0.6f, true, true, false, 0));
        Actor.Pokemon.OnDamageDealt += OnDamageDealt;
        yield return new WaitForSeconds(0.6f);
        empowered = true;
        ShowFocusRPC();
        float expires = Time.time + 8f;
        while (remaining > 0 && Time.time < expires) yield return null;
        empowered = false;
    }

    private void OnDamageDealt(ulong targetId, DamageInfo damage)
    {
        if (!empowered || !IsServer || Combat == null
            || (damage.proprieties & DamageProprieties.IsBasicAttack) == 0
            || (damage.proprieties & (DamageProprieties.IsMuscleBand | DamageProprieties.Unused_2)) != 0) return;
        if (lastAttack != Combat.AttackSerial)
        {
            if (remaining == 0) return;
            remaining--;
            lastAttack = Combat.AttackSerial;
            hitTargets.Clear();
        }
        if (hitTargets.Add(targetId) && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var target)
            && target.TryGetComponent(out Pokemon pokemon))
            pokemon.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 0.27f, 3, 60, DamageType.Physical, DamageProprieties.Unused_2));
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (Actor != null) Actor.Pokemon.OnDamageDealt -= OnDamageDealt;
            if (activeFocus.TryGetValue(actorId, out var current) && current == this) activeFocus.Remove(actorId);
        }
        base.OnNetworkDespawn();
    }
}
