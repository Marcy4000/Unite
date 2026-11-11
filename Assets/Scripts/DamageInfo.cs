using System;
using Unity.Netcode;

public enum DamageType : byte { Physical, Special, True }

// Orthogonal to the legacy flags: secondary damage must not recursively proc itself.
public enum DamageSource : byte { Direct, Item, Secondary, Redirected }

[Flags]
public enum DamageProprieties : byte
{
    None = 0,
    CanCrit = 1 << 0,
    IsBasicAttack = 1 << 1,
    IsUniteMove = 1 << 2,
    IsHeal = 1 << 3,
    IsMuscleBand = 1 << 4,
    WasCriticalHit = 1 << 5,
    Unused_2 = 1 << 6,
    Unused_1 = 1 << 7
}

[Serializable]
public struct DamageInfo : INetworkSerializable
{
    public ulong attackerId;
    public float ratio;
    public short slider;
    public short baseDmg;
    public DamageType type;
    public DamageProprieties proprieties;
    // Zero is treated as one for legacy/default-initialized damage records.
    public float multiplier;
    public float bonusCritRate;
    public DamageSource source;
    // Additional ticks of one attack still deal damage/lifesteal, but must not
    // dispatch a second set of legacy on-hit item/passive procs. Opt-in only.
    public bool suppressOnHitEffects;

    public DamageInfo(ulong attackerId, float ratio, short slider, short baseDmg, DamageType type, DamageProprieties proprieties = DamageProprieties.None)
    {
        this.attackerId = attackerId;
        this.ratio = ratio;
        this.slider = slider;
        this.baseDmg = baseDmg;
        this.type = type;
        this.proprieties = proprieties;
        multiplier = 1f;
        bonusCritRate = 0;
        source = DamageSource.Direct;
        suppressOnHitEffects = false;
    }

    // Apply once when constructing a hit. Conditional formulas and combat flags stay authored.
    public static DamageInfo Tuned(string id, DamageInfo authored)
    {
        var tuning = Unite.Updates.BalanceRuntime.Damage(id);
        if (tuning == null || (authored.proprieties & DamageProprieties.IsHeal) != 0) return authored;
        if (tuning.overrideAttack) authored.ratio = tuning.attackCoefficient;
        if (tuning.overrideLevel) authored.slider = (short)tuning.levelDamage;
        if (tuning.overrideBase) authored.baseDmg = (short)tuning.baseDamage;
        return authored;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref attackerId);
        serializer.SerializeValue(ref ratio);
        serializer.SerializeValue(ref slider);
        serializer.SerializeValue(ref baseDmg);
        serializer.SerializeValue(ref type);
        serializer.SerializeValue(ref proprieties);
        serializer.SerializeValue(ref multiplier);
        serializer.SerializeValue(ref bonusCritRate);
        serializer.SerializeValue(ref source);
        serializer.SerializeValue(ref suppressOnHitEffects);
    }
}
