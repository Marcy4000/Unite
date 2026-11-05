using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public class InputRebindingTests : InputTestFixture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Type managerType;
    private GameObject root;
    private Component manager;
    private object previousManager;
    private IDisposable controls;
    private InputActionAsset runtimeAsset;
    private InputAction sourceAction;
    private InputAction runtimeAction;
    private Keyboard keyboard;
    private Gamepad gamepad;
    private string key;
    private string previousBinding;
    private bool hadBinding;

    public override void Setup()
    {
        base.Setup();
        managerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("InputManager")).First(t => t != null);
        previousManager = managerType.GetField("Instance").GetValue(null);
        root = new GameObject("Input rebinding tests");
        root.SetActive(false);
        manager = root.AddComponent(managerType);
        var controlsType = managerType.GetProperty("Controls").PropertyType;
        controls = (IDisposable)Activator.CreateInstance(controlsType);
        runtimeAsset = (InputActionAsset)controlsType.GetProperty("asset").GetValue(controls);
        managerType.GetField("playerControls", Private).SetValue(manager, controls);
        managerType.GetField("Instance").SetValue(null, manager);
        sourceAction = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerControls.inputactions")
            .FindAction("Movement/MoveA", true);
        runtimeAction = runtimeAsset.FindAction(sourceAction.id.ToString(), true);
        runtimeAsset.Enable();
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
    }

    public override void TearDown()
    {
        // Also cancel a pending operation if an assertion failed mid-rebind.
        QueueButton(keyboard.escapeKey, 1f);
        UpdateInput();
        InputSystem.RemoveDevice(keyboard);
        InputSystem.RemoveDevice(gamepad);
        runtimeAsset.Disable();
        Object.DestroyImmediate(runtimeAsset);
        managerType.GetField("playerControls", Private).SetValue(manager, null);
        Object.DestroyImmediate(root);
        managerType.GetField("Instance").SetValue(null, previousManager);
        if (key != null)
        {
            if (hadBinding) PlayerPrefs.SetString(key, previousBinding);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        key = null;
        base.TearDown();
    }

    private static void UpdateInput() => InputSystem.Update();

    private static void QueueButton(ButtonControl control, float value)
    {
        using (StateEvent.From(control.device, out var inputEvent))
        {
            control.WriteValueIntoEvent(value, inputEvent);
            InputSystem.QueueEvent(inputEvent);
        }
    }

    private void PreservePreference(string scheme)
    {
        int index = runtimeAction.GetBindingIndex(InputBinding.MaskByGroup(scheme));
        key = $"{runtimeAction.actionMap.name}.{runtimeAction.name}.binding.{index}.{runtimeAction.bindings[index].groups}";
        hadBinding = PlayerPrefs.HasKey(key);
        previousBinding = PlayerPrefs.GetString(key);
    }

    private void StartRebind(string scheme, Action<InputAction> complete = null, Action<InputAction> cancel = null)
    {
        managerType.GetMethod("StartRebinding").Invoke(manager,
            new object[] { sourceAction, scheme, -1, complete, cancel });
    }

    [Test]
    public void ControllerRebindChangesGameplayImmediatelyAndPersists()
    {
        VerifyRebind("Gamepad", gamepad.leftShoulder, gamepad.rightShoulder);
    }

    [Test]
    public void KeyboardRebindChangesGameplayImmediatelyAndPersists()
    {
        sourceAction = sourceAction.actionMap.FindAction("UniteMove", true);
        runtimeAction = runtimeAsset.FindAction(sourceAction.id.ToString(), true);
        string oldPath = runtimeAction.bindings[
            runtimeAction.GetBindingIndex(InputBinding.MaskByGroup("MouseAndKeyboard"))].effectivePath;
        var oldControl = (ButtonControl)keyboard[oldPath.Substring(oldPath.IndexOf('/') + 1)];
        VerifyRebind("MouseAndKeyboard", keyboard.pKey, oldControl);
    }

    private void VerifyRebind(string scheme, ButtonControl newControl, ButtonControl oldControl)
    {
        PreservePreference(scheme);
        string originalSource = sourceAction.SaveBindingOverridesAsJson();
        InputAction completed = null;
        StartRebind(scheme, a => completed = a);
        Assert.That(runtimeAction.enabled, Is.False, "Rebinding must disable the gameplay action.");
        QueueButton(newControl, 1f);
        UpdateInput();
        currentTime += 0.1;
        UpdateInput();
        Assert.That(completed, Is.SameAs(runtimeAction));
        Assert.That(runtimeAction.enabled, Is.True);
        Assert.That(sourceAction.SaveBindingOverridesAsJson(), Is.EqualTo(originalSource));
        Assert.That(PlayerPrefs.GetString(key), Is.Not.Empty);

        QueueButton(newControl, 0f);
        UpdateInput();
        QueueButton(oldControl, 1f);
        UpdateInput();
        Assert.That(runtimeAction.IsPressed(), Is.False, "Old control must stop activating gameplay.");
        QueueButton(oldControl, 0f);
        QueueButton(newControl, 1f);
        UpdateInput();
        Assert.That(runtimeAction.IsPressed(), Is.True, "New control must work without restarting.");

        int index = runtimeAction.GetBindingIndex(InputBinding.MaskByGroup(scheme));
        string saved = runtimeAction.bindings[index].effectivePath;
        runtimeAction.RemoveBindingOverride(index);
        managerType.GetMethod("LoadAllBindings").Invoke(manager, null);
        Assert.That(runtimeAction.bindings[index].effectivePath, Is.EqualTo(saved));
        AssertLabel(scheme, saved);
    }

    [Test]
    public void CancellingKeepsRuntimeBindingAndLabel()
    {
        PreservePreference("Gamepad");
        int index = runtimeAction.GetBindingIndex(InputBinding.MaskByGroup("Gamepad"));
        runtimeAction.ApplyBindingOverride(index, "<Gamepad>/leftShoulder");
        InputAction cancelled = null;
        StartRebind("Gamepad", cancel: a => cancelled = a);
        QueueButton(keyboard.escapeKey, 1f);
        UpdateInput();
        Assert.That(cancelled, Is.SameAs(runtimeAction));
        Assert.That(runtimeAction.enabled, Is.True);
        Assert.That(runtimeAction.bindings[index].effectivePath, Is.EqualTo("<Gamepad>/leftShoulder"));
        Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(previousBinding));
        AssertLabel("Gamepad", "<Gamepad>/leftShoulder");
    }

    private void AssertLabel(string scheme, string path)
    {
        var uiType = managerType.Assembly.GetType("RebindUI", true);
        var ui = root.AddComponent(uiType);
        var reference = InputActionReference.Create(sourceAction);
        try
        {
            uiType.GetField("rebindingAction", Private).SetValue(ui, reference);
            string label = (string)uiType.GetMethod("GetBindingForControlScheme", Private)
                .Invoke(ui, new object[] { scheme });
            Assert.That(label, Is.EqualTo(InputControlPath.ToHumanReadableString(path,
                InputControlPath.HumanReadableStringOptions.OmitDevice)));
        }
        finally { Object.DestroyImmediate(reference); }
    }
}
