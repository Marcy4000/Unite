using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Authored presentation, replicated phases, server hitboxes. The player root remains owner-driven.</summary>
public class FlareonMoveObject : NetworkBehaviour
{
    [SerializeField] private GameObject graphics, body, wave, impact, strike, recastMarker, fullHeat, flamePreview;
    [SerializeField] private Transform sweep;
    [SerializeField] private SphereCollider area;
    [SerializeField] private BoxCollider flameArea;
    // Existing prefabs may still contain these retired charge decorations.
    [SerializeField, HideInInspector] private GameObject[] attackPips;
    [SerializeField] private GameObject[] heatPips;
    [SerializeField] private AudioSource effectAudio;
    [SerializeField] private AudioClip ignitionSound, impactSound;
    private readonly NetworkVariable<ulong> actorId = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<FlareonCast> kind = new NetworkVariable<FlareonCast>();
    private readonly NetworkVariable<Vector3> aim = new NetworkVariable<Vector3>(Vector3.forward);
    private readonly NetworkVariable<byte> phase = new NetworkVariable<byte>();
    private readonly NetworkVariable<double> started = new NetworkVariable<double>(), phaseStarted = new NetworkVariable<double>();
    private static readonly Dictionary<ulong, FlareonMoveObject> stateViews = new Dictionary<ulong, FlareonMoveObject>();
    private readonly HashSet<Pokemon> hit = new HashSet<Pokemon>();
    private MovesController combat;
    private PlayerManager actor;
    private Pokemon target;
    private int cast;
    private bool initialized, upgraded, holdsAsset, movementEnded, refunded, cleaned;
    private Vector3 origin, lastPoint;
    private float lastReportAge;
    private AsyncOperationHandle<GameObject> asset;
    private Transform movedModel;
    private Vector3 modelOrigin;
    private double basicStarted = -100;
    private bool basicBoosted;
    private bool graphicsAttachedToActor;
    private double contactUntil;
    private bool contactBoosted;
    private Vector3 basicDirection;
    private byte shownPhase = byte.MaxValue;
    private bool Live => actor != null && actor.IsSpawned && actor.Pokemon.CurrentHp > 0 && IsSpawned;
    private float Age => (float)(NetworkManager.ServerTime.Time - started.Value);
    private float PhaseAge => (float)(NetworkManager.ServerTime.Time - phaseStarted.Value);

    private void Awake()
    {
        if (attackPips != null) foreach (GameObject pip in attackPips) if (pip != null) pip.SetActive(false);
    }

