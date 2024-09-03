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
