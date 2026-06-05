using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ClickyKeys
{
    /// <summary>
    /// Small shared reader/writer for <c>%AppData%\ClickyKeys\config.json</c>.
    ///
    /// MainWindow already has its own private LoadInitSettings/SaveInitSettings
    /// (which additionally handle version-bump/update logic). This service
    /// exists so components that aren't MainWindow — notably <see cref="App"/>
    /// at startup and the <see cref="Settings"/> window — can read and persist
    /// the same <see cref="Configuration"/> without reaching into MainWindow's
    /// internals. All three talk to the same file and the same type, so they
    /// stay compatible.
    ///
    /// Writes go through <see cref="AtomicFile"/> (temp file + atomic move) and
    /// are serialized behind <see cref="_gate"/>, so a Settings-window save and
    /// a MainWindow save can't interleave into a half-written file.
    ///
    /// IMPORTANT: persisting a single field still rewrites the whole file. To
    /// avoid clobbering values another writer set, <see cref="Update"/> does a
    /// load-mutate-save under the lock rather than blindly serializing a stale
    /// in-memory object.
    /// </summary>
    internal static class ConfigStore
    {
        private static readonly object _gate = new();

        private static readonly JsonSerializerOptions ReadOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private static readonly JsonSerializerOptions WriteOpts = new()
        {
            WriteIndented = true,
        };

        public static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClickyKeys");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.json");
            }
        }

        /// <summary>
        /// Returns the persisted configuration, or a fresh default
        /// <see cref="Configuration"/> when the file is missing or unreadable.
        /// Never throws — a corrupt config self-heals to defaults.
        /// </summary>
        public static Configuration Load()
        {
            lock (_gate)
            {
                return LoadNoLock();
            }
        }

        /// <summary>
        /// Persists <paramref name="cfg"/> wholesale. Prefer
        /// <see cref="Update"/> when you only mean to change one field.
        /// </summary>
        public static void Save(Configuration cfg)
        {
            lock (_gate)
            {
                SaveNoLock(cfg);
            }
        }

        /// <summary>
        /// Load-mutate-save under the lock. Use this for single-field edits so
        /// concurrent writers don't overwrite each other's unrelated fields.
        /// </summary>
        public static void Update(Action<Configuration> mutate)
        {
            lock (_gate)
            {
                var cfg = LoadNoLock();
                mutate(cfg);
                SaveNoLock(cfg);
            }
        }

        private static Configuration LoadNoLock()
        {
            try
            {
                var path = ConfigPath;
                if (!File.Exists(path))
                    return new Configuration();

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Configuration>(json, ReadOpts)
                       ?? new Configuration();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConfigStore.Load failed: {ex}");
                return new Configuration();
            }
        }

        private static void SaveNoLock(Configuration cfg)
        {
            try
            {
                var json = JsonSerializer.Serialize(cfg, WriteOpts);
                AtomicFile.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConfigStore.Save failed: {ex}");
            }
        }
    }
}
