using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BasicAttackBase
{
    public PlayerManager playerManager;
    public float range = 0f;

    public virtual void Initialize(PlayerManager manager)
    {
        playerManager = manager;
    }

    public virtual void Perform()
    {
    }
}

public static class BasicAttacksDatabase
{
    public static BasicAttackBase GetBasitAttatck(string pokemonName)
    {
       pokemonName = pokemonName.ToLower();
        switch (pokemonName)
        {
            case "cinderace":
                return new CinderBasicAtk();
            default:
                return new BasicAttackBase();
        }
    }
}
