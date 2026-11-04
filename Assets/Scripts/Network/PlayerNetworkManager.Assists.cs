using Unity.Netcode;

public partial class PlayerNetworkManager
{
    // Other legacy statistics are owner-written. Assists are resolved and stored
    // on the server so a client round trip cannot lose the last KO at match end.
    private NetworkVariable<ushort> serverAssists = new NetworkVariable<ushort>();

    private PlayerStats WithAuthoritativeAssists(PlayerStats stats)
    {
        stats.assists = serverAssists.Value;
        return stats;
    }

    internal void RecordAssistServer()
    {
        if (IsServer && serverAssists.Value < ushort.MaxValue) serverAssists.Value++;
    }

    private void HandleKnockoutAwarded(KnockoutResult result)
    {
        if (playerManager != null && result.Killer != playerManager.Pokemon) RecordAssistServer();
    }

    private void HandleAssistCountChanged(ushort previous, ushort updated)
    {
        HandlePlayerStatsChange(playerStats.Value, playerStats.Value);
    }
}
