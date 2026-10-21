using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class JolteonPrefabTests
{
    private const string Folder = "Assets/Prefabs/Objects/Moves/Jolteon/Rework/";
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("Agility")]
    [TestCase("Charge")]
    [TestCase("ThunderFang")]
    [TestCase("Discharge")]
    [TestCase("Electroweb")]
    [TestCase("WebMark")]
    [TestCase("LightningBlitz")]
    [TestCase("Overcharge")]
    [TestCase("Ready")]
    public void EachEffectHasAuthoredGraphicsAndUniqueRegisteredNetworkIdentity(string name)
    {
        string path = Folder + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        Component effect = prefab.GetComponent(GameType("JoltEffectObject"));
        Assert.That(effect, Is.Not.Null);
        Assert.That(new SerializedObject(effect).FindProperty("graphics").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.Not.Empty, "Meshes must be baked assets.");
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader.name, Does.Not.Contain("Error"));
            }
        var network = prefab.GetComponent(GameType("Unity.Netcode.NetworkObject"));
        long hash = new SerializedObject(network).FindProperty("GlobalObjectIdHash").longValue;
        Assert.That(hash, Is.Not.Zero);
        Assert.That(File.ReadAllText(path), Does.Contain("GlobalObjectIdHash: " + hash));
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset"));
        var list = registry.FindProperty("List");
        int matches = 0;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) matches++;
        Assert.That(matches, Is.EqualTo(1));
        var group = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset"));
        list = group.FindProperty("m_SerializeEntries"); matches = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            var entry = list.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("m_GUID").stringValue != AssetDatabase.AssetPathToGUID(path)) continue;
            matches++;
            Assert.That(entry.FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path));
        }
        Assert.That(matches, Is.EqualTo(1));
    }

    [Test]
    public void WebUsesAnExplicitSweptQueryNotAPhysicalObstacle()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Electroweb.prefab");
        var area = prefab.GetComponent<BoxCollider>();
        Assert.That(area.enabled, Is.False);
        Assert.That(area.isTrigger, Is.True);
        Assert.That(area.size.x, Is.EqualTo(3.5f));
        Assert.That(prefab.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform")), Is.Not.Null);
        var serialized = new SerializedObject(prefab.GetComponent(GameType("JoltEffectObject")));
        Assert.That(serialized.FindProperty("webArea").objectReferenceValue, Is.EqualTo(area));
    }

    [Test]
    public void CastHashesAreDistinct()
    {
        var hashes = Directory.GetFiles(Folder, "*.prefab").Select(path =>
            new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent(GameType("Unity.Netcode.NetworkObject")))
                .FindProperty("GlobalObjectIdHash").longValue).ToArray();
        Assert.That(hashes.Length, Is.EqualTo(9));
        Assert.That(hashes, Is.Unique);
    }

    [Test]
    public void ChargeDoesNotRootOrSpendTheOffensiveMeter()
    {
        string source = File.ReadAllText("Assets/Scripts/Moves/Jolteon/JoltCharge.cs");
        Assert.That(source, Does.Not.Contain("StopMovementForTime"));
        Assert.That(source, Does.Not.Contain("CanMove = false"));
        Assert.That(source, Does.Not.Contain("TryConsumeCharge"));
        Assert.That(source, Does.Not.Contain("ReducePassiveCharge"));
        Assert.That(source, Does.Contain("JoltMoveRules.ChargeSlowDuration"));
    }

    [Test]
    public void NewCastsDoNotConsumeTheLegacySwiftSpawnCallback()
    {
        foreach (string name in new[] { "JoltCharge.cs", "JoltDashMove.cs", "JoltElectroweb.cs", "JolteonUnite.cs" })
            Assert.That(File.ReadAllText("Assets/Scripts/Moves/Jolteon/" + name), Does.Not.Contain("onObjectSpawned"));
    }
}
