#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GlaceonMigrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Read(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name);
        return property != null ? property.GetValue(instance) : instance.GetType().GetField(name).GetValue(instance);
    }
    private static void Field(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);

    [TestCase("ExecuteSwift")]
    [TestCase("ExecuteTailWhip")]
    [TestCase("ExecuteIceShard")]
    [TestCase("ExecuteIcyWind")]
    [TestCase("ExecuteFreezeDry")]
    [TestCase("ExecuteIcicleSpear")]
    [TestCase("ExecuteUnite")]
    public void ExecutionHasNamedNoArgumentTimeline(string methodName)
    {
        MethodInfo method = GameType("GlaceonExecution").GetMethod(methodName, Private);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.ReturnType, Is.EqualTo(typeof(IEnumerator)));
        Assert.That(method.GetParameters(), Is.Empty);
    }

    [TestCase("GlaceSwift", "GlaceSwift", "None", 5f, 0f)]
    [TestCase("GlaceTailWhip", "GlaceTailWhip", "Target", 8f, 8f)]
    [TestCase("GlaceIceShard", "GlaceIceShard", "None", 8.5f, 0f)]
    [TestCase("GlaceFreezeDry", "GlaceFreezeDry", "Direction", 8.5f, 6f)]
    [TestCase("GlaceIcyWind", "GlaceIcyWind", "Dash", 6.5f, 4f)]
    [TestCase("GlaceIcicleSpear", "GlaceIcycleSpear", "Direction", 5f, 9f)]
    [TestCase("GlaceUnite", "GlaceonUnite", "Direction", 0f, 6.2f)]
    public void DefinitionsUseOptInCombatAndPreserveExecutableMetadata(string name, string moveId, string aim, float cooldown, float range)
    {
        object definition = Activator.CreateInstance(GameType(name));
        Assert.That(GameType("CombatMove").IsInstanceOfType(definition), Is.True);
        Assert.That(Read(definition, "PrefabPath"), Is.Null);
        Assert.That(Read(definition, "MoveId").ToString(), Is.EqualTo(moveId));
        Assert.That(Read(definition, "AimStyle").ToString(), Is.EqualTo(aim));
        Assert.That(Read(definition, "Cooldown"), Is.EqualTo(cooldown));
        Assert.That(Read(definition, "Range"), Is.EqualTo(range));
        Assert.That(Read(definition, "CooldownOnFinish"), Is.False);
        Assert.That(Read(definition, "CooldownOnEffectEnd"), Is.EqualTo(name == "GlaceIcicleSpear"));
        Assert.That(Read(definition, "StartsOnPress"), Is.EqualTo(name == "GlaceIcicleSpear"));
    }

    [Test]
    public void BasicAttackIsServerResolvedAndKeepsChargedPresentation()
    {
        object attack = Activator.CreateInstance(GameType("GlaceBasicAtk"));
        Assert.That(GameType("ChargedBasicAttackBase").IsInstanceOfType(attack), Is.True);
        Assert.That(Read(attack, "ServerResolved"), Is.True);
        attack.GetType().GetMethod("ApplyServerState").Invoke(attack, new object[] { 2, 0f });
        Assert.That(Read(attack, "Charge"), Is.EqualTo((byte)2));
        attack.GetType().GetProperty("Charge").SetValue(attack, (byte)1);
        Assert.That(Read(attack, "Charge"), Is.EqualTo((byte)2));
    }

    [TestCase(-100, 0)]
    [TestCase(0, 0)]
    [TestCase(2, 2)]
    [TestCase(8, 8)]
    [TestCase(9, 8)]
    [TestCase(100, 8)]
    public void CrystalCapacityIsBounded(int input, int expected)
    {
        object result = GameType("GlaceonCharacterRuntime").GetMethod("ClampCrystals").Invoke(null, new object[] { input });
        Assert.That(result, Is.EqualTo((byte)expected));
    }

    [Test]
    public void PersistentPresentationRestoresCrystalsModesAndLocksWithoutAnRpc()
    {
        WithReplica((runtime, actor) =>
        {
            var snapshot = Activator.CreateInstance(GameType("GlaceonCharacterRuntime+PresentationState"));
            snapshot.GetType().GetField("Revision").SetValue(snapshot, 17L);
            snapshot.GetType().GetField("Crystals").SetValue(snapshot, (byte)7);
            snapshot.GetType().GetField("Charge").SetValue(snapshot, (byte)2);
            snapshot.GetType().GetField("Mode").SetValue(snapshot, (byte)2);
            snapshot.GetType().GetField("CrystalUntil").SetValue(snapshot, 1234.5);
            foreach (string name in new[] { "SpearLocked", "ShardLocked", "MovementLocked", "Flying" })
                snapshot.GetType().GetField(name).SetValue(snapshot, true);
            string json = JsonUtility.ToJson(snapshot);
            runtime.GetType().GetMethod("ApplyPresentationState").Invoke(runtime, new object[] { json });
            Assert.That(Read(runtime, "Crystals"), Is.EqualTo((byte)7));
            Assert.That(Read(runtime, "Charge"), Is.EqualTo((byte)2));
            Assert.That(Read(runtime, "BasicMode"), Is.EqualTo((byte)2));
            Assert.That(Read(runtime, "CanStartBasicAttack"), Is.False);
            string roundTrip = (string)runtime.GetType().GetMethod("CapturePresentationState").Invoke(runtime, null);
            Assert.That(roundTrip, Is.EqualTo(json));
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(roundTrip), Is.LessThan(4096));
        });
    }

    [Test]
    public void StaleSnapshotCannotUndoANewerImmediateTransition()
    {
        WithReplica((runtime, actor) =>
        {
            var method = runtime.GetType().GetMethod("ReceiveSnapshot");
            method.Invoke(runtime, new object[] { (byte)8, 1234.5, (byte)2, (byte)1, false, true, false, false, 10L });
            method.Invoke(runtime, new object[] { (byte)1, 1.0, (byte)0, (byte)0, true, false, true, true, 9L });
            Assert.That(Read(runtime, "Crystals"), Is.EqualTo((byte)8));
            Assert.That(Read(runtime, "Charge"), Is.EqualTo((byte)2));
            Assert.That(Read(runtime, "BasicMode"), Is.EqualTo((byte)1));
            Assert.That(Read(runtime, "CanStartBasicAttack"), Is.True);
        });
    }

    [Test]
    public void OwnerCannotChangeAuthoritativeCrystalsOrCharge()
    {
        WithReplica((runtime, actor) =>
        {
            runtime.GetType().GetMethod("SetCrystals").Invoke(runtime, new object[] { 8 });
            runtime.GetType().GetMethod("FillCharge").Invoke(runtime, null);
            Assert.That(Read(runtime, "Crystals"), Is.EqualTo((byte)0));
            Assert.That(Read(runtime, "Charge"), Is.EqualTo((byte)0));
            object passive = Activator.CreateInstance(GameType("GlaceonPassive"));
            passive.GetType().GetMethod("Start").Invoke(passive, new[] { actor });
            Assert.DoesNotThrow(() => passive.GetType().GetMethod("UpdateIciclesCount").Invoke(passive, new object[] { (byte)8 }));
            Assert.That(Read(passive, "IciclesCount"), Is.EqualTo((byte)0));
        });
    }

    [Test]
    public void PresentationRejectsMalformedJsonAndClampsFields()
    {
        WithReplica((runtime, actor) =>
        {
            var apply = runtime.GetType().GetMethod("ApplyPresentationState");
            Assert.DoesNotThrow(() => apply.Invoke(runtime, new object[] { "not json" }));
            apply.Invoke(runtime, new object[] { "{\"Revision\":1,\"Crystals\":255,\"Charge\":255,\"Mode\":255}" });
            Assert.That(Read(runtime, "Crystals"), Is.EqualTo((byte)8));
            Assert.That(Read(runtime, "Charge"), Is.EqualTo((byte)2));
            Assert.That(Read(runtime, "BasicMode"), Is.EqualTo((byte)2));
        });
    }

    [TestCase("FreezeDryProjectile")]
    [TestCase("IcicleSpearHitbox")]
    [TestCase("GlaceUniteArea")]
    public void NonServerEffectInitializationIsInert(string name)
    {
        var root = new GameObject("Untrusted Glaceon effect");
        try
        {
            root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            Component effect = root.AddComponent(GameType(name));
            object damage = Activator.CreateInstance(GameType("DamageInfo"));
            object team = Activator.CreateInstance(GameType("Team"));
            if (name == "FreezeDryProjectile")
            {
                effect.GetType().GetMethod("SetDirection").Invoke(effect, new[] { (object)Vector3.zero, Vector2.right, damage, 6f });
                Assert.That(effect.GetType().GetField("canMove", Private).GetValue(effect), Is.False);
            }
            else if (name == "IcicleSpearHitbox")
            {
                effect.GetType().GetMethod("InitializeServer").Invoke(effect, new[] { damage, team, false });
                Assert.That(effect.GetType().GetField("initialized", Private).GetValue(effect), Is.False);
            }
            else
            {
                effect.GetType().GetMethod("InitializeServer").Invoke(effect, new[] { (object)Vector3.zero, Quaternion.identity, damage, team, 0UL });
                Assert.That(effect.GetType().GetField("initialized", Private).GetValue(effect), Is.False);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void ScopedCleanupIsIdempotent()
    {
        int count = 0;
        var lease = (IDisposable)Activator.CreateInstance(GameType("GlaceonCleanup"), new object[] { (Action)(() => count++) });
        lease.Dispose();
        lease.Dispose();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void NonServerCannotPrepareCustomExecutionAndStopIsIdempotent()
    {
        WithReplica((runtime, actor) =>
        {
            object context = Activator.CreateInstance(GameType("CombatExecutionContext"));
            context.GetType().GetField("Actor").SetValue(context, actor);
            context.GetType().GetField("Direction").SetValue(context, Vector3.forward);
            context.GetType().GetField("MoveId").SetValue(context, Enum.Parse(GameType("AvailableMoves"), "GlaceFreezeDry"));
            object execution = runtime.GetType().GetMethod("CreateExecution").Invoke(runtime, new[] { context });
            var prepare = (IEnumerator)execution.GetType().GetMethod("Prepare").Invoke(execution, null);
            Assert.That(prepare.MoveNext(), Is.False);
            Assert.That(Read(execution, "Prepared"), Is.False);
            object reason = Enum.Parse(GameType("Unite.Combat.CastOutcome"), "Cancelled");
            Assert.DoesNotThrow(() => execution.GetType().GetMethod("Stop").Invoke(execution, new[] { reason }));
            Assert.DoesNotThrow(() => execution.GetType().GetMethod("Stop").Invoke(execution, new[] { reason }));
        });
    }

    private static void WithReplica(Action<object, object> test)
    {
        var root = new GameObject("Glaceon presentation replica");
        root.SetActive(false);
        IDisposable runtime = null;
        try
        {
            root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            Component controller = root.AddComponent(GameType("MovesController"));
            Component actor = root.AddComponent(GameType("PlayerManager"));
            Field(actor, "movesController", controller);
            runtime = (IDisposable)Activator.CreateInstance(GameType("GlaceonCharacterRuntime"), new object[] { actor });
            test(runtime, actor);
        }
        finally
        {
            runtime?.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
