using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Authored geometry is presentation only. All hits and movement confirmations execute on the server.
public class EmboarMoveObject : NetworkBehaviour
{
    [SerializeField] private GameObject graphics, telegraph, body, burst, waveFront;
    [SerializeField] private SphereCollider area;
    [SerializeField] private BoxCollider waveArea;
    [SerializeField] private GameObject[] pledgePips;
    [SerializeField] private AudioSource effectAudio;
    [SerializeField] private AudioClip rushSound, impactSound;
    private readonly NetworkVariable<ulong> actorId = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<EmboarCast> kind = new NetworkVariable<EmboarCast>();
    private readonly NetworkVariable<Vector3> aim = new NetworkVariable<Vector3>(Vector3.forward);
    private readonly NetworkVariable<Vector3> goal = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<byte> phase = new NetworkVariable<byte>();
    private readonly NetworkVariable<double> phaseStarted = new NetworkVariable<double>();
    private readonly NetworkVariable<bool> boosted = new NetworkVariable<bool>();
    private readonly HashSet<Pokemon> hit = new HashSet<Pokemon>();
    private MovesController combat;
    private PlayerManager actor;
    private Pokemon target;
    private int cast;
    private ushort effectId;
    private bool initialized, upgraded, holdsAsset, movementEnded, cleaned;
    private Vector3 origin, lastPoint;
    private float lastReportAge, createdAt;
    private AsyncOperationHandle<GameObject> asset;
    private Transform liftedModel;
    private Vector3 modelOrigin;
    private byte shownPhase = byte.MaxValue;
    public bool MovementEnded => movementEnded;
    private bool Live => actor != null && actor.IsSpawned && actor.Pokemon.CurrentHp > 0 && IsSpawned;
    private float Age => Time.time - createdAt;
    private float PhaseAge => (float)((NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble) - phaseStarted.Value);

