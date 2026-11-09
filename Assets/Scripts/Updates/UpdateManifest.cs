using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Unite.Updates
{
    [Serializable]
    public sealed class UpdateManifest
    {
        public int schemaVersion = 1;
        public string playerVersion;
        public string compatibilityLine;
        public string platform;
        public string addressablesVersion;
        public string patchVersion;
        public string catalogUrl;
        public string patchUrl;
        public string patchSha256;
        public string[] requiredLabels = { "download-required" };

        public void Validate(string player, string line, string target)
        {
            if (schemaVersion != 1 || playerVersion != player || compatibilityLine != line || platform != target)
                throw new InvalidOperationException("This update requires a different player build. Install the latest game build.");
            RequireVersion(addressablesVersion); RequireVersion(patchVersion);
            RequireHttps(catalogUrl); RequireHttps(patchUrl);
            if (patchSha256 == null || !Regex.IsMatch(patchSha256, "^[a-f0-9]{64}$"))
                throw new InvalidOperationException("Invalid balance checksum.");
            if (requiredLabels == null || requiredLabels.Length == 0 || Array.Exists(requiredLabels, string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("The required content labels are missing.");
        }

        public static void RequireVersion(string value)
        {
            if (string.IsNullOrEmpty(value) || !Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$"))
                throw new InvalidOperationException("Versions must be 1–64 letters, digits, dots, underscores or hyphens.");
        }
        public static void RequireHttps(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException("Update URLs must use public HTTPS without credentials.");
        }
        public static string Sha256(string text)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }

    public static class GameRelease
    {
        public static string AddressablesVersion { get; private set; } = "local";
        public static string PatchVersion { get; private set; } = "local";
        public static bool Ready { get; private set; }
        public static string Display => UnityEngine.Application.version + " / " + AddressablesVersion + " / " + PatchVersion;

        public static void Begin() { Ready = false; AddressablesVersion = "local"; PatchVersion = "local"; BalanceRuntime.Reset(); }
        public static void Commit(string content, string patch) { AddressablesVersion = content; PatchVersion = patch; Ready = true; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Begin();
    }
}
