using Unity.Netcode;
using UnityEngine;
using System.Linq;

public class HeldItemRuntime : HeldItemBase
{
    private readonly AvailableHeldItems kind;
    private float nextUse;
    private float nextTick;
    private float windowEnd;
    private float damageBoostEnd;
    private int stacks;
    private int attacks;
    private int recoveryTicks;
    private float charge;
    private Vector3 lastPosition;
    private bool speedActive;
    private bool scoring;
    private Pokemon Pokemon => playerManager.Pokemon;
    private ushort ModifierId => (ushort)(26100 + (int)kind);

    public HeldItemRuntime(AvailableHeldItems item) { kind = item; Name = item.ToString(); }

    public override void Initialize(PlayerManager controller)
    {
        base.Initialize(controller);
        lastPosition = Pokemon.transform.position;
        nextTick = Time.time + 1;
        Pokemon.OnDeath += OnDeath;
        if (kind == AvailableHeldItems.WiseGlasses) SetModifier(7, Stat.SpAttack, true, 0, false);
        if (kind == AvailableHeldItems.AssaultVest) nextUse = Time.time + 15;
    }

    private void OnDeath(DamageInfo damage) => Reset();

    public override void Dispose()
    {
        if (playerManager == null || Pokemon == null) return;
        Pokemon.OnDeath -= OnDeath;
        Pokemon.ClearHeldItemModifier(ModifierId);
        if (Pokemon.IsSpawned)
        {
            if (kind == AvailableHeldItems.AssaultVest) Pokemon.RemoveShieldWithIDRPC(HeldItemRules.AssaultVestId);
            if (kind == AvailableHeldItems.ScoreShield) Pokemon.RemoveShieldWithIDRPC(HeldItemRules.ScoreShieldId);
        }
    }

    private void SetModifier(float amount, Stat stat, bool percentage, float duration, bool removable = true)
        => Pokemon.SetHeldItemModifier(ModifierId, new StatChange(amount, stat, duration, duration > 0, true, percentage, ModifierId, removable));

    private void Deal(Pokemon target, int amount, DamageType type = DamageType.Physical)
    {
        if (target == null || target.CurrentHp <= 0 || amount <= 0) return;
        target.ResolveDamage(new DamageInfo(Pokemon.NetworkObjectId, 0, 0, (short)Mathf.Min(short.MaxValue, amount), type,
            DamageProprieties.IsMuscleBand) { source = DamageSource.Item });
    }

    public void Hit(Pokemon target, DamageInfo damage, int resolved, int healthDamage, int previousHp)
    {
        bool attackBased = HeldItemRules.AttackBased(Pokemon, damage);
        if ((kind == AvailableHeldItems.CurseBangle && attackBased)
            || (kind == AvailableHeldItems.CurseIncense && !attackBased && damage.type != DamageType.True))
            target.ApplyHeldItemCurse();
        if (kind == AvailableHeldItems.DrainCrown && HeldItemRules.Primary(damage) && HeldItemRules.Basic(damage) && attackBased)
            Pokemon.HealDamageRPC(Mathf.FloorToInt(healthDamage * 0.15f));
        if (Pokemon.CurrentHp <= 0 || damage.source == DamageSource.Item
            || damage.proprieties.HasFlag(DamageProprieties.IsMuscleBand)) return;
        if (kind == AvailableHeldItems.ResonantGuard && target.Type == PokemonType.Player && Time.time >= nextUse)
        {
            nextUse = Time.time + 10;
            ShieldPair(100 + Mathf.FloorToInt(Pokemon.GetMaxHp() * 0.06f), HeldItemRules.ResonantGuardId, 3);
        }
        if (kind == AvailableHeldItems.ChargingCharm)
        {
            if (HeldItemRules.Primary(damage) && HeldItemRules.Basic(damage)) charge = Mathf.Min(100, charge + 9);
            if (charge >= 100)
            {
                charge = 0;
                Deal(target, 40 + Mathf.FloorToInt(Pokemon.GetAttack() * 0.7f));
            }
        }
        if (!HeldItemRules.Primary(damage)) return;
        bool basic = HeldItemRules.Basic(damage);
        switch (kind)
        {
            case AvailableHeldItems.MuscleBand:
                if (basic) Deal(target, HeldItemRules.MuscleDamage(previousHp, target.Type == PokemonType.Player));
                break;
            case AvailableHeldItems.ChoiceSpecs:
                if (!basic && Time.time >= nextUse)
                {
                    nextUse = Time.time + 8;
                    Deal(target, 60 + Mathf.FloorToInt(Pokemon.GetSpAttack() * 0.4f), DamageType.Special);
                }
                break;
            case AvailableHeldItems.ShellBell:
                if (!basic && Time.time >= nextUse && !Pokemon.IsHPFull())
                {
                    nextUse = Time.time + 8;
                    Pokemon.HealDamageRPC(HeldItemRules.ShellHealing(Pokemon.GetSpAttack()));
                }
                break;
            case AvailableHeldItems.ScopeLens:
                if (basic && damage.proprieties.HasFlag(DamageProprieties.WasCriticalHit) && Time.time >= nextUse)
                {
                    nextUse = Time.time + 1;
                    Deal(target, Mathf.FloorToInt(Pokemon.GetAttack() * 0.75f));
                }
                break;
            case AvailableHeldItems.RapidFireScarf:
                if (!basic || Time.time < nextUse) break;
                if (Time.time >= windowEnd) attacks = 0;
                windowEnd = Time.time + 3;
                if (++attacks >= 3)
                {
                    attacks = 0;
                    nextUse = Time.time + 10;
                    SetModifier(25, Stat.AtkSpeed, false, 5);
                }
                break;
            case AvailableHeldItems.RazorClaw:
                if (!basic || Time.time >= windowEnd || Time.time < nextUse) break;
                windowEnd = 0;
                nextUse = Time.time + 1.5f;
                Deal(target, 20 + Mathf.FloorToInt(Pokemon.GetAttack() * 0.5f));
                CharacterInfo character = null;
                if (CharactersList.Instance != null)
                    foreach (CharacterInfo info in CharactersList.Instance.Characters)
                        if (info != null && info.pokemonName == Pokemon.BaseStats.PokemonName) { character = info; break; }
                if (character != null && character.Range == PokemonRange.Melee && target.CurrentHp > 0)
                    target.AddStatChange(new StatChange(30, Stat.Speed, 1, true, false, true, ModifierId).WithSource(Pokemon.NetworkObjectId));
                break;
        }
    }

