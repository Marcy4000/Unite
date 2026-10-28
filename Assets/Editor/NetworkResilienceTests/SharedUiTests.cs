using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class SharedUiTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    private GameObject root;
    private bool hadName;
    private string previousName;
    private bool cursorVisible;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, Private).Invoke(target, args);

    [SetUp]
    public void Setup()
    {
        hadName = PlayerPrefs.HasKey("PlayerName");
        previousName = PlayerPrefs.GetString("PlayerName");
        cursorVisible = Cursor.visible;
        root = new GameObject("Shared UI regression fixture");
        root.SetActive(false);
    }

    [TearDown]
    public void Cleanup()
    {
        Object.DestroyImmediate(root);
        Cursor.visible = cursorVisible;
        if (hadName) PlayerPrefs.SetString("PlayerName", previousName);
        else PlayerPrefs.DeleteKey("PlayerName");
        PlayerPrefs.Save();
    }

    [Test]
    public void CursorOnlyHidesForIdleControllerGameplay()
    {
        var input = root.AddComponent(GameType("InputManager"));
        Call(input, "UpdateInputMode", false, false, true, 10f);
        Assert.That(Call(input, "ShouldShowCursor", true, false, true, 10f), Is.False);
        Assert.That(Call(input, "ShouldShowCursor", false, false, true, 10f), Is.True);
        Assert.That(Call(input, "ShouldShowCursor", true, true, true, 10f), Is.True);
        Assert.That(Call(input, "ShouldShowCursor", true, false, false, 10f), Is.True);
        Call(input, "UpdateInputMode", true, false, true, 11f);
        Assert.That(Call(input, "ShouldShowCursor", true, false, true, 11f), Is.True);
        Call(input, "UpdateInputMode", false, false, true, 12f);
        Assert.That(Call(input, "ShouldShowCursor", true, false, true, 12f), Is.True);
        Assert.That(Call(input, "ShouldShowCursor", true, false, true, 14f), Is.False);
        Call(input, "UpdateInputMode", false, true, true, 15f);
        Assert.That(Call(input, "ShouldShowCursor", true, false, true, 15f), Is.True);
    }

    [Test]
    public void UnactuatedControllerDoesNotChangeKeyboardMode()
    {
        var input = root.AddComponent(GameType("InputManager"));
        Call(input, "UpdateInputMode", true, false, false, 1f);
        Call(input, "UpdateInputMode", false, false, false, 10f);
        Assert.That(input.GetType().GetProperty("IsControllerMode").GetValue(input), Is.False);
        Assert.That(Call(input, "ShouldShowCursor", true, false, true, 10f), Is.True);
    }

    [Test]
    public void PlayerNamePersistsAndWhitespaceCannotOverwriteIt()
    {
        var boot = root.AddComponent(GameType("BootSceneController"));
        Assert.That(Call(boot, "SavePlayerName", "Trainer 42"), Is.True);
        Assert.That(PlayerPrefs.GetString("PlayerName"), Is.EqualTo("Trainer 42"));
        Assert.That(Call(boot, "SavePlayerName", "  \t"), Is.False);
        Assert.That(Call(boot, "SavePlayerName", (object)null), Is.False);
        Assert.That(PlayerPrefs.GetString("PlayerName"), Is.EqualTo("Trainer 42"));
        var field = new GameObject("Name input", typeof(RectTransform));
        field.transform.SetParent(root.transform, false);
        var input = field.AddComponent(GameType("TMPro.TMP_InputField"));
        boot.GetType().GetField("playerNameInputField", Private).SetValue(boot, input);
        Call(boot, "RestorePlayerName");
        Assert.That(input.GetType().GetProperty("text").GetValue(input), Is.EqualTo("Trainer 42"));
    }

    [Test]
    public void SettingsCreditsAndKeybindPanelsRequireMenuCursor()
    {
        var settings = root.AddComponent(GameType("SettingsManager"));
        root.SetActive(true);
        foreach (var name in new[] { "settingsPanel", "creditsPanel", "keybindsPanel" })
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(root.transform, false);
            panel.SetActive(false);
            settings.GetType().GetField(name, Private).SetValue(settings, panel);
            Assert.That(settings.GetType().GetProperty("IsMenuOpen").GetValue(settings), Is.False);
            panel.SetActive(true);
            Assert.That(settings.GetType().GetProperty("IsMenuOpen").GetValue(settings), Is.True);
            panel.SetActive(false);
        }
    }

    [Test]
    public void ErrorOverlayUsesOpaqueHighContrastUiAndSelectableDismissal()
    {
        var notice = root.AddComponent(GameType("ConnectionFailureNotice"));
        Call(notice, "BuildOverlay");
        var overlay = (GameObject)notice.GetType().GetField("overlay", Private).GetValue(notice);
        Assert.That(overlay.transform.Find("Backdrop/Dialog"), Is.Not.Null);
        var title = notice.GetType().GetField("titleText", Private).GetValue(notice);
        Assert.That(title.GetType().GetProperty("color").GetValue(title), Is.EqualTo(Color.white));
        Assert.That(title.GetType().GetProperty("richText").GetValue(title), Is.False);
        var dismiss = (Component)notice.GetType().GetField("dismissButton", Private).GetValue(notice);
        Assert.That(dismiss.gameObject.name, Is.EqualTo("OK"));
        Assert.That(dismiss.GetType().GetProperty("interactable").GetValue(dismiss), Is.True);
        Assert.That(notice.GetType().GetMethod("OnGUI", Private), Is.Null);
    }
}
