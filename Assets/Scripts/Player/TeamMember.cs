using Unity.Netcode;

public enum Team : byte { Neutral, Blue, Orange }

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
        team = team.ToLower();

        switch (team)
        {
            case "blue":
                return Team.Blue;
            case "orange":
                return Team.Orange;
            default:
                return Team.Neutral;
        }
    }
}
