using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Progress before/after SBP's own progress UI, with a durable log if Unity stalls.</summary>
public sealed class UpdateBuildProgress : IDisposable
{
    private static UpdateBuildProgress current;
    public static bool IsRunning => current != null;
    public static string LatestLogPath { get; private set; }
    private readonly string title;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly StreamWriter log;
    private string stage;
    private long lastPaint = -1000;

    private UpdateBuildProgress(string title)
    {
        this.title = title;
        string directory = Path.Combine(UpdateBuildPipeline.OutputRoot, "Logs");
        Directory.CreateDirectory(directory);
        LatestLogPath = Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
        log = new StreamWriter(LatestLogPath) { AutoFlush = true };
        current = this;
        Write("Started: " + title + " | " + UpdateBuildPipeline.Target);
    }

    public static void Run(string title, Action action)
    {
        if (current != null) { action(); return; }
        using (var progress = new UpdateBuildProgress(title))
        {
            try
            {
                Report("Starting", 0);
                action();
                progress.Write("Completed");
            }
            catch (OperationCanceledException) { progress.Write("Cancelled"); throw; }
            catch (Exception exception) { progress.Write("Failed: " + exception); throw; }
        }
    }

    public static void Report(string stage, float fraction)
    {
        if (current == null) return;
        if (current.stage != stage) { current.stage = stage; current.Write(stage); }
        current.Paint(stage, fraction, true);
    }
    public static void Detail(string detail, float fraction)
    {
        current?.Paint(current.stage + " — " + detail, fraction, false);
    }
    private void Paint(string detail, float fraction, bool force)
    {
        if (Application.isBatchMode || (!force && elapsed.ElapsedMilliseconds - lastPaint < 100)) return;
        lastPaint = elapsed.ElapsedMilliseconds;
        if (EditorUtility.DisplayCancelableProgressBar(title, detail, Mathf.Clamp01(fraction)))
            throw new OperationCanceledException("Update build cancelled. Existing published content has not been changed.");
    }
    private void Write(string message)
    {
        string line = "[" + elapsed.Elapsed.TotalSeconds.ToString("0.0") + "s] " + message;
        log.WriteLine(line);
        Debug.Log("[Update build] " + line);
    }
    public void Dispose()
    {
        current = null;
        if (!Application.isBatchMode) EditorUtility.ClearProgressBar();
        log.Dispose();
    }
}
