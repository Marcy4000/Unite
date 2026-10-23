using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EmboarTests
{
    private const string Folder = "Assets/Prefabs/Objects/Moves/Emboar/";
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Kind(string value) => Enum.Parse(GameType("EmboarCast"), value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static object Rule(string method, params object[] args) => GameType("EmboarMoveRules").GetMethod(method).Invoke(null, args);
    private static T Property<T>(object state, string name) => (T)state.GetType().GetProperty(name).GetValue(state);

    [TestCase("Ember", false)]
    [TestCase("Smog", false)]
    [TestCase("FirePledge", false)]
    [TestCase("Flamethrower", false)]
    [TestCase("Unite", false)]
    [TestCase("HeatCrash", true)]
    [TestCase("FlameCharge", true)]
    public void OnlyMobilityBranchesConsumeReckless(string move, bool consumes)
    {
        object state = Activator.CreateInstance(GameType("EmboarRecklessState"));
        Call(state, "Fill", 10d);
        Assert.That(Call(state, "Consume", Kind(move), 11d), Is.EqualTo(consumes));
        Assert.That(Call(state, "Ready", 11d), Is.EqualTo(!consumes));
        if (consumes)
        {
            Assert.That(Property<double>(state, "RearmUntil"), Is.EqualTo(16));
            Assert.That(Call(state, "Consume", Kind(move), 11d), Is.False);
        }
    }
    [Test]
    public void RearmIsFiveSecondsAndNormalDamageCannotBypassIt()
    {
        object state = Activator.CreateInstance(GameType("EmboarRecklessState"));
        Call(state, "Fill", 0d); Call(state, "Consume", Kind("HeatCrash"), 1d);
        Call(state, "Add", 100, 5.99d); Assert.That(Property<int>(state, "Charge"), Is.Zero);
        Call(state, "Add", 100, 6d); Assert.That(Call(state, "Ready", 6d), Is.True);
    }
    [Test]
    public void ExpirationUsesItsActualDeadlineEvenAfterALongFrame()
    {
        object state = Activator.CreateInstance(GameType("EmboarRecklessState"));
        Call(state, "Fill", 0d); Call(state, "Tick", 30d);
        Assert.That(Property<double>(state, "RearmUntil"), Is.EqualTo(20));
        Call(state, "Add", 100, 30d); Assert.That(Call(state, "Ready", 30d), Is.True);
    }
    [Test]
    public void UniteRefillsOnceRatherThanExtendingReadyEveryFrame()
    {
        object state = Activator.CreateInstance(GameType("EmboarRecklessState"));
        Call(state, "Fill", 1d); Call(state, "Consume", Kind("FlameCharge"), 2d); Call(state, "Fill", 3d);
        Assert.That(Call(state, "Ready", 3d), Is.True);
        Assert.That(Property<double>(state, "ReadyUntil"), Is.EqualTo(18));
        Call(state, "Clear"); Assert.That(Property<int>(state, "Charge"), Is.Zero); Assert.That(Call(state, "Ready", 3d), Is.False);
    }
    [Test]
    public void PledgeNeedsThreeConfirmedSwingsAndLaunchesOnlyTheLast()
    {
        object state = Activator.CreateInstance(GameType("EmboarAttackState"));
        Call(state, "Arm", 10d);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(Call(state, "Boosted", 11d), Is.True);
            Assert.That(Call(state, "Launcher", 11d), Is.EqualTo(i == 2));
            Assert.That(Property<int>(state, "PledgeHits"), Is.EqualTo(3 - i));
            Call(state, "Confirm", 11d);
        }
        Assert.That(Property<int>(state, "PledgeHits"), Is.Zero);
        Assert.That(Call(state, "Launcher", 11d), Is.False);
    }
    [Test]
    public void MissesAndRepeatedReadinessChecksDoNotSpendPledge()
    {
        object state = Activator.CreateInstance(GameType("EmboarAttackState")); Call(state, "Arm", 0d);
        for (int i = 0; i < 20; i++) { Call(state, "Boosted", 1d); Call(state, "Launcher", 1d); }
        Assert.That(Property<int>(state, "PledgeHits"), Is.EqualTo(3));
    }
    [Test]
    public void ExpiredPledgeCannotLeaveAStaleLauncher()
    {
        object state = Activator.CreateInstance(GameType("EmboarAttackState"));
        Call(state, "Arm", 0d); Call(state, "Confirm", 1d); Call(state, "Confirm", 2d);
        Assert.That(Call(state, "Launcher", 2d), Is.True);
        Assert.That(Call(state, "Launcher", 5d), Is.False);
        Assert.That(Property<int>(state, "PledgeHits"), Is.Zero);
        Call(state, "Clear"); Assert.That(Property<int>(state, "Charge"), Is.Zero);
    }
    [Test]
    public void NaturalBoostedAttacksStillFollowAThreeHitCycle()
    {
        object state = Activator.CreateInstance(GameType("EmboarAttackState"));
        for (int i = 0; i < 9; i++)
        {
            Assert.That(Call(state, "Boosted", 1d), Is.EqualTo(i % 3 == 2)); Call(state, "Confirm", 1d);
        }
    }
    [Test]
    public void PledgeFinisherDoesNotLeaveAnExtraBankedBoostedAttack()
    {
        object state = Activator.CreateInstance(GameType("EmboarAttackState"));
        Call(state, "Confirm", 0d); Call(state, "Confirm", 0d); Call(state, "Arm", 1d);
        for (int i = 0; i < 3; i++) Call(state, "Confirm", 2d);
        Assert.That(Call(state, "Boosted", 2d), Is.False); Assert.That(Call(state, "Launcher", 2d), Is.False);
    }
    [TestCase(0, 2.5f)]
    [TestCase(4, 3f)]
    [TestCase(6, 3.5f)]
    [TestCase(14, 3.5f)]
    public void AttackRangeWorksWhenInitializedAfterEvolution(int level, float expected) => Assert.That(Rule("BasicRange", level), Is.EqualTo(expected));
    [TestCase(0f, false, 5f)]
    [TestCase(0.2f, false, 7f)]
    [TestCase(0.4f, false, 9f)]
    [TestCase(0f, true, 9f)]
    public void SprintAccelerationAndRecklessArePredictable(float age, bool empowered, float speed) => Assert.That(Rule("ChargeSpeed", age, empowered), Is.EqualTo(speed));
    [Test]
    public void DestinationRejectsInvalidCoordinatesAndClampsTravel()
    {
        Assert.That(Rule("Destination", Vector3.zero, new Vector3(float.NaN, 0, 0), 5f), Is.EqualTo(Vector3.zero));
        Assert.That(Rule("Destination", Vector3.zero, new Vector3(0, 100, 30), 5f), Is.EqualTo(Vector3.forward * 5));
        Assert.That(Rule("Direction", Vector3.zero, Vector3.zero), Is.EqualTo(Vector3.forward));
    }
    [Test]
    public void BothUniteHitsAreFlaggedAndItsBudgetIsNotAppliedTwice()
    {
        object rush = Rule("Damage", 42UL, Kind("Unite"), 0), slam = Rule("Damage", 42UL, Kind("Unite"), 1);
        var type = rush.GetType();
        Assert.That((float)type.GetField("ratio").GetValue(rush) + (float)type.GetField("ratio").GetValue(slam), Is.EqualTo(3.4f).Within(0.001));
        Assert.That((short)type.GetField("baseDmg").GetValue(rush) + (short)type.GetField("baseDmg").GetValue(slam), Is.EqualTo(650));
        Assert.That(type.GetField("proprieties").GetValue(slam).ToString(), Does.Contain("IsUniteMove"));
        Assert.That(type.GetField("proprieties").GetValue(slam).ToString(), Does.Not.Contain("CanCrit"));
    }
    [TestCase("EmboarEmber", "Ember", 0)]
    [TestCase("EmboarSmog", "Smog", 1)]
    [TestCase("EmboarFirePledge", "FirePledge", 0)]
    [TestCase("EmboarReversal", "Flamethrower", 0)]
    [TestCase("EmboarHeatCrash", "HeatCrash", 1)]
    [TestCase("EmboarFlameCharge", "FlameCharge", 1)]
    [TestCase("EmboarUnite", "Unite", 2)]
    public void ExistingAssetsFactoriesAndSlotsRemainCompatible(string name, string kind, int slot)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Emboar/" + name + ".asset"); Assert.That(asset, Is.Not.Null);
        var serialized = new SerializedObject(asset);
        var move = Rule("Move", Kind(kind));
        Assert.That(serialized.FindProperty("move").intValue, Is.EqualTo(Convert.ToInt32(move)));
        Assert.That(serialized.FindProperty("moveType").intValue, Is.EqualTo(slot));
        var runtime = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { move });
        Assert.That(runtime.GetType().Name, Is.EqualTo(name));
        Assert.That(serialized.FindProperty("description").stringValue, Is.Not.Empty);
        if (slot == 2)
        {
            Assert.That(serialized.FindProperty("moveName").stringValue, Is.EqualTo("Inferno Ringbreaker"));
            Assert.That(serialized.FindProperty("uniteEnergyCost").intValue, Is.EqualTo(100000));
        }
        else Assert.That(runtime.GetType().GetField("Cooldown").GetValue(runtime), Is.EqualTo(serialized.FindProperty("cooldown").floatValue));
    }
    [TestCase("Ember")]
    [TestCase("Smog")]
    [TestCase("FirePledge")]
    [TestCase("Flamethrower")]
    [TestCase("HeatCrash")]
    [TestCase("FlameCharge")]
    [TestCase("Unite")]
    [TestCase("Reckless")]
    public void EveryEffectHasAuthoredArtHitboxesSoundAndNetworkRegistration(string kind)
    {
        string path = (string)Rule("Path", Kind(kind)); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        var script = prefab.GetComponent(GameType("EmboarMoveObject")); Assert.That(script, Is.Not.Null);
        var serialized = new SerializedObject(script);
        foreach (string field in new[] { "graphics", "body", "burst", "waveFront", "telegraph", "area", "waveArea", "effectAudio", "rushSound", "impactSound" })
            Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true)) { Assert.That(collider.isTrigger, Is.True); Assert.That(collider.enabled, Is.False); }
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.Not.Empty);
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials) { Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Does.Not.Contain("Error")); }
        Assert.That(prefab.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform")), Is.Not.Null);
        var network = new SerializedObject(prefab.GetComponent(GameType("Unity.Netcode.NetworkObject")));
        long hash = network.FindProperty("GlobalObjectIdHash").longValue; Assert.That(hash, Is.Not.Zero);
        Assert.That(File.ReadAllText(path), Does.Contain("GlobalObjectIdHash: " + hash));
        var list = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        int count = 0;
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) count++;
        Assert.That(count, Is.EqualTo(1));
        string guid = AssetDatabase.AssetPathToGUID(path);
        list = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset")).FindProperty("m_SerializeEntries"); count = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            var entry = list.GetArrayElementAtIndex(i); if (entry.FindPropertyRelative("m_GUID").stringValue != guid) continue;
            count++; Assert.That(entry.FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path));
        }
        Assert.That(count, Is.EqualTo(1));
    }
    [Test]
    public void ArtDimensionsAndPledgePipsMatchGameplay()
    {
        var unite = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "EmboarUniteObject.prefab");
        Assert.That(unite.GetComponentInChildren<SphereCollider>(true).radius, Is.EqualTo(3.5f));
        var flame = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "EmboarFlamethrowerObject.prefab").GetComponentInChildren<BoxCollider>(true);
        Assert.That(flame.center.z - flame.size.z / 2, Is.EqualTo(0));
        var pledge = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "EmboarFirePledgeObject.prefab").GetComponent(GameType("EmboarMoveObject")));
        Assert.That(pledge.FindProperty("pledgePips").arraySize, Is.EqualTo(3));
    }
    [Test]
    public void BasicsDoNotRequestAnimationsAndReworkedMovesDoNotUseSharedSpawnCallbacks()
    {
        string basic = File.ReadAllText("Assets/Scripts/Moves/Emboar/EmboarBasicAtk.cs");
        Assert.That(basic, Does.Not.Contain("PlayAnimation(")); Assert.That(basic, Does.Not.Contain("SetTrigger("));
        foreach (string name in new[] { "EmboarMoveBase", "EmboarMoveObject" })
        {
            string source = File.ReadAllText("Assets/Scripts/Moves/Emboar/" + name + ".cs");
            Assert.That(source, Does.Not.Contain("onObjectSpawned")); Assert.That(source, Does.Not.Contain("DOJump("));
            Assert.That(source, Does.Not.Contain("CreatePrimitive(")); Assert.That(source, Does.Not.Contain("new Mesh"));
        }
    }
    [Test]
    public void MarshadowCopiesTheSlamInsteadOfTheRemovedSteroid()
    {
        object move = Enum.Parse(GameType("AvailableMoves"), "EmboarUnite");
        string description = (string)GameType("MarshadowCopyRules").GetMethod("Description").Invoke(null, new[] { move });
        Assert.That(description, Does.Contain("slam")); Assert.That(description, Does.Contain("does not copy Reckless")); Assert.That(description, Does.Not.Contain("cooldown-reduction"));
    }
}
