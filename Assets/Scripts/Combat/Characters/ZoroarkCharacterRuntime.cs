using System;
using UnityEngine;

public sealed class ZoroarkCharacterRuntime : CombatCharacterRuntime
{
    [Serializable]
    private sealed class PresentationSnapshot
    {
        public bool Night, Active, Unique, Finisher, Airborne, AerialSpent, Illusion;
        public int Count;
        public AvailableMoves WindowMove;
        public double WindowUntil, IllusionNext, IllusionUntil;
        public ulong Source;
        public int Hp, MaxHp, Level, Shield, Energy;
        public string DisplayName;
    }

    private bool comboNight, comboActive, comboFinisher;
    private int comboCount;
    public ZoroarkCharacterRuntime(PlayerManager actor) : base(actor) { }

    public ZoroarkMoveEffect ZoroarkNight { get; internal set; }
    public ZoroarkMoveEffect ZoroarkFeint { get; internal set; }
    public bool ZoroarkFeintReady { get; private set; }
    public bool ZoroarkUniqueReady { get; private set; }
    private bool zoroarkNightActive;
    private bool zoroarkAirborne, zoroarkAerialSpent;
    private IDisposable zoroarkAerialSpentLock;
    public bool ZoroarkAerialOpen => zoroarkAirborne && !zoroarkAerialSpent;
    private float zoroarkInputAt;
    private Vector2 zoroarkMovement;
    public Vector2 ZoroarkMovementInput => zoroarkMovement;
    public AvailableMoves ZoroarkWindowMove { get; private set; }
    private double zoroarkWindowUntil;
    public float ZoroarkWindowRemaining => NetworkManager != null ? Mathf.Max(0, (float)(zoroarkWindowUntil - NetworkManager.ServerTime.Time)) : 0;
    private SegmentedGaugeModule zoroarkComboGauge;
    private AuxiliaryAction illusionAction;
    private Pokemon zoroarkEvents;
    private double illusionNext, illusionUntil;
    private float illusionLocalNext;
    private bool illusionActive;
    private GameObject illusionActorModelAtStart;
    private GameObject illusionModel;
    private Animator illusionAnimator;
    private Renderer[] illusionRenderers;
    private bool illusionHasWalking;
    private Renderer[] illusionOriginalRenderers;
    private ulong illusionSource;
    private int illusionHp, illusionMax, illusionLevel, illusionShield, illusionEnergy;
    private string illusionName;
    public bool HasZoroarkDisguise => illusionActive;
    public Sprite ZoroarkDisguisePortrait { get; private set; }
    public int ZoroarkDisguiseEnergy => illusionEnergy;
    public bool ZoroarkDisguiseIsPlayer { get; private set; }
    public Pokemon ZoroarkDisguiseSource => illusionActive && NetworkManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(illusionSource, out var obj) ? obj.GetComponent<Pokemon>() : null;
    private bool IsZoroark => Pokemon != null && Pokemon.BaseStats != null && Pokemon.BaseStats.Passive == AvailablePassives.ZoroarkPassive;
    public bool IsZoroarkEnemyView => illusionActive && LobbyController.Instance != null && !Actor.CurrentTeam.IsOnSameTeam(LobbyController.Instance.GetLocalPlayerTeam());

    public override void Tick()
    {
        if (!IsZoroark) return;
        if (IsServer)
        {
            if (zoroarkEvents == null)
            {
                zoroarkEvents = Pokemon;
                Pokemon.OnOffensiveAction += BreakZoroarkIllusion;
                Pokemon.OnDamageTaken += BreakIllusionOnDamage;
                Pokemon.OnDeath += BreakIllusionOnDamage;
                Pokemon.OnEvolution += BreakZoroarkIllusion;
            }
            if (illusionActive && (NetworkManager.ServerTime.Time >= illusionUntil || Pokemon.CurrentHp <= 0
                || IllusionModelFor(Pokemon) != illusionActorModelAtStart)) BreakZoroarkIllusion();
        }
        if (!IsOwner || !Actor.IsLocalHuman) return;
        if (illusionAction == null)
        {
            illusionAction = new AuxiliaryAction("Illusion", Resources.Load<Sprite>("ZoroarkIllusionIcon"), ZoroarkMoveRules.IllusionCooldown,
                Controller.RequestZoroarkIllusionRPC, CanUseZoroarkIllusion,
                () => illusionLocalNext - Time.time);
            AuxiliaryActionUI.AuxiliaryAction = illusionAction;
        }
        if (illusionActive && (Controller.Controls.Movement.Score.WasPressedThisFrame() || Controller.Controls.Movement.Recall.WasReleasedThisFrame()))
            Controller.BreakZoroarkIllusionRPC();
    }

