using System;
using Unite.Updates;
using System.Collections.Generic;
using Unity.Services.Multiplayer;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

namespace Unite.Multiplayer
{
    public sealed class SessionVersionMismatchException : InvalidOperationException
    {
        public SessionVersionMismatchException() : base(
            $"This session uses a different game version. You are running {GameRelease.Display}. " +
            "All players must update to the same version and create a new lobby.") { }
    }

    public static class SessionCompatibility
    {
        public const string PropertyName = "GameVersion";
        public static string Current => ForVersion(Application.version);
        public static string ForVersion(string version) => ForVersions(version, GameRelease.AddressablesVersion, GameRelease.PatchVersion);
        public static string ForVersions(string player, string content, string patch) =>
            // A fixed-size key also fits Lobby/Matchmaker indexed string limits for long version names.
            "unite-v2:" + UpdateManifest.Sha256(player + "\n" + content + "\n" + patch);

        public static void Validate(IReadOnlyDictionary<string, SessionProperty> properties)
        {
            if (properties == null || !properties.TryGetValue(PropertyName, out var version) ||
                version == null || !string.Equals(version.Value, Current, StringComparison.Ordinal))
                throw new SessionVersionMismatchException();
        }

        public static void ValidateRoster(IEnumerable<Player> players)
        {
            if (players == null) throw new SessionVersionMismatchException();
            foreach (var player in players)
            {
                var data = player.CustomData?.GetAs<Dictionary<string, string>>();
                if (data == null || !data.TryGetValue(PropertyName, out var version) ||
                    !string.Equals(version, Current, StringComparison.Ordinal))
                    throw new SessionVersionMismatchException();
            }
        }
    }
}
