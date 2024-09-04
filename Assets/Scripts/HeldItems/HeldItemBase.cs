using System.Collections.Generic;

public abstract class HeldItemBase
{
    public string Name;
    public PlayerManager playerManager;

    public virtual void Initialize(PlayerManager controller)
    {
        playerManager = controller;
    }

    public abstract void Update();

    public abstract void Reset();
}

public enum AvailableHeldItems : byte
{
    None,
    Leftovers,
    RockyHelmet,
    SpAtkSpecs,
    AttackWeight,
    AeosCookie,
    WiseGlasses,
    MuscleBand,
    ChoiceSpecs,
    FloatStone,
    RapidFireScarf,
    ShellBell,
    EnergyAmp,
    FocusBand,
    BuddyBarrier,
    ResonantGuard,
    DrainCrown,
    RazorClaw,
    ScopeLens
}

public static class HeldItemDatabase
{
    public static int MAX_HELD_ITEMS = 3;

    public static HeldItemBase GetHeldItem(AvailableHeldItems heldItem)
    {
        switch (heldItem)
        {
            case AvailableHeldItems.None:
                return new EmptyHeldItem();
            case AvailableHeldItems.Leftovers:
                return new Leftovers();
            case AvailableHeldItems.SpAtkSpecs:
                return new SpAtkSpecs();
            case AvailableHeldItems.AttackWeight:
                return new AttackWeight();
            case AvailableHeldItems.AeosCookie:
                return new AeosCookie();
            case AvailableHeldItems.RockyHelmet:
                return new RockyHelmet();
            case AvailableHeldItems.WiseGlasses:
                return new WiseGlasses();
            case AvailableHeldItems.MuscleBand:
                return new MuscleBand();
            case AvailableHeldItems.ChoiceSpecs:
                return new ChoiceSpecs();
            case AvailableHeldItems.FloatStone:
                return new FloatStone();
            case AvailableHeldItems.RapidFireScarf:
                return new RapidFireScarf();
            case AvailableHeldItems.ShellBell:
                return new ShellBell();
            case AvailableHeldItems.EnergyAmp:
                return new EnergyAmp();
            case AvailableHeldItems.FocusBand:
                return new FocusBand();
            case AvailableHeldItems.BuddyBarrier:
                return new BuddyBarrier();
            case AvailableHeldItems.ResonantGuard:
                return new ResonantGuard();
            case AvailableHeldItems.DrainCrown:
                return new DrainCrown();
            case AvailableHeldItems.RazorClaw:
                return new RazorClaw();
            case AvailableHeldItems.ScopeLens:
                return new ScopeLens();
            default:
                return new EmptyHeldItem();
        }
    }

    public static string SerializeHeldItems(byte[] heldItemsIds)
    {
        return System.Convert.ToBase64String(heldItemsIds);
    }

    public static List<HeldItemInfo> DeserializeHeldItems(string heldItemsIds)
    {
        byte[] itemIDs = System.Convert.FromBase64String(heldItemsIds);

        List<HeldItemInfo> items = new List<HeldItemInfo>();

        foreach (byte id in itemIDs)
        {
            items.Add(CharactersList.Instance.GetHeldItemByID(id));
        }

        return items;
    }
}

public class EmptyHeldItem : HeldItemBase
{
    public override void Update()
    {
        // Do nothing
    }

    public override void Reset()
    {
        // Do nothing
    }
}