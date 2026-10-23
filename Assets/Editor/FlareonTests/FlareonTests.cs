using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class FlareonTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Kind(string name) => Enum.Parse(Type("FlareonCast"), name);
    private static object Rule(string method, params object[] args) => Type("FlareonMoveRules").GetMethod(method).Invoke(null, args);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);

    [TestCase("Swift", "FlareonSwift", 7.5f, 0)]
    [TestCase("Tackle", "FlareonTackle", 6f, 1)]
    [TestCase("HeatWave", "FlareonHeatWave", 9f, 0)]
    [TestCase("Takedown", "FlareonTakedown", 7f, 0)]
    [TestCase("Flamethrower", "FlareonFlamethrower", 7.5f, 1)]
    [TestCase("FlareBlitz", "FlareonFlareBlitz", 7.5f, 1)]
    [TestCase("Unite", "FlareonUnite", 0f, 2)]
    public void FactoriesUseTheNewCastFlowWithoutChangingSerializedEnums(string kind, string className, float cooldown, int slot)
    {
        object enumValue = Rule("Move", Kind(kind));
        object move = Type("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { enumValue });
        Assert.That(move.GetType().Name, Is.EqualTo(className));
        Assert.That(Type("FlareonMoveBase").IsInstanceOfType(move), Is.True);
        Assert.That(Get(move, "Cooldown"), Is.EqualTo(cooldown));
        Assert.That(Rule("Slot", Kind(kind)), Is.EqualTo(slot));
        Assert.DoesNotThrow(() => Call(move, "ResetMove"));
        Assert.DoesNotThrow(() => Call(move, "ResetMove"));
    }
    [Test]
    public void ConnectedBasicsCycleAndOnlyTheThirdConfirmsABoost()
    {
        object state = Activator.CreateInstance(Type("FlareonComboState"));
        Assert.That(Call(state, "ConfirmBasic", 0d), Is.False);
        Assert.That(Call(state, "ConfirmBasic", 1d), Is.False);
        Assert.That(state.GetType().GetProperty("AttackCharge").GetValue(state), Is.EqualTo(2));
        Assert.That(Call(state, "ConfirmBasic", 2d), Is.True);
        Assert.That(state.GetType().GetProperty("AttackCharge").GetValue(state), Is.Zero);
    }
    [Test]
    public void IdleAndDeathClearTheAttackAndHeatState()
    {
        object state = Activator.CreateInstance(Type("FlareonComboState"));
        Call(state, "ConfirmBasic", 0d); Call(state, "AddStack", 0d); Call(state, "Tick", 5d);
        Assert.That(state.GetType().GetProperty("AttackCharge").GetValue(state), Is.Zero);
        Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.Zero);
        Call(state, "ConfirmBasic", 6d); Call(state, "AddStack", 6d); Call(state, "Clear");
        Assert.That(state.GetType().GetProperty("AttackCharge").GetValue(state), Is.Zero);
        Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.Zero);
    }
    [Test]
    public void HeatCapsAtThreeAndQualifyingHitsRefreshItsExpiration()
    {
        object state = Activator.CreateInstance(Type("FlareonComboState"));
        for (int i = 0; i < 6; i++) Call(state, "AddStack", (double)i);
        Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.EqualTo(3));
        Call(state, "Tick", 9.99d); Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.EqualTo(3));
        Call(state, "Tick", 10d); Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.Zero);
    }
    [TestCase(false, 8, 0.75f, 1.2f)]
    [TestCase(true, 10, 0.6f, 1.4f)]
    public void UpgradesHaveDeterministicPulseCountsAndInstanceLocalControlDurations(bool plus, int count, float interval, float stun)
    {
        Assert.That(Rule("HeatPulses", plus), Is.EqualTo(count)); Assert.That(Rule("HeatInterval", plus), Is.EqualTo(interval));
        Assert.That(Rule("LaunchDuration", plus), Is.EqualTo(stun));
        Assert.That(Rule("LaunchDuration", false), Is.EqualTo(1.2f));
    }
    [TestCase("HeatWave", 8, false)]
    [TestCase("HeatWave", 9, true)]
    [TestCase("FlareBlitz", 10, false)]
    [TestCase("FlareBlitz", 11, true)]
    public void UpgradeLevelsMatchTheLicense(string kind, int level, bool expected) => Assert.That(Rule("Upgraded", Kind(kind), level), Is.EqualTo(expected));
    [Test]
    public void BurnPayoffsAndUniteFlagsArePreserved()
    {
        object normal = Rule("Damage", 8UL, Kind("Takedown"), false, false, false);
        object bonus = Rule("Damage", 8UL, Kind("Takedown"), false, true, false);
        Assert.That((float)Get(bonus, "ratio"), Is.GreaterThan((float)Get(normal, "ratio")));
        object unite = Rule("Damage", 8UL, Kind("Unite"), false, false, false);
        Assert.That(Get(unite, "proprieties").ToString(), Does.Contain("IsUniteMove"));
    }
    [TestCase("Swift")]
    [TestCase("Tackle")]
    [TestCase("HeatWave")]
    [TestCase("Takedown")]
    [TestCase("Slam")]
    [TestCase("Flamethrower")]
    [TestCase("FlareBlitz")]
    [TestCase("Unite")]
    [TestCase("State")]
    public void EffectsHaveRegisteredEditableArtAndUniquePersistedNetworkHashes(string kind)
    {
        string path = (string)Rule("Path", Kind(kind)); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        var script = prefab.GetComponent(Type("FlareonMoveObject")); Assert.That(script, Is.Not.Null);
        var data = new SerializedObject(script);
        foreach (string field in new[] { "graphics", "area", "flameArea", "sweep", "effectAudio", "ignitionSound", "impactSound" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
        { Assert.That(collider.isTrigger, Is.True); Assert.That(collider.enabled, Is.False); }
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.Not.Empty);
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials) { Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Does.Not.Contain("Error")); }
        var network = new SerializedObject(prefab.GetComponent(Type("Unity.Netcode.NetworkObject")));
        long hash = network.FindProperty("GlobalObjectIdHash").longValue; Assert.That(hash, Is.Not.Zero);
        Assert.That(File.ReadAllText(path), Does.Contain("GlobalObjectIdHash: " + hash));
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, registry.arraySize).Count(i => registry.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.EqualTo(1));
        var entries = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset")).FindProperty("m_SerializeEntries");
        string guid = AssetDatabase.AssetPathToGUID(path);
        Assert.That(Enumerable.Range(0, entries.arraySize).Count(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == guid
            && entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_Address").stringValue == path), Is.EqualTo(1));
    }
    [TestCase("Swift", 2f)]
    [TestCase("HeatWave", 3.7f)]
    [TestCase("Unite", 2f)]
    public void SerializedHitboxesMatchThePreviewAndCombatRules(string kind, float expected)
    {
        var script = AssetDatabase.LoadAssetAtPath<GameObject>((string)Rule("Path", Kind(kind))).GetComponent(Type("FlareonMoveObject"));
        var area = (SphereCollider)new SerializedObject(script).FindProperty("area").objectReferenceValue;
        Assert.That(area.radius, Is.EqualTo(expected));
        if (kind != "Unite") Assert.That(Rule("Range", Kind(kind)), Is.EqualTo(expected));
    }
    [Test]
    public void BasicsDoNotRequestAnUnfinishedFlareonAnimationOrCreateRuntimeGeometry()
    {
        string basic = File.ReadAllText("Assets/Scripts/Moves/Flareon/FlareonBasicAtk.cs");
        Assert.That(basic, Does.Not.Contain("PlayAnimation"));
        Assert.That(basic, Does.Contain("FlareonBasicRPC"));
        foreach (string path in Directory.GetFiles("Assets/Scripts/Moves/Flareon", "*.cs").Where(p => p.EndsWith("Object.cs") || p.EndsWith("Rules.cs")))
            foreach (string banned in new[] { "CreatePrimitive(", "new Mesh", "new GameObject(", "AddComponent<LineRenderer>" }) Assert.That(File.ReadAllText(path), Does.Not.Contain(banned));
    }
    [Test]
    public void StateViewContainsTheAuthoredSweepPreviewAndComboIndicators()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)Rule("Path", Kind("State")));
        var data = new SerializedObject(prefab.GetComponent(Type("FlareonMoveObject")));
        foreach (string field in new[] { "flamePreview", "recastMarker", "strike", "fullHeat" }) Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(data.FindProperty("attackPips").arraySize, Is.EqualTo(3)); Assert.That(data.FindProperty("heatPips").arraySize, Is.EqualTo(3));
    }
}
