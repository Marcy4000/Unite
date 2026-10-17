using System;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;

namespace Unite.Multiplayer
{
    public static class SessionCapacityAdapter
    {
        // MPS 2.4 exposes MaxPlayers only as a read-only session property.
        // Never write session/network metadata through this compatibility path.
        public static async Task SetCapacityAsync(ISession session, int maxPlayers)
        {
            if (!session.IsHost)
                throw new InvalidOperationException("Only the session host can change capacity.");
            if (maxPlayers < session.PlayerCount)
                throw new InvalidOperationException("The new capacity cannot exclude current players.");
            if (session.MaxPlayers == maxPlayers)
                return;

            await LobbyService.Instance.UpdateLobbyAsync(session.Id, new UpdateLobbyOptions { MaxPlayers = maxPlayers });
            await session.RefreshAsync();
        }
    }
}
