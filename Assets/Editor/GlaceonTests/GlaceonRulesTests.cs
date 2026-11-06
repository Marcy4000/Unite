using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GlaceonRulesTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object state, string name, params object[] args) => state.GetType().GetMethod(name).Invoke(state, args);
    private static object Get(object state, string name) => state.GetType().GetProperty(name).GetValue(state);
    [Test]
    public void CrystalReturnsPreserveNewGainsClampAndRefreshExpiry()
    {
        var state = Activator.CreateInstance(T("GlaceonCrystalState"));
        Call(state, "Add", 6, 10d); Assert.That(Call(state, "TakeAll", 11d), Is.EqualTo(6));
        Call(state, "Add", 2, 11.2d); Call(state, "Add", 6, 11.5d);
        Assert.That(Get(state, "Count"), Is.EqualTo(8)); Assert.That(Get(state, "ExpiresAt"), Is.EqualTo(16.5d));
        Call(state, "Tick", 16.49d); Assert.That(Get(state, "Count"), Is.EqualTo(8));
        Call(state, "Tick", 16.5d); Assert.That(Get(state, "Count"), Is.Zero);
        Call(state, "Add", 8, 20d); Call(state, "Clear"); Call(state, "Tick", 21d); Assert.That(Get(state, "Count"), Is.Zero);
    }
    [TestCase("GlaceSwift", 5)] [TestCase("GlaceTailWhip", 8)] [TestCase("GlaceIcyWind", 6.5f)]
    [TestCase("GlaceIceShard", 8.5f)] [TestCase("GlaceFreezeDry", 7.5f)] [TestCase("GlaceonUnite", 0)] [TestCase("GlaceIcycleSpear", 5)]
    public void MovesUseServerAcceptanceAndRegisteredAuthoritativeEffects(string name, float cooldown)
    {
        var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { Enum.Parse(T("AvailableMoves"), name) });
        Assert.That(T("PrefabMove").IsInstanceOfType(move), Is.True);
        Assert.That(Get(move, "RequiresServerAcceptance"), Is.True); Assert.That(Get(move, "CooldownOnFinish"), Is.False);
        Assert.That(move.GetType().GetField("Cooldown").GetValue(move), Is.EqualTo(cooldown));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)Get(move, "PrefabPath")); Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent(T("PrefabMoveEffect")), Is.Not.Null);
        Assert.That(prefab.GetComponent(T("ClientNetworkTransform")), Is.Null);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, registry.arraySize).Any(i => registry.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.True);
        var addressables = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset")).FindProperty("m_SerializeEntries");
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        Assert.That(Enumerable.Range(0, addressables.arraySize).Any(i => addressables.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == guid), Is.True);
    }
    [Test]
    public void WindRulesUseFourHitThresholdSixHitCapAndWildHpCap()
    {
        var rules = T("GlaceonMoveRules");
        Assert.That(rules.GetMethod("WindSlow").Invoke(null, new object[] { 3 }), Is.Zero);
        Assert.That(rules.GetMethod("WindSlow").Invoke(null, new object[] { 4 }), Is.EqualTo(32));
        Assert.That(rules.GetMethod("WindSlow").Invoke(null, new object[] { 8 }), Is.EqualTo(48));
        Assert.That(rules.GetMethod("WindExtraDamage").Invoke(null, new object[] { 100000, true }), Is.EqualTo(300));
    }
    [Test]
    public void SpearKeepsHeldInputActiveAfterAcceptanceForBotAndHumanRelease()
    {
        var spear = Activator.CreateInstance(T("GlaceIcicleSpear"));
        spear.GetType().GetField("aiming", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(spear, true);
        spear.GetType().GetField("IsActive").SetValue(spear, true);
        spear.GetType().GetMethod("AcceptExecution", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(spear, null);
        Assert.That(spear.GetType().GetField("IsActive").GetValue(spear), Is.True);
    }

    [Test]
    public void OldCastAcknowledgementsCannotMatchANewerGlaceonRequest()
    {
        var move = Activator.CreateInstance(T("GlaceFreezeDry"));
        T("GlaceonMove").GetField("executionRequest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(move, 42);
        var matches = T("PrefabMove").GetMethod("MatchesExecutionRequest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(matches.Invoke(move, new object[] { 41 }), Is.False);
        Assert.That(matches.Invoke(move, new object[] { 42 }), Is.True);
    }

}
