using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private bool cresseliaUniteAirborne, cresseliaUniteLanding;
    private double cresseliaFlightStarted;
    private float cresseliaNextUnite;
    private int cresseliaFlightVisual, cresseliaWarningVisual;
    private Vector3 cresseliaLandingPoint;
    private Coroutine cresseliaFlightRoutine, cresseliaLandingRoutine;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void BeginCresseliaUniteRPC()
    {
        UpdateCresseliaServer();
        if (!IsLivingCresselia || cresseliaUniteAirborne || pokemon.CurrentLevel < 8 || Time.time < cresseliaNextUnite
            || pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts))
        { CresseliaUnitePhaseRPC(0, transform.position, CresseliaNow); return; }
        // The shared owner-side energy system spends at takeoff, never at recast.
        // This server guard also prevents duplicate begin messages within a cast.
        cresseliaNextUnite = Time.time + CresseliaMoveRules.FlightDuration + CresseliaMoveRules.LandingDuration;
        cresseliaUniteAirborne = true; cresseliaUniteLanding = false;
        cresseliaFlightStarted = CresseliaNow;
        // Owner-predicted ascent may already have replicated before this RPC.
        // Never add flight height to an already airborne authoritative transform.
        Vector3 takeoff = transform.position;
        if (playerManager.PlayerMovement.TryCresseliaLanding(takeoff, out Vector3 ground)) takeoff.y = ground.y;
        pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0, false, CresseliaMoveRules.UniteInvincibleId));
        cresseliaFlightVisual = ShowCresseliaEffect(CresseliaEffect.UniteFlight, transform.position, transform.forward,
            CresseliaMoveRules.FlightDuration + CresseliaMoveRules.LandingDuration, NetworkObjectId);
        cresseliaWarningVisual = ShowCresseliaEffect(CresseliaEffect.UniteWarning, transform.position, transform.forward,
            CresseliaMoveRules.FlightDuration, NetworkObjectId, CresseliaMoveRules.UniteRadius, 1);
        CresseliaUnitePhaseRPC(1, takeoff, cresseliaFlightStarted);
        cresseliaFlightRoutine = StartCoroutine(CresseliaFlightWatchdog());
    }

    private IEnumerator CresseliaFlightWatchdog()
    {
        while (cresseliaUniteAirborne && !cresseliaUniteLanding && CresseliaNow < cresseliaFlightStarted + CresseliaMoveRules.FlightDuration)
            yield return null;
        cresseliaFlightRoutine = null;
        if (cresseliaUniteAirborne && !cresseliaUniteLanding) BeginCresseliaLanding();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RecastCresseliaUniteRPC()
    {
        if (!cresseliaUniteAirborne || cresseliaUniteLanding || CresseliaNow < cresseliaFlightStarted + CresseliaMoveRules.TakeoffDuration) return;
        BeginCresseliaLanding();
    }

    private void BeginCresseliaLanding()
    {
        if (!IsLivingCresselia) { EndCresseliaUniteServer(true); return; }
        if (!playerManager.PlayerMovement.TryCresseliaLanding(transform.position, out cresseliaLandingPoint))
        { EndCresseliaUniteServer(true); return; }
        cresseliaUniteLanding = true;
        StopCresseliaEffectRPC(cresseliaWarningVisual);
        cresseliaWarningVisual = ShowCresseliaEffect(CresseliaEffect.UniteWarning, cresseliaLandingPoint, transform.forward,
            CresseliaMoveRules.LandingDuration, 0, CresseliaMoveRules.UniteRadius);
        double started = CresseliaNow;
        CresseliaUnitePhaseRPC(2, cresseliaLandingPoint, started);
        cresseliaLandingRoutine = StartCoroutine(CresseliaLand(started));
    }

    private IEnumerator CresseliaLand(double started)
    {
        while (cresseliaUniteAirborne && CresseliaNow < started + CresseliaMoveRules.LandingDuration) yield return null;
        cresseliaLandingRoutine = null;
        if (!cresseliaUniteAirborne || !IsLivingCresselia) yield break;
        foreach (Pokemon target in CresseliaMoveRuntime.Targets(Physics.OverlapSphere(cresseliaLandingPoint, CresseliaMoveRules.UniteRadius,
            LayerMask.GetMask("Players", "WildPokemons"))))
        {
            if (!CresseliaMoveRuntime.CanHit(playerManager, target)) continue;
            target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 1.35f, 9, 840, DamageType.Special, DamageProprieties.IsUniteMove));
            if (CresseliaMoveRuntime.Living(target)) target.AddStatusEffect(new StatusEffect(StatusType.Asleep, 1.2f, true, 0));
        }
        ShowCresseliaEffect(CresseliaEffect.UniteImpact, cresseliaLandingPoint, Vector3.forward, 0.55f, 0, CresseliaMoveRules.UniteRadius);
        EndCresseliaUniteServer(false);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelCresseliaUniteRPC() { EndCresseliaUniteServer(true); }

    private void EndCresseliaUniteServer(bool cancelled)
    {
        if (!IsServer || !cresseliaUniteAirborne) return;
        cresseliaUniteAirborne = false; cresseliaUniteLanding = false;
        if (cresseliaFlightRoutine != null) StopCoroutine(cresseliaFlightRoutine);
        if (cresseliaLandingRoutine != null) StopCoroutine(cresseliaLandingRoutine);
        cresseliaFlightRoutine = null; cresseliaLandingRoutine = null;
        if (pokemon != null && pokemon.IsSpawned) pokemon.RemoveStatusEffectWithID(CresseliaMoveRules.UniteInvincibleId);
        if (!IsSpawned) return;
        StopCresseliaEffectRPC(cresseliaFlightVisual); StopCresseliaEffectRPC(cresseliaWarningVisual);
        if (cancelled && IsLivingCresselia && !playerManager.PlayerMovement.TryCresseliaLanding(transform.position, out cresseliaLandingPoint))
            cresseliaLandingPoint = transform.position;
        CresseliaUnitePhaseRPC(IsLivingCresselia ? 0 : -1, cresseliaLandingPoint, CresseliaNow);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void CresseliaUnitePhaseRPC(int phase, Vector3 point, double started)
    {
        if (uniteMove is CresseliaUnite move) move.ApplyServerPhase(phase, point, started);
    }
}
