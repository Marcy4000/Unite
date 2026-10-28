using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Adds missing defaults only. Re-running this command preserves edited builds,
// weights, outfits and explicit scene bindings.
public static class BotContentSetup
{
    private static readonly string[] Compatible = { "Absol", "Cinderace", "Cresselia", "Duraludon", "Emboar", "Flareon", "Flygon",
        "Glaceon", "Jolteon", "Marshadow", "Meowstic", "Sylveon", "Vaporeon" };

    [MenuItem("Tools/Bots/Create missing loadouts and map bindings")]
    public static void Configure()
    {
        ConfigureRoster();
        BotSceneSetup.WithScene("Assets/Scenes/RemoatStadium.unity", ConfigureMap);
        BotSceneSetup.WithScene("Assets/Scenes/TheiaSkyRuins.unity", ConfigureMap);
    }

    public static void ConfigureRoster()
    {
        BotSceneSetup.WithScene("Assets/Scenes/BootScene.unity", scene =>
            ConfigureRoster(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharactersList>(true)).Single()));
    }

    private static void ConfigureRoster(CharactersList catalog)
    {
        EnsureFolder("Assets/Resources/Bots/Loadouts");
        const string path = "Assets/Resources/Bots/Roster.asset";
        var roster = AssetDatabase.LoadAssetAtPath<BotRosterConfig>(path);
        if (roster == null) { roster = ScriptableObject.CreateInstance<BotRosterConfig>(); AssetDatabase.CreateAsset(roster, path); }
        var builds = new List<BotLoadout>(roster.loadouts);
        foreach (var info in catalog.Characters.Where(c => c != null && !c.Hidden && Compatible.Contains(c.pokemonName)))
        {
            var data = AssetDatabase.LoadAssetAtPath<PokemonBase>(AssetDatabase.GUIDToAssetPath(info.pokemon.AssetGUID));
            if (data == null || data.GetUniteMove() == null || !info.model.RuntimeKeyIsValid()) continue;
            var a = Branches(data, MoveType.MoveA); var b = Branches(data, MoveType.MoveB);
            foreach (var moveA in a) foreach (var moveB in b)
            {
                string buildPath = $"Assets/Resources/Bots/Loadouts/{info.name}-{moveA.move}-{moveB.move}.asset";
                var build = AssetDatabase.LoadAssetAtPath<BotLoadout>(buildPath);
                if (build == null)
                {
                    build = ScriptableObject.CreateInstance<BotLoadout>();
                    build.pokemon = info; build.gameplayPokemon = data; build.moveA = moveA; build.moveB = moveB;
                    var items = info.DamageType == DamageType.Special
                        ? new[] { AvailableHeldItems.WiseGlasses, AvailableHeldItems.ChoiceSpecs, AvailableHeldItems.FocusBand }
                        : new[] { AvailableHeldItems.MuscleBand, AvailableHeldItems.ScopeLens, AvailableHeldItems.FocusBand };
                    if (info.pClass == PokemonClass.Supporter || info.pClass == PokemonClass.Defender)
                        items = new[] { AvailableHeldItems.ResonantGuard, AvailableHeldItems.BuddyBarrier, AvailableHeldItems.FocusBand };
                    build.heldItems = items.Select(item => (byte)Array.FindIndex(catalog.HeldItems, entry => entry != null && entry.heldItemID == item)).ToArray();
                    var battleItems = catalog.BattleItems.Where(item => item != null &&
                        (item.battleItemType == AvailableBattleItems.Potion || item.battleItemType == AvailableBattleItems.Eject ||
                         item.battleItemType == AvailableBattleItems.XSpeed || item.battleItemType == AvailableBattleItems.XAttack)).ToArray();
                    build.battleItem = battleItems[builds.Count % battleItems.Length];
                    AssetDatabase.CreateAsset(build, buildPath);
                }
                if (!builds.Contains(build)) builds.Add(build);
            }
        }
        roster.loadouts = builds.ToArray();
        EditorUtility.SetDirty(roster); AssetDatabase.SaveAssets();
    }

    private static MoveAsset[] Branches(PokemonBase data, MoveType slot) => data.LearnableMoves
        .Where(t => !t.isUpgraded && t.moves.Any(m => m != null && m.moveType == slot)).OrderByDescending(t => t.level)
        .First().moves.Where(m => m != null && m.moveType == slot).ToArray();

    private static void ConfigureMap(UnityEngine.SceneManagement.Scene scene)
    {
        var spawners = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WildPokemonSpawner>(true)).ToArray();
        var context = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BotMapContext>(true)).SingleOrDefault();
        if (context == null)
        {
            var root = new GameObject("BotMapContext");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            context = root.AddComponent<BotMapContext>();
        }
        var camps = new List<BotMapContext.Camp>(context.camps);
        foreach (var spawner in spawners)
        {
            if (camps.Any(c => c.spawner == spawner)) continue;
            var parents = spawner.GetComponentsInParent<Transform>().Select(t => t.name).ToArray();
            var id = (AvailableWildPokemons)new SerializedObject(spawner).FindProperty("wildPokemonID").enumValueIndex;
            var entry = new BotMapContext.Camp { spawner = spawner, team = Team.Neutral, lane = "mid" };
            if (id == AvailableWildPokemons.Rayquaza || id == AvailableWildPokemons.Zapdos) entry.kind = BotMapContext.CampKind.FinalObjective;
            else if (spawner.IsObjective) entry.kind = BotMapContext.CampKind.Objective;
            else if (parents.Any(n => n.StartsWith("StartingAipoms") || n.StartsWith("StartingBunnelby")))
            {
                entry.kind = BotMapContext.CampKind.Opening;
                entry.team = parents.Any(n => n.EndsWith("Blue")) ? Team.Blue : Team.Orange;
                entry.lane = spawner.transform.position.z > 0 ? "top" : "bottom";
            }
            else if (parents.Contains("BlueJungle") || parents.Contains("OrangeJungle"))
            {
                entry.kind = BotMapContext.CampKind.Jungle;
                entry.team = parents.Contains("BlueJungle") ? Team.Blue : Team.Orange;
            }
            else if (parents.Contains("TopPath") || parents.Contains("BotPath"))
            {
                entry.kind = BotMapContext.CampKind.Lane;
                entry.lane = parents.Contains("TopPath") ? "top" : "bottom";
            }
            else if (parents.Contains("MidLane")) entry.kind = BotMapContext.CampKind.Center;
            else throw new InvalidOperationException("Classify this camp explicitly: " + spawner.name);
            camps.Add(entry);
        }
        context.camps = camps.ToArray();
        if (context.goals.Length == 0) context.goals = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GoalZone>(true)).ToArray();
        EditorUtility.SetDirty(context);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
