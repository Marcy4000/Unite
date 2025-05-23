using System;
using UnityEngine;

[Serializable]
public abstract class MoveBase
{
    public string Name;
    public float Cooldown;
    public PlayerManager playerManager;
    public bool IsActive = false;
    public bool IsUpgraded = false;

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

    public abstract void ResetMove();
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
            case AvailableMoves.SylveonUnite:
                return new SylvUnite();

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

            case AvailableMoves.MarshadowDrainPunch:
                return new MarshadowDrainPunch();
            case AvailableMoves.MarshadowShadowSneak:
                return new MarshadowShadowSneak();
            case AvailableMoves.MarshadowPhantomForce:
                return new MarshadowPhantomForce();
            case AvailableMoves.MarshadowSpectralThief:
                return new MarshadowSpectralThief();
            case AvailableMoves.MarshadowCloseCombat:
                return new MarshadowCloseCombat();
            case AvailableMoves.MarshadowAuraSphere:
                return new MarshadowAuraSphere();
            case AvailableMoves.MarshadowUnite:
                return new MarshadowUnite();

            case AvailableMoves.CresseliaSafeguard:
                return new CresseliaSafeguard();
            case AvailableMoves.CresseliaAuroraBeam:
                return new CresseliaAuroraBeam();
            case AvailableMoves.CresseliaPsybeam:
                return new CresseliaPsybeam();
            case AvailableMoves.CresseliaMoonblast:
                return new CresseliaMoonblast();
            case AvailableMoves.CresseliaMoonlight:
                return new CresseliaMoonlight();
            case AvailableMoves.CresseliaLunarBlessing:
                return new CresseliaLunarBlessing();
            case AvailableMoves.CresseliaUnite:
                return new CresseliaUnite();

            case AvailableMoves.EmboarEmber:
                return new EmboarEmber();
            case AvailableMoves.EmboarSmog:
                return new EmboarSmog();
            case AvailableMoves.EmboarFirePledge:
                return new EmboarFirePledge();
            case AvailableMoves.EmboarReversal:
                return new EmboarReversal();
            case AvailableMoves.EmboarFlameCharge:
                return new EmboarFlameCharge();
            case AvailableMoves.EmboarHeatCrash:
                return new EmboarHeatCrash();
            case AvailableMoves.EmboarUnite:
                return new EmboarUnite();

            case AvailableMoves.FlareonSwift:
                return new FlareonSwift();
            case AvailableMoves.FlareonTackle:
                return new FlareonTackle();
            case AvailableMoves.FlareonFlareBlitz:
                return new FlareonFlareBlitz();
            case AvailableMoves.FlareonFlameThrower:
                return new FlareonFlamethrower();
            case AvailableMoves.FlareonHeatWave:
                return new FlareonHeatWave();
            case AvailableMoves.FlareonTakedown:
                return new FlareonTakedown();
            case AvailableMoves.FlareonUnite:
                return new FlareonUnite();

            case AvailableMoves.PsyduckRaceDash:
                return new PsyduckRaceDash();
            case AvailableMoves.PsyduckRaceBarrier:
                return new PsyduckRaceBarrier();
            case AvailableMoves.PsyduckRaceConfuseRay:
                return new PsyduckRaceConfuseRay();
            case AvailableMoves.PsyduckRaceFreezeRay:
                return new PsyduckRaceFreezeRay();
            case AvailableMoves.PsyduckRaceBubble:
                return new PsyduckRaceBubble();
            case AvailableMoves.PsyduckRaceShield:
                return new PsyduckRaceShield();

            case AvailableMoves.SerperiorCut:
                return new SerperiorCut();
            case AvailableMoves.SerperiorAerialAce:
                return new SerperiorAerialAce();
            case AvailableMoves.SerperiorWringOut:
                return new SerperiorWringOut();

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

    MarshadowDrainPunch,
    MarshadowShadowSneak,
    MarshadowSpectralThief,
    MarshadowPhantomForce,
    MarshadowAuraSphere,
    MarshadowCloseCombat,
    MarshadowUnite,

    CresseliaSafeguard,
    CresseliaAuroraBeam,
    CresseliaPsybeam,
    CresseliaMoonblast,
    CresseliaLunarBlessing,
    CresseliaMoonlight,
    CresseliaUnite,

    EmboarEmber,
    EmboarSmog,
    EmboarFirePledge,
    EmboarReversal,
    EmboarHeatCrash,
    EmboarFlameCharge,
    EmboarUnite,

    FlareonSwift,
    FlareonTackle,
    FlareonFlareBlitz,
    FlareonTakedown,
    FlareonHeatWave,
    FlareonFlameThrower,
    FlareonUnite,

    PsyduckRaceDash,
    PsyduckRaceBarrier,
    PsyduckRaceConfuseRay,
    PsyduckRaceFreezeRay,
    PsyduckRaceBubble,
    PsyduckRaceShield,

    SerperiorCut,
    SerperiorAerialAce,
    SerperiorLeafBlade,
    SerperiorDragonTail,
    SerperiorWringOut,
    SerperiorBrutalSwing,
    SerperiorUnite,
}
