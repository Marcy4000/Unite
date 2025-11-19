using System;
using Unity.Netcode;

public enum Team : byte
{
    Neutral,
    Blue,
    Orange,
    Green,
    Red,
    Purple,
    Yellow,
    Pink,
    Cyan,
    Lime
}

[Serializable]
public struct TeamMember : INetworkSerializable
{
    private Team team;

    public Team Team
    {
        get => team;
        set => team = value;
    }

    public TeamMember(Team team)
    {
        this.team = team;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref team);
    }

    public bool IsOnSameTeam(TeamMember other)
    {
        return team == other.team;
    }

    public bool IsOnSameTeam(Team other)
    {
        return team == other;
    }

    public static Team GetTeamFromString(string team)
    {
        if (string.IsNullOrEmpty(team))
            return Team.Neutral;
        team = team.ToLowerInvariant();
        foreach (Team t in Enum.GetValues(typeof(Team)))
        {
            if (t == Team.Neutral) continue;
            if (t.ToString().ToLowerInvariant() == team)
                return t;
        }
        return Team.Neutral;
    }
}
