using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DamageType { Physical, Special, True }

public class DamageInfo
{
    public Pokemon attacker;
    public float ratio;
    public int slider;
    public int baseDmg;
    public DamageType type;

    public DamageInfo(Pokemon attacker, float ratio, int slider, int baseDmg, DamageType type)
    {
        this.attacker = attacker;
        this.ratio = ratio;
        this.slider = slider;
        this.baseDmg = baseDmg;
        this.type = type;
    }
}
