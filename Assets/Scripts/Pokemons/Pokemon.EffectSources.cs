using System;
using Unity.Netcode;

public partial class Pokemon
{
    // Opt-in attribution for server-authored effects; owner-authored legacy RPCs
    // can be attributed to their sender without guessing from the last attacker.
    public event Func<Pokemon, DamageInfo, float> OutgoingDamageMultiplier;
    private ulong? EffectSender(RpcParams context)
    {
        ulong sender = context.Receive.SenderClientId;
        if (NetworkManager == null || sender == Unity.Netcode.NetworkManager.ServerClientId) return null;
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
            if (obj.OwnerClientId == sender && obj.TryGetComponent(out PlayerManager player)) return player.NetworkObjectId;
        return null;
    }
    private float OutgoingMultiplier(Pokemon target, DamageInfo damage)
    {
        float result = 1;
        if (OutgoingDamageMultiplier != null)
            foreach (Func<Pokemon, DamageInfo, float> modifier in OutgoingDamageMultiplier.GetInvocationList())
                result *= UnityEngine.Mathf.Max(0, modifier(target, damage));
        return result;
    }
}
