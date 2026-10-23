using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Each cast owns an authored prefab, a hit set, and its Addressables handle.
// Only the server queries hitboxes or applies damage. NGO replicates the visual.
public class CinderMoveObject : NetworkBehaviour
{
    [SerializeField] private CinderCast kind;
    [SerializeField] private GameObject graphics, travel, impact;
    [SerializeField] private SphereCollider hitbox, explosionArea;
    [SerializeField] private GameObject[] markPips;
    private readonly NetworkVariable<ulong> actorId = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<ulong> attachedId = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<byte> phase = new NetworkVariable<byte>();
    private readonly NetworkVariable<byte> marks = new NetworkVariable<byte>();
    private readonly HashSet<Pokemon> hit = new HashSet<Pokemon>();
    private MovesController combat;
    private PlayerManager actor;
    private Pokemon target;
    private int cast;
    private Vector3 direction, movementEndpoint;
    private bool upgraded, boosted, healing, slow, movementFinished, kickHit, initialized;
    private AsyncOperationHandle<GameObject> handle;
    private bool holdsAsset;
    private float presentationAge;
    private byte previousPhase = byte.MaxValue;
    private Vision attachedVision;

    public void HoldAsset(AsyncOperationHandle<GameObject> asset) { handle = asset; holdsAsset = true; }
    public override void OnNetworkSpawn()
    {
        graphics.SetActive(false);
        if (IsServer && kind != CinderCast.Cinders && kind != CinderCast.Burn) StartCoroutine(Watchdog());
    }
    private IEnumerator Watchdog() { yield return new WaitForSeconds(12); Despawn(); }
    public void Initialize(MovesController owner, PlayerManager player, CinderCast castKind, int id, Vector3 aim, Pokemon victim,
        bool upgrade, bool boost, bool heal, bool slowing)
    {
        if (!IsServer || initialized) return;
        initialized = true; combat = owner; actor = player; kind = castKind; cast = id; direction = aim;
        target = victim; upgraded = upgrade; boosted = boost; healing = heal; slow = slowing;
        actorId.Value = actor.NetworkObjectId;
        attachedId.Value = (kind == CinderCast.Cinders || kind == CinderCast.Burn) && target != null ? target.NetworkObjectId : actor.NetworkObjectId;
        actor.Pokemon.OnDeath += OnDeath;
        StartCoroutine(Run());
    }
    private IEnumerator Run()
    {
        if (kind == CinderCast.Cinders || kind == CinderCast.Burn || kind == CinderCast.Blaze)
        {
            float maximum = kind == CinderCast.Blaze ? 5f : float.PositiveInfinity;
            float started = Time.time;
            while (Live && Time.time - started < maximum && target != null && target.IsSpawned && target.CurrentHp > 0)
            {
                if (kind == CinderCast.Cinders)
                {
                    int count = combat.GetCinderaceMarks(target); marks.Value = (byte)count;
                    if (count == 0) break;
                }
                if (kind == CinderCast.Burn && !combat.HasCinderaceBurn(target)) break;
                transform.position = target.transform.position; yield return null;
            }
        }
        else if (CinderMoveRules.Dash(kind)) yield return Dash();
        else if (kind == CinderCast.BlazeKick) yield return Kick();
        else if (kind == CinderCast.Feint)
        {
            combat.ApplyCinderaceFeint(upgraded);
            float until = Time.time + 0.73f;
            while (Live && Time.time < until) { transform.position = actor.transform.position; yield return null; }
        }
        else yield return Projectile();
        Despawn();
    }
    private bool Live => actor != null && actor.IsSpawned && actor.Pokemon.CurrentHp > 0 && IsSpawned;

