using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class BotFollowupSceneTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static void InScene(string path, Action<Scene> check)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool loaded = scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { check(scene); }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void LobbyContainsBoundBotButtonsWithoutAClonedStartListener()
    {
        InScene("Assets/Scenes/LobbyScene.unity", scene =>
        {
            var ui = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(GameType("PartyScreenUI"), true)).Single();
            var serialized = new SerializedObject(ui);
            foreach (string field in new[] { "addBotsButton", "clearBotsButton" })
            {
                var button = serialized.FindProperty(field).objectReferenceValue as Component;
                Assert.That(button, Is.Not.Null, field);
                Assert.That(button.gameObject.activeSelf, Is.True);
                Assert.That(((RectTransform)button.transform).rect.width, Is.GreaterThan(100));
                var clicks = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                Assert.That(clicks.arraySize, Is.Zero, "Bot buttons must not inherit StartGame's persistent callback.");
            }
        });
    }

    [Test]
    public void TheiaHasCarvingBarriersBoundToTheCorrectReplicatedMatchObjects()
    {
        InScene("Assets/Scenes/TheiaSkyRuins.unity", scene =>
        {
            var barriers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(GameType("DynamicMapBarrier"), true)).ToArray();
            Assert.That(barriers.Length, Is.EqualTo(3));
            foreach (var barrier in barriers)
            {
                var serialized = new SerializedObject(barrier);
                var obstacle = serialized.FindProperty("navigationObstacle").objectReferenceValue as NavMeshObstacle;
                Assert.That(obstacle, Is.Not.Null);
                Assert.That(obstacle.carving, Is.True);
                Assert.That(obstacle.carveOnlyStationary, Is.False);
                Assert.That(serialized.FindProperty("blockingColliders").arraySize, Is.GreaterThan(0));
                Assert.That(serialized.FindProperty("wallRenderers").arraySize, Is.GreaterThan(0));
                if (barrier.name == "pref_sm_citysky_mid_lift_lv3")
                {
                    Assert.That(serialized.FindProperty("blocksWhileObjectiveAlive").objectReferenceValue, Is.Not.Null);
                    Assert.That(serialized.FindProperty("opensWhenGoalActive").objectReferenceValue, Is.Null);
                    Assert.That(barrier.GetComponentsInChildren<Collider>(true).Max(collider => collider.bounds.max.y), Is.GreaterThan(1.5f));
                }
                else
                {
                    var goal = serialized.FindProperty("opensWhenGoalActive").objectReferenceValue;
                    Assert.That(goal, Is.Not.Null);
                    Assert.That(goal.GetType().GetProperty("GoalTier").GetValue(goal), Is.EqualTo(0));
                    string expectedTeam = barrier.name.EndsWith("Red") ? "Orange" : "Blue";
                    Assert.That(goal.GetType().GetProperty("Team").GetValue(goal).ToString(), Is.EqualTo(expectedTeam));
                }
            }
        });
    }
}
