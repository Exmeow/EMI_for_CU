using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace EMI
{
    internal static class UpdateChecker
    {
        public const string LatestReleaseUrl =
            "https://github.com/Exmeow/EMI_for_CU/releases/latest";

        private const string LatestReleaseApiUrl =
            "https://api.github.com/repos/Exmeow/EMI_for_CU/releases/latest";

        private static bool _checkStarted;
        private static bool _updateAvailable;
        private static bool _hiddenForSession;

        public static event Action Changed;

        public static string LatestTag { get; private set; }

        public static bool ShouldShowNotice => _updateAvailable && !_hiddenForSession;

        public static IEnumerator CheckForUpdates()
        {
            if (_checkStarted)
            {
                yield break;
            }

            _checkStarted = true;
            using (UnityWebRequest request = UnityWebRequest.Get(LatestReleaseApiUrl))
            {
                request.timeout = 10;
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.SetRequestHeader("User-Agent", "EMI-for-CU/" + EmiPlugin.PluginVersion);
                request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    EmiPlugin.Log?.LogWarning(
                        $"[EMI] Update check failed ({request.responseCode}): {request.error}");
                    yield break;
                }

                ReleaseResponse release;
                try
                {
                    release = JsonConvert.DeserializeObject<ReleaseResponse>(
                        request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogWarning($"[EMI] Update response could not be parsed: {exception.Message}");
                    yield break;
                }

                if (release == null ||
                    !TryParseVersion(release.TagName, out Version latestVersion) ||
                    !TryParseVersion(EmiPlugin.PluginVersion, out Version currentVersion))
                {
                    EmiPlugin.Log?.LogWarning(
                        $"[EMI] Update response contained an invalid version tag: {release?.TagName ?? "<null>"}");
                    yield break;
                }

                LatestTag = FormatDisplayVersion(latestVersion);
                _updateAvailable = latestVersion > currentVersion;
                EmiPlugin.Log?.LogInfo(
                    _updateAvailable
                        ? $"[EMI] Update available: {LatestTag} " +
                          $"(current {FormatDisplayVersion(currentVersion)})."
                        : $"[EMI] Version is current: {FormatDisplayVersion(currentVersion)}; " +
                          $"latest {LatestTag}.");
                Changed?.Invoke();
            }
        }

        public static void HideForSession()
        {
            if (_hiddenForSession)
            {
                return;
            }

            _hiddenForSession = true;
            Changed?.Invoke();
        }

        private static string FormatDisplayVersion(Version version)
        {
            string value = "v" + version.Major + "." + version.Minor + "." + version.Build;
            return version.Revision > 0
                ? value + "." + version.Revision
                : value;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            int suffix = normalized.IndexOfAny(new[] { '-', '+' });
            if (suffix >= 0)
            {
                normalized = normalized.Substring(0, suffix);
            }

            string[] parts = normalized.Split('.');
            if (parts.Length == 0 || parts.Length > 4)
            {
                return false;
            }

            int[] components = new int[4];
            for (int index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(
                        parts[index],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out components[index]) ||
                    components[index] < 0)
                {
                    return false;
                }
            }

            version = new Version(
                components[0],
                components[1],
                components[2],
                components[3]);
            return true;
        }

        private sealed class ReleaseResponse
        {
            [JsonProperty("tag_name")]
            public string TagName { get; set; }
        }
    }
}