    public void HoldAsset(AsyncOperationHandle<GameObject> handle) { asset = handle; holdsAsset = true; }
    public override void OnNetworkSpawn() { if (graphics != null) graphics.SetActive(false); if (IsServer) StartCoroutine(Watchdog()); }
    public void Initialize(MovesController owner, PlayerManager player, FlareonCast move, int id, Vector3 direction, Pokemon victim, bool plus)
    {
        if (!IsServer || initialized) return;
        initialized = true; combat = owner; actor = player; target = victim; cast = id; upgraded = plus;
        origin = lastPoint = player.transform.position;
        actorId.Value = player.NetworkObjectId; kind.Value = move; aim.Value = direction;
        started.Value = phaseStarted.Value = NetworkManager.ServerTime.Time;
        actor.Pokemon.OnDeath += OnDeath; StartCoroutine(Run());
    }
    private IEnumerator Watchdog() { yield return new WaitForSeconds(20); if (kind.Value != FlareonCast.State || !initialized) Despawn(); }
    private IEnumerator Wait(float seconds, bool interruptible = false)
    {
        double until = NetworkManager.ServerTime.Time + seconds;
        while (Live && NetworkManager.ServerTime.Time < until)
        {
            if (interruptible && actor.Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) { Despawn(); yield break; }
            yield return null;
        }
    }
    private IEnumerator Run()
    {
        switch (kind.Value)
        {
            case FlareonCast.State: while (Live) yield return null; break;
            case FlareonCast.Swift:
                // Ten ticks over four seconds, independent of frame rate and animation length.
                for (int i = 0; i < 10 && Live; i++)
                {
                    yield return Wait(0.4f); if (!Live) break;
                    transform.position = actor.transform.position; SetPhase((byte)(i + 1));
                    foreach (Pokemon victim in InArea()) combat.HitFlareonTarget(victim, FlareonMoveRules.Damage(actorId.Value, kind.Value));
                }
                break;
            case FlareonCast.HeatWave:
                actor.Pokemon.RemoveAllStatChangeWithIDRPC(FlareonMoveRules.SpeedBuff);
                actor.Pokemon.AddStatChange(new StatChange(20, Stat.Speed, 6, true, true, true, FlareonMoveRules.SpeedBuff));
                for (int i = 0; i < FlareonMoveRules.HeatPulses(upgraded) && Live; i++)
                {
                    yield return Wait(i == 0 ? 0.35f : FlareonMoveRules.HeatInterval(upgraded)); if (!Live) break;
                    transform.position = actor.transform.position; SetPhase((byte)(i + 1));
                    foreach (Pokemon victim in InArea()) combat.HitFlareonTarget(victim, FlareonMoveRules.Damage(actorId.Value, kind.Value, upgraded, victim.HasStatusEffect(StatusType.Burned)));
                }
                yield return Wait(0.25f);
                break;
            case FlareonCast.Flamethrower:
                actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 0.65f, true, FlareonMoveRules.CastProtection));
                if (upgraded) Shield(FlareonMoveRules.FlameShield, 0.1f, 4);
                yield return Wait(0.2f); if (!Live) break;
                SetPhase(1);
                double end = NetworkManager.ServerTime.Time + 0.4;
                while (Live && NetworkManager.ServerTime.Time < end)
                {
                    transform.SetPositionAndRotation(actor.transform.position, Quaternion.LookRotation(aim.Value));
                    sweep.localRotation = Quaternion.Euler(0, Mathf.Lerp(-30, 30, Mathf.Clamp01(PhaseAge / 0.4f)), 0);
                    foreach (Pokemon victim in InFlame())
                    {
                        if (!FlareonMoveRules.CanHit(actor, victim) || !hit.Add(victim)) continue;
                        combat.HitFlareonTarget(victim, FlareonMoveRules.Damage(actorId.Value, kind.Value, upgraded), 3);
                    }
                    yield return null;
                }
                SetPhase(2); yield return Wait(0.12f); break;
            default:
                if (kind.Value == FlareonCast.Unite) actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0.65f, true, FlareonMoveRules.CastProtection));
                double timeout = NetworkManager.ServerTime.Time + 2;
                while (Live && !movementEnded && NetworkManager.ServerTime.Time < timeout)
                {
                    if (actor.Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts) || actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) { Despawn(); yield break; }
                    yield return null;
                }
                if (!Live || !movementEnded) break;
                transform.position = lastPoint; SetPhase(1);
                if (kind.Value == FlareonCast.Takedown || kind.Value == FlareonCast.Slam) LandTakedown();
                else if (kind.Value == FlareonCast.Unite)
                {
                    foreach (Pokemon victim in InArea())
                    {
                        if (!FlareonMoveRules.CanHit(actor, victim)) continue;
                        if (combat.HitFlareonTarget(victim, FlareonMoveRules.Damage(actorId.Value, kind.Value), 3))
                            combat.HitFlareonTarget(victim, new DamageInfo(actorId.Value, 0, 0, (short)Mathf.Min(short.MaxValue, Mathf.RoundToInt(victim.GetMissingHp() * 0.05f)), DamageType.Physical, DamageProprieties.IsUniteMove));
                    }
                    Shield(FlareonMoveRules.UniteShield, 0.12f, 3);
                }
                yield return Wait(kind.Value == FlareonCast.Unite ? 0.4f : 0.2f); break;
        }
        Despawn();
    }
    private void LandTakedown()
    {
        if (!FlareonMoveRules.CanHit(actor, target) || (target.transform.position - lastPoint).sqrMagnitude > 2.25f) return;
        bool slam = kind.Value == FlareonCast.Slam;
        if (slam)
        {
            bool connected = combat.HitFlareonTarget(target, FlareonMoveRules.Damage(actorId.Value, kind.Value));
            if (connected && FlareonMoveRules.CanControl(actor, target))
                target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.2f, true, FlareonMoveRules.LaunchId));
            if (connected) Shield(FlareonMoveRules.SlamShield, 0.05f, 3); return;
        }
        bool launched = false;
        foreach (Pokemon victim in InArea())
        {
            bool connected = combat.HitFlareonTarget(victim, FlareonMoveRules.Damage(actorId.Value, kind.Value, upgraded, victim.HasStatusEffect(StatusType.Burned)));
            if (victim == target) launched = connected;
        }
        if (launched && FlareonMoveRules.CanControl(actor, target))
        {
            float duration = FlareonMoveRules.LaunchDuration(upgraded);
            target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, FlareonMoveRules.LaunchId)); target.ApplyKnockupRPC(1, duration);
            combat.ArmFlareonRecast(target, duration - 0.08f);
        }
    }
    public void ReportMovement(bool finished, Vector3 point)
    {
        if (!IsServer || !initialized || !Live || !FlareonMoveRules.Driven(kind.Value) || movementEnded || !FlareonMoveRules.Finite(point)
            || actor.Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts) || actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) return;
        Vector3 delta = point - origin; delta.y = 0;
        float maxSpeed = FlareonMoveRules.Range(kind.Value) / FlareonMoveRules.DriveTime(kind.Value) + 4;
        if (delta.sqrMagnitude > Mathf.Pow(FlareonMoveRules.Range(kind.Value) + 0.8f, 2) || Mathf.Abs(point.y - origin.y) > 2
            || (point - lastPoint).magnitude > maxSpeed * Mathf.Max(0, Age - lastReportAge) + 0.8f
            || Age < FlareonMoveRules.Windup(kind.Value) - 0.04f) return;
        if (kind.Value == FlareonCast.Tackle || kind.Value == FlareonCast.FlareBlitz)
        {
            foreach (Pokemon victim in FlareonMoveRules.Targets(Physics.OverlapCapsule(lastPoint + Vector3.up * 0.6f, point + Vector3.up * 0.6f, 0.8f)))
            {
                if (!FlareonMoveRules.CanHit(actor, victim) || !hit.Add(victim)) continue;
                if (!combat.HitFlareonTarget(victim, FlareonMoveRules.Damage(actorId.Value, kind.Value), kind.Value == FlareonCast.FlareBlitz ? upgraded ? 3 : 2 : 0)) continue;
                if (kind.Value == FlareonCast.FlareBlitz)
                {
                    if (FlareonMoveRules.CanControl(actor, victim))
                    {
                        victim.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.35f, true, FlareonMoveRules.LaunchId)); victim.ApplyKnockupRPC(0.8f, 0.35f);
                    }
                    if (!refunded) { refunded = true; combat.ReduceFlareonCooldown(MoveType.MoveB, 3); }
                }
            }
        }
        lastPoint = point; lastReportAge = Age; transform.SetPositionAndRotation(point, Quaternion.LookRotation(aim.Value));
        if (finished) movementEnded = true;
    }
    private HashSet<Pokemon> InArea() => FlareonMoveRules.Targets(Physics.OverlapSphere(transform.TransformPoint(area.center), area.radius));
    private HashSet<Pokemon> InFlame() => FlareonMoveRules.Targets(Physics.OverlapBox(flameArea.transform.TransformPoint(flameArea.center), flameArea.size * 0.5f, flameArea.transform.rotation));
    private void Shield(ushort id, float ratio, float duration)
    {
        actor.Pokemon.RemoveShieldWithIDRPC(id); actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(actor.Pokemon.GetMaxHp() * ratio), id, 0, duration, true));
    }
    private void SetPhase(byte value) { phaseStarted.Value = NetworkManager.ServerTime.Time; phase.Value = value; }
    private void Update()
    {
        if (IsServer && initialized && !Live) { Despawn(); return; }
    }
    private void LateUpdate()
    {
        if (!IsClient || actorId.Value == ulong.MaxValue || graphics == null) return;
        if (actor == null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId.Value, out var obj)) { actor = obj.GetComponent<PlayerManager>(); combat = actor != null ? actor.MovesController : null; }
        if (actor == null || combat == null) return;
        bool attached = kind.Value == FlareonCast.State || kind.Value == FlareonCast.Swift || kind.Value == FlareonCast.HeatWave
            || FlareonMoveRules.Driven(kind.Value) && phase.Value == 0;
        bool visible = actor.Vision == null || actor.Vision.IsRendered;
        graphics.SetActive(visible); if (!visible) { RestoreModel(); return; }
        PositionGraphics(attached, kind.Value);
        if (shownPhase != phase.Value)
        {
            shownPhase = phase.Value;
            if (effectAudio != null && kind.Value != FlareonCast.State && kind.Value != FlareonCast.Swift)
                effectAudio.PlayOneShot(phase.Value == 0 ? ignitionSound : impactSound);
        }
        if (kind.Value == FlareonCast.State) { PresentState(); return; }
        if (body != null) body.SetActive(kind.Value == FlareonCast.Swift || kind.Value == FlareonCast.HeatWave || phase.Value == 0 || kind.Value == FlareonCast.Flamethrower && phase.Value == 1);
        if (sweep != null && kind.Value == FlareonCast.Flamethrower)
        {
            sweep.gameObject.SetActive(phase.Value == 1); sweep.localRotation = Quaternion.Euler(0, Mathf.Lerp(-30, 30, Mathf.Clamp01(PhaseAge / 0.4f)), 0);
        }
        if (wave != null)
        {
            bool show = kind.Value == FlareonCast.HeatWave && phase.Value > 0 && PhaseAge <= 0.3f;
            wave.SetActive(show); if (show) wave.transform.localScale = Vector3.one * FlareonMoveRules.HeatRadius * Mathf.Lerp(0.15f, 1, Mathf.Clamp01(PhaseAge / 0.25f));
        }
        if (impact != null)
        {
            bool show = phase.Value > 0 && kind.Value != FlareonCast.HeatWave && kind.Value != FlareonCast.Swift && kind.Value != FlareonCast.Flamethrower;
            impact.SetActive(show); if (show) impact.transform.localScale = Vector3.one * (kind.Value == FlareonCast.Unite ? 2 : 1) * Mathf.Lerp(0.15f, 1, Mathf.Clamp01(PhaseAge / 0.2f));
        }
        if (kind.Value == FlareonCast.Swift && body != null) body.transform.localRotation = Quaternion.Euler(0, Age * 450, 0);
        if ((kind.Value == FlareonCast.Unite || kind.Value == FlareonCast.Slam) && phase.Value == 0)
        {
            float progress = Mathf.Clamp01(Age / (FlareonMoveRules.Windup(kind.Value) + FlareonMoveRules.DriveTime(kind.Value)));
            MoveModel(Vector3.up * Mathf.Sin(progress * Mathf.PI) * (kind.Value == FlareonCast.Unite ? 1.4f : 0.8f));
        }
        else RestoreModel();
    }
    private void PositionGraphics(bool attached, FlareonCast move)
    {
        if (attached && (move == FlareonCast.Swift || move == FlareonCast.HeatWave))
        {
            // These auras move with the rendered actor, not their independently replicated hitbox root.
            // Otherwise a pulse coroutine or NetworkTransform interpolation can move the root again
            // after the world-space graphics position was set, producing a one-frame forward offset.
            if (graphics.transform.parent != actor.transform) graphics.transform.SetParent(actor.transform, false);
            graphicsAttachedToActor = true;
            graphics.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return;
        }
        RestoreGraphicsParent();
        graphics.transform.SetPositionAndRotation(attached ? actor.transform.position : transform.position, Quaternion.LookRotation(attached ? actor.transform.forward : aim.Value));
    }
    private void RestoreGraphicsParent()
    {
        if (!graphicsAttachedToActor) return;
        graphicsAttachedToActor = false;
        if (graphics != null) graphics.transform.SetParent(transform, false);
    }
    private void PresentState()
    {
        stateViews[actorId.Value] = this;
        bool owner = actor.IsOwner;
        for (int i = 0; i < heatPips.Length; i++)
        {
            heatPips[i].SetActive(owner && i < combat.FlareonAttackStacks);
            heatPips[i].transform.localScale = new Vector3(0.09f, 0.15f, 0.09f) * Mathf.Lerp(0.6f, 1, Mathf.Clamp01(combat.FlareonStackRemaining / 5));
        }
        if (fullHeat != null) fullHeat.SetActive(combat.FlareonAttackStacks == 3);
        if (recastMarker != null)
        {
            Pokemon victim = combat.FlareonRecastTarget;
            bool visible = victim != null && !victim.HasStatusEffect(StatusType.Invisible) && (!victim.TryGetComponent(out Vision vision) || vision.IsRendered);
            recastMarker.SetActive(owner && combat.FlareonRecastAvailable && visible);
            if (visible && combat.FlareonRecastAvailable) recastMarker.transform.position = victim.transform.position + Vector3.up * 0.12f;
        }
        if (flamePreview != null)
        {
            var move = combat.GetMove(MoveType.MoveB) as FlareonMoveBase;
            bool aiming = owner && move != null && move.Kind == FlareonCast.Flamethrower && move.IsActive;
            flamePreview.SetActive(aiming);
            if (aiming) flamePreview.transform.SetPositionAndRotation(actor.transform.position + Vector3.up * 0.04f, Quaternion.LookRotation(move.AimDirection));
        }
        float age = (float)(NetworkManager.ServerTime.Time - basicStarted);
        if (impact != null)
        {
            bool connected = NetworkManager.ServerTime.Time < contactUntil;
            impact.SetActive(connected);
            if (connected) { impact.transform.localPosition = new Vector3(0, 0.25f, 1.4f); impact.transform.localScale = Vector3.one * (contactBoosted ? 0.55f : 0.3f); }
        }
        if (strike != null)
        {
            bool show = age >= 0.07f && age < 0.26f; strike.SetActive(show);
            if (show)
            {
                strike.transform.SetPositionAndRotation(actor.transform.position + Vector3.up * 0.65f + basicDirection * 1.2f, Quaternion.LookRotation(basicDirection));
                strike.transform.localScale = Vector3.one * (basicBoosted ? 1.2f : 0.75f) * Mathf.Sin(Mathf.Clamp01((age - 0.07f) / 0.19f) * Mathf.PI);
            }
        }
        // Placeholder feedback only: no invented/missing basic-attack animation names.
        if (age >= 0 && age < 0.18f && actor.Pokemon.CurrentLevel >= 3)
            MoveModel(actor.transform.InverseTransformDirection(basicDirection) * Mathf.Sin(age / 0.18f * Mathf.PI) * 0.08f);
        else RestoreModel();
    }
    public static void PresentBasic(ulong id, bool boosted, Vector3 direction)
    {
        if (!stateViews.TryGetValue(id, out var view) || view == null) return;
        view.basicStarted = view.NetworkManager.ServerTime.Time; view.basicBoosted = boosted; view.basicDirection = direction;
        if (view.effectAudio != null) { view.effectAudio.pitch = boosted ? 0.85f : 1.15f; view.effectAudio.PlayOneShot(view.ignitionSound, 0.35f); }
    }
    public static void PresentContact(ulong id, bool boosted)
    {
        if (!stateViews.TryGetValue(id, out var view) || view == null) return;
        view.contactUntil = view.NetworkManager.ServerTime.Time + 0.12; view.contactBoosted = boosted;
        if (view.effectAudio != null) view.effectAudio.PlayOneShot(view.impactSound);
    }
    private void MoveModel(Vector3 offset)
    {
        Transform model = actor != null && actor.Pokemon.ActiveModel != null ? actor.Pokemon.ActiveModel.transform : null;
        if (movedModel != model) { RestoreModel(); movedModel = model; if (model != null) modelOrigin = model.localPosition; }
        if (movedModel != null) movedModel.localPosition = modelOrigin + offset;
    }
    private void RestoreModel() { if (movedModel != null) movedModel.localPosition = modelOrigin; movedModel = null; }
    private void OnDeath(DamageInfo damage) { Despawn(); }
    public void Despawn() { if (IsServer && IsSpawned) NetworkObject.Despawn(true); }
    public override void OnNetworkDespawn()
    {
        StopAllCoroutines(); RestoreModel();
        if (graphics != null) graphics.SetActive(false);
        RestoreGraphicsParent();
        if (!cleaned)
        {
            cleaned = true;
            if (kind.Value == FlareonCast.State && stateViews.TryGetValue(actorId.Value, out var view) && view == this) stateViews.Remove(actorId.Value);
            if (IsServer && actor != null) actor.Pokemon.OnDeath -= OnDeath;
            if (IsServer && combat != null) combat.EndFlareonObject(cast);
        }
        base.OnNetworkDespawn();
    }
    public override void OnDestroy()
    {
        RestoreModel();
        // Destruction without a network despawn must not orphan visual children on the actor.
        if (graphicsAttachedToActor && graphics != null) Destroy(graphics);
        graphicsAttachedToActor = false;
        if (holdsAsset && asset.IsValid()) Addressables.Release(asset);
        holdsAsset = false; base.OnDestroy();
    }
}
