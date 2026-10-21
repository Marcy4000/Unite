using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DuraludonPrefabTests
{
    private const string Folder = "Assets/Prefabs/Objects/Moves/Duraludon/";
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("DuraLaserFocusObject")]
    [TestCase("DuraDragonPulseObject")]
    [TestCase("DuraDragonTailObject")]
    [TestCase("DuraStealthRockObject")]
    [TestCase("DuraludonUniteObject")]
    [TestCase("DuraFlashCannonObject", "DuraFlashCannonObj")]
    [TestCase("DuraMetalClawProjectile")]
    public void EveryMoveHasAnAuthoredRegisteredNetworkPrefab(string name, string scriptName = null)
    {
        string path = Folder + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        Assert.That(prefab.GetComponent(GameType(scriptName ?? name)), Is.Not.Null);
        var network = prefab.GetComponent(GameType("Unity.Netcode.NetworkObject"));
        Assert.That(network, Is.Not.Null);
        Assert.That(new SerializedObject(network).FindProperty("GlobalObjectIdHash").longValue, Is.Not.Zero,
            "A zero spawn hash prevents remote clients from spawning the prefab.");
        if (prefab.GetComponent(GameType("DuraMoveObject")) != null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path), @"(?m)^  GlobalObjectIdHash: (\d+)");
            Assert.That(match.Success, Is.True);
            Assert.That(Convert.ToInt64(match.Groups[1].Value), Is.EqualTo(new SerializedObject(network).FindProperty("GlobalObjectIdHash").longValue),
                "The validated spawn hash must be persisted in the prefab, not just cached by the editor.");
        }
        Assert.That(prefab.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform")), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
        Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), Is.True);

        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset"));
        var entries = registry.FindProperty("List");
        int matches = 0;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) matches++;
        Assert.That(matches, Is.EqualTo(1), "Network prefab must be registered exactly once.");

        string guid = AssetDatabase.AssetPathToGUID(path);
        var group = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset"));
        entries = group.FindProperty("m_SerializeEntries");
        matches = 0;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("m_GUID").stringValue != guid) continue;
            matches++;
            Assert.That(entry.FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path));
        }
        Assert.That(matches, Is.EqualTo(1), "Addressable key must match the runtime cast path.");
    }

    [TestCase("LaserFocus", "DuraLaserFocusObject")]
    [TestCase("DragonPulse", "DuraDragonPulseObject")]
    [TestCase("DragonTail", "DuraDragonTailObject")]
    [TestCase("StealthRock", "DuraStealthRockObject")]
    [TestCase("RevolvingRuin", "DuraludonUniteObject")]
    public void SpawnPathsMatchTheAuthoredAssets(string effect, string prefab)
    {
        object value = Enum.Parse(GameType("DuraMoveEffect"), effect);
        Assert.That(GameType("DuraMovePrefabPaths").GetMethod("Get").Invoke(null, new[] { value }), Is.EqualTo(Folder + prefab + ".prefab"));
    }

    [TestCase("DuraLaserFocusObject", "shielding", "focus")]
    [TestCase("DuraDragonPulseObject", "pulseArea", "pulseWave")]
    [TestCase("DuraDragonTailObject", "tailArea", "tailSweep")]
    [TestCase("DuraStealthRockObject", "rockArea", "projectile", "hiddenField", "triggeredField")]
    [TestCase("DuraludonUniteObject", "sweepArea", "ringArea", "beamPivot", "beam", "burningRing", "explosion")]
    public void NewPrefabHitboxesAndPresentationReferencesAreWired(string name, params string[] fields)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
        var serialized = new SerializedObject(prefab.GetComponent(GameType(name)));
        Assert.That(serialized.FindProperty("graphics").objectReferenceValue, Is.Not.Null);
        foreach (string field in fields) Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
        {
            Assert.That(collider.isTrigger, Is.True);
            Assert.That(collider.enabled, Is.False, "Hitboxes are queried explicitly, not physical obstructions.");
        }
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.Not.Empty, "Mesh must be baked to an asset or a Unity primitive.");
        }
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader, Is.Not.Null);
                Assert.That(material.shader.name, Does.Not.Contain("Error"));
            }
    }

    [Test]
    public void MarksUseAPrefabWithFiveOrbsAndASeparateFinalRing()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "DuraDragonPulseMarks.prefab");
        var view = prefab.GetComponent(GameType("DuraDragonPulseMarkView"));
        var serialized = new SerializedObject(view);
        var orbs = serialized.FindProperty("orbs");
        Assert.That(orbs.arraySize, Is.EqualTo(5));
        for (int i = 0; i < orbs.arraySize; i++) Assert.That(orbs.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("finalRing").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.GetComponent(GameType("Unity.Netcode.NetworkObject")), Is.Null, "Marks are local owner-only decoration.");
        var boosted = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/BasicAtk/DuraludonBoostedAtk.prefab");
        var helper = new SerializedObject(boosted.GetComponent(GameType("DuraBoostedObj")));
        Assert.That(helper.FindProperty("pulseMarkPrefab").objectReferenceValue, Is.EqualTo(view));
    }

    [TestCase(0f, 3f)]
    [TestCase(0.5f, 5.25f)]
    [TestCase(1f, 7.5f)]
    public void PulsePrefabHitboxTracksCharge(float charge, float expectedRadius)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Folder + "DuraDragonPulseObject.prefab");
        try
        {
            Component pulse = root.GetComponent(GameType("DuraDragonPulseObject"));
            GameType("DuraMoveObject").GetProperty("Power", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pulse, charge);
            pulse.GetType().GetMethod("Configure", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pulse, null);
            var area = (SphereCollider)new SerializedObject(pulse).FindProperty("pulseArea").objectReferenceValue;
            Assert.That(area.radius, Is.EqualTo(expectedRadius));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void RuntimeMoveScriptsDoNotGenerateTheirOwnGeometry()
    {
        foreach (string path in Directory.GetFiles("Assets/Scripts/Moves/Duraludon", "*.cs"))
        {
            string source = File.ReadAllText(path);
            foreach (string pattern in new[] { "new GameObject(", "CreatePrimitive(", "AddComponent<LineRenderer>", "new Mesh ", "DrawCircle(" })
                Assert.That(source, Does.Not.Contain(pattern), path);
        }
    }

    [Test]
    public void CastPrefabsHaveDistinctSpawnHashes()
    {
        string[] names = { "DuraLaserFocusObject", "DuraDragonPulseObject", "DuraDragonTailObject", "DuraStealthRockObject", "DuraludonUniteObject" };
        var hashes = names.Select(name =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            return new SerializedObject(prefab.GetComponent(GameType("Unity.Netcode.NetworkObject")))
                .FindProperty("GlobalObjectIdHash").longValue;
        }).ToArray();
        Assert.That(hashes, Is.Unique);
        Assert.That(hashes.All(hash => hash != 0), Is.True);
    }
}
