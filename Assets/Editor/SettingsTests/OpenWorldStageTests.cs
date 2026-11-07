using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class OpenWorldStageTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [Test]
    public void HudUsesExistingScrollViewAndReusableTrackButtonPrefab()
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab");
        var menu = hud.GetComponent(GameType("OpenWorldStageMenu"));
        Assert.That(menu, Is.Not.Null);
        var data = new SerializedObject(menu);
        var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
        Assert.That(panel.transform.IsChildOf(hud.transform), Is.True);
        Assert.That(data.FindProperty("closeButton").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("status").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("tracks"), Is.Null, "The jukebox, not the HUD, owns the playlist.");
        var scroll = (ScrollRect)data.FindProperty("scroll").objectReferenceValue;
        Assert.That(scroll.name, Is.EqualTo("Scroll View"));
        Assert.That(scroll.transform.IsChildOf(panel.transform), Is.True);
        Assert.That(scroll.vertical, Is.True);
        Assert.That(scroll.viewport, Is.Not.Null);
        Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        Assert.That(scroll.content.childCount, Is.Zero);
        Assert.That(scroll.content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
        Assert.That(scroll.content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
        var template = (Component)data.FindProperty("buttonPrefab").objectReferenceValue;
        Assert.That(EditorUtility.IsPersistent(template), Is.True);
        var row = new SerializedObject(template);
        var button = (Button)row.FindProperty("button").objectReferenceValue;
        var label = (TMP_Text)row.FindProperty("label").objectReferenceValue;
        Assert.That(label.font, Is.Not.Null);
        Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(template.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThan(0f));
    }

    [Test]
    public void TrackButtonInitializationPreservesCustomListenersAndAvoidsDuplicates()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Lobby/OpenWorldStageTrackButton.prefab");
        try
        {
            var row = root.GetComponent(GameType("OpenWorldStageTrackButton"));
            var data = new SerializedObject(row);
            var button = (Button)data.FindProperty("button").objectReferenceValue;
            int customCalls = 0;
            int selectionCalls = 0;
            button.onClick.AddListener(() => customCalls++);
            UnityEngine.Events.UnityAction select = () => selectionCalls++;
            row.GetType().GetMethod("Initialize").Invoke(row, new object[] { 3, select });
            row.GetType().GetMethod("Initialize").Invoke(row, new object[] { 3, select });
            button.onClick.Invoke();
            Assert.That(customCalls, Is.EqualTo(1));
            Assert.That(selectionCalls, Is.EqualTo(1));
            Assert.That(row.GetType().GetProperty("TrackIndex").GetValue(row), Is.EqualTo(3));
            row.GetType().GetMethod("Clear").Invoke(row, null);
            button.onClick.Invoke();
            Assert.That(customCalls, Is.EqualTo(2));
            Assert.That(selectionCalls, Is.EqualTo(1));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void CityContainsNetworkedConsoleBoundedStageAndValidMusic()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/ChineseLobby.unity");
        try
        {
            var stage = scene.GetRootGameObjects().Single(root => root.name == "Stage Jukebox");
            Assert.That(stage.GetComponent(GameType("Unity.Netcode.NetworkObject")), Is.Not.Null);
            var jukebox = stage.GetComponent(GameType("OpenWorldStageJukebox"));
            var data = new SerializedObject(jukebox);
            var area = (Collider)data.FindProperty("stageArea").objectReferenceValue;
            Assert.That(area, Is.Not.Null);
            Assert.That(area.isTrigger, Is.True);
            var console = (Component)data.FindProperty("console").objectReferenceValue;
            var npc = new SerializedObject(console);
            Assert.That(npc.FindProperty("interaction").enumValueIndex, Is.EqualTo(4));
            Assert.That(npc.FindProperty("interactionLabel").stringValue, Is.EqualTo("DJ Jukebox"));
            Assert.That(console.GetComponentInChildren<BoxCollider>().isTrigger, Is.False);
            Assert.That(jukebox.GetType().GetMethod("Contains").Invoke(jukebox, new object[] { console.transform.position }), Is.True);
            Assert.That(jukebox.GetType().GetMethod("Contains").Invoke(jukebox, new object[] { Vector3.zero }), Is.False);
            var music = data.FindProperty("tracks").GetArrayElementAtIndex(0).FindPropertyRelative("music").objectReferenceValue;
            var musicData = new SerializedObject(music);
            Assert.That(musicData.FindProperty("files").arraySize, Is.EqualTo(1));
            Assert.That(musicData.FindProperty("files").GetArrayElementAtIndex(0).objectReferenceValue, Is.Not.Null);
            Assert.That(musicData.FindProperty("loopMode").enumValueIndex, Is.EqualTo(1));
            Assert.That(musicData.FindProperty("pitchShift").floatValue, Is.Zero);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(0d, 100f, 4f, 80f, 2, 0f)]
    [TestCase(81d, 100f, 4f, 80f, 2, 5f)]
    [TestCase(156d, 100f, 4f, 80f, 2, 4f)]
    [TestCase(0d, 100f, 4f, 80f, 3, 4f)]
    [TestCase(125d, 100f, 0f, 0f, 1, 25f)]
    [TestCase(-1d, 100f, 0f, 0f, 1, 0f)]
    [TestCase(5d, 0f, 0f, 0f, 1, 0f)]
    public void SharedPlaybackClockRespectsIntroAndLoopPoints(double elapsed, float length, float start, float end, int mode, float expected)
    {
        Assert.That(GameType("OpenWorldStageJukebox").GetMethod("PlaybackTime").Invoke(null,
            new object[] { elapsed, length, start, end, mode }), Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(0f, 0.55f, -32f, true)]
    [TestCase(9f, 0f, -30f, true)]
    [TestCase(9.01f, 0f, -30f, false)]
    [TestCase(0f, 5.01f, -30f, false)]
    [TestCase(0f, -1.01f, -30f, false)]
    [TestCase(0f, 0f, -22.99f, false)]
    [TestCase(0f, 0f, -37.01f, false)]
    public void StageAreaChecksAllThreeAxes(float positionX, float positionY, float positionZ, bool expected)
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Lobby/OpenWorldStageJukebox.prefab");
        try
        {
            var stage = root.GetComponent(GameType("OpenWorldStageJukebox"));
            var node = new GameObject("Test area");
            node.transform.SetParent(root.transform, false);
            node.transform.localPosition = new Vector3(0f, 2f, -30f);
            var box = node.AddComponent<BoxCollider>();
            box.size = new Vector3(18f, 6f, 14f);
            var data = new SerializedObject(stage);
            data.FindProperty("stageArea").objectReferenceValue = box;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(stage.GetType().GetMethod("Contains").Invoke(stage,
                new object[] { new Vector3(positionX, positionY, positionZ) }), Is.EqualTo(expected));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void JukeboxOwnsPlaylistAndSelectsOnlyPlayableDefaults()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Lobby/OpenWorldStageJukebox.prefab");
        try
        {
            var stage = root.GetComponent(GameType("OpenWorldStageJukebox"));
            var type = stage.GetType();
            var data = new SerializedObject(stage);
            var tracks = data.FindProperty("tracks");
            Assert.That(tracks.arraySize, Is.EqualTo(7));
            data.FindProperty("defaultTrack").intValue = 2;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(type.GetProperty("DefaultTrack").GetValue(stage), Is.EqualTo(2));
            tracks.GetArrayElementAtIndex(2).FindPropertyRelative("music").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(type.GetProperty("DefaultTrack").GetValue(stage), Is.EqualTo(0));
            Assert.That(type.GetMethod("IsValidTrack").Invoke(stage, new object[] { 2 }), Is.False);
            tracks.GetArrayElementAtIndex(0).FindPropertyRelative("music").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(type.GetProperty("DefaultTrack").GetValue(stage), Is.EqualTo(1));
            tracks.arraySize = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(type.GetProperty("DefaultTrack").GetValue(stage), Is.EqualTo(-1));
            Assert.That(type.GetMethod("IsValidTrack").Invoke(stage, new object[] { -1 }), Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void CustomizedSphereStageAreaKeepsItsShapeAndChecksRadius()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Lobby/OpenWorldStageJukebox.prefab");
        try
        {
            var stage = root.GetComponent(GameType("OpenWorldStageJukebox"));
            var area = (SphereCollider)new SerializedObject(stage).FindProperty("stageArea").objectReferenceValue;
            Assert.That(area, Is.Not.Null);
            Vector3 center = area.transform.TransformPoint(area.center);
            Assert.That(stage.GetType().GetMethod("Contains").Invoke(stage, new object[] { center }), Is.True);
            Assert.That(stage.GetType().GetMethod("Contains").Invoke(stage, new object[] { center + Vector3.right * area.radius * 0.99f }), Is.True);
            Assert.That(stage.GetType().GetMethod("Contains").Invoke(stage, new object[] { center + Vector3.right * area.radius * 1.01f }), Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void BlankTrackDisplayNameFallsBackToMusicAssetName()
    {
        var type = GameType("OpenWorldStageJukebox").GetNestedType("Track");
        var track = Activator.CreateInstance(type);
        var music = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Audio/Music/Psyduck-Racing-BGM.asset");
        type.GetField("music").SetValue(track, music);
        type.GetField("title").SetValue(track, "  ");
        Assert.That(type.GetProperty("DisplayName").GetValue(track), Is.EqualTo(music.name));
        type.GetField("title").SetValue(track, "Race night");
        Assert.That(type.GetProperty("DisplayName").GetValue(track), Is.EqualTo("Race night"));
    }

    [Test]
    public void MigrationSnapshotPreservesSelectionAndRejectsInvalidValues()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Lobby/OpenWorldStageJukebox.prefab");
        try
        {
            var type = GameType("OpenWorldStageJukebox");
            var stage = root.GetComponent(type);
            var snapshotType = type.GetNestedType("Snapshot");
            var snapshot = Activator.CreateInstance(snapshotType);
            snapshotType.GetField("track").SetValue(snapshot, 1);
            snapshotType.GetField("elapsed").SetValue(snapshot, 123d);
            snapshotType.GetField("cooldown").SetValue(snapshot, 3f);
            type.GetMethod("RestoreSnapshot").Invoke(stage, new[] { snapshot });
            Assert.That(type.GetMethod("CaptureSnapshot").Invoke(stage, null), Is.SameAs(snapshot));
            var invalid = Activator.CreateInstance(snapshotType);
            snapshotType.GetField("track").SetValue(invalid, 999);
            type.GetMethod("RestoreSnapshot").Invoke(stage, new[] { invalid });
            Assert.That(type.GetMethod("CaptureSnapshot").Invoke(stage, null), Is.SameAs(snapshot));
            snapshotType.GetField("track").SetValue(invalid, 2);
            snapshotType.GetField("elapsed").SetValue(invalid, double.NaN);
            type.GetMethod("RestoreSnapshot").Invoke(stage, new[] { invalid });
            Assert.That(type.GetMethod("CaptureSnapshot").Invoke(stage, null), Is.SameAs(snapshot));
            snapshotType.GetField("elapsed").SetValue(invalid, 0d);
            snapshotType.GetField("cooldown").SetValue(invalid, 6f);
            type.GetMethod("RestoreSnapshot").Invoke(stage, new[] { invalid });
            Assert.That(type.GetMethod("CaptureSnapshot").Invoke(stage, null), Is.SameAs(snapshot));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
