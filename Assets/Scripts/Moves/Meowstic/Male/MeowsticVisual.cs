using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Only animates authored prefab geometry. Gameplay never waits for visual loads.
public class MeowsticVisual : MonoBehaviour
{
    [SerializeField] private GameObject graphics;
    [SerializeField] private Transform motion, wave, timer;
    [SerializeField] private GameObject[] attackPips = Array.Empty<GameObject>(), enemyPips = Array.Empty<GameObject>(), storagePips = Array.Empty<GameObject>();
    [SerializeField] private GameObject boostedReady;
    [SerializeField] private AudioSource sound;
    private MovesController actor;
    private MeowsticEffect effect;
    private ulong targetId;
    private float duration, started, size;
    private int detail;
    private bool initialized, holdsAsset;
    private AsyncOperationHandle<GameObject> handle;
    private Action onDestroyed;
    private Transform liftedModel;
    private Vector3 modelPosition;
    private Vector3 timerScale;
    private MeowsticProjectileMotion projectileMotion;

    public void Initialize(MovesController actor, MeowsticEffect effect, float duration, float age, ulong target,
        float size, int detail, AsyncOperationHandle<GameObject> handle, Action onDestroyed)
    {
        this.actor = actor; this.effect = effect; this.duration = duration; targetId = target;
        this.size = size; this.detail = detail; this.handle = handle; this.onDestroyed = onDestroyed;
        holdsAsset = true; initialized = true; started = Time.time - age;
        if (timer != null) timerScale = timer.localScale;
        transform.localScale = new Vector3(size, 1, size);
        if (effect == MeowsticEffect.Unite && actor.Pokemon.ActiveModel != null)
        {
            liftedModel = actor.Pokemon.ActiveModel.transform; modelPosition = liftedModel.localPosition;
        }
        if (Projectile)
        {
            if (effect == MeowsticEffect.Basic || effect == MeowsticEffect.Boosted)
                transform.position = MeowsticMoveRules.BasicAttackOrigin(actor.Pokemon, effect == MeowsticEffect.Boosted);
            // Age still controls effect expiry, but must not teleport a newly
            // loaded projectile downrange before its first visible frame.
            projectileMotion = new MeowsticProjectileMotion(transform.position, Time.time);
        }
        Update();
        if (sound != null && sound.clip != null && graphics.activeSelf && age < 0.2f) sound.Play();
    }
    private bool Projectile => effect == MeowsticEffect.Basic || effect == MeowsticEffect.Boosted || effect == MeowsticEffect.Reflection;
    private Pokemon FindTarget()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) ? obj.GetComponent<Pokemon>() : null;
    }
    private void Update()
    {
        if (!initialized) return;
        float age = Time.time - started;
        if (actor == null || !actor.IsSpawned || (duration > 0 && age >= duration)) { Destroy(gameObject); return; }
        Pokemon target = FindTarget();
        if (targetId != ulong.MaxValue && target == null) { Destroy(gameObject); return; }
        bool visible = effect == MeowsticEffect.State || effect == MeowsticEffect.RoomAim || target == null
            || !target.TryGetComponent(out PlayerManager player) || player.Vision == null || player.Vision.IsRendered;
        graphics.SetActive(visible && (effect != MeowsticEffect.State || actor.Pokemon.CurrentHp > 0));
        if (Projectile)
        {
            if (target == null || target.CurrentHp <= 0) { Destroy(gameObject); return; }
            Vector3 goal = target.transform.position + Vector3.up * 0.8f;
            transform.position = projectileMotion.Step(goal, Time.time);
            if ((goal - transform.position).sqrMagnitude < 0.04f) { Destroy(gameObject); return; }
        }
        else if (target != null) transform.position = target.transform.position;
        if (motion != null) motion.localRotation = Quaternion.Euler(0, age * (effect == MeowsticEffect.MagicCoat ? -80 : 60), 0);
        if (wave != null)
        {
            float fraction = duration > 0 ? Mathf.Clamp01(age / duration) : 0;
            if (effect == MeowsticEffect.Leer) fraction = Mathf.Clamp01(age / 0.1f);
            if (effect == MeowsticEffect.Scratch) fraction = Mathf.Clamp01(age / 0.12f);
            float pulse = effect == MeowsticEffect.Detonation || effect == MeowsticEffect.Pulse || effect == MeowsticEffect.Leer || effect == MeowsticEffect.Scratch
                ? Mathf.Lerp(0.1f, 1, fraction) : 0.9f + Mathf.Sin(age * 7) * 0.04f;
            wave.localScale = new Vector3(pulse, 1, pulse);
        }
        if (timer != null) timer.localScale = Vector3.Scale(timerScale, new Vector3(Mathf.Max(0.02f, 1 - age / Mathf.Max(0.01f, duration)), 1, 1));
        if (effect == MeowsticEffect.State)
        {
            for (int i = 0; i < attackPips.Length; i++) attackPips[i].SetActive(i < actor.MeowsticAttackCharge);
            for (int i = 0; i < enemyPips.Length; i++) enemyPips[i].SetActive(actor.Pokemon.CurrentLevel >= 3 && i < actor.MeowsticNearbyEnemies);
            if (boostedReady != null) boostedReady.SetActive(actor.MeowsticAttackCharge == 2);
        }
        if (effect == MeowsticEffect.Reflect)
            for (int i = 0; i < storagePips.Length; i++) storagePips[i].SetActive(actor.MeowsticStoredDamage > i * Mathf.Max(1, detail) / storagePips.Length);
        if (liftedModel != null) liftedModel.localPosition = modelPosition + Vector3.up * (0.3f + Mathf.Sin(age * 4) * 0.04f);
    }
    private void OnDestroy()
    {
        if (liftedModel != null) liftedModel.localPosition = modelPosition;
        if (holdsAsset && handle.IsValid()) Addressables.Release(handle);
        holdsAsset = false; onDestroyed?.Invoke(); onDestroyed = null;
    }
}
