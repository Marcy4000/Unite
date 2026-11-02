using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EspeonTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string value) => Enum.Parse(T(type), value);
    private static object Call(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, args.Select(a => a.GetType()).ToArray()).Invoke(instance, args);
    private static object Get(object instance, string property) => instance.GetType().GetProperty(property).GetValue(instance);
    private static object Rule(string method, params object[] args) => T("EspeonMoveRules").GetMethod(method).Invoke(null, args);
    private static object Field(object instance, string field) => instance.GetType().GetField(field).GetValue(instance);

    [TestCase("EspeonSwift", 6)]
    [TestCase("EspeonGrowl", 7)]
    [TestCase("EspeonPsyshock", 5)]
    [TestCase("EspeonStoredPower", 5)]
    [TestCase("EspeonPsybeam", 7)]
    [TestCase("EspeonFutureSight", 7)]
    [TestCase("EspeonPsychicSolare", 0)]
    public void MovesHaveFactoriesArtAndRegisteredNetworkPrefabs(string name, float cooldown)
    {
        object id = E("AvailableMoves", name);
        var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(Field(move, "Cooldown"), Is.EqualTo(cooldown));
        Assert.That(Get(move, "CooldownOnFinish"), Is.False, "Only accepted server casts commit cooldowns.");
        var asset = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Espeon/" + name + ".asset"));
        Assert.That(asset.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        string previewGuid = asset.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue;
        Assert.That(previewGuid, Is.Not.Empty);
        var previews = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovePreviews.asset")).FindProperty("m_SerializeEntries");
        Assert.That(Enumerable.Range(0, previews.arraySize).Any(i => previews.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == previewGuid), Is.True,
            "An AssetReference alone is insufficient: menu previews must be registered with Addressables.");
        var root = AssetDatabase.LoadAssetAtPath<GameObject>((string)Get(move, "PrefabPath"));
        Assert.That(root, Is.Not.Null); Assert.That(root.GetComponent(T("PrefabMoveEffect")), Is.Not.Null);
        Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(0));
        Assert.That(root.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        var network = new SerializedObject(root.GetComponent(T("Unity.Netcode.NetworkObject")));
        Assert.That(network.FindProperty("GlobalObjectIdHash").longValue, Is.Not.Zero);
        var list = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == root), Is.True);
    }
    [Test]
    public void LearnsetAndPassiveMatchZeroBasedLevelsWithoutChangingEvolution()
    {
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Espeon.asset"));
        var rows = data.FindProperty("learnableMoves");
        CollectionAssert.AreEqual(new[] { 0, 1, 3, 5, 7, 9, 11 }, Enumerable.Range(0, rows.arraySize).Select(i => rows.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue));
        Assert.That(data.FindProperty("passive").enumValueIndex, Is.EqualTo(Convert.ToInt32(E("AvailablePassives", "EspeonPassive"))));
        Assert.That(data.FindProperty("evolutions").GetArrayElementAtIndex(1).FindPropertyRelative("level").intValue, Is.EqualTo(3));
        Assert.That(rows.GetArrayElementAtIndex(2).FindPropertyRelative("moves").arraySize, Is.EqualTo(2));
        Assert.That(rows.GetArrayElementAtIndex(5).FindPropertyRelative("isUpgraded").boolValue, Is.True);
    }
    [Test]
    public void AmmunitionStartsEmptyCapsAtFiveAndKeepsItsIndependentGenerationClock()
    {
        var blasts = Activator.CreateInstance(T("EspeonBlasts"));
        Call(blasts, "Reset", 10d); Assert.That(Get(blasts, "Count"), Is.Zero);
        Call(blasts, "Tick", 11.99d); Assert.That(Get(blasts, "Count"), Is.Zero);
        Call(blasts, "Tick", 12d); Assert.That(Get(blasts, "Count"), Is.EqualTo(1));
        Call(blasts, "Add", 3); Assert.That(Get(blasts, "Count"), Is.EqualTo(4));
        Assert.That(Call(blasts, "Spend"), Is.EqualTo(4));
        Call(blasts, "Tick", 13.99d); Assert.That(Get(blasts, "Count"), Is.Zero);
        Call(blasts, "Tick", 14d); Assert.That(Get(blasts, "Count"), Is.EqualTo(1));
        Call(blasts, "Tick", 100d); Assert.That(Get(blasts, "Count"), Is.EqualTo(5));
        Assert.That(Call(blasts, "Spend"), Is.EqualTo(5));
        Call(blasts, "Tick", 100.01d); Assert.That(Get(blasts, "Count"), Is.Zero);
    }
    [TestCase(false, false, 0.7f, 19, 360)]
    [TestCase(false, true, 0.21f, 6, 108)]
    [TestCase(true, false, 0.726f, 14, 374)]
    [TestCase(true, true, 0.22f, 4, 112)]
    public void VolleyUsesDetailedReferenceCoefficients(bool psychic, bool repeat, float ratio, int slider, int baseDamage)
    {
        var damage = Rule("Volley", 7ul, psychic, repeat);
        Assert.That(Field(damage, "ratio"), Is.EqualTo(ratio)); Assert.That(Field(damage, "slider"), Is.EqualTo(slider)); Assert.That(Field(damage, "baseDmg"), Is.EqualTo(baseDamage));
        Assert.That(Convert.ToInt32(Field(damage, "proprieties")) & 1, Is.Zero);
    }
    [Test]
    public void TimedBoostUsesItsOwnFormulaAndOnlyBasicsCanCrit()
    {
        var damage = Rule("Basic", 7ul, true, true);
        Assert.That(Field(damage, "ratio"), Is.EqualTo(0.54f)); Assert.That(Field(damage, "slider"), Is.EqualTo(12)); Assert.That(Field(damage, "baseDmg"), Is.EqualTo(230));
        Assert.That(Convert.ToInt32(Field(damage, "proprieties")) & 3, Is.EqualTo(3));
        var eevee = Rule("Basic", 7ul, true, false); Assert.That(Field(eevee, "ratio"), Is.EqualTo(0.35f));
    }
    [TestCase("Burned")]
    [TestCase("Poisoned")]
    [TestCase("Frozen")]
    [TestCase("Asleep")]
    [TestCase("Incapacitated")]
    [TestCase("Immobilized")]
    public void PassiveRecognizesSoftConditionsAndHardHindrances(string name)
    {
        object status = Activator.CreateInstance(T("StatusEffect"), E("StatusType", name), 1f, true, (ushort)1);
        Assert.That(T("EspeonMoveRules").GetMethod("TriggersPassive", new[] { T("StatusEffect") }).Invoke(null, new[] { status }), Is.True);
    }
    [Test]
    public void NegativeEffectsCanRetainTheirExactOriginalSource()
    {
        object status = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Burned"), 1f, true, (ushort)1);
        status = Call(status, "WithSource", 123456ul);
        Assert.That(Field(status, "HasSource"), Is.True); Assert.That(Field(status, "SourceId"), Is.EqualTo(123456ul));
    }
    [Test]
    public void SourceAttributionDoesNotBreakLegacyEffectRemovalByValue()
    {
        object status = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Frozen"), 1f, true, (ushort)9);
        object attributed = Call(status, "WithSource", 123ul);
        Assert.That(Call(status, "Equals", attributed), Is.True);
        Assert.That(status.Equals(attributed), Is.True); Assert.That(status.GetHashCode(), Is.EqualTo(attributed.GetHashCode()));
        object stat = Activator.CreateInstance(T("StatChange"), (short)30, E("Stat", "Speed"), 2f, true, false, true, (ushort)9, true);
        attributed = Call(stat, "WithSource", 123ul);
        Assert.That(Call(stat, "Equals", attributed), Is.True);
        Assert.That(stat.Equals(attributed), Is.True); Assert.That(stat.GetHashCode(), Is.EqualTo(attributed.GetHashCode()));
    }
    [Test]
    public void GeometryConvergesAtAimAndSplitFanExcludesEnemiesBehind()
    {
        Vector3 left = (Vector3)Rule("ProjectileOrigin", Vector3.zero, Vector3.forward, 0, 5);
        Vector3 right = (Vector3)Rule("ProjectileOrigin", Vector3.zero, Vector3.forward, 4, 5);
        Assert.That(left, Is.EqualTo(Vector3.left * 2)); Assert.That(right, Is.EqualTo(Vector3.right * 2));
        Assert.That(Rule("InSplitFan", Vector3.zero, Vector3.forward, Vector3.forward), Is.True);
        Assert.That(Rule("InSplitFan", Vector3.zero, Vector3.back, Vector3.forward), Is.False);
    }
    [Test]
    public void BasicAttackRangeSharesGrowlAimTuningOnOwnerAndServer()
    {
        var targetRange = T("EspeonMoveRules").GetField("TargetRange");
        float original = (float)targetRange.GetValue(null);
        try
        {
            targetRange.SetValue(null, 8.25f);
            var growl = Activator.CreateInstance(T("EspeonGrowl"));
            var attack = Activator.CreateInstance(T("EspeonBasicAtk"));
            attack.GetType().GetMethod("Initialize").Invoke(attack, new object[] { null });
            Assert.That(Field(attack, "range"), Is.EqualTo(Get(growl, "Range")));
            attack.GetType().GetMethod("InitializeServer").Invoke(attack, new object[] { null });
            Assert.That(Field(attack, "range"), Is.EqualTo(Get(growl, "Range")));
        }
        finally { targetRange.SetValue(null, original); }
    }
    [Test]
    public void EeveeUsesExistingSharedAnimatorStatesIncludingSwift()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Eevee/Eevee.controller");
        foreach (string clip in new[] { "spell1", "spell2", "atk4", "atk5", "atk6", "idle" })
            Assert.That(controller.layers[0].stateMachine.states.Any(s => s.state.name == (string)Rule("EeveeAnimation", clip)), Is.True, clip);
    }
    [Test]
    public void AnimationsAndBonusArtArePresent()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Espeon/Espeon.controller");
        foreach (string clip in new[] { "spell1", "spell1a", "spell1b", "spell2", "spell2a", "spell2b", "spellu", "atk1", "atk3" })
            Assert.That(controller.layers[0].stateMachine.states.Any(s => s.state.name == "ani_" + clip + "_bat_019600"), Is.True);
        Assert.That(Resources.Load<Sprite>("Effects/Espeon/BonusPsyshock"), Is.Not.Null);
        Assert.That(Resources.Load<Sprite>("Effects/Espeon/BonusStoredPower"), Is.Not.Null);
    }
}
