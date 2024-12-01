using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RebindUI : MonoBehaviour
{
    [SerializeField] private TMP_Text keybindLabel;

    [SerializeField] private Button controllerRebindButton;
    [SerializeField] private Button keyboardRebindButton;

    [SerializeField] private InputActionReference rebindingAction;
    [SerializeField] private TMP_Text controllerRebindText;
    [SerializeField] private TMP_Text keyboardRebindText;

    [SerializeField] private Image controllerRebindImage;
    [SerializeField] private Image keyboardRebindImage;

    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color rebindingColor = Color.yellow;

    private void Start()
    {
        // Set up button listeners
        controllerRebindButton.onClick.AddListener(() => Rebind(rebindingAction.action, "Gamepad"));
        keyboardRebindButton.onClick.AddListener(() => Rebind(rebindingAction.action, "MouseAndKeyboard"));

        keybindLabel.text = rebindingAction.action.name; // Update keybind label

        // Update button text with current bindings
        UpdateRebindTexts(true);
    }

    private void UpdateRebindTexts(bool firstTime=false)
    {
        if (firstTime)
        {
            // Update keyboard binding text
            string keyboardBinding = GetBindingForControlSchemeFromManager("MouseAndKeyboard");
            keyboardRebindText.text = string.IsNullOrEmpty(keyboardBinding) ? "Unbound" : keyboardBinding;
            // Update controller binding text
            string controllerBinding = GetBindingForControlSchemeFromManager("Gamepad");
            controllerRebindText.text = string.IsNullOrEmpty(controllerBinding) ? "Unbound" : controllerBinding;
        }
        else
        {
            // Update keyboard binding text
            string keyboardBinding = GetBindingForControlScheme("MouseAndKeyboard");
            keyboardRebindText.text = string.IsNullOrEmpty(keyboardBinding) ? "Unbound" : keyboardBinding;
            // Update controller binding text
            string controllerBinding = GetBindingForControlScheme("Gamepad");
            controllerRebindText.text = string.IsNullOrEmpty(controllerBinding) ? "Unbound" : controllerBinding;

            // Reset button visuals
            ResetButtonVisuals();
        }
    }

    private string GetBindingForControlScheme(string controlScheme)
    {
        // Iterate through bindings to find the one matching the control scheme
        for (int i = 0; i < rebindingAction.action.bindings.Count; i++)
        {
            var binding = rebindingAction.action.bindings[i];
            if (binding.groups.Contains(controlScheme)) // Match control scheme
            {
                return InputControlPath.ToHumanReadableString(binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        return null; // No binding found for this control scheme
    }

    private string GetBindingForControlSchemeFromManager(string controlScheme)
    {
        // Iterate through bindings to find the one matching the control scheme
        foreach (var actionMap in InputManager.Instance.Controls.asset.actionMaps)
        {
            foreach (var action in actionMap.actions)
            {
                if (action.name == rebindingAction.action.name)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i];
                        if (binding.groups.Contains(controlScheme))
                        {
                            return InputControlPath.ToHumanReadableString(binding.effectivePath,
                                InputControlPath.HumanReadableStringOptions.OmitDevice);
                        }
                    }
                }
            }
        }

        return null; // No binding found for this control scheme
    }

    private void Rebind(InputAction action, string controlScheme)
    {
        StartRebindVisuals(controlScheme); // Update visuals

        InputManager.Instance.StartRebinding(
            action,
            controlScheme,
            onComplete: a =>
            {
                UpdateRebindTexts(); // Update text after rebinding
                ResetButtonVisuals(); // Reset visuals after rebinding
            },
            onCancel: a => ResetButtonVisuals() // Reset visuals if rebinding is canceled
        );
    }

    private void StartRebindVisuals(string controlScheme)
    {
        if (controlScheme == "MouseAndKeyboard")
        {
            keyboardRebindImage.color = rebindingColor;
            keyboardRebindText.text = "Rebinding...";
        }
        else if (controlScheme == "Gamepad")
        {
            controllerRebindImage.color = rebindingColor;
            controllerRebindText.text = "Rebinding...";
        }
    }

    private void ResetButtonVisuals()
    {
        // Reset visuals for keyboard
        keyboardRebindImage.color = defaultColor;
        keyboardRebindText.text = GetBindingForControlScheme("MouseAndKeyboard") ?? "Unbound";

        // Reset visuals for controller
        controllerRebindImage.color = defaultColor;
        controllerRebindText.text = GetBindingForControlScheme("Gamepad") ?? "Unbound";
    }
}