    private IEnumerator Projectile()
    {
        if (kind == CinderCast.Unite)
        {
            combat.CinderaceUnstoppable(CinderMoveRules.UniteImmune, 1);
            combat.CinderBuff(CinderMoveRules.UniteSpeed, Stat.Speed, 30, 6, true);
            combat.CinderBuff(CinderMoveRules.UniteAttackSpeed, Stat.AtkSpeed, 35, 6, false);
            actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(actor.Pokemon.GetMaxHp() * 0.2f), CinderMoveRules.UniteShield, 0, 6, true));
        }
        float windup = CinderMoveRules.Windup(kind), start = Time.time;
        while (Live && Time.time - start < windup)
        {
            if (kind != CinderCast.Unite && actor.Pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts)) { Despawn(); yield break; }
            transform.position = actor.transform.position + Vector3.up * (kind == CinderCast.Unite ? 2.2f : 0.8f);
            yield return null;
        }
        if (!Live) yield break;
        phase.Value = 1;
        transform.position = actor.transform.position + Vector3.up * 0.8f;
        Vector3 origin = transform.position;
        // Unite aims at the target's location when kicked, not a free ground aim.
        Vector3 destination = target != null ? target.transform.position + Vector3.up * 0.7f : origin + direction * CinderMoveRules.Range(kind);
        float speed = kind == CinderCast.Unite ? 16f : kind == CinderCast.PyroBall ? 22f : 24f;
        float end = Time.time + 3;
        bool reduced = false;
        while (Live && Time.time < end)
        {
            Vector3 from = transform.position;
            if (kind == CinderCast.PyroBall)
            {
                Vector3 next = from + direction * (speed * Time.deltaTime);
                if ((next - origin).magnitude > CinderMoveRules.Range(kind)) next = origin + direction * CinderMoveRules.Range(kind);
                foreach (Pokemon victim in Sweep(from, next))
                {
                    if (!hit.Add(victim)) continue;
                    if (combat.CinderaceHit(victim, CinderMoveRules.Damage(actorId.Value, kind)))
                    {
                        combat.ApplyCinderaceBurn(victim);
                        if (upgraded && !reduced) { reduced = true; combat.ReduceCinderaceMobilityCooldown(); }
                    }
                }
                transform.position = next;
                if ((next - origin).sqrMagnitude >= CinderMoveRules.Range(kind) * CinderMoveRules.Range(kind) - 0.01f) break;
            }
            else
            {
                if (kind != CinderCast.Unite)
                {
                    if (!CinderMoveRules.CanHit(actor, target)) break;
                    destination = target.transform.position + Vector3.up * 0.7f;
                }
                transform.position = Vector3.MoveTowards(from, destination, speed * Time.deltaTime);
                if ((transform.position - destination).sqrMagnitude < 0.04f)
                {
                    if (kind == CinderCast.Unite)
                    {
                        bool any = false;
                        foreach (Pokemon victim in Area(explosionArea))
                        {
                            var damage = CinderMoveRules.Damage(actorId.Value, kind);
                            damage.baseDmg = (short)Mathf.Min(short.MaxValue, damage.baseDmg + Mathf.FloorToInt(victim.GetMissingHp() * 0.14f));
                            any |= combat.CinderaceHit(victim, damage);
                        }
                        if (any) combat.CinderBuff(CinderMoveRules.UniteHitSpeed, Stat.Speed, 10, 5, true);
                    }
                    else
                    {
                        var damage = CinderMoveRules.Damage(actorId.Value, kind, boosted);
                        if (healing) damage.proprieties &= ~DamageProprieties.CanCrit;
                        if (kind == CinderCast.Basic)
                        {
                            // Resolve once, before computing the total wild damage cap.
                            if ((damage.proprieties & DamageProprieties.CanCrit) != 0 && Random.Range(0, 100) < actor.Pokemon.GetCritRate())
                                damage.proprieties |= DamageProprieties.WasCriticalHit;
                            damage.proprieties &= ~DamageProprieties.CanCrit;
                            if (target.Type != PokemonType.Player)
                            {
                                int raw = Mathf.FloorToInt(damage.ratio * actor.Pokemon.GetAttack());
                                if ((damage.proprieties & DamageProprieties.WasCriticalHit) != 0) raw += Mathf.FloorToInt(raw * actor.Pokemon.GetCritModifier());
                                damage.multiplier = raw > 0 ? Mathf.Min(1, (boosted ? 1300f : 1000f) / raw) : 1;
                            }
                        }
                        if (combat.CinderaceHit(target, damage, healing, slow) && kind == CinderCast.Ember) combat.ApplyCinderaceBurn(target);
                    }
                    phase.Value = 2; yield return new WaitForSeconds(0.25f); break;
                }
            }
            yield return null;
        }
    }
    private IEnumerator Dash()
    {
        Vector3 previous = actor.transform.position;
        Vector3 origin = previous;
        float until = Time.time + 2f, finishAt = float.PositiveInfinity;
        while (Live && Time.time < until)
        {
            if (actor.Pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts) || actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) break;
            // The reliable phase message carries the final endpoint, since it
            // can arrive ahead of the last unreliable NetworkTransform update.
            Vector3 current = movementFinished ? movementEndpoint : actor.transform.position;
            transform.SetPositionAndRotation(current, Quaternion.LookRotation(direction));
            if ((current - previous).sqrMagnitude <= 36 && (current - origin).sqrMagnitude <= 36)
                foreach (Pokemon victim in Sweep(previous + Vector3.up * 0.7f, current + Vector3.up * 0.7f))
                    if (hit.Add(victim)) combat.CinderaceHit(victim, CinderMoveRules.Damage(actorId.Value, kind));
            previous = current;
            if (movementFinished && float.IsPositiveInfinity(finishAt)) finishAt = Time.time + 0.15f;
            if (Time.time >= finishAt) break;
            yield return null;
        }
        if (Live && movementFinished && upgraded && kind == CinderCast.FlameCharge) combat.ArmCinderaceFlameSlow();
    }
    private IEnumerator Kick()
    {
        combat.CinderaceUnstoppable(CinderMoveRules.KickImmune, 2);
        float until = Time.time + 2;
        while (Live && !movementFinished && Time.time < until)
        {
            transform.position = actor.transform.position; yield return null;
        }
        if (actor != null && actor.IsSpawned) actor.Pokemon.RemoveStatusEffectWithID(CinderMoveRules.KickImmune);
    }
    public void MovementPhase(bool finished, Vector3 position)
    {
        if (!IsServer || !Live) return;
        if (CinderMoveRules.Dash(kind)) { movementFinished = finished; movementEndpoint = position; return; }
        if (kind != CinderCast.BlazeKick) return;
        if (!kickHit && !finished && CinderMoveRules.CanHit(actor, target)
            && Vector3.Distance(position, target.transform.position) <= 2.5f)
        {
            kickHit = true;
            bool applied = combat.CinderaceHit(target, CinderMoveRules.Damage(actorId.Value, kind));
            if (!applied) return;
            combat.ApplyCinderaceBurn(target);
            if (target.CurrentHp > 0)
            {
                target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));
                if (!target.HasStatusEffect(StatusType.Unstoppable) && !target.HasStatusEffect(StatusType.HindranceResistance)) target.ApplyKnockbackRPC(direction, 8);
            }
            if (upgraded) combat.CinderBuff(CinderMoveRules.KickSpeed, Stat.AtkSpeed, 30, 3, false);
        }
        if (finished) movementFinished = true;
    }
    private HashSet<Pokemon> Sweep(Vector3 from, Vector3 to)
    {
        return Targets(Physics.OverlapCapsule(from, to, hitbox.radius));
    }
    private HashSet<Pokemon> Area(SphereCollider area) => Targets(Physics.OverlapSphere(transform.TransformPoint(area.center), area.radius));
    private HashSet<Pokemon> Targets(Collider[] colliders)
    {
        var result = new HashSet<Pokemon>();
        foreach (Collider collider in colliders)
            if (collider.TryGetComponent(out Pokemon victim) && CinderMoveRules.CanHit(actor, victim)) result.Add(victim);
        return result;
    }
    private void Update()
    {
        if (IsServer && initialized && !Live) { Despawn(); return; }
        if (!IsClient || actorId.Value == ulong.MaxValue) return;
        bool attached = kind == CinderCast.Cinders || kind == CinderCast.Burn || kind == CinderCast.Blaze || kind == CinderCast.Feint
            || kind == CinderCast.BlazeKick || CinderMoveRules.Dash(kind);
        bool visible = true;
        if (attached)
        {
            if (attachedVision == null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(attachedId.Value, out var body))
                attachedVision = body.GetComponentInChildren<Vision>();
            visible = attachedVision != null && attachedVision.IsRendered;
            if (kind == CinderCast.Cinders)
                visible &= NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId.Value, out var source) && source.OwnerClientId == NetworkManager.LocalClientId;
        }
        if (!visible) { graphics.SetActive(false); return; }
        if (!graphics.activeSelf) graphics.SetActive(true);
        if (previousPhase != phase.Value) { previousPhase = phase.Value; presentationAge = 0; }
        presentationAge += Time.deltaTime;
        travel.SetActive(phase.Value != 2); impact.SetActive(phase.Value == 2);
        if (phase.Value == 2) impact.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.2f, presentationAge / 0.25f);
        if (kind == CinderCast.Cinders)
            for (int i = 0; i < markPips.Length; i++) markPips[i].SetActive(i < marks.Value);
        if (kind == CinderCast.Blaze || kind == CinderCast.Feint) travel.transform.localRotation = Quaternion.Euler(0, presentationAge * 240, 0);
    }
    private void OnDeath(DamageInfo damage) { Despawn(); }
    public void Despawn() { if (IsServer && IsSpawned) NetworkObject.Despawn(true); }
    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        if (actor != null)
        {
            actor.Pokemon.OnDeath -= OnDeath;
            if (IsServer && actor.IsSpawned)
            {
                if (kind == CinderCast.BlazeKick) actor.Pokemon.RemoveStatusEffectWithID(CinderMoveRules.KickImmune);
                if (kind == CinderCast.Unite) actor.Pokemon.RemoveStatusEffectWithID(CinderMoveRules.UniteImmune);
                if (kind == CinderCast.Cinders) combat.CinderaceMarksVisualEnded(target);
                combat.EndCinderaceObject(cast);
            }
        }
        base.OnNetworkDespawn();
    }
    public override void OnDestroy()
    {
        if (holdsAsset && handle.IsValid()) Addressables.Release(handle);
        holdsAsset = false; base.OnDestroy();
    }
}
