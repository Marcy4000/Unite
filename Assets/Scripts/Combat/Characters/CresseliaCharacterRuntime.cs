using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;

public sealed class CresseliaCharacterRuntime : CombatCharacterRuntime
{
    [Serializable]
    public sealed class PresentationState
    {
        public long Revision;
        public double BoostReadyAt;
        public bool Escorting;
        public bool MovementLocked;
        public bool UniteLocked;
        public int UnitePhase;
        public double UnitePhaseStarted;
        public Vector3 UnitePoint;
        public Vector3 UniteOrigin;
    }

    private readonly CombatScope lifetime = new CombatScope();
    private readonly List<CombatScope> lastingScopes = new List<CombatScope>();
    private readonly List<CresseliaShieldLease> shields = new List<CresseliaShieldLease>();
    private readonly List<IDisposable> unitePresentationLocks = new List<IDisposable>();

    private Pokemon boundPokemon;
    private double boostReadyAt;
    private bool escorting;
    private float nextEscortScan;
    private ushort auroraDebuffId, moonblastDebuffId;
    private bool debuffIdsLeased;
    private long stateRevision;
    private int movementLocks;
    private bool movementLocked, appliedMovement;
    private int uniteLocks;
    private bool uniteLocked, appliedUniteLocks;
    private int unitePhase;
    private double unitePhaseStarted;
    private Vector3 unitePoint, uniteOrigin;
    private bool appliedUniteFlightState, uniteTakeoffFlightApplied;
    private bool oldSnap = true, oldFlying, oldKnockback = true;
    private bool shownSecondaryCooldown;
    private CombatRoutine basicRecovery;
    private bool disposed;

    public CresseliaCharacterRuntime(PlayerManager actor) : base(actor)
    {
        lifetime.Own(new CombatCallbackLease(() => basicRecovery?.Dispose()));
        BindEvents();
    }

    internal double CurrentTime => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    internal bool IsLivingCresselia => Pokemon != null && Pokemon.BaseStats != null && Pokemon.CurrentHp > 0
        && Pokemon.BaseStats.Passive == AvailablePassives.CresseliaPassive;

