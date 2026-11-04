using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class NinetalesTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object Rule(string method, params object[] args) => T("NinetalesMoveRules").GetMethod(method).Invoke(null, args);
    private static V Field<V>(object target, string name) => (V)target.GetType().GetField(name).GetValue(target);
    private static float Constant(string name) => (float)T("NinetalesMoveRules").GetField(name).GetRawConstantValue();
    private static string Age(string script) => File.ReadAllText("Assets/Moves/Dumps/age-named-v2/3c44b23/age_new_037_" + script + "_pbb.bin.md");
    private static float AgeDistance(string script, string field)
    {
        var match = Regex.Match(Age(script), @"(?m)^    " + field + @" \[f\d+\] = (\d+);");
        Assert.That(match.Success, Is.True, "Missing AGE distance " + script + "/" + field);
        return int.Parse(match.Groups[1].Value) / 1000f;
    }
    private static float AgeBoxSize(string script, string axis)
    {
        var match = Regex.Match(Age(script), @"(?ms)^    MSize \[f\d+\] = 15 bytes;.*?^        " + axis + @" \[f\d+\] = (\d+);");
        Assert.That(match.Success, Is.True, "Missing decoded AGE box size " + script + "/" + axis);
        return int.Parse(match.Groups[1].Value) / 1000f;
    }

    [TestCase("PassiveRadius", "P1B1")]
    [TestCase("VeilRadius", "S22B1")]
    [TestCase("FrostRadius", "S21B1")]
    public void CollisionRadiiMatchMetadataBackedAgeFields(string rule, string script)
        => Assert.That(Constant(rule), Is.EqualTo(AgeDistance(script, "MRadius")));

    [Test]
    public void RectangularGeometryUsesAgeCollisionSizesNotIndicatorWidths()
    {
        foreach (string script in new[] { "A1B2", "A1B4" })
        {
            Assert.That(Age(script), Does.Contain("MShapType [f35475] = 2; enum Cube"));
            Assert.That(Constant("BoostedWidth"), Is.EqualTo(AgeBoxSize(script, "X")));
            Assert.That(Constant("BoostedLength"), Is.EqualTo(AgeBoxSize(script, "Z")));
        }
        Assert.That(Constant("AvalancheWidth"), Is.EqualTo(AgeBoxSize("S11B1", "X")));
        Assert.That(Constant("IcyCollisionWidth"), Is.EqualTo(AgeBoxSize("S2B1", "X")));
        Assert.That(Constant("IcyWidth"), Is.EqualTo(2.5f), "Keep the databin aiming width separate from collision width.");
    }
    [TestCase("NinetalesPowderSnow", 5, "Direction")]
    [TestCase("NinetalesIcyWind", 8, "Direction")]
    [TestCase("NinetalesAvalanche", 7, "Ground")]
    [TestCase("NinetalesDazzlingGleam", 7, "Direction")]
    [TestCase("NinetalesBlizzard", 7, "Direction")]
    [TestCase("NinetalesAuroraVeil", 12, "Dash")]
    [TestCase("NinetalesSnowGlobe", 0, "Circle")]
    public void CompleteMoveAssetsAndFactories(string name, float cooldown, string aim)
    {
        var id = E("AvailableMoves", name); var move = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(move.GetType().Name, Is.EqualTo(name)); Assert.That(Field<float>(move, "Cooldown"), Is.EqualTo(cooldown));
        Assert.That(move.GetType().GetProperty("AimStyle").GetValue(move).ToString(), Is.EqualTo(aim));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/AlolanNinetales/" + name + ".asset"); Assert.That(asset, Is.Not.Null);
        var serialized = new SerializedObject(asset);
        Assert.That(serialized.FindProperty("move").longValue, Is.EqualTo(Convert.ToInt64(id)));
        Assert.That(serialized.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        string preview = serialized.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue;
        Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(preview)), Is.Not.Null);
        Assert.That(serialized.FindProperty("description").stringValue, Is.Not.Empty);
        Assert.That(serialized.FindProperty("cooldown").floatValue, Is.EqualTo(cooldown));
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(registry.GetType().GetMethod("GetMoveAsset").Invoke(registry, new[] { id }), Is.SameAs(asset));
        if (name == "NinetalesSnowGlobe") Assert.That(serialized.FindProperty("uniteEnergyCost").intValue, Is.EqualTo(100000));
        if (name == "NinetalesAuroraVeil") Assert.That(move.GetType().GetMethod("EffectiveCooldown").Invoke(move, new object[] { true }), Is.EqualTo(10f));
        move.GetType().GetMethod("ResetMove").Invoke(move, null);
    }
    [Test]
    public void LearnsetEvolutionAndExistingValuesArePreserved()
    {
        var pokemon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/AlolanNinetales.asset"));
        Assert.That(pokemon.FindProperty("passive").intValue, Is.EqualTo(Convert.ToInt32(E("AvailablePassives", "NinetalesPassive"))));
        int[] levels = { 0, 2, 3, 5, 8, 10, 12 }; var rows = pokemon.FindProperty("learnableMoves"); Assert.That(rows.arraySize, Is.EqualTo(7));
        for (int i = 0; i < levels.Length; i++) { var row = rows.GetArrayElementAtIndex(i); Assert.That(row.FindPropertyRelative("level").intValue, Is.EqualTo(levels[i])); Assert.That(row.FindPropertyRelative("isUpgraded").boolValue, Is.EqualTo(i >= 5)); Assert.That(row.FindPropertyRelative("moves").arraySize, Is.EqualTo(i == 4 ? 1 : 2)); }
        Assert.That(pokemon.FindProperty("evolutions").GetArrayElementAtIndex(1).FindPropertyRelative("level").intValue, Is.EqualTo(3));
        Assert.That(Convert.ToInt32(E("AvailableMoves", "AbsolMidnightSlash")), Is.EqualTo(104));
        Assert.That(Convert.ToInt32(E("StatusType", "BlueBuff")), Is.EqualTo(15));
        Assert.That(T("BasicAttacksDatabase").GetMethod("GetBasicAttack").Invoke(null, new object[] { "Alolan Ninetales" }).GetType().Name, Is.EqualTo("NinetalesBasicAtk"));
        Assert.That(T("PassiveDatabase").GetMethod("GetPassive").Invoke(null, new[] { E("AvailablePassives", "NinetalesPassive") }).GetType().Name, Is.EqualTo("NinetalesPassive"));
    }
    [Test]
    public void SnowStacksRefreshExpireAndConsumeExactlyFourHits()
    {
        var state = Activator.CreateInstance(T("NinetalesSnowStacks")); var add = state.GetType().GetMethod("Add");
        Assert.That(add.Invoke(state, new object[] { 0f }), Is.False);
        Assert.That(add.Invoke(state, new object[] { 3f }), Is.False);
        Assert.That(state.GetType().GetProperty("Expires").GetValue(state), Is.EqualTo(7f));
        Assert.That(add.Invoke(state, new object[] { 3.1f }), Is.False); Assert.That(add.Invoke(state, new object[] { 3.2f }), Is.True);
        Assert.That(state.GetType().GetProperty("Count").GetValue(state), Is.EqualTo(0));
        Assert.That(add.Invoke(state, new object[] { 3.3f }), Is.False); Assert.That(add.Invoke(state, new object[] { 7.3f }), Is.False);
        Assert.That(state.GetType().GetProperty("Count").GetValue(state), Is.EqualTo(1));
    }
    [TestCase("PowderDamage", 0.78f, 13, 240)]
    [TestCase("IcyDamage", 0.61f, 7, 240)]
    [TestCase("AvalancheDamage", 0.44f, 10, 229)]
    [TestCase("PassiveDamage", 0.6f, 0, 100)]
    public void SpecialFormulasMatchDetailedNotes(string name, float ratio, short slider, short flat)
    {
        var damage = Rule(name, 7ul); Assert.That(Field<float>(damage, "ratio"), Is.EqualTo(ratio)); Assert.That(Field<short>(damage, "slider"), Is.EqualTo(slider)); Assert.That(Field<short>(damage, "baseDmg"), Is.EqualTo(flat)); Assert.That(Field<object>(damage, "type").ToString(), Is.EqualTo("Special")); Assert.That(Field<object>(damage, "proprieties").ToString(), Does.Not.Contain("CanCrit"));
    }
    [Test]
    public void BoostedUniteUpgradesAndHealingUseCorrectFormulas()
    {
        var boosted = Rule("BasicDamage", 7ul, true); Assert.That(Field<float>(boosted, "ratio"), Is.EqualTo(0.5746f)); Assert.That(Field<object>(boosted, "proprieties").ToString(), Does.Contain("CanCrit"));
        var normal = Rule("BasicDamage", 7ul, false); Assert.That(Field<object>(normal, "type").ToString(), Is.EqualTo("Physical"));
        var frozen = Rule("UniteDamage", 7ul, true); var unfrozen = Rule("UniteDamage", 7ul, false);
        Assert.That(Field<float>(frozen, "ratio"), Is.EqualTo(2.42f)); Assert.That(Field<float>(unfrozen, "ratio"), Is.EqualTo(1.59f)); Assert.That(Field<object>(frozen, "proprieties").ToString(), Is.EqualTo("IsUniteMove"));
        var first = Rule("BlizzardDamage", 7ul, true, 0); var second = Rule("BlizzardDamage", 7ul, true, 1); var tick = Rule("BlizzardDamage", 7ul, true, 2);
        Assert.That(Field<float>(second, "ratio"), Is.EqualTo(Field<float>(first, "ratio") * 2)); Assert.That(Field<short>(tick, "baseDmg"), Is.EqualTo(115));
        Assert.That(Rule("VeilHeal", 100f, 12), Is.EqualTo(74));
    }
    [TestCase("NinetalesPowderSnow")][TestCase("NinetalesIcyWind")][TestCase("NinetalesAvalanche")][TestCase("NinetalesDazzlingGleam")]
    [TestCase("NinetalesBlizzard")][TestCase("NinetalesAuroraVeil")][TestCase("NinetalesSnowGlobe")][TestCase("NinetalesBasicProjectile")]
    public void PrefabsHaveSavedUniqueHashesRegistrationsAndEditableArt(string name)
    {
        string path = "Assets/Prefabs/Objects/Moves/AlolanNinetales/" + name + ".prefab"; var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent(T("PrefabMoveEffect")), Is.Not.Null); Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), Is.True);
        Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        long hash = new SerializedObject(prefab.GetComponent(T("Unity.Netcode.NetworkObject"))).FindProperty("GlobalObjectIdHash").longValue;
        Assert.That(hash, Is.Not.Zero); Assert.That(File.ReadAllText(path), Does.Contain("GlobalObjectIdHash: " + hash));
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.Not.Empty);
        var list = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        int matches = 0; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) matches++; Assert.That(matches, Is.EqualTo(1));
        var entries = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset")).FindProperty("m_SerializeEntries"); matches = 0;
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == AssetDatabase.AssetPathToGUID(path)) matches++;
        Assert.That(matches, Is.EqualTo(1));
    }
    [Test]
    public void WallIsPhysicalAndUsesTheAgreedDimensions()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/AlolanNinetales/NinetalesAvalanche.prefab");
        var wall = prefab.GetComponentInChildren<BoxCollider>(true); Assert.That(wall.isTrigger, Is.False); Assert.That(wall.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Wall"))); Assert.That(wall.size, Is.EqualTo(new Vector3(4, 2.5f, 0.6f))); Assert.That(wall.GetComponent<UnityEngine.AI.NavMeshObstacle>().carving, Is.True);
        Assert.That(wall.includeLayers.value & LayerMask.GetMask("Players", "WildPokemons"), Is.EqualTo(LayerMask.GetMask("Players", "WildPokemons")));
        Assert.That(wall.layerOverridePriority, Is.GreaterThan(0));
    }
    [TestCase("NinetalesCone")][TestCase("NinetalesWind")]
    public void ImportedAimIndicatorsRetainTheirFaceCorrection(string name)
    {
        var prefab = Resources.Load<GameObject>("AimIndicators/" + name); Assert.That(prefab, Is.Not.Null);
        var sizing = prefab.transform.Find("NormalizedFootprint"); Assert.That(sizing, Is.Not.Null); Assert.That(sizing.GetChild(0).localScale.y, Is.EqualTo(-1));
        Assert.That(prefab.GetComponentsInChildren<MeshFilter>().Any(f => AssetDatabase.GetAssetPath(f.sharedMesh).EndsWith(".fbx")), Is.True);
    }
    [Test]
    public void BothModelsSupplyTheActualAnimationStatesAndNoRootMotion()
    {
        foreach (string model in new[] { "AlolanVulpix", "AlolanNinetales" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/AlolanNinetales/" + model + ".prefab"); var animator = prefab.GetComponentInChildren<Animator>(); Assert.That(animator.applyRootMotion, Is.False);
            var clips = animator.runtimeAnimatorController.animationClips.Select(c => c.name).ToArray();
            string[] actions = model == "AlolanVulpix" ? new[] { "atk1", "atk2", "spell1", "spell2", "idle" } : new[] { "atk1", "atk3", "spell1", "spell1a", "spell1b", "spell2", "spell2a", "spell2b", "spellg", "idle" };
            foreach (var action in actions) Assert.That(clips, Does.Contain(Rule("Animation", action, model == "AlolanNinetales")));
        }
    }

    [Test]
    public void PassiveWarningHasAnAuthoredBoundaryAndSynchronizedOneSecondBuildup()
    {
        var prefab = Resources.Load<GameObject>("Effects/AlolanNinetales/SnowWarning"); Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.Find("WarningBoundary").GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        Assert.That(prefab.transform.Find("WarningBuildup").GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        Assert.That(prefab.GetComponent(T("NinetalesPassiveWarningView")), Is.Not.Null);
        var root = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var view = root.GetComponent(T("NinetalesPassiveWarningView"));
            CallWarning(view, 0f); Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(2, 1, 2)));
            Assert.That(root.transform.Find("WarningBuildup").localScale.x, Is.EqualTo(0).Within(0.001f));
            CallWarning(view, 0.5f); Assert.That(root.transform.Find("WarningBuildup").localScale.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(root.transform.Find("WarningBoundary").localScale, Is.EqualTo(Vector3.one), "The outer boundary must remain fixed while its fill grows.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Assert.That((float)T("NinetalesMoveRules").GetField("PassiveWarningDuration").GetRawConstantValue(), Is.EqualTo(1f));
    }
    private static void CallWarning(Component view, float elapsed) => view.GetType().GetMethod("Initialize").Invoke(view, new object[] { elapsed, 1f, Constant("PassiveRadius") });
}
