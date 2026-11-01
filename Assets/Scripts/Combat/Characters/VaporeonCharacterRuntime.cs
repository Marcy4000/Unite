using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Unite.Combat;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class VaporeonCharacterRuntime : CombatCharacterRuntime
{
    private readonly List<EffectScope> effects = new List<EffectScope>();
    private readonly Dictionary<ulong, EffectScope> rings = new Dictionary<ulong, EffectScope>();
    private static readonly Dictionary<ulong, float> pullReady = new Dictionary<ulong, float>();
    private EffectScope rain, whirlpool;
    private Pokemon boundPokemon;
    private CombatRoutine basicRecovery;
    private byte charge;
    private double lastBasic, hydrationReady;
    private bool diving, disposed;
    private long revision;
    private double recoveryUntil;
    private readonly Dictionary<int, VisualState> visuals = new Dictionary<int, VisualState>();
    private readonly Dictionary<int, VisualRequest> renderedVisuals = new Dictionary<int, VisualRequest>();
    private int nextVisual;
    private bool castLocked, castRooted;
    private int unlockedSlot = -1;
    private readonly Dictionary<int, LockState> activeLocks = new Dictionary<int, LockState>();
    private int nextLock;
    private IDisposable presentationLock;
    private bool presentedRoot, presentedLock;
    private int presentedUnlockedSlot = -2;
    private double hopStarted = -10;
    private Vector3 hopOrigin, hopDestination;
    private bool hopActive;
    private WallCrossingTraversal presentationHop;
    private Transform model;
    private Vector3 modelOrigin;
    private float sink;
    private bool modelPosed;

    [Serializable]
    private sealed class VisualState
    {
        public int Id;
        public VaporEffect Effect;
        public Vector3 Position;
        public float Duration;
        public ulong Target;
        public float Size;
        public int Detail;
        public double Started;
    }

    private sealed class VisualRequest
    {
        public bool Cancelled;
        public VaporVisual Instance;
    }

    private sealed class LockState
    {
        public bool Movement;
        public int ExceptSlot;
    }

    [Serializable]
    private sealed class PresentationState
    {
        public long Revision;
        public byte Charge;
        public bool Diving;
        public bool CastLocked;
        public bool CastRooted;
        public int UnlockedSlot;
        public bool HopActive;
        public double HopStarted;
        public double RecoveryUntil;
        public Vector3 HopOrigin;
        public Vector3 HopDestination;
        public VisualState[] Visuals;
    }

    public VaporeonCharacterRuntime(PlayerManager actor) : base(actor) { BindEvents(); }
    internal double Now => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    internal bool Living => !disposed && IsSpawned && Pokemon != null && Pokemon.CurrentHp > 0;
    public byte Charge => charge;
    public bool Diving => diving;
    public override bool CanStartBasicAttack => !diving;
    public override float MoveInputBufferDuration => 0.22f;
    public override bool CanBufferMoveInput => Living && !diving && recoveryUntil > Now && recoveryUntil - Now <= 0.18;

    internal IDisposable TrackRecovery(float duration)
    {
        recoveryUntil = Now + duration;
        revision++;
        return new CombatCallbackLease(() => { recoveryUntil = 0; revision++; });
    }

    public override bool PrepareCast(CombatExecutionContext context)
    {
        if (!Living || diving || Pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) return false;
        if (context.MoveId == AvailableMoves.VaporHelpingHand)
            return context.Target != null && context.Target != Pokemon && VaporMoveRuntime.CanHeal(Actor, context.Target);
        if (context.MoveId == AvailableMoves.VaporAquaRing && context.Target != null)
            return context.Target != Pokemon && VaporMoveRuntime.CanHeal(Actor, context.Target);
        return true;
    }

    public override bool RevalidateCast(CombatExecutionContext context) => PrepareCast(context);
    public override CombatExecution CreateExecution(CombatExecutionContext context) => new VaporeonExecution(context, this);

    public override void Tick()
    {
        if (disposed || !IsSpawned || Pokemon == null) return;
        if (IsServer)
        {
            BindEvents();
            if (charge > 0 && Now - lastBasic >= VaporMoveRules.BoostExpiry)
            {
                charge = 0;
                revision++;
            }
            foreach (int identifier in new List<int>(visuals.Keys))
            {
                VisualState visual = visuals[identifier];
                if (visual.Duration > 0 && Now >= visual.Started + visual.Duration)
                {
                    visuals.Remove(identifier);
                    revision++;
                }
            }
        }
        PresentState();
    }

    private void BindEvents()
    {
        if (boundPokemon == Pokemon) return;
        if (boundPokemon != null) boundPokemon.OnDeath -= OnDeath;
        boundPokemon = Pokemon;
        if (boundPokemon != null) boundPokemon.OnDeath += OnDeath;
    }

    public void PerformBasicAttack(Pokemon target)
    {
        if (!IsServer || !Living || diving || Pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)
            || !VaporMoveRuntime.CanHit(Actor, target)
            || (target.transform.position - transform.position).sqrMagnitude > 6.5f * 6.5f) return;
        if (Now - lastBasic >= VaporMoveRules.BoostExpiry) charge = 0;
        bool boosted = charge == 2;
        charge = (byte)(boosted ? 0 : charge + 1);
        lastBasic = Now;
        revision++;
        string animation = Pokemon.CurrentLevel >= 3
            ? boosted ? "Armature_pm0134_00_ba21_tokusyu02_gfbanm" : "Armature_pm0134_00_ba21_tokusyu01_gfbanm"
            : boosted ? "ani_atk6_bat_0133" : "ani_atk4_bat_0133";
        if (Pokemon.CurrentLevel >= 3) Actor.AnimationManager?.SetFloat("VaporeonActionSpeed", 3f);
        basicRecovery?.Dispose();
        basicRecovery = new CombatRoutine(Actor, BasicRecovery(target, animation));
        StartEffect(scope => HomingBasic(scope, target, boosted));
    }

    private IEnumerator BasicRecovery(Pokemon target, string animation)
    {
        float duration = Mathf.Min(0.15f, Controller.GetAtkSpeedCooldown() * 0.25f);
        Vector3 direction = target != null ? target.transform.position - transform.position : transform.forward;
        Controller.PresentServerAttackRPC(true, direction, animation, duration);
        try { yield return new WaitForSeconds(duration); }
        finally
        {
            if (Actor != null && Actor.IsSpawned)
            {
                Controller.PresentServerAttackRPC(false, Vector3.zero, "");
                RecoverAnimation();
            }
        }
    }

    private IEnumerator HomingBasic(EffectScope scope, Pokemon target, bool boosted)
    {
        Vector3 position = transform.position + Vector3.up * 0.8f;
        scope.Visual(boosted ? VaporEffect.Boosted : VaporEffect.Basic, position, 3f, target.NetworkObjectId);
        double end = Now + 3;
        while (scope.Live && VaporMoveRuntime.CanHit(Actor, target) && Now < end)
        {
            Vector3 goal = target.transform.position + Vector3.up * 0.7f;
            position = Vector3.MoveTowards(position, goal, VaporMoveRules.BasicSpeed * Time.deltaTime);
            if ((position - goal).sqrMagnitude < 0.04f)
            {
                target.TakeDamageRPC(VaporMoveRules.BasicDamage(Controller.NetworkObjectId, boosted));
                ShowEffect(VaporEffect.DiveSplash, goal, 0.2f, size: boosted ? 0.6f : 0.3f);
                yield break;
            }
            yield return null;
        }
    }

    internal int HealAlly(Pokemon recipient, int amount)
    {
        if (!Living || !VaporMoveRuntime.CanHeal(Actor, recipient)) return 0;
        amount = Mathf.Min(Mathf.Max(0, amount), recipient.GetMissingHp());
        if (amount <= 0) return 0;
        recipient.HealDamageRPC(amount, Pokemon.NetworkObjectId);
        SupportAlly(recipient);
        return amount;
    }

    internal void SupportAlly(Pokemon recipient)
    {
        if (!Living || Pokemon.CurrentLevel < 3 || recipient == Pokemon || Now < hydrationReady) return;
        hydrationReady = Now + 6;
        Pokemon.RemoveAllStatChangeWithIDRPC(VaporMoveRules.HydrationId);
        Pokemon.AddStatChange(new StatChange(10, Stat.Speed, 1.5f, true, true, true, VaporMoveRules.HydrationId));
        ShowEffect(VaporEffect.Hydration, transform.position, 1.5f, Controller.NetworkObjectId);
    }

    internal void StartSwiftVolley(Vector3 center) => StartEffect(scope => SwiftVolley(scope, center));
    private IEnumerator SwiftVolley(EffectScope scope, Vector3 center)
    {
        scope.Visual(VaporEffect.Swift, center, 2.8f);
        for (int volley = 0; volley < 4 && scope.Live; volley++)
        {
            yield return Wait(scope, 0.7);
            if (!scope.Live) yield break;
            foreach (Pokemon target in VaporMoveRuntime.Targets(center, 1f))
                if (VaporMoveRuntime.CanHit(Actor, target))
                    target.TakeDamageRPC(new DamageInfo(Controller.NetworkObjectId, 0.41f, 5, 170, DamageType.Special));
        }
    }

    internal void StartHelpingHand(Pokemon target) => StartEffect(scope => HelpingHand(scope, target));
    private IEnumerator HelpingHand(EffectScope scope, Pokemon target)
    {
        scope.Own(new VaporModifierLease(target, Stat.Attack, 35, true));
        scope.Own(new VaporModifierLease(target, Stat.SpAttack, 35, true));
        scope.Visual(VaporEffect.HelpingHand, target.transform.position, 3.5f, target.NetworkObjectId);
        SupportAlly(target);
        double end = Now + 3.5;
        while (scope.Live && VaporMoveRuntime.Living(target) && Now < end) yield return null;
    }

    internal void StartAquaRing(Pokemon target, bool upgraded)
    {
        if (rings.TryGetValue(target.NetworkObjectId, out EffectScope previous)) previous.Dispose();
        EffectScope scope = CreateEffect();
        rings[target.NetworkObjectId] = scope;
        RunEffect(scope, AquaRing(scope, target, upgraded));
    }

    private IEnumerator AquaRing(EffectScope scope, Pokemon target, bool upgraded)
    {
        ulong recipientId = target.NetworkObjectId;
        var shield = scope.Own(new VaporShieldLease(target, Mathf.FloorToInt(target.GetMaxHp() * VaporMoveRules.ShieldRatio(upgraded))));
        var attack = scope.Own(new VaporModifierLease(target, Stat.Attack, 25, true));
        var special = scope.Own(new VaporModifierLease(target, Stat.SpAttack, 25, true));
        scope.Visual(VaporEffect.AquaRing, target.transform.position, 6f, recipientId, 1f, shield.Id);
        SupportAlly(target);
        double start = Now;
        int pulses = 0;
        while (scope.Live && shield.Intact && Now - start < VaporMoveRules.RingDuration)
        {
            double age = Now - start;
            if (age >= 4.5) { attack.Dispose(); special.Dispose(); }
            if (pulses < 8 && age >= 0.15 + pulses * VaporMoveRules.RingTick)
            {
                pulses++;
                HealAlly(target, VaporMoveRules.Heal(Pokemon, 0.51f, 6, 300));
                ShowEffect(VaporEffect.HealPulse, target.transform.position, 0.3f, recipientId, 0.85f);
            }
            yield return null;
        }
        if (scope.Live && VaporMoveRuntime.Living(target) && !shield.Intact)
            ShowEffect(VaporEffect.ShieldBreak, target.transform.position, 0.3f, recipientId);
        if (rings.TryGetValue(recipientId, out EffectScope current) && current == scope) rings.Remove(recipientId);
    }

    internal void StartRainDance(Vector3 center, bool upgraded)
    {
        CancelRainDance();
        rain = CreateEffect();
        RunEffect(rain, RainDanceField(rain, center, upgraded));
    }

    internal void CancelRainDance() { rain?.Dispose(); rain = null; }

    private IEnumerator RainDanceField(EffectScope scope, Vector3 center, bool upgraded)
    {
        scope.Visual(VaporEffect.Rain, center, VaporMoveRules.RainDuration, size: VaporMoveRules.RainRadius);
        var occupants = scope.Own(new Occupants());
        double start = Now, nextScan = start, nextHeal = start + 1.2;
        while (scope.Live && Now - start < VaporMoveRules.RainDuration)
        {
            if (Now >= nextScan)
            {
                nextScan = Now + 0.1;
                occupants.Refresh(VaporMoveRuntime.Targets(center, VaporMoveRules.RainRadius), target =>
                {
                    var leases = new List<IDisposable>();
                    if (VaporMoveRuntime.CanHeal(Actor, target))
                    {
                        leases.Add(new VaporModifierLease(target, Stat.Defense, VaporMoveRules.RainDefense(upgraded), true));
                        leases.Add(new VaporModifierLease(target, Stat.SpDefense, VaporMoveRules.RainDefense(upgraded), true));
                    }
                    else if (CanSlow(target)) leases.Add(new VaporModifierLease(target, Stat.Speed, 20, false));
                    return leases;
                }, target => VaporMoveRuntime.CanHeal(Actor, target) || CanSlow(target));
            }
            if (Now >= nextHeal)
            {
                nextHeal += 1.2;
                foreach (Pokemon target in VaporMoveRuntime.Targets(center, VaporMoveRules.RainRadius))
                    HealAlly(target, VaporMoveRules.Heal(Pokemon, 0.34f, 6, 250));
                ShowEffect(VaporEffect.HealPulse, center, 0.35f, size: VaporMoveRules.RainRadius);
            }
            yield return null;
        }
    }

    internal void StartWhirlpool(Vector3 center, bool upgraded)
    {
        whirlpool?.Dispose();
        whirlpool = CreateEffect();
        RunEffect(whirlpool, WhirlpoolField(whirlpool, center, upgraded));
    }

    private bool CanSlow(Pokemon target) => VaporMoveRuntime.CanHit(Actor, target) && target.Type != PokemonType.Objective
        && !target.HasStatusEffect(StatusType.Unstoppable);

    private IEnumerator WhirlpoolField(EffectScope scope, Vector3 center, bool upgraded)
    {
        scope.Visual(VaporEffect.Whirlpool, center, VaporMoveRules.WhirlpoolDuration, size: VaporMoveRules.WhirlpoolRadius);
        var occupants = scope.Own(new Occupants());
        double start = Now, nextScan = start;
        int pulses = 0;
        bool collapsed = false;
        while (scope.Live && Now - start < VaporMoveRules.WhirlpoolDuration)
        {
            double age = Now - start;
            var targets = VaporMoveRuntime.Targets(center, VaporMoveRules.WhirlpoolRadius);
            if (Now >= nextScan)
            {
                nextScan = Now + 0.1;
                occupants.Refresh(targets, target => new List<IDisposable>
                    { new VaporModifierLease(target, Stat.Speed, VaporMoveRules.WhirlpoolSlow(upgraded), false) }, CanSlow);
            }
            while (pulses < 6 && age >= pulses * 0.5)
            {
                pulses++;
                foreach (Pokemon target in targets)
                    if (VaporMoveRuntime.CanHit(Actor, target)) target.TakeDamageRPC(VaporMoveRules.WhirlpoolTick(Controller.NetworkObjectId));
            }
            if (!collapsed && age >= VaporMoveRules.WhirlpoolCollapse)
            {
                collapsed = true;
                ShowEffect(VaporEffect.Collapse, center, 0.3f, size: VaporMoveRules.WhirlpoolRadius);
                foreach (ulong expired in new List<ulong>(pullReady.Keys))
                    if (pullReady[expired] <= Time.time) pullReady.Remove(expired);
                foreach (Pokemon target in targets)
                {
                    if (!CanSlow(target) || target.Type != PokemonType.Player || target.HasStatusEffect(StatusType.HindranceResistance)
                        || pullReady.ContainsKey(target.NetworkObjectId) || !target.TryGetComponent(out PlayerMovement movement)) continue;
                    pullReady[target.NetworkObjectId] = Time.time + VaporMoveRules.PullImmunity;
                    movement.VaporeonDisplaceRPC(center, VaporMoveRules.PullDistance, true);
                }
            }
            yield return null;
        }
    }

    internal void StartDiveImpact(Vector3 center) => StartEffect(scope => DiveImpact(scope, center));

    private IEnumerator DiveImpact(EffectScope scope, Vector3 center)
    {
        scope.Own(AcquireActionLock(true));
        ushort identifier = VaporEffectIds.Acquire();
        Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, VaporMoveRules.DiveWarning + 0.1f, true, identifier));
        scope.Own(new CombatCallbackLease(() =>
        {
            if (Pokemon != null && Pokemon.IsSpawned) Pokemon.RemoveStatusEffectWithID(identifier);
            VaporEffectIds.Release(identifier);
        }));
        scope.Visual(VaporEffect.DiveWarning, center, VaporMoveRules.DiveWarning, size: VaporMoveRules.DiveRadius);
        yield return Wait(scope, VaporMoveRules.DiveWarning);
        if (!scope.Live) yield break;
        ShowEffect(VaporEffect.DiveSplash, center, 0.35f, size: VaporMoveRules.DiveRadius);
        foreach (Pokemon target in VaporMoveRuntime.Targets(center, VaporMoveRules.DiveRadius))
        {
            if (!VaporMoveRuntime.CanHit(Actor, target)) continue;
            target.TakeDamageRPC(new DamageInfo(Controller.NetworkObjectId, 0.7f, 6, 200, DamageType.Special));
            if (!VaporMoveRuntime.Living(target) || target.Type == PokemonType.Objective) continue;
            target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.25f, true, 0));
            if (target.TryGetComponent(out PlayerMovement movement)) movement.VaporeonDisplaceRPC(center, 0.6f, false);
        }
        RecoverAnimation();
    }

    internal void ApplyTempest(Vector3 center)
    {
        ShowEffect(VaporEffect.TempestImpact, center, 0.45f, size: VaporMoveRules.TempestRadius);
        StartEffect(scope => TempestDebuffs(scope, center));
    }

    private IEnumerator TempestDebuffs(EffectScope scope, Vector3 center)
    {
        foreach (Pokemon target in VaporMoveRuntime.Targets(center, VaporMoveRules.TempestRadius))
        {
            if (VaporMoveRuntime.CanHit(Actor, target))
            {
                target.TakeDamageRPC(VaporMoveRules.TempestDamage(Controller.NetworkObjectId));
                if (!VaporMoveRuntime.Living(target)) continue;
                scope.Own(new VaporModifierLease(target, Stat.Defense, 35, false));
                scope.Own(new VaporModifierLease(target, Stat.SpDefense, 35, false));
            }
            else if (VaporMoveRuntime.CanHeal(Actor, target)) HealAlly(target, VaporMoveRules.Heal(Pokemon, 2.6f, 5, 840));
        }
        yield return Wait(scope, 5);
    }

    internal int ShowEffect(VaporEffect effect, Vector3 position, float duration, ulong target = ulong.MaxValue, float size = 1f, int detail = 0)
    {
        if (!IsServer || !IsSpawned) return 0;
        int identifier = ++nextVisual;
        visuals.Add(identifier, new VisualState { Id = identifier, Effect = effect, Position = position,
            Duration = duration, Target = target, Size = size, Detail = detail, Started = Now });
        revision++;
        return identifier;
    }

    internal void StopEffect(int identifier)
    {
        if (identifier != 0 && IsServer && visuals.Remove(identifier)) revision++;
    }

    internal void SetDiving(bool value) { if (diving != value) { diving = value; revision++; } }
    internal void RecoverAnimation() { if (Living) VaporMoveRuntime.ReturnToLocomotion(Actor); }

    internal IDisposable AcquireActionLock(bool movement, bool lockServer = true, int exceptSlot = -1)
    {
        var action = new CombatActionLock(Actor, movement, lockServer: lockServer, unlockedMoveSlot: exceptSlot);
        int identifier = ++nextLock;
        activeLocks.Add(identifier, new LockState { Movement = movement, ExceptSlot = exceptSlot });
        RefreshLockState();
        return new CombatCallbackLease(() =>
        {
            action.Dispose();
            activeLocks.Remove(identifier);
            RefreshLockState();
        });
    }

    private void RefreshLockState()
    {
        castLocked = activeLocks.Count > 0;
        castRooted = false;
        unlockedSlot = -1;
        foreach (LockState state in activeLocks.Values)
        {
            castRooted |= state.Movement;
            if (activeLocks.Count == 1) unlockedSlot = state.ExceptSlot;
        }
        revision++;
    }

    internal void StartHop(Vector3 origin, Vector3 destination)
    {
        hopOrigin = origin; hopDestination = destination; hopStarted = Now; hopActive = true; revision++;
    }

    internal void EndHop() { if (hopActive) { hopActive = false; revision++; } }

    private EffectScope CreateEffect()
    {
        var scope = new EffectScope(this);
        effects.Add(scope);
        return scope;
    }

    private void StartEffect(Func<EffectScope, IEnumerator> create)
    {
        EffectScope scope = CreateEffect();
        RunEffect(scope, create(scope));
    }

    private void RunEffect(EffectScope scope, IEnumerator routine) => Actor.StartCoroutine(RunEffectRoutine(scope, routine));

    private IEnumerator RunEffectRoutine(EffectScope scope, IEnumerator routine)
    {
        try { while (scope.Live && routine.MoveNext()) yield return routine.Current; }
        finally { (routine as IDisposable)?.Dispose(); scope.Dispose(); effects.Remove(scope); }
    }

    private IEnumerator Wait(EffectScope scope, double duration)
    {
        double end = Now + duration;
        while (scope.Live && Now < end) yield return null;
    }

    public override string CapturePresentationState()
    {
        var snapshot = new PresentationState
        {
            Revision = revision, Charge = charge, Diving = diving, CastLocked = castLocked, CastRooted = castRooted,
            UnlockedSlot = unlockedSlot, HopActive = hopActive, HopStarted = hopStarted, HopOrigin = hopOrigin,
            HopDestination = hopDestination, RecoveryUntil = recoveryUntil, Visuals = ActiveVisuals()
        };
        string result = EncodeSnapshot(snapshot);
        if (result.Length <= 4090) return result;
        var retained = new List<VisualState>(snapshot.Visuals);
        retained.RemoveAll(visual => !PersistentVisual(visual.Effect));
        snapshot.Visuals = retained.ToArray();
        return EncodeSnapshot(snapshot);
    }

    private VisualState[] ActiveVisuals()
    {
        var active = new List<VisualState>();
        foreach (VisualState visual in visuals.Values)
            if (visual.Duration <= 0 || Now < visual.Started + visual.Duration) active.Add(visual);
        return active.ToArray();
    }

    private static bool PersistentVisual(VaporEffect effect) => effect == VaporEffect.Basic || effect == VaporEffect.Boosted
        || effect == VaporEffect.Swift || effect == VaporEffect.HelpingHand
        || effect == VaporEffect.AquaRing || effect == VaporEffect.Rain || effect == VaporEffect.Whirlpool
        || effect == VaporEffect.DiveWake || effect == VaporEffect.DiveWarning || effect == VaporEffect.TempestWarning;

    private static string EncodeSnapshot(PresentationState snapshot)
    {
        using (var output = new MemoryStream())
        {
            using (var compressed = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
            using (var writer = new BinaryWriter(compressed))
            {
                writer.Write(snapshot.Revision);
                writer.Write(snapshot.Charge);
                byte flags = (byte)((snapshot.Diving ? 1 : 0) | (snapshot.CastLocked ? 2 : 0)
                    | (snapshot.CastRooted ? 4 : 0) | (snapshot.HopActive ? 8 : 0));
                writer.Write(flags);
                writer.Write((sbyte)snapshot.UnlockedSlot);
                writer.Write(snapshot.HopStarted);
                writer.Write(snapshot.RecoveryUntil);
                WriteVector(writer, snapshot.HopOrigin);
                WriteVector(writer, snapshot.HopDestination);
                writer.Write(snapshot.Visuals.Length);
                foreach (VisualState visual in snapshot.Visuals)
                {
                    writer.Write(visual.Id); writer.Write((byte)visual.Effect); WriteVector(writer, visual.Position);
                    writer.Write(visual.Duration); writer.Write(visual.Target); writer.Write(visual.Size);
                    writer.Write(visual.Detail); writer.Write(visual.Started);
                }
            }
            return Convert.ToBase64String(output.ToArray());
        }
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.x); writer.Write(value.y); writer.Write(value.z);
    }

    private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static PresentationState DecodeSnapshot(string value)
    {
        using (var input = new MemoryStream(Convert.FromBase64String(value)))
        using (var compressed = new DeflateStream(input, CompressionMode.Decompress))
        using (var reader = new BinaryReader(compressed))
        {
            var snapshot = new PresentationState { Revision = reader.ReadInt64(), Charge = reader.ReadByte() };
            byte flags = reader.ReadByte();
            snapshot.Diving = (flags & 1) != 0; snapshot.CastLocked = (flags & 2) != 0;
            snapshot.CastRooted = (flags & 4) != 0; snapshot.HopActive = (flags & 8) != 0;
            snapshot.UnlockedSlot = reader.ReadSByte(); snapshot.HopStarted = reader.ReadDouble();
            snapshot.RecoveryUntil = reader.ReadDouble();
            snapshot.HopOrigin = ReadVector(reader); snapshot.HopDestination = ReadVector(reader);
            int count = reader.ReadInt32();
            if (count < 0 || count > 1024) throw new InvalidDataException("Invalid Vaporeon visual count.");
            snapshot.Visuals = new VisualState[count];
            for (int index = 0; index < count; index++)
            {
                snapshot.Visuals[index] = new VisualState { Id = reader.ReadInt32(), Effect = (VaporEffect)reader.ReadByte(),
                    Position = ReadVector(reader), Duration = reader.ReadSingle(), Target = reader.ReadUInt64(), Size = reader.ReadSingle(),
                    Detail = reader.ReadInt32(), Started = reader.ReadDouble() };
            }
            return snapshot;
        }
    }

    public override void ApplyPresentationState(string state)
    {
        if (IsServer || disposed || string.IsNullOrEmpty(state)) return;
        PresentationState snapshot;
        try { snapshot = DecodeSnapshot(state); }
        catch (Exception exception) when (exception is FormatException || exception is IOException || exception is ArgumentException) { return; }
        if (snapshot == null || IsStaleRevision(snapshot.Revision, revision)) return;
        revision = snapshot.Revision;
        charge = (byte)Mathf.Clamp(snapshot.Charge, 0, 2);
        diving = snapshot.Diving;
        castLocked = snapshot.CastLocked; castRooted = snapshot.CastRooted; unlockedSlot = snapshot.UnlockedSlot;
        hopActive = snapshot.HopActive; hopStarted = snapshot.HopStarted;
        recoveryUntil = snapshot.RecoveryUntil;
        hopOrigin = snapshot.HopOrigin; hopDestination = snapshot.HopDestination;
        visuals.Clear();
        if (snapshot.Visuals != null)
            foreach (VisualState visual in snapshot.Visuals)
                if (visual != null && visual.Id > 0 && VaporMoveRules.Finite(visual.Position)) visuals[visual.Id] = visual;
        PresentState();
    }

    private static bool IsStaleRevision(long incoming, long current) => incoming < current;

    private void PresentState()
    {
        if (!Controller.IsClient || disposed) return;
        foreach (int identifier in new List<int>(renderedVisuals.Keys))
            if (!visuals.ContainsKey(identifier)) RemoveVisual(identifier);
        foreach (VisualState visual in visuals.Values)
            if (!renderedVisuals.ContainsKey(visual.Id) && (visual.Duration <= 0 || Now < visual.Started + visual.Duration))
                LoadVisual(visual);
        PresentMovement();
        PresentModel();
    }

    private void LoadVisual(VisualState visual)
    {
        var request = new VisualRequest();
        renderedVisuals.Add(visual.Id, request);
        Addressables.LoadAssetAsync<GameObject>(VaporMoveRules.EffectPath(visual.Effect)).Completed += handle =>
        {
            float age = disposed || Actor == null || !Actor.IsSpawned ? visual.Duration : (float)Math.Max(0, Now - visual.Started);
            if (disposed || request.Cancelled || !IsSpawned || !visuals.ContainsKey(visual.Id)
                || (visual.Duration > 0 && age >= visual.Duration) || handle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(handle);
                if (renderedVisuals.TryGetValue(visual.Id, out VisualRequest current) && current == request) renderedVisuals.Remove(visual.Id);
                return;
            }
            GameObject instance = UnityEngine.Object.Instantiate(handle.Result, visual.Position, Quaternion.identity);
            request.Instance = instance.GetComponent<VaporVisual>();
            if (request.Instance == null)
            {
                UnityEngine.Object.Destroy(instance);
                Addressables.Release(handle);
                renderedVisuals.Remove(visual.Id);
                return;
            }
            request.Instance.Initialize(Actor, visual.Effect, visual.Position, visual.Duration, age, visual.Target,
                visual.Size, visual.Detail, handle, () =>
                {
                    if (renderedVisuals.TryGetValue(visual.Id, out VisualRequest current) && current == request)
                        renderedVisuals.Remove(visual.Id);
                });
        };
    }

    private void RemoveVisual(int identifier)
    {
        if (!renderedVisuals.TryGetValue(identifier, out VisualRequest request)) return;
        renderedVisuals.Remove(identifier);
        request.Cancelled = true;
        if (request.Instance != null) UnityEngine.Object.Destroy(request.Instance.gameObject);
    }

    private void PresentMovement()
    {
        if (!IsOwner || IsServer || Actor.PlayerMovement == null) return;
        if (presentedLock != castLocked || presentedRoot != castRooted || presentedUnlockedSlot != unlockedSlot)
        {
            presentationLock?.Dispose();
            presentationLock = castLocked ? new CombatActionLock(Actor, castRooted, lockServer: false, unlockedMoveSlot: unlockedSlot) : null;
            presentedLock = castLocked; presentedRoot = castRooted; presentedUnlockedSlot = unlockedSlot;
        }
        if (diving && !Actor.PlayerMovement.IsVaporeonDiveTraversing) Actor.PlayerMovement.BeginVaporeonDiveTraversal();
        else if (!diving && Actor.PlayerMovement.IsVaporeonDiveTraversing) Actor.PlayerMovement.EndVaporeonDiveTraversal();
        if (hopActive)
        {
            if (presentationHop == null) { presentationHop = new WallCrossingTraversal(); presentationHop.Begin(Actor.PlayerMovement); }
            float progress = Mathf.Clamp01((float)(Now - hopStarted) / VaporMoveRules.Windup(VaporCast.RainDance));
            presentationHop.Move(Vector3.Lerp(hopOrigin, hopDestination, progress) - transform.position);
        }
        else if (presentationHop != null) { presentationHop.End(); presentationHop = null; }
    }

    private void PresentModel()
    {
        Transform current = Pokemon != null && Pokemon.ActiveModel != null ? Pokemon.ActiveModel.transform : null;
        if (current != model)
        {
            if (model != null && modelPosed) model.localPosition = modelOrigin;
            model = current; modelPosed = false; sink = 0;
            if (model != null) modelOrigin = model.localPosition;
        }
        if (model == null) return;
        float elapsed = (float)(Now - hopStarted);
        bool hopping = elapsed >= 0 && elapsed < 0.35f && Pokemon.CurrentHp > 0;
        bool submerged = diving && Pokemon.CurrentHp > 0;
        if (!hopping && !submerged && sink <= 0)
        {
            if (modelPosed) model.localPosition = modelOrigin; else modelOrigin = model.localPosition;
            modelPosed = false; return;
        }
        modelPosed = true;
        sink = Mathf.MoveTowards(sink, submerged ? 1.7f : 0, Time.deltaTime * 12f);
        float height = hopping ? Mathf.Sin(elapsed / 0.35f * Mathf.PI) * 0.7f : 0;
        model.localPosition = modelOrigin + Vector3.up * (height - sink);
    }

    private void OnDeath(DamageInfo damage)
    {
        foreach (EffectScope scope in new List<EffectScope>(effects)) scope.Dispose();
        effects.Clear(); rings.Clear(); rain = null; whirlpool = null;
        basicRecovery?.Dispose(); basicRecovery = null;
        charge = 0; diving = false; castLocked = false; hopActive = false; recoveryUntil = 0; visuals.Clear();
        if (IsServer) revision++;
    }

    public override void Dispose()
    {
        if (disposed) return;
        OnDeath(default(DamageInfo));
        disposed = true;
        presentationLock?.Dispose(); presentationLock = null;
        if (presentationHop != null) { presentationHop.Cancel(); presentationHop = null; }
        if (Actor != null && Actor.PlayerMovement != null && Actor.PlayerMovement.IsVaporeonDiveTraversing)
            Actor.PlayerMovement.EndVaporeonDiveTraversal(false);
        foreach (int identifier in new List<int>(renderedVisuals.Keys)) RemoveVisual(identifier);
        if (model != null && modelPosed) model.localPosition = modelOrigin;
        if (boundPokemon != null) boundPokemon.OnDeath -= OnDeath;
        boundPokemon = null;
    }

    private sealed class EffectScope : IDisposable
    {
        private readonly VaporeonCharacterRuntime owner;
        private readonly CombatScope scope = new CombatScope();
        public bool Live => !scope.IsDisposed && owner.Living;
        public EffectScope(VaporeonCharacterRuntime owner) { this.owner = owner; }
        public Resource Own<Resource>(Resource resource) where Resource : IDisposable => scope.Own(resource);
        public void Visual(VaporEffect effect, Vector3 position, float duration, ulong target = ulong.MaxValue, float size = 1f, int detail = 0)
        {
            int identifier = owner.ShowEffect(effect, position, duration, target, size, detail);
            scope.Own(new CombatCallbackLease(() => owner.StopEffect(identifier)));
        }
        public void Dispose() => scope.Dispose();
    }

    private sealed class Occupants : IDisposable
    {
        private readonly Dictionary<Pokemon, List<IDisposable>> occupants = new Dictionary<Pokemon, List<IDisposable>>();
        public void Refresh(HashSet<Pokemon> targets, Func<Pokemon, List<IDisposable>> create, Func<Pokemon, bool> valid)
        {
            foreach (Pokemon target in new List<Pokemon>(occupants.Keys))
                if (!targets.Contains(target) || !valid(target)) { foreach (IDisposable lease in occupants[target]) lease.Dispose(); occupants.Remove(target); }
            foreach (Pokemon target in targets)
                if (valid(target) && !occupants.ContainsKey(target)) occupants.Add(target, create(target));
        }
        public void Dispose() { foreach (List<IDisposable> leases in occupants.Values) foreach (IDisposable lease in leases) lease.Dispose(); occupants.Clear(); }
    }
}
