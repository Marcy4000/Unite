using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class UmbreonTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string value) => Enum.Parse(T(type), value);
    private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
    private static object Field(object instance, string name) => instance.GetType().GetField(name).GetValue(instance);
    private static object Rule(string method, params object[] args) => T("UmbreonMoveRules").GetMethod(method, args.Select(a => a.GetType()).ToArray()).Invoke(null, args);
    [TestCase("UmbreonSwift", 6)]
    [TestCase("UmbreonFakeTears", 8)]
    [TestCase("UmbreonMeanLook", 9)]
    [TestCase("UmbreonFoulPlay", 7.5f)]
    [TestCase("UmbreonWish", 6)]
    [TestCase("UmbreonSnarl", 10)]
    [TestCase("UmbreonMoonlightPrance", 0)]
    public void FactoriesArtAndNetworkRegistration(string name, float cooldown)
    {
        object id = E("AvailableMoves", name);
        var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(Field(move, "Cooldown"), Is.EqualTo(cooldown));
        Assert.That(Get(move, "RequiresServerAcceptance"), Is.True);
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Umbreon/" + name + ".asset"); Assert.That(asset, Is.Not.Null);
        var data = new SerializedObject(asset); Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        string guid = data.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue; Assert.That(guid, Is.Not.Empty);
        var previews = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovePreviews.asset")).FindProperty("m_SerializeEntries");
        Assert.That(Enumerable.Range(0, previews.arraySize).Any(i => previews.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == guid), Is.True);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)Get(move, "PrefabPath")); Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent(T("PrefabMoveEffect")), Is.Not.Null);
        Assert.That(new SerializedObject(prefab.GetComponent(T("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue, Is.Not.Zero);
        var list = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.True);
    }
    [Test]
    public void ProgressionAndEvolutionAreZeroBased()
    {
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Umbreon.asset")); var rows = data.FindProperty("learnableMoves");
        CollectionAssert.AreEqual(new[] { 0, 1, 3, 5, 7, 9, 11 }, Enumerable.Range(0, rows.arraySize).Select(i => rows.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue));
        Assert.That(data.FindProperty("evolutions").GetArrayElementAtIndex(1).FindPropertyRelative("level").intValue, Is.EqualTo(3));
        Assert.That(data.FindProperty("passive").enumValueIndex, Is.EqualTo(Convert.ToInt32(E("AvailablePassives", "UmbreonPassive"))));
        Assert.That(rows.GetArrayElementAtIndex(5).FindPropertyRelative("isUpgraded").boolValue, Is.True);
    }
    [TestCase("Burned", true)]
    [TestCase("Poisoned", true)]
    [TestCase("Frozen", true)]
    [TestCase("Asleep", true)]
    [TestCase("Incapacitated", true)]
    [TestCase("Immobilized", true)]
    [TestCase("VisionObscuring", true)]
    [TestCase("Bound", false)]
    [TestCase("Unstoppable", false)]
    public void PassiveClassification(string name, bool expected)
    {
        object status = Activator.CreateInstance(T("StatusEffect"), E("StatusType", name), 1f, true, (ushort)1);
        Assert.That(Rule("TriggersPassive", status), Is.EqualTo(expected));
    }
    [TestCase(false, false, 1f, 0, 0)]
    [TestCase(true, false, 1.4f, 0, 0)]
    [TestCase(true, true, 1.96f, 5, 180)]
    public void BasicCoefficients(bool boosted, bool mean, float ratio, int slider, int baseDamage)
    {
        var damage = Rule("Basic", 7ul, boosted, mean);
        Assert.That(Field(damage, "ratio"), Is.EqualTo(ratio)); Assert.That(Field(damage, "slider"), Is.EqualTo(slider)); Assert.That(Field(damage, "baseDmg"), Is.EqualTo(baseDamage));
        Assert.That(Convert.ToInt32(Field(damage, "proprieties")) & 3, Is.EqualTo(3));
    }
    [Test]
    public void WishAndFoulUseAttackRatherThanSpecialAttack()
    {
        Assert.That(Rule("WishHeal", 100, 5, false), Is.EqualTo(990));
        Assert.That(Rule("WishHeal", 100, 11, true), Is.EqualTo(1278));
        Assert.That(Rule("FoulFollowup", 300, 100, 500), Is.EqualTo(712), "Snapshot wins if it exceeded own Attack at first hit, even if own Attack later rises.");
        Assert.That(Rule("FoulFollowup", 90, 100, 200), Is.EqualTo(475));
    }
    [Test]
    public void DashTuningAndFollowupCooldownAreIndependent()
    {
        var move = Activator.CreateInstance(T("UmbreonFoulPlay")); var field = T("UmbreonMoveRules").GetField("DashDistance"); float previous = (float)field.GetValue(null);
        try { field.SetValue(null, 6.25f); Assert.That(Get(move, "Range"), Is.EqualTo(6.25f)); }
        finally { field.SetValue(null, previous); }
        Assert.That(Get(move, "CooldownOnEffectEnd"), Is.True); Assert.That(Get(move, "CooldownOnFinish"), Is.False);
        var wish = Activator.CreateInstance(T("UmbreonWish")); Assert.That(Get(wish, "AllowSelfTarget"), Is.True); Assert.That(Get(wish, "IsOffensive"), Is.False);
    }
    [Test]
    public void AnimatorStatesAndFollowupIconExist()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Umbreon/Umbreon.controller");
        foreach (var clip in new[] { "idle", "atk1", "atk2", "atk3", "spell1", "spell2", "spell1a", "spell1b12", "spell1b22", "spell2a", "spell2b2", "spell2b4", "spellu" })
            Assert.That(controller.layers[0].stateMachine.states.Any(s => s.state.name == "ani_" + clip + "_bat_0197"), Is.True, clip);
        Assert.That(Resources.Load<Sprite>("Effects/Umbreon/FoulFollowup"), Is.Not.Null);
        var eevee = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Eevee/Eevee.controller");
        foreach (var clip in new[] { "idle", "atk4", "atk5", "atk6", "spell1", "spell2" })
            Assert.That(eevee.layers[0].stateMachine.states.Any(s => s.state.name == (string)Rule("EeveeAnimation", clip)), Is.True, clip);
    }
}
