using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OpenWorldChatBubbleTests
{
    private GameObject root;
    private Component bubble;
    private RectTransform panel;
    private TMP_Text text;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private object Field(string name) => bubble.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bubble);
    private void SetField(string name, object value) => bubble.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bubble, value);
    private void Call(string method, params object[] arguments) => bubble.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(bubble, arguments);

    [SetUp]
    public void LoadTrainer()
    {
        root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab"));
        bubble = root.GetComponent(GameType("OpenWorldTrainerChatBubble"));
        Assert.That(bubble, Is.Not.Null);
        panel = (RectTransform)Field("bubbleRoot");
        text = (TMP_Text)Field("messageText");
    }

    [TearDown]
    public void Cleanup() => Object.DestroyImmediate(root);

    [Test]
    public void BubbleIsAuthoredAboveNameplateWithChatSpriteAndNonInteractiveText()
    {
        Assert.That(panel, Is.Not.Null);
        Assert.That(text, Is.Not.Null);
        Assert.That(panel.gameObject.activeSelf, Is.False);
        Assert.That(panel.IsChildOf(root.transform), Is.True);
        Assert.That(panel.localPosition.y, Is.GreaterThan(root.transform.Find("PlayerName").localPosition.y));
        Assert.That(panel.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(panel.GetComponent(GameType("LookAtCamera")), Is.Not.Null);
        Assert.That(text.font, Is.Not.Null);
        Assert.That(text.richText, Is.False);
        Assert.That(text.parseCtrlCharacters, Is.False);
        Assert.That(text.raycastTarget, Is.False);
        Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
        Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        var image = panel.GetComponentInChildren<Image>(true);
        Assert.That(image.raycastTarget, Is.False);
        Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo("Assets/Sprites/Chat/t_HeroSelection_bg_Chat.png"));
        Assert.That(image.sprite.border, Is.Not.EqualTo(Vector4.zero));
        Assert.That(panel.GetComponent<GraphicRaycaster>(), Is.Null);
    }

    [Test]
    public void NewMessageReplacesTextAndRestartsTimer()
    {
        Call("Show", "First message");
        SetField("hideTime", -1f);
        Call("Show", "  <b>Second message</b>  ");
        Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(text.text, Is.EqualTo("<b>Second message</b>"));
        Assert.That((float)Field("hideTime"), Is.GreaterThan(Time.unscaledTime));
        Assert.That(text.richText, Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \n ")]
    public void EmptyMessagesDoNotShowBubble(string message)
    {
        Call("Show", message);
        Assert.That(panel.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void LongMessagesHaveBoundedSizeAndDisplayLength()
    {
        Call("Show", string.Join(" ", Enumerable.Repeat("Long message", 100)));
        var min = (Vector2)Field("minimumSize");
        var max = (Vector2)Field("maximumSize");
        Assert.That(panel.sizeDelta.x, Is.InRange(min.x, max.x));
        Assert.That(panel.sizeDelta.y, Is.InRange(min.y, max.y));
        Assert.That(text.text.Length, Is.EqualTo((int)Field("maximumCharacters") + 1));
        Assert.That(text.text.EndsWith("…"), Is.True);
    }

    [Test]
    public void TruncationDoesNotSplitEmojiSurrogates()
    {
        SetField("maximumCharacters", 4);
        Call("Show", "abc\U0001F600def");
        Assert.That(text.text, Is.EqualTo("abc…"));
    }

    [TestCase("Update")]
    [TestCase("OnDisable")]
    [TestCase("Hide")]
    public void TimeoutAndCleanupHideAndClearBubble(string cleanup)
    {
        Call("Show", "Visible message");
        SetField("hideTime", -1f);
        Call(cleanup);
        Assert.That(panel.gameObject.activeSelf, Is.False);
        Assert.That(text.text, Is.Empty);
    }
}
