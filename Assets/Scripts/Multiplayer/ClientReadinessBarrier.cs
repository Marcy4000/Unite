using System;
using System.Collections.Generic;
using System.Linq;

namespace Unite.Multiplayer
{
    [Flags]
    public enum ClientLoadStage
    {
        None = 0,
        Map = 1,
        Pokemon = 2,
        Ready = 4
    }

    /// <summary>
    /// A fixed set of expected clients. Readiness messages cannot add clients or
    /// revive a disconnected client's entry. The caller supplies a deadline.
    /// </summary>
    public sealed class ClientReadinessBarrier
    {
        private readonly Dictionary<ulong, ClientLoadStage> clients = new Dictionary<ulong, ClientLoadStage>();
        private readonly ClientLoadStage required;

        public ClientReadinessBarrier(ClientLoadStage required)
        {
            if (required == ClientLoadStage.None) throw new ArgumentOutOfRangeException(nameof(required));
            this.required = required;
        }

        public bool IsReady => clients.Count > 0 && clients.Values.All(stage => (stage & required) == required);
        public ulong[] PendingClients => clients.Where(pair => (pair.Value & required) != required)
            .Select(pair => pair.Key).OrderBy(id => id).ToArray();

        public void Reset(IEnumerable<ulong> clientIds)
        {
            clients.Clear();
            foreach (var clientId in clientIds) clients[clientId] = ClientLoadStage.None;
        }

        public bool MarkReady(ulong clientId, ClientLoadStage stage)
        {
            if (stage == ClientLoadStage.None || (stage & ~required) != 0 || !clients.ContainsKey(clientId)) return false;
            clients[clientId] |= stage;
            return true;
        }

        public bool MarkReady(ulong senderClientId, ulong claimedClientId, ClientLoadStage stage)
        {
            return senderClientId == claimedClientId && MarkReady(senderClientId, stage);
        }

        public void Remove(ulong clientId) => clients.Remove(clientId);
    }
}
