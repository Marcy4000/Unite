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

    // Selected moves tracking
    public AvailableMoves moveA;
    public AvailableMoves moveB;
    public AvailableMoves uniteMove;
    public FixedString32Bytes basicAttackName; // Pokemon name
    public AvailableBattleItems battleItem;
    
    // Upgrade status
    public bool moveAUpgraded;
    public bool moveBUpgraded;
    public bool uniteMoveUpgraded;

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
        
        // Initialize move tracking fields with default values
        this.moveA = AvailableMoves.LockedMove;
        this.moveB = AvailableMoves.LockedMove;
        this.uniteMove = AvailableMoves.LockedMove;
        this.basicAttackName = "";
        this.battleItem = AvailableBattleItems.None;
        this.moveAUpgraded = false;
        this.moveBUpgraded = false;
        this.uniteMoveUpgraded = false;
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
        
        // Initialize move tracking fields with default values
        this.moveA = AvailableMoves.LockedMove;
        this.moveB = AvailableMoves.LockedMove;
        this.uniteMove = AvailableMoves.LockedMove;
        this.basicAttackName = "";
        this.battleItem = AvailableBattleItems.None;
        this.moveAUpgraded = false;
        this.moveBUpgraded = false;
        this.uniteMoveUpgraded = false;
    }

    // Constructor with move tracking parameters
    public PlayerStats(string playerId, ushort kills, ushort deaths, ushort assists, ushort score, 
                      uint damageDealt, uint damageTaken, uint healingDone,
                      AvailableMoves moveA, AvailableMoves moveB, AvailableMoves uniteMove,
                      string basicAttackName, AvailableBattleItems battleItem,
                      bool moveAUpgraded, bool moveBUpgraded, bool uniteMoveUpgraded)
    {
        this.playerId = playerId;
        this.kills = kills;
        this.deaths = deaths;
        this.assists = assists;
        this.score = score;
        this.damageDealt = damageDealt;
        this.damageTaken = damageTaken;
        this.healingDone = healingDone;
        
        this.moveA = moveA;
        this.moveB = moveB;
        this.uniteMove = uniteMove;
        this.basicAttackName = basicAttackName;
        this.battleItem = battleItem;
        this.moveAUpgraded = moveAUpgraded;
        this.moveBUpgraded = moveBUpgraded;
        this.uniteMoveUpgraded = uniteMoveUpgraded;
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
        
        // Serialize move tracking fields
        serializer.SerializeValue(ref moveA);
        serializer.SerializeValue(ref moveB);
        serializer.SerializeValue(ref uniteMove);
        serializer.SerializeValue(ref basicAttackName);
        serializer.SerializeValue(ref battleItem);
        serializer.SerializeValue(ref moveAUpgraded);
        serializer.SerializeValue(ref moveBUpgraded);
        serializer.SerializeValue(ref uniteMoveUpgraded);
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
