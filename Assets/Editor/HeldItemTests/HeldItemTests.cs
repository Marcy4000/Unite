using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class HeldItemTests
{
    [Serializable] private class Reference { public int level; public Item[] items; }
    [Serializable] private class Item { public int id; public string kind; public string name; public Boost[] stats; public string iconGuid; }
    [Serializable] private class Boost { public int stat; public float amount; }
    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Kind(string name) => Enum.Parse(Find("AvailableHeldItems"), name);
    private static object Rule(string name, params object[] arguments) => Find("HeldItemRules").GetMethod(name).Invoke(null, arguments);
    private static Reference Data => JsonUtility.FromJson<Reference>(File.ReadAllText("Assets/Editor/HeldItemTests/Level30Items.json"));

    [Test]
    public void EveryEquippableItemHasAnExistingIconFactoryAndExactLevel30Stats()
    {
        Assert.That(Data.level, Is.EqualTo(30));
        Assert.That(Enum.GetValues(Find("AvailableHeldItems")).Length, Is.EqualTo(Data.items.Length + 1));
        foreach (Item item in Data.items)
        {
            object kind = Kind(item.kind);
            Assert.That(Convert.ToInt32(kind), Is.EqualTo(item.id), item.kind);
            object runtime = Find("HeldItemDatabase").GetMethod("GetHeldItem").Invoke(null, new[] { kind });
            Assert.That(Find("HeldItemRuntime").IsInstanceOfType(runtime), Is.True, item.kind);
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/HeldItems/" + item.kind + ".asset");
            Assert.That(asset, Is.Not.Null, item.kind);
            var serialized = new SerializedObject(asset);
            Assert.That(serialized.FindProperty("heldItemID").intValue, Is.EqualTo(item.id));
            var icon = serialized.FindProperty("icon").objectReferenceValue;
            Assert.That(icon, Is.Not.Null, item.kind);
            Assert.That(AssetDatabase.GetAssetPath(icon), Does.StartWith("Assets/Sprites/Items/HeldItems/"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(icon)), Is.EqualTo(item.iconGuid));
            var stats = serialized.FindProperty("statBoosts");
            Assert.That(stats.arraySize, Is.EqualTo(item.stats.Length), item.kind);
            for (int index = 0; index < stats.arraySize; index++)
            {
                var boost = stats.GetArrayElementAtIndex(index);
                Assert.That(boost.FindPropertyRelative("AffectedStat").intValue, Is.EqualTo(item.stats[index].stat), item.kind);
                Assert.That(boost.FindPropertyRelative("BoostAmount").floatValue, Is.EqualTo(item.stats[index].amount).Within(0.0001), item.kind);
                Assert.That(boost.FindPropertyRelative("IsPercentage").boolValue, Is.False, item.kind);
            }
        }
    }

    [TestCase("BootScene")]
    [TestCase("PhotoMaker")]
    public void SelectionCatalogPreservesEveryNetworkIndex(string scene)
    {
        string text = File.ReadAllText("Assets/Scenes/" + scene + ".unity");
        string catalog = text.Split(new[] { "  heldItems:\n" }, StringSplitOptions.None)[1].Split(new[] { "  maps:" }, StringSplitOptions.None)[0];
        string[] entries = catalog.Split('\n').Where(line => line.Contains("guid:")).ToArray();
        Assert.That(entries.Length, Is.EqualTo(Data.items.Length + 1));
        foreach (Item item in Data.items)
            Assert.That(entries[item.id], Does.Contain(AssetDatabase.AssetPathToGUID("Assets/HeldItems/" + item.kind + ".asset")), item.kind);
    }

    [TestCase(1000, true, 30)]
    [TestCase(20000, true, 360)]
    [TestCase(20000, false, 600)]
    [TestCase(0, false, 0)]
    public void MuscleBandUsesRemainingHealthAndOnlyCapsPlayers(int health, bool player, int expected)
        => Assert.That(Rule("MuscleDamage", health, player), Is.EqualTo(expected));

    [TestCase(5000, 1000, 480)]
    [TestCase(5000, 4000, 120)]
    [TestCase(5000, 6000, 0)]
    public void FocusBandUsesMissingHealth(int maximum, int current, int expected)
        => Assert.That(Rule("FocusHealing", maximum, current), Is.EqualTo(expected));

    [TestCase(0, 80)]
    [TestCase(100, 140)]
    public void ShellBellMatchesReference(int specialAttack, int expected)
        => Assert.That(Rule("ShellHealing", specialAttack), Is.EqualTo(expected));

    [TestCase(5000, 0, 530)]
    [TestCase(5000, 14, 950)]
    public void ScoreShieldUsesOneBasedPokemonLevel(int hp, int level, int expected)
        => Assert.That(Rule("ScoreShield", hp, level), Is.EqualTo(expected));

    [Test]
    public void FractionalStatsSurviveValueIdentity()
    {
        object stat = Enum.Parse(Find("Stat"), "Cdr");
        object change = Activator.CreateInstance(Find("StatChange"), 4.5f, stat, 0f, false, true, false, (ushort)123, false);
        Assert.That(change.GetType().GetProperty("EffectiveAmount").GetValue(change), Is.EqualTo(4.5f));
        object integral = Activator.CreateInstance(Find("StatChange"), (short)4, stat, 0f, false, true, false, (ushort)123, false);
        Assert.That(change.Equals(integral), Is.False);
    }

    [Test]
    public void UntrustedLoadoutsPreserveThreeSlotsWithoutDuplicatePassives()
    {
        Type listType = Find("CharactersList");
        FieldInfo singleton = listType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        var root = new GameObject("Held item catalog bounds"); root.SetActive(false);
        try
        {
            Component list = root.AddComponent(listType);
            Array catalog = Array.CreateInstance(Find("HeldItemInfo"), Data.items.Length + 1);
            catalog.SetValue(AssetDatabase.LoadMainAssetAtPath("Assets/HeldItems/EmptyHeldItem.asset"), 0);
            foreach (Item item in Data.items) catalog.SetValue(AssetDatabase.LoadMainAssetAtPath("Assets/HeldItems/" + item.kind + ".asset"), item.id);
            listType.GetField("heldItems", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(list, catalog);
            singleton.SetValue(null, list);
            foreach (int id in new[] { -1, catalog.Length, int.MaxValue })
                Assert.That(listType.GetMethod("GetHeldItemByID").Invoke(list, new object[] { id }), Is.Null);
            MethodInfo deserialize = Find("HeldItemDatabase").GetMethod("DeserializeHeldItems");
            foreach (string encoded in new[] { null, "", "not base64!", new string('A', 1000), Convert.ToBase64String(new byte[] { 255, 255, 255 }) })
            {
                var items = (IList)deserialize.Invoke(null, new object[] { encoded });
                Assert.That(items.Count, Is.EqualTo(3));
                foreach (object item in items) Assert.That(item, Is.SameAs(catalog.GetValue(0)));
            }
            var duplicates = (IList)deserialize.Invoke(null, new object[] { Convert.ToBase64String(new byte[] { 1, 1, 1, 2 }) });
            Assert.That(duplicates.Count, Is.EqualTo(3));
            Assert.That(duplicates[0], Is.SameAs(catalog.GetValue(1)));
            Assert.That(duplicates[1], Is.SameAs(catalog.GetValue(0)));
            Assert.That(duplicates[2], Is.SameAs(catalog.GetValue(0)));
        }
        finally { singleton.SetValue(null, previous); UnityEngine.Object.DestroyImmediate(root); }
    }
}
