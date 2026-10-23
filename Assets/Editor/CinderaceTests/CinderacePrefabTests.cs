using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CinderacePrefabTests
{
    private const string Folder = "Assets/Prefabs/Objects/Moves/Cinderace/";
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    [TestCase("Ember")]
    [TestCase("LowSweep")]
    [TestCase("PyroBall")]
    [TestCase("BlazeKick")]
    [TestCase("FlameCharge")]
    [TestCase("Feint")]
    [TestCase("Unite")]
    [TestCase("Basic")]
    [TestCase("Cinders")]
    [TestCase("Burn")]
    [TestCase("Blaze")]
    public void CastPrefabsHaveAuthoredGraphicsHitboxesAndBothRegistrations(string kind)
    {
        string path = Folder + "Cinder" + kind + "Object.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), Is.True);
        var component = prefab.GetComponent(GameType("CinderMoveObject")); Assert.That(component, Is.Not.Null);
        var data = new SerializedObject(component);
        foreach (string field in new[] { "graphics", "travel", "impact", "hitbox", "explosionArea" }) Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(data.FindProperty("kind").intValue, Is.EqualTo(Convert.ToInt32(Enum.Parse(GameType("CinderCast"), kind))));
        var network = prefab.GetComponent(GameType("Unity.Netcode.NetworkObject")); Assert.That(network, Is.Not.Null);
        long hash = new SerializedObject(network).FindProperty("GlobalObjectIdHash").longValue; Assert.That(hash, Is.Not.Zero);
        Assert.That(File.ReadAllText(path), Does.Contain("GlobalObjectIdHash: " + hash), "NGO hash must be persisted for player builds.");
        Assert.That(prefab.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform")), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
        Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
        { Assert.That(collider.enabled, Is.False); Assert.That(collider.isTrigger, Is.True); }
        foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.Not.Empty);
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true)) foreach (Material material in renderer.sharedMaterials)
        { Assert.That(material, Is.Not.Null); Assert.That(material.shader, Is.Not.Null); Assert.That(material.shader.name, Does.Not.Contain("Error")); }
        var entries = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, entries.arraySize).Count(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.EqualTo(1));
        string guid = AssetDatabase.AssetPathToGUID(path);
        entries = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset")).FindProperty("m_SerializeEntries");
        int found = 0;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i); if (entry.FindPropertyRelative("m_GUID").stringValue != guid) continue;
            found++; Assert.That(entry.FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path));
        }
        Assert.That(found, Is.EqualTo(1));
    }
    [Test]
    public void PrefabSpawnHashesAreUnique()
    {
        long[] hashes = Enum.GetNames(GameType("CinderCast")).Select(kind => new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Cinder" + kind + "Object.prefab")
            .GetComponent(GameType("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue).ToArray();
        Assert.That(hashes, Is.Unique);
    }
    [Test]
    public void MarksHaveFourAuthoredPipsAndUniteHasAnExplosionArea()
    {
        var marks = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CinderCindersObject.prefab").GetComponent(GameType("CinderMoveObject")));
        Assert.That(marks.FindProperty("markPips").arraySize, Is.EqualTo(4));
        var unite = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CinderUniteObject.prefab").GetComponent(GameType("CinderMoveObject")));
        Assert.That(((SphereCollider)unite.FindProperty("explosionArea").objectReferenceValue).radius, Is.EqualTo(3f));
    }
    [Test]
    public void RuntimeDoesNotGenerateMeshesPrimitivesOrLineRenderers()
    {
        foreach (string path in Directory.GetFiles("Assets/Scripts/Moves/Cinderace", "*.cs"))
            foreach (string pattern in new[] { "new GameObject(", "CreatePrimitive(", "new Mesh", "AddComponent<LineRenderer>" }) Assert.That(File.ReadAllText(path), Does.Not.Contain(pattern), path);
    }
    [Test]
    public void ExistingCinderaceControllerSuppliesAllAnimationStates()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Cinderace/Cinderace.controller");
        string[] states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        foreach (string state in new[] { "ani_spell1_bat_0815", "ani_spell1a_bat_0815", "ani_spell1b1_bat_0815", "ani_spell1b2_bat_0815", "ani_spell2_bat_0815",
            "ani_spell2a_bat_0815", "ani_spell2b_bat_0815", "ani_spellu_bat_0815", "ani_atk1_bat_0815", "ani_atk2_bat_0815", "ani_atk3_bat_0815" })
            Assert.That(states, Does.Contain(state));
    }
    [Test]
    public void RebuildingPreservesExistingPrefabLayouts()
    {
        string[] paths = Enum.GetNames(GameType("CinderCast")).Select(kind => Folder + "Cinder" + kind + "Object.prefab").ToArray();
        string[] before = paths.Select(File.ReadAllText).ToArray();
        GameType("CinderacePrefabBuilder").GetMethod("Build").Invoke(null, null);
        for (int i = 0; i < paths.Length; i++) Assert.That(File.ReadAllText(paths[i]), Is.EqualTo(before[i]), paths[i]);
    }
}
