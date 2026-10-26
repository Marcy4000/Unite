using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Authored meshes/particles only. Gameplay never waits for this component.
public class VaporVisual : MonoBehaviour
{
    [SerializeField] private GameObject graphics, enemyTeeth;
    [SerializeField] private Transform accent, rotating, progress;
    [SerializeField] private AudioSource sound;
    [SerializeField] private SpriteRenderer[] swiftStars = Array.Empty<SpriteRenderer>();
    private PlayerManager actor;
    private Pokemon target;
    private VaporEffect effect;
    private Vector3 origin;
    private float elapsed, duration, size;
    private int detail;
    private bool initialized;
    private AsyncOperationHandle<GameObject> handle;
    private Action completed;
    public void Initialize(PlayerManager actor, VaporEffect effect, Vector3 origin, float duration, float age,
        ulong targetId, float size, int detail, AsyncOperationHandle<GameObject> handle, Action completed)
    {
        this.actor = actor; this.effect = effect; this.origin = origin; this.duration = duration;
        elapsed = age; this.size = size; this.detail = detail; this.handle = handle; this.completed = completed;
        initialized = true;
        BindTarget(targetId); transform.position = origin;
        var local = Aim.Instance != null ? Aim.Instance.GetComponent<PlayerManager>() : null;
        bool friendly = actor.IsOwner || (local != null && actor.CurrentTeam.IsOnSameTeam(local.CurrentTeam));
        if (enemyTeeth != null) enemyTeeth.SetActive(!friendly);
        var block = new MaterialPropertyBlock();
        bool swift = effect == VaporEffect.Swift || (effect == VaporEffect.Aim && detail == (int)VaporCast.Swift);
        block.SetColor("_BaseColor", friendly ? swift ? new Color(1, 0.82f, 0.3f) : new Color(0.16f, 0.8f, 1) : new Color(1, 0.3f, 0.25f));
        foreach (MeshRenderer renderer in graphics.GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.name.Contains("Boundary") || (swift && effect == VaporEffect.Aim)) renderer.SetPropertyBlock(block);
        Apply(0);
        if (sound != null && age < 0.18f && effect != VaporEffect.Aim && effect != VaporEffect.BoostReady) sound.Play();
    }
    private void BindTarget(ulong id)
    {
        target = null;
        if (actor.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)) target = obj.GetComponent<Pokemon>();
    }
    public void UpdateAim(Vector3 center, ulong targetId) { origin = center; BindTarget(targetId); }
    private void Update()
    {
        if (!initialized) return;
        if (actor == null || !actor.IsSpawned || (duration > 0 && elapsed >= duration)) { Destroy(gameObject); return; }
        elapsed += Time.deltaTime; Apply(Time.deltaTime);
    }
    private bool Attached => effect == VaporEffect.AquaRing || effect == VaporEffect.HelpingHand || effect == VaporEffect.DiveWake
        || effect == VaporEffect.Hydration || effect == VaporEffect.BoostReady || (effect == VaporEffect.HealPulse && target != null)
        || (effect == VaporEffect.Aim && VaporMoveRules.IsSupport((VaporCast)detail));
    private void Apply(float delta)
    {
        // Serialized enum/asset retained for compatibility, but the generic
        // personal counter now owns readiness. Never render this old decoration.
        if (effect == VaporEffect.BoostReady) { graphics.SetActive(false); return; }
        if (Attached)
        {
            if (!VaporMoveRuntime.Living(target)) { graphics.SetActive(false); return; }
            bool visible = !target.TryGetComponent(out Vision vision) || (vision.IsRendered && vision.IsVisiblyEligible);
            graphics.SetActive(visible);
            transform.position = target.transform.position;
        }
        else if (effect != VaporEffect.Basic && effect != VaporEffect.Boosted) transform.position = origin;
        float normalized = duration > 0 ? Mathf.Clamp01(elapsed / duration) : 0;
        if (effect == VaporEffect.Basic || effect == VaporEffect.Boosted)
        {
            if (!VaporMoveRuntime.Living(target)) { Destroy(gameObject); return; }
            Vector3 goal = target.transform.position + Vector3.up * 0.7f;
            // Advance from cast origin once on late load; thereafter follow the same speed as the server.
            if (delta == 0) transform.position = origin;
            transform.position = Vector3.MoveTowards(transform.position, goal, VaporMoveRules.BasicSpeed * (delta > 0 ? delta : elapsed));
            Vector3 direction = goal - transform.position;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            return;
        }
        if (effect == VaporEffect.Swift)
        {
            accent.localScale = new Vector3(size, 1, size);
            ApplySwiftStars();
            if (progress != null) progress.localScale = Vector3.one * (1 - normalized);
            return;
        }
        if (effect == VaporEffect.Whirlpool || effect == VaporEffect.Rain || effect == VaporEffect.Aim
            || effect == VaporEffect.DiveWake || effect == VaporEffect.DiveWarning || effect == VaporEffect.TempestWarning)
        {
            accent.localScale = new Vector3(size, 1, size);
            if (rotating != null)
            {
                float angular = effect == VaporEffect.Whirlpool ? 120 + 120 * Mathf.Clamp01(elapsed / VaporMoveRules.WhirlpoolCollapse) : 35;
                rotating.localRotation = Quaternion.Euler(0, -elapsed * angular, 0);
                if (effect == VaporEffect.Whirlpool) rotating.localScale = Vector3.one * (elapsed < VaporMoveRules.WhirlpoolCollapse
                    ? Mathf.Lerp(1, 0.65f, elapsed / VaporMoveRules.WhirlpoolCollapse) : 1);
            }
            if (progress != null) progress.localScale = Vector3.one * (1 - normalized);
        }
        else if (effect == VaporEffect.AquaRing || effect == VaporEffect.HelpingHand || effect == VaporEffect.Hydration || effect == VaporEffect.BoostReady)
        {
            if (rotating != null) rotating.localRotation = Quaternion.Euler(0, elapsed * 85, 0);
            if (effect == VaporEffect.AquaRing && detail > 0 && !target.HasShieldWithID((ushort)detail)) graphics.SetActive(false);
            accent.localScale = Vector3.one * (1 + Mathf.Sin(elapsed * 5) * 0.035f);
        }
        else
        {
            float scale = effect == VaporEffect.Collapse ? Mathf.Lerp(1, 0.12f, normalized) : Mathf.Lerp(0.2f, 1, Mathf.Clamp01(normalized * 1.7f));
            accent.localScale = new Vector3(size * scale, 1, size * scale);
            if (progress != null) progress.localScale = Vector3.one * (1 - normalized);
        }
    }
    private void ApplySwiftStars()
    {
        // Swift belongs to Eevee: the same camera-facing yellow stars as the
        // other Eevees, falling once per existing 0.7s damage volley, not water.
        float phase = Mathf.Repeat(elapsed, 0.7f) / 0.7f;
        foreach (var star in swiftStars)
        {
            if (star == null) continue;
            Vector3 position = star.transform.localPosition;
            position.y = Mathf.Lerp(2.2f, 0.16f, phase);
            star.transform.localPosition = position;
            star.transform.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.25f, phase);
            star.color = new Color(1, 1, 1, Mathf.Clamp01(phase / 0.06f));
        }
    }
    private void OnDestroy()
    {
        if (initialized && handle.IsValid()) Addressables.Release(handle);
        completed?.Invoke(); completed = null;
    }
}
