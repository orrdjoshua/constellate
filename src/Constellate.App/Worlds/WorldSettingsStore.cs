using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Constellate.App.Worlds
{
    public enum WorldSaveBehavior
    {
        Ask,
        Save,
        Discard
    }

    public sealed record WorldSaveSettings(
        bool AutoSaveEnabled = true,
        int AutoSaveIntervalSeconds = 60,
        WorldSaveBehavior SwitchBehavior = WorldSaveBehavior.Ask,
        WorldSaveBehavior ExitBehavior = WorldSaveBehavior.Ask);

    internal sealed class WorldSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _configPath;

        public WorldSettingsStore(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                throw new ArgumentException("Project root path is required.", nameof(projectRootPath));
            }

            _configPath = Path.Combine(projectRootPath, ".constellate", "config", "world-settings.json");
        }

        public WorldSaveSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var settings = JsonSerializer.Deserialize<WorldSaveSettings>(json, JsonOptions);
                    if (settings is not null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // Keep global save settings non-destructive if the config is malformed.
            }

            return new WorldSaveSettings();
        }

        public void SaveSettings(WorldSaveSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var configDirectory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            File.WriteAllText(_configPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
    }
}
