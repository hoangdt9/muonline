#nullable enable
using System;
using System.IO;

namespace Client.Main.Configuration
{
    /// <summary>
    /// Resolves MU client asset root so extracted/downloaded data is stable across rebuilds and branch switches.
    /// </summary>
    public static class GameDataPathResolver
    {
        private const string AppFolderName = "MUMono";

        /// <summary>
        /// Returns absolute path for MU asset folder (models, textures inside extracted ZIP layout).
        /// </summary>
        /// <param name="configuredPath">
        /// Optional path from appsettings / appsettings.local.json.
        /// If null or whitespace, uses OS-local application data directory / <see cref="AppFolderName"/> / Data.
        /// Supports ~/ prefix on Unix and macOS, environment variables (%VAR% / $VAR).
        /// </param>
        public static string Resolve(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return Path.GetFullPath(ExpandUserAndEnvironment(configuredPath.Trim()));

            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, AppFolderName, "Data");
        }

        private static string ExpandUserAndEnvironment(string path)
        {
            if (path.Length >= 2 && path[0] == '~' && (path[1] == '/' || path[1] == '\\'))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, path.Substring(2));
            }

            if (path == "~")
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return Environment.ExpandEnvironmentVariables(path);
        }
    }
}
