using Unity.Netcode;
using UnityEngine;

public struct UniteRoyaleGameResults : INetworkSerializable
{
    public UniteRoyalePlayerResult[] PlayerStats;

    public UniteRoyaleGameResults(UniteRoyalePlayerResult[] playerStats)
    {
        PlayerStats = playerStats;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerStats);
    }
}

public struct UniteRoyalePlayerResult : INetworkSerializable
{
    public PlayerStats PlayerStats;
    public byte position;

    public UniteRoyalePlayerResult(PlayerStats playerStats, byte position)
    {
        PlayerStats = playerStats;
        this.position = position;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerStats);
        serializer.SerializeValue(ref position);
    }
}