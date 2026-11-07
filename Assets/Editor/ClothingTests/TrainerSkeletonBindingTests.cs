using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class TrainerSkeletonBindingTests
{
    private Scene scene;
    private GameObject container;
    private GameObject trainer;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, Flags).Invoke(target, args);
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        container = new GameObject("Skeleton binding test");
        container.SetActive(false);
        SceneManager.MoveGameObjectToScene(container, scene);
        trainer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrainerModels/TrainerModel.prefab"), container.transform);
        foreach (var behaviour in trainer.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) behaviour.enabled = false;
        foreach (var animator in trainer.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
    }

    private Component Sync(bool male) => (Component)Field(trainer.GetComponent(GameType("TrainerModel")), male ? "maleBoneSync" : "femaleBoneSync");
    private Transform Holder(bool male, int slot) => ((Transform[])Field(trainer.GetComponent(GameType("TrainerModel")), male ? "clothingHoldersMale" : "clothingHoldersFemale"))[slot];
    private GameObject Garment(bool male, int slot, string path) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), Holder(male, slot), false);

    // Compare against the original recursive local-pose copy, independently of the binder.
    private static void LegacySync(Transform source, Transform target)
    {
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        foreach (Transform child in source)
        {
            var match = target.Find(child.name);
            if (match != null) LegacySync(child, match);
        }
    }

    [TestCase(true, 7, "Male/Bottom/Lv3/pref_lob_40000bottom_lv3/pref_lob_40000bottom_lv3.fbx", true)]
    [TestCase(false, 1, "Female/Hair/Lv3/pref_lob_50000hair1_lv3/pref_lob_50000hair1_lv3.fbx", true)]
    [TestCase(false, 5, "Female/Outwear/Lv3/pref_lob_50073outerwear_lv3/pref_lob_50073outerwear_lv3.fbx", true)]
    [TestCase(false, 4, "Female/Top/Lv3/pref_lob_50604top_lv3/pref_lob_50604top_lv3.fbx", true)]
    [TestCase(false, 4, "Female/Top/Lv3/pref_lob_50650top_lv3/pref_lob_50650top_lv3.fbx", true)]
    [TestCase(true, 4, "Male/Top/Lv3/pref_lob_40650top_lv3/pref_lob_40650top_lv3.fbx", true)]
    [TestCase(false, 4, "Female/Top/Lv3/pref_lob_50645top_lv3/pref_lob_50645top_lv3.fbx", false)]
    [TestCase(false, 9, "Female/Foot/Lv3/pref_lob_50012foot_lv3/pref_lob_50012foot_lv3.fbx", false)]
    [TestCase(true, 9, "Male/Foot/Lv3/pref_lob_40361foot_lv3/pref_lob_40361foot_lv3.fbx", false)]
    public void BindingPreservesAnimatedGeometryAndOriginalMeshData(bool male, int slot, string model, bool shared)
    {
        string path = "Assets/Model/Clothing/" + model;
        var legacy = Garment(male, slot, path);
        var garment = Garment(male, slot, path);
        var skins = garment.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var oldSkins = legacy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var rigid = garment.GetComponentsInChildren<MeshRenderer>(true);
        var oldRigid = legacy.GetComponentsInChildren<MeshRenderer>(true);
        var meshes = skins.Select(renderer => renderer.sharedMesh).ToArray();
        var bindposes = meshes.Select(mesh => mesh.bindposes).ToArray();
        var bounds = skins.Select(renderer => renderer.localBounds).ToArray();
        var owned = garment.AddComponent(GameType("TrainerClothingInstance"));
        var sync = Sync(male);
        int originalTransforms = garment.GetComponentsInChildren<Transform>(true).Length;
        Call(owned, "BindToSkeleton", sync);
        Assert.That(owned.GetType().GetProperty("UsesSharedSkeleton").GetValue(owned), Is.EqualTo(shared));
        Set(sync, "clothingRoots", new[] { garment.transform.Find("reference") });
        Call(sync, "RebuildSyncData");
        int pairs = (int)sync.GetType().GetProperty("SyncPairCount").GetValue(sync);
        if (shared) Assert.That(pairs, Is.Zero);
        else Assert.That(pairs, Is.GreaterThan(0));
        if (shared) Assert.That(garment.GetComponentsInChildren<Transform>(true).Length, Is.LessThan(originalTransforms));

        // Include rotated, nonuniformly scaled trainer placement (also used by UI previews).
        trainer.transform.SetPositionAndRotation(new Vector3(3f, 2f, -4f), Quaternion.Euler(0f, 43f, 0f));
        trainer.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);
        var skeleton = (Transform)Field(sync, "baseSkeletonRoot");
        var bakedOld = new Mesh();
        var bakedNew = new Mesh();
        try
        {
            foreach (string animation in new[] { "idle", "run", "sitdown", "interactive06a" })
            {
                string gender = male ? "male" : "female";
                string folder = male ? "pref_char_lob_9001_male" : "pref_char_lob_9002_female";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Model/Clothing/{folder}/AnimationsCN/ani_{animation}_lob_{gender}.anim");
                Assert.That(clip, Is.Not.Null);
                foreach (float phase in new[] { 0.13f, 0.61f })
                {
                    clip.SampleAnimation(skeleton.parent.gameObject, clip.length * phase);
                    LegacySync(skeleton, legacy.transform.Find("reference"));
                    Call(sync, "ForceSync");
                    for (int i = 0; i < skins.Length; i++)
                    {
                        oldSkins[i].BakeMesh(bakedOld);
                        skins[i].BakeMesh(bakedNew);
                        var before = bakedOld.vertices;
                        var after = bakedNew.vertices;
                        Assert.That(after.Length, Is.EqualTo(before.Length));
                        float largest = 0f;
                        for (int v = 0; v < before.Length; v++)
                            largest = Mathf.Max(largest, Vector3.Distance(oldSkins[i].transform.TransformPoint(before[v]), skins[i].transform.TransformPoint(after[v])));
                        Assert.That(largest, Is.LessThan(0.0001f), path + " / " + clip.name);
                        var oldNormals = bakedOld.normals;
                        var newNormals = bakedNew.normals;
                        Assert.That(newNormals.Length, Is.EqualTo(oldNormals.Length));
                        float largestNormalDifference = 0f;
                        for (int n = 0; n < oldNormals.Length; n++)
                            largestNormalDifference = Mathf.Max(largestNormalDifference, Vector3.Distance(oldNormals[n], newNormals[n]));
                        Assert.That(largestNormalDifference, Is.LessThan(0.0001f), "Skinned normals changed.");
                        Assert.That(skins[i].sharedMesh, Is.SameAs(meshes[i]));
                        Assert.That(meshes[i].bindposes, Is.EqualTo(bindposes[i]));
                        Assert.That(skins[i].localBounds, Is.EqualTo(bounds[i]));
                    }
                    for (int i = 0; i < rigid.Length; i++)
                        Assert.That(Vector3.Distance(rigid[i].transform.position, oldRigid[i].transform.position), Is.LessThan(0.0001f));
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(bakedOld);
            Object.DestroyImmediate(bakedNew);
        }
    }

    [Test]
    public void CloneOwnsItsBranchesAndCleanupDoesNotDestroyAnotherGarment()
    {
        const string path = "Assets/Model/Clothing/Female/Outwear/Lv3/pref_lob_50073outerwear_lv3/pref_lob_50073outerwear_lv3.fbx";
        var first = Garment(false, 5, path).AddComponent(GameType("TrainerClothingInstance"));
        var second = Garment(false, 5, path).AddComponent(GameType("TrainerClothingInstance"));
        first.name = "First bound garment";
        second.name = "Second bound garment";
        Call(first, "BindToSkeleton", Sync(false));
        Call(second, "BindToSkeleton", Sync(false));
        var firstNodes = (Component[])Field(first, "attachments");
        var secondNodes = (Component[])Field(second, "attachments");
        Assert.That(firstNodes.Length, Is.GreaterThan(0));
        var clone = Object.Instantiate(trainer, container.transform);
        var cloneOwners = clone.GetComponentsInChildren(GameType("TrainerClothingInstance"), true);
        var cloned = cloneOwners.Single(owner => owner.name == first.name);
        Assert.That(((Component[])Field(cloned, "attachments")).All(node => node.transform.IsChildOf(clone.transform)), Is.True);
        Assert.That(((Component[])Field(cloned, "attachments")).All(node =>
            node.GetType().GetProperty("Owner").GetValue(node) == cloned), Is.True);
        var clonedSync = clone.GetComponentsInChildren(GameType("BoneSync"), true)
            .Single(component => component.name == Sync(false).name);
        var originalLookup = (System.Collections.IDictionary)Call(Sync(false), "GetSkeletonLookup");
        var clonedLookup = (System.Collections.IDictionary)Call(clonedSync, "GetSkeletonLookup");
        Assert.That(clonedLookup.Count, Is.EqualTo(originalLookup.Count), "Cloning must not absorb garment extensions into the canonical rig.");
        Call(first, "ReleaseResources");
        Assert.That(firstNodes.All(node => node == null), Is.True);
        Assert.That(secondNodes.All(node => node != null), Is.True);
        Assert.That(((Component[])Field(cloned, "attachments")).All(node => node != null), Is.True);
    }

    [TearDown]
    public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);
}
