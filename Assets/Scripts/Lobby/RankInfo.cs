using UnityEngine;

[System.Serializable]
public class RankInfo
{
    public string rankName;
    public int totalClasses;
    public int diamondsToPromote;
    public bool isSpecialRank;
    public Sprite rankIcon;
    public Sprite[] classesIcons;

    public RankInfo(string name, int classes, int diamonds, bool special = false, Sprite icon = null, Sprite[] classIcons = null)
    {
        rankName = name;
        totalClasses = classes;
        diamondsToPromote = diamonds;
        isSpecialRank = special;
        rankIcon = icon;
        classesIcons = classIcons;
    }
}
