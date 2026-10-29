using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PsyduckRacingRulesTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("PsyduckRaceBarrier")]
    [TestCase("PsyduckRaceBubble")]
    [TestCase("PsyduckRaceConfuseRay")]
    [TestCase("PsyduckRaceFreezeRay")]
    [TestCase("PsyduckRaceShield")]
    public void InactiveOrCancelledItemDoesNotSpawnOrClearAnotherItem(string moveName)
    {
        object move = Activator.CreateInstance(GameType(moveName));
        Assert.DoesNotThrow(() => move.GetType().GetMethod("Finish").Invoke(move, null));
        move.GetType().GetField("IsActive").SetValue(move, true);
        move.GetType().GetMethod("Cancel").Invoke(move, null);
        Assert.DoesNotThrow(() => move.GetType().GetMethod("Finish").Invoke(move, null));
    }

    private static void Variable(Component component, string fieldName, object value)
    {
        var field = component.GetType().GetField(fieldName, Private);
        field.SetValue(component, Activator.CreateInstance(field.FieldType, value,
            Enum.Parse(GameType("Unity.Netcode.NetworkVariableReadPermission"), "Everyone"),
            Enum.Parse(GameType("Unity.Netcode.NetworkVariableWritePermission"), "Server")));
    }

    [TestCase(1UL, 2UL)]
    [TestCase(2UL, 1UL)]
    [TestCase(3UL, 2UL)]
    [TestCase(4UL, 3UL)]
    [TestCase(99UL, 0UL)]
    public void HomingTargetsNearestRacerAheadWithLeaderFallback(ulong caster, ulong expected)
    {
        var root = new GameObject("Racing target rules"); root.SetActive(false);
        try
        {
            var manager = root.AddComponent(GameType("RaceManager"));
            var counters = (IDictionary)manager.GetType().GetProperty("PlayerLapCounters").GetValue(manager);
            for (ulong index = 1; index <= 4; index++)
            {
                var child = new GameObject("Lap counter"); child.transform.SetParent(root.transform);
                var counter = child.AddComponent(GameType("RaceLapCounter"));
                Variable(counter, "assignedPlayerID", index);
                Variable(counter, "currentPlace", (short)index);
                counters.Add(index, counter);
            }
            var target = manager.GetType().GetMethod("GetNextPlayer").Invoke(manager, new object[] { caster });
            Assert.That(target == null ? 0UL : target.GetType().GetProperty("AssignedPlayerID").GetValue(target), Is.EqualTo(expected));
            foreach (Component counter in counters.Values) Variable(counter, "raceFinished", true);
            Assert.That(manager.GetType().GetMethod("GetNextPlayer").Invoke(manager, new object[] { caster }), Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
