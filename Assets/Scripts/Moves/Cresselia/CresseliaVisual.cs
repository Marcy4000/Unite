using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Presentation only. No hit detection, healing, shields or gameplay timers live here.
public class CresseliaVisual : MonoBehaviour
{
    [SerializeField] private GameObject graphics, escortGlow, boostGlow;
    [SerializeField] private Transform accent;
    [SerializeField] private AudioSource sound;
    private PlayerManager actor;
    private Pokemon target;
    private CresseliaEffect effect;
    private Vector3 origin, direction;
    private float elapsed, duration, size, initialShield;
    private int detail;
    private bool initialized, playedSound, wasBoostReady;
    private float nextGroundCheck;
    private AsyncOperationHandle<GameObject> handle;
    private Action completed;

    public void Initialize(PlayerManager actor, CresseliaEffect effect, Vector3 origin, Vector3 direction,
        float duration, float age, ulong targetId, float size, int detail, AsyncOperationHandle<GameObject> handle, Action completed)
    {
        this.actor = actor; this.effect = effect; this.origin = origin;
        this.direction = CresseliaMoveRules.Direction(direction, actor.transform.forward);
        this.duration = duration; elapsed = age; this.size = size; this.detail = detail;
        if (effect == CresseliaEffect.LunarShield) initialShield = size;
        this.handle = handle; this.completed = completed; initialized = true;
        bool needsTarget = effect == CresseliaEffect.Basic || effect == CresseliaEffect.Boosted || effect == CresseliaEffect.Cleanse
            || effect == CresseliaEffect.LunarShield || effect == CresseliaEffect.ShieldHeal || effect == CresseliaEffect.ShieldBreak
            || effect == CresseliaEffect.BeamHeal || effect == CresseliaEffect.UniteFlight || (effect == CresseliaEffect.UniteWarning && detail == 1);
        if (needsTarget && actor.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)) target = obj.GetComponent<Pokemon>();
        ApplyTeamColors();
        transform.position = origin;
        ApplyPresentation(0);
    }

    private void ApplyTeamColors()
    {
        if (effect != CresseliaEffect.Moonlight && effect != CresseliaEffect.UniteWarning) return;
        PlayerManager local = Aim.Instance != null ? Aim.Instance.GetComponent<PlayerManager>() : null;
        bool friendly = actor.IsOwner || (local != null && actor.CurrentTeam.IsOnSameTeam(local.CurrentTeam));
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", friendly ? new Color(1, 0.83f, 0.4f) : new Color(1, 0.3f, 0.5f));
        foreach (MeshRenderer renderer in graphics.GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.name.Contains("Boundary")) renderer.SetPropertyBlock(block);
    }

    private void Update()
    {
        if (!initialized) return;
        if (actor == null || !actor.IsSpawned || (effect != CresseliaEffect.Passive && elapsed >= duration)) { Destroy(gameObject); return; }
        elapsed += Time.deltaTime;
        ApplyPresentation(Time.deltaTime);
    }

    private void ApplyPresentation(float delta)
    {
        bool alive = actor.Pokemon.CurrentHp > 0;
        bool personal = effect == CresseliaEffect.Passive || effect == CresseliaEffect.UniteFlight;
        bool visible = !personal || alive;
        if (target != null && effect != CresseliaEffect.UniteWarning && target.TryGetComponent(out Vision vision))
            visible &= vision.IsRendered && vision.IsVisiblyEligible;
        graphics.SetActive(visible);
        if (effect == CresseliaEffect.Passive)
        {
            transform.SetPositionAndRotation(actor.transform.position, Quaternion.identity);
            bool ready = actor.MovesController.CresseliaBoostRemaining <= 0;
            escortGlow.SetActive(alive && actor.MovesController.CresseliaEscorting);
            boostGlow.SetActive(alive && ready);
            if (ready && !wasBoostReady && sound != null && elapsed > 0.5f) sound.Play();
            wasBoostReady = ready;
            accent.localRotation = Quaternion.Euler(0, elapsed * 32f, 0);
            return;
        }
        if (!playedSound && visible)
        {
            playedSound = true;
            // Do not play a delayed cast sound after a slow asset load.
            if (sound != null && elapsed < 0.18f) sound.Play();
        }
        if (effect == CresseliaEffect.Basic || effect == CresseliaEffect.Boosted)
        {
            if (target == null || !target.IsSpawned || target.CurrentHp <= 0) { Destroy(gameObject); return; }
            Vector3 goal = target.transform.position + Vector3.up * 0.7f;
            transform.position = Vector3.MoveTowards(transform.position, goal, CresseliaMoveRules.BasicSpeed * (delta > 0 ? delta : elapsed));
            Vector3 facing = goal - transform.position;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
            accent.localRotation = Quaternion.Euler(0, 0, elapsed * 220);
        }
        else if (effect == CresseliaEffect.AuroraBeam || effect == CresseliaEffect.Moonblast)
        {
            transform.position = origin + direction * elapsed * CresseliaMoveRules.BeamSpeed;
            accent.localRotation = Quaternion.Euler(0, 0, elapsed * (effect == CresseliaEffect.Moonblast ? 240 : 0));
        }
        else if (effect == CresseliaEffect.Psybeam)
        {
            float growth = Mathf.Clamp01(elapsed / 0.08f);
            accent.localScale = new Vector3(size, 1, growth);
        }
        else if (effect == CresseliaEffect.Moonlight)
        {
            accent.localScale = new Vector3(size, 1, size);
            float fade = Mathf.Clamp01((duration - elapsed) / 0.4f);
            // The boundary stays at the gameplay radius; expiration is shown by
            // the inner lunar glyph, not by shrinking the area misleadingly.
            if (boostGlow != null) boostGlow.transform.localScale = Vector3.one * (0.9f + 0.1f * Mathf.Sin(elapsed * 3)) * fade;
        }
        else if (effect == CresseliaEffect.UniteWarning)
        {
            if (target != null && Time.time >= nextGroundCheck)
            {
                nextGroundCheck = Time.time + 0.1f;
                if (actor.PlayerMovement.TryCresseliaLanding(target.transform.position, out Vector3 ground)) transform.position = ground;
            }
            accent.localScale = new Vector3(size, 1, size);
            if (boostGlow != null) boostGlow.transform.localRotation = Quaternion.Euler(0, elapsed * 140, 0);
        }
        else if (effect == CresseliaEffect.BeamHeal)
            transform.position = target != null ? Vector3.Lerp(origin, target.transform.position + Vector3.up, Mathf.Clamp01(elapsed / duration)) : origin;
        else if (effect == CresseliaEffect.LunarShield || effect == CresseliaEffect.Cleanse || effect == CresseliaEffect.ShieldHeal
            || effect == CresseliaEffect.ShieldBreak || effect == CresseliaEffect.UniteFlight)
        {
            if (target == null || !target.IsSpawned || target.CurrentHp <= 0) { Destroy(gameObject); return; }
            transform.position = target.transform.position;
            accent.localRotation = Quaternion.Euler(0, elapsed * 65, 0);
            if (effect == CresseliaEffect.LunarShield)
            {
                float remaining = target.GetShieldWithID((ushort)detail)?.Amount ?? 0;
                initialShield = Mathf.Max(initialShield, remaining);
                float fraction = initialShield > 0 ? remaining / initialShield : 1;
                accent.localScale = Vector3.one * Mathf.Lerp(0.75f, 1, fraction);
            }
            else if (effect != CresseliaEffect.UniteFlight) accent.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.25f, elapsed / duration);
        }
        else
        {
            float expansion = Mathf.Lerp(0.15f, 1, Mathf.Clamp01(elapsed / (duration * 0.7f)));
            accent.localScale = new Vector3(size * expansion, 1, size * expansion);
        }
    }

    private void OnDestroy()
    {
        if (initialized && handle.IsValid()) Addressables.Release(handle);
        completed?.Invoke(); completed = null;
    }
}
