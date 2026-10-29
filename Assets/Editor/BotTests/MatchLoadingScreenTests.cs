using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MatchLoadingScreenTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private GameObject holder;
    private Component loadingScreen;
    private Component gameManager;
    private object previousLoadingScreen;
    private object previousGameManager;
    private FieldInfo loadingInstance;
    private FieldInfo gameInstance;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [SetUp]
    public void SetUp()
    {
        loadingInstance = GameType("LoadingScreen").GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        gameInstance = GameType("GameManager").GetField("Instance");
        previousLoadingScreen = loadingInstance.GetValue(null);
        previousGameManager = gameInstance.GetValue(null);
        root = new GameObject("Match loading regression");
        root.SetActive(false);
        loadingScreen = root.AddComponent(GameType("LoadingScreen"));
        gameManager = root.AddComponent(GameType("GameManager"));
        holder = new GameObject("Loading overlay");
        holder.transform.SetParent(root.transform);
        loadingScreen.GetType().GetField("holder", Private).SetValue(loadingScreen, holder);
        loadingInstance.SetValue(null, loadingScreen);
        gameInstance.SetValue(null, gameManager);
    }

    [TearDown]
    public void TearDown()
    {
        loadingInstance.SetValue(null, previousLoadingScreen);
        gameInstance.SetValue(null, previousGameManager);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private object State(string name) => Enum.Parse(GameType("GameState"), name);

    private void ChangeState(string previous, string current) => gameManager.GetType()
        .GetMethod("GameStateChanged", Private).Invoke(gameManager, new[] { State(previous), State(current) });

    [Test]
    public void FastClientKeepsOverlayAfterLocalStartRoutineCompletes()
    {
        var routine = (IEnumerator)gameManager.GetType().GetMethod("StartGameRoutine").Invoke(gameManager, null);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(holder.activeSelf, Is.True);
        ChangeState("Waiting", "Initialising");
        Assert.That(holder.activeSelf, Is.True);
    }

    [Test]
    public void AuthoritativeStartingStateHidesOverlayAfterAnimationHandlersRun()
    {
        bool animationInitialized = false;
        var stateEvent = gameManager.GetType().GetEvent("onGameStateChanged");
        var handlerMethod = GetType().GetMethod(nameof(Subscribe), Private)
            .MakeGenericMethod(GameType("GameState"));
        handlerMethod.Invoke(this, new object[] { stateEvent, (Action)(() =>
        {
            Assert.That(holder.activeSelf, Is.True);
            animationInitialized = true;
        }) });

        ChangeState("Initialising", "Starting");

        Assert.That(animationInitialized, Is.True);
        Assert.That(holder.activeSelf, Is.False);
    }

    private void Subscribe<TState>(EventInfo stateEvent, Action onStateChanged)
    {
        stateEvent.AddEventHandler(gameManager, (Action<TState>)(_ => onStateChanged()));
    }
}
