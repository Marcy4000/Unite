using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum DamageType { Physical, Special, True }

public struct DamageInfo : INetworkSerializable
{
    //public Pokemon attacker;
    public ulong attackerId;
    public float ratio;
    public short slider;
    public short baseDmg;
    public DamageType type;

    /*public DamageInfo(Pokemon attacker, float ratio, int slider, int baseDmg, DamageType type)
    {
        this.attacker = attacker;
        this.ratio = ratio;
        this.slider = slider;
        this.baseDmg = baseDmg;
        this.type = type;
    }*/

    public DamageInfo(ulong attackerId, float ratio, short slider, short baseDmg, DamageType type)
    {
        this.attackerId = attackerId;
        this.ratio = ratio;
        this.slider = slider;
        this.baseDmg = baseDmg;
        this.type = type;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref attackerId);
        serializer.SerializeValue(ref ratio);
        serializer.SerializeValue(ref slider);
        serializer.SerializeValue(ref baseDmg);
        serializer.SerializeValue(ref type);
    }
}
