using System;
using System.Linq;
using Unite.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct LobbySeat : INetworkSerializable, IEquatable<LobbySeat>
{
    public FixedString64Bytes PlayerId;
    public Team Team;
    public short Position;
    public bool Spectator;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref Team);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Spectator);
    }
    public bool Equals(LobbySeat other) => PlayerId.Equals(other.PlayerId) && Team == other.Team &&
        Position == other.Position && Spectator == other.Spectator;
}

public partial class PlayerNetworkManager
{
    // The host coordinator survives both selection and gameplay. Never trust
    // owner-written UGS role changes after the match roster has been frozen.
    private NetworkList<LobbySeat> lobbySeats = new NetworkList<LobbySeat>();
    private bool composingSeats;
    private bool seatsFrozen;
    public bool IsSpectator => LocalPlayer?.IsSpectator == true;

    private void OnSeatsChanged(NetworkListEvent<LobbySeat> change) => participantViewDirty = true;

    public bool TryGetSeat(string id, out LobbySeat seat)
    {
        foreach (var candidate in lobbySeats)
            if (candidate.PlayerId.ToString() == id) { seat = candidate; return true; }
        seat = default;
        return false;
    }

    public void FreezeLobbySeats() { if (IsServer) seatsFrozen = true; }

    public void ComposeLobbySeats(GameSessionView view)
    {
        var lobby = LobbyController.Instance;
        if (!IsBotRoster || composingSeats || lobby == null || !lobby.IsCustomLobby()) return;
        composingSeats = true;
        try
        {
            if (IsServer && SceneManager.GetActiveScene().name == "LobbyScene" && !view.IsLocked)
            {
                seatsFrozen = false;
                for (int i = lobbySeats.Count - 1; i >= 0; i--)
                    if (!view.Players.Any(p => !p.IsBot && p.Id == lobbySeats[i].PlayerId.ToString())) lobbySeats.RemoveAt(i);
                var map = CharactersList.Instance.GetCurrentLobbyMap();
                // Remove invalid map seats before finding replacements. Spectator
                // seats are independent of map team count and team size.
                for (int i = lobbySeats.Count - 1; i >= 0; i--)
                    if (!lobbySeats[i].Spectator && (!map.availableTeams.Contains(lobbySeats[i].Team) ||
                        lobbySeats[i].Position < 0 || lobbySeats[i].Position >= map.maxTeamSize)) lobbySeats.RemoveAt(i);
                foreach (var player in view.Players.Where(p => !p.IsBot).OrderBy(p => p.Id, StringComparer.Ordinal))
                {
                    if (TryGetSeat(player.Id, out _)) continue;
                    short position = 0;
                    try { position = NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value); } catch (Exception) { }
                    var requested = new LobbySeat { PlayerId = player.Id, Team = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value),
                        Position = position, Spectator = player.IsSpectator };
                    if (ValidSeat(requested, view) && SeatFree(requested, view)) { lobbySeats.Add(requested); continue; }
                    bool assigned = false;
                    foreach (var team in map.availableTeams)
                    {
                        for (short slot = 0; slot < map.maxTeamSize; slot++)
                        {
                            var candidate = new LobbySeat { PlayerId = player.Id, Team = team, Position = slot };
                            if (!SeatFree(candidate, view)) continue;
                            lobbySeats.Add(candidate); assigned = true; break;
                        }
                        if (assigned) break;
                    }
                    if (!assigned)
                        for (short slot = 0; slot < SpectatorSlotRules.Capacity; slot++)
                        {
                            var candidate = new LobbySeat { PlayerId = player.Id, Spectator = true, Position = slot };
                            if (!SeatFree(candidate, view)) continue;
                            lobbySeats.Add(candidate); assigned = true; break;
                        }
                    // Bots occupy combat seats, not UGS member slots. If both
                    // combat and observer seats look full because of bots, a new
                    // human must replace that bot roster rather than become an
                    // unseated sixth spectator or duplicate a combatant slot.
                    if (!assigned && bots.Count > 0)
                    {
                        ClearBots();
                        foreach (var team in map.availableTeams)
                        {
                            for (short slot = 0; slot < map.maxTeamSize; slot++)
                            {
                                var candidate = new LobbySeat { PlayerId = player.Id, Team = team, Position = slot };
                                if (!SeatFree(candidate, view)) continue;
                                lobbySeats.Add(candidate); assigned = true; break;
                            }
                            if (assigned) break;
                        }
                    }
                }
            }
            foreach (var player in view.Players.Where(p => !p.IsBot))
            {
                if (!TryGetSeat(player.Id, out var seat)) continue;
                string team = seat.Spectator ? SpectatorSlotRules.TeamValue : seat.Team.ToString();
                player.Data["PlayerTeam"] = new PlayerProperty(team, VisibilityPropertyOptions.Member);
                player.Data["PlayerPos"] = new PlayerProperty(NumberEncoder.ToBase64(seat.Position), VisibilityPropertyOptions.Member);
            }
        }
        finally { composingSeats = false; }
    }

    private bool ValidSeat(LobbySeat seat, GameSessionView view)
    {
        if (seat.Spectator) return SpectatorSlotRules.IsValidSlot(seat.Position);
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        return map.availableTeams.Contains(seat.Team) && seat.Position >= 0 && seat.Position < map.maxTeamSize;
    }

    private bool SeatFree(LobbySeat seat, GameSessionView view)
    {
        foreach (var other in lobbySeats)
            if (!other.PlayerId.Equals(seat.PlayerId) && other.Spectator == seat.Spectator && other.Position == seat.Position &&
                (seat.Spectator || other.Team == seat.Team)) return false;
        // ComposeParticipants clears the previous map's bots immediately after
        // this pass. Those obsolete bot slots must not force resized humans
        // into spectator seats while the new map is being reconciled.
        if (previousMap != null && view.Data.TryGetValue("SelectedMap", out var selectedMap) && previousMap != selectedMap.Value) return true;
        return seat.Spectator || !BotEntries().Any(bot => bot.Team == seat.Team && bot.Position == seat.Position);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestLobbySeatRpc(Team team, short position, bool spectator, RpcParams rpcParams = default)
    {
        var lobby = LobbyController.Instance;
        if (!IsBotRoster || seatsFrozen || lobby?.Lobby == null || !lobby.IsCustomLobby() || lobby.Lobby.IsLocked ||
            SceneManager.GetActiveScene().name != "LobbyScene") return;
        var manager = lobby.PlayerNetworkManagers.Find(m => m != null && m.IsSpawned && m.OwnerClientId == rpcParams.Receive.SenderClientId);
        var player = manager?.LocalPlayer;
        if (player == null || player.IsBot) return;
        var seat = new LobbySeat { PlayerId = player.Id, Team = team, Position = position, Spectator = spectator };
        if (!ValidSeat(seat, lobby.Lobby) || !SeatFree(seat, lobby.Lobby)) return;
        for (int i = 0; i < lobbySeats.Count; i++)
            if (lobbySeats[i].PlayerId.Equals(seat.PlayerId))
            {
                lobbySeats[i] = seat;
                lobby.RefreshParticipants();
                return;
            }
        lobbySeats.Add(seat);
        lobby.RefreshParticipants();
    }
}
