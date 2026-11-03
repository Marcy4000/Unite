using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

public class OpenWorldLobbyUiLifecycleTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var instance in objects) if (instance != null) Object.Destroy(instance);
        objects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator AuthoredLobbyUiDoesNotCreateObjectsOrComponentsAtStartup()
    {
        var lobbyType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("LobbyController"))
            .First(type => type != null);
        if ((Object)lobbyType.GetProperty("Instance").GetValue(null) != null)
            Assert.Ignore("An external lobby controller is active.");
#if UNITY_EDITOR
        foreach (var path in new[] { "Assets/Prefabs/Lobby/OpenWorldLobbyHUD.prefab", "Assets/Prefabs/Lobby/OpenWorldHomeButton.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            objects.Add(instance);
        }
#else
        Assert.Ignore("Requires the Editor-authored lobby UI prefabs.");
#endif
        int objectCount = objects.Sum(instance => instance.GetComponentsInChildren<Transform>(true).Length);
        int componentCount = objects.Sum(instance => instance.GetComponentsInChildren<Component>(true).Length);
        LogAssert.Expect(LogType.Warning, "Start from BootScene to connect to the open world lobby.");
        yield return null;
        yield return null;
        Assert.That(objects.Sum(instance => instance.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(objectCount));
        Assert.That(objects.Sum(instance => instance.GetComponentsInChildren<Component>(true).Length), Is.EqualTo(componentCount));
        var homeButton = objects[1].GetComponentInChildren<Button>(true);
        Assert.That(homeButton.gameObject.activeSelf, Is.False);
        Assert.That(objects[0].GetComponentsInChildren<Transform>(true)
            .Single(child => child.name == "City movement stick").gameObject.activeSelf, Is.EqualTo(Application.isMobilePlatform));
        LogAssert.NoUnexpectedReceived();
    }
}
