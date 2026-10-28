using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class BotNetworkContractTests
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);

    [TestCase("BotParticipant")]
    [TestCase("ParticipantCharacter")]
    public void ParticipantIdentityAndChoiceRoundTripThroughNetcode(string name)
    {
        var type = GameType(name);
        var value = Activator.CreateInstance(type);
        type.GetField("Id").SetValue(value, new FixedString32Bytes("bot-9"));
        type.GetField("Character").SetValue(value, (short)4);
        if (name == "BotParticipant")
        {
            type.GetField("Loadout").SetValue(value, (short)23);
            type.GetField("Outfit").SetValue(value, new FixedString128Bytes("trainer-outfit"));
        }
        using (var writer = new FastBufferWriter(1024, Allocator.Temp))
        {
            writer.WriteNetworkSerializable((INetworkSerializable)value);
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                var method = typeof(FastBufferReader).GetMethods().Single(info => info.Name == "ReadNetworkSerializable" &&
                    info.GetParameters().Length == 1 && info.GetParameters()[0].ParameterType.GetElementType().IsGenericParameter);
                object[] arguments = { null };
                method.MakeGenericMethod(type).Invoke(reader, arguments);
                Assert.That(type.GetField("Id").GetValue(arguments[0]), Is.EqualTo(new FixedString32Bytes("bot-9")));
                Assert.That(type.GetField("Character").GetValue(arguments[0]), Is.EqualTo((short)4));
                if (name == "BotParticipant")
                {
                    Assert.That(type.GetField("Loadout").GetValue(arguments[0]), Is.EqualTo((short)23));
                    Assert.That(type.GetField("Outfit").GetValue(arguments[0]), Is.EqualTo(new FixedString128Bytes("trainer-outfit")));
                }
            }
        }
    }

    [Test]
    public void CharacterRequestsUseSenderIdentityAndCommitsRequireServerAuthority()
    {
        var type = GameType("PlayerNetworkManager");
        var request = type.GetMethod("SelectParticipantCharacterRpc");
        Assert.That(request.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(short), typeof(RpcParams) }));
        Assert.That(request.GetCustomAttribute<RpcAttribute>().InvokePermission, Is.EqualTo(RpcInvokePermission.Everyone));
        Assert.That(type.GetMethod("CommitCharactersRpc", Fields).GetCustomAttribute<RpcAttribute>().InvokePermission,
            Is.EqualTo(RpcInvokePermission.Server));
    }

    [Test]
    public void BotControlNeverNeedsTheHumanInputSingleton()
    {
        var root = new GameObject("Bot input isolation");
        root.SetActive(false);
        try
        {
            root.AddComponent<NetworkObject>();
            var manager = root.AddComponent(GameType("PlayerManager"));
            manager.GetType().GetMethod("ConfigureBotBeforeSpawn").Invoke(manager,
                new[] { (object)"bot-1", Enum.Parse(GameType("Team"), "Orange"), (short)0 });
            Assert.That(manager.GetType().GetProperty("IsBot").GetValue(manager), Is.True);
            Assert.That(manager.GetType().GetProperty("IsLocalHuman").GetValue(manager), Is.False);
            Assert.That(manager.GetType().GetProperty("MovementInput").GetValue(manager), Is.EqualTo(Vector2.zero));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void BrainConstructionDoesNotCallNativeNavigationBeforeStart()
    {
        var root = new GameObject("Bot brain construction");
        root.SetActive(false);
        try { Assert.DoesNotThrow(() => root.AddComponent(GameType("BotBrain"))); }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
    }
}
