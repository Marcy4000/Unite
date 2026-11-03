using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BattleItemTests
{
    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Kind(string name) => Enum.Parse(Find("AvailableBattleItems"), name);
    private static object Rule(string method, params object[] arguments) => Find("BattleItemRules").GetMethod(method).Invoke(null, arguments);

    [TestCase("Eject", "EjectButton", 80)]
    [TestCase("XSpeed", "XSpeed", 50)]
    [TestCase("XAttack", "XAttack", 40)]
    [TestCase("FullHeal", "FullHeal", 60)]
    [TestCase("GoalGetter", "GoalGetter", 60)]
    [TestCase("Potion", "Potion", 30)]
    [TestCase("SlowSmoke", "SlowSmoke", 35)]
    [TestCase("ShedinjaDoll", "ShedinjaDoll", 45)]
    [TestCase("FluffyTail", "FluffyTail", 22)]
    [TestCase("GoalHacker", "GoalHacker", 80)]
    [TestCase("Brick", "BrickLol", 55)]
    public void EveryItemHasTheCorrectFactoryAndCooldownBeforeFirstUse(string kind, string className, float cooldown)
    {
        object item = Find("BattleItemDatabase").GetMethod("GetBattleItem").Invoke(null, new[] { Kind(kind) });
        Assert.That(item.GetType().Name, Is.EqualTo(className));
        Assert.That(item.GetType().GetField("Cooldown").GetValue(item), Is.EqualTo(cooldown));
        Assert.That(item.GetType().GetField("IsActive").GetValue(item), Is.False);
        Assert.That(Rule("Cooldown", Kind(kind)), Is.EqualTo(cooldown));
    }

    [TestCase(0, 160)]
    [TestCase(5000, 1160)]
    [TestCase(10000, 2160)]
    public void PotionMatchesReference(int hp, int expected) => Assert.That(Rule("PotionHealing", hp), Is.EqualTo(expected));

    [TestCase(100, 100, 1, 260)]
    [TestCase(300, 500, 15, 840)]
    public void TailMatchesReference(int attack, int specialAttack, int level, int expected)
        => Assert.That(Rule("TailDamage", attack, specialAttack, level), Is.EqualTo(expected));

    [Test]
    public void NonFiniteNetworkInputIsRejected()
    {
        Assert.That(Rule("Finite", Vector3.zero), Is.True);
        Assert.That(Rule("Finite", new Vector3(float.NaN, 0, 0)), Is.False);
        Assert.That(Rule("Finite", new Vector3(0, float.PositiveInfinity, 0)), Is.False);
        Assert.That(Rule("Finite", new Vector3(0, 0, float.NegativeInfinity)), Is.False);
    }

    [Test]
    public void FinishingTwiceDoesNotPublishTwiceAndCancelClearsSuccess()
    {
        object item = Activator.CreateInstance(Find("EmptyBattleItem"));
        int notifications = 0;
        item.GetType().GetEvent("onBattleItemOver").AddEventHandler(item, (Action)(() => notifications++));
        item.GetType().GetField("IsActive").SetValue(item, true);
        item.GetType().GetField("wasUseSuccessful").SetValue(item, true);
        item.GetType().GetMethod("Finish").Invoke(item, null);
        item.GetType().GetMethod("Finish").Invoke(item, null);
        Assert.That(notifications, Is.EqualTo(1));
        item.GetType().GetMethod("Cancel").Invoke(item, null);
        Assert.That(item.GetType().GetField("wasUseSuccessful").GetValue(item), Is.False);
    }

    [TestCase("BootScene")]
    [TestCase("PhotoMaker")]
    public void SelectionCatalogContainsEveryItemAtItsStableNetworkId(string scene)
    {
        string text = File.ReadAllText("Assets/Scenes/" + scene + ".unity");
        string catalog = text.Split(new[] { "  battleItems:\n" }, StringSplitOptions.None)[1].Split(new[] { "  heldItems:" }, StringSplitOptions.None)[0];
        string[] entries = catalog.Split('\n').Where(line => line.Contains("guid:")).ToArray();
        Assert.That(entries.Length, Is.EqualTo(12));
        foreach (string kind in new[] { "Eject", "XSpeed", "XAttack", "FullHeal", "GoalGetter", "Potion", "SlowSmoke", "ShedinjaDoll", "Brick", "FluffyTail", "GoalHacker" })
        {
            string path = "Assets/Battle Items/" + (kind == "Eject" ? "EjectButton" : kind == "Brick" ? "BrickLol" : kind) + ".asset";
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That(asset, Is.Not.Null, path);
            var serialized = new SerializedObject(asset);
            int id = Convert.ToInt32(Kind(kind));
            Assert.That(serialized.FindProperty("battleItemType").intValue, Is.EqualTo(id));
            Assert.That(serialized.FindProperty("icon").objectReferenceValue, Is.Not.Null, path);
            Assert.That(entries[id], Does.Contain(AssetDatabase.AssetPathToGUID(path)));
        }
    }

    [Test]
    public void InvalidSelectionIndicesReturnNull()
    {
        var root = new GameObject("Battle item index bounds");
        root.SetActive(false);
        try
        {
            var list = root.AddComponent(Find("CharactersList"));
            var array = Array.CreateInstance(Find("BattleItemAsset"), 1);
            Find("CharactersList").GetField("battleItems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(list, array);
            foreach (int index in new[] { -1, 1, int.MaxValue })
                Assert.That(Find("CharactersList").GetMethod("GetBattleItemByID").Invoke(list, new object[] { index }), Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
