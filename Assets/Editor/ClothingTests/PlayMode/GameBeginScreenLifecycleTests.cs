using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameBeginScreenLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private GameObject services;
    private GameObject sources;
    private Component screen;
    private Component lobby;
    private Component preloader;
    private object session;
    private object oldLobby;
    private object oldPreloader;
    private object oldClothesList;
    private float oldTimeScale;
    private Component[] previews;
    private IDictionary models;
    private object outfit;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, Private | BindingFlags.Public).Invoke(target, args);

    private GameObject Child(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    [SetUp]
    public void SetUp()
    {
        oldTimeScale = Time.timeScale;
        services = new GameObject("Inactive entrance services");
        services.SetActive(false);
        lobby = services.AddComponent(GameType("LobbyController"));
        preloader = services.AddComponent(GameType("PlayerClothesPreloader"));
        oldLobby = GameType("LobbyController").GetProperty("Instance").GetValue(null);
        oldPreloader = GameType("PlayerClothesPreloader").GetProperty("Instance").GetValue(null);
        GameType("LobbyController").GetProperty("Instance").SetValue(null, lobby);
        GameType("PlayerClothesPreloader").GetProperty("Instance").SetValue(null, preloader);
        oldClothesList = GameType("ClothesList").GetField("Instance").GetValue(null);
        GameType("ClothesList").GetField("Instance").SetValue(null, services.AddComponent(GameType("ClothesList")));
        models = (IDictionary)Get(preloader, "playerModels");
        sources = new GameObject("Live trainer sources");
        outfit = Activator.CreateInstance(GameType("PlayerClothesInfo"));
        session = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionView"));
        var players = (IList)Property(session, "Players");
        foreach (string team in new[] { "Blue", "Orange" })
        {
            for (short index = 0; index < 3; index++)
            {
                var player = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), team + index, false);
                var data = (IDictionary)Property(player, "Data");
                data["PlayerTeam"] = PlayerProperty(team);
                data["PlayerPos"] = PlayerProperty(Convert.ToBase64String(BitConverter.GetBytes(index)));
                players.Add(player);
            }
        }
        Set(lobby, "partyLobby", session);
        root = new GameObject("Reusable three versus three entrance", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.SetActive(false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        screen = root.AddComponent(GameType("GameBeginScreenUI"));
        var blueTeam = Child("Blue team", root.transform);
        var orangeTeam = Child("Orange team", root.transform);
        var blueHolder = Child("Blue players", blueTeam.transform);
        var orangeHolder = Child("Orange players", orangeTeam.transform);
        Set(screen, "blueTeam", (RectTransform)blueTeam.transform);
        Set(screen, "orangeTeam", (RectTransform)orangeTeam.transform);
        Set(screen, "bluePlayerHolder", blueHolder);
        Set(screen, "orangePlayerHolder", orangeHolder);
        Set(screen, "lineObject", Child("Line", root.transform));
        var bluePlayers = Enumerable.Range(0, 3).Select(index => (RectTransform)Child("Blue " + index, blueHolder.transform).transform).ToArray();
        var orangePlayers = Enumerable.Range(0, 3).Select(index => (RectTransform)Child("Orange " + index, orangeHolder.transform).transform).ToArray();
        Set(screen, "blueTeamPlayers", bluePlayers);
        Set(screen, "orangeTeamPlayers", orangePlayers);
#if UNITY_EDITOR
        foreach (string state in new[] { "off", "on" })
            foreach (string gender in new[] { "male", "female" })
            {
                var sprites = Enumerable.Range(1, 5).Select(index => AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/Sprites/Lobby/PartyScreen/t_matchroom_{state}_{gender}{index}.png")).ToArray();
                string field = (state == "off" ? "purple" : "orange") + (gender == "male" ? "Male" : "Female") + "Placeholders";
                Set(screen, field, sprites);
            }
#endif
        previews = bluePlayers.Concat(orangePlayers).Select(player =>
        {
            player.sizeDelta = new Vector2(100f, 150f);
            var host = Child("Real preview", player);
            ((RectTransform)host.transform).sizeDelta = new Vector2(100f, 150f);
            var preview = host.AddComponent(GameType("UI.ThreeDimensional.UIObject3D"));
            Set(preview, "_RenderScale", 0.25f);
            return preview;
        }).ToArray();
    }

    private object PlayerProperty(string value) => Activator.CreateInstance(GameType("Unity.Services.Multiplayer.PlayerProperty"), value,
        Enum.Parse(GameType("Unity.Services.Multiplayer.VisibilityPropertyOptions"), "Public"));

    private Component AddTrainer(string id)
    {
        var host = new GameObject(id);
        host.SetActive(false);
        host.transform.SetParent(sources.transform, false);
        host.transform.position = Vector3.down * 100f;
        var trainer = host.AddComponent(GameType("TrainerModel"));
        Set(trainer, "clothingHoldersMale", new Transform[0]);
        Set(trainer, "clothingHoldersFemale", new Transform[0]);
        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.transform.SetParent(host.transform, false);
        host.SetActive(true);
        models[id] = trainer;
        Call(trainer, "InitializeClothes", outfit);
        return trainer;
    }

    private void AddTrainers(bool includeFirst = true)
    {
        foreach (string team in new[] { "Blue", "Orange" })
            for (int index = 0; index < 3; index++)
                if (includeFirst || team != "Blue" || index != 0) AddTrainer(team + index);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = oldTimeScale;
        if (root != null) Object.Destroy(root);
        if (sources != null) Object.Destroy(sources);
        yield return null;
        Object.Destroy(services);
        GameType("LobbyController").GetProperty("Instance").SetValue(null, oldLobby);
        GameType("PlayerClothesPreloader").GetProperty("Instance").SetValue(null, oldPreloader);
        GameType("ClothesList").GetField("Instance").SetValue(null, oldClothesList);
        yield return null;
    }

    private void Show()
    {
        Call(screen, "InitializeUI", 3, 3);
        Call(screen, "FadeIn");
    }

    private IEnumerator Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "The transition/real trainer preview did not finish.");
    }

    private bool Closed => (bool)Property(screen, "IsClosed");
    private bool Placeholder(int index) => previews[index].transform.parent.Find("Trainer placeholder").gameObject.activeSelf;
    private bool Ready(int index)
    {
        var preview = previews[index];
        if (Placeholder(index) || !(bool)Property(preview, "IsReady") || !(bool)Property(preview, "HasRendered")) return false;
        var target = (Transform)Call(preview, "GetTargetInstance");
        var trainer = target.GetComponent(GameType("TrainerModel"));
        return (bool)Property(trainer, "IsInitialized") && !(bool)Property(trainer, "IsLoading") &&
            ((Image)Property(preview, "imageComponent")).color.a == 1f;
    }

    [UnityTest]
    public IEnumerator MissingFirstSlotDoesNotBlockCoverOrLaterRealPreviews()
    {
        AddTrainers(false);
        Show();
        Assert.That(Closed, Is.False);
        Assert.That(Enumerable.Range(0, 6).All(Placeholder), Is.True);
        yield return Until(() => Closed && Enumerable.Range(1, 5).All(Ready));
        Assert.That(Placeholder(0), Is.True);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(Closed, Is.True, "Cover must not auto-hide after ingress.");
        AddTrainer("Blue0");
        yield return Until(() => Ready(0));
        Assert.That(Closed, Is.True);
    }

    [UnityTest]
    public IEnumerator SlowActualTargetKeepsItsPlaceholderWhileLaterSlotsFinishFirstRender()
    {
        AddTrainers();
        Component delayedTarget = null;
        var updates = (UnityEngine.Events.UnityEvent)previews[0].GetType().GetField("OnUpdateTarget").GetValue(previews[0]);
        updates.AddListener(() =>
        {
            if (delayedTarget != null || !(bool)Property(previews[0], "IsReady")) return;
            var target = (Transform)Call(previews[0], "GetTargetInstance");
            delayedTarget = target.GetComponent(GameType("TrainerModel"));
            Set(delayedTarget, "initialized", false);
            Set(delayedTarget, "initializing", true);
        });
        Show();
        yield return Until(() => Closed && Enumerable.Range(1, 5).All(Ready));
        Assert.That(delayedTarget, Is.Not.Null);
        Assert.That(Property(previews[0], "IsReady"), Is.True);
        Assert.That(Property(previews[0], "HasRendered"), Is.True);
        Assert.That(Property(delayedTarget, "IsInitialized"), Is.False);
        Assert.That(Placeholder(0), Is.True, "A rendered but uncommitted clone must stay covered.");
        Set(delayedTarget, "initializing", false);
        Call(delayedTarget, "InitializeClothes", outfit);
        yield return Until(() => Ready(0));
        Assert.That(Closed, Is.True);
    }

    [UnityTest]
    public IEnumerator TwoConsecutiveThreeVersusThreeCyclesRehydrateEveryActualClone()
    {
        AddTrainers();
        var originalSources = models.Values.Cast<Component>().ToArray();
        for (int match = 0; match < 2; match++)
        {
            Show();
            yield return Until(() => Closed && Enumerable.Range(0, 6).All(Ready));
            Assert.That(models.Values.Cast<Component>(), Is.EquivalentTo(originalSources));
            Call(screen, "FadeOut");
            yield return Until(() => !root.activeSelf);
            Assert.That(Closed, Is.False);
            Assert.That(Property(screen, "IsOpen"), Is.True);
            Assert.That(Get(screen, "fadeInRoutine"), Is.Null);
            Assert.That(Get(screen, "fadeOutRoutine"), Is.Null);
        }
    }

    [UnityTest]
    public IEnumerator EarlyOpeningWaitsForIngressEvenWithNoTrainers()
    {
        Show();
        Call(screen, "FadeOut");
        yield return null;
        Assert.That(root.activeSelf, Is.True);
        Assert.That(Get(screen, "fadeInRoutine"), Is.Not.Null);
        yield return Until(() => Get(screen, "fadeInRoutine") == null);
        Assert.That(root.activeSelf, Is.True);
        yield return Until(() => !root.activeSelf);
    }

    [UnityTest]
    public IEnumerator NewPresentationCancelsPreviousPendingHide()
    {
        AddTrainers();
        Show();
        Call(screen, "FadeOut");
        yield return null;
        Show();
        yield return Until(() => Closed && Enumerable.Range(0, 6).All(Ready));
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(root.activeSelf, Is.True);
        Assert.That(Get(screen, "fadeOutRoutine"), Is.Null);
    }

    [UnityTest]
    public IEnumerator ImmediateHideDuringRehydrationCancelsThenAllowsNextCycle()
    {
        AddTrainers();
        Show();
        yield return Until(() => (bool)Property(previews[0], "IsReady"));
        Call(screen, "HideImmediately");
        Assert.That(root.activeSelf, Is.False);
        Assert.That(Closed, Is.False);
        Assert.That(Get(screen, "fadeInRoutine"), Is.Null);
        Assert.That(Get(screen, "fadeOutRoutine"), Is.Null);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(root.activeSelf, Is.False);
        Show();
        yield return Until(() => Closed && Enumerable.Range(0, 6).All(Ready));
    }

    [UnityTest]
    public IEnumerator ExternalDisableCancelsActualPreviewPreparationAndNextEntranceStillCloses()
    {
        AddTrainers();
        Show();
        yield return Until(() => (bool)Property(previews[0], "IsReady"));
        root.SetActive(false);
        Assert.That(Closed, Is.False);
        Assert.That(Get(screen, "fadeInRoutine"), Is.Null);
        Assert.That(Get(screen, "fadeOutRoutine"), Is.Null);
        Show();
        yield return Until(() => Closed && Enumerable.Range(0, 6).All(Ready));
    }

    [UnityTest]
    public IEnumerator PreviewRebuildStaysCoveredUntilNewCloneHasRendered()
    {
        AddTrainers();
        Show();
        yield return Until(() => Closed && Enumerable.Range(0, 6).All(Ready));
        var previousTarget = (Transform)Call(previews[1], "GetTargetInstance");
        Call(previews[1], "HardUpdateDisplay");
        Assert.That(Property(previews[1], "HasRendered"), Is.False);
        Call(screen, "LateUpdate");
        Assert.That(Placeholder(1), Is.True);
        yield return Until(() => Ready(1) && (Transform)Call(previews[1], "GetTargetInstance") != previousTarget);
        Assert.That(Closed, Is.True);
    }

    [UnityTest]
    public IEnumerator RosterReplacementDoesNotReusePreviousPlayersPreview()
    {
        AddTrainers();
        Show();
        yield return Until(() => Closed && Enumerable.Range(0, 6).All(Ready));
        var previous = (Transform)Call(previews[1], "GetTargetInstance");
        var unchangedTargets = previews.Select(preview => (Transform)Call(preview, "GetTargetInstance")).ToArray();
        var players = (IList)Property(session, "Players");
        var replacement = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), "Replacement", false);
        var data = (IDictionary)Property(replacement, "Data");
        data["PlayerTeam"] = PlayerProperty("Blue");
        data["PlayerPos"] = PlayerProperty(Convert.ToBase64String(BitConverter.GetBytes((short)1)));
        players[1] = replacement;
        yield return Until(() => Placeholder(1));
        AddTrainer("Replacement");
        yield return Until(() => Ready(1));
        Assert.That((Transform)Call(previews[1], "GetTargetInstance"), Is.Not.SameAs(previous));
        foreach (int index in new[] { 0, 2, 3, 4, 5 })
            Assert.That((Transform)Call(previews[index], "GetTargetInstance"), Is.SameAs(unchangedTargets[index]));
        Assert.That(Closed, Is.True);
    }

    [UnityTest]
    public IEnumerator RosterGenderSelectsPlaceholderBeforeModelsLoadAndAcrossRematches()
    {
        var players = (IList)Property(session, "Players");
        for (int match = 0; match < 2; match++)
        {
            for (int index = 0; index < players.Count; index++)
            {
                object clothes = Activator.CreateInstance(GameType("PlayerClothesInfo"));
                clothes.GetType().GetProperty("IsMale").SetValue(clothes, (index + match) % 2 == 0);
                var data = (IDictionary)Property(players[index], "Data");
                data["ClothingInfo"] = PlayerProperty((string)Call(clothes, "Serialize"));
            }
            Show();
            for (int index = 0; index < previews.Length; index++)
            {
                string state = index < 3 ? "off" : "on";
                string gender = (index + match) % 2 == 0 ? "male" : "female";
                var image = previews[index].transform.parent.Find("Trainer placeholder").GetComponent<Image>();
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.sprite.name, Is.EqualTo($"t_matchroom_{state}_{gender}{index % 3 + 1}"));
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(Placeholder(index), Is.True);
            }
            yield return Until(() => Closed);
            Assert.That(Enumerable.Range(0, 6).All(Placeholder), Is.True);
            Call(screen, "FadeOut");
            yield return Until(() => !root.activeSelf);
        }
    }

    [UnityTest]
    public IEnumerator TenPlayerEntranceKeepsFirstPlayerSlideAndRendersEveryPreview()
    {
        var allPreviews = previews.ToList();
        var roster = (IList)Property(session, "Players");
        foreach (string team in new[] { "Blue", "Orange" })
        {
            string field = team == "Blue" ? "blueTeamPlayers" : "orangeTeamPlayers";
            var players = ((RectTransform[])Get(screen, field)).ToList();
            for (short index = 3; index < 5; index++)
            {
                var player = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), team + index, false);
                var data = (IDictionary)Property(player, "Data");
                data["PlayerTeam"] = PlayerProperty(team);
                data["PlayerPos"] = PlayerProperty(Convert.ToBase64String(BitConverter.GetBytes(index)));
                roster.Add(player);
                var slot = (RectTransform)Child(team + index, players[0].parent).transform;
                slot.sizeDelta = new Vector2(100f, 150f);
                players.Add(slot);
                var host = Child("Real preview", slot);
                ((RectTransform)host.transform).sizeDelta = slot.sizeDelta;
                var preview = host.AddComponent(GameType("UI.ThreeDimensional.UIObject3D"));
                Set(preview, "_RenderScale", 0.25f);
                allPreviews.Add(preview);
            }
            Set(screen, field, players.ToArray());
        }
        previews = allPreviews.ToArray();
        foreach (string team in new[] { "Blue", "Orange" })
            for (int index = 0; index < 5; index++) AddTrainer(team + index);
        var first = ((RectTransform[])Get(screen, "blueTeamPlayers"))[0];
        Vector2 finalPosition = first.anchoredPosition;
        Call(screen, "InitializeUI", 5, 5);
        Call(screen, "FadeIn");
        var holder = ((GameObject)Get(screen, "bluePlayerHolder")).GetComponent<CanvasGroup>();
        yield return Until(() => holder.alpha == 1f);
        Assert.That(first.anchoredPosition.x, Is.LessThan(finalPosition.x));
        yield return null;
        Assert.That(first.anchoredPosition.x, Is.LessThan(finalPosition.x), "The first player must not snap to its final position.");
        yield return Until(() => Closed && Enumerable.Range(0, 10).All(Ready));
        Assert.That(first.anchoredPosition, Is.EqualTo(finalPosition));
    }

    [UnityTest]
    public IEnumerator CloseAndOpenUseUnscaledTimeAndFirstSlideStartsOffscreen()
    {
        Time.timeScale = 0f;
        Show();
        var blue = (RectTransform)Get(screen, "blueTeam");
        Assert.That(blue.anchoredPosition.x, Is.LessThan(0f));
        yield return null;
        Assert.That(blue.anchoredPosition.x, Is.LessThan(0f), "Startup frame delta must not skip the first slide.");
        yield return Until(() => Closed);
        Call(screen, "FadeOut");
        yield return Until(() => !root.activeSelf);
    }
}
