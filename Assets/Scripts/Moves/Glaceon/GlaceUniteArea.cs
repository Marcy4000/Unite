using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GlaceUniteArea : NetworkBehaviour
{
    private DamageInfo damageInfo;
    private Team orangeTeam;
    private readonly NetworkVariable<ulong> glaceonID = new NetworkVariable<ulong>();
    private readonly NetworkVariable<bool> zoneActive = new NetworkVariable<bool>(false);
    private float activeTimer, glaceonSpearsTimer = 1;
    private readonly StatChange glaceonBuff = new StatChange(50, Stat.Speed, 6, true, true, true, 0);
    private readonly StatChange enemiesDebuff = new StatChange(50, Stat.Speed, 1, true, false, true, 0);
    private readonly List<Pokemon> pokemonInZone = new List<Pokemon>();
    private readonly Dictionary<Pokemon, HashSet<Collider>> collidersInZone = new Dictionary<Pokemon, HashSet<Collider>>();
    private Coroutine buffRoutine, debuffRoutine, damageRoutine;
    private PlayerManager attacker;
    private bool initialized;
    public event System.Action onGiveGlaceonSpears;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void InitializeRPC(Vector3 position, Quaternion rotation, DamageInfo info, Team team, ulong id)
        => InitializeServer(position, rotation, info, team, id);
    public void InitializeServer(Vector3 position, Quaternion rotation, DamageInfo info, Team team, ulong id)
    {
        if (!IsServer || initialized || !CombatTargeting.Finite(position)
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(info.attackerId, out var source)
            || !source.TryGetComponent(out attacker)) return;
        initialized = true;
        transform.SetPositionAndRotation(position, rotation);
        damageInfo = info;
        orangeTeam = team;
        glaceonID.Value = id;
        activeTimer = 8.5f;
        glaceonSpearsTimer = 1;
        zoneActive.Value = true;
        buffRoutine = StartCoroutine(ApplyBuff());
        debuffRoutine = StartCoroutine(ApplyDebuff());
        damageRoutine = StartCoroutine(DamageEnemies());
    }
    private void Update()
    {
        if (!IsServer || !IsSpawned || !zoneActive.Value) return;
        glaceonSpearsTimer -= Time.deltaTime;
        if (glaceonSpearsTimer <= 0)
        {
            foreach (var pokemon in GetLivePokemonInZone())
                if (pokemon.NetworkObjectId == glaceonID.Value) { onGiveGlaceonSpears?.Invoke(); break; }
            glaceonSpearsTimer = 1;
        }
        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0) { zoneActive.Value = false; NetworkObject.Despawn(true); }
    }
    private IEnumerator DamageEnemies()
    {
        yield return new WaitForSeconds(0.2f);
        if (!IsSpawned || !zoneActive.Value) yield break;
        foreach (var pokemon in GetLivePokemonInZone())
            if (GlaceonCharacterRuntime.CanHit(attacker, pokemon)) pokemon.ResolveDamage(damageInfo);
    }
    private IEnumerator ApplyBuff()
    {
        while (IsSpawned && zoneActive.Value)
        {
            foreach (var pokemon in GetLivePokemonInZone())
                if (pokemon.NetworkObjectId == glaceonID.Value) { pokemon.AddStatChange(glaceonBuff); break; }
            yield return new WaitForSeconds(6);
        }
    }
    private IEnumerator ApplyDebuff()
    {
        while (IsSpawned && zoneActive.Value)
        {
            foreach (var pokemon in GetLivePokemonInZone())
                if (GlaceonCharacterRuntime.CanHit(attacker, pokemon)) pokemon.AddStatChange(enemiesDebuff);
            yield return new WaitForSeconds(1);
        }
    }
    private IEnumerable<Pokemon> GetLivePokemonInZone()
    {
        pokemonInZone.RemoveAll(pokemon => pokemon == null || !pokemon.IsSpawned);
        foreach (var pokemon in pokemonInZone.ToArray())
            if (pokemon != null && pokemon.IsSpawned) yield return pokemon;
    }
    public override void OnNetworkDespawn()
    {
        if (buffRoutine != null) StopCoroutine(buffRoutine);
        if (debuffRoutine != null) StopCoroutine(debuffRoutine);
        if (damageRoutine != null) StopCoroutine(damageRoutine);
        buffRoutine = debuffRoutine = damageRoutine = null;
        pokemonInZone.Clear();
        collidersInZone.Clear();
        onGiveGlaceonSpears = null;
        base.OnNetworkDespawn();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !CombatTargeting.TryGetPokemon(other, out var pokemon)) return;
        if (!collidersInZone.TryGetValue(pokemon, out var colliders))
        {
            collidersInZone[pokemon] = colliders = new HashSet<Collider>();
            if (!pokemonInZone.Contains(pokemon)) pokemonInZone.Add(pokemon);
        }
        colliders.Add(other);
    }
    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        var pokemon = other.GetComponentInParent<Pokemon>();
        if (pokemon == null || !collidersInZone.TryGetValue(pokemon, out var colliders)) return;
        colliders.Remove(other);
        if (colliders.Count == 0) { collidersInZone.Remove(pokemon); pokemonInZone.Remove(pokemon); }
    }
}