    public void Taken(Pokemon attacker, DamageInfo damage, int amount)
    {
        if (kind == AvailableHeldItems.AssaultVest && attacker != null && damage.type != DamageType.True
            && !HeldItemRules.AttackBased(attacker, damage)) nextUse = Time.time + 15;
        if (kind == AvailableHeldItems.WeaknessPolicy)
        {
            if (Time.time >= windowEnd) stacks = 0;
            windowEnd = Time.time + 3;
            stacks = Mathf.Min(4, stacks + 1);
            SetModifier(stacks * 2.5f, Stat.Attack, true, 3);
        }
        if (kind != AvailableHeldItems.RockyHelmet || Time.time < nextUse || !HeldItemRules.Primary(damage)
            || amount < Pokemon.GetMaxHp() * 0.03f || attacker == null || attacker.TeamMember.IsOnSameTeam(Pokemon.TeamMember.Team)) return;
        nextUse = Time.time + 0.6f;
        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values.ToArray())
            if (obj.TryGetComponent(out Pokemon target) && target != Pokemon && target.CurrentHp > 0
                && !target.TeamMember.IsOnSameTeam(Pokemon.TeamMember.Team)
                && (target.transform.position - Pokemon.transform.position).sqrMagnitude <= 16)
                Deal(target, Mathf.FloorToInt(Pokemon.GetMaxHp() * 0.025f));
    }

    public void Move(MoveType slot)
    {
        if (kind == AvailableHeldItems.RazorClaw && Time.time >= nextUse) windowEnd = Time.time + 3;
        if (slot != MoveType.UniteMove || Time.time < nextUse) return;
        if (kind == AvailableHeldItems.BuddyBarrier)
        {
            nextUse = Time.time + 30;
            ShieldPair(Mathf.FloorToInt(Pokemon.GetMaxHp() * 0.25f), HeldItemRules.BuddyBarrierId, 5);
        }
        if (kind == AvailableHeldItems.EnergyAmp)
        {
            nextUse = Time.time + 15;
            damageBoostEnd = Time.time + 4;
        }
    }

    public float DamageMultiplier => kind == AvailableHeldItems.EnergyAmp && Time.time < damageBoostEnd ? 1.21f : 1;

    private void ShieldPair(int amount, ushort id, float duration)
    {
        Pokemon.GrantHeldItemShield(new ShieldInfo(amount, id, 0, duration, true), Pokemon);
        Pokemon lowest = null;
        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values.ToArray())
            if (obj.TryGetComponent(out Pokemon ally) && ally != Pokemon && ally.Type == PokemonType.Player && ally.CurrentHp > 0
                && ally.TeamMember.IsOnSameTeam(Pokemon.TeamMember)
                && (ally.transform.position - Pokemon.transform.position).sqrMagnitude <= 100
                && (lowest == null || ally.CurrentHp < lowest.CurrentHp)) lowest = ally;
        if (lowest != null) lowest.GrantHeldItemShield(new ShieldInfo(amount, id, 0, duration, true), Pokemon);
    }

    public void Score()
    {
        if (stacks >= 6) return;
        Stat stat;
        int amount;
        switch (kind)
        {
            case AvailableHeldItems.AttackWeight: stat = Stat.Attack; amount = 12; break;
            case AvailableHeldItems.SpAtkSpecs: stat = Stat.SpAttack; amount = 16; break;
            case AvailableHeldItems.AeosCookie: stat = Stat.MaxHp; amount = 200; break;
            default: return;
        }
        SetModifier(++stacks * amount, stat, false, 0, false);
    }

    public void Knockout(bool assist)
    {
        if (kind != AvailableHeldItems.AccelBracer && kind != AvailableHeldItems.DriveLens) return;
        stacks = Mathf.Min(20, stacks + (assist ? 1 : 2));
        SetModifier(stacks * 0.6f, kind == AvailableHeldItems.AccelBracer ? Stat.Attack : Stat.SpAttack, true, 0, false);
    }

    public void Hindrance(Pokemon target)
    {
        if (kind != AvailableHeldItems.VanguardBell || Time.time < nextUse || target.Type != PokemonType.Player
            || Pokemon.CurrentHp <= 0 || target.TeamMember.IsOnSameTeam(Pokemon.TeamMember.Team)) return;
        nextUse = Time.time + 5;
        Pokemon.HealDamageRPC(Mathf.FloorToInt(Pokemon.GetMaxHp() * 0.1f));
    }

    public void Scoring(bool active)
    {
        if (kind != AvailableHeldItems.ScoreShield) return;
        if (!active)
        {
            scoring = false;
            Pokemon.RemoveShieldWithIDRPC(HeldItemRules.ScoreShieldId);
        }
        else if (!scoring)
        {
            scoring = true;
            if (Time.time < nextUse) return;
            nextUse = Time.time + 20;
            Pokemon.GrantHeldItemShield(new ShieldInfo(HeldItemRules.ScoreShield(Pokemon.GetMaxHp(), Pokemon.CurrentLevel),
                HeldItemRules.ScoreShieldId, ushort.MaxValue, 0, false), Pokemon);
        }
    }

    public override void Update()
    {
        float now = Time.time;
        if (Pokemon.CurrentHp <= 0) { lastPosition = Pokemon.transform.position; return; }
        if (kind == AvailableHeldItems.ChargingCharm)
        {
            float distance = Vector3.Distance(lastPosition, Pokemon.transform.position);
            if (distance <= 5) charge = Mathf.Min(100, charge + distance);
            lastPosition = Pokemon.transform.position;
        }
        if (kind == AvailableHeldItems.FloatStone)
        {
            if (Pokemon.IsOutOfCombat && !speedActive) { SetModifier(20, Stat.Speed, true, 0); speedActive = true; }
            else if (!Pokemon.IsOutOfCombat && speedActive) { Pokemon.ClearHeldItemModifier(ModifierId); speedActive = false; }
        }
        if (kind == AvailableHeldItems.AssaultVest && now >= nextUse)
        {
            nextUse = now + 15;
            Pokemon.RemoveShieldWithIDRPC(HeldItemRules.AssaultVestId);
            Pokemon.GrantHeldItemShield(new ShieldInfo(Mathf.FloorToInt(Pokemon.GetMaxHp() * 0.2f), HeldItemRules.AssaultVestId, 0, 0, false), Pokemon);
        }
        if (kind == AvailableHeldItems.FocusBand)
        {
            if (now >= nextUse && Pokemon.CurrentHp < Pokemon.GetMaxHp() * 0.25f)
            {
                nextUse = now + 80;
                recoveryTicks = 3;
                nextTick = now;
            }
            if (recoveryTicks > 0 && now >= nextTick)
            {
                recoveryTicks--;
                nextTick = now + 1;
                Pokemon.HealDamageRPC(HeldItemRules.FocusHealing(Pokemon.GetMaxHp(), Pokemon.CurrentHp));
            }
        }
        if (kind == AvailableHeldItems.Leftovers)
        {
            if (!Pokemon.IsOutOfCombat || Pokemon.IsHPFull()) nextTick = now + 1;
            else if (now >= nextTick) { nextTick = now + 1; Pokemon.HealDamageRPC(Mathf.FloorToInt(Pokemon.GetMaxHp() * 0.04f)); }
        }
        if (kind == AvailableHeldItems.ExpShare && now >= nextTick)
        {
            nextTick = now + 1;
            int experience = Pokemon.TotalExperience;
            foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values.ToArray())
                if (obj.TryGetComponent(out Pokemon ally) && ally != Pokemon && ally.Type == PokemonType.Player
                    && ally.TeamMember.IsOnSameTeam(Pokemon.TeamMember) && ally.TotalExperience < experience) return;
            Pokemon.GainHeldItemPassiveExperience(5);
        }
    }

    public override void Reset()
    {
        recoveryTicks = 0;
        attacks = 0;
        windowEnd = 0;
        damageBoostEnd = 0;
        speedActive = false;
        scoring = false;
        charge = 0;
        if (kind == AvailableHeldItems.AssaultVest) { nextUse = Time.time + 15; Pokemon.RemoveShieldWithIDRPC(HeldItemRules.AssaultVestId); }
        if (kind == AvailableHeldItems.ScoreShield) Pokemon.RemoveShieldWithIDRPC(HeldItemRules.ScoreShieldId);
    }
}
