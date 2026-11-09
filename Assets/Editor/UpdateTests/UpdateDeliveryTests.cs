using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Multiplayer;
using Unite.Updates;
using UnityEditor;
using UnityEngine;

public sealed class UpdateDeliveryTests
{
    [SetUp] public void Begin() => GameRelease.Begin();
    [TearDown] public void Reset() => GameRelease.Begin();

    private static BalancePatch Patch() => new BalancePatch
    {
        compatibilityLine = "test", patchVersion = "2",
        moves = new[] { new MoveBalance { id = "move", cooldown = 7, upgradedCooldown = 5, overrideCooldown = true, uniteEnergyCost = 90000 } },
        numbers = new[] { new BalanceNumber { id = "held.focus.ratio", value = 0.2f } }
    };
    private static UpdateManifest Manifest() => new UpdateManifest
    {
        playerVersion = "0.8", compatibilityLine = "test", platform = "Android", addressablesVersion = "4", patchVersion = "2",
        catalogUrl = "https://content.example.com/Android/catalogs/4/catalog.bin", patchUrl = "https://content.example.com/patch.json",
        patchSha256 = UpdateManifest.Sha256("patch")
    };
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    [TestCase(false)]
    [TestCase(true)]
    public void BuildProgressReleasesItsBusyStateAndLogsFailuresOrCancellation(bool cancel)
    {
        var progress = GameType("UpdateBuildProgress");
        Action failure = () =>
        {
            if (cancel) throw new OperationCanceledException("test cancellation");
            throw new InvalidOperationException("test failure");
        };
        Assert.Throws<TargetInvocationException>(() => progress.GetMethod("Run").Invoke(null, new object[] { "Progress cleanup test", failure }));
        Assert.That(progress.GetProperty("IsRunning").GetValue(null), Is.False);
        string path = (string)progress.GetProperty("LatestLogPath").GetValue(null);
        StringAssert.Contains(cancel ? "Cancelled" : "Failed", System.IO.File.ReadAllText(path));
    }

