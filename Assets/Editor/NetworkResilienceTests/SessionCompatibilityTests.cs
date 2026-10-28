using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Multiplayer;
using Unity.Services.Multiplayer;
using MatchmakerPlayer = Unity.Services.Matchmaker.Models.Player;

public class SessionCompatibilityTests
{
    [Test]
    public void BrowserAndQuickJoinFilterUsesCurrentIndexedVersion()
    {
        var controller = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("LobbyController"))
            .First(type => type != null);
        var filters = (List<FilterOption>)controller.GetMethod("GetBrowserFilters", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var versionFilter = filters.Single(filter => filter.Field == FilterField.StringIndex1);
        Assert.That(versionFilter.Value, Is.EqualTo(SessionCompatibility.Current));
        Assert.That(versionFilter.Operation, Is.EqualTo(FilterOperation.Equal));
    }

    [Test]
    public void DiscriminatorIsVersionSpecificAndCannotMatchShippedClients()
    {
        Assert.That(SessionCompatibility.ForVersion("1.0"), Is.Not.EqualTo("sessions-v1"));
        Assert.That(SessionCompatibility.ForVersion("1.0"), Is.Not.EqualTo(SessionCompatibility.ForVersion("1.1")));
        Assert.That(SessionCompatibility.Current, Is.EqualTo(SessionCompatibility.ForVersion(UnityEngine.Application.version)));
    }

    [TestCase(null)]
    [TestCase("sessions-v1")]
    [TestCase("unite-app:incompatible-test-version")]
    public void SessionRejectsMissingLegacyAndDifferentVersions(string version)
    {
        var properties = new Dictionary<string, SessionProperty>();
        if (version != null) properties[SessionCompatibility.PropertyName] = new SessionProperty(version);
        var error = Assert.Throws<SessionVersionMismatchException>(() => SessionCompatibility.Validate(properties));
        StringAssert.Contains(UnityEngine.Application.version, error.Message);
        StringAssert.Contains("same version", error.Message);
    }

    [Test]
    public void SessionAcceptsOnlyExactCurrentVersion()
    {
        var properties = new Dictionary<string, SessionProperty>
        {
            [SessionCompatibility.PropertyName] = new SessionProperty(SessionCompatibility.Current)
        };
        Assert.DoesNotThrow(() => SessionCompatibility.Validate(properties));
        properties[SessionCompatibility.PropertyName] = new SessionProperty(SessionCompatibility.Current + " ");
        Assert.Throws<SessionVersionMismatchException>(() => SessionCompatibility.Validate(properties));
    }

    [Test]
    public void MixedMatchmakerAssignmentsAreRejectedBeforeHandoff()
    {
        var current = new MatchmakerPlayer("current", new Dictionary<string, string>
        {
            [SessionCompatibility.PropertyName] = SessionCompatibility.Current
        });
        Assert.DoesNotThrow(() => SessionCompatibility.ValidateRoster(new[] { current }));
        Assert.Throws<SessionVersionMismatchException>(() => SessionCompatibility.ValidateRoster(new[] { current, new MatchmakerPlayer("legacy") }));
        var other = new MatchmakerPlayer("other", new Dictionary<string, string>
        {
            [SessionCompatibility.PropertyName] = "unite-app:other"
        });
        Assert.Throws<SessionVersionMismatchException>(() => SessionCompatibility.ValidateRoster(new[] { current, other }));
    }
}