    public bool CanUseZoroarkIllusion()
    {
        return IsSpawned && IsZoroark && Pokemon.CurrentHp > 0 && Actor.PlayerState == PlayerState.Alive
            && !Actor.IsScoring && !Actor.IsRecalling
            && !Pokemon.HasStatusEffect(StatusType.Bound)
            && !Controller.BasicAttackStatus.HasStatus(ActionStatusType.Disabled)
            && !Controller.BasicAttackStatus.HasStatus(ActionStatusType.Busy)
            && (GameManager.Instance == null || GameManager.Instance.GameState == GameState.Playing);
    }

    public override bool HandleMovement(Vector2 input)
    {
        if (!IsZoroark || !IsSpawned || !IsOwner || Pokemon.CurrentHp <= 0) return false;
        if (Time.time >= zoroarkInputAt || (input - zoroarkMovement).sqrMagnitude > 0.01f)
        {
            zoroarkInputAt = Time.time + 0.05f;
            zoroarkMovement = input;
            Controller.ZoroarkMovementRPC(input);
        }
        return zoroarkNightActive;
    }

    public void ReceiveMovement(Vector2 input)
    {
        if (!IsZoroark || !float.IsFinite(input.x) || !float.IsFinite(input.y)) return;
        zoroarkMovement = Vector2.ClampMagnitude(input, 1);
        if (ZoroarkNight != null && Controller.ServerActionLocks == 0) ZoroarkNight.Movement(zoroarkMovement);
    }
    public Vector3 ZoroarkBounceDirection(Vector3 fallback)
        => zoroarkMovement.sqrMagnitude > 0.01f ? new Vector3(zoroarkMovement.x, 0, zoroarkMovement.y).normalized : fallback;

    public void ReceiveWindow(AvailableMoves move, double expiresAt)
    {
        if (expiresAt <= 0 && ZoroarkWindowMove != move) return;
        ZoroarkWindowMove = move;
        zoroarkWindowUntil = expiresAt;
        if (IsOwner && Actor != null && Actor.IsLocalHuman && BattleUIManager.instance != null)
            BattleUIManager.instance.ShowMoveSecondaryCooldown(1, ZoroarkWindowRemaining);
    }

    public void ReceiveCombo(bool night, int count, bool active, bool uniqueReady, bool finisherReady, bool airborne)
    {
        comboNight = night;
        comboCount = count;
        comboActive = active;
        comboFinisher = finisherReady;
        bool nextAirborne = night && active && airborne;
        if (nextAirborne != zoroarkAirborne && Controller.GetMove(MoveType.MoveA) is ZoroarkMove && Controller.GetMove(MoveType.MoveA).IsActive) Controller.GetMove(MoveType.MoveA).Cancel();
        bool feintReady = !night && active && finisherReady;
        if (ZoroarkFeintReady != feintReady && Controller.GetMove(MoveType.MoveB) is ZoroarkFeintAttack && Controller.GetMove(MoveType.MoveB).IsActive)
            Controller.GetMove(MoveType.MoveB).Cancel();
        zoroarkNightActive = night && active;
        ZoroarkUniqueReady = night && active && uniqueReady;
        ZoroarkFeintReady = feintReady;
        zoroarkAirborne = nextAirborne;
        if (!zoroarkAirborne)
        {
            zoroarkAerialSpent = false;
            zoroarkAerialSpentLock?.Dispose(); zoroarkAerialSpentLock = null;
        }
        if (IsOwner && Controller.ZoroarkMoveStatus(0) != null && Controller.GetMove(MoveType.MoveA) != null) Controller.RefreshZoroarkMoveUI();
        if (!Controller.IsClient || Actor == null || Actor.HPBar == null) return;
        if (zoroarkComboGauge == null && active)
            zoroarkComboGauge = Actor.HPBar.AddModuleFromPrefab(Resources.Load<GameObject>("HPBarModules/SegmentedGauge")?.GetComponent<SegmentedGaugeModule>());
        if (zoroarkComboGauge == null) return;
        zoroarkComboGauge.SetSteps(count, night ? ZoroarkMoveRules.NightSteps : ZoroarkMoveRules.FeintSteps);
        zoroarkComboGauge.SetFillColor(finisherReady ? new Color(1, 0.8f, 0.15f) : new Color(0.9f, 0.12f, 0.25f));
        zoroarkComboGauge.SetVisibility(active && !IsZoroarkEnemyView);
    }

