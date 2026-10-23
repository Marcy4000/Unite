using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CinderaceMoveTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Kind(string name) => Enum.Parse(GameType("CinderCast"), name);
    private static object Rule(string name, params object[] args) => GameType("CinderMoveRules").GetMethod(name).Invoke(null, args);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);

    [TestCase("Ember", 1.15f, 8, 155)]
    [TestCase("LowSweep", 0.36f, 3, 100)]
    [TestCase("PyroBall", 3.45f, 32, 820)]
    [TestCase("BlazeKick", 0.48f, 4, 120)]
    [TestCase("FlameCharge", 0.47f, 3, 130)]
    [TestCase("Unite", 2.47f, 14, 670)]
    public void DirectDamageMatchesUniteDb(string kind, float ratio, int slider, int baseDamage)
    {
        object damage = Rule("Damage", 42UL, Kind(kind), false); Type type = damage.GetType();
        Assert.That(type.GetField("attackerId").GetValue(damage), Is.EqualTo(42UL));
        Assert.That(type.GetField("ratio").GetValue(damage), Is.EqualTo(ratio));
        Assert.That(type.GetField("slider").GetValue(damage), Is.EqualTo((short)slider));
        Assert.That(type.GetField("baseDmg").GetValue(damage), Is.EqualTo((short)baseDamage));
        string flags = type.GetField("proprieties").GetValue(damage).ToString();
        Assert.That(flags, Does.Contain("Unused_1"), "Moves apply two cinders.");
        Assert.That(flags, Does.Not.Contain("CanCrit"), "Only the kick is a guaranteed crit; other moves cannot crit.");
        Assert.That(flags.Contains("WasCriticalHit"), Is.EqualTo(kind == "BlazeKick"));
        Assert.That(flags.Contains("IsUniteMove"), Is.EqualTo(kind == "Unite"));
    }
    [TestCase(false, 1f)]
    [TestCase(true, 1.4f)]
    public void BasicDamageUsesCorrectRatioAndProcFlags(bool boosted, float ratio)
    {
        object damage = Rule("Damage", 9UL, Kind("Basic"), boosted); Type type = damage.GetType();
        Assert.That(type.GetField("ratio").GetValue(damage), Is.EqualTo(ratio));
        string flags = type.GetField("proprieties").GetValue(damage).ToString();
        Assert.That(flags, Does.Contain("IsBasicAttack").And.Contain("CanCrit"));
        Assert.That(flags.Contains("Unused_1"), Is.EqualTo(boosted));
    }
    [Test]
    public void FifthCinderDetonatesAndConsumesAllStacks()
    {
        object state = Activator.CreateInstance(GameType("CinderStackState"));
        Assert.That(Call(state, "Add", 2, 0f), Is.False);
        Assert.That(Call(state, "Add", 1, 1f), Is.False);
        Assert.That(Call(state, "Add", 2, 2f), Is.True);
        Assert.That(Call(state, "Get", 2f), Is.EqualTo(0));
        Assert.That(Call(state, "Add", 2, 3f), Is.False);
        Assert.That(Call(state, "Get", 3f), Is.EqualTo(2));
    }
    [Test]
    public void SixthCinderDoesNotCarryAnExtraStackPastDetonation()
    {
        object state = Activator.CreateInstance(GameType("CinderStackState"));
        Call(state, "Add", 2, 1f); Call(state, "Add", 2, 2f);
        Assert.That(Call(state, "Add", 2, 3f), Is.True);
        Assert.That(Call(state, "Get", 3f), Is.EqualTo(0));
    }
    [Test]
    public void CindersExpireAndRefreshFromTheLastHit()
    {
        object state = Activator.CreateInstance(GameType("CinderStackState"));
        Call(state, "Add", 2, 1f);
        Assert.That(Call(state, "Get", 5.99f), Is.EqualTo(2));
        Assert.That(Call(state, "Get", 6f), Is.EqualTo(0));
        Call(state, "Add", 1, 6f); Call(state, "Add", 1, 10f);
        Assert.That(Call(state, "Get", 14.99f), Is.EqualTo(2));
        Call(state, "Clear"); Assert.That(Call(state, "Get", 11f), Is.EqualTo(0));
    }
    [TestCase(100, 0, 10000, false, 115)]
    [TestCase(100, 6, 10000, false, 535)]
    [TestCase(100, 6, 10000, true, 500)]
    [TestCase(100, -1, -100, false, 115)]
    public void FlareScalesWithZeroBasedLevelAndCapsWildDamage(int attack, int level, int hp, bool wild, int expected)
    {
        Assert.That(Rule("FlareDamage", attack, level, hp, wild), Is.EqualTo(expected));
    }
    [TestCase("CinderEmber", "CinderEmber", 6f, 0)]
    [TestCase("CinderLowSweep", "CinderLowSweep", 7f, 1)]
    [TestCase("CinderPyroball", "CinderPyroball", 4.5f, 0)]
    [TestCase("CinderBlazeKick", "CinderBlazeKick", 9.5f, 0)]
    [TestCase("CinderFlameCharge", "CinderFlameCharge", 5f, 1)]
    [TestCase("CinderFeint", "CinderFeint", 9.5f, 1)]
    [TestCase("BlazingBycicleKick", "BlazingBycicleKick", 0f, 2)]
    public void MovesHaveFactoriesAssetsIconsPreviewsAndRegistryEntries(string assetName, string enumName, float cooldown, int slot)
    {
        object id = Enum.Parse(GameType("AvailableMoves"), enumName);
        object move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(move, Is.Not.Null); Assert.That(move.GetType().GetField("Cooldown").GetValue(move), Is.EqualTo(cooldown));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Cinderace/" + assetName + ".asset"); Assert.That(asset, Is.Not.Null);
        var data = new SerializedObject(asset);
        Assert.That(data.FindProperty("move").intValue, Is.EqualTo(Convert.ToInt32(id)));
        Assert.That(data.FindProperty("moveType").intValue, Is.EqualTo(slot));
        Assert.That(data.FindProperty("cooldown").floatValue, Is.EqualTo(slot == 2 ? 112 : cooldown));
        Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        Assert.That(AssetDatabase.GUIDToAssetPath(data.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue), Is.Not.Empty);
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(Call(registry, "GetMoveAsset", id), Is.SameAs(asset));
    }
    [Test]
    public void LearnsetIncludesBothBranchesAtSevenAndEightWithUpgradesAtElevenAndThirteen()
    {
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset"));
        var learns = data.FindProperty("learnableMoves"); int[] levels = { 0, 2, 6, 7, 8, 10, 12 };
        Assert.That(learns.arraySize, Is.EqualTo(levels.Length));
        for (int i = 0; i < levels.Length; i++)
        {
            var entry = learns.GetArrayElementAtIndex(i);
            Assert.That(entry.FindPropertyRelative("level").intValue, Is.EqualTo(levels[i]));
            Assert.That(entry.FindPropertyRelative("isUpgraded").boolValue, Is.EqualTo(i >= 5));
            var choices = entry.FindPropertyRelative("moves"); Assert.That(choices.arraySize, Is.EqualTo(i == 4 ? 1 : 2));
            for (int j = 0; j < choices.arraySize; j++) Assert.That(choices.GetArrayElementAtIndex(j).objectReferenceValue, Is.Not.Null);
        }
        for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
            Assert.That(learns.GetArrayElementAtIndex(2 + i).FindPropertyRelative("moves").GetArrayElementAtIndex(j).objectReferenceValue,
                Is.SameAs(learns.GetArrayElementAtIndex(5 + i).FindPropertyRelative("moves").GetArrayElementAtIndex(j).objectReferenceValue));
    }
    [Test]
    public void ExistingSerializedEnumsAreNotRenumbered()
    {
        Type type = GameType("AvailableMoves");
        Assert.That(Convert.ToInt32(Enum.Parse(type, "CinderEmber")), Is.EqualTo(1));
        Assert.That(Convert.ToInt32(Enum.Parse(type, "BlazingBycicleKick")), Is.EqualTo(6));
        Assert.That(Convert.ToInt32(Enum.Parse(type, "CinderBlazeKick")), Is.GreaterThan(Convert.ToInt32(Enum.Parse(type, "DuraludonUnite"))));
    }
    [Test]
    public void EffectIdsAreDistinctAndReleasedForReuseWithoutCharacterSpecificBurnClassification()
    {
        Type ids = GameType("CinderEffectIds"); var acquire = ids.GetMethod("Acquire"); var release = ids.GetMethod("Release");
        object first = acquire.Invoke(null, null), second = acquire.Invoke(null, null);
        try
        {
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(GameType("CinderMoveRules").GetMethod("ManagedBurn"), Is.Null,
                "Periodic damage is described by StatusEffect.PeriodicDamage, never by a character's reserved ID range.");
        }
        finally { release.Invoke(null, new[] { first }); release.Invoke(null, new[] { second }); }
    }
    [TestCase("PyroBall", 9, false)]
    [TestCase("PyroBall", 10, true)]
    [TestCase("BlazeKick", 10, true)]
    [TestCase("FlameCharge", 11, false)]
    [TestCase("FlameCharge", 12, true)]
    [TestCase("Feint", 12, true)]
    public void UpgradeThresholdsUseProjectLevelIndex(string kind, int level, bool expected) => Assert.That(Rule("Upgraded", Kind(kind), level), Is.EqualTo(expected));

    [Test]
    public void DrivenDashMovesDespiteTheCastsOwnMovementRestriction()
    {
        var go = new GameObject("Cinderace movement test");
        try
        {
            var controller = go.AddComponent<CharacterController>();
            Component movement = go.AddComponent(GameType("PlayerMovement"));
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            movement.GetType().GetField("characterController", flags).SetValue(movement, controller);
            movement.GetType().GetField("canMove", flags).SetValue(movement, false);
            movement.GetType().GetField("movementRestrictions", flags).SetValue(movement, 1);
            Assert.That(movement.GetType().GetProperty("CanMove").GetValue(movement), Is.False);
            Call(movement, "CinderaceDashStep", Vector3.forward * 2.5f);
            Call(movement, "CinderaceDashStep", Vector3.forward * 2.5f);
            Assert.That(go.transform.position.z, Is.EqualTo(5).Within(0.01f));
            Assert.That(movement.GetType().GetProperty("CanMove").GetValue(movement), Is.False, "Driving cannot clear another action's lock.");
            Assert.That(movement.GetType().GetField("activeWalkmesh", flags).GetValue(movement), Is.Null);
            Assert.That(movement.GetType().GetProperty("IsDashing").GetValue(movement), Is.False, "No coroutine or traversal flag may outlive cancellation.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [Test]
    public void ResetIsSafeAndIdempotentBeforeTheMoveHasBeenUsed()
    {
        foreach (string name in new[] { "CinderEmber", "CinderLowSweep", "CinderPyroball", "CinderBlazeKick", "CinderFlameCharge", "CinderFeint", "BlazingBycicleKick" })
        {
            object move = Activator.CreateInstance(GameType(name));
            Call(move, "ResetMove"); Call(move, "ResetMove");
            Assert.That(move.GetType().GetField("IsActive").GetValue(move), Is.False);
        }
    }
}
