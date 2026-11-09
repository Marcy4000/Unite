using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class UmbreonWishEffect : UmbreonMoveEffect
{
    private ushort protection;
    private Pokemon recipient;
    private double deadline;
    public override bool PersistsAfterCasterDeath => true;
    protected override IEnumerator Execute()
    {
        if (!UmbreonMoveRules.Living(Target) || !Target.TeamMember.IsOnSameTeam(Actor.CurrentTeam)) yield break;
        protection = UmbreonEffectIds.Acquire(); deadline = NetworkManager.ServerTime.Time + UmbreonMoveRules.WishDelay;
        SetRecipient(Target);
        PlayAnimation(UmbreonMoveRules.Animation(Actor, "spell2a"), 0.35f);
        while (NetworkManager.ServerTime.Time < deadline)
        {
            if (recipient == null) yield break;
            if (!UmbreonMoveRules.Living(recipient))
            {
                var next = UmbreonMoveRules.LowestAlly(NetworkManager.SpawnManager.SpawnedObjects.Values.Select(o => o.GetComponent<Pokemon>()),
                    recipient, recipient.transform.position, UmbreonMoveRules.WishTransferRadius);
                if (next == null) yield break;
                SetRecipient(next);
            }
            yield return null;
        }
        if (UmbreonMoveRules.Living(recipient))
        {
            recipient.HealDamageRPC(UmbreonMoveRules.WishHeal(Actor.Pokemon.GetAttack(), Actor.Pokemon.CurrentLevel, Upgraded), Actor.NetworkObjectId);
            Visual("WishBloom", recipient.transform.position + Vector3.up, 1, 0.6f);
        }
    }
    private void SetRecipient(Pokemon target)
    {
        if (recipient != null)
        {
            recipient.OnDeath -= RecipientDied;
            if (recipient.IsSpawned) recipient.RemoveAllStatChangeWithIDRPC(protection);
        }
        recipient = target;
        recipient.OnDeath += RecipientDied;
        float remaining = Mathf.Max(0, (float)(deadline - NetworkManager.ServerTime.Time));
        recipient.AddStatChange(new StatChange((short)(Upgraded ? 30 : 20), Stat.DamageReduction, remaining, true, true, false, protection).WithSource(Actor.NetworkObjectId));
        RecipientRPC(target.NetworkObjectId);
    }
    private void RecipientDied(DamageInfo damage)
    {
        if (!IsServer || recipient == null) return;
        var next = UmbreonMoveRules.LowestAlly(NetworkManager.SpawnManager.SpawnedObjects.Values.Select(o => o.GetComponent<Pokemon>()),
            recipient, recipient.transform.position, UmbreonMoveRules.WishTransferRadius);
        if (next != null) SetRecipient(next);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void RecipientRPC(ulong id)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)) recipient = obj.GetComponent<Pokemon>();
    }
    protected override void Update()
    {
        base.Update();
        if (recipient != null) transform.position = recipient.transform.position;
    }
    protected override void Cleanup()
    {
        if (IsServer)
        {
            if (recipient != null) recipient.OnDeath -= RecipientDied;
            if (recipient != null && recipient.IsSpawned) recipient.RemoveAllStatChangeWithIDRPC(protection);
            UmbreonEffectIds.Release(protection, UmbreonMoveRules.WishDelay);
        }
        base.Cleanup();
    }
}
