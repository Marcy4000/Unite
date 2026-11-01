using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
    private bool initialized, upgraded, holdsAsset, cleaned;
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
    public void InitializeCombat(PlayerManager player, FlareonCast move, Vector3 direction, Pokemon victim, bool plus)
    {
        if (!IsServer || initialized || player == null) return;
        initialized = true; actor = player; combat = player.MovesController; target = victim; upgraded = plus;
        actorId.Value = player.NetworkObjectId; kind.Value = move; aim.Value = direction;
        started.Value = phaseStarted.Value = NetworkManager.ServerTime.Time;
        actor.Pokemon.OnDeath += OnDeath;
    }
    public void SetCombatPhase(byte value)
    {
        if (IsServer && initialized) SetPhase(value);
    }
    public void MoveCombatObject(Vector3 position, Vector3 direction)
    {
        if (!IsServer || !initialized) return;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(FlareonMoveRules.Direction(direction, transform.forward)));
    }
    public void DamageCombatArea(FlareonCharacterRuntime runtime, FlareonCast move, float burnDuration = 0)
    {
        if (!IsServer || !Live || runtime == null) return;
        foreach (Pokemon victim in InArea())
            runtime.HitTarget(victim, FlareonMoveRules.Damage(actorId.Value, move, upgraded, victim.HasStatusEffect(StatusType.Burned)), burnDuration);
    }
    public void SweepCombatFlame(FlareonCharacterRuntime runtime, float progress)
    {
        if (!IsServer || !Live || runtime == null || sweep == null || flameArea == null) return;
        sweep.localRotation = Quaternion.Euler(0, Mathf.Lerp(-30, 30, Mathf.Clamp01(progress)), 0);
        foreach (Pokemon victim in InFlame())
        {
            if (!FlareonMoveRules.CanHit(actor, victim) || !hit.Add(victim)) continue;
            runtime.HitTarget(victim, FlareonMoveRules.Damage(actorId.Value, FlareonCast.Flamethrower, upgraded), 3);
        }
    }
    public bool SweepCombatTravel(FlareonCharacterRuntime runtime, FlareonCast move, Vector3 previous, Vector3 point, bool cooldownRefunded)
    {
        if (!IsServer || !Live || runtime == null) return cooldownRefunded;
        foreach (Pokemon victim in FlareonMoveRules.Targets(Physics.OverlapCapsule(previous + Vector3.up * 0.6f, point + Vector3.up * 0.6f, 0.8f)))
        {
            if (!FlareonMoveRules.CanHit(actor, victim) || !hit.Add(victim)) continue;
            float burn = move == FlareonCast.FlareBlitz ? upgraded ? 3 : 2 : 0;
            if (!runtime.HitTarget(victim, FlareonMoveRules.Damage(actorId.Value, move), burn)) continue;
            if (move != FlareonCast.FlareBlitz) continue;
            if (FlareonMoveRules.CanControl(actor, victim))
            {
                victim.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.35f, true, FlareonMoveRules.LaunchId));
                victim.ApplyKnockupRPC(0.8f, 0.35f);
            }
            if (!cooldownRefunded)
            {
                cooldownRefunded = true;
                actor.MovesController.Combat.ReduceCooldown(1, 3);
            }
        }
        return cooldownRefunded;
    }
    public bool LandCombatTakedown(FlareonCharacterRuntime runtime, bool slam)
    {
        if (!IsServer || !Live || runtime == null || !FlareonMoveRules.CanHit(actor, target)
            || (target.transform.position - transform.position).sqrMagnitude > 2.25f) return false;
        if (slam)
        {
            bool connected = runtime.HitTarget(target, FlareonMoveRules.Damage(actorId.Value, FlareonCast.Slam));
            if (connected && FlareonMoveRules.CanControl(actor, target))
                target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.2f, true, FlareonMoveRules.LaunchId));
            if (connected) Shield(FlareonMoveRules.SlamShield, 0.05f, 3);
            return connected;
        }
        bool launched = false;
        foreach (Pokemon victim in InArea())
        {
            bool connected = runtime.HitTarget(victim, FlareonMoveRules.Damage(actorId.Value, FlareonCast.Takedown, upgraded, victim.HasStatusEffect(StatusType.Burned)));
            if (victim == target) launched = connected;
        }
        if (!launched || !FlareonMoveRules.CanControl(actor, target)) return false;
        float duration = FlareonMoveRules.LaunchDuration(upgraded);
        target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, FlareonMoveRules.LaunchId));
        target.ApplyKnockupRPC(1, duration);
        return true;
    }
    public void LandCombatUnite(FlareonCharacterRuntime runtime)
    {
        if (!IsServer || !Live || runtime == null) return;
        foreach (Pokemon victim in InArea())
        {
            if (!FlareonMoveRules.CanHit(actor, victim)) continue;
            if (runtime.HitTarget(victim, FlareonMoveRules.Damage(actorId.Value, FlareonCast.Unite), 3))
                runtime.HitTarget(victim, new DamageInfo(actorId.Value, 0, 0, (short)Mathf.Min(short.MaxValue, Mathf.RoundToInt(victim.GetMissingHp() * 0.05f)), DamageType.Physical, DamageProprieties.IsUniteMove));
        }
        Shield(FlareonMoveRules.UniteShield, 0.12f, 3);
    }
    public void GrantCombatShield(ushort id, float ratio, float duration)
    {
        if (IsServer && Live) Shield(id, ratio, duration);
    }
    private IEnumerator Watchdog() { yield return new WaitForSeconds(20); if (kind.Value != FlareonCast.State || !initialized) Despawn(); }
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
        FlareonCharacterRuntime runtime = combat.FlareonRuntime;
        if (runtime != null)
        {
            if (basicStarted != runtime.BasicStarted)
            {
                basicStarted = runtime.BasicStarted;
                basicBoosted = runtime.BasicBoosted;
                basicDirection = runtime.BasicDirection;
                if (effectAudio != null && NetworkManager.ServerTime.Time - basicStarted < 0.3)
                {
                    effectAudio.pitch = basicBoosted ? 0.85f : 1.15f;
                    effectAudio.PlayOneShot(ignitionSound, 0.35f);
                }
            }
            if (contactUntil != runtime.ContactUntil)
            {
                contactUntil = runtime.ContactUntil;
                contactBoosted = runtime.ContactBoosted;
                if (effectAudio != null && NetworkManager.ServerTime.Time < contactUntil)
                    effectAudio.PlayOneShot(impactSound);
            }
        }
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
