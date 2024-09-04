using System.Collections.Generic;
using Unity.Netcode;

public class HeldItemManager : NetworkBehaviour
{
    private HeldItemBase[] heldItemBases;

    public void Initialize(PlayerManager playerManager)
    {
        List<HeldItemInfo> heldItems = HeldItemDatabase.DeserializeHeldItems(playerManager.LobbyPlayer.Data["HeldItems"].Value);
        heldItemBases = new HeldItemBase[heldItems.Count];

        for (int i = 0; i < heldItemBases.Length; i++)
        {
            heldItemBases[i] = HeldItemDatabase.GetHeldItem(heldItems[i].heldItemID);
            heldItemBases[i].Initialize(playerManager);
        }

        foreach (var heldItemInfo in heldItems)
        {
            foreach (var statBoost in heldItemInfo.statBoosts)
            {
                StatChange statChange = new StatChange(statBoost.BoostAmount, statBoost.AffectedStat, 0f, false, true, statBoost.IsPercentage, 0, false);
                playerManager.Pokemon.AddStatChange(statChange);
            }
        }
    }

    public void Update()
    {   
        if (!IsOwner)
        {
            return;
        }

        for (int i = 0; i < heldItemBases.Length; i++)
        {
            if (heldItemBases[i] == null)
            {
                continue;
            }

            heldItemBases[i].Update();
        }
    }
}