    [Test] public void LabellingThousandsOfEntriesSendsOneModificationNotification()
    {
        var settings = UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.Create("", "UpdateLabelTest", false, false);
        var group = settings.CreateGroup("Remote", false, false, false, null);
        try
        {
            for (int index = 0; index < 2048; index++)
                settings.CreateOrMoveEntry(Guid.NewGuid().ToString("N"), group, false, false);
            int notifications = 0;
            settings.OnModification += (source, change, data) => notifications++;
            GameType("UpdateBuildPipeline").GetMethod("ApplyRequiredLabel").Invoke(null, new object[] { settings, group.entries, true });
            Assert.That(notifications, Is.EqualTo(1), "Per-entry notifications can stall the editor before the bundle build starts.");
            Assert.That(group.entries.All(entry => entry.labels.Contains("download-required")), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(group);
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test] public void SessionKeyChangesForEachReleaseLayer()
    {
        string key = SessionCompatibility.ForVersions("1", "2", "3");
        Assert.That(key, Is.Not.EqualTo(SessionCompatibility.ForVersions("2", "2", "3")));
        Assert.That(key, Is.Not.EqualTo(SessionCompatibility.ForVersions("1", "3", "3")));
        Assert.That(key, Is.Not.EqualTo(SessionCompatibility.ForVersions("1", "2", "4")));
    }
    [Test] public void ManifestRejectsWrongPlayerPlatformSchemaAndUnsafeUrls()
    {
        var manifest = Manifest();
        Assert.DoesNotThrow(() => manifest.Validate("0.8", "test", "Android"));
        Assert.Throws<InvalidOperationException>(() => manifest.Validate("0.7", "test", "Android"));
        Assert.Throws<InvalidOperationException>(() => manifest.Validate("0.8", "test", "StandaloneWindows64"));
        manifest.catalogUrl = "http://content.example.com/catalog.bin";
        Assert.Throws<InvalidOperationException>(() => manifest.Validate("0.8", "test", "Android"));
        manifest = Manifest(); manifest.patchSha256 = "not-a-hash";
        Assert.Throws<InvalidOperationException>(() => manifest.Validate("0.8", "test", "Android"));
        manifest = Manifest(); manifest.requiredLabels = Array.Empty<string>();
        Assert.Throws<InvalidOperationException>(() => manifest.Validate("0.8", "test", "Android"));
    }
    [Test] public void InvalidPatchCannotPartiallyReplaceInstalledRules()
    {
        var patch = Patch(); BalanceRuntime.Install(patch, "test", "2");
        patch.numbers[0].value = 0.8f;
        Assert.That(BalanceRuntime.Number("held.focus.ratio", 0), Is.EqualTo(0.2f));
        patch.moves[0].cooldown = float.NaN;
        Assert.Throws<InvalidOperationException>(() => BalanceRuntime.Install(patch, "test", "2"));
        Assert.That(BalanceRuntime.Move("move").cooldown, Is.EqualTo(7));
        Assert.That(BalanceRuntime.Number("held.focus.ratio", 0), Is.EqualTo(0.2f));
    }
    [Test] public void BalanceIsPinnedAfterSuccessfulBoot()
    {
        BalanceRuntime.Install(Patch(), "test", "2"); GameRelease.Commit("4", "2");
        Assert.Throws<InvalidOperationException>(() => BalanceRuntime.Install(Patch(), "test", "2"));
    }
    [Test] public void DuplicateIdsAndMalformedTablesAreRejected()
    {
        var patch = Patch(); patch.moves = new[] { patch.moves[0], patch.moves[0] };
        Assert.Throws<InvalidOperationException>(() => patch.Validate("test", "2"));
        patch = Patch(); patch.pokemon = new[] { new PokemonBalance { id = "missing-stats" } };
        Assert.Throws<InvalidOperationException>(() => patch.Validate("test", "2"));
        Assert.Throws<InvalidOperationException>(() => Patch().Validate("other-schema", "2"));
    }
    [Test] public void ExportedDefaultsValidateAndPreserveWildAndPlayableTables()
    {
        var defaults = Resources.Load<TextAsset>("BalanceDefaults");
        Assert.That(defaults, Is.Not.Null);
        var patch = JsonUtility.FromJson<BalancePatch>(defaults.text);
        Assert.DoesNotThrow(() => patch.Validate(patch.compatibilityLine, patch.patchVersion));
        Assert.That(patch.pokemon.Any(p => p.maxHp.Length == 15), Is.True);
        Assert.That(patch.pokemon.Any(p => p.maxHp.Length == 22), Is.True);
        foreach (var entry in patch.pokemon)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(entry.id));
            var authored = JsonUtility.FromJson<PokemonBalance>(JsonUtility.ToJson(asset));
            CollectionAssert.AreEqual(authored.maxHp, entry.maxHp, entry.name);
            CollectionAssert.AreEqual(authored.atkSpeed, entry.atkSpeed, entry.name);
        }
    }
    [Test] public void CooldownPatchReachesBaseAndUpgradedMoveWithoutChangingAuthoredField()
    {
        BalanceRuntime.Install(Patch(), "test", "2");
        var type = GameType("CinderEmber"); var move = Activator.CreateInstance(type);
        type.GetProperty("BalanceId").SetValue(move, "move");
        type.GetField("Cooldown").SetValue(move, 6f);
        Assert.That(type.GetProperty("PatchedCooldown").GetValue(move), Is.EqualTo(7f));
        type.GetField("IsUpgraded").SetValue(move, true);
        Assert.That(type.GetProperty("PatchedCooldown").GetValue(move), Is.EqualTo(5f));
        Assert.That(type.GetField("Cooldown").GetValue(move), Is.EqualTo(6f));
    }
    [Test] public void PokemonGetterUsesPatchWithoutMutatingSerializedStats()
    {
        var patch = JsonUtility.FromJson<BalancePatch>(Resources.Load<TextAsset>("BalanceDefaults").text);
        var entry = patch.pokemon.First(p => p.name == "Espeon");
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(entry.id));
        var before = JsonUtility.ToJson(asset);
        entry.maxHp[0] += 100;
        BalanceRuntime.Install(patch, patch.compatibilityLine, patch.patchVersion);
        var hp = (int[])asset.GetType().GetProperty("MaxHp").GetValue(asset);
        Assert.That(hp[0], Is.EqualTo(entry.maxHp[0]));
        Assert.That(JsonUtility.ToJson(asset), Is.EqualTo(before));
    }
    [Test] public void PatchCannotTargetUnknownAssetsOrChangeTheirLevelCount()
    {
        var json = Resources.Load<TextAsset>("BalanceDefaults").text;
        var baseline = JsonUtility.FromJson<BalancePatch>(json);
        var patch = JsonUtility.FromJson<BalancePatch>(json);
        Assert.DoesNotThrow(() => patch.ValidateAgainst(baseline));
        patch.pokemon[0].maxHp = new int[baseline.pokemon[0].maxHp.Length == 15 ? 22 : 15];
        Assert.Throws<InvalidOperationException>(() => patch.ValidateAgainst(baseline));
        patch = JsonUtility.FromJson<BalancePatch>(json);
        patch.moves[0].id = "unknown";
        Assert.Throws<InvalidOperationException>(() => patch.ValidateAgainst(baseline));
    }

    [Test] public void ExportRefusesToOverwriteAnExistingImmutableRelease()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UniteUpdateTest-" + Guid.NewGuid());
        string path = System.IO.Path.Combine(directory, "release.json");
        var method = GameType("UpdateBuildPipeline").GetMethod("WriteImmutable");
        try
        {
            method.Invoke(null, new object[] { path, "first" });
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { path, "first" }));
            Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { path, "changed" }));
            Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo("first"));
        }
        finally { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true); }
    }

    [Test] public void ServerCooldownUsesTheSamePatchAsTheOwner()
    {
        var moveAssetType = GameType("MoveAsset");
        var baseType = GameType("PokemonBase");
        var asset = ScriptableObject.CreateInstance(moveAssetType);
        var stats = ScriptableObject.CreateInstance(baseType);
        var go = new GameObject("balance-test");
        try
        {
            var id = Enum.Parse(GameType("AvailableMoves"), "CinderEmber");
            moveAssetType.GetField("move").SetValue(asset, id);
            moveAssetType.GetField("balanceId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, "move");
            var row = Activator.CreateInstance(GameType("LearnableMove"));
            var moveArray = Array.CreateInstance(moveAssetType, 1); moveArray.SetValue(asset, 0);
            row.GetType().GetField("moves").SetValue(row, moveArray);
            var rows = Array.CreateInstance(row.GetType(), 1); rows.SetValue(row, 0);
            baseType.GetField("learnableMoves", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stats, rows);
            var pokemon = go.AddComponent(GameType("Pokemon"));
            pokemon.GetType().GetField("baseStats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pokemon, stats);
            var controller = go.AddComponent(GameType("MovesController"));
            controller.GetType().GetField("pokemon", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, pokemon);
            BalanceRuntime.Install(Patch(), "test", "2");
            var method = controller.GetType().GetMethod("PatchedMoveCooldown", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method.Invoke(controller, new[] { id, (object)6f }), Is.EqualTo(7f));
            row.GetType().GetField("isUpgraded").SetValue(row, true);
            Assert.That(method.Invoke(controller, new[] { id, (object)6f }), Is.EqualTo(5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(stats);
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    [Test] public void InvalidEndpointStopsDownloaderAndLeavesPlayBlocked()
    {
        var settings = ScriptableObject.CreateInstance<UpdateDeliverySettings>();
        try
        {
            settings.baseUrl = "http://insecure.example.com"; settings.useRemoteInEditor = true;
            var downloader = Activator.CreateInstance(GameType("UpdateDownloader"), new Action<string>(_ => { }));
            var run = (IEnumerator)downloader.GetType().GetMethod("Run").Invoke(downloader, new object[] { settings });
            while (run.MoveNext()) { }
            Assert.That(downloader.GetType().GetProperty("Succeeded").GetValue(downloader), Is.False);
            StringAssert.Contains("HTTPS", (string)downloader.GetType().GetProperty("Error").GetValue(downloader));
            Assert.That(GameRelease.Ready, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }
}
