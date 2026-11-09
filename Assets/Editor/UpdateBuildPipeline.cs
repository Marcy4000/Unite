using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unite.Updates;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>Produces files for manual upload. Never uploads or publishes anything.</summary>
public static class UpdateBuildPipeline
{
    public const string SettingsPath = "Assets/Resources/UpdateDeliverySettings.asset";
    public const string TuningPath = "Assets/Resources/BalanceTuning.asset";
    public const string RequiredLabel = "download-required";
    public static string OutputRoot => Path.GetFullPath("Build/Updates").Replace('\\', '/');
    private static readonly string[] RemoteGroups = { "Maps", "ClothingItems", "ClothingShared", "ClothingIcons", "TrainerShaders", "TrainerCardArt" };
    public static UpdateDeliverySettings Settings => GetOrCreate<UpdateDeliverySettings>(SettingsPath);
    public static BalanceTuning Tuning => GetOrCreate<BalanceTuning>(TuningPath);
    public static string Target => EditorUserBuildSettings.activeBuildTarget.ToString();
    private static string PlatformRoot => OutputRoot + "/Upload/" + Settings.compatibilityLine + "/" + Target;
    private static string ArchiveRoot => OutputRoot + "/Artifacts/" + Settings.compatibilityLine + "/" + Target + "/" + Settings.addressablesVersion;
    private static string Descriptor => ArchiveRoot + "/release.json";
    private static string CatalogStaging => OutputRoot + "/Staging/" + Settings.compatibilityLine + "/" + Target;

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        return asset;
    }

    public static void ValidateSettings(bool requireRemote)
    {
        UpdateManifest.RequireVersion(Settings.compatibilityLine);
        UpdateManifest.RequireVersion(Settings.addressablesVersion);
        UpdateManifest.RequireVersion(Settings.patchVersion);
        UpdateManifest.RequireVersion(PlayerSettings.bundleVersion);
        if (Settings.retryCount < 0 || Settings.retryCount > 5 || Settings.requestTimeoutSeconds < 1)
            throw new BuildFailedException("Use 0–5 retries and a positive request timeout.");
        if (requireRemote || !string.IsNullOrWhiteSpace(Settings.baseUrl)) UpdateManifest.RequireHttps(Settings.baseUrl);
    }

    public static void ConfigureAddressables()
    {
        ValidateSettings(true);
        var aa = AddressableAssetSettingsDefaultObject.Settings;
        if (aa == null) throw new BuildFailedException("Addressables settings are missing.");
        aa.CCDEnabled = false;
        aa.BuildRemoteCatalog = true;
        aa.DisableCatalogUpdateOnStartup = true;
        aa.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
        aa.OverridePlayerVersion = Settings.addressablesVersion;
        aa.CatalogRequestsTimeout = Settings.requestTimeoutSeconds;
        aa.ContentStateBuildPath = OutputRoot + "/WorkingState/" + Settings.compatibilityLine + "/" + Target;
        aa.profileSettings.SetValue(aa.activeProfileId, AddressableAssetSettings.kRemoteBuildPath, PlatformRoot + "/bundles");
        aa.profileSettings.SetValue(aa.activeProfileId, AddressableAssetSettings.kRemoteLoadPath, Settings.Root + "/" + Target + "/bundles");
        SetProfileVariable(aa, "Update.CatalogBuildPath", CatalogStaging);
        // Unity content-update builds require this path to stay fixed across a baseline.
        // Runtime uses the manifest's immutable catalog URL instead. Never publish this working path.
        SetProfileVariable(aa, "Update.CatalogLoadPath", Settings.Root + "/" + Target + "/catalog-working");
        aa.RemoteCatalogBuildPath.SetVariableByName(aa, "Update.CatalogBuildPath");
        aa.RemoteCatalogLoadPath.SetVariableByName(aa, "Update.CatalogLoadPath");
        UpdateBuildProgress.Report("Configuring remote groups and labels", 0.2f);
        aa.AddLabel(RequiredLabel, false);
        foreach (var group in aa.groups)
        {
            if (group == null || group.GetSchema<BundledAssetGroupSchema>() == null) continue;
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = !RemoteGroups.Contains(group.Name);
            EditorUtility.SetDirty(update);
        }
        foreach (string name in RemoteGroups)
        {
            var group = aa.FindGroup(name);
            if (group == null) throw new BuildFailedException("Missing remote content group: " + name);
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) throw new BuildFailedException("Missing bundled schema: " + name);
            schema.BuildPath.SetVariableByName(aa, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(aa, AddressableAssetSettings.kRemoteLoadPath);
            schema.UseAssetBundleCache = true;
            schema.Timeout = Settings.requestTimeoutSeconds;
            schema.RetryCount = Settings.retryCount;
            // Explicit entries, including folders, propagate labels to generated child entries.
            ApplyRequiredLabel(aa, group.entries, false);
            EditorUtility.SetDirty(group);
            UpdateBuildProgress.Detail(group.Name + " (" + group.entries.Count + " entries)", 0.3f);
            EditorUtility.SetDirty(schema);
        }
        // One notification for the entire configuration pass, rather than thousands of UI/cache invalidations.
        aa.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        EditorUtility.SetDirty(aa);
        UpdateBuildProgress.Report("Saving Addressables configuration", 0.35f);
        AssetDatabase.SaveAssets();
    }

    public static void ApplyRequiredLabel(AddressableAssetSettings settings, IEnumerable<AddressableAssetEntry> entries, bool notify = true)
    {
        settings.AddLabel(RequiredLabel, false);
        int index = 0;
        foreach (var entry in entries)
        {
            entry.SetLabel(RequiredLabel, true, false, false);
            if (++index % 64 == 0) UpdateBuildProgress.Detail("Labelling entry " + index, 0.25f);
        }
        if (notify) settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
    }

    private static void SetProfileVariable(AddressableAssetSettings aa, string name, string value)
    {
        aa.profileSettings.CreateValue(name, value);
        aa.profileSettings.SetValue(aa.activeProfileId, name, value);
    }

    [MenuItem("Build/Updates/Export balance JSON")]
    public static void ExportBalance() => UpdateBuildProgress.Run("Export balance JSON", ExportBalanceCore);

    private static void ExportBalanceCore()
    {
        UpdateBuildProgress.Report("Exporting balance JSON", 0.05f);
        ValidateSettings(false);
        var patch = CreateBalancePatch();
        string json = JsonUtility.ToJson(patch, true) + "\n";
        string hash = UpdateManifest.Sha256(json);
        WriteImmutable(OutputRoot + "/Artifacts/Patches/" + Settings.compatibilityLine + "/" + Settings.patchVersion + ".sha256", hash);
        string relative = Settings.compatibilityLine + "/patches/" + Settings.patchVersion + "-" + hash + ".json";
        WriteImmutable(OutputRoot + "/Upload/" + relative, json);
        Write("Assets/Resources/BalanceDefaults.json", json);
        AssetDatabase.ImportAsset("Assets/Resources/BalanceDefaults.json");
        Debug.Log("Balance JSON exported: " + OutputRoot + "/Upload/" + relative);
    }

    public static BalancePatch CreateBalancePatch()
    {
        var patch = new BalancePatch { compatibilityLine = Settings.compatibilityLine, patchVersion = Settings.patchVersion };
        var pokemon = new List<PokemonBalance>();
        var moves = new List<MoveBalance>();
        foreach (string guid in AssetDatabase.FindAssets("t:PokemonBase").OrderBy(x => x, StringComparer.Ordinal))
        {
            var asset = AssetDatabase.LoadAssetAtPath<PokemonBase>(AssetDatabase.GUIDToAssetPath(guid));
            SetIdentity(asset, guid);
            // Read authored fields, never the active runtime overrides in the inspector/play session.
            var entry = JsonUtility.FromJson<PokemonBalance>(JsonUtility.ToJson(asset));
            entry.id = guid; entry.name = asset.name;
            pokemon.Add(entry);
            UpdateBuildProgress.Detail("Pokémon: " + asset.name, 0.08f);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:MoveAsset").OrderBy(x => x, StringComparer.Ordinal))
        {
            var asset = AssetDatabase.LoadAssetAtPath<MoveAsset>(AssetDatabase.GUIDToAssetPath(guid));
            SetIdentity(asset, guid);
            UpdateBuildProgress.Detail("Move: " + asset.name, 0.12f);
            var serialized = new SerializedObject(asset);
            var definition = MoveDatabase.GetMove(asset.move);
            // Preserve existing compiled cooldowns until explicitly overridden in exported JSON.
            // Assets whose constructors are not implemented still export their authored cooldown.
            float cooldown = definition != null ? definition.Cooldown : serialized.FindProperty("cooldown").floatValue;
            float upgraded = definition is PrefabMove prefab ? prefab.EffectiveCooldown(true) : cooldown;
            if (definition is PrefabMove normal) cooldown = normal.EffectiveCooldown(false);
            moves.Add(new MoveBalance { id = guid, name = asset.name, cooldown = cooldown, upgradedCooldown = upgraded,
                uniteEnergyCost = serialized.FindProperty("uniteEnergyCost").intValue });
        }
        foreach (var entry in Tuning.moveOverrides)
        {
            int index = moves.FindIndex(m => m.id == entry.id);
            if (index < 0) throw new BuildFailedException("Unknown move override ID: " + entry.id);
            entry.overrideCooldown = true;
            moves[index] = entry;
        }
        patch.pokemon = pokemon.ToArray(); patch.moves = moves.ToArray();
        var numbers = new List<BalanceNumber>(Tuning.numbers);
        int[] experience = { 100, 100, 400, 500, 650, 650, 750, 870, 1080, 1290, 1550, 1860, 2230, 2670, 3000 };
        for (int i = 0; i < experience.Length; i++)
            if (!numbers.Any(n => n.id == "experience.level." + i)) numbers.Add(new BalanceNumber { id = "experience.level." + i, value = experience[i] });
        patch.numbers = numbers.OrderBy(n => n.id, StringComparer.Ordinal).ToArray();
        patch.Validate(Settings.compatibilityLine, Settings.patchVersion);
        AssetDatabase.SaveAssets();
        return patch;
    }

    private static void SetIdentity(UnityEngine.Object asset, string guid)
    {
        var serialized = new SerializedObject(asset);
        var property = serialized.FindProperty("balanceId");
        if (property.stringValue == guid) return;
        property.stringValue = guid;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    [MenuItem("Build/Updates/Build Addressables baseline (active platform)")]
    public static void BuildBaseline() => BuildContent(null);

    [MenuItem("Build/Updates/Build Addressables update (choose previous state)")]
    public static void BuildContentUpdate()
    {
        string state = EditorUtility.OpenFilePanel("Previous release addressables_content_state.bin", OutputRoot + "/Artifacts", "bin");
        if (!string.IsNullOrEmpty(state)) BuildContent(state);
    }

    public static void BuildContent(string previousState) => UpdateBuildProgress.Run(
        string.IsNullOrEmpty(previousState) ? "Build Addressables baseline" : "Build Addressables content update",
        () => BuildContentCore(previousState));

    [MenuItem("Build/Updates/Prepare Addressables configuration only")]
    public static void PrepareContent() => UpdateBuildProgress.Run("Prepare Addressables (no bundle build)", () =>
    {
        ValidateSettings(true);
        ExportBalance();
        ConfigureAddressables();
    });

    private static void BuildContentCore(string previousState)
    {
        UpdateBuildProgress.Report("Validating content release", 0.01f);
        if (Application.isPlaying) throw new BuildFailedException("Exit Play mode before exporting releases.");
        ValidateSettings(true);
        if (File.Exists(Descriptor)) throw new BuildFailedException("This Addressables version already exists. Increment it before building content.");
        string catalogDirectory = PlatformRoot + "/catalogs/" + Settings.addressablesVersion;
        if (Directory.Exists(catalogDirectory) && Directory.EnumerateFiles(catalogDirectory).Any())
            throw new BuildFailedException("Catalog output already exists. Choose a new version (or remove an unpublished failed build manually).");
        ExportBalance();
        ConfigureAddressables();
        if (Directory.Exists(CatalogStaging)) Directory.Delete(CatalogStaging, true);
        UpdateBuildProgress.Report("Building bundles — Unity's build progress takes over; large builds can take many minutes", 0.4f);
        AddressablesPlayerBuildResult result;
        if (string.IsNullOrEmpty(previousState)) AddressableAssetSettings.BuildPlayerContent(out result);
        else
        {
            if (!File.Exists(previousState)) throw new BuildFailedException("Previous content state does not exist.");
            var state = ContentUpdateScript.LoadContentState(previousState);
            if (state == null || state.editorVersion != Application.unityVersion)
                throw new BuildFailedException("Content update state must come from the same Unity version. Build a new baseline/player for engine upgrades.");
            var changedLocal = ContentUpdateScript.GatherModifiedEntries(AddressableAssetSettingsDefaultObject.Settings, previousState);
            if (changedLocal == null || changedLocal.Count != 0)
                throw new BuildFailedException("Local Addressables changed. Build a new baseline and player, or revert those changes before a content-only update.");
            result = ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, previousState);
        }
        if (result == null || !string.IsNullOrEmpty(result.Error)) throw new BuildFailedException(result?.Error ?? "Addressables build failed.");
        UpdateBuildProgress.Report("Archiving catalog and local player content", 0.9f);
        string catalog = Directory.GetFiles(CatalogStaging, "catalog_*.bin").Single();
        CopyDirectory(CatalogStaging, catalogDirectory);
        string statePath = ContentUpdateScript.GetContentStateDataPath(false, AddressableAssetSettingsDefaultObject.Settings);
        if (!File.Exists(statePath)) throw new BuildFailedException("Content state was not produced: " + statePath);
        Directory.CreateDirectory(ArchiveRoot);
        File.Copy(statePath, ArchiveRoot + "/addressables_content_state.bin", false);
        CopyDirectory(Addressables.BuildPath, ArchiveRoot + "/PlayerContent");
        var manifest = MakeManifest(Settings.Root + "/" + Target + "/catalogs/" + Settings.addressablesVersion + "/" + Path.GetFileName(catalog));
        WriteImmutable(Descriptor, JsonUtility.ToJson(manifest, true));
        UpdateBuildProgress.Report("Writing release manifest", 0.98f);
        ExportReleaseManifest();
        Debug.Log("Content built. Upload files under " + OutputRoot + "/Upload; publish latest.json LAST. Keep Artifacts for future builds.");
    }

    private static UpdateManifest MakeManifest(string catalogUrl)
    {
        var json = File.ReadAllText("Assets/Resources/BalanceDefaults.json");
        string hash = UpdateManifest.Sha256(json);
        return new UpdateManifest
        {
            playerVersion = PlayerSettings.bundleVersion, compatibilityLine = Settings.compatibilityLine, platform = Target,
            addressablesVersion = Settings.addressablesVersion, patchVersion = Settings.patchVersion, catalogUrl = catalogUrl,
            patchUrl = Settings.Root + "/patches/" + Settings.patchVersion + "-" + hash + ".json", patchSha256 = hash
        };
    }

    [MenuItem("Build/Updates/Export release manifest (reuse content)")]
    public static void ExportReleaseManifest()
    {
        ValidateSettings(true);
        if (!File.Exists(Descriptor)) throw new BuildFailedException("Build this platform's Addressables version first.");
        ExportBalance();
        var original = JsonUtility.FromJson<UpdateManifest>(File.ReadAllText(Descriptor));
        var manifest = MakeManifest(original.catalogUrl);
        manifest.Validate(PlayerSettings.bundleVersion, Settings.compatibilityLine, Target);
        string directory = OutputRoot + "/Upload/" + Settings.compatibilityLine + "/players/" + PlayerSettings.bundleVersion + "/" + Target;
        string json = JsonUtility.ToJson(manifest, true) + "\n";
        // Reusing a version with different rules would let incompatible peers share the same session key.
        WriteImmutable(directory + "/releases/" + Settings.addressablesVersion + "-" + Settings.patchVersion + ".json", json);
        Write(directory + "/latest.json", json);
        Debug.Log("Release manifest exported: " + directory + "/latest.json");
    }

    public static void BuildPlayer(BuildTarget target, bool server = false)
    {
        if (EditorUserBuildSettings.activeBuildTarget != target)
            throw new BuildFailedException("Switch the active build target to " + target + " first, then run the build again.");
        ValidateSettings(true);
        if (!File.Exists(Descriptor)) throw new BuildFailedException("Build/archive Addressables for this version and platform first.");
        var original = JsonUtility.FromJson<UpdateManifest>(File.ReadAllText(Descriptor));
        string expectedPrefix = Settings.Root + "/" + Target + "/catalogs/" + Settings.addressablesVersion + "/";
        if (!original.catalogUrl.StartsWith(expectedPrefix, StringComparison.Ordinal))
            throw new BuildFailedException("Hosting URL changed. Build a new Addressables version before building the player.");
        ExportReleaseManifest();
        // Restore the matching local catalog + local bundles, never rebuild remote content for a code-only release.
        if (Directory.Exists(Addressables.BuildPath)) Directory.Delete(Addressables.BuildPath, true);
        CopyDirectory(ArchiveRoot + "/PlayerContent", Addressables.BuildPath);
        var aa = AddressableAssetSettingsDefaultObject.Settings;
        aa.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
        string extension = target == BuildTarget.Android ? ".apk" : target == BuildTarget.StandaloneWindows64 ? ".exe" : target == BuildTarget.WebGL ? "" : ".x86_64";
        string directory = Path.GetFullPath("Build/Players/" + PlayerSettings.bundleVersion + "/" + Target + (server ? "Server" : ""));
        Directory.CreateDirectory(directory);
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = target == BuildTarget.WebGL ? directory : Path.Combine(directory, "Unite" + extension),
            target = target,
            subtarget = server ? (int)StandaloneBuildSubtarget.Server : (int)StandaloneBuildSubtarget.Player
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Player build failed: " + report.summary.result);
        Debug.Log("Player built: " + directory);
    }

    public static void WriteImmutable(string path, string contents)
    {
        if (File.Exists(path) && File.ReadAllText(path) != contents)
            throw new BuildFailedException("Refusing to overwrite immutable release data: " + path + ". Increment the version.");
        Write(path, contents);
    }
    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }
    private static void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source)) throw new BuildFailedException("Missing build archive: " + source);
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        for (int index = 0; index < files.Length; index++)
        {
            string file = files[index];
            UpdateBuildProgress.Detail("Copying " + (index + 1) + "/" + files.Length + ": " + Path.GetFileName(file), 0.9f);
            var destination = Path.Combine(target, file.Substring(source.Length).TrimStart('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(file, destination, true);
        }
    }
}
