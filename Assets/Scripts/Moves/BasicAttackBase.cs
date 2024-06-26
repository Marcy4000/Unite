using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class BasicAttackBase
{
    public PlayerManager playerManager;
    public float range = 0f;

    public virtual void Initialize(PlayerManager manager)
    {
        playerManager = manager;
    }

    public abstract void Perform();

    public abstract void Update();
}

public class EmptyBasicAtk : BasicAttackBase
{
    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
    }

    public override void Perform()
    {
        Debug.Log("Empty basic attack");
    }

    public override void Update()
    {
    }
}

public static class BasicAttacksDatabase
{
    public static BasicAttackBase GetBasicAttack(string pokemonName)
    {
       pokemonName = pokemonName.ToLower();
        switch (pokemonName)
        {
            case "cinderace":
                return new CinderBasicAtk();
            case "glaceon":
                return new GlaceBasicAtk();
            case "sylveon":
                return new SylvBasicAtk();
            case "vaporeon":
                return new VaporBasicAtk();
            case "jolteon":
                return new JoltBasicAtk();
            case "flygon":
                return new FlygonBasicAtk();
            default:
                return new EmptyBasicAtk();
        }
    }
}
