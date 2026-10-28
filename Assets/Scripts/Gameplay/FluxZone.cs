using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class FluxZone : NetworkBehaviour
{
    [SerializeField] private Team team;
    [SerializeField] private int laneID;
    [SerializeField] private int tier;
    [SerializeField] private GameObject graphic;

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>();
    private HashSet<Pokemon> pokemonInZone = new HashSet<Pokemon>();
    private readonly Dictionary<Pokemon, HashSet<Collider>> overlaps = new();
    private readonly Dictionary<Pokemon, StatChange> appliedChanges = new();

    public Team Team => team;
    public int LaneID => laneID;
    public int Tier => tier;
    public bool IsActive => isActive.Value;

    public override void OnNetworkSpawn()
    {
        isActive.OnValueChanged += OnActiveValueChanged;
        if (IsServer)
        {
            isActive.Value = true;
        }
        OnActiveValueChanged(false, isActive.Value);
    }

    public void OnActiveValueChanged(bool oldValue, bool newValue)
    {
        if (graphic != null) graphic.SetActive(newValue);
        if (!newValue)
        {
            ClearZoneEffects();
        }
    }

    private void ClearZoneEffects()
    {
        var changes = new List<KeyValuePair<Pokemon, StatChange>>(appliedChanges);
        appliedChanges.Clear();
        overlaps.Clear();
        pokemonInZone.Clear();
        foreach (var entry in changes)
            if (entry.Key != null && entry.Key.IsSpawned && entry.Key.IsServer)
                entry.Key.RemoveStatChangeRPC(entry.Value);
    }

    private void OnDisable() => ClearZoneEffects();

    public override void OnNetworkDespawn()
    {
        isActive.OnValueChanged -= OnActiveValueChanged;
        ClearZoneEffects();
        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !IsActive || !IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (pokemon.Type != PokemonType.Player)
            {
                return;
            }

            if (!overlaps.TryGetValue(pokemon, out var colliders))
            {
                colliders = new HashSet<Collider>();
                overlaps.Add(pokemon, colliders);
            }
            if (!colliders.Add(other) || !pokemonInZone.Add(pokemon)) return;
            int value = pokemon.TeamMember.IsOnSameTeam(team) ? 60 : 50;
            StatChange statChange = new StatChange((short)value, Stat.Speed, 0, false, pokemon.TeamMember.IsOnSameTeam(team), true, 1);
            appliedChanges.Add(pokemon, statChange);
            pokemon.AddStatChange(statChange);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!overlaps.TryGetValue(pokemon, out var colliders) || !colliders.Remove(other) || colliders.Count > 0)
            {
                return;
            }
            pokemonInZone.Remove(pokemon);
            overlaps.Remove(pokemon);
            if (appliedChanges.Remove(pokemon, out var change)) pokemon.RemoveStatChangeRPC(change);
        }
    }

    public void SetIsActive(bool value)
    {
        if (IsServer)
        {
            isActive.Value = value;
        }
        else
        {
            SetIsActiveRPC(value);
        }
    }

    [Rpc(SendTo.Server)]
    private void SetIsActiveRPC(bool value)
    {
        isActive.Value = value;
    }

    private void LateUpdate()
    {
        if (!IsServer)
        {
            return;
        }

        if (isActive.Value)
        {
            pokemonInZone.RemoveWhere(pokemon => pokemon == null);
        }
    }
}
