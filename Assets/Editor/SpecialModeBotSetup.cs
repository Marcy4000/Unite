using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class SpecialModeBotSetup
{
    [MenuItem("Tools/Bots/Create missing special-mode bindings")]
    public static void Configure()
    {
        WithGeometry("Assets/Scenes/AuromaParkMap.unity", () => BotSceneSetup.WithScene("Assets/Scenes/UniteRoyale.unity", scene =>
        {
            var context = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BotArenaContext>(true)).SingleOrDefault();
            if (context == null) context = Create<BotArenaContext>(scene);
            if (context.objectives.Length == 0) context.objectives = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WildPokemonSpawner>(true)).Where(w => w.IsObjective).ToArray();
            if (context.patrolPoints.Length == 0)
            {
                var points = new System.Collections.Generic.List<Vector3>();
                foreach (float x in new[] { -25f, -12f, 0f, 12f, 25f }) foreach (float z in new[] { -25f, 0f, 25f })
                    if (NavMesh.SamplePosition(new Vector3(x, 0, z), out var hit, 6, NavMesh.AllAreas)) points.Add(hit.position);
                if (points.Count < 4) throw new InvalidOperationException("Royale navigation is not available for authoring.");
                context.patrolPoints = points.ToArray();
            }
            EditorUtility.SetDirty(context);
        }));
        WithGeometry("Assets/Scenes/PsyduckRacingMap.unity", () => BotSceneSetup.WithScene("Assets/Scenes/PsyduckRacing.unity", scene =>
        {
            var route = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BotRaceRoute>(true)).SingleOrDefault();
            if (route == null) route = Create<BotRaceRoute>(scene);
            if (route.segments.Length != 0) return;
            var checkpoints = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RaceCheckpoint>(true)).OrderBy(c => c.CheckpointIndex).ToArray();
            var pads = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Launchpad>(true)).ToArray();
            var segments = new BotRaceRoute.Segment[checkpoints.Length];
            var path = new NavMeshPath();
            for (int i = 0; i < checkpoints.Length; i++)
            {
                var previous = checkpoints[(i + checkpoints.Length - 1) % checkpoints.Length];
                var guides = new System.Collections.Generic.List<Vector3>();
                float segmentDistance = Vector3.Distance(previous.transform.position, checkpoints[i].transform.position);
                var requiredLaunch = pads.FirstOrDefault(p => Vector3.Distance(previous.transform.position, p.transform.position) < segmentDistance * .6f &&
                    Vector3.Distance(p.endPoint.position, checkpoints[i].transform.position) < segmentDistance);
                if (requiredLaunch != null) { guides.Add(requiredLaunch.transform.position); guides.Add(requiredLaunch.endPoint.position); }
                else if (NavMesh.SamplePosition(previous.transform.position, out var start, 12, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(checkpoints[i].transform.position, out var end, 12, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    guides.AddRange(path.corners.Skip(1).Take(Mathf.Max(0, path.corners.Length - 2)));
                else
                {
                    var pad = pads.OrderBy(p => Vector3.Distance(previous.transform.position, p.transform.position) + Vector3.Distance(p.endPoint.position, checkpoints[i].transform.position)).FirstOrDefault();
                    if (pad != null) { guides.Add(pad.transform.position); guides.Add(pad.endPoint.position); }
                }
                segments[i] = new BotRaceRoute.Segment { checkpoint = checkpoints[i], guides = guides.ToArray() };
            }
            route.segments = segments; EditorUtility.SetDirty(route);
        }));
    }
    private static T Create<T>(Scene scene) where T : Component
    {
        var root = new GameObject(typeof(T).Name); SceneManager.MoveGameObjectToScene(root, scene); return root.AddComponent<T>();
    }
    private static void WithGeometry(string path, Action action)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author outside Play mode.");
        var geometry = SceneManager.GetSceneByPath(path); bool loaded = geometry.isLoaded;
        if (!loaded) geometry = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { action(); } finally { if (!loaded) EditorSceneManager.CloseScene(geometry, true); }
    }
}
