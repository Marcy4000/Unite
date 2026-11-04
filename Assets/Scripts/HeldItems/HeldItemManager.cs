using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class HeldItemManager : NetworkBehaviour
{
    private readonly List<HeldItemRuntime> items = new List<HeldItemRuntime>();
    private readonly List<ushort> statIds = new List<ushort>();
    private readonly NetworkVariable<ulong> equipment = new NetworkVariable<ulong>();
    private PlayerManager player;
    private bool initialized;
    private float nextRegeneration;

    public bool Has(AvailableHeldItems item) => (equipment.Value & (1UL << (int)item)) != 0;

    public float DamageMultiplier
    {
        get
        {
            float multiplier = 1;
            foreach (HeldItemRuntime item in items) multiplier *= item.DamageMultiplier;
            return multiplier;
        }
    }

    public void Initialize(PlayerManager controller)
    {
        player = controller;
        if (!IsServer || initialized) return;
        var lobbyPlayer = player.LobbyPlayer;
        if (lobbyPlayer == null || lobbyPlayer.Data == null || !lobbyPlayer.Data.TryGetValue("HeldItems", out var selection)) return;
        initialized = true;
        int startingMaximumHp = player.Pokemon.GetMaxHp();
        foreach (HeldItemInfo info in HeldItemDatabase.DeserializeHeldItems(selection.Value))
        {
            if (info == null || info.heldItemID == AvailableHeldItems.None || Has(info.heldItemID)) continue;
            if (info.heldItemID == AvailableHeldItems.RustedSword && player.Pokemon.BaseStats.PokemonName != "Zacian") continue;
            equipment.Value |= 1UL << (int)info.heldItemID;
            for (int index = 0; index < info.StatBoosts.Length; index++)
            {
                HeldItemStatBoost boost = info.StatBoosts[index];
                if (boost == null) continue;
                ushort id = (ushort)(27000 + (int)info.heldItemID * 4 + index);
                statIds.Add(id);
                player.Pokemon.SetHeldItemModifier(id, new StatChange(boost.BoostAmount, boost.AffectedStat, 0, false, true, boost.IsPercentage, id, false));
            }
            var runtime = (HeldItemRuntime)HeldItemDatabase.GetHeldItem(info.heldItemID);
            runtime.Initialize(player);
            items.Add(runtime);
        }
        player.Pokemon.RefreshHeldItemStartingHealth(startingMaximumHp);
        nextRegeneration = Time.time + 5;
    }

    public void OnMove(MoveType slot)
    {
        if (!IsServer || player == null || player.Pokemon.CurrentHp <= 0) return;
        foreach (HeldItemRuntime item in items) item.Move(slot);
    }

    public void OnHit(Pokemon target, DamageInfo damage, int resolved, int healthDamage, int previousHp)
    {
        if (!IsServer || player == null || resolved <= 0 || target == null || target == player.Pokemon
            || target.TeamMember.IsOnSameTeam(player.CurrentTeam.Team)) return;
        foreach (HeldItemRuntime item in items.ToArray())
        {
            if (player == null || player.Pokemon.CurrentHp <= 0) return;
            item.Hit(target, damage, resolved, healthDamage, previousHp);
        }
    }

    public void OnTaken(Pokemon attacker, DamageInfo damage, int resolved)
    {
        if (!IsServer || player == null || player.Pokemon.CurrentHp <= 0 || resolved <= 0) return;
        foreach (HeldItemRuntime item in items.ToArray()) item.Taken(attacker, damage, resolved);
    }

    public void OnScore(int amount)
    {
        if (!IsServer || amount <= 0) return;
        foreach (HeldItemRuntime item in items) item.Score();
    }

    public void OnHindrance(Pokemon target)
    {
        if (!IsServer || target == null) return;
        foreach (HeldItemRuntime item in items) item.Hindrance(target);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SetScoringRPC(bool active)
    {
        if (player == null) return;
        if (active && (player.Pokemon.CurrentHp <= 0 || player.CurrentEnergy == 0 || player.GoalZone == null
            || !player.GoalZone.IsActive || player.GoalZone.Team == player.CurrentTeam.Team)) return;
        foreach (HeldItemRuntime item in items) item.Scoring(active);
    }

    private void Update()
    {
        if (!IsServer || player == null) return;
        if (!initialized) { Initialize(player); return; }
        if (GameManager.Instance != null && GameManager.Instance.GameState != GameState.Playing) return;
        foreach (HeldItemRuntime item in items) item.Update();
        if (player.Pokemon.CurrentHp > 0 && Time.time >= nextRegeneration)
        {
            nextRegeneration = Time.time + 5;
            player.Pokemon.HealEnvironmentRPC(Mathf.FloorToInt(player.Pokemon.GetPreciseStatChange(Stat.HpRecovery)));
        }
    }

    public override void OnNetworkPreDespawn()
    {
        DisposeHeldItems();
        base.OnNetworkPreDespawn();
    }

    public override void OnNetworkDespawn()
    {
        DisposeHeldItems();
        base.OnNetworkDespawn();
    }

    private void DisposeHeldItems()
    {
        foreach (HeldItemRuntime item in items) item.Dispose();
        if (IsServer && player != null && player.Pokemon != null)
        {
            foreach (ushort id in statIds) player.Pokemon.ClearHeldItemModifier(id);
            equipment.Value = 0;
        }
        statIds.Clear();
        items.Clear();
        initialized = false;
        player = null;
    }
}
