using System;
using System.Collections.Generic;

// The server and each allied client keep separate rosters. List order is arrival
// order, not lobby order or the order in which Pokemon portraits become available.
public sealed class LaneSelectionRoster
{
    public struct Choice
    {
        public string PlayerId;
        public string LaneId;

        public Choice(string playerId, string laneId)
        {
            PlayerId = playerId;
            LaneId = laneId;
        }
    }

    private readonly List<Choice> choices = new List<Choice>();
    public IReadOnlyList<Choice> Choices => choices;

    public string GetLane(string playerId)
    {
        foreach (var choice in choices)
            if (choice.PlayerId == playerId) return choice.LaneId;
        return string.Empty;
    }

    public bool SetLane(string playerId, string laneId)
    {
        if (string.IsNullOrEmpty(playerId)) return false;
        laneId = laneId ?? string.Empty;
        if (GetLane(playerId) == laneId) return false;
        choices.RemoveAll(choice => choice.PlayerId == playerId);
        if (laneId.Length > 0) choices.Add(new Choice(playerId, laneId));
        return true;
    }

    public bool RemoveWhere(Predicate<Choice> predicate) => choices.RemoveAll(predicate) > 0;

    public void Replace(string[] playerIds, string[] laneIds)
    {
        choices.Clear();
        for (int i = 0; i < playerIds.Length; i++) SetLane(playerIds[i], laneIds[i]);
    }

    public void Clear() => choices.Clear();
}
