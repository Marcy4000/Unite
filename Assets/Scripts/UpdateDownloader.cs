using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unite.Updates;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Runs only from boot, before any gameplay assets or sessions are opened.</summary>
public sealed class UpdateDownloader
{
    public string Error { get; private set; }
    public bool Succeeded { get; private set; }
    private readonly Action<string> progress;
    public UpdateDownloader(Action<string> progress) { this.progress = progress; }

    // Drive nested routines ourselves so validation, filesystem and request failures all reach the UI.
    public IEnumerator Run(UpdateDeliverySettings settings)
    {
        Error = null; Succeeded = false; GameRelease.Begin();
        var stack = new Stack<IEnumerator>();
        stack.Push(Download(settings));
        try
        {
            while (stack.Count > 0)
            {
                object next = null;
                bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception exception) { Error = exception.Message; }
                if (Error != null) break;
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
            Succeeded = Error == null && GameRelease.Ready;
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        }
    }

    private IEnumerator Download(UpdateDeliverySettings settings)
    {
        bool remote = settings != null && !string.IsNullOrWhiteSpace(settings.baseUrl);
#if UNITY_EDITOR
        remote &= settings != null && settings.useRemoteInEditor;
#endif
        UpdateManifest manifest = null;
        BalancePatch patch = null;
        if (remote)
        {
            UpdateManifest.RequireHttps(settings.baseUrl);
            progress("Checking for updates...");
            string json = null;
            yield return GetText(settings.ManifestUrl(Platform), settings, text => json = text);
            manifest = JsonUtility.FromJson<UpdateManifest>(json);
            if (manifest == null) throw new InvalidOperationException("Empty update manifest.");
            manifest.Validate(Application.version, settings.compatibilityLine, Platform);
            string cache = Path.Combine(Application.persistentDataPath, "Updates", settings.compatibilityLine, manifest.patchSha256 + ".json");
            string balance = File.Exists(cache) ? File.ReadAllText(cache) : null;
            if (balance == null || UpdateManifest.Sha256(balance) != manifest.patchSha256)
            {
                progress("Downloading balance update...");
                yield return GetText(manifest.patchUrl, settings, text => balance = text);
                if (UpdateManifest.Sha256(balance) != manifest.patchSha256) throw new InvalidOperationException("Balance download checksum failed. Retry the update.");
            }
            patch = JsonUtility.FromJson<BalancePatch>(balance);
            if (patch == null) throw new InvalidOperationException("Empty balance update.");
            patch.Validate(settings.compatibilityLine, manifest.patchVersion);
            var defaults = Resources.Load<TextAsset>("BalanceDefaults");
            if (defaults == null) throw new InvalidOperationException("This player build is missing its balance baseline.");
            var baseline = JsonUtility.FromJson<BalancePatch>(defaults.text);
            baseline.Validate(settings.compatibilityLine, baseline.patchVersion);
            patch.ValidateAgainst(baseline);
            AtomicWrite(cache, balance);
        }
        else
        {
            var defaults = Resources.Load<TextAsset>("BalanceDefaults");
            if (defaults != null)
            {
                patch = JsonUtility.FromJson<BalancePatch>(defaults.text);
                patch.Validate(settings != null ? settings.compatibilityLine : patch.compatibilityLine, patch.patchVersion);
            }
        }

        progress("Preparing game content...");
        var initialization = Addressables.InitializeAsync(false);
        try
        {
            yield return initialization;
            if (initialization.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException("Content initialization failed: " + initialization.OperationException?.Message);
        }
        finally { if (initialization.IsValid()) Addressables.Release(initialization); }

        if (remote)
        {
            // The URL is immutable and versioned. Disable automatic startup catalog updates in build settings.
            var catalog = Addressables.LoadContentCatalogAsync(manifest.catalogUrl, false);
            try
            {
                yield return catalog;
                if (catalog.Status != AsyncOperationStatus.Succeeded)
                    throw new InvalidOperationException("Could not load the content catalog: " + catalog.OperationException?.Message);
                // A missing label must fail closed rather than silently download nothing.
                foreach (var label in manifest.requiredLabels)
                    if (!catalog.Result.Locate(label, null, out var locations) || locations.Count == 0)
                        throw new InvalidOperationException("Required content is absent from the catalog: " + label);
                // Additive catalogs otherwise leave duplicate keys resolved against the bundled baseline.
                // This catalog is a complete release snapshot, so make it the sole lookup source.
                Addressables.ClearResourceLocators();
                Addressables.AddResourceLocator(catalog.Result);
            }
            finally { if (catalog.IsValid()) Addressables.Release(catalog); }

            var size = Addressables.GetDownloadSizeAsync((IEnumerable)manifest.requiredLabels);
            long missing;
            try
            {
                yield return size;
                if (size.Status != AsyncOperationStatus.Succeeded) throw new InvalidOperationException("Could not calculate the download size.");
                missing = size.Result;
            }
            finally { if (size.IsValid()) Addressables.Release(size); }
            if (missing > 0)
            {
                bool downloaded = false;
                for (int attempt = 0; attempt <= settings.retryCount && !downloaded; attempt++)
                {
                    var download = Addressables.DownloadDependenciesAsync((IEnumerable)manifest.requiredLabels, Addressables.MergeMode.Union, false);
                    try
                    {
                        while (!download.IsDone)
                        {
                            var status = download.GetDownloadStatus();
                            progress($"Downloading game content: {status.Percent:P0}\n{status.DownloadedBytes / 1048576f:0.0} / {status.TotalBytes / 1048576f:0.0} MB\nUnchanged content stays cached.");
                            yield return null;
                        }
                        downloaded = download.Status == AsyncOperationStatus.Succeeded;
                        if (!downloaded && attempt == settings.retryCount)
                            throw new InvalidOperationException("Content download failed. Check your connection and free storage, then retry. " + download.OperationException?.Message);
                    }
                    finally { if (download.IsValid()) Addressables.Release(download); }
                    if (!downloaded) yield return new WaitForSecondsRealtime(1);
                }
            }
        }
        if (patch != null) BalanceRuntime.Install(patch, patch.compatibilityLine, patch.patchVersion);
        GameRelease.Commit(manifest?.addressablesVersion ?? settings?.addressablesVersion ?? "local", manifest?.patchVersion ?? patch?.patchVersion ?? "local");
        progress("Game is up to date.");
    }

    private static IEnumerator GetText(string url, UpdateDeliverySettings settings, Action<string> accept)
    {
        for (int attempt = 0; attempt <= settings.retryCount; attempt++)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = Math.Max(1, settings.requestTimeoutSeconds);
                // The manifest must be revalidated at every boot; immutable patch URLs can be cached.
                request.SetRequestHeader("Cache-Control", "no-cache");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (request.downloadHandler.data.Length > 8 * 1024 * 1024) throw new InvalidOperationException("Update metadata is unexpectedly large.");
                    accept(request.downloadHandler.text); yield break;
                }
                if (attempt == settings.retryCount) throw new InvalidOperationException("Could not check/download updates: " + request.error + ". Check your connection and retry.");
            }
            yield return new WaitForSecondsRealtime(1);
        }
    }

    private static void AtomicWrite(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        // Never replace an already validated immutable cache entry.
        if (File.Exists(path) && File.ReadAllText(path) == contents) return;
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, contents, new System.Text.UTF8Encoding(false));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporary, path);
    }

    public static string Platform
    {
        get
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_WEBGL
            return "WebGL";
#elif UNITY_STANDALONE_LINUX
            return "StandaloneLinux64";
#elif UNITY_STANDALONE_OSX
            return "StandaloneOSX";
#else
            return "StandaloneWindows64";
#endif
        }
    }
}
