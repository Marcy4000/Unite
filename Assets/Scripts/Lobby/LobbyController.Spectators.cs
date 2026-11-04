using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unite.Multiplayer;
using Unity.Services.Multiplayer;

public partial class LobbyController
{
    private string persistedSeat;
    private string persistedSeatSession;
    public bool IsLocalSpectator => localPlayer?.IsSpectator == true;
    public Team ResultsPerspectiveTeam => IsLocalSpectator ? Team.Blue : GetLocalPlayerTeam();
    public IEnumerable<GameSessionPlayer> Combatants => partyLobby == null
        ? Enumerable.Empty<GameSessionPlayer>() : partyLobby.Players.Where(player => !player.IsSpectator);

    private static int GetSessionCapacity(MapInfo map, LobbyType type) =>
        SpectatorSlotRules.SessionCapacity(type == LobbyType.Standards ? map.maxTeamSize : map.maxTeamSize * map.availableTeams.Count,
            type == LobbyType.Custom);

    public void JoinSpectatorSlot(short slot)
    {
        if (IsCustomLobby() && SpectatorSlotRules.IsValidSlot(slot)) BotRoster?.RequestLobbySeatRpc(Team.Neutral, slot, true);
    }

    public void PersistAuthoritativeSeat(string team, short position)
    {
        if (session == null) return;
        string key = team + ":" + position;
        if (persistedSeatSession == session.Id && persistedSeat == key) return;
        persistedSeatSession = session.Id;
        persistedSeat = key;
        ObserveTask(PersistSeatMirrorAsync(session, key, team, position));
    }

    private async Task PersistSeatMirrorAsync(ISession targetSession, string key, string team, short position)
    {
        // Do not recursively rebuild the shared session view from inside its
        // own composition pass. Coalesce rapid seat changes before taking the
        // existing player write gate; the final service write must be newest.
        await Task.Yield();
        if (session != targetSession || persistedSeat != key || persistedSeatSession != targetSession.Id) return;
        try
        {
            await SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
            {
                ["PlayerTeam"] = MemberPlayerProperty(team),
                ["PlayerPos"] = MemberPlayerProperty(NumberEncoder.ToBase64(position))
            });
        }
        catch
        {
            if (persistedSeat == key && persistedSeatSession == targetSession.Id) persistedSeat = null;
            throw;
        }
    }

    private bool HavePublishedSeatRoles() => session == null || session.Players.All(member =>
    {
        var player = GetPlayerByID(member.Id);
        return player != null && member.Properties.TryGetValue("PlayerTeam", out var role) &&
            player.Data.TryGetValue("PlayerTeam", out var accepted) && role.Value == accepted.Value;
    });

    private bool ValidateCombatantRosterForStart()
    {
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        var players = Combatants.ToArray();
        if (players.Length == 0 || players.Any(player => !map.availableTeams.Contains(TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value)))) return false;
        if (players.Select(player => player.Data["PlayerTeam"].Value + ":" + player.Data["PlayerPos"].Value).Distinct().Count() != players.Length) return false;
        foreach (var player in players)
        {
            short position = NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value);
            if (position < 0 || position >= map.maxTeamSize) return false;
        }
        var counts = map.availableTeams.Select(team => players.Count(player => player.Data["PlayerTeam"].Value == team.ToString())).ToArray();
        return map.startRestriction == StartRestriction.None ||
            (map.startRestriction == StartRestriction.SameTeamSizes && counts.Distinct().Count() == 1) ||
            (map.startRestriction == StartRestriction.FullTeams && counts.All(count => count == map.maxTeamSize));
    }
}
