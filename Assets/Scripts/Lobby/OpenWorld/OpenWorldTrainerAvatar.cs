using System;
using System.Collections.Generic;
using Unite.Multiplayer;
using TMPro;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject), typeof(CharacterController), typeof(ClientNetworkTransform))]
public sealed partial class OpenWorldTrainerAvatar : NetworkBehaviour
{
    [SerializeField] private GameObject trainerModelPrefab;
    [SerializeField] private TMP_Text nameText;
    [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;
    [SerializeField] private float gravity = -25f;
    [SerializeField, Min(0.1f)] private float terminalSpeed = 45f;
    [SerializeField] private Vector3 visualScale = Vector3.one;
    [SerializeField] private GameObject[] movementBlockingPanels;
    [SerializeField, Min(0.1f)] private float cameraDistance = 5f;
    [SerializeField, Min(0f)] private float cameraHeight = 1.4f;
    [SerializeField, Range(10f, 70f)] private float cameraPitch = 25f;
    [SerializeField, Min(0f)] private float cameraSharpness = 15f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;

    private readonly NetworkVariable<FixedString128Bytes> playerId = new NetworkVariable<FixedString128Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString128Bytes> playerName = new NetworkVariable<FixedString128Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString512Bytes> clothing = new NetworkVariable<FixedString512Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private static readonly int StartRun = Animator.StringToHash("startRun");
    private static readonly int StopRun = Animator.StringToHash("stopRun");
    private static readonly int UseAltIdle = Animator.StringToHash("useAltIdle");
    private static readonly int ResetAnim = Animator.StringToHash("ResetAnim");
    private const float RemoteRunStopDelay = 0.3f;
    private static Vector3 lastLocalPosition;
    private static string lastLocalPlayerId;
    private static string lastLocalPoseVersion;
    private static float lastLocalYaw;
    private static bool hasLocalPosition;

    private readonly List<TMP_InputField> chatInputs = new List<TMP_InputField>();
    private CharacterController characterController;
    private ClientNetworkTransform networkTransform;
    private OpenWorldTrainerEmotes emotes;
    private OpenWorldTrainerInteractionAnimation interactionAnimation;
    private TrainerModel trainerModel;
    private Animator movementAnimator;
    private Camera localCamera;
    private CinemachineBrain cameraBrain;
    private CinemachineCamera followCamera;
    private Transform cameraTarget;
    private Func<bool> inputBlocked;
    private Vector3 previousPosition;
    private float verticalSpeed;
    private float cameraYaw;
    private float nextChatScan;
    private float remoteRunTimeRemaining;
    private bool isRunning;
    private bool explicitInputBlocked;
    private bool hasFocus = true;
    private bool isPaused;
    private bool cameraNeedsSnap;
    private Vector3 safeSpawnPosition;
    private Camera previousMainCamera;
    private bool previousCameraEnabled;

    public static OpenWorldTrainerAvatar LocalAvatar { get; private set; }
    public static event Action<OpenWorldTrainerAvatar> LocalAvatarChanged;
    public static event Action<string, Vector3, float> LocalPoseSaved;
    public event Action<OpenWorldTrainerAvatar> IdentityChanged;
    public string PlayerId => playerId.Value.ToString();
    public string PlayerName => playerName.Value.ToString();
    public string Clothing => clothing.Value.ToString();
    public Vector3 Position => transform.position;
    public float Yaw => transform.eulerAngles.y;
    public Camera LocalCamera => localCamera;
    public CinemachineCamera FollowCamera => followCamera;
    public Animator AnimationAnimator => trainerModel != null && trainerModel.IsInitialized ? trainerModel.ActiveAnimator : null;
    public bool CanStartEmote => !IsSeated && !IsInputBlocked() && (interactionAnimation == null || !interactionAnimation.IsPlaying);

    public bool PlayNpcInteractionAnimation(AnimationClip maleAnimation, AnimationClip femaleAnimation, float blendDuration = 0.2f)
    {
        if (!IsSpawned || !IsOwner || !IsClient || LocalAvatar != this || IsSeated || AnimationAnimator == null) return false;
        var clip = trainerModel.IsMale ? maleAnimation : femaleAnimation;
        if (clip == null) return false;
        if (emotes != null) emotes.CancelForMovement();
        if (interactionAnimation == null)
            interactionAnimation = GetComponent<OpenWorldTrainerInteractionAnimation>() ?? gameObject.AddComponent<OpenWorldTrainerInteractionAnimation>();
        return interactionAnimation.Play(AnimationAnimator, clip, blendDuration);
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        networkTransform = GetComponent<ClientNetworkTransform>();
        emotes = GetComponent<OpenWorldTrainerEmotes>();
        characterController.enabled = false;
    }

    public void Configure(GameObject visualPrefab, Func<bool> movementBlocked = null)
    {
        if (IsSpawned)
            throw new InvalidOperationException("Configure the trainer avatar before spawning it.");
        if (visualPrefab == null || visualPrefab.GetComponent<TrainerModel>() == null)
            throw new ArgumentException("The visual prefab must have a TrainerModel on its root.", nameof(visualPrefab));
        trainerModelPrefab = visualPrefab;
        inputBlocked = movementBlocked;
    }

    public void SetInputBlocker(Func<bool> movementBlocked) => inputBlocked = movementBlocked;
    public void SetInputBlocked(bool blocked) => explicitInputBlocked = blocked;

    public void Initialize(string playerId, string name, string clothes)
    {
        if (NetworkManager == null || !NetworkManager.IsServer)
            throw new InvalidOperationException("Only the server can initialize trainer identity.");
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("An authenticated player ID is required.", nameof(playerId));

        if (string.IsNullOrEmpty(clothes))
            clothes = new PlayerClothesInfo
            {
                IsMale = true,
                HairColor = new Color32(80, 60, 45, 255),
                EyeColor = new Color32(100, 80, 60, 255)
            }.Serialize();
        PlayerClothesInfo.Deserialize(clothes);
        var nextId = new FixedString128Bytes(playerId);
        var nextName = new FixedString128Bytes(string.IsNullOrWhiteSpace(name) ? "Trainer" : name);
        var nextClothes = new FixedString512Bytes(clothes);
        if (IsSpawned)
        {
            this.playerId.Value = nextId;
            playerName.Value = nextName;
            clothing.Value = nextClothes;
        }
        else
        {
            this.playerId.Reset(nextId);
            playerName.Reset(nextName);
            clothing.Reset(nextClothes);
        }
    }

    public void SetSpawnPosition(Vector3 position) => SetSpawnPosition(position, transform.eulerAngles.y);

    public void SetSpawnPosition(Vector3 position, float yaw) => SetPose(position, yaw, true);

    private void SetPose(Vector3 position, float yaw, bool alignCamera)
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z) || !float.IsFinite(yaw))
            throw new ArgumentException("Spawn pose must be finite.", nameof(position));
        if (IsSpawned && !IsOwner)
            throw new InvalidOperationException("Set the position before spawn, or on the owning client.");

        bool restoreController = characterController != null && characterController.enabled;
        if (restoreController) characterController.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        if (IsSpawned) networkTransform.Teleport(position, transform.rotation, transform.localScale);
        if (restoreController) characterController.enabled = true;
        verticalSpeed = 0f;
        previousPosition = position;
        remoteRunTimeRemaining = 0f;
        if (alignCamera)
        {
            cameraYaw = yaw;
            cameraNeedsSnap = true;
        }
        if (IsSpawned && IsOwner) RememberLocalPose();
    }

    public static bool TryGetLocalPosition(out Vector3 position)
    {
        InvalidateOutdatedLocalPose();
        position = LocalAvatar != null ? LocalAvatar.transform.position : lastLocalPosition;
        return LocalAvatar != null || hasLocalPosition;
    }

    public static bool TryGetLocalPose(string playerId, out Vector3 position, out float yaw)
    {
        InvalidateOutdatedLocalPose();
        position = lastLocalPosition;
        yaw = lastLocalYaw;
        return hasLocalPosition && string.Equals(lastLocalPlayerId, playerId, StringComparison.Ordinal);
    }

    private static void InvalidateOutdatedLocalPose()
    {
        if (lastLocalPoseVersion == OpenWorldLobbyRules.Version) return;
        hasLocalPosition = false;
        lastLocalPlayerId = null;
        lastLocalPosition = default;
        lastLocalYaw = 0f;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        playerId.OnValueChanged += OnIdentityChanged;
        playerName.OnValueChanged += OnIdentityChanged;
        clothing.OnValueChanged += OnClothingChanged;
        previousPosition = transform.position;
        remoteRunTimeRemaining = 0f;
        nextIdleVariationAt = 0d;
        appliedIdleVariation = 0;
        safeSpawnPosition = transform.position;
        CreateVisual();
        if (trainerModel != null)
        {
            trainerModel.onClothesInitialized -= RefreshFakeShadow;
            trainerModel.onClothesInitialized += RefreshFakeShadow;
            RefreshFakeShadow();
        }
        RefreshNameplate();
        ApplyClothing();
        RefreshOwnership();
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        if (IsSpawned) RefreshOwnership();
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        ReleaseLocalControl();
    }

    public override void OnNetworkDespawn()
    {
        GetComponent<OpenWorldTrainerChatBubble>()?.Hide();
        ClearSeat();
        ReleaseLocalControl();
        playerId.OnValueChanged -= OnIdentityChanged;
        playerName.OnValueChanged -= OnIdentityChanged;
        clothing.OnValueChanged -= OnClothingChanged;
        if (trainerModel != null) trainerModel.onClothesInitialized -= RefreshFakeShadow;
        base.OnNetworkDespawn();
    }

    private void OnEnable()
    {
        if (IsSpawned) RefreshOwnership();
    }

    private void OnDisable()
    {
        ClearSeat();
        ReleaseLocalControl();
    }
    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        if (!focused) ResetTouchLookInput();
    }

    private void OnApplicationPause(bool paused)
    {
        isPaused = paused;
        if (paused) ResetTouchLookInput();
    }

    private void CreateVisual()
    {
        if (trainerModel != null || (NetworkManager != null && !NetworkManager.IsClient)) return;
        if (trainerModelPrefab == null)
        {
            Debug.LogError("OpenWorldTrainerAvatar requires a TrainerModel prefab.", this);
            return;
        }

        var visual = Instantiate(trainerModelPrefab, transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = visualScale;
        trainerModel = visual.GetComponent<TrainerModel>();
        foreach (var text in visual.GetComponentsInChildren<TMP_Text>(true)) Destroy(text.gameObject);
        foreach (var canvas in visual.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
    }

    private void RefreshFakeShadow()
    {
        if (trainerModel == null || !trainerModel.IsInitialized) return;
        if (!TryGetComponent<FakeMeshShadow>(out var fakeShadow))
            fakeShadow = gameObject.AddComponent<FakeMeshShadow>();
        fakeShadow.SetSource(trainerModel.transform, forceRebuild: true);
    }

    private void OnIdentityChanged(FixedString128Bytes previous, FixedString128Bytes current)
    {
        RefreshNameplate();
        IdentityChanged?.Invoke(this);
    }

    private void RefreshNameplate()
    {
        if (nameText == null) return;
        nameText.gameObject.SetActive(IsClient);
        nameText.text = PlayerName;
    }

    private void OnClothingChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        ApplyClothing();
        IdentityChanged?.Invoke(this);
    }

    private void ApplyClothing()
    {
        if (trainerModel == null || clothing.Value.IsEmpty) return;
        if (interactionAnimation != null) interactionAnimation.StopPlayback();
        try
        {
            trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(Clothing));
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"Invalid roaming trainer clothing: {exception.Message}", this);
        }
    }

    private void RefreshOwnership()
    {
        characterController.enabled = IsOwner && IsClient;
        if (!IsOwner || !IsClient || !isActiveAndEnabled) return;
        if (LocalAvatar != null && LocalAvatar != this) LocalAvatar.ReleaseLocalControl();
        bool changed = LocalAvatar != this;
        LocalAvatar = this;
        InitializeTouchLookInput();
        verticalSpeed = 0f;
        nextChatScan = 0f;
        if (localCamera == null)
        {
            var cameraObject = new GameObject("Open World Trainer Camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, gameObject.scene);
            localCamera = cameraObject.AddComponent<Camera>();
            var sceneCamera = Camera.main;
            previousMainCamera = sceneCamera;
            previousCameraEnabled = sceneCamera != null && sceneCamera.enabled;
            if (sceneCamera != null) sceneCamera.enabled = false;
            cameraObject.tag = "MainCamera";
            localCamera.depth = sceneCamera != null ? Mathf.Max(100f, sceneCamera.depth + 1f) : 100f;
            localCamera.nearClipPlane = 0.1f;
            localCamera.farClipPlane = 1000f;
            localCamera.fieldOfView = 55f;
            cameraBrain = cameraObject.AddComponent<CinemachineBrain>();
            cameraBrain.ChannelMask = OutputChannels.Channel15;
            cameraBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            cameraBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            cameraTarget = new GameObject("Open World Camera Target").transform;
            cameraTarget.SetParent(transform, false);
            cameraTarget.localPosition = Vector3.up * cameraHeight;
            var followObject = new GameObject("Open World Follow Camera");
            followObject.transform.SetParent(cameraObject.transform, false);
            followCamera = followObject.AddComponent<CinemachineCamera>();
            followCamera.OutputChannel = OutputChannels.Channel15;
            followCamera.Priority = -10000;
            followCamera.Follow = cameraTarget;
            followCamera.Lens.FieldOfView = 55f;
            followCamera.Lens.NearClipPlane = 0.1f;
            followCamera.Lens.FarClipPlane = 1000f;
            var follow = followObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.CameraDistance = cameraDistance;
            follow.ShoulderOffset = Vector3.zero;
            follow.VerticalArmLength = 0f;
            follow.Damping = Vector3.one * (cameraSharpness > 0f ? 1f / cameraSharpness : 0f);
            follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
            {
                Enabled = true,
                CollisionFilter = cameraCollisionMask,
                IgnoreTag = CompareTag("Player") ? "Player" : string.Empty,
                CameraRadius = 0.2f,
                DampingIntoCollision = 0f,
                DampingFromCollision = 0.3f
            };
            cameraYaw = transform.eulerAngles.y;
            cameraNeedsSnap = true;
        }
        if (changed) LocalAvatarChanged?.Invoke(this);
    }

    private void ReleaseLocalControl()
    {
        ReleaseTouchLookInput();
        if (interactionAnimation != null) interactionAnimation.StopPlayback();
        if (characterController != null) characterController.enabled = false;
        if (LocalAvatar == this)
        {
            RememberLocalPose();
            LocalAvatar = null;
            LocalAvatarChanged?.Invoke(null);
        }
        if (localCamera != null)
        {
            if (followCamera != null) followCamera.gameObject.SetActive(false);
            localCamera.enabled = false;
            Destroy(localCamera.gameObject);
            localCamera = null;
        }
        if (cameraTarget != null) Destroy(cameraTarget.gameObject);
        if (previousMainCamera != null) previousMainCamera.enabled = previousCameraEnabled;
        previousMainCamera = null;
        cameraTarget = null;
        cameraBrain = null;
        followCamera = null;
        verticalSpeed = 0f;
    }

    private void RememberLocalPose()
    {
        string authId = PlayerId;
        if (string.IsNullOrEmpty(authId)) return;
        lastLocalPosition = transform.position;
        lastLocalPlayerId = authId;
        lastLocalYaw = transform.eulerAngles.y;
        lastLocalPoseVersion = OpenWorldLobbyRules.Version;
        hasLocalPosition = true;
        LocalPoseSaved?.Invoke(authId, lastLocalPosition, lastLocalYaw);
    }

    private bool IsInputBlocked()
    {
        if (DialogueSystem.Instance != null && DialogueSystem.Instance.BlocksWorldInput) return true;
        if (!hasFocus || isPaused || explicitInputBlocked || OpenWorldStageMenu.IsMenuOpen || OpenWorldTrainerMenu.IsMenuOpen || OpenWorldEmoteMenu.IsMenuOpen || OpenWorldPokemonMenu.IsMenuOpen || (inputBlocked != null && inputBlocked()) ||
            (SettingsManager.Instance != null && SettingsManager.Instance.IsMenuOpen)) return true;
        if (movementBlockingPanels != null)
            foreach (var panel in movementBlockingPanels)
                if (panel != null && panel.activeInHierarchy) return true;

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.activeInHierarchy &&
            (selected.GetComponentInParent<TMP_InputField>() != null || selected.GetComponentInParent<InputField>() != null))
            return true;

        if (Time.unscaledTime >= nextChatScan)
        {
            nextChatScan = Time.unscaledTime + 1f;
            chatInputs.Clear();
            foreach (var chat in FindObjectsByType<ChatManager>(FindObjectsSortMode.None))
                chatInputs.AddRange(chat.GetComponentsInChildren<TMP_InputField>(true));
        }
        foreach (var field in chatInputs)
            if (field != null && field.isActiveAndEnabled) return true;
        return false;
    }

    internal static Vector3 CalculatePlanarMovement(Vector2 input, float yaw)
    {
        input = Vector2.ClampMagnitude(input, 1f);
        return Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || !IsClient || LocalAvatar != this || !characterController.enabled) return;
        var lobby = LobbyController.Instance;
        if (lobby == null || lobby.OpenWorldLobby == null || !lobby.OpenWorldLobby.IsConnected)
        {
            ResetTouchLookInput();
            verticalSpeed = 0f;
            return;
        }
        if (transform.position.y < -20f) SetSpawnPosition(safeSpawnPosition);
        bool blocked = IsInputBlocked();
        if (!blocked)
            UpdateLookInput();
        else
            ResetTouchLookInput();

        Vector2 input = Vector2.zero;
        var manager = InputManager.Instance;
        if (!blocked && manager != null && manager.Controls != null)
            input = manager.Controls.Movement.Move.ReadValue<Vector2>();
        if (!IsSeated) Move(input);
    }

    private void UpdateLookInput()
    {
        UpdateTouchLookInput();
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            var look = mouse.delta.ReadValue();
            cameraYaw += look.x * 0.15f;
            cameraPitch = Mathf.Clamp(cameraPitch - look.y * 0.15f, 10f, 70f);
        }
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            var look = gamepad.rightStick.ReadValue();
            cameraYaw += look.x * 120f * Time.deltaTime;
            cameraPitch = Mathf.Clamp(cameraPitch - look.y * 90f * Time.deltaTime, 10f, 70f);
        }
    }

    private void Move(Vector2 input)
    {
        if (IsSeated) return;
        var movement = CalculatePlanarMovement(input, cameraYaw);
        if (movement.sqrMagnitude > 0.0001f)
        {
            if (interactionAnimation != null) interactionAnimation.CancelForMovement();
            if (emotes != null) emotes.CancelForMovement();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(movement), turnSpeed * Time.deltaTime);
        }
        if (characterController.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed = Mathf.Max(verticalSpeed + Mathf.Min(gravity, -0.1f) * Time.deltaTime, -terminalSpeed);
        var collision = characterController.Move((movement * moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
        if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        UpdateSeatAnimation();
        float deltaTime = Time.deltaTime;
        var displacement = transform.position - previousPosition;
        displacement.y = 0f;
        previousPosition = transform.position;
        bool moving = deltaTime > 0f && displacement.sqrMagnitude > 0.01f * deltaTime * deltaTime;
        // Network interpolation can briefly stop between position updates. Keep the
        // remote run cycle alive through those gaps, without delaying local stops.
        if (IsOwner)
            remoteRunTimeRemaining = 0f;
        else if (moving)
            remoteRunTimeRemaining = RemoteRunStopDelay;
        else
            remoteRunTimeRemaining = Mathf.Max(0f, remoteRunTimeRemaining - deltaTime);
        UpdateAnimation(moving || remoteRunTimeRemaining > 0f);
        UpdateIdleVariation();
        if (IsOwner && LocalAvatar == this)
        {
            RememberLocalPose();
            if (localCamera != null) UpdateCamera();
        }
    }

    private void UpdateAnimation(bool shouldRun)
    {
        if (IsSeated || (emotes != null && emotes.IsPlaying) ||
            (interactionAnimation != null && interactionAnimation.IsPlaying && !interactionAnimation.IsBlendingOut))
        {
            movementAnimator = null;
            isRunning = false;
            remoteRunTimeRemaining = 0f;
            return;
        }
        var animator = trainerModel != null && trainerModel.IsInitialized ? trainerModel.ActiveAnimator : null;
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (movementAnimator != animator)
        {
            movementAnimator = animator;
            animator.ResetTrigger(StartRun);
            animator.ResetTrigger(StopRun);
            isRunning = false;
        }
        animator.SetBool(UseAltIdle, false);
        if (isRunning == shouldRun) return;
        if (shouldRun) animator.ResetTrigger(ResetAnim);
        animator.ResetTrigger(shouldRun ? StopRun : StartRun);
        animator.SetTrigger(shouldRun ? StartRun : StopRun);
        isRunning = shouldRun;
    }

    private void UpdateCamera()
    {
        cameraTarget.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        if (cameraNeedsSnap) followCamera.PreviousStateIsValid = false;
        cameraBrain.ManualUpdate();
        cameraNeedsSnap = false;
    }
}
