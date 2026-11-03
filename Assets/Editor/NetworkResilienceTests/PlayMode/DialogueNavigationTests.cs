using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class DialogueNavigationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private GameObject audioRoot;
    private GameObject eventRoot;
    private Component dialogue;
    private TMP_Text body;
    private Button next;
    private EventSystem events;
    private EventSystem previousEvents;
    private Keyboard keyboard;
    private Gamepad gamepad;
    private float previousTimeScale;
    private InputSettings inputSettings;
    private InputSettings.BackgroundBehavior previousBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private object Field(string name) => dialogue.GetType().GetField(name, Private).GetValue(dialogue);
    private bool Visible => (bool)dialogue.GetType().GetProperty("IsVisible").GetValue(dialogue);
    private void Call(string method) => dialogue.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(dialogue, null);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previousTimeScale = Time.timeScale;
        var type = GameType("DialogueSystem");
        if (type.GetProperty("Instance").GetValue(null) != null) Assert.Ignore("An external dialogue system is active.");
        inputSettings = InputSystem.settings;
        previousBackgroundBehavior = inputSettings.backgroundBehavior;
        previousEditorInputBehavior = inputSettings.editorInputBehaviorInPlayMode;
        inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
#if UNITY_EDITOR
        audioRoot = new GameObject("Dialogue navigation audio");
        audioRoot.SetActive(false);
        var audio = audioRoot.AddComponent(GameType("JSAM.AudioManager"));
        var libraries = Array.CreateInstance(GameType("JSAM.AudioLibrary"), 1);
        libraries.SetValue(AssetDatabase.LoadMainAssetAtPath("Assets/Audio/DefaultAudio.asset"), 0);
        Set(audio, "preloadedLibraries", libraries);
        audioRoot.SetActive(true);
#endif
        previousEvents = EventSystem.current;
        eventRoot = new GameObject("Dialogue input events", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events = eventRoot.GetComponent<EventSystem>();
        eventRoot.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        EventSystem.current = events;
        root = new GameObject("Dialogue navigation fixture");
        root.SetActive(false);
        dialogue = root.AddComponent(type);
        var panel = new GameObject("Dialogue panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var title = Label("Title", panel.transform);
        body = Label("Body", panel.transform);
        next = Button("Next", panel.transform);
        var previous = Button("Previous", panel.transform);
        var preview = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        preview.transform.SetParent(panel.transform, false);
        Set(dialogue, "dialoguePanel", panel);
        Set(dialogue, "titleText", title);
        Set(dialogue, "bodyText", body);
        Set(dialogue, "nextButton", next);
        Set(dialogue, "previousButton", previous);
        Set(dialogue, "previewImage", preview.GetComponent<Image>());
        Set(dialogue, "dialogues", Array.CreateInstance(GameType("Dialogue"), 0));
        root.SetActive(true);
        yield return null;
        Set(dialogue, "hasFocus", true);
    }

    private static TMP_Text Label(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        node.transform.SetParent(parent, false);
        return node.GetComponent<TMP_Text>();
    }

    private static Button Button(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        node.transform.SetParent(parent, false);
        return node.GetComponent<Button>();
    }

    private void Open()
    {
        var pages = Array.CreateInstance(GameType("Dialogue"), 3);
        for (int index = 0; index < pages.Length; index++)
        {
            var page = Activator.CreateInstance(GameType("Dialogue"));
            page.GetType().GetField("Title").SetValue(page, $"Speaker {index}");
            page.GetType().GetField("Body").SetValue(page, $"Page {index}: This dialogue is long enough to test revealing text before advancing to the next page.");
            pages.SetValue(page, index);
        }
        dialogue.GetType().GetMethod("SetDialogues", new[] { pages.GetType() }).Invoke(dialogue, new object[] { pages });
    }

    private IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    private IEnumerator Press(GamepadButton button)
    {
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
        yield return null;
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = previousTimeScale;
        if (root != null) Object.Destroy(root);
        if (audioRoot != null) Object.Destroy(audioRoot);
        if (eventRoot != null) Object.Destroy(eventRoot);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (gamepad != null) InputSystem.RemoveDevice(gamepad);
        if (inputSettings != null)
        {
            inputSettings.backgroundBehavior = previousBackgroundBehavior;
            inputSettings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        }
        yield return null;
        if (previousEvents != null) EventSystem.current = previousEvents;
    }

    [UnityTest]
    public IEnumerator KeyboardRevealsPagesNavigatesAndCloses()
    {
        Open();
        yield return Press(Key.Enter);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(0));
        Assert.That(Field("isWriting"), Is.False);
        yield return Press(Key.Space);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(1));
        yield return Press(Key.LeftArrow);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(0));
        yield return Press(Key.RightArrow);
        yield return Press(Key.RightArrow);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(2));
        yield return Press(Key.E);
        Assert.That(Field("isWriting"), Is.False);
        yield return Press(Key.Enter);
        Assert.That(Visible, Is.False);
    }

    [UnityTest]
    public IEnumerator ControllerSubmitDoesNotDoubleAdvanceThroughEventSystem()
    {
        Open();
        events.SetSelectedGameObject(next.gameObject);
        Assert.That(events.sendNavigationEvents, Is.False);
        yield return Press(GamepadButton.South);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(0));
        Assert.That(Field("isWriting"), Is.False);
        yield return Press(GamepadButton.South);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(1));
        yield return Press(GamepadButton.DpadRight);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(2));
        yield return Press(GamepadButton.LeftShoulder);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(1));
        yield return Press(GamepadButton.East);
        Assert.That(Visible, Is.False);
        Assert.That(events.sendNavigationEvents, Is.True);
    }

    [UnityTest]
    public IEnumerator OpeningInputIsIgnoredAndClosingInputIsConsumedForTheFrame()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        InputSystem.Update();
        Open();
        Call("Update");
        Assert.That(Field("isWriting"), Is.True);
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(0));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Call("HideDialoguePanel");
        Assert.That(dialogue.GetType().GetProperty("BlocksWorldInput").GetValue(dialogue), Is.True);
        yield return null;
        Assert.That(dialogue.GetType().GetProperty("BlocksWorldInput").GetValue(dialogue), Is.False);
        Assert.That(events.sendNavigationEvents, Is.True);
    }

    [UnityTest]
    public IEnumerator PausedTextStillTypesAndEscapeRestoresNavigationSelection()
    {
        var selection = Button("Previous menu selection", eventRoot.transform).gameObject;
        events.SetSelectedGameObject(selection);
        Time.timeScale = 0f;
        Open();
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(body.text.Length, Is.GreaterThan(1));
        yield return Press(Key.Escape);
        Assert.That(Visible, Is.False);
        Assert.That(events.sendNavigationEvents, Is.True);
        Assert.That(events.currentSelectedGameObject, Is.SameAs(selection));
    }

    [UnityTest]
    public IEnumerator DisablingDialogueRestoresThePreviousNavigationSetting()
    {
        events.sendNavigationEvents = false;
        Open();
        root.SetActive(false);
        Assert.That(events.sendNavigationEvents, Is.False);
        Assert.That(Visible, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ControllerStickUsesDeadzoneAndDoesNotRepeatImmediately()
    {
        Open();
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(0.4f, 0f) });
        yield return null;
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(0));
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.right });
        yield return null;
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(1));
        yield return null;
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(1));
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.left });
        yield return null;
        Assert.That(Field("currentDialogueIndex"), Is.EqualTo(0));
        yield return Press(GamepadButton.East);
        Assert.That(Visible, Is.False);
    }
}
