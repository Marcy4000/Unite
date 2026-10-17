using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Matchmaker.Models;

namespace Unite.Multiplayer
{
    public sealed class MatchRoster
    {
        public sealed class Assignment
        {
            public string Team { get; }
            public short Position { get; }

            internal Assignment(string team, short position)
            {
                Team = team;
                Position = position;
            }
        }

        public IReadOnlyDictionary<string, Assignment> Players { get; }

        public MatchRoster(StoredMatchProperties match, int maxTeamSize)
        {
            if (match?.Players == null || match.Teams == null || match.Teams.Count != 2)
                throw new InvalidOperationException("StandardsQueue must return a player roster and exactly two teams.");

            var expected = new HashSet<string>(match.Players.Select(player => player.Id));
            if (expected.Count == 0 || expected.Count != match.Players.Count)
                throw new InvalidOperationException("Matchmaker returned an empty or duplicate player roster.");
            var teams = match.Teams.OrderBy(team => team.TeamName, StringComparer.Ordinal)
                .ThenBy(team => team.TeamId, StringComparer.Ordinal).ToArray();
            var namedTeams = teams.Any(team => string.Equals(team.TeamName, "Blue", StringComparison.OrdinalIgnoreCase)) &&
                teams.Any(team => string.Equals(team.TeamName, "Orange", StringComparison.OrdinalIgnoreCase));
            var assignments = new Dictionary<string, Assignment>();
            for (var index = 0; index < teams.Length; index++)
            {
                var team = teams[index];
                if (team.PlayerIds == null || team.PlayerIds.Count == 0 || team.PlayerIds.Count > maxTeamSize)
                    throw new InvalidOperationException("Matchmaker returned an invalid team size for the selected map.");
                var teamName = namedTeams
                    ? string.Equals(team.TeamName, "Blue", StringComparison.OrdinalIgnoreCase) ? "Blue" : "Orange"
                    : index == 0 ? "Blue" : "Orange";
                var playerIds = team.PlayerIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
                for (short position = 0; position < playerIds.Length; position++)
                {
                    var playerId = playerIds[position];
                    if (!expected.Contains(playerId) || assignments.ContainsKey(playerId))
                        throw new InvalidOperationException("Matchmaker teams contain an unknown or duplicate player.");
                    assignments.Add(playerId, new Assignment(teamName, position));
                }
            }
            if (assignments.Count != expected.Count)
                throw new InvalidOperationException("Some match players have no assigned team.");
            Players = assignments;
        }
    }
}
