using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MoveBase
{
    public string name;
    public float cooldown;
    public MovesController movesController;
    public bool isActive = false;

    public bool wasMoveSuccessful = false;

    public event Action<MoveBase> onMoveOver;

    public virtual void Start(MovesController controller)
    {
        movesController = controller;
        isActive = true;
        wasMoveSuccessful = false;
        Debug.Log("Executing move: " + name);
    }

    public virtual void Update(){
    }

    public virtual void Finish(){
        onMoveOver?.Invoke(this);
        isActive = false;
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
            case AvailableMoves.BlazingBycicleKick:
                return new BlazingBycicleKick();
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
    BlazingBycicleKick
}
