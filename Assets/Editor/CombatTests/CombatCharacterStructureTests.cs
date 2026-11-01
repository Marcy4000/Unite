using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class CombatCharacterStructureTests
{
    private const BindingFlags DeclaredInstanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [TestCase("Cinderace", "Ember,LowSweep,PyroBall,BlazeKick,FlameCharge,Feint,Unite")]
    [TestCase("Glaceon", "Swift,TailWhip,IceShard,IcyWind,FreezeDry,IcicleSpear,Unite")]
    [TestCase("Cresselia", "AuroraBeam,Psybeam,Moonblast,Moonlight,Safeguard,LunarBlessing,Unite")]
    [TestCase("Zoroark", "Slash,FurySwipes,ShadowClaw,Cut,NightSlash,FeintAttack,Unite")]
    [TestCase("Sylveon", "Swift,BabyDollEyes,HyperVoice,MysticalFire,DrainingKiss,CalmMind,Unite")]
    [TestCase("Vaporeon", "Swift,HelpingHand,AquaRing,RainDance,Whirlpool,Dive,Unite")]
    [TestCase("Jolteon", "Swift,Agility,Charge,ThunderFang,Discharge,Electroweb,Unite")]
    [TestCase("Flareon", "Swift,Tackle,HeatWave,Takedown,Flamethrower,FlareBlitz,Unite")]
    public void MigratedCharactersDeclarePrimaryPairAndNamedMoveRoutines(string character, string moves)
    {
        Type runtime = GameType(character + "CharacterRuntime");
        Type execution = GameType(character + "Execution");
        Assert.That(runtime.BaseType, Is.EqualTo(GameType("CombatCharacterRuntime")));
        Assert.That(execution.BaseType, Is.EqualTo(GameType("CombatExecution")));
        Assert.That(runtime.GetMethod("CreateExecution", DeclaredInstanceMethods), Is.Not.Null);
        Assert.That(File.Exists(CharacterPath(character + "CharacterRuntime.cs")), Is.True);
        Assert.That(File.Exists(CharacterPath(character + "Execution.cs")), Is.True);
        Assert.That(File.Exists(Path.Combine(Application.dataPath, "../Docs/CombatMigrations", character + ".md")), Is.True);

        foreach (string move in moves.Split(','))
        {
            MethodInfo method = execution.GetMethod("Execute" + move, DeclaredInstanceMethods, null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, character + " must declare Execute" + move + "().");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(IEnumerator)), character + "." + method.Name);
        }
    }

    [TestCase("Cinderace", "Ember,LowSweep,PyroBall,BlazeKick,FlameCharge,Feint,Unite")]
    [TestCase("Glaceon", "Swift,TailWhip,IceShard,IcyWind,FreezeDry,IcicleSpear,Unite")]
    [TestCase("Cresselia", "AuroraBeam,Psybeam,Moonblast,Moonlight,Safeguard,LunarBlessing,Unite")]
    [TestCase("Zoroark", "Slash,FurySwipes,ShadowClaw,Cut,NightSlash,FeintAttack,Unite")]
    [TestCase("Sylveon", "Swift,BabyDollEyes,HyperVoice,MysticalFire,DrainingKiss,CalmMind,Unite")]
    [TestCase("Vaporeon", "Swift,HelpingHand,AquaRing,RainDance,Whirlpool,Dive,Unite")]
    [TestCase("Jolteon", "Swift,Agility,Charge,ThunderFang,Discharge,Electroweb,Unite")]
    [TestCase("Flareon", "Swift,Tackle,HeatWave,Takedown,Flamethrower,FlareBlitz,Unite")]
    public void ExecuteOnlyDispatchesToNamedMoveRoutines(string character, string moves)
    {
        string source = File.ReadAllText(CharacterPath(character + "Execution.cs"));
        string body = MethodBody(source, @"private\s+IEnumerator\s+Execute\s*\(\s*\)");
        MatchCollection dispatches = Regex.Matches(body, @"case\s+AvailableMoves\.\w+\s*:\s*yield\s+return\s+(Execute\w+)\(\s*\)\s*;\s*break\s*;");
        string[] actual = dispatches.Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
        string[] expected = moves.Split(',').Select(move => "Execute" + move).ToArray();
        Assert.That(actual, Is.EquivalentTo(expected), character + " must dispatch each move once.");
        string selectorOnly = Regex.Replace(body, @"case\s+AvailableMoves\.\w+\s*:\s*yield\s+return\s+Execute\w+\(\s*\)\s*;\s*break\s*;", string.Empty);
        Assert.That(selectorOnly, Does.Match(@"^\s*switch\s*\(\s*Context\.MoveId\s*\)\s*\{\s*\}\s*$"),
            character + " Execute() must contain only its move dispatcher, not cast gameplay or lifecycle setup.");
    }

    [Test]
    public void ZoroarkSerializedEffectDoesNotRetainMoveTimelines()
    {
        Type adapter = GameType("ZoroarkMoveEffect");
        string[] timelines = adapter.GetMethods(DeclaredInstanceMethods)
            .Where(method => method.ReturnType == typeof(IEnumerator) && method.Name != "Execute")
            .Select(method => method.Name).ToArray();
        Assert.That(timelines, Is.Empty, "Zoroark cast timelines belong to ZoroarkExecution, not the serialized effect adapter.");
    }

    private static Type GameType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Missing combat type " + name);
        return type;
    }

    private static string CharacterPath(string fileName) => Path.Combine(Application.dataPath, "Scripts/Combat/Characters", fileName);

    private static string MethodBody(string source, string signature)
    {
        Match declaration = Regex.Match(source, signature);
        Assert.That(declaration.Success, Is.True, "Missing canonical Execute() dispatcher.");
        int opening = source.IndexOf('{', declaration.Index + declaration.Length);
        Assert.That(opening, Is.GreaterThanOrEqualTo(0));
        int depth = 1;
        for (int position = opening + 1; position < source.Length; position++)
        {
            if (source[position] == '{') depth++;
            if (source[position] == '}') depth--;
            if (depth == 0) return source.Substring(opening + 1, position - opening - 1);
        }
        Assert.Fail("Unbalanced Execute() dispatcher.");
        return string.Empty;
    }
}
