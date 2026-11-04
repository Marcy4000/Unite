using UnityEngine;

public partial class Pokemon
{
    private HeldItemManager heldItemEffects;
    private HeldItemManager HeldItems => heldItemEffects != null ? heldItemEffects : heldItemEffects = GetComponent<HeldItemManager>();
    private float curseEndsAt;

    public bool HasHeldItem(AvailableHeldItems item) => HeldItems != null && HeldItems.Has(item);

    public void RefreshHeldItemStartingHealth(int previousMaximum)
    {
        if (IsServer && CurrentHp > 0 && CurrentHp == previousMaximum) currentHp.Value = GetMaxHp();
    }

    public float GetPreciseStatChange(Stat stat)
    {
        float amount = 0;
        foreach (StatChange change in statChanges)
            if (change.AffectedStat == stat) amount += change.IsBuff ? change.EffectiveAmount : -change.EffectiveAmount;
        return amount;
    }

    public void ClearHeldItemModifier(ushort id)
    {
        if (!IsServer) return;
        for (int index = statChanges.Count - 1; index >= 0; index--)
            if (statChanges[index].ID == id) RemoveStatAt(index);
    }

    public void SetHeldItemModifier(ushort id, StatChange change)
    {
        if (!IsServer) return;
        ClearHeldItemModifier(id);
        AddStatChange(change);
    }

    public void ApplyHeldItemCurse()
    {
        if (IsServer && CurrentHp > 0) curseEndsAt = Time.time + 1.5f;
    }

    private int HeldItemHealing(int amount, Pokemon healer)
    {
        float multiplier = Time.time < curseEndsAt ? 0.7f : 1;
        if (healer == this && HasHeldItem(AvailableHeldItems.BigRoot)) multiplier *= 1.2f;
        else if (healer != null && healer != this && healer.TeamMember.IsOnSameTeam(TeamMember)
            && healer.HasHeldItem(AvailableHeldItems.RescueHood)) multiplier *= 1.23f;
        return Mathf.FloorToInt(amount * multiplier);
    }

    public void GrantHeldItemShield(ShieldInfo shield, Pokemon source)
    {
        if (!IsServer || CurrentHp <= 0) return;
        if (source != null) shield = shield.WithSource(source.NetworkObjectId);
        for (int index = 0; index < shields.Count; index++)
            if (shields[index].ID == shield.ID)
            {
                shieldTimers[index] = shield.IsTimed ? shield.Duration : -1;
                return;
            }
        AddShieldRPC(shield);
    }

    public int TotalExperience
    {
        get
        {
            int amount = CurrentExp + StoredExp;
            if (baseStats != null)
                for (int level = 0; level < CurrentLevel; level++) amount += baseStats.GetExpForNextLevel(level);
            return amount;
        }
    }

    public void GainHeldItemPassiveExperience(int amount)
    {
        if (!IsServer || CurrentHp <= 0 || baseStats == null) return;
        if (baseStats.IsLevelBeforeEvo(CurrentLevel)) StoreExperience(amount);
        else GainExperienceRPC(amount);
    }

    private void NotifyHeldItemHindrance(ulong source)
    {
        if (IsServer && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(source, out var obj)
            && obj.TryGetComponent(out HeldItemManager manager)) manager.OnHindrance(this);
    }
}
