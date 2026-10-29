using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GlaceUniteLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("ApplyBuff")]
    [TestCase("ApplyDebuff")]
    [TestCase("DamageEnemies")]
    public void AreaRoutinesPruneDestroyedAndDespawnedTargetsAndStopWhenInactive(string method)
    {
        var root = new GameObject("Glaceon zone lifecycle");
        var target = new GameObject("Stale zone target");
        var destroyedTarget = new GameObject("Destroyed zone target");
        Component area = null;
        try
        {
            root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            area = root.AddComponent(GameType("GlaceUniteArea"));
            target.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            destroyedTarget.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            var targets = (IList)area.GetType().GetField("pokemonInZone", Private).GetValue(area);
            targets.Add(target.AddComponent(GameType("Pokemon")));
            targets.Add(destroyedTarget.AddComponent(GameType("Pokemon")));
            targets.Add(null);
            UnityEngine.Object.DestroyImmediate(destroyedTarget);
            GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsSpawned").SetValue(area, true);
            object active = area.GetType().GetField("zoneActive", Private).GetValue(area);
            active.GetType().GetProperty("Value").SetValue(active, true);
            var routine = (IEnumerator)area.GetType().GetMethod(method, Private).Invoke(area, null);
            Assert.DoesNotThrow(() => routine.MoveNext());
            if (method == "DamageEnemies") Assert.That(routine.MoveNext(), Is.False);
            Assert.That(targets.Count, Is.Zero);
            active.GetType().GetProperty("Value").SetValue(active, false);
            Assert.That(routine.MoveNext(), Is.False);
        }
        finally
        {
            if (area != null) GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsSpawned").SetValue(area, false);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(target);
            if (destroyedTarget != null) UnityEngine.Object.DestroyImmediate(destroyedTarget);
        }
    }

    [TestCase("ApplyBuff")]
    [TestCase("ApplyDebuff")]
    [TestCase("DamageEnemies")]
    public void AreaDespawnClearsTargetsAndPreventsFurtherPulses(string method)
    {
        var root = new GameObject("Glaceon despawn cleanup");
        try
        {
            root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
            Component area = root.AddComponent(GameType("GlaceUniteArea"));
            var targets = (IList)area.GetType().GetField("pokemonInZone", Private).GetValue(area);
            targets.Add(null);
            object active = area.GetType().GetField("zoneActive", Private).GetValue(area);
            active.GetType().GetProperty("Value").SetValue(active, true);
            var routine = (IEnumerator)area.GetType().GetMethod(method, Private).Invoke(area, null);
            if (method == "DamageEnemies") Assert.That(routine.MoveNext(), Is.True);
            for (int attempt = 0; attempt < 2; attempt++) area.GetType().GetMethod("OnNetworkDespawn").Invoke(area, null);
            Assert.That(targets.Count, Is.Zero);
            Assert.That(routine.MoveNext(), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
