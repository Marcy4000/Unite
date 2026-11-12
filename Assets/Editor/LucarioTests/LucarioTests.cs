using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class LucarioTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object P(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
    private static object F(object instance, string name) => instance.GetType().GetField(name).GetValue(instance);
    private static object Rule(string name, params object[] args) => T("LucarioMoveRules").GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(null, args);

    [TestCase("QuickAttack", 6)]
    [TestCase("MeteorMash", 7)]
    [TestCase("ExtremeSpeed", 7.5f)]
    [TestCase("PowerUpPunch", 7)]
    [TestCase("BoneRush", 9)]
    [TestCase("CloseCombat", 6.5f)]
    [TestCase("AuraCannon", 0)]
    public void MoveAssetsFactoriesAndRegistrations(string name, float cooldown)
    {
        string id = "Lucario" + name;
        var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { E("AvailableMoves", id) });
        Assert.That(F(move, "Cooldown"), Is.EqualTo(cooldown)); Assert.That(P(move, "RequiresServerAcceptance"), Is.True);
        Assert.That(P(move, "CooldownOnFinish"), Is.False);
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Lucario/" + id + ".asset"); Assert.That(asset, Is.Not.Null);
        var data = new SerializedObject(asset); Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        string guid = data.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue;
        Assert.That(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)), Is.Not.Null);
        var previewEntries = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovePreviews.asset")).FindProperty("m_SerializeEntries");
        Assert.That(Enumerable.Range(0, previewEntries.arraySize).Any(i => previewEntries.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == guid), Is.True);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)P(move, "PrefabPath")); Assert.That(prefab, Is.Not.Null);
        var effect = prefab.GetComponent(T("LucarioMoveEffect")); Assert.That(effect, Is.Not.Null);
        Assert.That(new SerializedObject(prefab.GetComponent(T("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue, Is.Not.Zero);
        var network = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        Assert.That(Enumerable.Range(0, network.arraySize).Any(i => network.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab), Is.True);
        foreach (string field in new[] { "graphics", "castVisual", "activeVisual", "impactVisual" }) Assert.That(new SerializedObject(effect).FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(prefab.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
        Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).All(m => AssetDatabase.Contains(m.sharedMesh)), Is.True);
        var registry = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset")).FindProperty("moveEntries");
        Assert.That(Enumerable.Range(0, registry.arraySize).Any(i => registry.GetArrayElementAtIndex(i).FindPropertyRelative("moveAsset").objectReferenceValue == asset), Is.True);
    }
    [Test]
    public void ProgressionAndExistingModelAreWired()
    {
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Lucario.asset")); var rows = data.FindProperty("learnableMoves");
        CollectionAssert.AreEqual(new[] { 0, 2, 4, 6, 8, 10, 12 }, Enumerable.Range(0, rows.arraySize).Select(i => rows.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue));
        Assert.That(rows.GetArrayElementAtIndex(5).FindPropertyRelative("isUpgraded").boolValue, Is.True);
        Assert.That(rows.GetArrayElementAtIndex(6).FindPropertyRelative("isUpgraded").boolValue, Is.True);
        Assert.That(data.FindProperty("passive").enumValueIndex, Is.EqualTo(Convert.ToInt32(E("AvailablePassives", "LucarioPassive"))));
        Assert.That(data.FindProperty("evolutions").arraySize, Is.EqualTo(1));
        Assert.That(data.FindProperty("evolutions").GetArrayElementAtIndex(0).FindPropertyRelative("newModel").objectReferenceValue, Is.Not.Null);
        var basic = T("BasicAttacksDatabase").GetMethod("GetBasicAttack").Invoke(null, new object[] { "Lucario" }); Assert.That(P(basic, "ServerResolved"), Is.True);
        Assert.That(T("PassiveDatabase").GetMethod("GetPassive").Invoke(null, new[] { E("AvailablePassives", "LucarioPassive") }).GetType().Name, Is.EqualTo("LucarioPassive"));
    }
    [TestCase(0, 3)]
    [TestCase(0.5f, 4.5f)]
    [TestCase(1, 6)]
    [TestCase(4, 6)]
    public void PunchRangeMatchesCharge(float held, float expected) => Assert.That((float)Rule("PunchRange", held), Is.EqualTo(expected).Within(0.001f));
    [TestCase(10000, true, 1000)]
    [TestCase(10000, false, 1200)]
    [TestCase(500, true, 60)]
    public void ExecuteCapIsOnlyForWildPokemon(int missing, bool wild, int expected) => Assert.That(Rule("ExecuteBonus", missing, wild), Is.EqualTo(expected));
    [TestCase(0, 88, 2.0916f)]
    [TestCase(1, 161, 3.818f)]
    public void PunchUsesDetailedUnchargedAndChargedFormulas(float held, short value, float ratio)
    {
        var damage = Rule("Punch", 1ul, held, 0, 0, false, false);
        Assert.That((float)F(damage, "ratio"), Is.EqualTo(ratio).Within(0.0001f)); Assert.That(F(damage, "baseDmg"), Is.EqualTo(value));
        Assert.That(F(Rule("Punch", 1ul, held, 0, 0, false, true), "multiplier"), Is.EqualTo(1.2f));
    }
    [TestCase(0, 0)]
    [TestCase(1, 34)]
    [TestCase(3, 102)]
    public void CombatHealingScalesPerEnemy(int count, int expected) => Assert.That(Rule("CombatHeal", 100f, count), Is.EqualTo(expected));
    [Test]
    public void PassiveAndExtremeHealingFormulas()
    {
        Assert.That(Rule("PassiveShield", 100f, 4), Is.EqualTo(500));
        Assert.That(Rule("ExtremeHeal", 100f, 4), Is.EqualTo(79));
        Assert.That(T("LucarioMoveRules").GetField("PassiveCooldown").GetValue(null), Is.EqualTo(45f));
        Assert.That(T("LucarioMoveRules").GetField("UniteEnergy").GetValue(null), Is.EqualTo(90000));
    }
    [Test]
    public void EffectIdsAvoidOtherLicensesAndRemainUnique()
    {
        var ids = T("LucarioEffectIds"); var acquired = new ushort[12];
        try
        {
            for (int i = 0; i < acquired.Length; i++) acquired[i] = (ushort)ids.GetMethod("Acquire").Invoke(null, null);
            Assert.That(acquired.Distinct().Count(), Is.EqualTo(acquired.Length));
            Assert.That(acquired.All(id => id >= 5000 && id < 6800), Is.True);
        }
        finally { foreach (var id in acquired) if (id != 0) ids.GetMethod("Release").Invoke(null, new object[] { id, 0f }); }
    }
    [Test]
    public void AllCombatAnimationsExistInSuppliedController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Model/Pokemons/Lucario/Lucario.controller");
        var names = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        foreach (string clip in new[] { "idle", "atk1", "atk2", "atk3", "atk4", "spell1", "spell1a", "spell1a2", "spell1a3", "spell1a5", "spell1b", "spell12", "spell3", "spell3a1", "spell3a2", "spell3a3", "spell3b2", "spell3b3", "spell3b4", "spell3b5", "spellg" })
            Assert.That(names, Does.Contain((string)Rule("Animation", clip)), clip);
    }
    [Test]
    public void AimUsesSuppliedNormalizedMeshAndGameplayDimensions()
    {
        var indicator = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Effects/Lucario/LineIndicator.prefab");
        Assert.That(indicator, Is.Not.Null);
        Assert.That(indicator.GetComponentsInChildren<MeshFilter>().Any(m => AssetDatabase.GetAssetPath(m.sharedMesh).Contains("Sprites/AimIndicator/pref_Indicator_Arrow_lv3")), Is.True);
        var materials = indicator.GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial).Distinct().ToArray();
        Assert.That(materials.Length, Is.EqualTo(2), "Both layers of the supplied arrow atlas must be retained.");
        foreach (var material in materials) Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo(1), "Black atlas backgrounds must not form opaque rectangles.");
        foreach (string name in new[] { "LucarioQuickAttack", "LucarioExtremeSpeed", "LucarioMeteorMash", "LucarioCloseCombat", "LucarioAuraCannon" })
        {
            var move = Activator.CreateInstance(T(name)); var scale = (Vector3)P(move, "IndicatorScale");
            Assert.That(scale.z, Is.EqualTo(P(move, "Range"))); Assert.That(P(move, "IndicatorRange"), Is.EqualTo(P(move, "Range")));
        }
    }
    [Test]
    public void AuraAcceleratesAndStopsAtAgreedRange()
    {
        var method = T("LucarioAuraCannonEffect").GetMethod("DistanceAt");
        float At(float t) => (float)method.Invoke(null, new object[] { t });
        Assert.That(At(0), Is.Zero); Assert.That(At(0.4f) - At(0.2f), Is.GreaterThan(At(0.2f)));
        Assert.That(At(20), Is.EqualTo(40));
    }
}
