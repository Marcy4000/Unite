using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class HypervoiceHitbox : NetworkBehaviour
{
    private int wavesAmount = SylvMoveRules.HyperVoiceWavesNormal;
    private int wavesRemaining = SylvMoveRules.HyperVoiceWavesNormal;
    private int wavesFired;

    private DamageInfo closeDamage;
    private DamageInfo farDamage;
    private Team teamToIgnore;
    private PlayerManager actor;
    private Vector3 lockedDirection = Vector3.forward;

    private readonly List<Pokemon> pokemonInTrigger = new List<Pokemon>();
    private readonly HashSet<Pokemon> waveTargets = new HashSet<Pokemon>();
    private float cooldown = 0.1f;
    private AsyncOperationHandle<GameObject> asset;

    public int WavesFired => wavesFired;
    public bool HasValidAssetHandle => asset.IsValid();

    public void HoldAsset(AsyncOperationHandle<GameObject> handle) => asset = handle;

    public void InitializeServer(PlayerManager owner, Vector3 direction, DamageInfo close, DamageInfo far, Team team, int waves)
    {
        actor = owner;
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        lockedDirection = flat.sqrMagnitude > 0.001f ? flat.normalized : (owner != null ? owner.transform.forward : Vector3.forward);
        transform.SetPositionAndRotation(owner != null ? owner.transform.position : transform.position, Quaternion.LookRotation(lockedDirection));
        ApplyConfig(close, far, team, waves);
        if (IsSpawned)
            InitializeRPC(close, far, team, waves);
    }

    [Rpc(SendTo.Everyone)]
    public void InitializeRPC(DamageInfo close, DamageInfo far, Team team, int waves)
    {
        ApplyConfig(close, far, team, waves);
    }

    private void ApplyConfig(DamageInfo close, DamageInfo far, Team team, int waves)
    {
        closeDamage = close;
        farDamage = far;
        teamToIgnore = team;
        wavesAmount = Mathf.Max(1, waves);
        wavesRemaining = wavesAmount;
        wavesFired = 0;
        cooldown = 0.1f;
    }

    public void DespawnSelf()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsServer) NetworkObject.Despawn(true);
            else DespawnRPC();
        }
        else if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    [Rpc(SendTo.Server)]
    public void DespawnRPC()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private void LateUpdate()
    {
        if (!IsServer) return;

        if (actor != null)
        {
            if (!actor.IsSpawned || actor.Pokemon == null || actor.Pokemon.CurrentHp <= 0)
            {
                DespawnSelf();
                return;
            }
            transform.SetPositionAndRotation(actor.transform.position, Quaternion.LookRotation(lockedDirection));
        }

        cooldown -= Time.deltaTime;
        while (cooldown <= 0f && wavesRemaining > 0)
        {
            FireWave();
            wavesRemaining--;
            wavesFired++;
            cooldown += wavesAmount > 1 ? 2.2f / (wavesAmount - 1) : SylvMoveRules.HyperVoiceDuration;
        }
    }

    private void FireWave()
    {
        waveTargets.Clear();
        for (int i = pokemonInTrigger.Count - 1; i >= 0; i--)
        {
            Pokemon candidate = pokemonInTrigger[i];
            if (candidate == null)
            {
                pokemonInTrigger.RemoveAt(i);
                continue;
            }
            if (CanHit(candidate))
                waveTargets.Add(candidate);
        }

        foreach (Pokemon candidate in CombatTargeting.SectorTargets(transform.position, transform.forward, SylvMoveRules.HyperVoiceRange, 60f))
        {
            if (CanHit(candidate))
                waveTargets.Add(candidate);
        }

        foreach (Pokemon target in waveTargets)
        {
            DamageInfo damageToUse = Vector3.Distance(target.transform.position, transform.position) > SylvMoveRules.HyperVoiceFarDistance
                ? farDamage
                : closeDamage;
            target.TakeDamageRPC(damageToUse);
        }
    }

    private bool CanHit(Pokemon pokemon)
    {
        if (pokemon == null || !pokemon.IsSpawned || pokemon.CurrentHp <= 0)
            return false;
        if (actor != null && pokemon == actor.Pokemon)
            return false;
        if (pokemon.NetworkObjectId == closeDamage.attackerId && closeDamage.attackerId != 0)
            return false;
        if (pokemon.TeamMember.Team == teamToIgnore)
            return false;
        if (pokemon.HasStatusEffect(StatusType.Invincible) || pokemon.HasStatusEffect(StatusType.Untargetable))
            return false;
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || other == null) return;
        Pokemon pokemon = other.GetComponent<Pokemon>() ?? other.GetComponentInParent<Pokemon>();
        if (CanHit(pokemon) && !pokemonInTrigger.Contains(pokemon))
            pokemonInTrigger.Add(pokemon);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer || other == null) return;
        Pokemon pokemon = other.GetComponent<Pokemon>() ?? other.GetComponentInParent<Pokemon>();
        if (pokemon != null)
            pokemonInTrigger.Remove(pokemon);
    }

    public override void OnDestroy()
    {
        if (asset.IsValid())
        {
            Addressables.Release(asset);
            asset = default;
        }
        base.OnDestroy();
    }
}
