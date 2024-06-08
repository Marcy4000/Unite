using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PassiveBase
{
    public PlayerManager playerManager;
    public bool IsActive = false;

    public virtual void Start(PlayerManager controller)
    {
        playerManager = controller;
    }

    public virtual void Update()
    {    
    }
}

public static class PassiveDatabase
{
    public static PassiveBase GetPassive(AvailablePassives passive)
    {
        switch (passive)
        {
            case AvailablePassives.EmptyPassive:
                return new EmptyPassive();
            case AvailablePassives.CinderPassive:
                return new CinderPassive();
            default:
                return new EmptyPassive();
        }
    }
}

public enum AvailablePassives
{
    EmptyPassive,
    CinderPassive
}