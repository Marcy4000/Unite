using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

public enum OpenWorldNpcKind { Trainer, Pokemon }
public enum OpenWorldNpcInteraction { Animation, Dialogue }

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class OpenWorldNpc : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private OpenWorldNpcKind kind;
    [SerializeField] private GameObject visual;
    [SerializeField, TextArea] private string clothes;

    [Header("Animation")]
    [SerializeField] private AnimationClip idleAnimation;
    [SerializeField] private AnimationClip interactAnimation;
    [SerializeField, Min(0f)] private float animationBlendDuration = 0.2f;
    [SerializeField] private bool playTrainerAnimation;
    [SerializeField] private AnimationClip maleTrainerAnimation;
    [SerializeField] private AnimationClip femaleTrainerAnimation;

    [Header("Interaction")]
    [SerializeField] private OpenWorldNpcInteraction interaction;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
    [SerializeField] private string interactionLabel = "Talk";
    [SerializeField] private Dialogue[] dialogues = Array.Empty<Dialogue>();
    [SerializeField] private DialoguePortraitMode portraitMode = DialoguePortraitMode.Hidden;
    [SerializeField] private GameObject portraitPrefab;

    [Header("Prompt")]
    [SerializeField] private Canvas prompt;
    [SerializeField] private Button interactButton;
    [SerializeField] private TMP_Text promptText;

    private static readonly List<OpenWorldNpc> ActiveNpcs = new List<OpenWorldNpc>();
    private static int selectionFrame = -1;
    private static OpenWorldNpc nearest;
    private TrainerModel trainer;
    private Animator animator;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private AnimationClip currentClip;
    private float animationTime;
    private bool initialized;
    private readonly OpenWorldAnimationPoseBlend poseBlend = new OpenWorldAnimationPoseBlend();

    public OpenWorldNpcInteraction Interaction => kind == OpenWorldNpcKind.Pokemon
        ? OpenWorldNpcInteraction.Animation : interaction;
    public bool IsInteracting { get; private set; }
    public bool IsReady => initialized && animator != null && animator.isActiveAndEnabled &&
        (trainer == null || trainer.IsInitialized);

    private void OnEnable()
    {
        ActiveNpcs.Add(this);
        selectionFrame = -1;
        if (interactButton != null) interactButton.onClick.AddListener(Interact);
        SetPrompt(false);
    }

    private void Start()
    {
        if (visual == null)
        {
            Debug.LogWarning($"NPC {name} needs a visual model.", this);
            return;
        }
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var modelAnimator in visual.GetComponentsInChildren<Animator>(true)) modelAnimator.applyRootMotion = false;
        foreach (var canvas in visual.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        trainer = kind == OpenWorldNpcKind.Trainer ? visual.GetComponent<TrainerModel>() : null;
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
    }

    private void Update()
    {
        var activeAnimator = !initialized ? null : trainer != null
            ? (trainer.IsInitialized ? trainer.ActiveAnimator : null)
            : visual.GetComponentInChildren<Animator>();
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

    private static void UpdateSelection()
    {
        if (selectionFrame == Time.frameCount) return;
        selectionFrame = Time.frameCount;
        nearest = null;
        var avatar = OpenWorldTrainerAvatar.LocalAvatar;
        if (avatar == null) return;
        float closest = float.PositiveInfinity;
        foreach (var npc in ActiveNpcs)
        {
            if (!npc.CanInteract(avatar)) continue;
            float distance = (npc.transform.position - avatar.Position).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance;
            nearest = npc;
        }
    }

    private bool CanInteract(OpenWorldTrainerAvatar avatar)
    {
        return isActiveAndEnabled && IsReady && !IsInteracting && avatar != null && avatar.IsSpawned &&
            avatar.CanStartEmote && (transform.position - avatar.Position).sqrMagnitude <= interactionDistance * interactionDistance;
    }

    public void Interact()
    {
        UpdateSelection();
        var avatar = OpenWorldTrainerAvatar.LocalAvatar;
        if (nearest != this || !CanInteract(avatar)) return;
        if (Interaction == OpenWorldNpcInteraction.Dialogue)
        {
            if (DialogueSystem.Instance == null || dialogues == null || dialogues.Length == 0) return;
            DialogueSystem.Instance.SetDialogues(dialogues, portraitMode, portraitPrefab);
        }
        if (interactAnimation != null)
        {
            IsInteracting = true;
            PlayClip(interactAnimation);
        }
        if (playTrainerAnimation) avatar.PlayNpcInteractionAnimation(maleTrainerAnimation, femaleTrainerAnimation, animationBlendDuration);
        SetPrompt(false);
    }

    private void PlayClip(AnimationClip clip)
    {
        bool hasPose = poseBlend.IsInitialized;
        poseBlend.Initialize(animator);
        poseBlend.Begin(hasPose ? animationBlendDuration : 0f);
        if (graph.IsValid()) graph.Destroy();
        currentClip = clip;
        animationTime = 0f;
        if (animator == null || clip == null) return;
        graph = PlayableGraph.Create($"NPC {name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(graph, "NPC Animation", animator);
        output.SetSourcePlayable(playable);
        graph.Play();
        graph.Evaluate(0f);
        poseBlend.Apply(0f);
        poseBlend.RememberPose();
    }

    private void UpdateAnimation(float deltaTime)
    {
        if (!graph.IsValid() || currentClip == null)
        {
            poseBlend.Apply(deltaTime);
            poseBlend.RememberPose();
            return;
        }
        animationTime += deltaTime;
        if (IsInteracting && animationTime >= currentClip.length)
        {
            IsInteracting = false;
            PlayClip(idleAnimation);
            return;
        }
        float sampleTime = currentClip.length > 0f ? animationTime % currentClip.length : 0f;
        playable.SetTime(sampleTime);
        graph.Evaluate(0f);
        poseBlend.Apply(deltaTime);
        poseBlend.RememberPose();
    }

    private void SetPrompt(bool visible)
    {
        if (prompt != null) prompt.gameObject.SetActive(visible);
        if (promptText != null) promptText.text = $"{interactionLabel}  [E / A]";
    }

    private void StopAnimation()
    {
        if (graph.IsValid()) graph.Destroy();
        poseBlend.Clear();
        IsInteracting = false;
        currentClip = null;
        animator = null;
    }

    private void OnDisable()
    {
        ActiveNpcs.Remove(this);
        selectionFrame = -1;
        if (nearest == this) nearest = null;
        if (interactButton != null) interactButton.onClick.RemoveListener(Interact);
        SetPrompt(false);
        StopAnimation();
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
    }
}
