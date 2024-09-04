using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHeldItemsIcons : MonoBehaviour
{
    [SerializeField] private Image[] icons;

    public void SetIcons(List<HeldItemInfo> heldItems)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (i < heldItems.Count)
            {
                icons[i].sprite = heldItems[i].icon;
                icons[i].gameObject.SetActive(true);
            }
            else
            {
                icons[i].gameObject.SetActive(false);
            }
        }
    }
}
