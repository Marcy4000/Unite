using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MapPlayerIconTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component pokemon;
    private Component vision;
    private Component icon;
    private GameObject[] visuals;

    // Gameplay lives in Assembly-CSharp, so match the other Editor fixtures
    // and access it through reflection without starting a lobby or network game.
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, Private).SetValue(target, value);

    private static void Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Invoke(target, args);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Minimap visibility regression test");
        root.SetActive(false);
        pokemon = root.AddComponent(GameType("Pokemon"));
        vision = root.AddComponent(GameType("Vision"));
        var player = root.AddComponent(GameType("PlayerManager"));
        Set(player, "pokemon", pokemon);
        Set(player, "vision", vision);

        var iconObject = new GameObject("Player icon", typeof(RectTransform));
        iconObject.transform.SetParent(root.transform);
        icon = iconObject.AddComponent(GameType("MapPlayerIcon"));
        Set(icon, "player", player);

        string[] fields = { "pokemonIcon", "background", "scoreCircle", "scoreText" };
        visuals = new GameObject[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            visuals[i] = new GameObject(fields[i], typeof(RectTransform));
            visuals[i].transform.SetParent(iconObject.transform);
            var component = visuals[i].AddComponent(GameType(i == 3
                ? "TMPro.TextMeshProUGUI" : "UnityEngine.UI.Image"));
            Set(icon, fields[i], component);
        }

        var refresh = (Action)Delegate.CreateDelegate(typeof(Action), icon,
            icon.GetType().GetMethod("RefreshVisibility", Private));
        pokemon.GetType().GetEvent("OnHpChange").AddEventHandler(pokemon, refresh);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
    }

    private void Health(int hp)
    {
        var variable = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon);
        variable.GetType().GetProperty("Value").SetValue(variable, hp);
        // Simulate the replicated HP notification without spawning NGO objects.
        ((Action)pokemon.GetType().GetField("OnHpChange", Private).GetValue(pokemon))?.Invoke();
    }

    private void AssertVisible(bool expected)
    {
        foreach (var visual in visuals)
            Assert.That(visual.activeSelf, Is.EqualTo(expected), visual.name);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void KnockoutStaysHiddenDespiteVisibilityUpdates(bool teammate)
    {
        Set(icon, "isTeammate", teammate);
        Set(vision, "isRendered", true);
        Health(100);
        AssertVisible(true);

        Health(0);
        AssertVisible(false);
        Call(icon, "SetVisibility", true);
        AssertVisible(false);
        Call(icon, "ShowIcon");
        AssertVisible(false);
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    public void RespawnRestoresOnlyTeammatesOrVisibleEnemies(bool teammate, bool rendered, bool expected)
    {
        Set(icon, "isTeammate", teammate);
        Set(vision, "isRendered", rendered);
        Health(0);
        AssertVisible(false);

        Health(100);
        AssertVisible(expected);
    }

    [Test]
    public void LivingEnemyStillFollowsVision()
    {
        Health(100);
        Call(icon, "SetVisibility", true);
        AssertVisible(true);
        Call(icon, "SetVisibility", false);
        AssertVisible(false);
    }
}
