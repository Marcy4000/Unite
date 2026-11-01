using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class IcicleSpearHitbox : NetworkBehaviour
{
    private DamageInfo damageInfo;
    private readonly StatChange enemySlow = new StatChange(20, Stat.Speed, 0.75f, true, false, true, 0);
    private Team teamToIgnore;
    private bool isUpgraded, initialized;
    private readonly Dictionary<Pokemon, HashSet<Collider>> pokemonInTrigger = new Dictionary<Pokemon, HashSet<Collider>>();
    private float cooldown = 0.75f;
    private PlayerManager attacker;
    public DamageInfo DamageInfo => damageInfo;
    public Team TeamToIgnore => teamToIgnore;
    public bool IsUpgraded => isUpgraded;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void InitializeRPC(DamageInfo damage, Team team, bool upgraded) => InitializeServer(damage, team, upgraded);
    public void InitializeServer(DamageInfo damage, Team team, bool upgraded)
    {
        if (!IsServer || initialized || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(damage.attackerId, out var source)
            || !source.TryGetComponent(out attacker)) return;
        damageInfo = damage;
        teamToIgnore = team;
        isUpgraded = upgraded;
        cooldown = 0.75f;
        initialized = true;
    }
    private void Update()
    {
        if (!IsServer || !IsSpawned || !initialized) return;
        cooldown -= Time.deltaTime;
        if (cooldown > 0) return;
        cooldown = 0.75f;
        foreach (var pair in new List<KeyValuePair<Pokemon, HashSet<Collider>>>(pokemonInTrigger))
        {
            Pokemon pokemon = pair.Key;
            pair.Value.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
            if (pokemon == null || !pokemon.IsSpawned || pair.Value.Count == 0) { pokemonInTrigger.Remove(pokemon); continue; }
            if (!GlaceonCharacterRuntime.CanHit(attacker, pokemon)) continue;
            pokemon.ResolveDamage(damageInfo);
            pokemon.AddStatChange(enemySlow);
            if (isUpgraded)
            {
                int damage = Mathf.FloorToInt(pokemon.GetMissingHp() * 0.05f);
                if (pokemon.Type == PokemonType.Wild) damage = Mathf.Min(damage, 300);
                pokemon.ResolveDamage(new DamageInfo(damageInfo.attackerId, 0, 0, (short)Mathf.Clamp(damage, 0, short.MaxValue), DamageType.Special));
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !CombatTargeting.TryGetPokemon(other, out var pokemon)) return;
        if (!pokemonInTrigger.TryGetValue(pokemon, out var colliders)) pokemonInTrigger[pokemon] = colliders = new HashSet<Collider>();
        colliders.Add(other);
    }
    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        var pokemon = other.GetComponentInParent<Pokemon>();
        if (pokemon != null && pokemonInTrigger.TryGetValue(pokemon, out var colliders))
        {
            colliders.Remove(other);
            if (colliders.Count == 0) pokemonInTrigger.Remove(pokemon);
        }
    }
    public override void OnNetworkDespawn()
    {
        initialized = false;
        pokemonInTrigger.Clear();
        base.OnNetworkDespawn();
    }
}
