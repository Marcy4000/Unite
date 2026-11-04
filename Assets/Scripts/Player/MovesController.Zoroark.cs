using System;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    public ZoroarkMoveEffect ZoroarkNight { get; internal set; }
    public ZoroarkMoveEffect ZoroarkFeint { get; internal set; }
    public bool ZoroarkFeintReady { get; private set; }
    public bool ZoroarkFeintActive { get; private set; }
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
    private bool IsZoroark => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.ZoroarkPassive;
    public bool IsZoroarkEnemyView => illusionActive && LobbyController.Instance != null && !playerManager.CurrentTeam.IsOnSameTeam(LobbyController.Instance.GetLocalPlayerTeam());

    private void UpdateZoroarkRuntime()
    {
        if (!IsZoroark) return;
        if (IsServer)
        {
            if (zoroarkEvents == null)
            {
                zoroarkEvents = pokemon;
                pokemon.OnOffensiveAction += BreakZoroarkIllusion;
                pokemon.OnDamageTaken += BreakIllusionOnDamage;
                pokemon.OnDeath += BreakIllusionOnDamage;
                pokemon.OnEvolution += BreakZoroarkIllusion;
            }
            if (illusionActive && (NetworkManager.ServerTime.Time >= illusionUntil || pokemon.CurrentHp <= 0
                || IllusionModelFor(pokemon) != illusionActorModelAtStart)) BreakZoroarkIllusion();
        }
        if (!IsOwner || !playerManager.IsLocalHuman) return;
        if (illusionAction == null)
        {
            illusionAction = new AuxiliaryAction("Illusion", Resources.Load<Sprite>("ZoroarkIllusionIcon"), ZoroarkMoveRules.IllusionCooldown,
                RequestZoroarkIllusionRPC, CanUseZoroarkIllusion,
                () => illusionLocalNext - Time.time);
            AuxiliaryActionUI.AuxiliaryAction = illusionAction;
        }
        if (illusionActive && (controls.Movement.Score.WasPressedThisFrame() || controls.Movement.Recall.WasReleasedThisFrame()))
            BreakZoroarkIllusionRPC();
    }

    private bool CanUseZoroarkIllusion()
    {
        // ScoreStatus.Disabled means "not standing in a scoring goal", not
        // "gameplay actions disabled". Illusion is independent of goal zones.
        return IsSpawned && IsZoroark && pokemon.CurrentHp > 0 && playerManager.PlayerState == PlayerState.Alive
            && !playerManager.IsScoring && !playerManager.IsRecalling
            && !pokemon.HasStatusEffect(StatusType.Bound)
            && !BasicAttackStatus.HasStatus(ActionStatusType.Disabled)
            && !BasicAttackStatus.HasStatus(ActionStatusType.Busy)
            && (GameManager.Instance == null || GameManager.Instance.GameState == GameState.Playing);
    }

    public bool HandleZoroarkMovement(Vector2 input)
    {
        if (!IsZoroark || !IsSpawned || !IsOwner || pokemon.CurrentHp <= 0) return false;
        if (Time.time >= zoroarkInputAt || (input - zoroarkMovement).sqrMagnitude > 0.01f)
        {
            zoroarkInputAt = Time.time + 0.05f;
            zoroarkMovement = input;
            ZoroarkMovementRPC(input);
        }
        return zoroarkNightActive;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ZoroarkMovementRPC(Vector2 input)
    {
        if (!IsZoroark || !float.IsFinite(input.x) || !float.IsFinite(input.y)) return;
        zoroarkMovement = Vector2.ClampMagnitude(input, 1);
        if (ZoroarkNight != null && ServerActionLocks == 0) ZoroarkNight.Movement(zoroarkMovement);
    }
    public Vector3 ZoroarkBounceDirection(Vector3 fallback)
        => zoroarkMovement.sqrMagnitude > 0.01f ? new Vector3(zoroarkMovement.x, 0, zoroarkMovement.y).normalized : fallback;

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ZoroarkWindowRPC(AvailableMoves move, double expiresAt)
    {
        if (expiresAt <= 0 && ZoroarkWindowMove != move) return;
        ZoroarkWindowMove = move;
        zoroarkWindowUntil = expiresAt;
        if (IsOwner && playerManager != null && playerManager.IsLocalHuman && BattleUIManager.instance != null)
            BattleUIManager.instance.ShowMoveSecondaryCooldown(1, ZoroarkWindowRemaining);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ZoroarkComboRPC(bool night, int count, bool active, bool uniqueReady, bool finisherReady, bool airborne)
    {
        bool nextAirborne = night && active && airborne;
        if (nextAirborne != zoroarkAirborne && moves[0] is ZoroarkMove && moves[0].IsActive) moves[0].Cancel();
        bool feintReady = !night && active && finisherReady;
        if (ZoroarkFeintReady != feintReady && moves[1] is ZoroarkFeintAttack && moves[1].IsActive)
            moves[1].Cancel(); // Re-aim when the mark-return action becomes a targeted finisher.
        zoroarkNightActive = night && active;
        ZoroarkUniqueReady = night && active && uniqueReady;
        ZoroarkFeintReady = feintReady;
        ZoroarkFeintActive = !night && active;
        zoroarkAirborne = nextAirborne;
        if (!zoroarkAirborne)
        {
            zoroarkAerialSpent = false;
            zoroarkAerialSpentLock?.Dispose(); zoroarkAerialSpentLock = null;
        }
        if (IsOwner && moveStatuses[0] != null && moves[0] != null) UpdateMoveUI(0);
        if (!IsClient || playerManager == null || playerManager.HPBar == null) return;
        if (zoroarkComboGauge == null && active)
            zoroarkComboGauge = playerManager.HPBar.AddModuleFromPrefab(Resources.Load<SegmentedGaugeModule>("HPBarModules/SegmentedGauge"));
        if (zoroarkComboGauge == null) return;
        zoroarkComboGauge.SetSteps(count, night ? ZoroarkMoveRules.NightSteps : ZoroarkMoveRules.FeintSteps);
        zoroarkComboGauge.SetFillColor(finisherReady ? new Color(1, 0.8f, 0.15f) : new Color(0.9f, 0.12f, 0.25f));
        zoroarkComboGauge.SetVisibility(active && !IsZoroarkEnemyView);
    }

    private bool CanRecastZoroark(int slot) => slot == 0 && ZoroarkAerialOpen && moveStatuses[0] != null
        && !moveStatuses[0].HasStatus(ActionStatusType.Disabled) && !moveStatuses[0].HasStatus(ActionStatusType.Stunned)
        && !moveStatuses[0].HasStatus(ActionStatusType.Busy);

    public void SpendZoroarkAerialReposition()
    {
        if (!IsOwner || !ZoroarkAerialOpen) return;
        zoroarkAerialSpent = true;
        zoroarkAerialSpentLock?.Dispose();
        zoroarkAerialSpentLock = AcquireMoveStatus(0, ActionStatusType.Busy);
        UpdateMoveUI(0);
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
        if (IsServer) ReduceMoveCooldownByFraction(MoveType.MoveA, 1);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestZoroarkIllusionRPC()
    {
        ActivateZoroarkIllusion();
    }

    private void ActivateZoroarkIllusion()
    {
        if (!IsServer || !IsZoroark || pokemon.CurrentHp <= 0 || NetworkManager.ServerTime.Time < illusionNext
            || pokemon.HasStatusEffect(StatusType.Bound) || ServerActionLocks > 0
            || (ZoroarkNight != null && ZoroarkNight.Finishing)
            || (GameManager.Instance != null && GameManager.Instance.GameState != GameState.Playing)) return;
        Pokemon chosen = null;
        float nearest = float.PositiveInfinity;
        bool playerChosen = false;
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (!obj.TryGetComponent(out Pokemon target) || target == pokemon || target.CurrentHp <= 0
                || target.TeamMember.IsOnSameTeam(playerManager.CurrentTeam) || IllusionModelFor(target) == null) continue;
            bool isPlayer = target.Type == PokemonType.Player;
            float distance = (target.transform.position - transform.position).sqrMagnitude;
            if (playerChosen && !isPlayer) continue;
            if (chosen == null || (isPlayer && !playerChosen) || distance < nearest)
            { chosen = target; playerChosen = isPlayer; nearest = distance; }
        }
        illusionNext = NetworkManager.ServerTime.Time + ZoroarkMoveRules.IllusionCooldown;
        illusionUntil = NetworkManager.ServerTime.Time + ZoroarkMoveRules.IllusionDuration;
        illusionActorModelAtStart = IllusionModelFor(pokemon);
        pokemon.CleanseConditions();
        pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, ZoroarkMoveRules.IllusionUnstoppable, true, ZoroarkMoveRules.IllusionStatus));
        // Cleansing still works when no eligible model exists.
        PresentZoroarkIllusionRPC(chosen != null, chosen != null ? chosen.NetworkObjectId : 0,
            chosen != null ? chosen.CurrentHp : 0, chosen != null ? chosen.GetMaxHp() : 1,
            chosen != null ? chosen.CurrentLevel : 0, chosen != null ? chosen.ShieldHp : 0,
            chosen != null && chosen.TryGetComponent(out PlayerManager player) ? player.CurrentEnergy : 0,
            chosen != null && chosen.TryGetComponent(out PlayerManager named) && named.HPBar != null ? named.HPBar.DisplayName : "");
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PresentZoroarkIllusionRPC(bool active, ulong source, int hp, int maxHp, int level, int shield, int energy, string displayName)
    {
        ClearIllusionPresentation();
        illusionActive = active; illusionSource = source;
        illusionHp = hp; illusionMax = maxHp; illusionLevel = level; illusionShield = shield; illusionEnergy = energy; illusionName = displayName;
        illusionLocalNext = Time.time + ZoroarkMoveRules.IllusionCooldown;
        if (!active || !IsClient) return;
        var copied = ZoroarkDisguiseSource;
        if (copied == null) return;
        var model = IllusionModelFor(copied, level);
        if (model == null) return;
        ZoroarkDisguisePortrait = copied.Portrait;
        ZoroarkDisguiseIsPlayer = copied.Type == PokemonType.Player;
        if (copied.BaseStats != null && copied.BaseStats.Evolutions != null)
            foreach (var evolution in copied.BaseStats.Evolutions)
                if (evolution.level <= level && evolution.newSprite != null) ZoroarkDisguisePortrait = evolution.newSprite;
        illusionModel = Instantiate(model, transform);
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
        illusionOriginalRenderers = pokemon.ActiveModel != null ? pokemon.ActiveModel.GetComponentsInChildren<Renderer>(true) : null;
        if (illusionOriginalRenderers != null) foreach (var renderer in illusionOriginalRenderers) renderer.forceRenderingOff = true;
        if (IsZoroarkEnemyView && playerManager.HPBar != null)
        {
            playerManager.HPBar.SetPresentationOverride(illusionName, illusionHp, illusionMax, illusionLevel, illusionShield, illusionEnergy, copied.Type == PokemonType.Player);
            var copiedBar = copied.TryGetComponent(out PlayerManager copiedPlayer) ? copiedPlayer.HPBar
                : copied.TryGetComponent(out WildPokemon copiedWild) ? copiedWild.HPBar : null;
            playerManager.HPBar.SetPresentationStyle(copiedBar, copied.Type == PokemonType.Player);
        }
        if (IsClient) TimedCombatVisual.Spawn(ZoroarkMoveRules.Visuals + "IllusionSmoke", transform.position, transform.forward, Vector3.one, 0.33f);
    }

    private void LateUpdate()
    {
        if (illusionModel == null || playerManager == null) return;
        bool visible = playerManager.Vision != null && playerManager.Vision.IsRendered && pokemon.CurrentHp > 0;
        foreach (var renderer in illusionRenderers) if (renderer != null) renderer.enabled = visible;
        if (illusionOriginalRenderers != null) foreach (var renderer in illusionOriginalRenderers) if (renderer != null) renderer.forceRenderingOff = true;
        if (illusionAnimator != null && illusionHasWalking) illusionAnimator.SetBool("Walking", playerManager.PlayerMovement.IsMoving);
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
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void BreakZoroarkIllusionRPC() { if (IsZoroark) BreakZoroarkIllusion(); }
    private void BreakZoroarkIllusion()
    {
        if (IsServer && illusionActive) EndZoroarkIllusionRPC();
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void EndZoroarkIllusionRPC() { illusionActive = false; ClearIllusionPresentation(); }
    private void ClearIllusionPresentation()
    {
        if (illusionModel != null) Destroy(illusionModel);
        illusionModel = null; illusionAnimator = null;
        illusionRenderers = null; illusionHasWalking = false;
        if (illusionOriginalRenderers != null) foreach (var renderer in illusionOriginalRenderers) if (renderer != null) renderer.forceRenderingOff = false;
        illusionOriginalRenderers = null;
        if (playerManager != null && playerManager.HPBar != null) playerManager.HPBar.ClearPresentationOverride();
    }
    private void DisposeZoroarkRuntime()
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
        if (ownedWindow && playerManager != null && playerManager.IsLocalHuman && BattleUIManager.instance != null)
            BattleUIManager.instance.ShowMoveSecondaryCooldown(1, 0);
        ZoroarkUniqueReady = ZoroarkFeintReady = ZoroarkFeintActive = false;
        zoroarkAerialSpentLock?.Dispose(); zoroarkAerialSpentLock = null;
        illusionActive = false; ClearIllusionPresentation();
        if (zoroarkComboGauge != null) zoroarkComboGauge.SetVisibility(false);
    }
}