    public bool CanRecast(int slot) => slot == 0 && ZoroarkAerialOpen && Controller.ZoroarkMoveStatus(0) != null
        && !Controller.ZoroarkMoveStatus(0).HasStatus(ActionStatusType.Disabled) && !Controller.ZoroarkMoveStatus(0).HasStatus(ActionStatusType.Stunned)
        && !Controller.ZoroarkMoveStatus(0).HasStatus(ActionStatusType.Busy);

    public void SpendZoroarkAerialReposition()
    {
        if (!ZoroarkAerialOpen) return;
        zoroarkAerialSpent = true;
        zoroarkAerialSpentLock?.Dispose();
        if (IsOwner)
        {
            zoroarkAerialSpentLock = Controller.AcquireMoveStatus(0, ActionStatusType.Busy);
            Controller.RefreshZoroarkMoveUI();
        }
    }

    public void ZoroarkNormalMoveStarted(Vector3 origin)
    {
        ZoroarkFeint?.MoveMark(origin);
    }
    public void ZoroarkNormalMoveHit()
    {
        ZoroarkNight?.ValidHit(false);
        ZoroarkFeint?.ValidHit(false);
    }
    public void ResetZoroarkOtherMove()
    {
        if (IsServer) Controller.ReduceMoveCooldownByFraction(MoveType.MoveA, 1);
    }

