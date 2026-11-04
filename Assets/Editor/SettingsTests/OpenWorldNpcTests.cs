using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class OpenWorldNpcTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("OpenWorldTrainerNpc", 0, 1)]
    [TestCase("OpenWorldPokemonNpc", 1, 0)]
    public void PrefabsHaveVisualsAnimationsAndUsablePrompts(string name, int kind, int interaction)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Lobby/{name}.prefab");
        Assert.That(prefab, Is.Not.Null);
        var npc = prefab.GetComponent(GameType("OpenWorldNpc"));
        Assert.That(npc, Is.Not.Null);
        var data = new SerializedObject(npc);
        Assert.That(data.FindProperty("kind").enumValueIndex, Is.EqualTo(kind));
        Assert.That(data.FindProperty("interaction").enumValueIndex, Is.EqualTo(interaction));
        var visual = (GameObject)data.FindProperty("visual").objectReferenceValue;
        Assert.That(visual, Is.Not.Null);
        Assert.That(visual.transform.IsChildOf(prefab.transform), Is.True);
        Assert.That(visual.GetComponentInChildren<Animator>(true), Is.Not.Null);
        foreach (string field in new[] { "idleAnimation", "interactAnimation" })
        {
            var clip = (AnimationClip)data.FindProperty(field).objectReferenceValue;
            Assert.That(clip, Is.Not.Null, field);
            Assert.That(clip.length, Is.GreaterThan(0f), field);
            Assert.That(clip.legacy, Is.False, field);
        }
        Assert.That(data.FindProperty("playTrainerAnimation").boolValue, Is.False);
        Assert.That(data.FindProperty("animationBlendDuration").floatValue, Is.EqualTo(0.2f));
        Assert.That(data.FindProperty("maleTrainerAnimation").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("femaleTrainerAnimation").objectReferenceValue, Is.Not.Null);
        var canvas = (Canvas)data.FindProperty("prompt").objectReferenceValue;
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
        var button = (Button)data.FindProperty("interactButton").objectReferenceValue;
        Assert.That(button.transform.IsChildOf(canvas.transform), Is.True);
        var label = (TMP_Text)data.FindProperty("promptText").objectReferenceValue;
        Assert.That(label.font, Is.Not.Null);
        Assert.That(label.raycastTarget, Is.False);
        Assert.That(prefab.GetComponent<CapsuleCollider>().isTrigger, Is.False);
        Assert.That(prefab.GetComponents<Component>().Any(component => component != null && component.GetType().Name == "NetworkObject"), Is.False);
        if (kind == 0)
        {
            Assert.That(visual.GetComponent(GameType("TrainerModel")), Is.Not.Null);
            var clothes = data.FindProperty("clothes").stringValue;
            Assert.DoesNotThrow(() => GameType("PlayerClothesInfo").GetMethod("Deserialize").Invoke(null, new object[] { clothes }));
            Assert.That(data.FindProperty("dialogues").arraySize, Is.GreaterThan(0));
            Assert.That(data.FindProperty("portraitMode").enumValueIndex, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GenericSupportsDialogueWithoutAnAnimator(bool hasStaticVisual)
    {
        var root = new GameObject("Generic NPC");
        root.SetActive(false);
        try
        {
            var npc = root.AddComponent(GameType("OpenWorldNpc"));
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            npc.GetType().GetField("kind", flags).SetValue(npc, Enum.ToObject(GameType("OpenWorldNpcKind"), 2));
            npc.GetType().GetField("interaction", flags).SetValue(npc, Enum.ToObject(GameType("OpenWorldNpcInteraction"), 1));
            if (hasStaticVisual)
            {
                var visual = new GameObject("Static visual");
                visual.transform.SetParent(root.transform, false);
                npc.GetType().GetField("visual", flags).SetValue(npc, visual);
            }
            npc.GetType().GetMethod("Start", flags).Invoke(npc, null);
            npc.GetType().GetMethod("Update", flags).Invoke(npc, null);
            Assert.That(npc.GetType().GetProperty("IsReady").GetValue(npc), Is.True);
            Assert.That(npc.GetType().GetProperty("Interaction").GetValue(npc).ToString(), Is.EqualTo("Dialogue"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void GenericPrefabHasDialogueAndPromptWithoutAModelOrPhysicalObstacle()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/OpenWorldGenericNpc.prefab");
        Assert.That(prefab, Is.Not.Null);
        var data = new SerializedObject(prefab.GetComponent(GameType("OpenWorldNpc")));
        Assert.That(data.FindProperty("kind").enumValueIndex, Is.EqualTo(2));
        Assert.That(data.FindProperty("visual").objectReferenceValue, Is.Null);
        Assert.That(data.FindProperty("interaction").enumValueIndex, Is.EqualTo(1));
        Assert.That(data.FindProperty("dialogues").arraySize, Is.GreaterThan(0));
        Assert.That(data.FindProperty("prompt").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("interactButton").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.GetComponent<Collider>().enabled, Is.False);
    }

    [Test]
    public void PokemonCannotEnterDialogueEvenWhenSerializedModeIsDialogue()
    {
        var root = new GameObject("Pokemon NPC constraint");
        try
        {
            var npc = root.AddComponent(GameType("OpenWorldNpc"));
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            npc.GetType().GetField("kind", flags).SetValue(npc, Enum.ToObject(GameType("OpenWorldNpcKind"), 1));
            npc.GetType().GetField("interaction", flags).SetValue(npc, Enum.ToObject(GameType("OpenWorldNpcInteraction"), 1));
            Assert.That(npc.GetType().GetProperty("Interaction").GetValue(npc).ToString(), Is.EqualTo("Animation"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void DialogueTriggersKeepLegacyProfessorDefault()
    {
        var root = new GameObject("Dialogue trigger defaults");
        try
        {
            var trigger = root.AddComponent(GameType("DialogueTrigger"));
            var data = new SerializedObject(trigger);
            Assert.That(data.FindProperty("portraitMode").enumValueIndex, Is.Zero);
            Assert.That(data.FindProperty("portraitPrefab").objectReferenceValue, Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void CityContainsBothNpcPrefabInstances()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/ChineseLobby.unity");
        try
        {
            var group = scene.GetRootGameObjects().Single(root => root.name == "Lobby NPCs");
            var npcs = group.GetComponentsInChildren(GameType("OpenWorldNpc"));
            var kinds = npcs.Select(npc => new SerializedObject(npc).FindProperty("kind").enumValueIndex).ToArray();
            Assert.That(kinds, Does.Contain(0), "The city should include a Trainer NPC.");
            Assert.That(kinds, Does.Contain(1), "The city should include a Pokémon NPC.");
            foreach (var npc in npcs)
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(npc), Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void DialoguePortraitCanHideReplaceAndRestoreProfessor()
    {
        var root = new GameObject("Dialogue portrait options");
        root.SetActive(false);
        try
        {
            var dialogue = root.AddComponent(GameType("DialogueSystem"));
            var portraitRoot = new GameObject("Portrait");
            portraitRoot.transform.SetParent(root.transform, false);
            var portrait = portraitRoot.AddComponent(GameType("UI.ThreeDimensional.UIObject3D"));
            var original = new GameObject("Professor");
            original.transform.SetParent(root.transform, false);
            var custom = new GameObject("Custom speaker");
            custom.transform.SetParent(root.transform, false);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            dialogue.GetType().GetField("professor", flags).SetValue(dialogue, portrait);
            dialogue.GetType().GetField("defaultPortrait", flags).SetValue(dialogue, original.transform);
            var configure = dialogue.GetType().GetMethod("ConfigurePortrait", flags);
            var mode = GameType("DialoguePortraitMode");
            configure.Invoke(dialogue, new object[] { Enum.ToObject(mode, 1), null });
            Assert.That(portraitRoot.activeSelf, Is.False);
            configure.Invoke(dialogue, new object[] { Enum.ToObject(mode, 2), custom });
            Assert.That(portraitRoot.activeSelf, Is.True);
            Assert.That(portrait.GetType().GetProperty("ObjectPrefab").GetValue(portrait), Is.SameAs(custom.transform));
            configure.Invoke(dialogue, new object[] { Enum.ToObject(mode, 0), null });
            Assert.That(portraitRoot.activeSelf, Is.True);
            Assert.That(portrait.GetType().GetProperty("ObjectPrefab").GetValue(portrait), Is.SameAs(original.transform));
            configure.Invoke(dialogue, new object[] { Enum.ToObject(mode, 2), null });
            Assert.That(portraitRoot.activeSelf, Is.False, "An unset custom speaker should not show the professor.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
