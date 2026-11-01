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

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void ComboSyncDoesNotSendStatRpcsWhenEitherBehaviourIsUnspawned(bool controllerSpawned, bool pokemonSpawned)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("Flareon unspawned combo cleanup");
        Component controller = null;
        Component pokemon = null;
        Type networkBehaviour = Type("Unity.Netcode.NetworkBehaviour");
        try
        {
            root.AddComponent(Type("Unity.Netcode.NetworkObject"));
            pokemon = root.AddComponent(Type("Pokemon"));
            controller = root.AddComponent(Type("MovesController"));
            Component manager = root.AddComponent(Type("PlayerManager"));
            manager.GetType().GetField("pokemon", privateInstance).SetValue(manager, pokemon);
            manager.GetType().GetField("movesController", privateInstance).SetValue(manager, controller);
            object runtime = Activator.CreateInstance(Type("FlareonCharacterRuntime"), new object[] { manager });
            object combo = runtime.GetType().GetField("combo", privateInstance).GetValue(runtime);
            Call(combo, "AddStack", 0d);
            networkBehaviour.GetProperty("IsServer").SetValue(controller, true);
            networkBehaviour.GetProperty("IsSpawned").SetValue(controller, controllerSpawned);
            networkBehaviour.GetProperty("IsSpawned").SetValue(pokemon, pokemonSpawned);
            Assert.DoesNotThrow(() => runtime.GetType().GetMethod("ApplyPassiveStacks", privateInstance).Invoke(runtime, null));
            Assert.That(combo.GetType().GetProperty("Stacks").GetValue(combo), Is.EqualTo(1));
        }
        finally
        {
            if (controller != null)
            {
                networkBehaviour.GetProperty("IsSpawned").SetValue(controller, false);
                networkBehaviour.GetProperty("IsServer").SetValue(controller, false);
            }
            if (pokemon != null) networkBehaviour.GetProperty("IsSpawned").SetValue(pokemon, false);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RepeatedUnspawnedTeardownClearsLocalComboAndUnbindsRuntime(bool wasServer)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("Flareon repeated teardown");
        Component controller = null;
        try
        {
            root.AddComponent(Type("Unity.Netcode.NetworkObject"));
            Component pokemon = root.AddComponent(Type("Pokemon"));
            controller = root.AddComponent(Type("MovesController"));
            Component manager = root.AddComponent(Type("PlayerManager"));
            manager.GetType().GetField("pokemon", privateInstance).SetValue(manager, pokemon);
            manager.GetType().GetField("movesController", privateInstance).SetValue(manager, controller);
            object runtime = Activator.CreateInstance(Type("FlareonCharacterRuntime"), new object[] { manager });
            object combo = runtime.GetType().GetField("combo", privateInstance).GetValue(runtime);
            Call(combo, "AddStack", 0d);
            Call(combo, "ConfirmBasic", 0d);
            runtime.GetType().GetField("boundPokemon", privateInstance).SetValue(runtime, pokemon);
            Type("Unity.Netcode.NetworkBehaviour").GetProperty("IsServer").SetValue(controller, wasServer);
            for (int attempt = 0; attempt < 2; attempt++)
                Assert.DoesNotThrow(() => ((IDisposable)runtime).Dispose());
            Assert.That(combo.GetType().GetProperty("Stacks").GetValue(combo), Is.Zero);
            Assert.That(combo.GetType().GetProperty("AttackCharge").GetValue(combo), Is.Zero);
            Assert.That(runtime.GetType().GetField("boundPokemon", privateInstance).GetValue(runtime), Is.Null);
        }
        finally
        {
            if (controller != null) Type("Unity.Netcode.NetworkBehaviour").GetProperty("IsServer").SetValue(controller, false);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

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
        Assert.That(Type("CombatMove").IsInstanceOfType(move), Is.True);
        Assert.That(move.GetType().GetProperty("MoveId").GetValue(move), Is.EqualTo(enumValue));
        Assert.That(move.GetType().GetProperty("LockDuringLoad").GetValue(move), Is.True);
        Assert.That(move.GetType().GetProperty("UsesExplicitUniteCastCheckpoint").GetValue(move), Is.EqualTo(kind == "Unite"));
        Assert.That(Get(move, "Cooldown"), Is.EqualTo(cooldown));
        Assert.That(Rule("Slot", Kind(kind)), Is.EqualTo(slot));
        Assert.DoesNotThrow(() => Call(move, "ResetMove"));
        Assert.DoesNotThrow(() => Call(move, "ResetMove"));
    }
    [Test]
    public void ObserverSnapshotsKeepNewestChargeStacksAndRecoveryDeadline()
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("Flareon snapshot ordering");
        try
        {
            root.AddComponent(Type("Unity.Netcode.NetworkObject"));
            Component controller = root.AddComponent(Type("MovesController"));
            Component manager = root.AddComponent(Type("PlayerManager"));
            manager.GetType().GetField("movesController", privateInstance).SetValue(manager, controller);
            object runtime = Activator.CreateInstance(Type("FlareonCharacterRuntime"), new object[] { manager });
            Type snapshotType = Type("FlareonCharacterRuntime+PresentationState");
            object snapshot = Activator.CreateInstance(snapshotType);
            snapshotType.GetField("Revision").SetValue(snapshot, 2L);
            snapshotType.GetField("AttackCharge").SetValue(snapshot, 2);
            snapshotType.GetField("Stacks").SetValue(snapshot, 3);
            snapshotType.GetField("RecoveryUntil").SetValue(snapshot, 42d);
            snapshotType.GetField("BasicDirection").SetValue(snapshot, Vector3.forward);
            Call(runtime, "ApplyPresentationState", JsonUtility.ToJson(snapshot));
            snapshotType.GetField("Revision").SetValue(snapshot, 1L);
            snapshotType.GetField("AttackCharge").SetValue(snapshot, 0);
            snapshotType.GetField("Stacks").SetValue(snapshot, 0);
            snapshotType.GetField("RecoveryUntil").SetValue(snapshot, 0d);
            Call(runtime, "ApplyPresentationState", JsonUtility.ToJson(snapshot));
            Assert.That(runtime.GetType().GetProperty("AttackCharge").GetValue(runtime), Is.EqualTo(2));
            Assert.That(runtime.GetType().GetProperty("Stacks").GetValue(runtime), Is.EqualTo(3));
            object applied = runtime.GetType().GetField("presentation", privateInstance).GetValue(runtime);
            Assert.That(snapshotType.GetField("RecoveryUntil").GetValue(applied), Is.EqualTo(42d));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
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
    [Test]
    public void AttackChargeAndHeatExpireIndependently()
    {
        object state = Activator.CreateInstance(Type("FlareonComboState"));
        Call(state, "ConfirmBasic", 1d);
        Call(state, "AddStack", 3d);
        Call(state, "Tick", 6d);
        Assert.That(state.GetType().GetProperty("AttackCharge").GetValue(state), Is.Zero);
        Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.EqualTo(1));
        Call(state, "Tick", 8d);
        Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.Zero);
    }
    [Test]
    public void BasicAtExpiryStartsNewCycleInsteadOfBoosting()
    {
        object state = Activator.CreateInstance(Type("FlareonComboState"));
        Call(state, "ConfirmBasic", 0d);
        Call(state, "ConfirmBasic", 1d);
        Assert.That(Call(state, "ConfirmBasic", 6d), Is.False);
        Assert.That(state.GetType().GetProperty("AttackCharge").GetValue(state), Is.EqualTo(1));
        Assert.That(state.GetType().GetProperty("AttackExpires").GetValue(state), Is.EqualTo(11d));
    }
    [Test]
    public void HeatAtExpiryStartsNewStackAndClearResetsDeadlines()
    {
        object state = Activator.CreateInstance(Type("FlareonComboState"));
        Call(state, "AddStack", 0d);
        Call(state, "AddStack", 1d);
        Call(state, "AddStack", 6d);
        Assert.That(state.GetType().GetProperty("Stacks").GetValue(state), Is.EqualTo(1));
        Assert.That(state.GetType().GetProperty("StacksExpire").GetValue(state), Is.EqualTo(11d));
        Call(state, "ConfirmBasic", 6d);
        Call(state, "Clear");
        Assert.That(state.GetType().GetProperty("AttackExpires").GetValue(state), Is.EqualTo(0d));
        Assert.That(state.GetType().GetProperty("StacksExpire").GetValue(state), Is.EqualTo(0d));
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
    public void SwiftUsesReducedPulseDamageAndRetainsCrits()
    {
        object damage = Rule("Damage", 8UL, Kind("Swift"), false, false, false);
        Assert.That(Get(damage, "attackerId"), Is.EqualTo(8UL));
        Assert.That(Get(damage, "ratio"), Is.EqualTo(0.7f));
        Assert.That(Get(damage, "slider"), Is.EqualTo(4));
        Assert.That(Get(damage, "baseDmg"), Is.EqualTo(100));
        Assert.That(Get(damage, "type").ToString(), Is.EqualTo("Physical"));
        Assert.That(Get(damage, "proprieties").ToString(), Is.EqualTo("CanCrit"));
    }
    [TestCase(false, false, 1f, 6, 160)]
    [TestCase(true, false, 1f, 6, 160)]
    [TestCase(false, true, 1.05f, 7, 200)]
    [TestCase(true, true, 1.05f, 7, 200)]
    public void HeatWaveUsesReducedNonCriticalDamageForBothUpgradeStates(bool upgraded, bool burned, float ratio, int slider, int baseDamage)
    {
        object damage = Rule("Damage", 8UL, Kind("HeatWave"), upgraded, burned, false);
        Assert.That(Get(damage, "attackerId"), Is.EqualTo(8UL));
        Assert.That(Get(damage, "ratio"), Is.EqualTo(ratio));
        Assert.That(Get(damage, "slider"), Is.EqualTo(slider));
        Assert.That(Get(damage, "baseDmg"), Is.EqualTo(baseDamage));
        Assert.That(Get(damage, "type").ToString(), Is.EqualTo("Physical"));
        Assert.That(Get(damage, "proprieties").ToString(), Is.EqualTo("None"));
    }
    [Test]
    public void HeatWaveSpeedBonusIsTenPercent()
    {
        Assert.That(Type("FlareonMoveRules").GetField("HeatSpeedBonus").GetRawConstantValue(), Is.EqualTo(10));
    }
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
        Assert.That(basic, Does.Contain("ServerResolved => true"));
        Assert.That(basic, Does.Contain("RequestServerBasicAttack"));
        Assert.That(basic, Does.Contain("PerformBasicAttack(target)"));
        Assert.That(basic, Does.Not.Contain("FlareonBasicRPC"));
        foreach (string path in Directory.GetFiles("Assets/Scripts/Moves/Flareon", "*.cs").Where(p => p.EndsWith("Object.cs") || p.EndsWith("Rules.cs")))
            foreach (string banned in new[] { "CreatePrimitive(", "new Mesh", "new GameObject(", "AddComponent<LineRenderer>" }) Assert.That(File.ReadAllText(path), Does.Not.Contain(banned));
    }
    [Test]
    public void StateViewKeepsSweepPreviewAndHeatIndicatorsWhileBasicsUseSharedCounter()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)Rule("Path", Kind("State")));
        var data = new SerializedObject(prefab.GetComponent(Type("FlareonMoveObject")));
        foreach (string field in new[] { "flamePreview", "recastMarker", "strike", "fullHeat" }) Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(data.FindProperty("heatPips").arraySize, Is.EqualTo(3));
        Assert.That(Type("ChargedBasicAttackBase").IsAssignableFrom(Type("FlareonBasicAtk")), Is.True);
    }
}
