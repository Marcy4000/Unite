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
    private List<Pokemon> pokemonInZone = new List<Pokemon>();

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
    }

    public void OnActiveValueChanged(bool oldValue, bool newValue)
    {
        graphic.SetActive(newValue);
        if (!newValue)
        {
            foreach (var player in pokemonInZone)
            {
                player.RemoveStatChangeWithIDRPC(1);
            }
            pokemonInZone.Clear();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsActive || !IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            pokemonInZone.Add(pokemon);
            int value = pokemon.TeamMember.IsOnSameTeam(team) ? 60 : 50;
            StatChange statChange = new StatChange((short)value, Stat.Speed, 0, false, pokemon.TeamMember.IsOnSameTeam(team), true, 1);
            pokemon.AddStatChange(statChange);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsActive || !IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!pokemonInZone.Contains(pokemon))
            {
                return;
            }
            pokemonInZone.Remove(pokemon);
            pokemon.RemoveStatChangeWithIDRPC(1);
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
            pokemonInZone.RemoveAll(pokemon => pokemon == null);
        }
    }
}