    public void ActivateZoroarkIllusion()
    {
        if (!IsServer || !IsZoroark || Pokemon.CurrentHp <= 0 || NetworkManager.ServerTime.Time < illusionNext
            || Pokemon.HasStatusEffect(StatusType.Bound) || Controller.ServerActionLocks > 0
            || (ZoroarkNight != null && ZoroarkNight.Finishing)
            || (GameManager.Instance != null && GameManager.Instance.GameState != GameState.Playing)) return;
        Pokemon chosen = null;
        float nearest = float.PositiveInfinity;
        bool playerChosen = false;
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (!obj.TryGetComponent(out Pokemon target) || target == Pokemon || target.CurrentHp <= 0
                || target.TeamMember.IsOnSameTeam(Actor.CurrentTeam) || IllusionModelFor(target) == null) continue;
            bool isPlayer = target.Type == PokemonType.Player;
            float distance = (target.transform.position - transform.position).sqrMagnitude;
            if (playerChosen && !isPlayer) continue;
            if (chosen == null || (isPlayer && !playerChosen) || distance < nearest)
            { chosen = target; playerChosen = isPlayer; nearest = distance; }
        }
        illusionNext = NetworkManager.ServerTime.Time + ZoroarkMoveRules.IllusionCooldown;
        illusionUntil = NetworkManager.ServerTime.Time + ZoroarkMoveRules.IllusionDuration;
        illusionActorModelAtStart = IllusionModelFor(Pokemon);
        Pokemon.CleanseConditions();
        Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, ZoroarkMoveRules.IllusionUnstoppable, true, ZoroarkMoveRules.IllusionStatus));
        Controller.PresentZoroarkIllusionRPC(chosen != null, chosen != null ? chosen.NetworkObjectId : 0,
            chosen != null ? chosen.CurrentHp : 0, chosen != null ? chosen.GetMaxHp() : 1,
            chosen != null ? chosen.CurrentLevel : 0, chosen != null ? chosen.ShieldHp : 0,
            chosen != null && chosen.TryGetComponent(out PlayerManager player) ? player.CurrentEnergy : 0,
            chosen != null && chosen.TryGetComponent(out PlayerManager named) && named.HPBar != null ? named.HPBar.DisplayName : "");
    }

    public void PresentIllusion(bool active, ulong source, int hp, int maxHp, int level, int shield, int energy, string displayName)
    {
        ClearIllusionPresentation();
        illusionActive = active; illusionSource = source;
        illusionHp = hp; illusionMax = maxHp; illusionLevel = level; illusionShield = shield; illusionEnergy = energy; illusionName = displayName;
        illusionLocalNext = Time.time + ZoroarkMoveRules.IllusionCooldown;
        if (!active || !Controller.IsClient) return;
        var copied = ZoroarkDisguiseSource;
        if (copied == null) return;
        var model = IllusionModelFor(copied, level);
        if (model == null) return;
        ZoroarkDisguisePortrait = copied.Portrait;
        ZoroarkDisguiseIsPlayer = copied.Type == PokemonType.Player;
        if (copied.BaseStats != null && copied.BaseStats.Evolutions != null)
            foreach (var evolution in copied.BaseStats.Evolutions)
                if (evolution.level <= level && evolution.newSprite != null) ZoroarkDisguisePortrait = evolution.newSprite;
        illusionModel = UnityEngine.Object.Instantiate(model, transform);
        illusionModel.name = "IllusionAppearance";
        illusionModel.transform.localPosition = Vector3.zero;
        illusionModel.transform.localRotation = Quaternion.identity;
        foreach (var collider in illusionModel.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var behaviour in illusionModel.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        illusionAnimator = illusionModel.GetComponentInChildren<Animator>(true);
        illusionRenderers = illusionModel.GetComponentsInChildren<Renderer>(true);
        if (illusionAnimator != null)
        {
            illusionAnimator.applyRootMotion = false;
            illusionHasWalking = Array.Exists(illusionAnimator.parameters, parameter => parameter.name == "Walking" && parameter.type == AnimatorControllerParameterType.Bool);
        }
        illusionOriginalRenderers = Pokemon.ActiveModel != null ? Pokemon.ActiveModel.GetComponentsInChildren<Renderer>(true) : null;
        if (illusionOriginalRenderers != null) foreach (var renderer in illusionOriginalRenderers) renderer.forceRenderingOff = true;
        if (IsZoroarkEnemyView && Actor.HPBar != null)
        {
            Actor.HPBar.SetPresentationOverride(illusionName, illusionHp, illusionMax, illusionLevel, illusionShield, illusionEnergy, copied.Type == PokemonType.Player);
            var copiedBar = copied.TryGetComponent(out PlayerManager copiedPlayer) ? copiedPlayer.HPBar
                : copied.TryGetComponent(out WildPokemon copiedWild) ? copiedWild.HPBar : null;
            Actor.HPBar.SetPresentationStyle(copiedBar, copied.Type == PokemonType.Player);
        }
        if (Controller.IsClient) TimedCombatVisual.Spawn(ZoroarkMoveRules.Visuals + "IllusionSmoke", transform.position, transform.forward, Vector3.one, 0.33f);
    }

    public override void LateTick()
    {
        if (illusionModel == null || Actor == null) return;
        bool visible = Actor.Vision != null && Actor.Vision.IsRendered && Pokemon.CurrentHp > 0;
        foreach (var renderer in illusionRenderers) if (renderer != null) renderer.enabled = visible;
        if (illusionOriginalRenderers != null) foreach (var renderer in illusionOriginalRenderers) if (renderer != null) renderer.forceRenderingOff = true;
        if (illusionAnimator != null && illusionHasWalking) illusionAnimator.SetBool("Walking", Actor.PlayerMovement.IsMoving);
    }

    private static GameObject IllusionModelFor(Pokemon target, int level = -1)
    {
        GameObject model = null;
        if (target.BaseStats != null && target.BaseStats.Evolutions != null)
            foreach (var evolution in target.BaseStats.Evolutions)
                if (evolution.level <= (level >= 0 ? level : target.CurrentLevel) && evolution.newModel != null) model = evolution.newModel;
        return model != null ? model : target.ActiveModel;
    }

    private void BreakIllusionOnDamage(DamageInfo _) => BreakZoroarkIllusion();
    public void BreakZoroarkIllusion()
    {
        if (IsServer && illusionActive) Controller.EndZoroarkIllusionRPC();
    }
    public void EndIllusion() { illusionActive = false; ClearIllusionPresentation(); }
    private void ClearIllusionPresentation()
    {
        if (illusionModel != null) UnityEngine.Object.Destroy(illusionModel);
        illusionModel = null; illusionAnimator = null;
        illusionRenderers = null; illusionHasWalking = false;
        if (illusionOriginalRenderers != null) foreach (var renderer in illusionOriginalRenderers) if (renderer != null) renderer.forceRenderingOff = false;
        illusionOriginalRenderers = null;
        if (Actor != null && Actor.HPBar != null) Actor.HPBar.ClearPresentationOverride();
    }
    public override void Dispose()
    {
        if (zoroarkEvents != null)
        {
            zoroarkEvents.OnOffensiveAction -= BreakZoroarkIllusion;
            zoroarkEvents.OnDamageTaken -= BreakIllusionOnDamage;
            zoroarkEvents.OnDeath -= BreakIllusionOnDamage;
            zoroarkEvents.OnEvolution -= BreakZoroarkIllusion;
        }
        zoroarkEvents = null;
        if (AuxiliaryActionUI.AuxiliaryAction == illusionAction) AuxiliaryActionUI.AuxiliaryAction = null;
        illusionAction?.Dispose(); illusionAction = null;
        zoroarkAirborne = zoroarkNightActive = zoroarkAerialSpent = false;
        bool ownedWindow = zoroarkWindowUntil > 0;
        zoroarkWindowUntil = 0;
        if (ownedWindow && Actor != null && Actor.IsLocalHuman && BattleUIManager.instance != null)
            BattleUIManager.instance.ShowMoveSecondaryCooldown(1, 0);
        ZoroarkUniqueReady = ZoroarkFeintReady = false;
        zoroarkAerialSpentLock?.Dispose(); zoroarkAerialSpentLock = null;
        illusionActive = false; ClearIllusionPresentation();
        if (zoroarkComboGauge != null) zoroarkComboGauge.SetVisibility(false);
        ZoroarkNight = null;
        ZoroarkFeint = null;
    }

    private sealed class AerialCastContext
    {
        public readonly ZoroarkMoveEffect Night;
        public AerialCastContext(ZoroarkMoveEffect night) { Night = night; }
    }

    public override bool CanStartBasicAttack => ZoroarkNight == null || !ZoroarkNight.Finishing;
    public override bool OverridesCooldown(int slot) => slot == 0 && ZoroarkAerialOpen;

    public override bool PrepareCast(CombatExecutionContext context)
    {
        if (ZoroarkNight != null && ZoroarkNight.Finishing && context.Slot != 0) return false;
        if (context.Slot == 0 && ZoroarkNight != null && ZoroarkNight.AerialWindow)
        {
            if (!ZoroarkNight.CanSpendAerialReposition) return false;
            context.IgnoreCooldown = true;
            context.CharacterData = new AerialCastContext(ZoroarkNight);
        }
        return true;
    }

    public override bool RevalidateCast(CombatExecutionContext context)
    {
        if (ZoroarkNight != null && ZoroarkNight.Finishing && context.Slot != 0) return false;
        if (context.CharacterData is AerialCastContext aerial)
            return context.Slot == 0 && aerial.Night != null && aerial.Night == ZoroarkNight
                && aerial.Night.CanSpendAerialReposition;
        return context.Slot != 0 || ZoroarkNight == null || !ZoroarkNight.AerialWindow;
    }

    public override void CommitCast(CombatExecutionContext context)
    {
        if (context.MoveId == AvailableMoves.ZoroarkFeintAttack) ResetZoroarkOtherMove();
        if (context.CharacterData is AerialCastContext aerial && aerial.Night.TrySpendAerialReposition())
            Controller.ZoroarkAerialSpentRPC();
    }

    public override void ConfigureEffect(PrefabMoveEffect effect, CombatExecutionContext context)
    {
        if (effect is ZoroarkMoveEffect zoroarkEffect)
            zoroarkEffect.AerialCast = context.CharacterData is AerialCastContext;
    }

    public override string CapturePresentationState()
    {
        return JsonUtility.ToJson(new PresentationSnapshot
        {
            Night = comboNight, Active = comboActive, Unique = ZoroarkUniqueReady, Finisher = comboFinisher,
            Count = comboCount, Airborne = zoroarkAirborne, AerialSpent = zoroarkAerialSpent,
            WindowMove = ZoroarkWindowMove, WindowUntil = zoroarkWindowUntil,
            Illusion = illusionActive, IllusionNext = illusionNext, IllusionUntil = illusionUntil,
            Source = illusionSource, Hp = illusionHp, MaxHp = illusionMax, Level = illusionLevel,
            Shield = illusionShield, Energy = illusionEnergy, DisplayName = illusionName
        });
    }

    public override void ApplyPresentationState(string state)
    {
        if (IsServer || string.IsNullOrEmpty(state)) return;
        PresentationSnapshot snapshot = JsonUtility.FromJson<PresentationSnapshot>(state);
        ReceiveWindow(snapshot.WindowMove, snapshot.WindowUntil);
        ReceiveCombo(snapshot.Night, snapshot.Count, snapshot.Active, snapshot.Unique, snapshot.Finisher, snapshot.Airborne);
        if (snapshot.AerialSpent && !zoroarkAerialSpent) SpendZoroarkAerialReposition();
        if (snapshot.Illusion != illusionActive || (snapshot.Illusion && (snapshot.Source != illusionSource || illusionModel == null)))
        {
            if (snapshot.Illusion) PresentIllusion(true, snapshot.Source, snapshot.Hp, snapshot.MaxHp, snapshot.Level,
                snapshot.Shield, snapshot.Energy, snapshot.DisplayName);
            else EndIllusion();
        }
        illusionNext = snapshot.IllusionNext;
        illusionUntil = snapshot.IllusionUntil;
        illusionLocalNext = Time.time + Mathf.Max(0, (float)(illusionNext - NetworkManager.ServerTime.Time));
    }
}
