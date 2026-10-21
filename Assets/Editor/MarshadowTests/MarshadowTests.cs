using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Assembly-CSharp is a predefined assembly; reflect its production types, as
// the existing Duraludon suite does, rather than duplicating game logic here.
public class MarshadowTests
{
    private int successfulCompletions;
    private void ObserveCompletion(object move)
    {
        if ((bool)move.GetType().GetField("wasMoveSuccessful").GetValue(move)) successfulCompletions++;
    }
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static object Rule(string method, params object[] args) => GameType("MarshadowMoveRules").GetMethod(method).Invoke(null, args);
    private static object Value(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static object EnumValue(string type, string name) => Enum.Parse(GameType(type), name);

    [Test]
    public void ScopedLocksDoNotClearLegacyOrOtherCastsRestrictions()
    {
        object status = Activator.CreateInstance(GameType("BattleActionStatus"), new object[] { 0f });
        object disabled = EnumValue("ActionStatusType", "Disabled");
        object none = EnumValue("ActionStatusType", "None");
        Call(status, "AddStatus", disabled);
        var a = (IDisposable)Call(status, "AcquireStatus", disabled);
        var b = (IDisposable)Call(status, "AcquireStatus", disabled);
        a.Dispose(); a.Dispose();
        Assert.That(Call(status, "HasStatus", disabled), Is.True);
        Call(status, "RemoveStatus", disabled);
        Assert.That(Call(status, "HasStatus", disabled), Is.True);
        b.Dispose();
        Assert.That(Call(status, "HasStatus", none), Is.True);
    }
    [Test]
    public void ScopedLockDisposalPreservesCooldownAndStun()
    {
        object status = Activator.CreateInstance(GameType("BattleActionStatus"), new object[] { 9f });
        object cooldown = EnumValue("ActionStatusType", "Cooldown"), stun = EnumValue("ActionStatusType", "Stunned");
        Call(status, "AddStatus", cooldown); Call(status, "AddStatus", stun);
        var lease = (IDisposable)Call(status, "AcquireStatus", EnumValue("ActionStatusType", "Disabled"));
        lease.Dispose();
        Assert.That(Call(status, "HasStatus", cooldown), Is.True);
        Assert.That(Call(status, "HasStatus", stun), Is.True);
        Assert.That(Value(status, "Cooldown"), Is.EqualTo(9f));
    }
    [Test]
    public void PassiveRefreshesAtTheCapAndExpiresFromTheLatestGain()
    {
        object state = Activator.CreateInstance(GameType("MarshadowStackState"));
        Call(state, "Gain", 0f); Call(state, "Gain", 2.9f); Call(state, "Gain", 3f); Call(state, "Gain", 6f);
        Assert.That(Value(state, "Count"), Is.EqualTo(3));
        Assert.That(Value(state, "ExpiresAt"), Is.EqualTo(10f));
        Assert.That(Call(state, "Tick", 9.99f), Is.False);
        Assert.That(Call(state, "Tick", 10f), Is.True);
        Assert.That(Value(state, "Count"), Is.Zero);
        Assert.That(Call(state, "Tick", 11f), Is.False);
        Call(state, "Gain", 20f); Call(state, "Clear");
        Assert.That(Value(state, "Count"), Is.Zero);
    }
    [Test]
    public void PassiveDoesNotCarryExpiredStacksIntoANewCombo()
    {
        object state = Activator.CreateInstance(GameType("MarshadowStackState"));
        Call(state, "Gain", 0f); Call(state, "Gain", 1f); Call(state, "Gain", 10f);
        Assert.That(Value(state, "Count"), Is.EqualTo(1));
    }
    [TestCase(0, false, false, 250)]
    [TestCase(1000, false, false, 450)]
    [TestCase(1000, true, false, 500)]
    [TestCase(10000, true, true, 700)]
    [TestCase(-100, true, false, 250)]
    [TestCase(1000000, true, false, 32767)]
    public void AuraExecuteHasAnUpgradeAndWildCap(int missing, bool upgraded, bool wild, int expected)
    {
        Assert.That(Rule("AuraExecute", missing, upgraded, wild), Is.EqualTo(expected));
    }
    [TestCase(0.2f, false, 0.2f)]
    [TestCase(20f, false, 6f)]
    [TestCase(20f, true, 8f)]
    [TestCase(-1f, true, 0f)]
    public void TheftKeepsRemainingDurationRatherThanRestartingOriginalDuration(float remaining, bool upgraded, float expected)
    {
        Assert.That(Rule("StolenDuration", remaining, upgraded), Is.EqualTo(expected));
    }
    [TestCase(true, true, true, 1f, true)]
    [TestCase(false, true, true, 1f, false)]
    [TestCase(true, false, true, 1f, false)]
    [TestCase(true, true, false, 1f, false)]
    [TestCase(true, true, true, 0f, false)]
    public void TheftRejectsDebuffsPermanentProtectedAndExpiredEffects(bool buff, bool timed, bool removable, float remaining, bool expected)
    {
        object change = Activator.CreateInstance(GameType("StatChange"));
        foreach (var pair in new[] { ("IsBuff", buff), ("IsTimed", timed), ("CanBeRemoved", removable) }) change.GetType().GetField(pair.Item1).SetValue(change, pair.Item2);
        Assert.That(Rule("CanSteal", change, remaining), Is.EqualTo(expected));
    }
    [Test]
    public void ZeroAimFallsBackToAFlatNormalizedDirection()
    {
        Assert.That(Rule("Direction", Vector3.zero, Vector3.zero), Is.EqualTo(Vector3.forward));
        Vector3 direction = (Vector3)Rule("Direction", new Vector3(3, 50, 4), Vector3.forward);
        Assert.That(direction.y, Is.Zero); Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.001f));
    }
    [TestCase("MarshadowDrainPunch")]
    [TestCase("MarshadowAuraSphere")]
    [TestCase("MarshadowCloseCombat")]
    [TestCase("MarshadowSpectralThief")]
    [TestCase("MarshadowPhantomForce")]
    public void ResetIsIdempotentAndCannotSpendACommittedSequenceTwice(string type)
    {
        successfulCompletions = 0;
        object move = Activator.CreateInstance(GameType(type));
        EventInfo finished = GameType("MoveBase").GetEvent("onMoveOver");
        Delegate observer = Delegate.CreateDelegate(finished.EventHandlerType, this,
            GetType().GetMethod(nameof(ObserveCompletion), BindingFlags.Instance | BindingFlags.NonPublic));
        finished.AddEventHandler(move, observer);
        GameType("MarshadowMoveBase").GetField("committed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(move, true);
        Call(move, "ResetMove"); Call(move, "ResetMove");
        Assert.That(successfulCompletions, Is.EqualTo(1));
        Assert.That(Value(move, "IsExecuting"), Is.False);
        Assert.That(move.GetType().GetField("IsActive").GetValue(move), Is.False);
    }
    [Test]
    public void ExistingMoveClipsHaveScopedSpeedControlButLocomotionDoesNot()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Model/Pokemons/Marshadow/Marshadow.controller");
        Assert.That(controller.parameters.Any(p => p.name == "MarshadowActionSpeed" && p.defaultFloat == 1f), Is.True);
        foreach (string name in new[] { "pm0883_ba20_buturi01", "pm0883_ba20_buturi02", "pm0883_ba21_tokusyu01", "pm0883_ba21_tokusyu02" })
        {
            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == name).state;
            Assert.That(state.speedParameterActive, Is.True);
            Assert.That(state.speedParameter, Is.EqualTo("MarshadowActionSpeed"));
        }
        foreach (string name in new[] { "pm0883_fi21_run01", "pm0883_ba10_waitA01" })
            Assert.That(controller.layers[0].stateMachine.states.First(s => s.state.name == name).state.speedParameterActive, Is.False);
    }
    [TestCase("MarshadowDrainPunch", 0)]
    [TestCase("MarshadowShadowSneak", 1)]
    [TestCase("MarshadowAuraSphere", 0)]
    [TestCase("MarshadowCloseCombat", 0)]
    [TestCase("MarshadowSpectralThief", 1)]
    [TestCase("MarshadowPhantomForce", 1)]
    [TestCase("MarshadowUnite", 2)]
    public void MoveAssetsFactoriesRegistryAndCooldownsAgree(string name, int slot)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Marshadow/" + name + ".asset");
        var serialized = new SerializedObject(asset);
        object move = EnumValue("AvailableMoves", name);
        object runtime = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { move });
        Assert.That(runtime.GetType().Name, Is.EqualTo(name));
        Assert.That(serialized.FindProperty("moveType").intValue, Is.EqualTo(slot));
        Assert.That(serialized.FindProperty("description").stringValue, Is.Not.Empty);
        Assert.That(serialized.FindProperty("icon").objectReferenceValue, Is.Not.Null);
        if (slot != 2) Assert.That(runtime.GetType().GetField("Cooldown").GetValue(runtime), Is.EqualTo(serialized.FindProperty("cooldown").floatValue));
        else Assert.That(serialized.FindProperty("uniteEnergyCost").intValue, Is.EqualTo(107100));
        var registry = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/MoveAssetRegistry.asset");
        Assert.That(Call(registry, "GetMoveAsset", move), Is.SameAs(asset));
    }
    [Test]
    public void CopyAdaptersNeverBorrowAnArbitraryForeignRuntimeOrMirrorItself()
    {
        Type rules = GameType("MarshadowCopyRules");
        foreach (string name in new[] { "BlazingBycicleKick", "GlaceonUnite", "SylveonUnite", "VaporeonUnite", "EmboarUnite", "FlareonUnite", "DuraludonUnite" })
        {
            object move = EnumValue("AvailableMoves", name);
            Assert.That(rules.GetMethod("Supports").Invoke(null, new[] { move }), Is.True);
            Assert.That(rules.GetMethod("Description").Invoke(null, new[] { move }), Is.Not.Empty);
        }
        foreach (string name in new[] { "MarshadowUnite", "LockedMove", "FlygonUnite", "CresseliaUnite", "MeowsticMUnite", "JolteonUnite", "SerperiorUnite" })
            Assert.That(rules.GetMethod("Supports").Invoke(null, new[] { EnumValue("AvailableMoves", name) }), Is.False);
    }
    [Test]
    public void LearnsetPreservesLicenseReferencesAndRealUpgradeLevels()
    {
        var asset = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Marshadow.asset"));
        int[] expected = { 0, 2, 4, 6, 8, 10, 12 };
        var moves = asset.FindProperty("learnableMoves");
        Assert.That(moves.arraySize, Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            var entry = moves.GetArrayElementAtIndex(i);
            Assert.That(entry.FindPropertyRelative("level").intValue, Is.EqualTo(expected[i]));
            Assert.That(entry.FindPropertyRelative("isUpgraded").boolValue, Is.EqualTo(i >= 5));
        }
    }
    [Test]
    public void EveryEffectIsAuthoredAndAddressableWithoutNetworkOrRuntimeGeometry()
    {
        Type effects = GameType("MarshadowEffect");
        var group = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData/AssetGroups/MovesAssets.asset"));
        var entries = group.FindProperty("m_SerializeEntries");
        foreach (object effect in Enum.GetValues(effects))
        {
            string path = (string)Rule("EffectPath", effect);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var view = new SerializedObject(prefab.GetComponent(GameType("MarshadowVisual")));
            Assert.That(view.FindProperty("graphics").objectReferenceValue, Is.Not.Null);
            Assert.That(view.FindProperty("accent").objectReferenceValue, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty, "Presentation must not obstruct or determine hits.");
            Assert.That(prefab.GetComponent(GameType("Unity.Netcode.NetworkObject")), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.Not.Empty);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials) Assert.That(material, Is.Not.Null);
            var sound = prefab.GetComponent<AudioSource>();
            if (sound != null) { Assert.That(sound.clip, Is.Not.Null); Assert.That(sound.playOnAwake, Is.False); }
            string guid = AssetDatabase.AssetPathToGUID(path);
            int matches = 0;
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
    public void SpectralDamageAndTheftRingsMatchGameplayRadii()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)Rule("EffectPath", EnumValue("MarshadowEffect", "SpectralThief")));
        Assert.That(prefab.transform.Find("Graphics/Accent/DamageBoundary").localScale.x,
            Is.EqualTo((float)GameType("MarshadowMoveRules").GetField("SpectralRadius").GetValue(null)));
        Assert.That(prefab.transform.Find("Graphics/Accent/TheftSweetSpot").localScale.x,
            Is.EqualTo((float)GameType("MarshadowMoveRules").GetField("TheftRadius").GetValue(null)));
    }
    [Test]
    public void BasicAttacksDoNotRequestAnimationsAndReworkDoesNotUseLegacySpawnCallbacks()
    {
        string basic = File.ReadAllText("Assets/Scripts/Moves/Marshadow/MarshadowBasicAtk.cs");
        Assert.That(basic, Does.Not.Contain("PlayAnimation("));
        foreach (string path in Directory.GetFiles("Assets/Scripts/Moves/Marshadow", "*.cs"))
        {
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Not.Contain("onObjectSpawned"), path);
            foreach (string forbidden in new[] { "CreatePrimitive(", "new Mesh ", "AddComponent<LineRenderer>", ".DOMove(", ".DOJump(" })
                Assert.That(source, Does.Not.Contain(forbidden), path);
        }
    }
}
