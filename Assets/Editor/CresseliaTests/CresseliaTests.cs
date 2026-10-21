using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CresseliaTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object EnumValue(string type, string value) => Enum.Parse(GameType(type), value);
    private static object Rule(string name, params object[] args) => GameType("CresseliaMoveRules").GetMethod(name).Invoke(null, args);

    [TestCase("AuroraBeam", "CresseliaAuroraBeam", 0)]
    [TestCase("Safeguard", "CresseliaSafeguard", 1)]
    [TestCase("Psybeam", "CresseliaPsybeam", 0)]
    [TestCase("Moonblast", "CresseliaMoonblast", 0)]
    [TestCase("LunarBlessing", "CresseliaLunarBlessing", 1)]
    [TestCase("Moonlight", "CresseliaMoonlight", 1)]
    public void FactoriesAssetsAndRulesAgree(string kind, string name, int slot)
    {
        object move = EnumValue("AvailableMoves", name);
        object runtime = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { move });
        Assert.That(runtime.GetType().Name, Is.EqualTo(name));
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Cresselia/" + name + ".asset");
        var data = new SerializedObject(asset);
        Assert.That(data.FindProperty("moveType").intValue, Is.EqualTo(slot));
        Assert.That(data.FindProperty("description").stringValue, Is.Not.Empty);
        Assert.That(data.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        Assert.That(runtime.GetType().GetField("Cooldown").GetValue(runtime), Is.EqualTo(data.FindProperty("cooldown").floatValue));
        Assert.That(Rule("Cooldown", EnumValue("CresseliaCast", kind)), Is.EqualTo(data.FindProperty("cooldown").floatValue));
        Assert.That(Rule("Slot", EnumValue("CresseliaCast", kind)), Is.EqualTo(slot));
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(registry.GetType().GetMethod("GetMoveAsset").Invoke(registry, new[] { move }), Is.SameAs(asset));
    }

    [TestCase("AuroraBeam")]
    [TestCase("Psybeam")]
    [TestCase("Moonblast")]
    [TestCase("Safeguard")]
    [TestCase("LunarBlessing")]
    [TestCase("Moonlight")]
    public void CastRecoveryIsShortAndSupportDoesNotWaitForProtection(string kind)
    {
        object move = EnumValue("CresseliaCast", kind);
        float windup = (float)Rule("Windup", move), recovery = (float)Rule("Recovery", move);
        Assert.That(windup + recovery, Is.LessThanOrEqualTo(0.4f));
        if ((bool)Rule("IsSupport", move)) Assert.That(windup, Is.Zero);
    }

    [TestCase("Psybeam", 9, false)]
    [TestCase("Psybeam", 10, true)]
    [TestCase("Moonblast", 10, true)]
    [TestCase("Moonlight", 11, false)]
    [TestCase("Moonlight", 12, true)]
    [TestCase("LunarBlessing", 12, true)]
    public void UpgradeThresholdsUseZeroBasedLevels(string kind, int level, bool expected)
    {
        Assert.That(Rule("Upgraded", EnumValue("CresseliaCast", kind), level), Is.EqualTo(expected));
    }

    [Test]
    public void AllFourBranchesHaveMeaningfulUpgrades()
    {
        foreach (string method in new[] { "MoonlightRadius", "PsybeamWidth", "MoonblastRadius", "ShieldRatio" })
            Assert.That((float)Rule(method, true), Is.GreaterThan((float)Rule(method, false)));
    }

    [TestCase(0, 0)]
    [TestCase(0.5f, 0)]
    [TestCase(6.5f, 5.5f)]
    public void RhythmCannotStoreOrOverfillBoostedAttacks(float remaining, float expected)
    {
        Assert.That(Rule("ReduceBoost", remaining), Is.EqualTo(expected));
    }

    [TestCase("Asleep", true, true)]
    [TestCase("Bound", true, true)]
    [TestCase("Burned", true, true)]
    [TestCase("Poisoned", true, true)]
    [TestCase("Invincible", true, false)]
    [TestCase("Unstoppable", true, false)]
    [TestCase("Asleep", false, false)]
    public void CleanseIncludesDamageOverTimeButNotBuffsOrPermanentEffects(string type, bool timed, bool expected)
    {
        object status = Activator.CreateInstance(GameType("StatusEffect"), EnumValue("StatusType", type), 2f, timed, (ushort)0);
        Assert.That(Rule("IsCleansable", status), Is.EqualTo(expected));
    }

    [Test]
    public void QuickAimRejectsNonFiniteAndVerticalVectors()
    {
        Assert.That(Rule("Direction", Vector3.zero, Vector3.zero), Is.EqualTo(Vector3.forward));
        Assert.That(Rule("Direction", new Vector3(float.NaN, 0, 1), Vector3.right), Is.EqualTo(Vector3.right));
        Vector3 direction = (Vector3)Rule("Direction", new Vector3(3, 100, 4), Vector3.forward);
        Assert.That(direction.y, Is.Zero); Assert.That(direction.magnitude, Is.EqualTo(1).Within(0.001f));
    }

    [Test]
    public void ShieldSourcesDoNotCollideAndDisposedIdsCanBeReused()
    {
        var pool = GameType("CresseliaEffectIds"); var acquire = pool.GetMethod("Acquire"); var release = pool.GetMethod("Release");
        var ids = new System.Collections.Generic.List<ushort>();
        try
        {
            for (int i = 0; i < 100; i++) ids.Add((ushort)acquire.Invoke(null, null));
            Assert.That(ids, Is.Unique);
            ushort first = ids[0]; release.Invoke(null, new object[] { first }); ids.RemoveAt(0);
            ushort reused = (ushort)acquire.Invoke(null, null); ids.Add(reused);
            Assert.That(reused, Is.EqualTo(first));
        }
        finally { foreach (ushort id in ids) release.Invoke(null, new object[] { id }); }
    }

    [Test]
    public void MultipleCollidersResolveToOnePokemonIncludingChildHitboxes()
    {
        var root = new GameObject("Cresselia target dedup test");
        try
        {
            root.AddComponent(GameType("Pokemon"));
            var a = root.AddComponent<BoxCollider>();
            var child = new GameObject("Child hitbox"); child.transform.SetParent(root.transform);
            var b = child.AddComponent<SphereCollider>();
            var result = (IEnumerable)GameType("CresseliaMoveRuntime").GetMethod("Targets").Invoke(null, new object[] { new Collider[] { a, b, a } });
            Assert.That(result.Cast<object>().Count(), Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase("CresseliaAuroraBeam")]
    [TestCase("CresseliaPsybeam")]
    [TestCase("CresseliaMoonblast")]
    [TestCase("CresseliaSafeguard")]
    [TestCase("CresseliaLunarBlessing")]
    [TestCase("CresseliaMoonlight")]
    [TestCase("CresseliaUnite")]
    public void ResetBeforeInitializationIsSafeAndIdempotent(string name)
    {
        object move = Activator.CreateInstance(GameType(name));
        var reset = move.GetType().GetMethod("ResetMove"); reset.Invoke(move, null); reset.Invoke(move, null);
        Assert.That(move.GetType().GetField("IsActive").GetValue(move), Is.False);
    }

    [Test]
    public void LearnsetAndRangedMetadataMatchTheKit()
    {
        var mon = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cresselia.asset"));
        var moves = mon.FindProperty("learnableMoves"); int[] levels = { 0, 2, 4, 6, 8, 10, 12 };
        Assert.That(moves.arraySize, Is.EqualTo(levels.Length));
        for (int i = 0; i < levels.Length; i++)
        {
            var entry = moves.GetArrayElementAtIndex(i);
            Assert.That(entry.FindPropertyRelative("level").intValue, Is.EqualTo(levels[i]));
            Assert.That(entry.FindPropertyRelative("isUpgraded").boolValue, Is.EqualTo(i >= 5));
            foreach (SerializedProperty reference in entry.FindPropertyRelative("moves")) Assert.That(reference.objectReferenceValue, Is.Not.Null);
        }
        Assert.That(mon.FindProperty("description").stringValue, Does.Contain("Lunar Rhythm"));
        var info = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/PokemonSelectInfo/Cresselia.asset"));
        Assert.That(info.FindProperty("Range").intValue, Is.EqualTo(1));
    }

    [Test]
    public void EveryLunarEffectIsAuthoredAddressableAndCosmeticOnly()
    {
        var group = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset"));
        var entries = group.FindProperty("m_SerializeEntries");
        foreach (object effect in Enum.GetValues(GameType("CresseliaEffect")))
        {
            string path = (string)Rule("EffectPath", effect);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null, path);
            var view = new SerializedObject(prefab.GetComponent(GameType("CresseliaVisual")));
            Assert.That(view.FindProperty("graphics").objectReferenceValue, Is.Not.Null);
            Assert.That(view.FindProperty("accent").objectReferenceValue, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponent(GameType("Unity.Netcode.NetworkObject")), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.Not.Empty);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials) { Assert.That(material, Is.Not.Null); Assert.That(material.shader, Is.Not.Null); }
            var sound = prefab.GetComponent<AudioSource>(); Assert.That(sound.clip, Is.Not.Null); Assert.That(sound.playOnAwake, Is.False);
            string guid = AssetDatabase.AssetPathToGUID(path); int matches = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("m_GUID").stringValue != guid) continue;
                matches++; Assert.That(entry.FindPropertyRelative("m_Address").stringValue, Is.EqualTo(path));
            }
            Assert.That(matches, Is.EqualTo(1), path);
        }
    }

    [Test]
    public void GroundGlyphsFaceTheGameplayCameraAndClipsHaveScopedSpeed()
    {
        foreach (string name in new[] { "LunarRing", "LunarCrescent" })
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Objects/Moves/Cresselia/Rework/Art/" + name + ".asset");
            Assert.That(mesh.normals.Count(n => n.y > 0.9f), Is.GreaterThan(mesh.vertexCount / 2), name);
        }
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Cresselia/Animations/Cresselia.controller");
        Assert.That(controller.parameters.Any(p => p.name == "CresseliaActionSpeed" && p.defaultFloat == 1), Is.True);
        foreach (string name in new[] { "pm0488_ba20_buturi01", "pm0488_ba21_tokusyu01", "pm0488_ba21_tokusyu02", "pm0488_ba01_land01" })
        {
            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == name).state;
            Assert.That(state.speedParameterActive, Is.True); Assert.That(state.speedParameter, Is.EqualTo("CresseliaActionSpeed"));
        }
        Assert.That(controller.layers[0].stateMachine.states.First(s => s.state.name == "pm0488_fi21_run01").state.speedParameterActive, Is.False);
    }

    [Test]
    public void EmboarReversalNoLongerDependsOnAnyCresseliaType()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Emboar/EmboarReversal.prefab");
        Assert.That(prefab.GetComponent(GameType("EmboarReversalProjectile")), Is.Not.Null);
        Assert.That(prefab.GetComponent(GameType("CresseliaPsybeamProjectile")), Is.Null);
        foreach (string path in Directory.GetFiles("Assets/Scripts/Moves/Emboar", "*.cs")) Assert.That(File.ReadAllText(path), Does.Not.Contain("Cresselia"), path);
    }

    [Test]
    public void RecastHudDoesNotPretendUniteEnergyWasRefunded()
    {
        var root = new GameObject("Cresselia recast UI test");
        try
        {
            var ui = root.AddComponent(GameType("UniteMoveUI"));
            Component Make(string field, string type)
            {
                var go = new GameObject(field); go.transform.SetParent(root.transform);
                Component component = go.AddComponent(GameType(type));
                ui.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, component);
                return component;
            }
            Make("bg", "UnityEngine.UI.Image"); var charge = Make("chargeIndicator", "UnityEngine.UI.Image");
            var text = Make("cdText", "TMPro.TextMeshProUGUI");
            var lockIcon = new GameObject("EnergyLock"); lockIcon.transform.SetParent(root.transform);
            ui.GetType().GetField("lockIcon", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, lockIcon);
            var update = ui.GetType().GetMethod("UpdateUI");
            update.Invoke(ui, new object[] { 0, 100000, true });
            Assert.That(lockIcon.activeSelf, Is.False);
            Assert.That(text.gameObject.activeSelf, Is.False);
            Assert.That(charge.GetType().GetProperty("fillAmount").GetValue(charge), Is.EqualTo(0f));
            update.Invoke(ui, new object[] { 0, 100000, false });
            Assert.That(lockIcon.activeSelf, Is.True);
            Assert.That(text.gameObject.activeSelf, Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void AuthoritativeDurationsAndBasicDamageFlagsAreExplicit()
    {
        Type rules = GameType("CresseliaMoveRules");
        Assert.That(rules.GetField("MoonlightDuration").GetRawConstantValue(), Is.EqualTo(5f));
        Assert.That(rules.GetField("MoonlightPulses").GetRawConstantValue(), Is.EqualTo(5));
        Assert.That(rules.GetField("BlessingDuration").GetRawConstantValue(), Is.EqualTo(3.5f));
        Assert.That(rules.GetField("FlightDuration").GetRawConstantValue(), Is.EqualTo(3f));
        Assert.That(rules.GetField("LandingDuration").GetRawConstantValue(), Is.EqualTo(0.3f));
        object damage = Rule("BasicDamage", 42UL, true);
        Assert.That(damage.GetType().GetField("attackerId").GetValue(damage), Is.EqualTo(42UL));
        Assert.That(damage.GetType().GetField("ratio").GetValue(damage), Is.EqualTo(0.5f));
        Assert.That(damage.GetType().GetField("proprieties").GetValue(damage).ToString(), Does.Contain("IsBasicAttack"));
    }

    [TestCase(0f)]
    [TestCase(7f)]
    [TestCase(-3f)]
    public void LandingUsesTerrainHeightInsteadOfWorldZero(float height)
    {
        var floor = new GameObject("Cresselia landing test terrain");
        var actor = new GameObject("Cresselia landing test actor");
        try
        {
            floor.transform.position = new Vector3(1000, height - 0.1f, 1000);
            floor.AddComponent<BoxCollider>().size = new Vector3(10, 0.2f, 10);
            actor.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            var movement = actor.AddComponent(GameType("PlayerMovement"));
            Physics.SyncTransforms();
            object[] args = { new Vector3(1000, height + 3, 1000), Vector3.zero };
            Assert.That(movement.GetType().GetMethod("TryCresseliaLanding").Invoke(movement, args), Is.True);
            Assert.That(((Vector3)args[1]).y, Is.EqualTo(height + 0.01f).Within(0.002f));
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(floor); }
    }
}
