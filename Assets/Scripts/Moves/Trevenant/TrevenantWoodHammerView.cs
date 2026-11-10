using UnityEngine;

/// <summary>Local, baked weapons follow animated hands after Animator evaluation. Never reparents the network effect or creates geometry.</summary>
[DefaultExecutionOrder(100)]
public class TrevenantWoodHammerView : MonoBehaviour
{
    [SerializeField] private Transform leftHammer, rightHammer, slamHammer;
    [SerializeField] private Transform sweepImpact, slamImpact;
    [SerializeField] private TrailRenderer[] sweepTrails, slamTrails;
    private PlayerManager actor;
    private Transform model, leftHand, rightHand;
    private int stage;
    private double swingBegan, swingEnds, impactBegan, impactEnds;
    private Transform currentImpact;
    private float swingDuration;
    private double Now => actor != null && actor.NetworkManager != null ? actor.NetworkManager.ServerTime.Time : Time.timeAsDouble;
    public bool HasHandBindings => leftHand != null && rightHand != null;
    public void Bind(PlayerManager caster) { actor = caster; ResolveModel(); Hide(); }
    public void BindModel(Transform root)
    {
        model = root; leftHand = rightHand = null;
        if (root != null)
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "Bip001 L Hand") leftHand = bone;
                if (bone.name == "Bip001 R Hand") rightHand = bone;
            }
    }
    private void ResolveModel()
    {
        if (actor == null || actor.Pokemon == null) return;
        var animator = actor.AnimationManager != null ? actor.AnimationManager.Animator : null;
        Transform root = animator != null ? animator.transform : actor.Pokemon.ActiveModel != null ? actor.Pokemon.ActiveModel.transform : null;
        if (model != root || (root != null && !HasHandBindings)) BindModel(root);
    }
    public void Swing(int swingStage, double began, float duration)
    {
        stage = swingStage; swingBegan = began; swingDuration = Mathf.Max(0.01f, duration); swingEnds = began + swingDuration;
        SetTrails(sweepTrails, false); SetTrails(slamTrails, false);
        ApplyPose(Now);
    }
    public void Impact(int swingStage, Vector3 origin, Vector3 direction, double began)
    {
        if (sweepImpact != null) sweepImpact.gameObject.SetActive(false);
        if (slamImpact != null) slamImpact.gameObject.SetActive(false);
        currentImpact = swingStage == 1 ? sweepImpact : slamImpact;
        impactBegan = began; impactEnds = began + TrevenantMoveRules.HammerImpactLifetime;
        if (currentImpact == null) return;
        currentImpact.SetPositionAndRotation(origin + Vector3.up * 0.05f, Quaternion.LookRotation(direction));
        currentImpact.gameObject.SetActive(true);
        foreach (var system in currentImpact.GetComponentsInChildren<ParticleSystem>(true))
        { system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); system.Play(true); }
    }
    private void LateUpdate()
    {
        ResolveModel(); ApplyPose(Now);
        if (currentImpact == null) return;
        if (Now >= impactEnds) { currentImpact.gameObject.SetActive(false); currentImpact = null; return; }
        float progress = Mathf.Clamp01((float)((Now - impactBegan) / Mathf.Max(0.01f, TrevenantMoveRules.HammerImpactLifetime)));
        currentImpact.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, progress);
    }
    // Public for preview/animation tests as well as LateUpdate. Positions remain
    // exact hand anchors; only the authored weapon geometry grows/fades.
    public void ApplyPose(double now)
    {
        bool visible = HasHandBindings && now < swingEnds && now >= swingBegan && stage > 0;
        Show(leftHammer, visible && stage == 1); Show(rightHammer, visible && stage == 1); Show(slamHammer, visible && stage == 2);
        bool trails = visible && now - swingBegan > 0.025;
        Emit(sweepTrails, trails && stage == 1); Emit(slamTrails, trails && stage == 2);
        if (!visible) return;
        float age = (float)(now - swingBegan);
        float growth = Mathf.Min(Mathf.Clamp01(age / 0.06f), Mathf.Clamp01((float)(swingEnds - now) / 0.08f));
        float scale = Mathf.Lerp(0.15f, 1, growth);
        var rotation = Quaternion.Euler(TrevenantMoveRules.HammerGripEuler);
        if (stage == 1)
        {
            Attach(leftHammer, leftHand, rotation, scale * TrevenantMoveRules.HammerSweepVisualScale);
            Attach(rightHammer, rightHand, rotation, scale * TrevenantMoveRules.HammerSweepVisualScale);
        }
        else if (slamHammer != null)
        {
            Vector3 across = rightHand.position - leftHand.position;
            Vector3 shaft = -(leftHand.right + rightHand.right);
            Vector3 normal = Vector3.Cross(across, shaft);
            Quaternion pose = shaft.sqrMagnitude > 0.001f && normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(normal.normalized, shaft.normalized) : rightHand.rotation * rotation;
            slamHammer.SetPositionAndRotation((leftHand.position + rightHand.position) * 0.5f, pose);
            slamHammer.localScale = Vector3.one * (scale * TrevenantMoveRules.HammerSlamVisualScale);
        }
    }
    private static void Attach(Transform weapon, Transform hand, Quaternion rotation, float scale)
    {
        if (weapon == null) return;
        weapon.SetPositionAndRotation(hand.TransformPoint(TrevenantMoveRules.HammerGripOffset), hand.rotation * rotation);
        weapon.localScale = Vector3.one * scale;
    }
    private static void Show(Transform item, bool show) { if (item != null && item.gameObject.activeSelf != show) item.gameObject.SetActive(show); }
    private static void Emit(TrailRenderer[] trails, bool value)
    { if (trails != null) foreach (var trail in trails) if (trail != null) trail.emitting = value; }
    private static void SetTrails(TrailRenderer[] trails, bool emitting)
    { if (trails != null) foreach (var trail in trails) if (trail != null) { trail.emitting = emitting; trail.Clear(); } }
    public void Hide()
    {
        stage = 0; swingEnds = 0; currentImpact = null;
        Show(leftHammer, false); Show(rightHammer, false); Show(slamHammer, false);
        Show(sweepImpact, false); Show(slamImpact, false);
        SetTrails(sweepTrails, false); SetTrails(slamTrails, false);
    }
    private void OnDisable() { Hide(); }
}