    public void HoldAsset(AsyncOperationHandle<GameObject> handle) { asset = handle; holdsAsset = true; }
    public override void OnNetworkSpawn()
    {
        if (graphics != null) graphics.SetActive(false);
        if (IsServer) StartCoroutine(Watchdog());
    }
    public void Initialize(MovesController owner, PlayerManager player, EmboarCast move, int id, Vector3 direction, Vector3 destination, Pokemon victim, bool plus, bool empowered)
    {
        if (!IsServer || initialized) return;
        initialized = true; combat = owner; actor = player; cast = id; target = victim; upgraded = plus;
        origin = lastPoint = player.transform.position; createdAt = Time.time; effectId = EmboarEffectIds.Acquire();
        actorId.Value = player.NetworkObjectId; kind.Value = move; aim.Value = direction; goal.Value = destination; boosted.Value = empowered;
        phaseStarted.Value = NetworkManager.ServerTime.Time;
        player.Pokemon.OnDeath += OnDeath;
        StartCoroutine(Run());
    }
    private IEnumerator Watchdog() { yield return new WaitForSeconds(17); Despawn(); }
    private IEnumerator Run()
    {
        switch (kind.Value)
        {
            case EmboarCast.Ember: yield return Ember(); break;
            case EmboarCast.Smog: yield return Smog(); break;
            case EmboarCast.FirePledge:
                combat.ArmEmboarPledge();
                yield return Wait(EmboarMoveRules.PledgeDuration);
                break;
            case EmboarCast.Flamethrower: yield return Flamethrower(); break;
            case EmboarCast.HeatCrash: yield return HeatCrash(); break;
            case EmboarCast.FlameCharge:
                actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.HindranceResistance, EmboarMoveRules.ChargeDuration + 0.3f, true, effectId));
                yield return AwaitMovement(EmboarMoveRules.ChargeDuration + 1.5f);
                if (Live && upgraded) combat.EmboarBuff(effectId, Stat.Speed, 20, 2, true);
                yield return Wait(0.12f);
                break;
            case EmboarCast.Unite: yield return Ringbreaker(); break;
            case EmboarCast.Reckless:
                while (Live && combat.EmboarRecklessReady) yield return null;
                break;
        }
        Despawn();
    }
    private IEnumerator Wait(float duration, bool interruptible = false)
    {
        float until = Time.time + duration;
        while (Live && Time.time < until)
        {
            if (interruptible && actor.Pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts)) { Despawn(); yield break; }
            yield return null;
        }
    }
    private IEnumerator AwaitMovement(float timeout)
    {
        float until = Time.time + timeout;
        while (Live && !movementEnded && Time.time < until)
        {
            if (actor.Pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts) || actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) { Despawn(); yield break; }
            yield return null;
        }
    }
    private IEnumerator Ember()
    {
        yield return Wait(0.18f, true);
        if (!Live) yield break;
        transform.position = actor.transform.position + Vector3.up * 1.1f + aim.Value * 0.7f;
        SetPhase(1);
        float until = Time.time + 2;
        while (Live && EmboarMoveRules.CanHit(actor, target) && Time.time < until)
        {
            Vector3 destination = target.transform.position + Vector3.up * 0.65f;
            transform.position = Vector3.MoveTowards(transform.position, destination, 15 * Time.deltaTime);
            if ((destination - transform.position).sqrMagnitude <= 0.16f)
            {
                combat.HitEmboarTarget(target, EmboarMoveRules.Damage(actorId.Value, kind.Value));
                SetPhase(2); yield return Wait(0.18f); yield break;
            }
            yield return null;
        }
    }
    private IEnumerator Smog()
    {
        transform.position = origin - aim.Value * 0.5f;
        yield return Wait(0.05f, true);
        if (!Live) yield break;
        SetPhase(1);
        float until = Time.time + 3;
        while (Live && Time.time < until)
        {
            foreach (Pokemon victim in InArea())
                if (EmboarMoveRules.CanControl(actor, victim) && hit.Add(victim)) victim.AddStatusEffect(new StatusEffect(StatusType.VisionObscuring, 1.2f, true, effectId));
            yield return new WaitForSeconds(0.1f);
        }
    }
    private IEnumerator Flamethrower()
    {
        combat.EmboarUnstoppable(0.5f);
        if (upgraded) actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(actor.Pokemon.GetMaxHp() * 0.1f), EmboarMoveRules.FlameShield, 0, 3, true));
        yield return Wait(0.18f, true);
        if (!Live) yield break;
        transform.SetPositionAndRotation(actor.transform.position, Quaternion.LookRotation(aim.Value));
        SetPhase(1);
        // Query the complete authored flame volume, including the point-blank space.
        foreach (Pokemon victim in InBox())
            if (combat.HitEmboarTarget(victim, EmboarMoveRules.Damage(actorId.Value, kind.Value)) && EmboarMoveRules.CanControl(actor, victim))
                victim.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.35f, true, effectId));
        yield return Wait(0.32f);
    }
    private IEnumerator HeatCrash()
    {
        yield return AwaitMovement(2);
        if (!Live || !movementEnded) yield break;
        Vector3 landing = lastPoint;
        SetPhase(1);
        foreach (Pokemon victim in InArea())
        {
            if (!combat.HitEmboarTarget(victim, EmboarMoveRules.Damage(actorId.Value, kind.Value))) continue;
            Slow(victim, 20, 1);
            if (boosted.Value && EmboarMoveRules.CanControl(actor, victim))
            {
                victim.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.55f, true, effectId));
                victim.ApplyKnockupRPC(0.9f, 0.55f);
            }
        }
        for (int stage = 1; stage <= 2; stage++)
        {
            yield return Wait(upgraded ? 0.2f : 0.28f);
            if (!Live) yield break;
            transform.position = landing + aim.Value * (stage * 2.4f);
            waveArea.size = new Vector3(3.2f + stage * 1.3f, 2, 2.2f);
            SetPhase((byte)(stage + 1));
            foreach (Pokemon victim in InBox())
                if (combat.HitEmboarTarget(victim, EmboarMoveRules.Damage(actorId.Value, kind.Value, stage))) Slow(victim, (short)(upgraded ? 30 : 25), 1.5f);
        }
        yield return Wait(0.3f);
    }
    private IEnumerator Ringbreaker()
    {
        combat.EmboarUnstoppable(0.9f);
        actor.Pokemon.RemoveShieldWithIDRPC(EmboarMoveRules.UniteShield);
        actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(actor.Pokemon.GetMaxHp() * 0.15f), EmboarMoveRules.UniteShield, 0, 4, true));
        yield return AwaitMovement(2);
        if (!Live || !movementEnded) yield break;
        bool hitPlayer = false;
        SetPhase(1);
        foreach (Pokemon victim in InArea())
        {
            if (!combat.HitEmboarTarget(victim, EmboarMoveRules.Damage(actorId.Value, kind.Value, 1))) continue;
            hitPlayer |= victim.Type == PokemonType.Player;
            Vector3 delta = victim.transform.position - transform.position; delta.y = 0;
            if (delta.sqrMagnitude <= EmboarMoveRules.UniteInnerRadius * EmboarMoveRules.UniteInnerRadius && EmboarMoveRules.CanControl(actor, victim))
            {
                victim.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.8f, true, effectId));
                victim.ApplyKnockupRPC(1.2f, 0.8f);
            }
            else Slow(victim, 35, 1.5f);
        }
        if (hitPlayer) combat.FillEmboarReckless();
        yield return Wait(0.65f);
    }
    public void ReportMovement(bool finished, Vector3 point)
    {
        if (!IsServer || !initialized || !Live || !EmboarMoveRules.Driven(kind.Value) || movementEnded || !EmboarMoveRules.Finite(point)) return;
        float maxRange = EmboarMoveRules.Range(kind.Value) + 0.5f;
        Vector3 delta = point - origin; delta.y = 0;
        float speed = kind.Value == EmboarCast.Unite ? 18 : kind.Value == EmboarCast.HeatCrash ? 9 : kind.Value == EmboarCast.Smog ? 13 : 10;
        if (delta.sqrMagnitude > maxRange * maxRange || Mathf.Abs(point.y - origin.y) > 3
            || (point - lastPoint).magnitude > speed * Mathf.Max(0, Age - lastReportAge) + 0.8f) return;
        float earliest = kind.Value == EmboarCast.Unite ? 0.45f : kind.Value == EmboarCast.HeatCrash ? 0.4f : kind.Value == EmboarCast.Smog ? 0.25f : 0;
        if (finished && Age < earliest) return;
        if ((kind.Value == EmboarCast.FlameCharge && (boosted.Value || Age >= EmboarMoveRules.ChargeRamp))
            || (kind.Value == EmboarCast.Unite && Age >= EmboarMoveRules.UniteWindup))
        {
            foreach (Pokemon victim in EmboarMoveRules.Targets(Physics.OverlapCapsule(lastPoint + Vector3.up * 0.7f, point + Vector3.up * 0.7f, 1)))
            {
                if (!EmboarMoveRules.CanHit(actor, victim) || hit.Contains(victim) || !combat.HitEmboarTarget(victim, EmboarMoveRules.Damage(actorId.Value, kind.Value))) continue;
                hit.Add(victim);
                if (kind.Value == EmboarCast.FlameCharge && EmboarMoveRules.CanControl(actor, victim))
                {
                    victim.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.55f, true, effectId));
                    victim.ApplyKnockupRPC(0.8f, 0.55f); Slow(victim, 20, 1.5f);
                }
            }
        }
        lastPoint = point; lastReportAge = Age;
        if (kind.Value != EmboarCast.Smog) transform.SetPositionAndRotation(point, Quaternion.LookRotation(aim.Value));
        if (finished) movementEnded = true;
    }
    private HashSet<Pokemon> InArea()
    {
        if (area == null) return new HashSet<Pokemon>();
        Vector3 scale = area.transform.lossyScale;
        float radius = area.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return EmboarMoveRules.Targets(Physics.OverlapSphere(area.transform.TransformPoint(area.center), radius));
    }
    private HashSet<Pokemon> InBox()
    {
        if (waveArea == null) return new HashSet<Pokemon>();
        Vector3 scale = waveArea.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return EmboarMoveRules.Targets(Physics.OverlapBox(waveArea.transform.TransformPoint(waveArea.center), Vector3.Scale(waveArea.size * 0.5f, scale), waveArea.transform.rotation));
    }
    private void Slow(Pokemon victim, short amount, float duration)
    {
        if (!EmboarMoveRules.CanControl(actor, victim)) return;
        victim.RemoveAllStatChangeWithIDRPC(effectId);
        victim.AddStatChange(new StatChange(amount, Stat.Speed, duration, true, false, true, effectId));
    }
    private void SetPhase(byte value) { phaseStarted.Value = NetworkManager.ServerTime.Time; phase.Value = value; }
    private void Update()
    {
        if (IsServer && initialized && !Live) { Despawn(); return; }
        if (!IsClient || actorId.Value == ulong.MaxValue || graphics == null) return;
        if (actor == null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId.Value, out var obj)) actor = obj.GetComponent<PlayerManager>();
        if (actor == null) return;
        bool attached = kind.Value == EmboarCast.FirePledge || kind.Value == EmboarCast.FlameCharge || kind.Value == EmboarCast.Reckless
            || ((kind.Value == EmboarCast.Unite || kind.Value == EmboarCast.HeatCrash) && phase.Value == 0);
        bool visible = !attached || actor.Vision == null || actor.Vision.IsRendered;
        graphics.SetActive(visible);
        if (!visible) return;
        if (shownPhase != phase.Value)
        {
            shownPhase = phase.Value;
            bool impact = (kind.Value == EmboarCast.Unite || kind.Value == EmboarCast.HeatCrash) && phase.Value > 0 || kind.Value == EmboarCast.Ember && phase.Value == 2;
            bool release = kind.Value == EmboarCast.FlameCharge && phase.Value == 0 || kind.Value == EmboarCast.Flamethrower && phase.Value == 1;
            if (effectAudio != null && (impact || release)) effectAudio.PlayOneShot(impact ? impactSound : rushSound);
        }
        graphics.transform.SetPositionAndRotation(attached ? actor.transform.position : transform.position, Quaternion.LookRotation(attached ? actor.transform.forward : aim.Value));
        if (telegraph != null)
        {
            bool show = phase.Value == 0 && (kind.Value == EmboarCast.HeatCrash || kind.Value == EmboarCast.Unite);
            telegraph.SetActive(show);
            if (show) telegraph.transform.position = goal.Value + Vector3.up * 0.06f;
        }
        if (body != null) body.SetActive(kind.Value == EmboarCast.FirePledge || kind.Value == EmboarCast.Reckless || kind.Value == EmboarCast.FlameCharge
            || kind.Value == EmboarCast.Smog && phase.Value >= 1 || kind.Value == EmboarCast.Ember && phase.Value == 1 || kind.Value == EmboarCast.Flamethrower && phase.Value == 1
            || (kind.Value == EmboarCast.Unite || kind.Value == EmboarCast.HeatCrash) && phase.Value == 0);
        if (burst != null)
        {
            bool show = kind.Value == EmboarCast.HeatCrash && phase.Value == 1 || kind.Value == EmboarCast.Unite && phase.Value >= 1 || kind.Value == EmboarCast.Ember && phase.Value == 2;
            burst.SetActive(show);
            if (show)
            {
                float radius = kind.Value == EmboarCast.Unite ? EmboarMoveRules.UniteRadius : kind.Value == EmboarCast.HeatCrash ? phase.Value == 1 ? EmboarMoveRules.CrashRadius : 1.7f + phase.Value * 0.4f : 0.6f;
                burst.transform.localScale = Vector3.one * radius * Mathf.Lerp(0.15f, 1, Mathf.Clamp01(PhaseAge / 0.2f));
            }
        }
        if (waveFront != null)
        {
            bool show = kind.Value == EmboarCast.HeatCrash && phase.Value >= 2;
            waveFront.SetActive(show);
            if (show) waveFront.transform.localScale = new Vector3(3.2f + (phase.Value - 1) * 1.3f, 1, 2.2f);
        }
        if (pledgePips != null)
            for (int i = 0; i < pledgePips.Length; i++) if (pledgePips[i] != null) pledgePips[i].SetActive(i < actor.MovesController.EmboarPledgeHits);
        PresentLeap();
    }
    private void PresentLeap()
    {
        if (kind.Value != EmboarCast.HeatCrash) return;
        // Once landed, this lingering wave effect must not overwrite a newer move's model offset.
        if (phase.Value > 0) { RestoreModel(); return; }
        Transform model = actor.Pokemon.ActiveModel != null ? actor.Pokemon.ActiveModel.transform : null;
        if (model != liftedModel) { RestoreModel(); liftedModel = model; if (model != null) modelOrigin = model.localPosition; }
        if (liftedModel != null)
        {
            float progress = phase.Value == 0 ? Mathf.Clamp01(PhaseAge / EmboarMoveRules.CrashLeap) : 1;
            liftedModel.localPosition = modelOrigin + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.85f);
        }
    }
    private void RestoreModel() { if (liftedModel != null) liftedModel.localPosition = modelOrigin; liftedModel = null; }
    private void OnDeath(DamageInfo damage) { Despawn(); }
    public void Despawn() { if (IsServer && IsSpawned) NetworkObject.Despawn(true); }
    public override void OnNetworkDespawn()
    {
        StopAllCoroutines(); RestoreModel();
        if (!cleaned)
        {
            cleaned = true;
            if (actor != null) actor.Pokemon.OnDeath -= OnDeath;
            if (IsServer)
            {
                EmboarEffectIds.Release(effectId);
                if (actor != null && kind.Value == EmboarCast.FlameCharge) actor.Pokemon.RemoveStatusEffectWithID(effectId);
                if (combat != null) combat.EndEmboarObject(cast);
            }
        }
        base.OnNetworkDespawn();
    }
    public override void OnDestroy()
    {
        RestoreModel();
        if (holdsAsset && asset.IsValid()) Addressables.Release(asset);
        holdsAsset = false; base.OnDestroy();
    }
}
