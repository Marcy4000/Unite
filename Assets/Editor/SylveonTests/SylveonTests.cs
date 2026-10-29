using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SylveonTests
{
    private const string Folder = "Assets/Prefabs/Objects/Moves/Sylveon/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object EnumValue(string type, string name) => Enum.Parse(GameType(type), name);
    private static object Rule(string name, params object[] args) => GameType("SylvMoveRules").GetMethod(name).Invoke(null, args);
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name).GetValue(value);

    [TestCase("SylvMysticalCringe", "SylvMysticalFire", "SylvMysticalFire", 7)]
    [TestCase("SylvDrainingKiss", "SylvDrainingKiss", "SylvDrainingKiss", 9)]
    [TestCase("SylvBabyDollEyes", "SylvBabyDollEyes", "SylvBabyDollEyes", 5)]
    [TestCase("SylvCalmMind", "SylvCalmMind", "SylvCalmMind", 8)]
    [TestCase("SylveonUnite", "SylvUnite", "SylvUnite", 0)]
    public void FactoriesAssetsAndRegistryAgree(string id, string type, string assetName, int cooldown)
    {
        object moveId = EnumValue("AvailableMoves", id);
        var move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { moveId });
        Assert.That(move.GetType().Name, Is.EqualTo(type));
        Assert.That(Field<float>(move, "Cooldown"), Is.EqualTo(cooldown));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Sylveon/" + assetName + ".asset");
        var serialized = new SerializedObject(asset);
        Assert.That(serialized.FindProperty("move").longValue, Is.EqualTo(Convert.ToInt64(moveId)));
        Assert.That(serialized.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue, Is.Not.Empty);
        Assert.That(serialized.FindProperty("description").stringValue, Is.Not.Empty);
        if (id != "SylveonUnite") Assert.That(serialized.FindProperty("cooldown").floatValue, Is.EqualTo(cooldown));
        else Assert.That(serialized.FindProperty("uniteEnergyCost").intValue, Is.EqualTo(100000));
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(registry.GetType().GetMethod("GetMoveAsset").Invoke(registry, new[] { moveId }), Is.SameAs(asset));
    }

    [Test]
    public void SerializedMoveValuesArePreserved()
    {
        Assert.That(Convert.ToUInt32(EnumValue("AvailableMoves", "SylvMysticalCringe")), Is.EqualTo(17));
        Assert.That(Convert.ToUInt32(EnumValue("AvailableMoves", "SylvDrainingKiss")), Is.EqualTo(19));
        Assert.That(Convert.ToUInt32(EnumValue("AvailableMoves", "SylveonUnite")), Is.EqualTo(20));
    }

    [Test]
    public void BothBranchesAreLearnableAndUpgradeWithoutChangingHyperVoiceLevel()
    {
        var serialized = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Sylveon.asset"));
        var rows = serialized.FindProperty("learnableMoves");
        int[] levels = { 0, 1, 3, 5, 7, 9, 11 };
        Assert.That(rows.arraySize, Is.EqualTo(levels.Length));
        for (int i = 0; i < levels.Length; i++) Assert.That(rows.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue, Is.EqualTo(levels[i]));
        foreach (int row in new[] { 2, 5 }) AssertChoices(rows.GetArrayElementAtIndex(row), "SylvHyperVoice", "SylvMysticalFire");
        foreach (int row in new[] { 3, 6 }) AssertChoices(rows.GetArrayElementAtIndex(row), "SylvCalmMind", "SylvDrainingKiss");
        Assert.That(rows.GetArrayElementAtIndex(5).FindPropertyRelative("isUpgraded").boolValue, Is.True);
        Assert.That(rows.GetArrayElementAtIndex(6).FindPropertyRelative("isUpgraded").boolValue, Is.True);
    }
    private static void AssertChoices(SerializedProperty row, params string[] names)
    {
        var choices = row.FindPropertyRelative("moves");
        Assert.That(choices.arraySize, Is.EqualTo(names.Length));
        for (int i = 0; i < names.Length; i++) Assert.That(choices.GetArrayElementAtIndex(i).objectReferenceValue.name, Is.EqualTo(names[i]));
    }

    [TestCase(false, 4)]
    [TestCase(true, 5)]
    public void FireUpgradeAddsOneFlame(bool upgraded, int count) { Assert.That(Rule("FlameCount", upgraded), Is.EqualTo(count)); }

    [Test]
    public void FireRepeatScalingDoesNotRoundSliderOrBaseBeforeCalculating()
    {
        object normal = Rule("FireDamage", 12ul, false), repeat = Rule("FireDamage", 12ul, true);
        Assert.That(Field<float>(normal, "ratio"), Is.EqualTo(0.8f));
        Assert.That(Field<short>(repeat, "slider"), Is.EqualTo(13));
        Assert.That(Field<short>(repeat, "baseDmg"), Is.EqualTo(482));
        Assert.That(Field<float>(repeat, "multiplier"), Is.EqualTo(0.28f));
        Assert.That(Field<ulong>(repeat, "attackerId"), Is.EqualTo(12ul));
        var actorObject = new GameObject("Sylveon damage formula actor");
        var targetObject = new GameObject("Sylveon damage formula target");
        var stats = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Sylveon.asset");
        try
        {
            var actor = actorObject.AddComponent(GameType("Pokemon")); var target = targetObject.AddComponent(GameType("Pokemon"));
            foreach (var pokemon in new[] { actor, target })
            {
                if (GameType("Pokemon").GetField("statChanges", Private).GetValue(pokemon) == null)
                    GameType("Pokemon").GetMethod("Awake", Private).Invoke(pokemon, null);
                GameType("Pokemon").GetField("baseStats", Private).SetValue(pokemon, stats);
            }
            var calculate = GameType("Pokemon").GetMethod("CalculateDamage", new[] { GameType("DamageInfo"), GameType("Pokemon") });
            int damage = (int)calculate.Invoke(target, new[] { normal, actor });
            int reduced = (int)calculate.Invoke(target, new[] { repeat, actor });
            Assert.That(reduced, Is.EqualTo(Mathf.FloorToInt(damage * 0.28f)).Within(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(actorObject); UnityEngine.Object.DestroyImmediate(targetObject); }
    }

    [TestCase(false, 0.23f, 190)]
    [TestCase(true, 0.345f, 285)]
    public void KissHealingUpgradeMatchesReference(bool upgraded, float ratio, int flat)
    {
        var heal = Rule("KissHeal", 12ul, upgraded);
        Assert.That(Field<float>(heal, "ratio"), Is.EqualTo(ratio));
        Assert.That(Field<short>(heal, "baseDmg"), Is.EqualTo(flat));
        Assert.That(Field<short>(heal, "slider"), Is.Zero);
        Assert.That(Field<object>(heal, "proprieties").ToString(), Does.Contain("IsHeal"));
    }

    [TestCase("None", true)]
    [TestCase("IsUniteMove", true)]
    [TestCase("IsBasicAttack", false)]
    [TestCase("IsHeal", false)]
    [TestCase("IsMuscleBand", false)]
    public void CalmMindBlocksMovesButNotAttacksOrItemProcs(string flags, bool expected)
    { Assert.That(Rule("CanBlock", EnumValue("DamageProprieties", flags)), Is.EqualTo(expected)); }

    [TestCase(0, 100)]
    [TestCase(1000, 1000)]
    public void CalmMindShieldMatchesReference(int spAttack, int amount) { Assert.That(Rule("CalmShieldAmount", spAttack), Is.EqualTo(amount)); }

    [TestCase("SylvMysticalFire")]
    [TestCase("SylvDrainingKiss")]
    [TestCase("SylvBabyDollEyes")]
    [TestCase("SylvCalmMind")]
    [TestCase("SylvUnite")]
    public void UninitializedCancelAndResetAreSafe(string name)
    {
        var move = Activator.CreateInstance(GameType(name));
        move.GetType().GetMethod("Cancel").Invoke(move, null);
        move.GetType().GetMethod("ResetMove").Invoke(move, null);
        move.GetType().GetMethod("ResetMove").Invoke(move, null);
        Assert.That(Field<bool>(move, "IsActive"), Is.False);
        Assert.That(Field<bool>(move, "wasMoveSuccessful"), Is.False);
    }

    [TestCase("SylvMysticalFire", "SylvMysticalFireEffect")]
    [TestCase("SylvDrainingKiss", "SylvDrainingKissEffect")]
    [TestCase("SylvBabyDollEyes", "HomingMoveEffect")]
    [TestCase("SylvCalmMind", "SylvCalmMindEffect")]
    [TestCase("SylvFairyFrolic", "SylvFairyFrolicEffect")]
    [TestCase("SylvBasicAttack", "HomingProjectile")]
    [TestCase("SylvBoostedAttack", "HomingProjectile")]
    public void PrefabsHaveBakedArtAndUniqueNetworkAndAddressableRegistrations(string name, string component)
    {
        string path = Folder + name + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        Assert.That(prefab.GetComponent(GameType(component)), Is.Not.Null);
        var network = prefab.GetComponent(GameType("Unity.Netcode.NetworkObject"));
        long hash = new SerializedObject(network).FindProperty("GlobalObjectIdHash").longValue;
        Assert.That(hash, Is.Not.Zero);
        Assert.That(File.ReadAllText(path), Does.Contain("GlobalObjectIdHash: " + hash));
        Assert.That(prefab.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform")), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), Is.True);
        foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.Not.Empty);
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            { Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Does.Not.Contain("Error")); }
        var networkEntries = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset")).FindProperty("List");
        int matches = 0;
        for (int i = 0; i < networkEntries.arraySize; i++)
            if (networkEntries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab) matches++;
        Assert.That(matches, Is.EqualTo(1));
        var addresses = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset")).FindProperty("m_SerializeEntries");
        matches = 0;
        for (int i = 0; i < addresses.arraySize; i++)
        {
            var entry = addresses.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("m_GUID").stringValue != AssetDatabase.AssetPathToGUID(path)) continue;
            matches++; Assert.That(entry.FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path));
        }
        Assert.That(matches, Is.EqualTo(1));
    }

    [Test]
    public void PrefabReferencesAndBoostedImpactBuffAreWired()
    {
        var fire = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylvMysticalFire.prefab").GetComponent(GameType("SylvMysticalFireEffect")));
        var flames = fire.FindProperty("flames"); Assert.That(flames.arraySize, Is.EqualTo(5));
        for (int i = 0; i < flames.arraySize; i++) Assert.That(flames.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
        var calm = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylvCalmMind.prefab").GetComponent(GameType("SylvCalmMindEffect")));
        foreach (string field in new[] { "graphics", "guard", "shield" }) Assert.That(calm.FindProperty(field).objectReferenceValue, Is.Not.Null);
        var frolic = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylvFairyFrolic.prefab").GetComponent(GameType("SylvFairyFrolicEffect")));
        foreach (string field in new[] { "graphics", "airborne", "landing", "buff", "landingArea", "landingWarning" }) Assert.That(frolic.FindProperty(field).objectReferenceValue, Is.Not.Null);
        var hitbox = (SphereCollider)frolic.FindProperty("landingArea").objectReferenceValue;
        Assert.That(hitbox.radius, Is.EqualTo(5)); Assert.That(hitbox.enabled, Is.False); Assert.That(hitbox.isTrigger, Is.True);
        var boosted = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylvBoostedAttack.prefab").GetComponent(GameType("HomingProjectile")));
        var stats = boosted.FindProperty("attackerOnHitStats"); Assert.That(stats.arraySize, Is.EqualTo(1));
        var buff = stats.GetArrayElementAtIndex(0);
        Assert.That(buff.FindPropertyRelative("Amount").intValue, Is.EqualTo(75));
        Assert.That(buff.FindPropertyRelative("Duration").floatValue, Is.EqualTo(1));
        Assert.That(buff.FindPropertyRelative("IsBuff").boolValue, Is.True);
    }

    [Test]
    public void SwiftHasALargerHitboxWithoutEnlargingItsGraphics()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylvSwift.prefab");
        var collider = prefab.GetComponent<SphereCollider>();
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one * 0.4f));
        Assert.That(collider.radius * prefab.transform.localScale.x, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(collider.enabled && collider.isTrigger, Is.True);
    }

    [Test]
    public void UniteWarningReusesTheLegacyCircleAndMatchesTheDamageRadius()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylvFairyFrolic.prefab");
        var effect = new SerializedObject(prefab.GetComponent(GameType("SylvFairyFrolicEffect")));
        var warning = (GameObject)effect.FindProperty("landingWarning").objectReferenceValue;
        var area = (SphereCollider)effect.FindProperty("landingArea").objectReferenceValue;
        Assert.That(warning.activeSelf, Is.False, "Shown only during the airborne wind-up.");
        Assert.That(warning.GetComponentsInChildren<Collider>(true), Is.Empty);
        var sprites = warning.GetComponentsInChildren<SpriteRenderer>(true);
        var legacy = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SylveonUnite.prefab");
        var originalSprites = legacy.transform.Find("GroundCircle").GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(sprites.Length, Is.EqualTo(originalSprites.Length));
        Assert.That(sprites.All(s => s.sprite == originalSprites[0].sprite), Is.True);
        Bounds bounds = sprites[0].bounds;
        foreach (var sprite in sprites) bounds.Encapsulate(sprite.bounds);
        Assert.That(bounds.size.x, Is.EqualTo(area.radius * 2).Within(0.01f));
        Assert.That(bounds.size.z, Is.EqualTo(area.radius * 2).Within(0.01f));
        Assert.That(warning.transform.localPosition.y, Is.EqualTo(0.05f).Within(0.001f));
    }

    [Test]
    public void HyperVoiceKeepsItsExistingDamageAndWaveUpgrade()
    {
        var move = Activator.CreateInstance(GameType("SylvHyperVoice"));
        Assert.That(Field<float>(move, "Cooldown"), Is.EqualTo(7));
        Assert.That(GameType("SylvHyperVoice").GetField("wavesAmountNormal", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue(), Is.EqualTo(6));
        Assert.That(GameType("SylvHyperVoice").GetField("wavesAmountUpgraded", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue(), Is.EqualTo(7));
        Assert.That(Field<float>(GameType("SylvHyperVoice").GetField("closeDamage", Private).GetValue(move), "ratio"), Is.EqualTo(0.32f));
        Assert.That(Field<float>(GameType("SylvHyperVoice").GetField("farDamage", Private).GetValue(move), "ratio"), Is.EqualTo(0.54f));
    }
}
