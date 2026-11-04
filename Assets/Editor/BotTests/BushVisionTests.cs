using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class BushVisionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> created = new();
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private GameObject Node(string name)
    {
        var root = new GameObject(name); created.Add(root); return root;
    }
    private Component Observer(string name, Vector3 position)
    {
        var root = Node(name); root.transform.position = position;
        root.AddComponent<BoxCollider>();
        return root.AddComponent(T("Vision"));
    }
    private BoxCollider Grass(GameObject root, string name, Vector3 center, Vector3 size)
    {
        var leaf = Node(name); leaf.transform.SetParent(root.transform); leaf.transform.position = center;
        var collider = leaf.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = size;
        var bush = leaf.AddComponent(T("BushCollider")); Set(bush, "bushRoot", root);
        return collider;
    }
    private Component Controller(Component vision)
    {
        var child = Node("vision controller"); child.transform.SetParent(vision.transform, false);
        var controller = child.AddComponent(T("VisionController")); Call(controller, "Awake"); return controller;
    }

    [TearDown]
    public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void LeavingOneOfTwoSharedRootCollidersDoesNotClearBushOrNotifyAgain()
    {
        var root = Node("shared L bush");
        var first = Grass(root, "horizontal arm", Vector3.zero, new Vector3(5, 2, 2));
        var second = Grass(root, "vertical arm", Vector3.zero, new Vector3(2, 2, 5));
        var vision = Observer("occupant", Vector3.zero);
        int changes = 0; Action<GameObject> callback = _ => changes++;
        vision.GetType().GetEvent("OnBushChanged").AddEventHandler(vision, callback);
        Call(vision, "OnTriggerEnter", first); Call(vision, "OnTriggerEnter", second);
        Call(vision, "OnTriggerStay", second);
        vision.transform.position = Vector3.forward * 2;
        Call(vision, "OnTriggerExit", first);
        Assert.That(Get(vision, "CurrentBush"), Is.SameAs(root));
        Assert.That(changes, Is.EqualTo(1));
        vision.transform.position = Vector3.forward * 5;
        Call(vision, "OnTriggerExit", second);
        Assert.That(Get(vision, "IsInBush"), Is.False);
        Assert.That(changes, Is.EqualTo(2));
    }

    [Test]
    public void OpponentsOnDifferentArmsOfAnLShapedBushSeeEachOther()
    {
        var root = Node("L bush");
        var first = Grass(root, "horizontal", Vector3.right * 2, new Vector3(6, 2, 2));
        var second = Grass(root, "vertical", Vector3.forward * 2, new Vector3(2, 2, 6));
        var a = Observer("first arm occupant", Vector3.right * 3);
        var b = Observer("second arm occupant", Vector3.forward * 3);
        Call(a, "OnTriggerEnter", first); Call(b, "OnTriggerEnter", second);
        Assert.That(Call(Controller(a), "CanSee", b), Is.True);
        Assert.That(Call(Controller(b), "CanSee", a), Is.True);
    }

    [Test]
    public void VisibilityUsesAllOccupiedRootsNotTheLastTriggerOrRepresentative()
    {
        var aRoot = Node("A bush"); var bRoot = Node("B bush");
        var aGrass = Grass(aRoot, "A", Vector3.zero, Vector3.one * 4);
        var bGrass = Grass(bRoot, "B", Vector3.right * 2, Vector3.one * 4);
        var a = Observer("both roots", Vector3.zero); var b = Observer("B only", Vector3.zero);
        Call(a, "OnTriggerEnter", aGrass); Call(a, "OnTriggerEnter", bGrass);
        Call(b, "OnTriggerEnter", bGrass);
        Assert.That(Get(a, "CurrentBush"), Is.SameAs(aRoot));
        Assert.That(Get(b, "CurrentBush"), Is.SameAs(bRoot));
        var controller = Controller(a);
        Assert.That(Call(controller, "CanSee", b), Is.True);
        a.transform.position = Vector3.left;
        Call(a, "OnTriggerExit", bGrass);
        Assert.That(Call(controller, "CanSee", b), Is.False, "Separate bush roots must stay separate.");
    }

    [Test]
    public void AuxiliaryTriggerExitsCannotClearBodyMembershipAndInvisibilityStillTracksGrass()
    {
        var root = Node("bush"); var grass = Grass(root, "grass", Vector3.zero, Vector3.one * 4);
        var vision = Observer("invisible occupant", Vector3.zero);
        vision.GetType().GetProperty("IsVisiblyEligible").SetValue(vision, false);
        Call(vision, "OnTriggerEnter", grass);
        Assert.That(Get(vision, "IsInBush"), Is.True);
        Call(vision, "OnTriggerExit", grass);
        Assert.That(Get(vision, "IsInBush"), Is.True, "The body is still inside, even if a secondary trigger exited.");
        Physics.SyncTransforms(); Call(vision, "RefreshBushOverlaps");
        Assert.That(Get(vision, "IsInBush"), Is.True);
    }

    [Test]
    public void DisabledDestroyedAndTeleportedOverlapsCannotLeaveStaleMembership()
    {
        var root = Node("bush"); var grass = Grass(root, "grass", Vector3.zero, Vector3.one * 4);
        var vision = Observer("occupant", Vector3.zero);
        Call(vision, "OnTriggerEnter", grass); grass.enabled = false;
        Call(vision, "PruneBushOverlaps"); Assert.That(Get(vision, "IsInBush"), Is.False);
        grass.enabled = true; Call(vision, "OnTriggerStay", grass);
        Assert.That(Get(vision, "IsInBush"), Is.True);
        Object.DestroyImmediate(grass.gameObject); Call(vision, "PruneBushOverlaps");
        Assert.That(Get(vision, "IsInBush"), Is.False);
        grass = Grass(root, "replacement", Vector3.zero, Vector3.one * 4);
        Call(vision, "OnTriggerEnter", grass);
        vision.transform.position = Vector3.right * 100;
        Call(vision, "PruneBushOverlaps"); Assert.That(Get(vision, "IsInBush"), Is.False);
        vision.transform.position = Vector3.zero; Physics.SyncTransforms();
        Call(vision, "RefreshBushOverlaps"); Assert.That(Get(vision, "IsInBush"), Is.True);
        Call(vision, "ClearTransientState"); Assert.That(Get(vision, "IsInBush"), Is.False);
        Call(vision, "OnTriggerStay", grass); Assert.That(Get(vision, "IsInBush"), Is.True);
    }

    [Test]
    public void RefreshGrowsItsQueryBufferAndSupportsCharacterControllers()
    {
        var root = Node("many grass pieces");
        for (int i = 0; i < 24; i++) Grass(root, "piece " + i, Vector3.zero, Vector3.one * 4);
        var occupant = Node("character controller occupant"); occupant.AddComponent<CharacterController>();
        var vision = occupant.AddComponent(T("Vision"));
        Physics.SyncTransforms(); Call(vision, "RefreshBushOverlaps");
        Assert.That(Get(vision, "CurrentBush"), Is.SameAs(root));
        var tracked = (System.Collections.IDictionary)vision.GetType().GetField("bushOverlaps", Private).GetValue(vision);
        Assert.That(tracked.Count, Is.EqualTo(24));
    }

    [Test]
    public void TheiaOverlappingGrassVolumesUseSharedRoots()
    {
        const string path = "Assets/Scenes/TheiaSkyRuinsMap.unity";
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Physics.SyncTransforms();
            var bushes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(T("BushCollider"), false)).ToArray();
            Assert.That(bushes.Length, Is.GreaterThan(20));
            int overlaps = 0;
            for (int i = 0; i < bushes.Length; i++)
            for (int j = i + 1; j < bushes.Length; j++)
            {
                var a = bushes[i].GetComponent<Collider>(); var b = bushes[j].GetComponent<Collider>();
                if (a == null || b == null || !a.enabled || !b.enabled || !a.bounds.Intersects(b.bounds)
                    || !Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation, out _, out float depth) || depth < 0.01f) continue;
                overlaps++;
                Assert.That(Get(bushes[i], "BushRoot"), Is.SameAs(Get(bushes[j], "BushRoot")),
                    bushes[i].transform.parent.name + " and " + bushes[j].transform.parent.name + " overlap but have different bush roots.");
            }
            Assert.That(overlaps, Is.GreaterThan(0), "Exercise the real multi-part grass patches, not just isolated bushes.");

            var multipart = bushes.GroupBy(b => Get(b, "BushRoot")).First(group => group.Count() > 1).ToArray();
            var occupants = new List<Component>();
            foreach (var bush in multipart)
            {
                var root = Node("real grass character occupant");
                root.transform.position = bush.GetComponent<Collider>().bounds.center;
                root.AddComponent<CharacterController>();
                var vision = root.AddComponent(T("Vision"));
                Physics.SyncTransforms(); Call(vision, "RefreshBushOverlaps");
                Assert.That(Get(vision, "CurrentBush"), Is.SameAs(Get(bush, "BushRoot")));
                occupants.Add(vision);
            }
            Assert.That(Call(occupants[0], "SharesBushWith", occupants[1]), Is.True,
                "Real character bodies on different mesh pieces must recognize the same patch.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
