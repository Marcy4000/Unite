#if UNITY_ANDROID
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using Object = UnityEngine.Object;

public class AndroidInputTests : InputTestFixture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component manager;
    private Type managerType;
    private object previousManager;
    private Gamepad gamepad;
    private Gamepad touchGamepad;
    private Touchscreen touchscreen;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    public override void Setup()
    {
        base.Setup();
        root = new GameObject("Android input tests");
        root.SetActive(false);
        managerType = GameType("InputManager");
        previousManager = managerType.GetField("Instance").GetValue(null);
        manager = root.AddComponent(managerType);
        managerType.GetField("Instance").SetValue(null, manager);
        gamepad = InputSystem.AddDevice<Gamepad>();
        touchGamepad = InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDeviceUsage(touchGamepad, "OnScreen");
        touchscreen = InputSystem.AddDevice<Touchscreen>();
    }

    public override void TearDown()
    {
        Object.DestroyImmediate(root);
        managerType.GetField("Instance").SetValue(null, previousManager);
        base.TearDown();
    }

    private bool ControllerMode => (bool)managerType.GetProperty("IsControllerMode").GetValue(manager);

    private void DetectInput() => managerType.GetMethod("UpdateAndroidInputMode", Private).Invoke(manager, null);

    [Test]
    public void ConnectedIdleControllerDefaultsToTouch()
    {
        DetectInput();
        Assert.That(ControllerMode, Is.False);
    }

    [Test]
    public void VirtualTouchGamepadDoesNotSelectControllerMode()
    {
        Set(touchGamepad.rightStick, Vector2.right);
        Press(touchGamepad.buttonSouth);
        DetectInput();
        Assert.That(ControllerMode, Is.False);
    }

    [Test]
    public void PhysicalControllerStickSelectsAndKeepsControllerMode()
    {
        Set(gamepad.rightStick, Vector2.right);
        DetectInput();
        Assert.That(ControllerMode, Is.True);
        Set(gamepad.rightStick, Vector2.zero);
        InputSystem.Update();
        DetectInput();
        Assert.That(ControllerMode, Is.True);
    }

    [Test]
    public void PhysicalControllerButtonSelectsControllerMode()
    {
        Press(gamepad.buttonSouth);
        DetectInput();
        Assert.That(ControllerMode, Is.True);
    }

    [Test]
    public void TouchOverridesControllerAndControllerCanResume()
    {
        Set(gamepad.rightStick, Vector2.right);
        DetectInput();
        BeginTouch(1, new Vector2(100f, 100f), screen: touchscreen);
        DetectInput();
        Assert.That(ControllerMode, Is.False);
        Set(touchGamepad.rightStick, Vector2.up);
        DetectInput();
        Assert.That(ControllerMode, Is.False);
        EndTouch(1, new Vector2(100f, 100f), screen: touchscreen);
        Set(gamepad.rightStick, Vector2.up);
        DetectInput();
        Assert.That(ControllerMode, Is.True);
    }

    [Test]
    public void DisconnectReturnsToTouchEvenWithVirtualGamepadPresent()
    {
        Set(gamepad.rightStick, Vector2.right);
        DetectInput();
        Assert.That(ControllerMode, Is.True);
        InputSystem.RemoveDevice(gamepad);
        DetectInput();
        Assert.That(ControllerMode, Is.False);
    }

    [TestCase("Touch", 5f)]
    [TestCase("Controller", 2f)]
    public void CircleAimUsesTouchRadiusOrStandardControllerPosition(string scheme, float expectedPosition)
    {
        var controlsType = managerType.GetProperty("Controls").PropertyType;
        var controls = Activator.CreateInstance(controlsType);
        var asset = (InputActionAsset)controlsType.GetProperty("asset").GetValue(controls);
        try
        {
            asset.Enable();
            Set(gamepad.rightStick, scheme == "Touch" ? Vector2.right : Vector2.zero);
            var aimType = GameType("Aim");
            var aim = root.AddComponent(aimType);
            var indicator = new GameObject("Circle indicator");
            indicator.transform.SetParent(root.transform, false);
            aimType.GetField("controls", Private).SetValue(aim, controls);
            aimType.GetField("activeControlScheme", Private).SetValue(aim, scheme);
            aimType.GetField("circleAreaIndicator", Private).SetValue(aim, indicator);
            aimType.GetField("maxCircleAimRadius", Private).SetValue(aim, 5f);
            aimType.GetField("circleAimPosition", Private).SetValue(aim, new Vector3(2f, 0f, 0f));
            var result = (Vector3)aimType.GetMethod("CircleAreaAim").Invoke(aim, null);
            Assert.That(result.x, Is.EqualTo(expectedPosition).Within(0.001f));
            Assert.That(result.z, Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            asset.Disable();
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void MobileStickWritesToItsVirtualDeviceNotThePhysicalController()
    {
        var buttonRoot = new GameObject("Mobile stick");
        buttonRoot.SetActive(false);
        var buttonType = GameType("MobileButtonWithStick");
        var button = (OnScreenControl)buttonRoot.AddComponent(buttonType);
        button.controlPath = "<Gamepad>/buttonSouth";
        try
        {
            typeof(OnScreenControl).GetMethod("OnEnable", Private).Invoke(button, null);
            buttonType.GetMethod("SendInputToControl", Private).Invoke(button, new object[] { Vector2.right });
            InputSystem.Update();
            Assert.That(((Gamepad)button.control.device).rightStick.ReadValue(), Is.EqualTo(Vector2.right));
            Assert.That(gamepad.rightStick.ReadValue(), Is.EqualTo(Vector2.zero));
        }
        finally
        {
            typeof(OnScreenControl).GetMethod("OnDisable", Private).Invoke(button, null);
            Object.DestroyImmediate(buttonRoot);
        }
    }
}
#endif
