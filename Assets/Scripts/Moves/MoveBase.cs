using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class MoveBase
{
    public string Name;
    public float Cooldown;
    public PlayerManager playerManager;
    public bool IsActive = false;

    public bool wasMoveSuccessful = false;

    public event Action<MoveBase> onMoveOver;

    public virtual void Start(PlayerManager controller)
    {
        playerManager = controller;
        IsActive = true;
        wasMoveSuccessful = false;
        Debug.Log("Executing move: " + Name);
    }

    public abstract void Update();

    public virtual void Finish(){
        onMoveOver?.Invoke(this);
        IsActive = false;
    }

    public virtual void Cancel(){
        IsActive = false;
    }
}

public static class MoveDatabase
{
    public static MoveBase GetMove(AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.LockedMove:
                return new LockedMove();
            case AvailableMoves.CinderEmber:
                return new CinderEmber();
            case AvailableMoves.CinderLowSweep:
                return new CinderLowSweep();
            case AvailableMoves.CinderPyroball:
                return new CinderPyroball();
            case AvailableMoves.CinderFlameCharge:
                return new CinderFlameCharge();
            case AvailableMoves.CinderFeint:
                return new CinderFeint();
            case AvailableMoves.BlazingBycicleKick:
                return new BlazingBycicleKick();
            case AvailableMoves.GlaceSwift:
                return new GlaceSwift();
            case AvailableMoves.GlaceTailWhip:
                return new GlaceTailWhip();
            default:
                return null;
        }
    }
}

public enum AvailableMoves
{
    LockedMove,
    CinderEmber,
    CinderLowSweep,
    CinderPyroball,
    CinderFlameCharge,
    CinderFeint,
    BlazingBycicleKick,
    GlaceSwift,
    GlaceTailWhip
}
