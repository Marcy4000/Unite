using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class VaporeonTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object EnumValue(string type, string value) => Enum.Parse(GameType(type), value);
    private static object Rule(string method, params object[] args) => GameType("VaporMoveRules").GetMethod(method).Invoke(null, args);

    [Test]
    public void WhirlpoolReusesOnlyTheUnusedSerializedEntry()
    {
        Assert.That(Convert.ToUInt32(EnumValue("AvailableMoves", "VaporWhirlpool")), Is.EqualTo(26));
        Assert.That(Enum.GetNames(GameType("AvailableMoves")), Does.Not.Contain("VaporHaze"));
        Assert.That(Convert.ToUInt32(EnumValue("AvailableMoves", "VaporeonUnite")), Is.EqualTo(27));
        Assert.That(Convert.ToUInt32(EnumValue("AvailableMoves", "JoltSwift")), Is.EqualTo(28));
    }
    [TestCase("Swift", "VaporSwift", 0)]
    [TestCase("HelpingHand", "VaporHelpingHand", 1)]
    [TestCase("AquaRing", "VaporAquaRing", 0)]
    [TestCase("RainDance", "VaporRainDance", 0)]
    [TestCase("Whirlpool", "VaporWhirlpool", 1)]
    public void MoveFactoriesAssetsRegistryAndRulesAgree(string kind, string name, int slot)
    {
        object move = EnumValue("AvailableMoves", name), cast = EnumValue("VaporCast", kind);
        object runtime = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { move });
        Assert.That(runtime.GetType().Name, Is.EqualTo(name));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Vaporeon/" + name + ".asset");
        Assert.That(asset, Is.Not.Null);
        var data = new SerializedObject(asset);
        Assert.That(data.FindProperty("move").longValue, Is.EqualTo(Convert.ToInt64(move)));
        Assert.That(data.FindProperty("moveType").intValue, Is.EqualTo(slot));
        Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        Assert.That(data.FindProperty("description").stringValue, Is.Not.Empty);
        Assert.That(runtime.GetType().GetField("Cooldown").GetValue(runtime), Is.EqualTo(data.FindProperty("cooldown").floatValue));
        Assert.That(Rule("Move", cast), Is.EqualTo(move)); Assert.That(Rule("Slot", cast), Is.EqualTo(slot));
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(registry.GetType().GetMethod("GetMoveAsset").Invoke(registry, new[] { move }), Is.SameAs(asset));
    }
    [Test]
    public void DiveAndTempestAreRegisteredWithoutChangingUniteEnergy()
    {
        foreach (string name in new[] { "VaporDive", "VaporeonUnite" })
        {
            object move = EnumValue("AvailableMoves", name);
            Assert.That(GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { move }).GetType().Name, Is.EqualTo(name));
            var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
            Assert.That(registry.GetType().GetMethod("GetMoveAsset").Invoke(registry, new[] { move }), Is.Not.Null);
        }
        var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Vaporeon/VaporeonUnite.asset"));
        Assert.That(data.FindProperty("uniteEnergyCost").intValue, Is.EqualTo(100000));
        var damage = Rule("TempestDamage", 1ul);
        Assert.That(damage.GetType().GetField("proprieties").GetValue(damage).ToString(), Does.Contain("IsUniteMove"));
    }
    [Test]
    public void LearnsetIncludesBothControlChoicesAndActualUpgrades()
    {
        var mon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Vaporeon.asset"));
        var moves = mon.FindProperty("learnableMoves"); int[] levels = { 0, 1, 3, 5, 7, 9, 11 };
        Assert.That(moves.arraySize, Is.EqualTo(7));
        for (int i = 0; i < levels.Length; i++)
            Assert.That(moves.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue, Is.EqualTo(levels[i]));
        foreach (int row in new[] { 3, 6 })
        {
            var choices = moves.GetArrayElementAtIndex(row).FindPropertyRelative("moves");
            Assert.That(choices.arraySize, Is.EqualTo(2));
            Assert.That(choices.GetArrayElementAtIndex(0).objectReferenceValue.name, Is.EqualTo("VaporDive"));
            Assert.That(choices.GetArrayElementAtIndex(1).objectReferenceValue.name, Is.EqualTo("VaporWhirlpool"));
            Assert.That(moves.GetArrayElementAtIndex(row).FindPropertyRelative("isUpgraded").boolValue, Is.EqualTo(row == 6));
        }
        Assert.That((float)Rule("ShieldRatio", true), Is.GreaterThan((float)Rule("ShieldRatio", false)));
        Assert.That((short)Rule("RainDefense", true), Is.GreaterThan((short)Rule("RainDefense", false)));
        Assert.That((short)Rule("WhirlpoolSlow", true), Is.GreaterThan((short)Rule("WhirlpoolSlow", false)));
        Assert.That((float)Rule("DiveCooldown", true), Is.LessThan((float)Rule("DiveCooldown", false)));
        Assert.That(mon.FindProperty("passive").intValue, Is.EqualTo(Convert.ToInt32(EnumValue("AvailablePassives", "VaporeonPassive"))));
        var passive = GameType("PassiveDatabase").GetMethod("GetPassive").Invoke(null, new[] { EnumValue("AvailablePassives", "VaporeonPassive") });
        Assert.That(passive.GetType().Name, Is.EqualTo("VaporeonPassive"));
        Assert.That(mon.FindProperty("description").stringValue, Does.Contain("Hydration"));
        var info = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Vaporeon.asset"));
        Assert.That(info.FindProperty("Range").intValue, Is.EqualTo(1));
    }
    [TestCase("AquaRing", 8, false)]
    [TestCase("AquaRing", 9, true)]
    [TestCase("RainDance", 9, true)]
    [TestCase("Whirlpool", 10, false)]
    [TestCase("Whirlpool", 11, true)]
    public void UpgradeThresholdsAreZeroBased(string kind, int level, bool expected)
    { Assert.That(Rule("Upgraded", EnumValue("VaporCast", kind), level), Is.EqualTo(expected)); }

    [TestCase("VaporSwift")]
    [TestCase("VaporHelpingHand")]
    [TestCase("VaporAquaRing")]
    [TestCase("VaporRainDance")]
    [TestCase("VaporWhirlpool")]
    [TestCase("VaporDive")]
    [TestCase("VaporeonUnite")]
    public void UninitializedResetAndCancellationAreSafeAndIdempotent(string name)
    {
        object move = Activator.CreateInstance(GameType(name));
        move.GetType().GetMethod("Cancel").Invoke(move, null);
        move.GetType().GetMethod("ResetMove").Invoke(move, null); move.GetType().GetMethod("ResetMove").Invoke(move, null);
        Assert.That(move.GetType().GetField("IsActive").GetValue(move), Is.False);
        Assert.That(move.GetType().GetField("wasMoveSuccessful").GetValue(move), Is.False);
    }
    [Test]
    public void CastRecoveryIsShortAndProtectionDoesNotRoot()
    {
        foreach (string name in new[] { "Swift", "HelpingHand", "AquaRing", "RainDance", "Whirlpool" })
        {
            object cast = EnumValue("VaporCast", name);
            Assert.That((float)Rule("Windup", cast) + (float)Rule("Recovery", cast), Is.LessThanOrEqualTo(0.46f));
            if ((bool)Rule("IsSupport", cast)) Assert.That(Rule("Windup", cast), Is.EqualTo(0f));
        }
        Assert.That(Rule("Recovery", EnumValue("VaporCast", "Tempest")), Is.EqualTo(0.75f));
    }
    [Test]
    public void RainSpendsItsCooldownBeforeProvidingMovement()
    {
        Assert.That(Rule("CommitOnStart", EnumValue("VaporCast", "RainDance")), Is.True);
        Assert.That(Rule("CommitOnStart", EnumValue("VaporCast", "Whirlpool")), Is.False);
        Assert.That(Rule("CommitOnStart", EnumValue("VaporCast", "Swift")), Is.False);
    }
    [Test]
    public void WhirlpoolDamageBudgetIsDividedAcrossSixPulses()
    {
        var tick = Rule("WhirlpoolTick", 12ul); var type = tick.GetType();
        Assert.That((float)type.GetField("ratio").GetValue(tick) * 6, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(Convert.ToInt32(type.GetField("baseDmg").GetValue(tick)) * 6, Is.EqualTo(198));
        Assert.That(type.GetField("attackerId").GetValue(tick), Is.EqualTo(12ul));
    }
    [Test]
    public void AimClampsDistanceAndRejectsInvalidCoordinates()
    {
        var origin = new Vector3(10, 1, 20);
        Assert.That(Rule("ClampDestination", origin, new Vector3(float.NaN, 0, 0), 7f), Is.EqualTo(origin));
        var target = (Vector3)Rule("ClampDestination", origin, origin + new Vector3(100, 900, 0), 7f);
        Assert.That(target, Is.EqualTo(origin + Vector3.right * 7));
        Assert.That(Rule("Direction", Vector3.zero, Vector3.zero), Is.EqualTo(Vector3.forward));
    }
    [Test]
    public void TargetQueriesDeduplicateRootAndChildHitboxes()
    {
        var root = new GameObject("Vaporeon deduplication") { layer = LayerMask.NameToLayer("Players") };
        try
        {
            root.AddComponent(GameType("Pokemon")); root.AddComponent<BoxCollider>();
            var child = new GameObject("Child hitbox") { layer = root.layer }; child.transform.SetParent(root.transform); child.AddComponent<SphereCollider>();
            Physics.SyncTransforms();
            var targets = (IEnumerable)GameType("VaporMoveRuntime").GetMethod("Targets").Invoke(null, new object[] { root.transform.position, 2f });
            Assert.That(targets.Cast<object>().Count(t => ReferenceEquals(t, root.GetComponent(GameType("Pokemon")))), Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test]
    public void SourceIdsAreUniqueAndLeasesReleaseOnlyTheirOwnContribution()
    {
        var root = new GameObject("Vaporeon modifier sources");
        var acquire = GameType("VaporEffectIds").GetMethod("Acquire"); var release = GameType("VaporEffectIds").GetMethod("Release");
        var ids = new List<ushort>(); IDisposable a = null, b = null;
        try
        {
            for (int i = 0; i < 50; i++) ids.Add((ushort)acquire.Invoke(null, null));
            Assert.That(ids, Is.Unique);
            var target = root.AddComponent(GameType("Pokemon")); var leaseType = GameType("VaporModifierLease");
            a = (IDisposable)Activator.CreateInstance(leaseType, target, EnumValue("Stat", "Speed"), (short)20, false);
            b = (IDisposable)Activator.CreateInstance(leaseType, target, EnumValue("Stat", "Speed"), (short)35, false);
            object groupA = leaseType.GetField("group", Private).GetValue(a), groupB = leaseType.GetField("group", Private).GetValue(b);
            Assert.That(groupA, Is.SameAs(groupB));
            var sources = (IList)groupA.GetType().GetField("Sources").GetValue(groupA);
            Assert.That(sources.Count, Is.EqualTo(2));
            Assert.That(leaseType.GetProperty("EffectiveAmount").GetValue(a), Is.EqualTo((short)35));
            b.Dispose(); b.Dispose(); Assert.That(sources.Count, Is.EqualTo(1)); Assert.That(sources[0], Is.SameAs(a));
            Assert.That(leaseType.GetProperty("EffectiveAmount").GetValue(a), Is.EqualTo((short)20));
            a.Dispose(); a.Dispose(); Assert.That(sources.Count, Is.Zero);
        }
        finally { a?.Dispose(); b?.Dispose(); foreach (ushort id in ids) release.Invoke(null, new object[] { id }); UnityEngine.Object.DestroyImmediate(root); }
    }
    private sealed class CountingLease : IDisposable
    {
        public int Disposals;
        public void Dispose() { Disposals++; }
    }
    [Test]
    public void EffectIdsCannotCollideWithJolteonOrMarshadow()
    {
        int first = Convert.ToInt32(GameType("VaporEffectIds").GetField("First").GetRawConstantValue());
        int last = Convert.ToInt32(GameType("VaporEffectIds").GetField("Last").GetRawConstantValue());
        int webMaximum = Convert.ToInt32(GameType("JoltMoveRules").GetField("WebMarkIdMax").GetRawConstantValue());
        Assert.That(first, Is.GreaterThan(webMaximum)); Assert.That(last, Is.LessThan(62000)); Assert.That(last - first + 1, Is.EqualTo(2000));
    }
    [Test]
    public void CancelledCastImmediatelyDisposesAnyLateWork()
    {
        var scopeType = GameType("MovesController").GetNestedType("VaporScope", BindingFlags.NonPublic);
        var scope = (IDisposable)Activator.CreateInstance(scopeType, new object[] { null });
        var first = new CountingLease(); var late = new CountingLease();
        scopeType.GetMethod("Own").Invoke(scope, new object[] { first }); scope.Dispose();
        scopeType.GetMethod("Own").Invoke(scope, new object[] { late }); scope.Dispose();
        Assert.That(first.Disposals, Is.EqualTo(1)); Assert.That(late.Disposals, Is.EqualTo(1));
    }
    [TestCase("Orange", 100, "Invincible", false, false)]
    [TestCase("Orange", 100, "Untargetable", false, false)]
    [TestCase("Orange", 100, null, false, true)]
    [TestCase("Blue", 100, null, true, false)]
    [TestCase("Blue", 0, null, false, false)]
    public void EnemyRejectionNeverFallsThroughToAllyHealing(string team, int hp, string status, bool canHeal, bool canHit)
    {
        var actorGo = new GameObject("Vaporeon filter actor");
        var targetGo = new GameObject("Vaporeon filter target");
        Component actorPokemon = null, targetPokemon = null;
        try
        {
            actorPokemon = MakePokemon(actorGo, "Blue", 100); targetPokemon = MakePokemon(targetGo, team, hp);
            var actor = actorGo.AddComponent(GameType("PlayerManager")); GameType("PlayerManager").GetField("pokemon", Private).SetValue(actor, actorPokemon);
            if (status != null)
            {
                var list = GameType("Pokemon").GetField("statusEffects", Private).GetValue(targetPokemon);
                var effect = Activator.CreateInstance(GameType("StatusEffect"), EnumValue("StatusType", status), 2f, true, (ushort)123);
                list.GetType().GetMethod("Add").Invoke(list, new[] { effect });
            }
            Assert.That(GameType("VaporMoveRuntime").GetMethod("CanHeal").Invoke(null, new object[] { actor, targetPokemon }), Is.EqualTo(canHeal));
            Assert.That(GameType("VaporMoveRuntime").GetMethod("CanHit").Invoke(null, new object[] { actor, targetPokemon }), Is.EqualTo(canHit));
        }
        finally
        {
            if (actorPokemon != null) GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsSpawned").SetValue(actorPokemon, false);
            if (targetPokemon != null) GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsSpawned").SetValue(targetPokemon, false);
            UnityEngine.Object.DestroyImmediate(actorGo); UnityEngine.Object.DestroyImmediate(targetGo);
        }
    }
    private static Component MakePokemon(GameObject go, string team, int hp)
    {
        var type = GameType("Pokemon"); var pokemon = go.AddComponent(type);
        if (type.GetField("statusEffects", Private).GetValue(pokemon) == null) type.GetMethod("Awake", Private).Invoke(pokemon, null);
        object hpValue = type.GetField("currentHp", Private).GetValue(pokemon); hpValue.GetType().GetProperty("Value").SetValue(hpValue, hp);
        object teamValue = type.GetField("team", Private).GetValue(pokemon);
        teamValue.GetType().GetProperty("Value").SetValue(teamValue, Activator.CreateInstance(GameType("TeamMember"), EnumValue("Team", team)));
        type.GetProperty("Type").SetValue(pokemon, EnumValue("PokemonType", "Player"));
        GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsSpawned").SetValue(pokemon, true); return pokemon;
    }
    [Test]
    public void EffectsAreAuthoredAddressableAndCosmeticOnly()
    {
        var group = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset"));
        var entries = group.FindProperty("m_SerializeEntries");
        foreach (object effect in Enum.GetValues(GameType("VaporEffect")))
        {
            string path = (string)Rule("EffectPath", effect); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var view = new SerializedObject(prefab.GetComponent(GameType("VaporVisual")));
            foreach (string field in new[] { "graphics", "accent", "rotating" }) Assert.That(view.FindProperty(field).objectReferenceValue, Is.Not.Null, path);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
            Assert.That(prefab.GetComponent(GameType("Unity.Netcode.NetworkObject")), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.Not.Empty);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true)) foreach (Material material in renderer.sharedMaterials)
            { Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Does.Not.Contain("Error")); }
            var audio = prefab.GetComponent<AudioSource>();
            if (audio != null) { Assert.That(audio.clip, Is.Not.Null); Assert.That(audio.playOnAwake, Is.False); }
            string guid = AssetDatabase.AssetPathToGUID(path); int matches = 0;
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_GUID").stringValue == guid)
                { matches++; Assert.That(entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path)); }
            Assert.That(matches, Is.EqualTo(1), path);
        }
    }
    [Test]
    public void RuntimeDoesNotBuildGeometryOrSubscribeToSharedSpawnCallbacks()
    {
        foreach (string name in new[] { "VaporMoveRuntime.cs", "VaporVisual.cs", "VaporDive.cs", "VaporBasicAtk.cs" })
        {
            string source = File.ReadAllText("Assets/Scripts/Moves/Vaporeon/" + name);
            foreach (string forbidden in new[] { "new GameObject(", "CreatePrimitive(", "new Mesh ", "onObjectSpawned", "DOJump(", "DOKill(" })
                Assert.That(source, Does.Not.Contain(forbidden), name);
        }
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Vaporeon/Vaporeon.controller");
        Assert.That(controller.parameters.Any(p => p.name == "VaporeonActionSpeed" && p.defaultFloat == 1), Is.True);
        foreach (string name in new[] { "Armature_pm0134_00_ba21_tokusyu01_gfbanm", "Armature_pm0134_00_ba21_tokusyu02_gfbanm", "Armature_pm0134_00_ba02_roar01_gfbanm" })
            Assert.That(controller.layers[0].stateMachine.states.First(s => s.state.name == name).state.speedParameterActive, Is.True);
    }
}
