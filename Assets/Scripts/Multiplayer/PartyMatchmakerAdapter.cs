using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

namespace Unite.Multiplayer
{
    public sealed class PartyMatchmakerAdapter
    {
        // MatchmakeSessionAsync in MPS 2.4 submits one player, not a party.
        // Keep only the multi-player ticket workflow here; Sessions owns all networking.
        public async Task<string> FindMatchAsync(List<Player> players, string queueName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ticket = await MatchmakerService.Instance.CreateTicketAsync(players, new CreateTicketOptions(queueName,
                new Dictionary<string, object> { [SessionCompatibility.PropertyName] = SessionCompatibility.Current }));
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var response = await MatchmakerService.Instance.GetTicketAsync(ticket.Id);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!(response.Value is MatchIdAssignment assignment))
                        throw new InvalidOperationException("The party queue must use peer-to-peer Match ID assignments.");

                    switch (assignment.Status)
                    {
                        case MatchIdAssignment.StatusOptions.Found:
                            if (string.IsNullOrEmpty(assignment.MatchId))
                                throw new InvalidOperationException("Matchmaker returned an empty match ID.");
                            return assignment.MatchId;
                        case MatchIdAssignment.StatusOptions.Timeout:
                        case MatchIdAssignment.StatusOptions.Failed:
                            throw new InvalidOperationException($"Matchmaking {assignment.Status}: {assignment.Message}");
                    }
                    await Task.Delay(1100, cancellationToken);
                }
            }
            finally
            {
                try
                {
                    await MatchmakerService.Instance.DeleteTicketAsync(ticket.Id);
                }
                catch (Exception exception)
                {
                    // A matched or expired ticket may already have been removed.
                    // Do not replace a successful assignment or cancellation with a cleanup error.
                    Debug.LogWarning($"Could not clean up party matchmaking ticket {ticket.Id}: {exception.Message}");
                }
            }
        }

        public async Task<StoredMatchmakingResults> GetResultsAsync(string matchId, CancellationToken cancellationToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var results = await MatchmakerService.Instance.GetMatchmakingResultsAsync(matchId);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (results?.MatchProperties?.Players == null || results.MatchProperties.Teams == null)
                        throw new InvalidOperationException("Matchmaker results do not contain the roster and teams.");
                    return results;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) when (attempt < 3)
                {
                    await Task.Delay(1100, cancellationToken);
                }
            }
        }
    }
}
