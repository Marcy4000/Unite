using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;

public class SharedGameplayRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> created = new();
    private delegate bool Slide(Collider collider, Vector3 from, Vector3 intended, out Vector3 result);
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static void Variable(object target, string name, object value)
    {
        var variable = target.GetType().GetField(name, Private).GetValue(target);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
    private GameObject Node(string name, bool active = true)
    {
        var root = new GameObject(name); root.SetActive(active); created.Add(root); return root;
    }
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void TeleportedVisionCannotRemainRevealedByAStaleOverlap()
    {
        var observer = Node("observer");
        var controller = observer.AddComponent(T("VisionController"));
        Call(controller, "Awake");
        var enemy = Node("enemy");
        var vision = enemy.AddComponent(T("Vision"));
        var body = enemy.AddComponent<BoxCollider>();
        Call(controller, "OnTriggerEnter", body);
        Assert.That(Call(controller, "ContainsVision", vision), Is.True);
        enemy.transform.position = Vector3.right * 100;
        Assert.That(Call(controller, "ContainsVision", vision), Is.False);
        enemy.transform.position = Vector3.right;
        Assert.That(Call(controller, "ContainsVision", vision), Is.True, "Re-entry must not depend on a new physics event.");
        observer.SetActive(false);
        Call(controller, "OnDisable");
        var tracked = controller.GetType().GetField("visibleObjects", Private).GetValue(controller);
        Assert.That(tracked.GetType().GetProperty("Count").GetValue(tracked), Is.EqualTo(0));
    }

    [Test]
    public void UnchangedVisibilityNotifiesOnceButNewlyLoadedGraphicsStillReceiveTheState()
    {
        var root = Node("vision");
        var vision = root.AddComponent(T("Vision"));
        int notifications = 0;
        Action<bool> callback = _ => notifications++;
        vision.GetType().GetEvent("OnVisibilityChanged").AddEventHandler(vision, callback);
        var first = Node("first graphic");
        Call(vision, "AddObject", first);
        notifications = 0;
        Call(vision, "SetVisibility", true);
        for (int i = 0; i < 100; i++) Call(vision, "SetVisibility", true);
        Assert.That(notifications, Is.EqualTo(1));
        var second = Node("loaded graphic"); second.SetActive(false);
        Call(vision, "AddObject", second);
        Assert.That(second.activeSelf, Is.True);
        Assert.That(notifications, Is.EqualTo(1));
        Call(vision, "RevealTemporarily", Enum.Parse(T("RevealType"), "Attack"), 100f);
        Call(vision, "ClearTransientState");
        Assert.That(vision.GetType().GetProperty("TemporarilyRevealed").GetValue(vision), Is.False);
    }

    [TestCase("Active", true, true)]
    [TestCase("Active", false, true)]
    [TestCase("Weakened", true, false)]
    [TestCase("Weakened", false, false)]
    [TestCase("Destroyed", true, false)]
    [TestCase("Destroyed", false, false)]
    public void HealthyGoalsProvideDefenseRegardlessOfScoringActivation(string status, bool active, bool expected)
    {
        var goal = Node("goal", false).AddComponent(T("GoalZone"));
        Variable(goal, "isActive", active);
        Variable(goal, "goalStatus", Enum.Parse(T("GoalStatus"), status));
        Assert.That(goal.GetType().GetProperty("ProvidesDefense").GetValue(goal), Is.EqualTo(expected));
    }

    [Test]
    public void GoalShieldUsesGpuAnimationAndAnIndependentMaterial()
    {
        var shader = Shader.Find("Unite/Goal Zone Shield");
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        var shared = new Material(shader); created.Add(shared);
        var root = Node("shield", false);
        var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = shared;
        var shield = root.AddComponent(T("GoalZoneShield"));
        Set(shield, "shieldRenderer", renderer);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            "Instantiating material due to calling renderer.material during edit mode. This will leak materials into the scene. You most likely want to use renderer.sharedMaterial instead.");
        Call(shield, "Awake");
        var instance = renderer.sharedMaterial; created.Add(instance);
        Assert.That(instance, Is.Not.SameAs(shared));
        Assert.That(instance.GetVector("_ShieldFade").y, Is.Zero);
        Assert.That(instance.GetFloat("_ScrollSpeed"), Is.EqualTo(0.5f));
        root.SetActive(true);
        Color teamColor = new Color(0, 2, 4, 1);
        Call(shield, "SetShieldColor", teamColor);
        Call(shield, "ShowShield");
        Assert.That(instance.GetColor("_EmissionColor"), Is.EqualTo(teamColor));
        var shown = instance.GetVector("_ShieldFade");
        Assert.That(shown.y, Is.EqualTo(1));
        Assert.That(shown.w, Is.EqualTo(1f / 0.3f));
        Call(shield, "ShowShield");
        Assert.That(instance.GetVector("_ShieldFade"), Is.EqualTo(shown));
        Set(shield, "shieldFade", new Vector4(0, 1, Time.time - 0.15f, 1f / 0.3f));
        Call(shield, "HideShield");
        Assert.That(instance.GetVector("_ShieldFade").x, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(instance.GetVector("_ShieldFade").y, Is.Zero);
        Assert.That(shared.GetVector("_ShieldFade"), Is.EqualTo(Vector4.zero));
        Assert.That(shield.GetType().GetMethod("Update", Private), Is.Null);
        Call(shield, "ShowShield");
        root.SetActive(false);
        Call(shield, "OnDisable");
        var hidden = instance.GetVector("_ShieldFade");
        Assert.That(hidden.x, Is.Zero);
        Assert.That(hidden.y, Is.Zero);
    }

    [Test]
    public void GoalPrefabShieldUsesTheAnimatedShaderAndOriginalMask()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GoalZone.prefab");
        var shield = prefab.GetComponentInChildren(T("GoalZoneShield"), true);
        Assert.That(shield, Is.Not.Null);
        var data = new SerializedObject(shield);
        var renderer = (MeshRenderer)data.FindProperty("shieldRenderer").objectReferenceValue;
        var material = renderer.sharedMaterial;
        Assert.That(material.shader.name, Is.EqualTo("Unite/Goal Zone Shield"));
        Assert.That(material.GetTexture("_EmissionMap"), Is.Not.Null);
        Assert.That(material.GetVector("_ShieldFade").y, Is.Zero);
    }

    [TestCase(1, 9, 3, 2, true)]
    [TestCase(6, 4, 3, 2, true)]
    [TestCase(7, 3, 3, 2, false)]
    [TestCase(4, 6, 2, 1, false)]
    public void SmallerMapsCountHumansAndUseTheActualTeamCount(int humans, int bots, int teamSize, int teams, bool expected)
    {
        var lobby = Node("lobby", false).AddComponent(T("LobbyController"));
        var view = new GameSessionView();
        for (int i = 0; i < humans; i++) view.Players.Add(new GameSessionPlayer("human-" + i));
        for (int i = 0; i < bots; i++) view.Players.Add(new GameSessionPlayer("bot-" + i, true));
        Set(lobby, "partyLobby", view);
        var map = ScriptableObject.CreateInstance(T("MapInfo")); created.Add(map);
        map.GetType().GetField("maxTeamSize").SetValue(map, teamSize);
        var available = (IList)map.GetType().GetField("availableTeams").GetValue(map);
        while (available.Count > teams) available.RemoveAt(available.Count - 1);
        Assert.That(Call(lobby, "CanSwitchToMap", map), Is.EqualTo(expected));
    }

    [Test]
    public void GroundPlacementIgnoresTriggersActorsAndGoalGeometry()
    {
        var location = new Vector3(10000, 0, 10000);
        var floor = Node("floor"); floor.transform.position = location + Vector3.down * 0.1f;
        floor.AddComponent<BoxCollider>().size = new Vector3(10, 0.2f, 10);
        var trigger = Node("trigger"); trigger.transform.position = location + Vector3.up;
        trigger.AddComponent<BoxCollider>().isTrigger = true;
        var actor = Node("actor"); actor.transform.position = location + Vector3.up * 2;
        actor.AddComponent<NetworkObject>(); actor.AddComponent<BoxCollider>();
        var goal = Node("goal geometry"); goal.transform.position = location + Vector3.up * 3;
        goal.AddComponent(T("GoalZone")); goal.AddComponent<BoxCollider>();
        Physics.SyncTransforms();
        object[] args = { location, 1, Vector3.zero };
        Assert.That(T("GroundPlacement").GetMethod("TryGetGround").Invoke(null, args), Is.True);
        Assert.That(((Vector3)args[2]).y, Is.EqualTo(0).Within(0.001f));
    }

    [TestCase(0f, 0.6f)]
    [TestCase(37f, 0.6f)]
    [TestCase(0f, 0.004f)]
    public void WalkmeshSlidesAlongRealEdgesIncludingSubCentimeterSteps(float rotation, float along)
    {
        var root = Node("walkmesh");
        root.transform.SetPositionAndRotation(Vector3.down * 30, Quaternion.Euler(0, rotation, 0));
        var mesh = new Mesh { vertices = new[] { new Vector3(-2, 0, -2), new Vector3(2, 0, -2), new Vector3(2, 0, 2), new Vector3(-2, 0, 2) }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        created.Add(mesh);
        var collider = root.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
        Physics.SyncTransforms();
        var from = root.transform.TransformPoint(new Vector3(1.99f, 30, 0));
        var intended = root.transform.TransformPoint(new Vector3(2.1f, 30, along));
        object[] args = { collider, from, intended, Vector3.zero };
        Assert.That(T("WalkmeshBoundary").GetMethod("TrySlide").Invoke(null, args), Is.True);
        var result = root.transform.InverseTransformPoint((Vector3)args[3]);
        Assert.That(result.x, Is.LessThanOrEqualTo(2.001f));
        Assert.That(result.z, Is.EqualTo(along).Within(0.001f));
    }

    [Test]
    public void HiddenAnimatorDoesNotQueryOrPlayAnInactiveController()
    {
        var root = Node("animation", false);
        var animator = root.AddComponent<Animator>();
        var manager = root.AddComponent(T("AnimationManager"));
        Call(manager, "AssignAnimator", animator);
        Call(manager, "PlayLocalAnimation", "unknown cast", 1f, 0f);
        Assert.That(Call(manager, "RecoverLocalAnimation", "unknown cast", "recovery", 0f), Is.True);
        Call(manager, "LateUpdate");
        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void AuthoredWalkmeshesAreReadableInPlayerBuildsAsWellAsTheEditor()
    {
        var models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Model/Navmeshes" });
        Assert.That(models.Length, Is.GreaterThan(0));
        foreach (var guid in models)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(((ModelImporter)AssetImporter.GetAtPath(path)).isReadable, Is.True, path);
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                Assert.That(mesh.isReadable, Is.True, path);
        }
    }

    [Test]
    public void WarmWallSlidingDoesNotAllocateManagedMemoryPerStep()
    {
        var root = Node("allocation walkmesh"); root.transform.position = Vector3.down * 30;
        var mesh = new Mesh { vertices = new[] { new Vector3(-2, 0, -2), new Vector3(2, 0, -2), new Vector3(2, 0, 2), new Vector3(-2, 0, 2) }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        created.Add(mesh);
        var collider = root.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
        Physics.SyncTransforms();
        var slide = (Slide)Delegate.CreateDelegate(typeof(Slide), T("WalkmeshBoundary").GetMethod("TrySlide"));
        Vector3 from = new Vector3(1.99f, 0, 0), intended = new Vector3(2.1f, 0, .004f);
        for (int i = 0; i < 10; i++) slide(collider, from, intended, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) slide(collider, from, intended, out _);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.LessThanOrEqualTo(128), "Geometry/corner buffers must be reused, not rebuilt per frame.");
    }

    [Test]
    public void LeavingResultsBeforeAssetsLoadDoesNotReleaseInvalidHandles()
    {
        var results = Node("early results exit", false).AddComponent(T("SettlementManager"));
        Assert.DoesNotThrow(() => Call(results, "OnDestroy"));
        Assert.DoesNotThrow(() => Call(results, "OnDestroy"), "Cleanup is idempotent.");
        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
    }
}
