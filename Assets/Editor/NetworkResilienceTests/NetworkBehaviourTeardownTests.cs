using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class NetworkBehaviourTeardownTests
{
    // Gameplay lives in Assembly-CSharp, which cannot be referenced by an asmdef.
    // Exercise its public lifecycle via reflection without contacting UGS or
    // starting a NetworkManager in the user's currently open scene.
    [TestCase("GameManager")]
    [TestCase("PlayerNetworkManager")]
    [TestCase("RacingReadyScreenManager")]
    public void TeardownIsSafeBeforeNetworkStartupAndWhenRepeated(string typeName)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Gameplay type {typeName} must be loaded.");
        var gameObject = new GameObject("Network teardown regression test");
        try
        {
            var component = gameObject.AddComponent(type);
            var despawn = type.GetMethod("OnNetworkDespawn");
            Assert.That(despawn, Is.Not.Null);
            Assert.DoesNotThrow(() => despawn.Invoke(component, null));
            Assert.DoesNotThrow(() => despawn.Invoke(component, null));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
        LogAssert.NoUnexpectedReceived();
    }
}
