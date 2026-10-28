using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit, idempotent authoring command. Never runs automatically or replaces a
// scene: only creates missing controls/barriers and updates their own bindings.
public static class BotSceneSetup
{
    public static void ConfigureLobbyButtons()
    {
        WithScene("Assets/Scenes/LobbyScene.unity", scene =>
        {
            var ui = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PartyScreenUI>(true)).Single();
            var serialized = new SerializedObject(ui);
            var template = (Button)serialized.FindProperty("startGameButton").objectReferenceValue;
            SetButton(serialized, "addBotsButton", template, "AddBotsButton", "Add Bots", new Vector2(-90, 89));
            SetButton(serialized, "clearBotsButton", template, "ClearBotsButton", "Clear Bots", new Vector2(150, 89));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    private static void SetButton(SerializedObject serialized, string field, Button template, string name, string label, Vector2 position)
    {
        var button = serialized.FindProperty(field).objectReferenceValue as Button;
        if (button == null)
        {
            var existing = template.transform.parent.Find(name);
            button = existing != null ? existing.GetComponent<Button>() : null;
        }
        if (button == null)
        {
            button = UnityEngine.Object.Instantiate(template, template.transform.parent);
            button.name = name;
            // Cloning must not retain the start-match persistent listener.
            button.onClick = new Button.ButtonClickedEvent();
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(220, 82);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                text.fontSize = text.fontSizeMax = 32;
                text.fontSizeMin = 24;
                text.enableAutoSizing = true;
            }
            button.gameObject.SetActive(true);
        }
        serialized.FindProperty(field).objectReferenceValue = button;
    }

    public static void ConfigureTheiaBarriers()
    {
        WithScene("Assets/Scenes/TheiaSkyRuins.unity", scene =>
        {
            var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var goals = transforms.Select(t => t.GetComponent<GoalZone>()).Where(goal => goal != null).ToArray();
            var rayquaza = transforms.Select(t => t.GetComponent<WildPokemonSpawner>()).Where(spawner => spawner != null)
                .Single(spawner => (AvailableWildPokemons)new SerializedObject(spawner).FindProperty("wildPokemonID").enumValueIndex == AvailableWildPokemons.Rayquaza);
            ConfigureBarrier(transforms.Single(t => t.name == "pref_sm_citysky_mid_lift01_lv3").gameObject,
                goals.Single(goal => goal.Team == Team.Blue && goal.GoalTier == 0), null);
            ConfigureBarrier(transforms.Single(t => t.name == "pref_sm_citysky_mid_lift01_lv3Red").gameObject,
                goals.Single(goal => goal.Team == Team.Orange && goal.GoalTier == 0), null);
            var centralWall = transforms.Single(t => t.name == "pref_sm_citysky_mid_lift_lv3").gameObject;
            // This model was authored in its lowered pose. Its closed pose must
            // obstruct humans as well as AI, not merely carve an invisible wall.
            var centerColliders = centralWall.GetComponentsInChildren<Collider>(true).Where(collider => !collider.isTrigger).ToArray();
            centralWall.transform.position += Vector3.up * (1.8f - centerColliders.Max(collider => collider.bounds.max.y));
            Physics.SyncTransforms();
            ConfigureBarrier(centralWall, null, rayquaza);
        });
    }

    private static void ConfigureBarrier(GameObject root, GoalZone goal, WildPokemonSpawner objective)
    {
        var colliders = root.GetComponentsInChildren<Collider>(true).Where(collider => !collider.isTrigger).ToArray();
        if (colliders.Length == 0) throw new InvalidOperationException(root.name + " has no solid barrier collider.");
        var bounds = colliders[0].bounds;
        foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
        var navigation = root.transform.Find("BotNavigationObstacle");
        if (navigation == null)
        {
            navigation = new GameObject("BotNavigationObstacle").transform;
            navigation.SetParent(root.transform, false);
        }
        navigation.position = new Vector3(bounds.center.x, Mathf.Max(0.5f, bounds.center.y), bounds.center.z);
        navigation.rotation = Quaternion.identity;
        var scale = root.transform.lossyScale;
        navigation.localScale = new Vector3(1 / Mathf.Abs(scale.x), 1 / Mathf.Abs(scale.y), 1 / Mathf.Abs(scale.z));
        var obstacle = navigation.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = navigation.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = new Vector3(bounds.size.x + 1, Mathf.Max(4, bounds.size.y + 2), bounds.size.z + 1);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.carvingTimeToStationary = 0;
        var barrier = root.GetComponent<DynamicMapBarrier>();
        if (barrier == null) barrier = root.AddComponent<DynamicMapBarrier>();
        var data = new SerializedObject(barrier);
        data.FindProperty("opensWhenGoalActive").objectReferenceValue = goal;
        data.FindProperty("blocksWhileObjectiveAlive").objectReferenceValue = objective;
        data.FindProperty("navigationObstacle").objectReferenceValue = obstacle;
        SetReferences(data, "blockingColliders", colliders);
        SetReferences(data, "wallRenderers", root.GetComponentsInChildren<Renderer>(true));
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReferences(SerializedObject target, string field, UnityEngine.Object[] values)
    {
        var property = target.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    internal static void WithScene(string path, Action<Scene> configure)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author scene changes outside Play mode.");
        var scene = SceneManager.GetSceneByPath(path);
        bool wasLoaded = scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("Save your existing edits before configuring " + path);
        try
        {
            configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
