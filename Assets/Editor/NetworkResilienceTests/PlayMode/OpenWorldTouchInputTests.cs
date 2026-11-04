using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OpenWorldTouchInputTests
{
    private static readonly Type AvatarType = Type.GetType("OpenWorldTrainerAvatar, Assembly-CSharp", true);
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component avatar;
    private Touchscreen screen;
    private Mouse mouse;
    private EventSystem previousEvents;

    [SetUp]
    public void SetUp()
    {
        previousEvents = EventSystem.current;
        root = new GameObject("Open world touch input test");
        avatar = root.AddComponent(AvatarType);
        ((Behaviour)avatar).enabled = false;
        screen = InputSystem.AddDevice<Touchscreen>();
        InputSystem.EnableDevice(screen);
        Invoke("InitializeTouchLookInput");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Invoke("ReleaseTouchLookInput");
        InputSystem.RemoveDevice(screen);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        mouse = null;
        Object.Destroy(root);
        yield return null;
        if (previousEvents != null) EventSystem.current = previousEvents;
    }

    [Test]
    public void WorldDragRotatesYawAndClampsPitch()
    {
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(300f, 300f));
        float yaw = ReadFloat("cameraYaw");
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(400f, 400f));
        Assert.That(ReadFloat("cameraYaw"), Is.GreaterThan(yaw));
        Assert.That(ReadFloat("cameraPitch"), Is.EqualTo(10f).Within(0.001f));
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(400f, -1000f));
        Assert.That(ReadFloat("cameraPitch"), Is.EqualTo(70f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator MovementStickTouchNeverBecomesCameraDrag()
    {
        CreateUiBlocker();
        yield return null;
        Assert.That(AvatarType.GetMethod("IsTouchLookOverUi", PrivateInstance)
            .Invoke(avatar, new object[] { new Vector2(50f, 50f) }), Is.True);
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(50f, 50f));
        float yaw = ReadFloat("cameraYaw");
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(400f, 300f));
        Assert.That(ReadFloat("cameraYaw"), Is.EqualTo(yaw));
        SendTouch(2, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(300f, 300f));
        SendTouch(2, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(400f, 300f));
        Assert.That(ReadFloat("cameraYaw"), Is.GreaterThan(yaw));
    }

    [Test]
    public void AnotherFingerCannotRotateOrReleaseCameraDrag()
    {
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(300f, 300f));
        SendTouch(2, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(500f, 300f));
        float yaw = ReadFloat("cameraYaw");
        SendTouch(2, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(600f, 300f));
        Assert.That(ReadFloat("cameraYaw"), Is.EqualTo(yaw));
        SendTouch(2, UnityEngine.InputSystem.TouchPhase.Ended, new Vector2(600f, 300f));
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(400f, 300f));
        Assert.That(ReadFloat("cameraYaw"), Is.GreaterThan(yaw));
    }

    [TestCase("ResetTouchLookInput")]
    [TestCase("OnApplicationFocus")]
    [TestCase("OnApplicationPause")]
    public void BlockingOrLosingFocusRequiresNewGesture(string method)
    {
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(300f, 300f));
        if (method == "OnApplicationFocus")
        {
            Invoke(method, false);
            Invoke(method, true);
        }
        else if (method == "OnApplicationPause")
        {
            Invoke(method, true);
            Invoke(method, false);
        }
        else
            Invoke(method);
        float yaw = ReadFloat("cameraYaw");
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(400f, 300f));
        Assert.That(ReadFloat("cameraYaw"), Is.EqualTo(yaw));
    }

    [TestCase(UnityEngine.InputSystem.TouchPhase.Ended)]
    [TestCase(UnityEngine.InputSystem.TouchPhase.Canceled)]
    public void EndingGestureDoesNotTransferToAlreadyHeldFinger(UnityEngine.InputSystem.TouchPhase phase)
    {
        SendTouch(1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(300f, 300f));
        SendTouch(2, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(500f, 300f));
        SendTouch(1, phase, new Vector2(300f, 300f));
        float yaw = ReadFloat("cameraYaw");
        SendTouch(2, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(600f, 300f));
        Assert.That(ReadFloat("cameraYaw"), Is.EqualTo(yaw));
        Assert.That(AvatarType.GetField("cameraTouchFinger", PrivateInstance).GetValue(avatar), Is.Null);
    }

    [UnityTest]
    public IEnumerator MobileEditorMouseDragRotatesOnlyWhenStartedOutsideUi()
    {
#if !UNITY_EDITOR || !(UNITY_ANDROID || UNITY_IOS)
        Assert.Ignore("Requires a mobile Editor build target.");
#endif
        CreateUiBlocker();
        yield return null;
        mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.EnableDevice(mouse);
        SendMouse(new Vector2(50f, 50f), Vector2.zero, true);
        float yaw = ReadFloat("cameraYaw");
        SendMouse(new Vector2(400f, 300f), new Vector2(350f, 250f), true);
        Assert.That(ReadFloat("cameraYaw"), Is.EqualTo(yaw));
        SendMouse(new Vector2(400f, 300f), Vector2.zero, false);
        SendMouse(new Vector2(400f, 300f), Vector2.zero, true);
        SendMouse(new Vector2(500f, 300f), new Vector2(100f, 0f), true);
        Assert.That(ReadFloat("cameraYaw"), Is.GreaterThan(yaw));
    }

    private void SendMouse(Vector2 position, Vector2 delta, bool pressed)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position, delta = delta }.WithButton(MouseButton.Left, pressed));
        InputSystem.Update();
        mouse.MakeCurrent();
        Invoke("UpdateTouchLookInput");
    }

    private void SendTouch(int touchId, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(screen, new TouchState { touchId = touchId, phase = phase, position = position });
        InputSystem.Update();
        Invoke("UpdateTouchLookInput");
    }

    private void CreateUiBlocker()
    {
        var events = new GameObject("Test events", typeof(EventSystem));
        events.transform.SetParent(root.transform);
        EventSystem.current = events.GetComponent<EventSystem>();
        var canvasObject = new GameObject("Test canvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var image = new GameObject("Movement stick hit area", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(canvasObject.transform, false);
        var rect = image.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(200f, 200f);
        Canvas.ForceUpdateCanvases();
    }

    private float ReadFloat(string field) => (float)AvatarType.GetField(field, PrivateInstance).GetValue(avatar);
    private void Invoke(string method, params object[] arguments) => AvatarType.GetMethod(method, PrivateInstance).Invoke(avatar, arguments);
}
