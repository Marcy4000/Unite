using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct ScoreInfo : INetworkSerializable
{
    public short scoredPoints;
    public ulong scorerId;

    public ScoreInfo(short scoredPoints, ulong scorerId)
    {
        this.scoredPoints = scoredPoints;
        this.scorerId = scorerId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref scoredPoints);
        serializer.SerializeValue(ref scorerId);
    }
}
