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

    public virtual void Dispose() { }
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
    ScopeLens,
    AssaultVest,
    WeaknessPolicy,
    ExpShare,
    ScoreShield,
    RescueHood,
    SlickSpoon,
    CurseBangle,
    CurseIncense,
    ChargingCharm,
    AccelBracer,
    DriveLens,
    BigRoot,
    VanguardBell,
    RustedSword
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
                return System.Enum.IsDefined(typeof(AvailableHeldItems), heldItem)
                    ? new HeldItemRuntime(heldItem) : new EmptyHeldItem();
        }
    }

    public static string SerializeHeldItems(byte[] heldItemsIds)
    {
        return System.Convert.ToBase64String(heldItemsIds ?? System.Array.Empty<byte>());
    }

    public static List<HeldItemInfo> DeserializeHeldItems(string heldItemsIds)
    {
        List<HeldItemInfo> items = new List<HeldItemInfo>();
        if (CharactersList.Instance == null) return items;
        byte[] itemIDs = System.Array.Empty<byte>();
        if (!string.IsNullOrEmpty(heldItemsIds) && heldItemsIds.Length <= 16)
            try { itemIDs = System.Convert.FromBase64String(heldItemsIds); }
            catch (System.FormatException) { }
        var equipped = new HashSet<AvailableHeldItems>();
        HeldItemInfo empty = CharactersList.Instance.GetHeldItemByID(0);
        for (int slot = 0; slot < MAX_HELD_ITEMS; slot++)
        {
            HeldItemInfo item = slot < itemIDs.Length ? CharactersList.Instance.GetHeldItemByID(itemIDs[slot]) : empty;
            if (item == null || (item.heldItemID != AvailableHeldItems.None && !equipped.Add(item.heldItemID))) item = empty;
            if (item != null) items.Add(item);
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
