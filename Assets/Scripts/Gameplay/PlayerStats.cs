using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct PlayerStats : INetworkSerializable
{
    public FixedString32Bytes playerId;

    public ushort kills;
    public ushort deaths;
    public ushort assists;
    public ushort score;

    public uint damageDealt;
    public uint damageTaken;
    public uint healingDone;

    public PlayerStats(string playerId, ushort kills, ushort deaths, ushort assists, ushort score, uint damageDealt, uint damageTaken, uint healingDone)
    {
        this.playerId = playerId;
        this.kills = kills;
        this.deaths = deaths;
        this.assists = assists;
        this.score = score;
        this.damageDealt = damageDealt;
        this.damageTaken = damageTaken;
        this.healingDone = healingDone;
    }

    public PlayerStats(string playerId, int kills, int deaths, int assists, int score, int damageDealt, int damageTaken, int healingDone)
    {
        this.playerId = playerId;
        this.kills = (ushort)kills;
        this.deaths = (ushort)deaths;
        this.assists = (ushort)assists;
        this.score = (ushort)score;
        this.damageDealt = (uint)damageDealt;
        this.damageTaken = (uint)damageTaken;
        this.healingDone = (uint)healingDone;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerId);
        serializer.SerializeValue(ref kills);
        serializer.SerializeValue(ref deaths);
        serializer.SerializeValue(ref assists);
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref damageDealt);
        serializer.SerializeValue(ref damageTaken);
        serializer.SerializeValue(ref healingDone);
    }

    public int CalculateBattleScore()
    {
        // Define the weights for each statistic
        const int killWeight = 100;
        const int deathPenalty = 50;
        const int assistWeight = 50;
        const int scoreWeight = 1;
        const float damageDealtWeight = 0.01f;
        const float damageTakenWeight = 0.005f;
        const float healingDoneWeight = 0.01f;

        // Calculate the raw battle score based on the defined weights
        int rawBattleScore = (kills * killWeight)
                           - (deaths * deathPenalty)
                           + (assists * assistWeight)
                           + (int)(score * scoreWeight)
                           + (int)(damageDealt * damageDealtWeight)
                           + (int)(damageTaken * damageTakenWeight)
                           + (int)(healingDone * healingDoneWeight);

        // Ensure the raw score is not negative
        rawBattleScore = Mathf.Max(rawBattleScore, 0);

        // Define the expected range for the raw score
        const int minRawScore = 0;
        const int maxRawScore = 10000; // You may need to adjust this based on your game balance

        // Normalize the raw score to a value between 0 and 1
        float normalizedScore = Mathf.InverseLerp(minRawScore, maxRawScore, rawBattleScore);

        // Scale the normalized score to the desired range of 6 to 99
        int minBattleScore = 6;
        int maxBattleScore = 99;
        int battleScore = Mathf.RoundToInt(Mathf.Lerp(minBattleScore, maxBattleScore, normalizedScore));

        return battleScore;
    }
}
