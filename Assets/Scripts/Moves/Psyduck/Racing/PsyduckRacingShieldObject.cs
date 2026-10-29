using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PsyduckRacingShieldObject : NetworkBehaviour
{
    private static readonly Dictionary<PlayerManager, PsyduckRacingShieldObject> shields = new Dictionary<PlayerManager, PsyduckRacingShieldObject>();
    private PlayerManager assignedPlayer;
    private float timer = 12f;
    private bool consumed;

    public void Initialize(ulong assignedPlayerID)
    {
        if (!IsServer || assignedPlayer != null) return;
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(assignedPlayerID, out var owner)
            || !owner.TryGetComponent(out assignedPlayer) || assignedPlayer.Pokemon == null)
        {
            NetworkObject.Despawn(true);
            return;
        }
        if (shields.TryGetValue(assignedPlayer, out var previous) && previous != null) previous.Consume();
        shields[assignedPlayer] = this;
        transform.position = assignedPlayer.transform.position;
        assignedPlayer.Pokemon.BeforeStatusEffect += BeforeStatusEffect;
        assignedPlayer.Pokemon.BeforeStatChange += BeforeStatChange;
    }

    public static bool TryBlock(PlayerManager player)
    {
        if (!shields.TryGetValue(player, out var shield) || shield == null || !shield.IsSpawned || shield.consumed) return false;
        shield.Consume();
        return true;
    }

    private bool BeforeStatusEffect(StatusEffect status)
    {
        if (consumed || (!status.IsNegativeStatus() && !(status.Type == StatusType.Scriptable && status.ID == 20))) return true;
        Consume();
        return false;
    }

    private StatChange? BeforeStatChange(StatChange stat)
    {
        if (consumed || stat.AffectedStat != Stat.Speed || stat.IsBuff) return stat;
        Consume();
        return null;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || consumed) return;
        timer -= Time.deltaTime;
        if (assignedPlayer == null || !assignedPlayer.IsSpawned || timer <= 0)
        {
            Consume();
            return;
        }
        transform.position = assignedPlayer.transform.position;
    }

    private void Consume()
    {
        if (consumed) return;
        consumed = true;
        Cleanup();
        if (IsServer && IsSpawned) NetworkObject.Despawn(true);
    }

    private void Cleanup()
    {
        if (assignedPlayer == null) return;
        if (assignedPlayer.Pokemon != null)
        {
            assignedPlayer.Pokemon.BeforeStatusEffect -= BeforeStatusEffect;
            assignedPlayer.Pokemon.BeforeStatChange -= BeforeStatChange;
        }
        if (shields.TryGetValue(assignedPlayer, out var current) && current == this) shields.Remove(assignedPlayer);
    }

    public override void OnNetworkDespawn()
    {
        Cleanup();
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        Cleanup();
        base.OnDestroy();
    }
}
