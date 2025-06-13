using System;
using UnityEngine;

[System.Serializable]
public struct PlayerRankData
{
    public byte currentRankIndex;    // 0-5 for the 6 ranks (Beginner to Master)
    public byte currentClass;        // 1 = highest class in rank, N = lowest
    public ushort currentDiamonds;   // current diamonds
    public ushort totalWins;         // total ranked wins
    public ushort totalLosses;       // total ranked losses

    public PlayerRankData(byte rankIndex, byte classIndex, ushort diamonds, ushort wins, ushort losses)
    {
        currentRankIndex = rankIndex;
        currentClass = classIndex;
        currentDiamonds = diamonds;
        totalWins = wins;
        totalLosses = losses;
    }

    public static PlayerRankData GetDefault()
    {
        // Start at Beginner rank (index 0), lowest class, with 0 diamonds
        var beginnerRank = RankedSystemConfig.Instance.GetRankConfig(0);
        return new PlayerRankData(0, (byte)beginnerRank.totalClasses, 0, 0, 0);
    }

    public string Serialize()
    {
        byte[] data = new byte[9];
        data[0] = currentRankIndex;
        data[1] = currentClass;
        
        // Convert ushort to 2 bytes each
        byte[] diamondsBytes = BitConverter.GetBytes(currentDiamonds);
        byte[] winsBytes = BitConverter.GetBytes(totalWins);
        byte[] lossesBytes = BitConverter.GetBytes(totalLosses);
        
        Array.Copy(diamondsBytes, 0, data, 2, 2);
        Array.Copy(winsBytes, 0, data, 4, 2);
        Array.Copy(lossesBytes, 0, data, 6, 2);
        
        return Convert.ToBase64String(data);
    }

    public static PlayerRankData Deserialize(string base64)
    {
        try
        {
            byte[] data = Convert.FromBase64String(base64);
            if (data.Length != 9)
                return GetDefault();

            byte rankIndex = data[0];
            byte classIndex = data[1];
            ushort diamonds = BitConverter.ToUInt16(data, 2);
            ushort wins = BitConverter.ToUInt16(data, 4);
            ushort losses = BitConverter.ToUInt16(data, 6);

            return new PlayerRankData(rankIndex, classIndex, diamonds, wins, losses);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to deserialize PlayerRankData: {e.Message}");
            return GetDefault();
        }
    }
}
