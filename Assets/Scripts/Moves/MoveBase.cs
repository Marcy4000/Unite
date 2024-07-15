using System;
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
        wasMoveSuccessful = false;
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
            case AvailableMoves.GlaceIcycleSpear:
                return new GlaceIcicleSpear();
            case AvailableMoves.GlaceIcyWind:
                return new GlaceIcyWind();
            case AvailableMoves.GlaceIceShard:
                return new GlaceIceShard();
            case AvailableMoves.GlaceFreezeDry:
                return new GlaceFreezeDry();
            case AvailableMoves.GlaceonUnite:
                return new GlaceUnite();

            case AvailableMoves.SylvSwift:
                return new SylvSwift();
            case AvailableMoves.SylvBabyDollEyes:
                return new SylvBabyDollEyes();
            case AvailableMoves.SylvHyperVoice:
                return new SylvHyperVoice();
            case AvailableMoves.SylvCalmMind:
                return new SylvCalmMind();

            case AvailableMoves.VaporSwift:
                return new VaporSwift();
            case AvailableMoves.VaporHelpingHand:
                return new VaporHelpingHand();
            case AvailableMoves.VaporRainDance:
                return new VaporRainDance();
            case AvailableMoves.VaporDive:
                return new VaporDive();
            case AvailableMoves.VaporAquaRing:
                return new VaporAquaRing();
            case AvailableMoves.VaporeonUnite:
                return new VaporeonUnite();

            case AvailableMoves.JoltSwift:
                return new JoltSwift();
            case AvailableMoves.JoltAgility:
                return new JoltAgility();
            case AvailableMoves.JoltThunderFang:
                return new JoltThunderFang();
            case AvailableMoves.JoltCharge:
                return new JoltCharge();
            case AvailableMoves.JoltDischarge:
                return new JoltDischarge();
            case AvailableMoves.JoltElectroWeb:
                return new JoltElectroweb();
            case AvailableMoves.JolteonUnite:
                return new JolteonUnite();

            case AvailableMoves.FlygonBite:
                return new FlygonBite();
            case AvailableMoves.FlygonDig:
                return new FlygonDig();
            case AvailableMoves.FlygonBoomBurst:
                return new FlygonBoomburst();
            case AvailableMoves.FlygonSuperSonic:
                return new FlygonSupersonic();
            case AvailableMoves.FlygonSandstorm:
                return new FlygonSandstorm();
            case AvailableMoves.FlygonEarthquake:
                return new FlygonEarthquake();
            case AvailableMoves.FlygonUnite:
                return new FlygonUnite();

            case AvailableMoves.MeowsticMScratch:
                return new MeowsticMScratch();
            case AvailableMoves.MeowsticMLeer:
                return new MeowsticMLeer();
            case AvailableMoves.MeowsticMReflect:
                return new MeowsticMReflect();
            case AvailableMoves.MeowsticMMagicCoat:
                return new MeowsticMMagicCoat();
            case AvailableMoves.MeowsticMPsychic:
                return new MeowsticMPsychic();
            case AvailableMoves.MeowsticMWonderRoom:
                return new MeowsticMWonderRoom();
            case AvailableMoves.MeowsticMUnite:
                return new MeowsticMUnite();

            default:
                return null;
        }
    }
}

public enum AvailableMoves : uint
{
    LockedMove,

    CinderEmber,
    CinderLowSweep,
    CinderPyroball,
    CinderFlameCharge,
    CinderFeint,
    BlazingBycicleKick,

    GlaceSwift,
    GlaceTailWhip,
    GlaceIcycleSpear,
    GlaceIcyWind,
    GlaceIceShard,
    GlaceFreezeDry,
    GlaceonUnite,

    SylvSwift,
    SylvBabyDollEyes,
    SylvHyperVoice,
    SylvMysticalCringe,
    SylvCalmMind,
    SylvDrainingKiss,
    SylveonUnite,

    VaporSwift,
    VaporHelpingHand,
    VaporRainDance,
    VaporAquaRing,
    VaporDive,
    VaporHaze,
    VaporeonUnite,

    JoltSwift,
    JoltAgility,
    JoltThunderFang,
    JoltDischarge,
    JoltCharge,
    JoltElectroWeb,
    JolteonUnite,

    FlygonBite,
    FlygonDig,
    FlygonSuperSonic,
    FlygonBoomBurst,
    FlygonEarthquake,
    FlygonSandstorm,
    FlygonUnite,

    MeowsticMScratch,
    MeowsticMLeer,
    MeowsticMReflect,
    MeowsticMMagicCoat,
    MeowsticMWonderRoom,
    MeowsticMPsychic,
    MeowsticMUnite,
}
