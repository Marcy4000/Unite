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

public enum AvailableHeldItems : byte { None, Leftovers, RockyHelmet, SpAtkSpecs }

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
                //return new Leftovers();
            case AvailableHeldItems.RockyHelmet:
                //return new RockyHelmet();
            case AvailableHeldItems.SpAtkSpecs:
                //return new SpAtkSpecs();
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