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

        ResetButtonVisuals();
    }

    private string GetBindingForControlScheme(string controlScheme)
    {
        var action = InputManager.Instance.GetRuntimeAction(rebindingAction.action);
        if (action == null) return null;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.groups.Contains(controlScheme))
            {
                return InputControlPath.ToHumanReadableString(binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        return null;
    }

    private void Rebind(InputAction action, string controlScheme)
    {
        StartRebindVisuals(controlScheme); // Update visuals

        InputManager.Instance.StartRebinding(
            action,
            controlScheme,
            onComplete: a =>
            {
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
