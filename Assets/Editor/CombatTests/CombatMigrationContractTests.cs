using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatMigrationContractTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("GlaceSwift")]
    [TestCase("GlaceTailWhip")]
    [TestCase("GlaceIcycleSpear")]
    [TestCase("GlaceIcyWind")]
    [TestCase("GlaceIceShard")]
    [TestCase("GlaceFreezeDry")]
    [TestCase("GlaceonUnite")]
    [TestCase("ZoroarkSlash")]
    [TestCase("ZoroarkFurySwipes")]
    [TestCase("ZoroarkShadowClaw")]
    [TestCase("ZoroarkCut")]
    [TestCase("ZoroarkNightSlash")]
    [TestCase("ZoroarkFeintAttack")]
    [TestCase("ZoroarkNightfallDaze")]
    [TestCase("CinderEmber")]
    [TestCase("CinderLowSweep")]
    [TestCase("CinderPyroball")]
    [TestCase("CinderBlazeKick")]
    [TestCase("CinderFlameCharge")]
    [TestCase("CinderFeint")]
    [TestCase("BlazingBycicleKick")]
    [TestCase("SylvSwift")]
    [TestCase("SylvBabyDollEyes")]
    [TestCase("SylvHyperVoice")]
    [TestCase("SylvMysticalCringe")]
    [TestCase("SylvDrainingKiss")]
    [TestCase("SylvCalmMind")]
    [TestCase("SylveonUnite")]
    [TestCase("CresseliaAuroraBeam")]
    [TestCase("CresseliaMoonlight")]
    [TestCase("CresseliaPsybeam")]
    [TestCase("CresseliaMoonblast")]
    [TestCase("CresseliaSafeguard")]
    [TestCase("CresseliaLunarBlessing")]
    [TestCase("CresseliaUnite")]
    [TestCase("VaporSwift")]
    [TestCase("VaporHelpingHand")]
    [TestCase("VaporAquaRing")]
    [TestCase("VaporRainDance")]
    [TestCase("VaporWhirlpool")]
    [TestCase("VaporDive")]
    [TestCase("VaporeonUnite")]
    [TestCase("JoltSwift")]
    [TestCase("JoltAgility")]
    [TestCase("JoltCharge")]
    [TestCase("JoltThunderFang")]
    [TestCase("JoltDischarge")]
    [TestCase("JoltElectroWeb")]
    [TestCase("JolteonUnite")]
    [TestCase("FlareonSwift")]
    [TestCase("FlareonTackle")]
    [TestCase("FlareonHeatWave")]
    [TestCase("FlareonTakedown")]
    [TestCase("FlareonFlameThrower")]
    [TestCase("FlareonFlareBlitz")]
    [TestCase("FlareonUnite")]
    public void PhaseOneAbilitiesUseCombatInputBridge(string name)
    {
        object id = Enum.Parse(GameType("AvailableMoves"), name);
        object move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(GameType("CombatMove").IsInstanceOfType(move), Is.True);
        Assert.That(move.GetType().GetProperty("CooldownOnFinish").GetValue(move), Is.False);
    }

    [TestCase("EmboarEmber")]
    public void OtherRosterAbilitiesRemainLegacy(string name)
    {
        object id = Enum.Parse(GameType("AvailableMoves"), name);
        object move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(GameType("CombatMove").IsInstanceOfType(move), Is.False);
    }

    [TestCase("ZoroarkNightfallDaze", true)]
    [TestCase("AbsolMidnightSlash", true)]
    [TestCase("GlaceonUnite", false)]
    [TestCase("BlazingBycicleKick", false)]
    [TestCase("SylveonUnite", false)]
    [TestCase("CresseliaUnite", false)]
    public void UniteSuccessCheckpointPolicyIsDeclaredByTheMove(string name, bool explicitCheckpoint)
    {
        object id = Enum.Parse(GameType("AvailableMoves"), name);
        object move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Assert.That(move.GetType().GetProperty("UsesExplicitUniteCastCheckpoint").GetValue(move), Is.EqualTo(explicitCheckpoint));
    }

    [Test]
    public void MigrationAssetContractsAreValid()
    {
        var errors = (IList)GameType("CombatArchitectureValidator").GetMethod("CollectErrors").Invoke(null, null);
        Assert.That(errors, Is.Empty, string.Join("\n", errors.Cast<object>()));
    }

    [TestCase("Glaceon")]
    [TestCase("Zoroark")]
    [TestCase("Cinderace")]
    [TestCase("Sylveon")]
    [TestCase("Cresselia")]
    [TestCase("Vaporeon")]
    [TestCase("Jolteon")]
    [TestCase("Flareon")]
    public void MigratedCharacterDefinitionIsEnabledAtTheCompositionRoot(string character)
    {
        UnityEngine.Object definition = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/" + character + ".asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(GameType("CombatKitFactory").GetMethod("SupportsDefinition").Invoke(null, new object[] { definition }), Is.True);
    }

    [Test]
    public void FollowUpTargetChangesInvalidateSlotSnapshotEquality()
    {
        Type snapshotType = GameType("CombatSlotSnapshot");
        object first = Activator.CreateInstance(snapshotType);
        object second = Activator.CreateInstance(snapshotType);
        snapshotType.GetField("FollowUpReady").SetValue(first, true);
        snapshotType.GetField("FollowUpReady").SetValue(second, true);
        snapshotType.GetField("FollowUpTargetId").SetValue(first, 12UL);
        snapshotType.GetField("FollowUpTargetId").SetValue(second, 13UL);
        Assert.That(first.Equals(second), Is.False);
        snapshotType.GetField("FollowUpTargetId").SetValue(second, 12UL);
        Assert.That(first.Equals(second), Is.True);
    }

    [TestCase("GlaceBasicAtk")]
    [TestCase("ZoroarkBasicAtk")]
    [TestCase("CinderBasicAtk")]
    [TestCase("SylvBasicAtk")]
    [TestCase("CresseliaBasicAtk")]
    [TestCase("VaporBasicAtk")]
    [TestCase("JoltBasicAtk")]
    [TestCase("FlareonBasicAtk")]
    public void MigratedBasicAttacksAreServerResolved(string type)
    {
        object attack = Activator.CreateInstance(GameType(type));
        Assert.That(attack.GetType().GetProperty("ServerResolved").GetValue(attack), Is.True);
    }

    [Test]
    public void HeldChannelDeclaresPressActivation()
    {
        object spear = Activator.CreateInstance(GameType("GlaceIcicleSpear"));
        Assert.That(spear.GetType().GetProperty("StartsOnPress").GetValue(spear), Is.True);
        Assert.That(spear.GetType().GetProperty("EndsOnRelease").GetValue(spear), Is.False);
        Assert.That(spear.GetType().GetProperty("CooldownOnEffectEnd").GetValue(spear), Is.True);
    }

    [TestCase("CinderEmber", "CinderEmber", "Target", 6f, 8f, true, true)]
    [TestCase("CinderLowSweep", "CinderLowSweep", "Dash", 7f, 5f, true, true)]
    [TestCase("CinderPyroball", "CinderPyroball", "Direction", 4.5f, 8f, true, true)]
    [TestCase("CinderBlazeKick", "CinderBlazeKick", "Target", 9.5f, 7f, true, true)]
    [TestCase("CinderFlameCharge", "CinderFlameCharge", "Dash", 5f, 5f, true, true)]
    [TestCase("CinderFeint", "CinderFeint", "None", 9.5f, 0f, false, false)]
    [TestCase("BlazingBycicleKick", "BlazingBycicleKick", "Target", 0f, 11f, true, true)]
    public void CinderaceMovesUseThinCombatMoveInputBridgesWithExactContracts(
        string moveName,
        string expectedId,
        string expectedAimStyle,
        float expectedCooldown,
        float expectedRange,
        bool expectedOffensive,
        bool expectedLockDuringLoad)
    {
        object id = Enum.Parse(GameType("AvailableMoves"), moveName);
        object move = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { id });
        Type type = move.GetType();
        Assert.That(GameType("CombatMove").IsInstanceOfType(move), Is.True);
        Assert.That(GameType("CinderMoveBase").IsInstanceOfType(move), Is.True);
        Assert.That(type.GetProperty("MoveId").GetValue(move).ToString(), Is.EqualTo(expectedId));
        Assert.That(type.GetProperty("PrefabPath").GetValue(move), Is.Null);
        Assert.That(type.GetProperty("CooldownOnFinish").GetValue(move), Is.False);
        Assert.That(type.GetProperty("AimStyle").GetValue(move).ToString(), Is.EqualTo(expectedAimStyle));
        Assert.That((float)type.GetField("Cooldown").GetValue(move), Is.EqualTo(expectedCooldown).Within(0.001f));
        Assert.That((float)type.GetProperty("Range").GetValue(move), Is.EqualTo(expectedRange).Within(0.001f));
        Assert.That(type.GetProperty("IsOffensive").GetValue(move), Is.EqualTo(expectedOffensive));
        Assert.That(type.GetProperty("LockDuringLoad").GetValue(move), Is.EqualTo(expectedLockDuringLoad));
    }

    [Test]
    public void CinderaceCharacterRuntimePresentationStateRoundTripsAndRejectsStaleSnapshots()
    {
        var root = new GameObject("Cinderace contract fixture");
        try
        {
            var pokemon = root.AddComponent(GameType("Pokemon"));
            var controller = root.AddComponent(GameType("MovesController"));
            var manager = root.AddComponent(GameType("PlayerManager"));
            ((Behaviour)pokemon).enabled = false;
            ((Behaviour)controller).enabled = false;
            ((Behaviour)manager).enabled = false;
            var cinderace = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset");
            const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            pokemon.GetType().GetField("baseStats", Private).SetValue(pokemon, cinderace);
            controller.GetType().GetField("pokemon", Private).SetValue(controller, pokemon);
            controller.GetType().GetField("playerManager", Private).SetValue(controller, manager);
            manager.GetType().GetField("pokemon", Private).SetValue(manager, pokemon);
            manager.GetType().GetField("movesController", Private).SetValue(manager, controller);

            object combat = controller.GetType().GetProperty("Combat").GetValue(controller);
            object character = combat.GetType().GetProperty("Character").GetValue(combat);
            Assert.That(character.GetType().Name, Is.EqualTo("CinderaceCharacterRuntime"));

            character.GetType().GetMethod("ReceiveSnapshot").Invoke(character, new object[] { (byte)5, true, 10u });
            Assert.That(character.GetType().GetProperty("Charge").GetValue(character), Is.EqualTo((byte)2));
            Assert.That(character.GetType().GetProperty("MovementLocked").GetValue(character), Is.EqualTo(true));
            Assert.That(controller.GetType().GetProperty("CinderaceAttackCharge").GetValue(controller), Is.EqualTo(2));

            // Stale revision must be ignored.
            character.GetType().GetMethod("ReceiveSnapshot").Invoke(character, new object[] { (byte)0, false, 9u });
            Assert.That(character.GetType().GetProperty("Charge").GetValue(character), Is.EqualTo((byte)2));
            Assert.That(character.GetType().GetProperty("MovementLocked").GetValue(character), Is.EqualTo(true));

            string json = (string)character.GetType().GetMethod("CapturePresentationState").Invoke(character, null);
            character.GetType().GetMethod("ReceiveSnapshot").Invoke(character, new object[] { (byte)1, false, 11u });
            Assert.That(character.GetType().GetProperty("Charge").GetValue(character), Is.EqualTo((byte)1));

            // Older revision in JSON must not overwrite newer snapshot.
            character.GetType().GetMethod("ApplyPresentationState").Invoke(character, new object[] { json });
            Assert.That(character.GetType().GetProperty("Charge").GetValue(character), Is.EqualTo((byte)1));

            // Malformed JSON is ignored safely.
            character.GetType().GetMethod("ApplyPresentationState").Invoke(character, new object[] { "{not-valid-json" });
            Assert.That(character.GetType().GetProperty("Charge").GetValue(character), Is.EqualTo((byte)1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
