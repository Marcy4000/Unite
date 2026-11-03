using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class OpenWorldPokemonUiTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [Test]
    public void CompanionPickerHasEmptyGridAndRuntimePortraitTemplate()
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab");
        var menu = hud.GetComponent(GameType("OpenWorldPokemonMenu"));
        Assert.That(menu, Is.Not.Null);
        var data = new SerializedObject(menu);
        foreach (string field in new[] { "openButton", "currentIcon", "currentName", "status", "panel", "scroll" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        var open = (Button)data.FindProperty("openButton").objectReferenceValue;
        Assert.That(((RectTransform)open.transform).anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
        Assert.That(((Image)data.FindProperty("currentIcon").objectReferenceValue).preserveAspect, Is.True);
        AssertClick(open, menu, "Toggle");
        var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
        Assert.That(panel.activeSelf, Is.False);
        AssertClick(panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Close companions"), menu, "Close");
        var scroll = (ScrollRect)data.FindProperty("scroll").objectReferenceValue;
        Assert.That(scroll.horizontal, Is.False);
        Assert.That(scroll.vertical, Is.True);
        Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(scroll.content.GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(3));
        Assert.That(scroll.content.childCount, Is.Zero);
        var buttonPrefab = (Button)data.FindProperty("buttonPrefab").objectReferenceValue;
        Assert.That(buttonPrefab, Is.Not.Null);
        Assert.That(AssetDatabase.Contains(buttonPrefab), Is.True);
        Assert.That(buttonPrefab.onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(buttonPrefab.GetComponentInChildren<TMPro.TMP_Text>(true), Is.Not.Null);
        Assert.That(buttonPrefab.transform.Find("Portrait").GetComponent<Image>().preserveAspect, Is.True);
    }

    [Test]
    public void TrainerReferencesFollowerConfiguredForTheExistingCityBake()
    {
        var trainer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldTrainer.prefab");
        var companion = trainer.GetComponent(GameType("OpenWorldPokemonCompanion"));
        Assert.That(companion, Is.Not.Null);
        var follower = (Component)new SerializedObject(companion).FindProperty("followerPrefab").objectReferenceValue;
        Assert.That(follower, Is.Not.Null);
        var agent = follower.GetComponent(GameType("UnityEngine.AI.NavMeshAgent"));
        Assert.That(((Behaviour)agent).enabled, Is.False, "The agent must wait for a valid NavMesh position.");
        var city = EditorSceneManager.OpenPreviewScene("Assets/Scenes/ChineseLobby.unity");
        try
        {
            var surface = city.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(GameType("Unity.AI.Navigation.NavMeshSurface"), true))
                .First(component => new SerializedObject(component).FindProperty("m_NavMeshData").objectReferenceValue != null);
            Assert.That(new SerializedObject(agent).FindProperty("m_AgentTypeID").intValue,
                Is.EqualTo(new SerializedObject(surface).FindProperty("m_AgentTypeID").intValue));
        }
        finally { EditorSceneManager.ClosePreviewScene(city); }
    }

    private static void AssertClick(Button button, Component target, string method)
    {
        Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(button.onClick.GetPersistentTarget(0), Is.EqualTo(target));
        Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(method));
    }
}
