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

    public abstract void Update();
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
            case AvailablePassives.GlaceonPassive:
                return new GlaceonPassive();
            case AvailablePassives.SylveonPassive:
                return new SylvPassive();
            case AvailablePassives.JolteonPassive:
                return new JoltPassive();
            case AvailablePassives.FlygonPassive:
                return new FlygonPassive();
            default:
                return new EmptyPassive();
        }
    }
}

public enum AvailablePassives
{
    EmptyPassive,
    CinderPassive,
    GlaceonPassive,
    SylveonPassive,
    VaporeonPassive,
    JolteonPassive,
    FlygonPassive
}