using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

public enum OpenWorldNpcKind { Trainer, Pokemon, Generic }
public enum OpenWorldNpcInteraction { Animation, Dialogue, Seat, Player, Jukebox }

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class OpenWorldNpc : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private OpenWorldNpcKind kind;
    [Tooltip("Model root. Optional for Generic NPCs; assign a child object for a custom model.")]
    [SerializeField] private GameObject visual;
    [SerializeField, TextArea] private string clothes;

    [Header("Animation")]
    [SerializeField] private AnimationClip idleAnimation;
    [SerializeField] private AnimationClip interactAnimation;
    [SerializeField, Min(0f)] private float animationBlendDuration = 0.2f;
    [SerializeField] private bool playTrainerAnimation;
    [SerializeField] private AnimationClip maleTrainerAnimation;
    [SerializeField] private AnimationClip femaleTrainerAnimation;

    [Header("Animation sampling")]
    [SerializeField] private bool optimizeAnimationSampling = true;
    [SerializeField, Min(0f)] private float fullRateDistance = 12f;
    [SerializeField, Min(0f)] private float reducedRateDistance = 30f;
    [SerializeField, Min(1f)] private float mediumAnimationRate = 30f;
    [SerializeField, Min(1f)] private float distantAnimationRate = 15f;
    [SerializeField, Min(0f)] private float samplingDistanceHysteresis = 2f;
    [Tooltip("Balanced quality: full-rate and 30 Hz distance limits.")]
    [SerializeField, Min(0f)] private float balancedFullRateDistance = 18f;
    [SerializeField, Min(0f)] private float balancedReducedRateDistance = 50f;
    [Tooltip("High Fidelity quality: full rate through this distance, then 30 Hz without a 15 Hz tier.")]
    [SerializeField, Min(0f)] private float highFullRateDistance = 30f;

    [Header("Interaction")]
    [SerializeField] private OpenWorldNpcInteraction interaction;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
    [SerializeField] private string interactionLabel = "Talk";
    [SerializeField] private Dialogue[] dialogues = Array.Empty<Dialogue>();
    [SerializeField] private DialoguePortraitMode portraitMode = DialoguePortraitMode.Hidden;
    [SerializeField] private GameObject portraitPrefab;

    [Header("Seat")]
    [Tooltip("Optional feet anchor. Its forward direction is the direction the seated trainer faces.")]
    [SerializeField] private Transform seatAnchor;
    [Tooltip("Offset from the anchor to the trainer's feet, in anchor-local units. The hips sit behind the feet; place the feet in front of the cushion, at floor height.")]
    [SerializeField] private Vector3 seatFeetOffset = new Vector3(0f, 0f, 0.45f);

    public Vector3 SeatPosition => (seatAnchor != null ? seatAnchor : transform).TransformPoint(seatFeetOffset);
    public float SeatYaw => (seatAnchor != null ? seatAnchor : transform).eulerAngles.y;

    [Header("Prompt")]
    [SerializeField] private Canvas prompt;
    [SerializeField] private Button interactButton;
    [SerializeField] private TMP_Text promptText;

    private static readonly List<OpenWorldNpc> ActiveNpcs = new List<OpenWorldNpc>();
    private static int selectionFrame = -1;
    private static OpenWorldNpc nearest;
    private TrainerModel trainer;
    private Animator animator;
    private GameObject cachedVisual;
    private Animator[] visualAnimators = Array.Empty<Animator>();
    private bool animatorCacheDirty = true;
    private string cachedInteractionLabel;
    private string cachedPromptLabel;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private AnimationClip currentClip;
    private float animationTime;
    private RuntimeAnimatorController savedController;
    private AnimatorCullingMode savedCullingMode;
    private bool ownsAnimator;
    private bool clipHasEvents;
    private Renderer[] animationRenderers = Array.Empty<Renderer>();
    private int samplingBand = -1;
    private int samplingQuality = -1;
    private float nextAnimationSample;
    private static Camera samplingCamera;
    private static int samplingCameraFrame = -1;
    private bool initialized;
    private readonly OpenWorldAnimationPoseBlend poseBlend = new OpenWorldAnimationPoseBlend();

    public OpenWorldNpcInteraction Interaction => kind == OpenWorldNpcKind.Pokemon
        ? OpenWorldNpcInteraction.Animation : interaction;
    public bool IsInteracting { get; private set; }
    public bool IsReady => initialized && (kind == OpenWorldNpcKind.Generic ||
        (animator != null && animator.isActiveAndEnabled && (trainer == null || trainer.IsInitialized)));

    private void OnEnable()
    {
        ActiveNpcs.Add(this);
        selectionFrame = -1;
        animatorCacheDirty = true;
        if (trainer != null) trainer.onClothesInitialized += OnTrainerClothesInitialized;
        if (initialized) RefreshFakeShadow();
        if (interactButton != null) interactButton.onClick.AddListener(Interact);
        SetPrompt(false);
    }

    private void Start()
    {
        if (visual == null)
        {
            if (kind == OpenWorldNpcKind.Generic) initialized = true;
            else Debug.LogWarning($"NPC {name} needs a visual model.", this);
            return;
        }
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var modelAnimator in visual.GetComponentsInChildren<Animator>(true)) modelAnimator.applyRootMotion = false;
        foreach (var canvas in visual.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        trainer = kind == OpenWorldNpcKind.Trainer ? visual.GetComponent<TrainerModel>() : null;
        if (trainer != null) trainer.onClothesInitialized += OnTrainerClothesInitialized;
        if (kind == OpenWorldNpcKind.Trainer)
        {
            if (trainer == null)
            {
                Debug.LogWarning($"NPC {name} needs a TrainerModel on its visual root.", this);
                return;
            }
            try
            {
                var outfit = string.IsNullOrWhiteSpace(clothes)
                    ? new PlayerClothesInfo { IsMale = true, HairColor = new Color32(80, 60, 40, 255), EyeColor = new Color32(80, 100, 120, 255) }
                    : PlayerClothesInfo.Deserialize(clothes);
                trainer.InitializeClothes(outfit);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"Invalid NPC clothing on {name}: {exception.Message}", this);
                return;
            }
        }
        initialized = true;
        RefreshFakeShadow();
    }

    private void Update()
    {
        var activeAnimator = !initialized ? null : trainer != null
            ? (trainer.IsInitialized ? trainer.ActiveAnimator : null)
            : FindVisualAnimator();
        if (animator != activeAnimator)
        {
            StopAnimation();
            animator = activeAnimator;
            if (animator != null) PlayClip(idleAnimation);
        }
        UpdateSelection();
        bool selected = nearest == this && CanInteract(OpenWorldTrainerAvatar.LocalAvatar);
        SetPrompt(selected);
        if (!selected) return;
        var camera = OpenWorldTrainerAvatar.LocalAvatar.LocalCamera;
        if (prompt != null && camera != null)
        {
            prompt.worldCamera = camera;
            prompt.transform.rotation = camera.transform.rotation;
        }
        if ((Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) Interact();
    }

    private void LateUpdate() => UpdateAnimation(Time.deltaTime);

    private void OnTransformChildrenChanged() => animatorCacheDirty = true;

    private Animator FindVisualAnimator()
    {
        if (animatorCacheDirty || cachedVisual != visual)
        {
            cachedVisual = visual;
            visualAnimators = visual != null
                ? visual.GetComponentsInChildren<Animator>(true) : Array.Empty<Animator>();
            animatorCacheDirty = false;
        }
        // Preserve active-child selection without searching the hierarchy every frame.
        foreach (var candidate in visualAnimators)
            if (candidate != null && (candidate.gameObject == visual || candidate.gameObject.activeInHierarchy))
                return candidate;
        return null;
    }

    private static void UpdateSelection()
    {
        if (selectionFrame == Time.frameCount) return;
        selectionFrame = Time.frameCount;
        nearest = null;
        var avatar = OpenWorldTrainerAvatar.LocalAvatar;
        if (avatar == null) return;
        Vector3 avatarPosition = avatar.Position;
        bool? canStartEmote = null;
        float closest = float.PositiveInfinity;
        foreach (var npc in ActiveNpcs)
        {
            float distance = (npc.transform.position - avatarPosition).sqrMagnitude;
            if (distance >= closest) continue;
            if (!npc.CanInteractAtDistance(avatar, distance, ref canStartEmote)) continue;
            closest = distance;
            nearest = npc;
        }
    }

    public bool IsWithinInteractionDistance(Vector3 position) =>
        (transform.position - position).sqrMagnitude <= interactionDistance * interactionDistance;

    private bool CanInteract(OpenWorldTrainerAvatar avatar)
    {
        if (avatar == null) return false;
        bool? canStartEmote = null;
        return CanInteractAtDistance(avatar, (transform.position - avatar.Position).sqrMagnitude, ref canStartEmote);
    }

    private bool CanInteractAtDistance(OpenWorldTrainerAvatar avatar, float distance, ref bool? canStartEmote)
    {
        // Standing up remains available even if the avatar has moved outside the seat's radius.
        if (Interaction == OpenWorldNpcInteraction.Seat && avatar.IsUsingSeat(this))
            return isActiveAndEnabled && avatar.CanLeaveSeat;
        if (!isActiveAndEnabled || !IsReady || IsInteracting || !avatar.IsSpawned ||
            !(distance <= interactionDistance * interactionDistance)) return false;

        if (Interaction == OpenWorldNpcInteraction.Jukebox && (OpenWorldStageMenu.Instance == null ||
            OpenWorldStageJukebox.Instance == null || !OpenWorldStageJukebox.Instance.CanUseConsole(avatar))) return false;
        if (Interaction == OpenWorldNpcInteraction.Player)
        {
            var target = GetComponent<OpenWorldTrainerAvatar>();
            if (target == null || !target.IsSpawned || target == avatar || string.IsNullOrEmpty(target.PlayerId) ||
                OpenWorldTrainerMenu.Instance == null) return false;
        }
        if (!canStartEmote.HasValue) canStartEmote = avatar.CanStartEmote;
        return canStartEmote.Value &&
            (Interaction != OpenWorldNpcInteraction.Seat || avatar.IsSeatAvailable(this));
    }

    public void Interact()
    {
        UpdateSelection();
        var avatar = OpenWorldTrainerAvatar.LocalAvatar;
        if (nearest != this || !CanInteract(avatar)) return;
        if (Interaction == OpenWorldNpcInteraction.Jukebox)
        {
            OpenWorldStageJukebox.Instance.OpenMenu();
            SetPrompt(false);
            return;
        }
        if (Interaction == OpenWorldNpcInteraction.Player)
        {
            OpenWorldTrainerMenu.Instance.Open(GetComponent<OpenWorldTrainerAvatar>());
            SetPrompt(false);
            return;
        }
        if (Interaction == OpenWorldNpcInteraction.Seat)
        {
            if (avatar.IsUsingSeat(this)) avatar.TryLeaveSeat();
            else avatar.TryUseSeat(this);
            SetPrompt(false);
            return;
        }
        if (Interaction == OpenWorldNpcInteraction.Dialogue)
        {
            if (DialogueSystem.Instance == null || dialogues == null || dialogues.Length == 0) return;
            DialogueSystem.Instance.SetDialogues(dialogues, portraitMode, portraitPrefab);
        }
        if (interactAnimation != null && animator != null && animator.isActiveAndEnabled)
        {
            IsInteracting = true;
            PlayClip(interactAnimation);
        }
        if (playTrainerAnimation) avatar.PlayNpcInteractionAnimation(maleTrainerAnimation, femaleTrainerAnimation, animationBlendDuration);
        SetPrompt(false);
    }

    private void PlayClip(AnimationClip clip)
    {
        if (clip != null) AcquireAnimator();
        bool hasPose = poseBlend.IsInitialized;
        poseBlend.Initialize(animator);
        if (hasPose && animationBlendDuration > 0f)
        {
            // Reconstruct the last displayed pose before replacing the graph. The
            // Animator may have overwritten the transforms since our LateUpdate.
            // This also preserves an interrupted blend without caching every frame.
            if (graph.IsValid())
            {
                graph.Evaluate(0f);
                poseBlend.Apply(0f);
            }
            poseBlend.RememberPose();
        }
        poseBlend.Begin(hasPose ? animationBlendDuration : 0f);
        if (graph.IsValid()) graph.Destroy();
        currentClip = clip;
        clipHasEvents = clip != null && clip.events.Length != 0;
        animationTime = 0f;
        samplingBand = -1;
        if (animator == null || clip == null)
        {
            ReleaseAnimator();
            return;
        }
        graph = PlayableGraph.Create($"NPC {name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(graph, "NPC Animation", animator);
        output.SetSourcePlayable(playable);
        graph.Play();
        graph.Evaluate(0f);
        poseBlend.Apply(0f);
    }

    private void UpdateAnimation(float deltaTime)
    {
        if (!graph.IsValid() || currentClip == null)
        {
            poseBlend.Apply(deltaTime);
            return;
        }
        animationTime += deltaTime;
        if (IsInteracting && animationTime >= currentClip.length)
        {
            IsInteracting = false;
            PlayClip(idleAnimation);
            return;
        }
        if (!ShouldSampleAnimation()) return;
        float sampleTime = currentClip.length > 0f ? animationTime % currentClip.length : 0f;
        playable.SetTime(sampleTime);
        graph.Evaluate(0f);
        poseBlend.Apply(deltaTime);
    }

    private void AcquireAnimator()
    {
        if (ownsAnimator || animator == null) return;
        // This graph owns the pose. Leaving a controller attached allows its
        // normal update to replace the held pose between manual samples.
        savedController = animator.runtimeAnimatorController;
        savedCullingMode = animator.cullingMode;
        animator.runtimeAnimatorController = null;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        ownsAnimator = true;
        RefreshAnimationRenderers();
    }

    private void OnTrainerClothesInitialized()
    {
        RefreshFakeShadow();
        RefreshAnimationRenderers();
    }

    private void RefreshFakeShadow()
    {
        if (visual == null || kind == OpenWorldNpcKind.Generic) return;
        if (kind == OpenWorldNpcKind.Trainer && (trainer == null || !trainer.IsInitialized)) return;
        if (!TryGetComponent<FakeMeshShadow>(out var fakeShadow))
            fakeShadow = gameObject.AddComponent<FakeMeshShadow>();
        fakeShadow.SetSource(visual.transform, forceRebuild: true);
    }

    private void RefreshAnimationRenderers()
    {
        animationRenderers = animator != null
            ? animator.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        samplingBand = -1;
    }

    private void ReleaseAnimator()
    {
        if (ownsAnimator && animator != null)
        {
            if (animator.runtimeAnimatorController == null)
                animator.runtimeAnimatorController = savedController;
            animator.cullingMode = savedCullingMode;
        }
        savedController = null;
        ownsAnimator = false;
    }

    private bool ShouldSampleAnimation()
    {
        var avatar = OpenWorldTrainerAvatar.LocalAvatar;
        if (!optimizeAnimationSampling || IsInteracting || poseBlend.IsBlending ||
            clipHasEvents)
        {
            samplingBand = -1;
            return true;
        }
        if (samplingCameraFrame != Time.frameCount)
        {
            samplingCameraFrame = Time.frameCount;
            samplingCamera = avatar != null ? avatar.LocalCamera : Camera.main;
        }
        if (samplingCamera == null) return true;
        // Follow the same Performant/Balanced/High Fidelity quality indices as
        // the graphics settings, including changes made while the lobby is open.
        int quality = QualitySettings.GetQualityLevel();
        if (quality != samplingQuality)
        {
            samplingQuality = quality;
            samplingBand = -1;
        }
        float fullDistance = quality >= 2 ? highFullRateDistance :
            quality == 1 ? balancedFullRateDistance : fullRateDistance;
        float reducedDistance = quality >= 2 ? float.PositiveInfinity :
            quality == 1 ? balancedReducedRateDistance : reducedRateDistance;
        float distanceSquared = (animator.transform.position - samplingCamera.transform.position).sqrMagnitude;
        bool nearPlayer = avatar != null && (transform.position - avatar.Position).sqrMagnitude <=
            Mathf.Max(fullDistance, interactionDistance) * Mathf.Max(fullDistance, interactionDistance);
        float nearBoundary = fullDistance + (samplingBand == 0 ? samplingDistanceHysteresis : 0f);
        float farBoundary = Mathf.Max(fullDistance, reducedDistance) +
            (samplingBand == 1 ? samplingDistanceHysteresis : 0f);
        int band = nearPlayer || distanceSquared <= nearBoundary * nearBoundary ? 0 :
            distanceSquared <= farBoundary * farBoundary ? 1 : 2;
        if (band != 0)
        {
            // isVisible is conservative: shadow and other-camera visibility also
            // keep sampling alive. Do not freeze an offscreen visible shadow.
            bool hasRenderer = false;
            bool visible = false;
            foreach (var renderer in animationRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                hasRenderer = true;
                if (renderer.isVisible) { visible = true; break; }
            }
            if (hasRenderer && !visible) band = 3;
        }
        if (band != samplingBand)
        {
            samplingBand = band;
            float interval = 1f / Mathf.Max(1f, band == 1 ? mediumAnimationRate : distantAnimationRate);
            // Sample immediately on promotion/visibility changes, then distribute
            // future work across NPCs instead of aligning every sampling deadline.
            nextAnimationSample = animationTime + interval *
                (0.5f + (unchecked((uint)GetInstanceID()) % 997) / 997f);
            return band != 3;
        }
        if (band == 3) return false;
        if (band == 0) return true;
        if (animationTime < nextAnimationSample) return false;
        float period = 1f / Mathf.Max(1f, band == 1 ? mediumAnimationRate : distantAnimationRate);
        nextAnimationSample += (Mathf.Floor((animationTime - nextAnimationSample) / period) + 1f) * period;
        return true;
    }

    private void SetPrompt(bool visible)
    {
        if (prompt != null && prompt.gameObject.activeSelf != visible) prompt.gameObject.SetActive(visible);
        if (visible && promptText != null)
        {
            if (cachedPromptLabel == null || cachedInteractionLabel != interactionLabel)
            {
                cachedInteractionLabel = interactionLabel;
                cachedPromptLabel = $"{interactionLabel}  [E / A]";
            }
            var avatar = OpenWorldTrainerAvatar.LocalAvatar;
            string label = avatar != null && avatar.IsUsingSeat(this) ? "Stand up  [E / A]" : cachedPromptLabel;
            if (promptText.text != label) promptText.text = label;
        }
    }

    private void StopAnimation()
    {
        if (graph.IsValid()) graph.Destroy();
        ReleaseAnimator();
        animationRenderers = Array.Empty<Renderer>();
        samplingBand = -1;
        poseBlend.Clear();
        IsInteracting = false;
        currentClip = null;
        animator = null;
    }

    private void OnDisable()
    {
        var avatar = OpenWorldTrainerAvatar.LocalAvatar;
        if (avatar != null && avatar.IsUsingSeat(this)) avatar.ClearSeat();
        ActiveNpcs.Remove(this);
        selectionFrame = -1;
        if (nearest == this) nearest = null;
        if (interactButton != null) interactButton.onClick.RemoveListener(Interact);
        SetPrompt(false);
        StopAnimation();
        if (trainer != null) trainer.onClothesInitialized -= OnTrainerClothesInitialized;
    }

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        animationBlendDuration = Mathf.Max(0f, animationBlendDuration);
        if (kind == OpenWorldNpcKind.Pokemon) interaction = OpenWorldNpcInteraction.Animation;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        if (Interaction == OpenWorldNpcInteraction.Seat)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(SeatPosition, 0.12f);
            Gizmos.DrawRay(SeatPosition, Quaternion.Euler(0f, SeatYaw, 0f) * Vector3.forward * 0.6f);
        }
    }
}
