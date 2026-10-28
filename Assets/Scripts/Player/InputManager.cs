using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    private PlayerControls playerControls;

    public PlayerControls Controls => playerControls;
    public bool IsControllerMode { get; private set; }
    private float lastMouseActivity;
    private bool hasFocus = true;
    private const float CursorIdleSeconds = 2f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(this);
            return;
        }

        // Initialize and enable PlayerControls
        playerControls = new PlayerControls();
        playerControls.Enable();

        LoadAllBindings();
    }

    private void OnDestroy()
    {
        playerControls?.Disable();
        playerControls?.Dispose();
        if (Instance == this)
        {
            Instance = null;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var mouseActive = mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0.01f ||
            mouse.scroll.ReadValue().sqrMagnitude > 0f || mouse.leftButton.wasPressedThisFrame ||
            mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame ||
            mouse.forwardButton.wasPressedThisFrame || mouse.backButton.wasPressedThisFrame);
        var keyboardActive = Keyboard.current?.anyKey.wasPressedThisFrame == true;
        var controllerActive = false;
        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.09f ||
                gamepad.rightStick.ReadValue().sqrMagnitude > 0.09f)
                controllerActive = true;
            foreach (var control in gamepad.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame) controllerActive = true;
        }
        UpdateInputMode(mouseActive, keyboardActive, controllerActive, Time.unscaledTime);
        if (Gamepad.all.Count == 0) IsControllerMode = false;
        var game = GameManager.Instance;
        var inGame = game != null && (game.GameState == GameState.Playing || game.GameState == GameState.Starting);
        var menuOpen = (SettingsManager.Instance != null && SettingsManager.Instance.IsMenuOpen) ||
            (LobbyController.Instance != null && (LobbyController.Instance.IsHandlingConnectionFailure ||
                LobbyController.Instance.LastConnectionFailure != Unite.Multiplayer.SessionFailureReason.None ||
                !string.IsNullOrEmpty(LobbyController.Instance.LastOperationError)));
        Cursor.visible = ShouldShowCursor(inGame, menuOpen, hasFocus, Time.unscaledTime);
    }

    private void UpdateInputMode(bool mouseActive, bool keyboardActive, bool controllerActive, float now)
    {
        if (mouseActive) lastMouseActivity = now;
        if (mouseActive || keyboardActive) IsControllerMode = false;
        else if (controllerActive) IsControllerMode = true;
    }

    private bool ShouldShowCursor(bool inGame, bool menuOpen, bool focused, float now)
    {
        return !inGame || menuOpen || !focused || !IsControllerMode || now - lastMouseActivity < CursorIdleSeconds;
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        lastMouseActivity = Time.unscaledTime;
        if (!focused) Cursor.visible = true;
    }

    private void OnDisable() => Cursor.visible = true;

    public void StartRebinding(
        InputAction action,
        string controlScheme = null,
        int bindingIndex = -1,
        System.Action<InputAction> onComplete = null,
        System.Action<InputAction> onCancel = null)
    {
        if (action == null)
        {
            Debug.LogError("InputAction cannot be null.");
            return;
        }

        // Find the binding index for the control scheme if not provided
        if (bindingIndex == -1 && !string.IsNullOrEmpty(controlScheme))
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].groups.Contains(controlScheme))
                {
                    bindingIndex = i;
                    break;
                }
            }

            if (bindingIndex == -1)
            {
                Debug.LogError($"No binding found for control scheme '{controlScheme}' in action '{action.name}'.");
                return;
            }
        }

        // Disable the action while rebinding
        action.Disable();

        // Start the interactive rebinding process
        var rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape") // Allow canceling
            .OnComplete(operation =>
            {
                OnRebindComplete(action, bindingIndex);
                onComplete?.Invoke(action);
                action.Enable();
                operation.Dispose();
            })
            .OnCancel(operation =>
            {
                OnRebindCancelled(action);
                onCancel?.Invoke(action);
                action.Enable();
                operation.Dispose();
            });

        rebindingOperation.Start();
    }

    private void OnRebindComplete(InputAction action, int bindingIndex)
    {
        // Save the new binding
        SaveBinding(action, bindingIndex, action.bindings[bindingIndex].effectivePath);

        Debug.Log($"Rebind Complete! New binding for {action.name}: {action.bindings[bindingIndex].effectivePath}");
    }

    private void OnRebindCancelled(InputAction action)
    {
        Debug.Log($"Rebind cancelled for {action.name}");
    }

    private void SaveBinding(InputAction action, int bindingIndex, string bindingPath)
    {
        string controlScheme = action.bindings[bindingIndex].groups; // e.g., "MouseAndKeyboard"
        string key = $"{action.actionMap.name}.{action.name}.binding.{bindingIndex}.{controlScheme}";
        PlayerPrefs.SetString(key, bindingPath);
        PlayerPrefs.Save();
    }

    private void LoadBinding(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            string controlScheme = action.bindings[i].groups; // e.g., "MouseAndKeyboard"
            string key = $"{action.actionMap.name}.{action.name}.binding.{i}.{controlScheme}";
            string savedBinding = PlayerPrefs.GetString(key);

            if (!string.IsNullOrEmpty(savedBinding))
            {
                action.ApplyBindingOverride(i, savedBinding);
                Debug.Log($"Loaded binding for {action.name} ({controlScheme}): {savedBinding}");
            }
        }
    }

    public void LoadAllBindings()
    {
        foreach (var actionMap in playerControls.asset.actionMaps)
        {
            foreach (var action in actionMap.actions)
            {
                LoadBinding(action);
            }
        }

        // Force the controls to refresh their bindings
        RefreshControls();
    }

    private void RefreshControls()
    {
        // This ensures the controls system is updated with any overrides
        playerControls.Disable();
        playerControls.Enable();
    }
}
