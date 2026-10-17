using System.Collections.Generic;
using System.Linq;
using Unity.Services.Multiplayer;

namespace Unite.Multiplayer
{
    // Game-owned views keep UI references stable across service updates. They also
    // let local settings exist before joining, without constructing Lobby models.
    public sealed class GameSessionPlayer
    {
        public string Id { get; }
        public Dictionary<string, PlayerProperty> Data { get; } = new Dictionary<string, PlayerProperty>();

        public GameSessionPlayer(string id)
        {
            Id = id;
        }

        public void Apply(IEnumerable<KeyValuePair<string, PlayerProperty>> properties)
        {
            var snapshot = properties.ToArray();
            Data.Clear();
            foreach (var property in snapshot)
                Data[property.Key] = property.Value;
        }
    }

    public sealed class GameSessionView
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string HostId { get; private set; }
        public string LobbyCode { get; private set; }
        public int MaxPlayers { get; private set; }
        public bool IsPrivate { get; private set; }
        public bool IsLocked { get; private set; }
        public List<GameSessionPlayer> Players { get; } = new List<GameSessionPlayer>();
        public Dictionary<string, SessionProperty> Data { get; } = new Dictionary<string, SessionProperty>();

        public void Apply(ISession session, GameSessionPlayer localPlayer,
            IReadOnlyDictionary<string, PlayerProperty> pendingPlayerProperties)
        {
            Id = session.Id;
            Name = session.Name;
            HostId = session.Host;
            LobbyCode = session.Code;
            MaxPlayers = session.MaxPlayers;
            IsPrivate = session.IsPrivate;
            IsLocked = session.IsLocked;
            Data.Clear();
            foreach (var property in session.Properties)
                Data[property.Key] = property.Value;

            var members = session.Players.Select(member =>
            {
                var snapshot = new GameSessionPlayer(member.Id);
                snapshot.Apply(member.Properties);
                return snapshot;
            });
            ApplyPlayers(members, localPlayer, pendingPlayerProperties);
        }

        public void ApplyPlayers(IEnumerable<GameSessionPlayer> members, GameSessionPlayer localPlayer,
            IReadOnlyDictionary<string, PlayerProperty> pendingPlayerProperties)
        {
            var snapshots = members.ToArray();
            var previousPlayers = Players.ToDictionary(player => player.Id);
            Players.Clear();
            foreach (var member in snapshots)
            {
                var player = member.Id == localPlayer.Id ? localPlayer :
                    previousPlayers.TryGetValue(member.Id, out var previous) ? previous : new GameSessionPlayer(member.Id);
                player.Apply(member.Data);
                if (player.Id == localPlayer.Id)
                    foreach (var pending in pendingPlayerProperties)
                        player.Data[pending.Key] = pending.Value;
                Players.Add(player);
            }
        }
    }
}
