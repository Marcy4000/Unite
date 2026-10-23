using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>Resolves variants and exclusions before any assets are requested.</summary>
public static class TrainerOutfitResolver
{
    public readonly struct Selection
    {
        public readonly ClothingItem Item;
        public readonly int ModelIndex;
        public readonly string RuntimeKey;

        public Selection(ClothingItem item, int modelIndex)
        {
            Item = item;
            ModelIndex = modelIndex;
            RuntimeKey = item.prefabs[modelIndex].RuntimeKey.ToString();
        }
    }

    public static Selection[] Resolve(ClothesList clothes, PlayerClothesInfo info, int slotCount)
    {
        var items = new ClothingItem[slotCount];
        for (int i = 0; i < items.Length; i++)
        {
            var type = (ClothingType)i;
            int index = info.GetClothingIndex(type == ClothingType.Eyes ? ClothingType.Face : type);
            items[i] = clothes.GetClothingItem(type, index, info.IsMale);
        }
        return ResolveItems(items, info);
    }

    public static Selection[] ResolveItems(ClothingItem[] items, PlayerClothesInfo info)
    {
        var selections = new Selection[items.Length];
        bool pantsDisabled = false;
        foreach (var item in items)
        {
            if (item != null && item.disablesClothingType.Contains(ClothingType.Pants))
                pantsDisabled = true;
        }

        var pants = items.Length > (int)ClothingType.Pants ? items[(int)ClothingType.Pants] : null;
        foreach (var item in items)
        {
            if (item == null || item.prefabs.Count == 0) continue;

            int variant = 0;
            if (item.clothingType == ClothingType.Shirt && item.prefabs.Count > 1)
                variant = info.Overwear != 0 ? 0 : 1;
            else if (item.clothingType == ClothingType.Socks)
                variant = Mathf.Clamp(pants != null && !pantsDisabled ? pants.sockTypeToUse : 2, 0, item.prefabs.Count - 1);

            AssetReferenceGameObject prefab = item.prefabs[variant];
            if (prefab == null || !prefab.RuntimeKeyIsValid()) continue;
            int slot = (int)item.clothingType;
            if (slot < selections.Length)
                selections[slot] = new Selection(item, variant);
        }

        // Match the original union-of-exclusions rule, including exclusions from
        // selected items which are themselves hidden by another item.
        var disabled = new bool[items.Length];
        foreach (var selection in selections)
        {
            if (selection.Item == null) continue;
            foreach (var type in selection.Item.disablesClothingType)
            {
                if ((int)type < disabled.Length) disabled[(int)type] = true;
            }
        }
        for (int i = 0; i < selections.Length; i++)
        {
            if (disabled[i]) selections[i] = default;
        }
        return selections;
    }
}
