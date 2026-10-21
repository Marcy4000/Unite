using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Cosmetic only: loading speed and client visibility never decide damage.
// All geometry/audio is authored into prefabs by the editor builder.
public class MarshadowVisual : MonoBehaviour
{
    [SerializeField] private GameObject graphics;
    [SerializeField] private Transform accent;
    [SerializeField] private GameObject[] pips;
    [SerializeField] private AudioSource sound;
    [SerializeField] private float impactDelay;
    private PlayerManager actor;
    private Pokemon target;
    private MarshadowEffect effect;
    private Vector3 origin, direction;
    private float remaining, elapsed, duration;
    private int stage;
    private bool initialized;
    private bool playedSound;
    private AsyncOperationHandle<GameObject> handle;
    private Action completed;

    public void Initialize(PlayerManager actor, MarshadowEffect effect, Vector3 origin, Vector3 direction, float remaining,
        ulong targetId, int stage, float age, AsyncOperationHandle<GameObject> handle, Action completed)
    {
        this.actor = actor; this.effect = effect; this.origin = origin;
        this.direction = MarshadowMoveRules.Direction(direction, actor.transform.forward);
        this.remaining = remaining; duration = remaining + age; elapsed = age;
        this.stage = stage; this.handle = handle; this.completed = completed; initialized = true;
        if ((effect == MarshadowEffect.AuraMark || targetId != 0) && actor.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)) target = obj.GetComponent<Pokemon>();
        if (pips != null) for (int i = 0; i < pips.Length; i++) pips[i].SetActive(i < stage);
        if (effect == MarshadowEffect.Rush) accent.localScale *= stage == 2 ? 1.3f : 0.85f;
        ApplyPresentation();
    }
    private void Update()
    {
        if (!initialized) return;
        if (actor == null || !actor.IsSpawned || actor.Pokemon.CurrentHp <= 0 || remaining <= 0
            || (effect == MarshadowEffect.AuraMark && (target == null || !target.IsSpawned || target.CurrentHp <= 0)))
        { Destroy(gameObject); return; }
        elapsed += Time.deltaTime; remaining -= Time.deltaTime;
        ApplyPresentation();
    }
    private void ApplyPresentation()
    {
        bool friendly = actor.IsOwner || (LobbyController.Instance != null && actor.CurrentTeam.Team == LobbyController.Instance.GetLocalPlayerTeam());
        bool ownDecoration = effect == MarshadowEffect.Passive || effect == MarshadowEffect.AuraMark
            || effect == MarshadowEffect.ShadowSneak || effect == MarshadowEffect.PhantomForce;
        bool follows = ownDecoration || effect == MarshadowEffect.Punch || effect == MarshadowEffect.BoostedPunch
            || effect == MarshadowEffect.DrainPunch || effect == MarshadowEffect.Rush || effect == MarshadowEffect.Kick || effect == MarshadowEffect.MirrorShadow;
        bool visible = !ownDecoration || friendly;
        if (follows && !friendly && actor.TryGetComponent(out Vision vision)) visible &= vision.IsRendered && vision.IsVisiblyEligible;
        if (effect == MarshadowEffect.AuraMark && target != null && target.TryGetComponent(out Vision targetVision)) visible &= targetVision.IsRendered;
        float delay = effect == MarshadowEffect.Kick && stage == 1 ? 0.22f : impactDelay;
        graphics.SetActive(visible && elapsed >= delay);
        if (sound != null && !playedSound && graphics.activeSelf)
        {
            playedSound = true;
            if (elapsed - delay < 0.18f) sound.Play();
        }
        if (effect == MarshadowEffect.AuraSphere) transform.position = origin + direction * (elapsed * MarshadowMoveRules.AuraSpeed) + Vector3.up * 0.7f;
        else if (effect == MarshadowEffect.AuraMark && target != null) transform.position = target.transform.position;
        else if (effect == MarshadowEffect.Theft) transform.position = Vector3.Lerp(origin, actor.transform.position, Mathf.Clamp01(elapsed / duration)) + Vector3.up;
        else if (follows) transform.SetPositionAndRotation(actor.transform.position, actor.transform.rotation);
        if (effect == MarshadowEffect.Passive || effect == MarshadowEffect.AuraMark || effect == MarshadowEffect.ShadowSneak || effect == MarshadowEffect.PhantomForce)
            accent.localRotation = Quaternion.Euler(0, elapsed * 65f, 0);
        if (effect == MarshadowEffect.SpectralThief)
        {
            float scale = stage == 3 ? 5f / MarshadowMoveRules.SpectralRadius : Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(elapsed / 0.15f));
            accent.localScale = Vector3.one * scale;
        }
        if (effect == MarshadowEffect.EchoBurst) accent.localScale = Vector3.one * stage * Mathf.Lerp(0.75f, 1, Mathf.Clamp01(elapsed / 0.15f));
        if (effect == MarshadowEffect.MirrorShadow) accent.localRotation = Quaternion.Euler(0, elapsed * 230f, 0);
        if (effect == MarshadowEffect.Kick) accent.localRotation = Quaternion.Euler(0, Mathf.Lerp(-45, 45, Mathf.Clamp01(elapsed / duration)), 0);
    }
    private void OnDestroy()
    {
        if (initialized && handle.IsValid()) Addressables.Release(handle);
        completed?.Invoke(); completed = null;
    }
}
