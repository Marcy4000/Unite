using UnityEngine;

// Unite-DB's detailed (RSB) notes are the source for these values. Distances and
// charge thresholds are expressed in this project's world units.
public static class DuraMoveRules
{
    public const float PulseChargeTime = 1.5f;
    public const float PulseCooldown = 7.5f;
    public const float MarkDuration = 5f;
    public const int MaxMarks = 6; // Five orbs, then the final ring.
    public const float RockRadius = 2f;
    public const float RuinRadius = 6f;

    public static int PulseStage(float charge)
    {
        return charge >= 1f ? 2 : charge >= 0.5f ? 1 : 0;
    }

    public static float PulseRadius(float charge)
    {
        return Mathf.Lerp(3f, 7.5f, Mathf.Clamp01(charge));
    }

    public static DamageInfo PulseDamage(ulong attacker, float charge, int marks, int missingHp)
    {
        int stage = PulseStage(charge);
        float[] baseRatios = { 0.32f, 0.40f, 0.50f };
        int[] bases = { 38, 48, 56 };
        float[] markRatios = { 0.45f, 0.57f, 0.69f };
        int[] markBases = { 54, 68, 82 };
        float[] ringRatios = { 0.59f, 0.75f, 0.90f };
        int[] ringBases = { 70, 87, 106 };
        marks = Mathf.Clamp(marks, 0, MaxMarks);
        int orbs = Mathf.Min(marks, 5);
        int ring = marks == MaxMarks ? 1 : 0;
        return new DamageInfo(attacker,
            baseRatios[stage] + orbs * markRatios[stage] + ring * ringRatios[stage],
            (short)(1 + orbs + ring),
            (short)Mathf.Min(short.MaxValue, bases[stage] + orbs * markBases[stage] + ring * ringBases[stage]
                + Mathf.RoundToInt(Mathf.Max(0, missingHp) * (orbs * 0.008f + ring * 0.016f))),
            DamageType.Physical, DamageProprieties.Unused_1);
    }
}
