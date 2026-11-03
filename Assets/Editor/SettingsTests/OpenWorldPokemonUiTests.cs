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
    public void CompanionPickerHasAuthoredPortraitsAndPersistentCallbacksForTheRoster()
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
        var boot = EditorSceneManager.OpenPreviewScene("Assets/Scenes/BootScene.unity");
        try
        {
            var roster = boot.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(GameType("CharactersList"), true)).Single();
            var characters = new SerializedObject(roster).FindProperty("characters");
            var expected = Enumerable.Range(0, characters.arraySize).Select(index => characters.GetArrayElementAtIndex(index).objectReferenceValue)
                .Where(character => character != null && !new SerializedObject(character).FindProperty("Hidden").boolValue).ToArray();
            var rows = data.FindProperty("pokemonButtons");
            Assert.That(rows.arraySize, Is.EqualTo(expected.Length));
            for (int index = 0; index < rows.arraySize; index++)
            {
                var row = rows.GetArrayElementAtIndex(index);
                Assert.That(row.FindPropertyRelative("character").objectReferenceValue, Is.EqualTo(expected[index]));
                var button = (Button)row.FindPropertyRelative("button").objectReferenceValue;
                AssertClick(button, menu, "SelectPokemon");
                var callback = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0);
                Assert.That(callback.FindPropertyRelative("m_Arguments.m_IntArgument").intValue, Is.EqualTo(index));
                Assert.That(button.transform.Find("Portrait").GetComponent<Image>().sprite, Is.Not.Null);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(boot); }
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
