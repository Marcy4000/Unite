using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    private PlayerControls playerControls;

    public PlayerControls Controls => playerControls;

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
        }

        // Initialize the player controls
        playerControls = new PlayerControls();
        playerControls.Enable();  // Enable all action maps
    }

    private void Start()
    {
        LoadAllBindings();
    }

    private void OnDestroy()
    {
        playerControls.Disable();  // Disable all action maps
    }

    // Start the rebinding process for a specific InputAction
    public void StartRebinding(InputAction action)
    {
        if (action != null)
        {
            // Disable the action while rebinding
            action.Disable();

            // Start the interactive rebinding process
            action.PerformInteractiveRebinding()
                .OnComplete(callback => OnRebindComplete(action))
                .OnCancel(callback => OnRebindCancelled(action))
                .Start();
        }
        else
        {
            Debug.LogError("InputAction cannot be null.");
        }
    }

    // Called when the rebind completes
    private void OnRebindComplete(InputAction action)
    {
        // Get the new binding (e.g., the key pressed)
        string newBinding = action.bindings[0].effectivePath;
        Debug.Log($"Rebind Complete! New binding for {action.name}: {newBinding}");

        // Optionally, you can save the new binding here
        SaveBinding(action, newBinding);
    }

    // Called when the rebind is cancelled
    private void OnRebindCancelled(InputAction action)
    {
        Debug.Log($"Rebind cancelled for {action.name}");
    }

    // Save the new binding to PlayerPrefs (optional)
    private void SaveBinding(InputAction action, string binding)
    {
        PlayerPrefs.SetString(action.name, binding);
        PlayerPrefs.Save();
    }

    // Load the saved binding for an action (optional)
    public void LoadBinding(InputAction action)
    {
        string savedBinding = PlayerPrefs.GetString(action.name);
        if (!string.IsNullOrEmpty(savedBinding))
        {
            action.ApplyBindingOverride(savedBinding);
        }
    }

    // To load bindings when the game starts
    public void LoadAllBindings()
    {
        foreach (var actionMap in playerControls.asset.actionMaps)
        {
            foreach (var action in actionMap.actions)
            {
                LoadBinding(action);
            }
        }
    }
}
