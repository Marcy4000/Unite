using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Gameplay lives in Assembly-CSharp, which asmdef assemblies cannot reference.
// Reflect the production types rather than copying their mechanics into tests.
public class DuraludonMoveTests
{
    private static Type GameType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    }

    private static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name).Invoke(target, args);
    }

    private static int Count(object charges) => (int)charges.GetType().GetProperty("Count").GetValue(charges);

    [Test]
    public void ChargesRecoverSequentiallyAndDoNotRefillOnEachUse()
    {
        object charges = Activator.CreateInstance(GameType("MoveUseState"));
        Call(charges, "Tick", 100f, 2, 14f);
        Assert.That(Call(charges, "TrySpend", 100f, 2.5f), Is.True);
        Assert.That(Call(charges, "TrySpend", 103f, 2.5f), Is.True);
        Assert.That(Count(charges), Is.Zero);
        Assert.That(Call(charges, "TrySpend", 104f, 2.5f), Is.False);
        Call(charges, "Tick", 113.9f, 2, 14f);
        Assert.That(Count(charges), Is.Zero);
        Call(charges, "Tick", 114f, 2, 14f);
        Assert.That(Count(charges), Is.EqualTo(1));
        Call(charges, "Tick", 128f, 2, 14f);
        Assert.That(Count(charges), Is.EqualTo(2));
    }

    [Test]
    public void ChargeRecoveryCatchesUpAfterDeathWithoutExceedingCapacity()
    {
        object charges = Activator.CreateInstance(GameType("MoveUseState"));
        Call(charges, "Tick", 0f, 2, 8.5f);
        Call(charges, "TrySpend", 0f, 1f);
        Call(charges, "TrySpend", 1f, 1f);
        Call(charges, "Tick", 80f, 2, 8.5f);
        Assert.That(Count(charges), Is.EqualTo(2));
        Call(charges, "TrySpend", 100f, 1f);
        Assert.That(charges.GetType().GetProperty("NextRecovery").GetValue(charges), Is.EqualTo(108.5f));
    }

    [Test]
    public void RockUpgradeAddsExactlyOneReserveWithoutResettingRecovery()
    {
        object charges = Activator.CreateInstance(GameType("MoveUseState"));
        Call(charges, "Tick", 20f, 2, 8.5f);
        Call(charges, "TrySpend", 20f, 1f);
        Call(charges, "TrySpend", 21f, 1f);
        Call(charges, "Tick", 21f, 3, 8.5f);
        Call(charges, "Tick", 21f, 3, 8.5f);
        Assert.That(Count(charges), Is.EqualTo(1));
        Call(charges, "Tick", 28.5f, 3, 8.5f);
        Assert.That(Count(charges), Is.EqualTo(2));
        Call(charges, "Tick", 37f, 3, 8.5f);
        Assert.That(Count(charges), Is.EqualTo(3));
    }

    [TestCase(0f, 0)]
    [TestCase(0.49f, 0)]
    [TestCase(0.5f, 1)]
    [TestCase(0.99f, 1)]
    [TestCase(1f, 2)]
    public void PulseChargeStages(float charge, int stage)
    {
        Assert.That(GameType("DuraMoveRules").GetMethod("PulseStage").Invoke(null, new object[] { charge }), Is.EqualTo(stage));
    }

    [TestCase(0f, 0, 0, 0.32f, 1, 38)]
    [TestCase(0f, 1, 1000, 0.77f, 2, 100)]
    [TestCase(0.5f, 3, 1000, 2.11f, 4, 276)]
    [TestCase(1f, 6, 1000, 4.85f, 7, 628)]
    [TestCase(1f, 100, 1000, 4.85f, 7, 628)]
    [TestCase(0f, -1, -1000, 0.32f, 1, 38)]
    public void PulseDamageMatchesOrbsFinalRingAndMissingHp(float charge, int marks, int missingHp, float ratio, int slider, int baseDamage)
    {
        object damage = GameType("DuraMoveRules").GetMethod("PulseDamage").Invoke(null, new object[] { 42UL, charge, marks, missingHp });
        Type type = damage.GetType();
        Assert.That((float)type.GetField("ratio").GetValue(damage), Is.EqualTo(ratio).Within(0.0001f));
        Assert.That((short)type.GetField("slider").GetValue(damage), Is.EqualTo(slider));
        Assert.That((short)type.GetField("baseDmg").GetValue(damage), Is.EqualTo(baseDamage));
        Assert.That((ulong)type.GetField("attackerId").GetValue(damage), Is.EqualTo(42UL));
        Assert.That(type.GetField("proprieties").GetValue(damage).ToString(), Does.Not.Contain("IsBasicAttack"));
    }

    [TestCase(-1f, 3f)]
    [TestCase(0f, 3f)]
    [TestCase(0.5f, 5.25f)]
    [TestCase(1f, 7.5f)]
    [TestCase(2f, 7.5f)]
    public void PulseRadiusIsClamped(float charge, float radius)
    {
        Assert.That(GameType("DuraMoveRules").GetMethod("PulseRadius").Invoke(null, new object[] { charge }), Is.EqualTo(radius));
    }

    [TestCase("DuraLaserFocus", "DuraLaserFocus", 0)]
    [TestCase("DuraMetalClaw", "DuraMetalClaw", 1)]
    [TestCase("DuraFlashCannon", "DuraFlashCannon", 0)]
    [TestCase("DuraDragonPulse", "DuraDragonPulse", 0)]
    [TestCase("DuraDragonTail", "DuraDragonTail", 1)]
    [TestCase("DuraStealthRock", "DuraStelthRock", 1)]
    [TestCase("DuraludonUnite", "DuraludonUnite", 2)]
    public void EveryMoveHasAnAssetFactoryAndRegistryEntry(string assetName, string enumName, int slot)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Duraludon/" + assetName + ".asset");
        Assert.That(asset, Is.Not.Null);
        var serialized = new SerializedObject(asset);
        object moveEnum = Enum.Parse(GameType("AvailableMoves"), enumName);
        Assert.That(serialized.FindProperty("move").intValue, Is.EqualTo(Convert.ToInt32(moveEnum)));
        Assert.That(serialized.FindProperty("moveType").intValue, Is.EqualTo(slot));
        Assert.That(serialized.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        string previewGuid = serialized.FindProperty("preview").FindPropertyRelative("m_AssetGUID").stringValue;
        Assert.That(AssetDatabase.GUIDToAssetPath(previewGuid), Is.Not.Empty);
        object move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { moveEnum });
        Assert.That(move, Is.Not.Null);
        Assert.That(move.GetType().Name, Is.EqualTo(assetName));
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(Call(registry, "GetMoveAsset", moveEnum), Is.SameAs(asset));
    }

    [Test]
    public void LearnsetIncludesBothBranchesUniteAndMatchingUpgrades()
    {
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Duraludon.asset");
        var learns = new SerializedObject(asset).FindProperty("learnableMoves");
        Assert.That(learns.arraySize, Is.EqualTo(7));
        int[] levels = { 0, 1, 4, 6, 8, 10, 12 }; // Pokemon uses zero-based levels.
        for (int i = 0; i < levels.Length; i++)
        {
            var entry = learns.GetArrayElementAtIndex(i);
            Assert.That(entry.FindPropertyRelative("level").intValue, Is.EqualTo(levels[i]));
            Assert.That(entry.FindPropertyRelative("isUpgraded").boolValue, Is.EqualTo(i >= 5));
            var moves = entry.FindPropertyRelative("moves");
            Assert.That(moves.arraySize, Is.EqualTo(i == 4 ? 1 : 2));
            for (int j = 0; j < moves.arraySize; j++) Assert.That(moves.GetArrayElementAtIndex(j).objectReferenceValue, Is.Not.Null);
        }
        for (int i = 0; i < 2; i++)
        {
            var branch = learns.GetArrayElementAtIndex(2 + i).FindPropertyRelative("moves");
            var upgraded = learns.GetArrayElementAtIndex(5 + i).FindPropertyRelative("moves");
            for (int j = 0; j < 2; j++)
                Assert.That(upgraded.GetArrayElementAtIndex(j).objectReferenceValue, Is.SameAs(branch.GetArrayElementAtIndex(j).objectReferenceValue));
        }
    }

    [Test]
    public void ExistingNetworkPrefabSuppliesAllCombatReferences()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/BasicAtk/DuraludonBoostedAtk.prefab");
        var component = prefab.GetComponent(GameType("DuraBoostedObj"));
        Assert.That(component, Is.Not.Null);
        var serialized = new SerializedObject(component);
        foreach (string field in new[] { "beamObject", "beamObjectCannon", "beamCollider", "beamColliderCannon" })
            Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        var beam = (GameObject)serialized.FindProperty("beamObject").objectReferenceValue;
        Assert.That(beam.GetComponent<Renderer>().sharedMaterial, Is.Not.Null);
        Assert.That(GameType("DuraBoostedObj").GetMethod("DragonPulseRPC"), Is.Not.Null);
        Assert.That(GameType("DuraBoostedObj").GetMethod("DragonTailRPC"), Is.Not.Null);
        Assert.That(GameType("DuraBoostedObj").GetMethod("StealthRockRPC"), Is.Not.Null);
        Assert.That(GameType("DuraBoostedObj").GetMethod("RevolvingRuinRPC"), Is.Not.Null);
    }

    [Test]
    public void NewMovesOnlyReferenceExistingAnimationStates()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            "Assets/Model/Pokemons/Duraludon/Animations/Duraludon.controller");
        string[] states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        foreach (string state in new[] { "ani_spell1b1_bat_0884", "ani_spell1b2_bat_0884", "ani_spell2a_bat_0884",
            "ani_spell2b_bat_0884", "ani_spell1a2a_bat_0884", "ani_spell1a2b_bat_0884", "ani_spellu_bat_0884" })
            Assert.That(states, Does.Contain(state));
    }
}