    public double BoostReadyAt => boostReadyAt;
    public float BoostRemaining => (float)Math.Max(0d, boostReadyAt - CurrentTime);
    public bool IsBoostReady => BoostRemaining <= 0f;
    public bool Escorting => escorting;
    public bool MovementLocked => movementLocked;
    public bool UniteLocked => uniteLocked;
    public int UnitePhase => unitePhase;
    public bool IsUniteAirborne => unitePhase > 0;
    public bool IsUniteLanding => unitePhase == 2;
    public int ActiveShieldCount => shields.Count;
    public int ActiveLastingScopeCount => lastingScopes.Count;
    public ushort AuroraDebuffId => auroraDebuffId;
    public ushort MoonblastDebuffId => moonblastDebuffId;
    public long StateRevision => stateRevision;
    public override bool CanStartBasicAttack => !disposed && IsLivingCresselia && !IsUniteAirborne
        && !Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts);

    public bool CanRecastUnite => unitePhase == 1 && CurrentTime >= unitePhaseStarted + CresseliaMoveRules.TakeoffDuration;

    public override bool PrepareCast(CombatExecutionContext context)
    {
        if (disposed || !IsLivingCresselia || IsUniteAirborne || Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts))
        {
            return false;
        }

        if (context.MoveId == AvailableMoves.CresseliaUnite)
        {
            return Pokemon.CurrentLevel >= 8;
        }

        if (!CresseliaMoveRules.TryFromMove(context.MoveId, out CresseliaCast kind))
        {
            return false;
        }

        bool starter = kind == CresseliaCast.AuroraBeam || kind == CresseliaCast.Safeguard;
        if (!starter && Pokemon.CurrentLevel < (CresseliaMoveRules.Slot(kind) == 0 ? 4 : 6))
        {
            return false;
        }

        if (CresseliaMoveRules.IsSupport(kind))
        {
            if (context.Target != null)
            {
                if (!IsValidAllyTarget(context.Target)) return false;
                context.Direction = CresseliaMoveRules.Direction(context.Target.transform.position - transform.position, transform.forward);
            }
            else
            {
                context.Target = null;
                context.Direction = CresseliaMoveRules.Direction(context.Direction, transform.forward);
            }
            return true;
        }

        if (kind == CresseliaCast.Moonlight)
        {
            Vector3 requested = transform.position + (CresseliaMoveRules.Finite(context.Direction) ? context.Direction : Vector3.zero);
            Vector3 offset = requested - transform.position;
            offset.y = 0f;
            Vector3 clamped = transform.position + Vector3.ClampMagnitude(offset, CresseliaMoveRules.Range(CresseliaCast.Moonlight));
            if (Actor.PlayerMovement == null || !Actor.PlayerMovement.TryCresseliaLanding(clamped, out Vector3 landing))
                landing = clamped;
            context.CharacterData = landing;
            context.Direction = CresseliaMoveRules.Direction(landing - transform.position, transform.forward);
            return true;
        }

        context.Direction = CresseliaMoveRules.Direction(context.Direction, transform.forward);
        return true;
    }

    public override bool RevalidateCast(CombatExecutionContext context)
    {
        if (disposed || !IsLivingCresselia || IsUniteAirborne || Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts))
            return false;
        if (context.MoveId == AvailableMoves.CresseliaUnite)
            return Pokemon.CurrentLevel >= 8;
        if (!CresseliaMoveRules.TryFromMove(context.MoveId, out CresseliaCast kind))
            return false;
        if (CresseliaMoveRules.IsSupport(kind) && context.Target != null)
            return IsValidAllyTarget(context.Target);
        return true;
    }

    public override void CommitCast(CombatExecutionContext context)
    {
        basicRecovery?.Dispose();
        basicRecovery = null;
    }

    public override CombatExecution CreateExecution(CombatExecutionContext context) => new CresseliaExecution(context, this);

    public bool IsValidAllyTarget(Pokemon candidate)
    {
        if (candidate == null || candidate == Pokemon || !CresseliaMoveRuntime.Living(candidate)) return false;
        if (!candidate.TeamMember.IsOnSameTeam(Actor.CurrentTeam)) return false;
        return (candidate.transform.position - transform.position).sqrMagnitude
            <= CresseliaMoveRules.SupportTargetValidationRange * CresseliaMoveRules.SupportTargetValidationRange;
    }

    public override void Tick()
    {
        if (disposed || Actor == null || Pokemon == null) return;
        if (IsServer)
        {
            BindEvents();
            EnsureDebuffIds();
            TickEscort();
        }
        TickUniteTrajectory();
        Present();
    }

    public void AdvanceRhythm()
    {
        if (disposed || !IsServer || !IsLivingCresselia) return;
        boostReadyAt = CurrentTime + CresseliaMoveRules.ReduceBoost(BoostRemaining);
        Publish();
    }

    public void PerformBasicAttack(Pokemon target)
    {
        if (!IsServer || disposed || !IsLivingCresselia || IsUniteAirborne
            || Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts)
            || !CresseliaMoveRuntime.CanHit(Actor, target)
            || (target.transform.position - transform.position).sqrMagnitude > CresseliaMoveRules.BasicValidationRange * CresseliaMoveRules.BasicValidationRange)
        {
            Controller?.RefundCresseliaBasicCooldown();
            return;
        }

        bool boosted = BoostRemaining <= 0f;
        if (boosted)
        {
            boostReadyAt = CurrentTime + CresseliaMoveRules.BoostCooldown;
            Publish();
        }

        string animation = boosted ? "pm0488_ba21_tokusyu02" : "pm0488_ba20_buturi01";
        basicRecovery?.Dispose();
        basicRecovery = new CombatRoutine(Actor, BasicRecovery(target, animation));
        SpawnLastingScope(scope => new CombatRoutine(Actor, HomingBasicAttack(target, boosted, scope)));
    }

    private IEnumerator BasicRecovery(Pokemon target, string animation)
    {
        const float duration = 0.15f;
        Vector3 direction = target != null
            ? CresseliaMoveRules.Direction(target.transform.position - transform.position, transform.forward)
            : transform.forward;
        Actor.AnimationManager?.SetFloat("CresseliaActionSpeed", 3f);
        Controller.PresentServerAttackRPC(true, direction, animation, 0f);
        try { yield return new WaitForSeconds(duration); }
        finally
        {
            if (Actor != null && IsSpawned)
            {
                Controller.PresentServerAttackRPC(false, Vector3.zero, "");
                RecoverAnimation(animation);
            }
        }
    }

    private IEnumerator HomingBasicAttack(Pokemon target, bool boosted, CombatScope scope)
    {
        Vector3 position = transform.position + Vector3.up * 0.8f;
        int visual = ShowEffect(boosted ? CresseliaEffect.Boosted : CresseliaEffect.Basic,
            position, transform.forward, 3f, target != null ? target.NetworkObjectId : 0UL);
        scope.Own(new CombatCallbackLease(() => StopEffect(visual)));
        float expires = Time.time + 3f;
        while (!scope.IsDisposed && IsSpawned && CresseliaMoveRuntime.CanHit(Actor, target) && Time.time < expires)
        {
            Vector3 goal = target.transform.position + Vector3.up * 0.7f;
            Vector3 next = Vector3.MoveTowards(position, goal, CresseliaMoveRules.BasicSpeed * Time.deltaTime);
            if ((next - goal).sqrMagnitude <= 0.25f * 0.25f)
            {
                target.TakeDamageRPC(CresseliaMoveRules.BasicDamage(Controller.NetworkObjectId, boosted));
                if (boosted && CresseliaMoveRuntime.Living(target))
                    target.AddStatusEffect(new StatusEffect(StatusType.Asleep, 0.15f, true, 0));
                ShowEffect(CresseliaEffect.MoonblastImpact, goal, Vector3.forward, 0.2f, 0, boosted ? 0.55f : 0.3f);
                break;
            }
            position = next;
            yield return null;
        }
        DisposeLastingScope(scope);
    }

    internal void RecoverAnimation(string expectedState)
    {
        if (!IsServer || !IsSpawned || Pokemon == null || Pokemon.CurrentHp <= 0
            || Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts) || IsUniteAirborne)
            return;
        if (Controller.ServerActionLocks == 0)
            Actor.AnimationManager?.SetFloat("CresseliaActionSpeed", 1f);
        bool walking = Actor.PlayerMovement != null && Actor.PlayerMovement.CanMove
            && (Actor.IsBot || Actor.IsOwner && InputManager.Instance != null)
            && Actor.MovementInput.sqrMagnitude > 0.01f;
        Actor.AnimationManager?.SetBool("Walking", walking);
        string recovery = walking ? "pm0488_fi21_run01" : "pm0488_ba10_waitA01";
        Actor.AnimationManager?.RecoverAnimationRPC(expectedState, recovery, 0.06f);
    }

    private void TickEscort()
    {
        if (Time.time < nextEscortScan) return;
        nextEscortScan = Time.time + CresseliaMoveRules.EscortScanInterval;
        bool escort = false;
        if (IsLivingCresselia)
        {
            foreach (Pokemon ally in CresseliaMoveRuntime.Targets(Physics.OverlapSphere(transform.position, CresseliaMoveRules.EscortRadius, LayerMask.GetMask("Players"))))
            {
                if (ally != Pokemon && CresseliaMoveRuntime.Living(ally)
                    && ally.TeamMember.IsOnSameTeam(Actor.CurrentTeam))
                {
                    escort = true;
                    break;
                }
            }
        }
        if (escort == escorting) return;
        escorting = escort;
        Pokemon.RemoveAllStatChangeWithIDRPC(CresseliaMoveRules.EscortId);
        if (escort)
            Pokemon.AddStatChange(new StatChange(CresseliaMoveRules.EscortSpeedPercent, Stat.Speed, 0f, false, true, true, CresseliaMoveRules.EscortId));
        Publish();
    }

    internal void LaunchAuroraBeam(Vector3 direction)
    {
        if (disposed || !IsServer) return;
        SpawnLastingScope(scope => new CombatRoutine(Actor, RunAuroraBeam(direction, scope)));
    }

    private IEnumerator RunAuroraBeam(Vector3 direction, CombatScope scope)
    {
        yield return RunBeamProjectile(CresseliaCast.AuroraBeam, direction, false, scope);
    }

    internal void LaunchMoonblast(Vector3 direction, bool upgraded)
    {
        if (disposed || !IsServer) return;
        SpawnLastingScope(scope => new CombatRoutine(Actor, RunMoonblast(direction, upgraded, scope)));
    }

    private IEnumerator RunMoonblast(Vector3 direction, bool upgraded, CombatScope scope)
    {
        yield return RunBeamProjectile(CresseliaCast.Moonblast, direction, upgraded, scope);
    }

    private IEnumerator RunBeamProjectile(CresseliaCast move, Vector3 direction, bool upgraded, CombatScope scope)
    {
        EnsureDebuffIds();
        Vector3 origin = transform.position + Vector3.up * 0.7f;
        float range = CresseliaMoveRules.Range(move);
        float travelled = 0f;
        float radius = move == CresseliaCast.Moonblast ? 0.7f : 0.4f;
        int visual = ShowEffect(move == CresseliaCast.Moonblast ? CresseliaEffect.Moonblast : CresseliaEffect.AuroraBeam,
            origin, direction, range / CresseliaMoveRules.BeamSpeed + 0.05f);
        scope.Own(new CombatCallbackLease(() => StopEffect(visual)));
        while (!scope.IsDisposed && IsSpawned && travelled < range)
        {
            float next = Mathf.Min(range, travelled + CresseliaMoveRules.BeamSpeed * Time.deltaTime);
            Vector3 from = origin + direction * travelled;
            Vector3 to = origin + direction * next;
            Pokemon first = null;
            float closest = float.MaxValue;
            foreach (Pokemon target in CresseliaMoveRuntime.Targets(Physics.OverlapCapsule(from, to, radius, LayerMask.GetMask("Players", "WildPokemons"))))
            {
                if (!CresseliaMoveRuntime.CanHit(Actor, target)) continue;
                float distance = Vector3.Dot(target.transform.position - origin, direction);
                if (distance < closest)
                {
                    closest = distance;
                    first = target;
                }
            }
            if (first != null)
            {
                if (move == CresseliaCast.Moonblast)
                {
                    ResolveMoonblastHit(first, upgraded);
                }
                else
                {
                    first.TakeDamageRPC(CresseliaMoveRules.Damage(move, Controller.NetworkObjectId));
                    RefreshDebuffs(first, false);
                    if (first.Type == PokemonType.Player) AdvanceRhythm();
                    ShowEffect(CresseliaEffect.MoonblastImpact, first.transform.position, direction, 0.25f, 0, 0.6f);
                }
                break;
            }
            travelled = next;
            yield return null;
        }
        DisposeLastingScope(scope);
    }

    private void ResolveMoonblastHit(Pokemon primary, bool upgraded)
    {
        float radius = CresseliaMoveRules.MoonblastRadius(upgraded);
        Vector3 center = primary.transform.position;
        ShowEffect(CresseliaEffect.MoonblastImpact, center, Vector3.forward, 0.35f, 0, radius);
        HashSet<Pokemon> targets = CresseliaMoveRuntime.Targets(Physics.OverlapSphere(center, radius, LayerMask.GetMask("Players", "WildPokemons")));
        targets.Add(primary);
        bool rhythm = false;
        foreach (Pokemon target in targets)
        {
            if (!CresseliaMoveRuntime.CanHit(Actor, target)) continue;
            target.TakeDamageRPC(CresseliaMoveRules.Damage(CresseliaCast.Moonblast, Controller.NetworkObjectId));
            RefreshDebuffs(target, true);
            rhythm |= target.Type == PokemonType.Player;
        }
        if (rhythm) AdvanceRhythm();
    }

    public void RefreshDebuffs(Pokemon target, bool moonblast)
    {
        if (!IsServer || disposed || !CresseliaMoveRuntime.Living(target)) return;
        EnsureDebuffIds();
        ushort id = moonblast ? moonblastDebuffId : auroraDebuffId;
        target.RemoveAllStatChangeWithIDRPC(id);
        target.AddStatChange(new StatChange(15, Stat.AtkSpeed, moonblast ? 5f : 3f, true, false, true, id));
        if (!moonblast) return;
        target.AddStatChange(new StatChange(30, Stat.Attack, 5f, true, false, true, id));
        target.AddStatChange(new StatChange(20, Stat.SpAttack, 5f, true, false, true, id));
    }

    internal void ExecutePsybeam(Vector3 direction, bool upgraded)
    {
        if (disposed || !IsServer) return;
        float length = CresseliaMoveRules.Range(CresseliaCast.Psybeam);
        float width = CresseliaMoveRules.PsybeamWidth(upgraded);
        Vector3 origin = transform.position;
        ShowEffect(CresseliaEffect.Psybeam, origin, direction, 0.35f, 0, width);
        HashSet<Pokemon> targets = CresseliaMoveRuntime.Targets(Physics.OverlapBox(
            origin + Vector3.up * 0.7f + direction * (length * 0.5f),
            new Vector3(width * 0.5f, 1f, length * 0.5f),
            Quaternion.LookRotation(direction),
            LayerMask.GetMask("Players", "WildPokemons")));
        int enemyPlayers = 0;
        foreach (Pokemon target in targets)
        {
            if (!CresseliaMoveRuntime.CanHit(Actor, target)) continue;
            if (target.Type == PokemonType.Player) enemyPlayers++;
            target.TakeDamageRPC(CresseliaMoveRules.Damage(CresseliaCast.Psybeam, Controller.NetworkObjectId));
            if (CresseliaMoveRuntime.Living(target))
                target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));
        }
        if (enemyPlayers > 0 && IsLivingCresselia)
        {
            Pokemon.HealDamageRPC(Mathf.FloorToInt(Pokemon.GetMaxHp() * 0.05f) * enemyPlayers);
            AdvanceRhythm();
            ShowEffect(CresseliaEffect.BeamHeal, origin + direction * 3f, -direction, 0.35f, Controller.NetworkObjectId);
        }
    }

    internal void StartMoonlightField(Vector3 center, bool upgraded)
    {
        if (disposed || !IsServer) return;
        SpawnLastingScope(scope => new CombatRoutine(Actor, RunMoonlightField(center, upgraded, scope)));
    }

    private IEnumerator RunMoonlightField(Vector3 center, bool upgraded, CombatScope scope)
    {
        float radius = CresseliaMoveRules.MoonlightRadius(upgraded);
        int fieldVisual = ShowEffect(CresseliaEffect.Moonlight, center, Vector3.forward, CresseliaMoveRules.MoonlightDuration, 0, radius);
        scope.Own(new CombatCallbackLease(() => StopEffect(fieldVisual)));
        bool rhythm = false;
        double started = CurrentTime;
        for (int pulse = 0; pulse < CresseliaMoveRules.MoonlightPulses && !scope.IsDisposed; pulse++)
        {
            while (!scope.IsDisposed && CurrentTime < started + pulse)
                yield return null;
            if (scope.IsDisposed || !IsSpawned) break;
            ShowEffect(CresseliaEffect.HealPulse, center, Vector3.forward, 0.35f, 0, radius);
            foreach (Pokemon ally in CresseliaMoveRuntime.Targets(Physics.OverlapSphere(center + Vector3.up * 0.5f, radius, LayerMask.GetMask("Players"))))
            {
                if (!CresseliaMoveRuntime.Living(ally)
                    || !ally.TeamMember.IsOnSameTeam(Actor.CurrentTeam))
                    continue;
                int amount = Mathf.Min(ally.GetMaxHp() - ally.CurrentHp, Mathf.FloorToInt(ally.GetMaxHp() * 0.08f));
                if (amount <= 0) continue;
                ally.HealDamageRPC(amount, Pokemon != null ? Pokemon.NetworkObjectId : Controller.NetworkObjectId);
                if (!rhythm && ally != Pokemon)
                {
                    rhythm = true;
                    AdvanceRhythm();
                }
            }
        }
        while (!scope.IsDisposed && CurrentTime < started + CresseliaMoveRules.MoonlightDuration)
            yield return null;
        DisposeLastingScope(scope);
    }

    internal void ExecuteSafeguard(Pokemon ally)
    {
        ApplyProtection(CresseliaCast.Safeguard, ally, false);
    }

    internal void ExecuteLunarBlessing(Pokemon ally, bool upgraded)
    {
        ApplyProtection(CresseliaCast.LunarBlessing, ally, upgraded);
    }

    private void ApplyProtection(CresseliaCast move, Pokemon ally, bool upgraded)
    {
        if (disposed || !IsServer || !IsLivingCresselia) return;
        CresseliaMoveRuntime.Cleanse(Pokemon);
        ShowEffect(CresseliaEffect.Cleanse, transform.position, transform.forward, 0.45f, Controller.NetworkObjectId);
        bool validAlly = ally != null && ally != Pokemon && CresseliaMoveRuntime.Living(ally)
            && ally.TeamMember.IsOnSameTeam(Actor.CurrentTeam);
        if (validAlly)
        {
            CresseliaMoveRuntime.Cleanse(ally);
            ShowEffect(CresseliaEffect.Cleanse, ally.transform.position, transform.forward, 0.45f, ally.NetworkObjectId);
        }
        if (move == CresseliaCast.LunarBlessing)
        {
            int amount = Mathf.FloorToInt(Pokemon.GetMaxHp() * CresseliaMoveRules.ShieldRatio(upgraded));
            StartBlessingShield(Pokemon, amount);
            if (validAlly)
            {
                StartBlessingShield(ally, amount);
                AdvanceRhythm();
            }
        }
    }

    internal void StartBlessingShield(Pokemon target, int amount)
    {
        if (disposed || !IsServer || !CresseliaMoveRuntime.Living(target) || amount <= 0) return;
        SpawnLastingScope(scope => new CombatRoutine(Actor, RunBlessingShield(target, amount, scope)));
    }

    private IEnumerator RunBlessingShield(Pokemon target, int amount, CombatScope scope)
    {
        var lease = new CresseliaShieldLease(target, amount);
        shields.Add(lease);
        int visual = ShowEffect(CresseliaEffect.LunarShield, target.transform.position, transform.forward,
            CresseliaMoveRules.BlessingDuration, target.NetworkObjectId, amount, lease.Id);
        bool resolved = false;
        scope.Own(new CombatCallbackLease(() =>
        {
            lease.Dispose();
            shields.Remove(lease);
            StopEffect(visual);
        }));
        double expires = CurrentTime + CresseliaMoveRules.BlessingDuration;
        while (!scope.IsDisposed && lease.Intact && CurrentTime < expires)
            yield return null;
        if (!scope.IsDisposed)
        {
            resolved = true;
            int heal = lease.Intact ? lease.Remaining : 0;
            lease.Dispose();
            shields.Remove(lease);
            StopEffect(visual);
            if (heal > 0 && CresseliaMoveRuntime.Living(target))
            {
                target.HealDamageRPC(heal, Pokemon != null ? Pokemon.NetworkObjectId : Controller.NetworkObjectId);
                ShowEffect(CresseliaEffect.ShieldHeal, target.transform.position, transform.forward, 0.45f, target.NetworkObjectId);
            }
            else if (CresseliaMoveRuntime.Living(target))
            {
                ShowEffect(CresseliaEffect.ShieldBreak, target.transform.position, transform.forward, 0.25f, target.NetworkObjectId);
            }
        }
        if (resolved || !scope.IsDisposed)
            DisposeLastingScope(scope);
    }

    internal IDisposable AcquireUniteLock()
    {
        uniteLocks++;
        uniteLocked = true;
        Publish();
        return new CombatCallbackLease(() =>
        {
            uniteLocks = Math.Max(0, uniteLocks - 1);
            uniteLocked = uniteLocks > 0;
            Publish();
        });
    }

    internal void SetUnitePhase(int phase, Vector3 point, Vector3 origin, double started)
    {
        unitePhase = phase;
        unitePoint = point;
        uniteOrigin = origin;
        unitePhaseStarted = started;
        if (phase <= 0 && IsLivingCresselia && CresseliaMoveRules.Finite(point) && phase == 0)
            transform.position = point;
        Publish();
    }

    private void TickUniteTrajectory()
    {
        if (unitePhase <= 0 || (!IsOwner && !IsServer) || Pokemon == null || Pokemon.CurrentHp <= 0) return;
        float elapsed = Mathf.Max(0f, (float)(CurrentTime - unitePhaseStarted));
        if (unitePhase == 1)
        {
            Vector3 position = transform.position;
            float targetY = uniteOrigin.y + Mathf.SmoothStep(0f, 3f, elapsed / CresseliaMoveRules.TakeoffDuration);
            position.y = Mathf.Max(position.y, targetY);
            transform.position = position;
            if (elapsed >= CresseliaMoveRules.TakeoffDuration && !uniteTakeoffFlightApplied)
            {
                uniteTakeoffFlightApplied = true;
                if (Actor.PlayerMovement != null) Actor.PlayerMovement.IsFlying = true;
                if (IsServer) Actor.AnimationManager?.PlayAnimation("pm0488_ba10_waitB01");
            }
        }
        else if (unitePhase == 2)
        {
            transform.position = Vector3.Lerp(uniteOrigin, unitePoint, Mathf.Clamp01(elapsed / CresseliaMoveRules.LandingDuration));
        }
    }

    internal IDisposable AcquireMovementLock()
    {
        movementLocks++;
        movementLocked = true;
        Publish();
        return new CombatCallbackLease(() =>
        {
            movementLocks = Math.Max(0, movementLocks - 1);
            movementLocked = movementLocks > 0;
            Publish();
        });
    }

    private CombatScope SpawnLastingScope(Func<CombatScope, IDisposable> factory)
    {
        var scope = new CombatScope();
        lastingScopes.Add(scope);
        scope.Own(new CombatCallbackLease(() => lastingScopes.Remove(scope)));
        IDisposable work = factory(scope);
        if (work != null) scope.Own(work);
        return scope;
    }

    private void DisposeLastingScope(CombatScope scope)
    {
        if (scope == null) return;
        lastingScopes.Remove(scope);
        scope.Dispose();
    }

    internal int ShowEffect(CresseliaEffect effect, Vector3 origin, Vector3 direction, float duration,
        ulong target = 0, float size = 1f, int detail = 0)
        => Controller != null ? Controller.ShowCresseliaEffectInternal(effect, origin, direction, duration, target, size, detail) : 0;

    internal void StopEffect(int id)
    {
        if (id != 0 && Controller != null && IsSpawned && IsServer)
            Controller.StopCresseliaEffectInternal(id);
    }

    private void Publish()
    {
        if (disposed) return;
        if (IsServer)
        {
            stateRevision++;
            Controller?.SyncCresseliaNetworkVariables(boostReadyAt, escorting);
            Present();
            if (IsSpawned)
            {
                Controller.CresseliaSnapshotRPC(
                    boostReadyAt,
                    escorting,
                    movementLocked,
                    uniteLocked,
                    unitePhase,
                    unitePhaseStarted,
                    unitePoint,
                    uniteOrigin,
                    stateRevision);
            }
        }
    }

    public override string CapturePresentationState() => JsonUtility.ToJson(new PresentationState
    {
        Revision = stateRevision,
        BoostReadyAt = boostReadyAt,
        Escorting = escorting,
        MovementLocked = movementLocked,
        UniteLocked = uniteLocked,
        UnitePhase = unitePhase,
        UnitePhaseStarted = unitePhaseStarted,
        UnitePoint = unitePoint,
        UniteOrigin = uniteOrigin
    });

    public override void ApplyPresentationState(string state)
    {
        if (disposed || IsServer || string.IsNullOrEmpty(state)) return;
        PresentationState snapshot;
        try { snapshot = JsonUtility.FromJson<PresentationState>(state); }
        catch (ArgumentException) { return; }
        if (snapshot == null || double.IsNaN(snapshot.BoostReadyAt) || double.IsInfinity(snapshot.BoostReadyAt)
            || double.IsNaN(snapshot.UnitePhaseStarted) || double.IsInfinity(snapshot.UnitePhaseStarted))
            return;
        ReceiveSnapshot(
            snapshot.BoostReadyAt,
            snapshot.Escorting,
            snapshot.MovementLocked,
            snapshot.UniteLocked,
            snapshot.UnitePhase,
            snapshot.UnitePhaseStarted,
            snapshot.UnitePoint,
            snapshot.UniteOrigin,
            snapshot.Revision);
    }

    public void ReceiveSnapshot(
        double readyAt,
        bool escort,
        bool movement,
        bool unite,
        int phase,
        double phaseStarted,
        Vector3 point,
        Vector3 origin,
        long revision)
    {
        if (disposed || revision < stateRevision) return;
        if (double.IsNaN(readyAt) || double.IsInfinity(readyAt) || double.IsNaN(phaseStarted) || double.IsInfinity(phaseStarted))
            return;
        stateRevision = revision;
        boostReadyAt = Math.Max(0d, readyAt);
        escorting = escort;
        movementLocked = movement;
        uniteLocked = unite;
        unitePhase = Mathf.Clamp(phase, -1, 2);
        unitePhaseStarted = Math.Max(0d, phaseStarted);
        if (CresseliaMoveRules.Finite(point)) unitePoint = point;
        if (CresseliaMoveRules.Finite(origin)) uniteOrigin = origin;
        Present();
    }

    private void Present()
    {
        PresentMovement();
        if (!IsOwner) return;
        PresentUniteLocks();
    }

    private void PresentMovement()
    {
        if ((!IsOwner && !IsServer) || Actor == null || Actor.PlayerMovement == null) return;
        if (movementLocked != appliedMovement)
        {
            if (movementLocked) Actor.PlayerMovement.AddMovementRestriction();
            else Actor.PlayerMovement.RemoveMovementRestriction();
            appliedMovement = movementLocked;
        }
        bool airborne = unitePhase > 0;
        if (airborne != appliedUniteFlightState)
        {
            if (airborne)
            {
                oldSnap = Actor.PlayerMovement.SnapToGround;
                oldFlying = Actor.PlayerMovement.IsFlying;
                oldKnockback = Actor.PlayerMovement.CanBeKnockedBack;
                uniteTakeoffFlightApplied = false;
                Actor.PlayerMovement.SnapToGround = false;
                Actor.PlayerMovement.CanBeKnockedBack = false;
                if (unitePhase == 1 && CurrentTime >= unitePhaseStarted + CresseliaMoveRules.TakeoffDuration)
                {
                    uniteTakeoffFlightApplied = true;
                    Actor.PlayerMovement.IsFlying = true;
                }
            }
            else
            {
                uniteTakeoffFlightApplied = false;
                Actor.PlayerMovement.SnapToGround = oldSnap;
                Actor.PlayerMovement.IsFlying = oldFlying;
                Actor.PlayerMovement.CanBeKnockedBack = oldKnockback;
                if (Pokemon != null && Pokemon.CurrentHp <= 0)
                    Actor.PlayerMovement.CanMove = false;
            }
            appliedUniteFlightState = airborne;
        }
    }

    private void PresentUniteLocks()
    {
        if (uniteLocked != appliedUniteLocks)
        {
            foreach (IDisposable lease in unitePresentationLocks) lease.Dispose();
            unitePresentationLocks.Clear();
            appliedUniteLocks = uniteLocked;
            if (uniteLocked && Controller != null)
            {
                unitePresentationLocks.Add(Controller.AcquireMoveStatus(0, ActionStatusType.Disabled));
                unitePresentationLocks.Add(Controller.AcquireMoveStatus(1, ActionStatusType.Disabled));
                unitePresentationLocks.Add(Controller.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
                unitePresentationLocks.Add(Controller.BattleItemStatus.AcquireStatus(ActionStatusType.Disabled));
                if (Actor.ScoreStatus != null) unitePresentationLocks.Add(Actor.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
                if (Actor.RecallStatus != null) unitePresentationLocks.Add(Actor.RecallStatus.AcquireStatus(ActionStatusType.Busy));
            }
        }

        bool shouldShowSecondary = unitePhase == 1;
        if (shouldShowSecondary != shownSecondaryCooldown)
        {
            shownSecondaryCooldown = shouldShowSecondary;
            if (Actor != null && Actor.IsLocalHuman && BattleUIManager.instance != null)
                BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(shouldShowSecondary
                    ? (float)Math.Max(0d, CresseliaMoveRules.FlightDuration - (CurrentTime - unitePhaseStarted)) : 0f);
        }
    }

    private void EnsureDebuffIds()
    {
        if (debuffIdsLeased || !IsServer) return;
        auroraDebuffId = CresseliaEffectIds.Acquire();
        moonblastDebuffId = CresseliaEffectIds.Acquire();
        debuffIdsLeased = true;
    }

    private void ReleaseDebuffIds()
    {
        if (!debuffIdsLeased) return;
        if (IsServer && NetworkManager != null && NetworkManager.SpawnManager != null)
        {
            foreach (NetworkObject obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
            {
                if (obj != null && obj.TryGetComponent(out Pokemon target) && target.IsSpawned)
                {
                    target.RemoveAllStatChangeWithIDRPC(auroraDebuffId);
                    target.RemoveAllStatChangeWithIDRPC(moonblastDebuffId);
                }
            }
        }
        CresseliaEffectIds.Release(auroraDebuffId);
        CresseliaEffectIds.Release(moonblastDebuffId);
        auroraDebuffId = 0;
        moonblastDebuffId = 0;
        debuffIdsLeased = false;
    }

    private void BindEvents()
    {
        if (boundPokemon == Pokemon) return;
        UnbindEvents();
        boundPokemon = Pokemon;
        if (boundPokemon != null)
            boundPokemon.OnDeath += OnDeath;
    }

    private void UnbindEvents()
    {
        if (boundPokemon == null) return;
        boundPokemon.OnDeath -= OnDeath;
        boundPokemon = null;
    }

    private void OnDeath(DamageInfo info)
    {
        basicRecovery?.Dispose();
        basicRecovery = null;
        if (IsServer)
        {
            escorting = false;
            nextEscortScan = 0f;
            if (Pokemon != null && Pokemon.IsSpawned)
            {
                Pokemon.RemoveAllStatChangeWithIDRPC(CresseliaMoveRules.EscortId);
                Pokemon.RemoveStatusEffectWithID(CresseliaMoveRules.UniteInvincibleId);
            }
            Publish();
        }
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        UnbindEvents();
        lifetime.Dispose();
        foreach (CombatScope scope in new List<CombatScope>(lastingScopes))
            scope.Dispose();
        lastingScopes.Clear();
        foreach (CresseliaShieldLease shield in new List<CresseliaShieldLease>(shields))
            shield.Dispose();
        shields.Clear();
        foreach (IDisposable lease in unitePresentationLocks)
            lease.Dispose();
        unitePresentationLocks.Clear();
        appliedUniteLocks = false;
        if (shownSecondaryCooldown && Actor != null && Actor.IsLocalHuman && BattleUIManager.instance != null)
        {
            shownSecondaryCooldown = false;
            BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(0f);
        }
        if (appliedMovement && Actor != null && Actor.PlayerMovement != null)
        {
            Actor.PlayerMovement.RemoveMovementRestriction();
            appliedMovement = false;
        }
        if (appliedUniteFlightState && Actor != null && Actor.PlayerMovement != null)
        {
            Actor.PlayerMovement.SnapToGround = oldSnap;
            Actor.PlayerMovement.IsFlying = oldFlying;
            Actor.PlayerMovement.CanBeKnockedBack = oldKnockback;
            appliedUniteFlightState = false;
        }
        if (IsServer && Pokemon != null && Pokemon.IsSpawned)
        {
            Pokemon.RemoveAllStatChangeWithIDRPC(CresseliaMoveRules.EscortId);
            Pokemon.RemoveStatusEffectWithID(CresseliaMoveRules.UniteInvincibleId);
        }
        ReleaseDebuffIds();
        Controller?.DisposeCresseliaPresentationInternal();
    }
}
