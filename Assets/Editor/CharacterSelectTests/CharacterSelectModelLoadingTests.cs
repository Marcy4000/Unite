using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

public class CharacterSelectModelLoadingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<AsyncOperationHandle<GameObject>> handles = new List<AsyncOperationHandle<GameObject>>();
    private ResourceManager resourceManager;
    private GameObject root;
    private GameObject model;
    private GameObject background;
    private Transform spawnPoint;
    private Component controller;

    private class PendingModelLoad : AsyncOperationBase<GameObject>
    {
        protected override void Execute() { }
        public void Finish(GameObject result) => Complete(result, true, (string)null);
    }

    private void Initialize(string controllerName)
    {
        resourceManager = new ResourceManager();
        root = new GameObject("Character select loading test");
        root.SetActive(false); // Avoid starting networking or changing the open scene.
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(controllerName))
            .First(candidate => candidate != null);
        controller = root.AddComponent(type);
        spawnPoint = new GameObject("Preview spawn point").transform;
        spawnPoint.SetParent(root.transform);
        Set("pokemonSpawnPoint", spawnPoint);
        model = new GameObject("Test Pokemon model");
        background = new GameObject("Loading background");
        background.transform.SetParent(root.transform);
        if (controllerName == "DraftSelectController") Set("background", background);
    }

    private void Set(string name, object value) => controller.GetType().GetField(name, PrivateInstance).SetValue(controller, value);
    private void Call(string name, params object[] args) => controller.GetType().GetMethod(name, PrivateInstance).Invoke(controller, args);

    private void Select(AsyncOperationHandle<GameObject> handle)
    {
        Set("characterSelectModelHandle", handle);
        var callback = (Action<AsyncOperationHandle<GameObject>>)Delegate.CreateDelegate(
            typeof(Action<AsyncOperationHandle<GameObject>>), controller,
            controller.GetType().GetMethod("OnCharacterSelectModelLoaded", PrivateInstance));
        handle.Completed += callback;
    }

    [TearDown]
    public void TearDown()
    {
        if (controller != null)
        {
            Set("currentPokemon", null);
            Set("characterSelectModelHandle", default(AsyncOperationHandle<GameObject>));
        }
        if (root != null) Object.DestroyImmediate(root);
        if (model != null) Object.DestroyImmediate(model);
        foreach (var handle in handles)
            if (handle.IsValid()) resourceManager.Release(handle);
        handles.Clear();
        resourceManager?.Dispose();
    }

    [TestCase("CharacterSelectController", true)]
    [TestCase("CharacterSelectController", false)]
    [TestCase("DraftSelectController", true)]
    [TestCase("DraftSelectController", false)]
    public void OnlyLatestSelectionSpawnsRegardlessOfCompletionOrder(string controllerName, bool olderFinishesFirst)
    {
        Initialize(controllerName);
        var older = resourceManager.CreateCompletedOperation(model, (string)null);
        var latest = resourceManager.CreateCompletedOperation(model, (string)null);
        handles.Add(older);
        handles.Add(latest);
        Set("characterSelectModelHandle", latest);

        var first = olderFinishesFirst ? older : latest;
        var second = olderFinishesFirst ? latest : older;
        Call("OnCharacterSelectModelLoaded", first);
        Assert.That(spawnPoint.childCount, Is.EqualTo(olderFinishesFirst ? 0 : 1));
        Call("OnCharacterSelectModelLoaded", second);
        Assert.That(spawnPoint.childCount, Is.EqualTo(1));
    }

    [TestCase("CharacterSelectController")]
    [TestCase("DraftSelectController")]
    public void ReselectingSamePendingAssetDoesNotKeepOldCallback(string controllerName)
    {
        Initialize(controllerName);
        var operation = new PendingModelLoad();
        var handle = resourceManager.StartOperation(operation, default);
        handles.Add(handle);
        resourceManager.Acquire(handle); // Keep the shared operation alive across reselection.
        Select(handle);
        Call("ClearPokemonPreview");
        Select(handle);

        operation.Finish(model);

        Assert.That(spawnPoint.childCount, Is.EqualTo(1), "The old subscription must be removed, even for a shared handle.");
    }

    [TestCase("CharacterSelectController")]
    [TestCase("DraftSelectController")]
    public void DisablingScreenPreventsPendingModelFromSpawning(string controllerName)
    {
        Initialize(controllerName);
        var operation = new PendingModelLoad();
        var handle = resourceManager.StartOperation(operation, default);
        handles.Add(handle);
        resourceManager.Acquire(handle);
        Select(handle);

        Call("OnDisable");
        Call("ClearPokemonPreview"); // Cleanup must also be safe when repeated.
        operation.Finish(model);

        Assert.That(spawnPoint.childCount, Is.Zero);
    }
}
