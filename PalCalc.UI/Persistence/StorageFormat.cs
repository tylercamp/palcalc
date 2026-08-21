using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PalCalc.UI.Persistence
{

    internal static class StorageFormat
    {
        public const int CurrentVersion = 2;
        public const int LegacyVersion = 0;
        public const string ManifestFileName = "storage-format.json";

        public static string ManifestPath(string dataPath) => Path.Combine(dataPath, ManifestFileName);

        public static bool HasAuthoritativeDocuments(string dataPath)
        {
            if (!Directory.Exists(dataPath))
                return false;

            return Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories)
                .Any(path => !string.Equals(Path.GetFileName(path), "settings.json", StringComparison.OrdinalIgnoreCase));
        }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class StorageManifest
    {
        [JsonProperty("version")]
        public int Version { get; init; }
    }

    internal sealed class StorageFormatException : Exception
    {
        public StorageFormatException(string message) : base(message) { }
    }

    internal abstract class StorageMigration
    {
        protected StorageMigration(int targetVersion)
            : this(targetVersion - 1, targetVersion)
        {
        }

        protected StorageMigration(int sourceVersion, int targetVersion)
        {
            SourceVersion = sourceVersion;
            TargetVersion = targetVersion;
        }

        public int SourceVersion { get; }
        public int TargetVersion { get; }

        public abstract void Apply(StorageMigrationContext context);
    }

    internal sealed class StorageMigrationContext
    {
        public StorageMigrationContext(string dataPath)
        {
            DataPath = dataPath;
            ManifestPath = StorageFormat.ManifestPath(dataPath);
        }

        public string DataPath { get; }
        public string ManifestPath { get; }

        public void WriteManifest(int version) => StorageFile.WriteAtomic(
            ManifestPath,
            JsonConvert.SerializeObject(new StorageManifest { Version = version }),
            backup: false
        );
    }

    internal static class StorageFile
    {
        public static void WriteAtomic(string path, string contents, bool backup)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempPath = path + $".tmp-{Guid.NewGuid():N}";
            var backupPath = path + ".bak";

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                // Keep the first pre-migration copy across retries and later migration steps.
                if (backup && File.Exists(path) && !File.Exists(backupPath))
                    File.Copy(path, backupPath, overwrite: true);

                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }

    internal static class StorageMigrationRunner
    {
        private static ILogger logger = Log.ForContext(typeof(StorageMigrationRunner));

        public static void EnsureCurrent(string dataPath)
        {
            Directory.CreateDirectory(dataPath);

            var manifestPath = StorageFormat.ManifestPath(dataPath);
            if (!File.Exists(manifestPath))
            {
                logger.Information("Persistent version manifest file missing");
                if (!StorageFormat.HasAuthoritativeDocuments(dataPath))
                {
                    logger.Information("No settings files found, assuming fresh install");
                    new StorageMigrationContext(dataPath).WriteManifest(StorageFormat.CurrentVersion);
                    return;
                }
                else
                {
                    logger.Information("Some old settings files found");
                }
            }

            var currentVersion = File.Exists(manifestPath)
                ? ReadManifest(manifestPath).Version
                : StorageFormat.LegacyVersion;

            logger.Information("Preparing migrations from detected manifest version {Version} to current version {Latest}", currentVersion, StorageFormat.CurrentVersion);

            if (currentVersion > StorageFormat.CurrentVersion)
            {
                throw new StorageFormatException(
                    $"Storage format version {currentVersion} is newer than the supported version {StorageFormat.CurrentVersion}."
                );
            }

            if (currentVersion < StorageFormat.LegacyVersion)
                throw new StorageFormatException($"Storage format version {currentVersion} is invalid.");

            if (currentVersion == StorageFormat.CurrentVersion)
            {
                logger.Information("No migrations needed");
                return;
            }

            var migrations = DiscoverMigrations();
            ValidateRoutes(migrations);

            var context = new StorageMigrationContext(dataPath);
            while (currentVersion < StorageFormat.CurrentVersion)
            {
                var migration = migrations
                    .Where(m => m.SourceVersion == currentVersion && m.TargetVersion <= StorageFormat.CurrentVersion)
                    .Where(m => CanReachCurrent(m.TargetVersion, migrations, new HashSet<int>()))
                    .OrderByDescending(m => m.TargetVersion)
                    .FirstOrDefault();

                if (migration == null)
                    throw new StorageFormatException($"No migration path from storage version {currentVersion}.");

                logger.Information("Running migration {MigrationClass} to move from {CurrentVersion} to {NextVersion}", migration.GetType().Name, currentVersion, migration.TargetVersion);

                migration.Apply(context);
                context.WriteManifest(migration.TargetVersion);
                currentVersion = migration.TargetVersion;

                logger.Information("Migration step complete");
            }
        }

        private static StorageManifest ReadManifest(string path)
        {
            return JsonConvert.DeserializeObject<StorageManifest>(File.ReadAllText(path))
                ?? throw new StorageFormatException("Storage format manifest was empty.");
        }

        private static List<StorageMigration> DiscoverMigrations() =>
            typeof(StorageMigration).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && typeof(StorageMigration).IsAssignableFrom(type))
                .Select(type => Activator.CreateInstance(type, nonPublic: true) as StorageMigration)
                .Where(migration => migration != null)
                .ToList();

        private static void ValidateRoutes(IReadOnlyCollection<StorageMigration> migrations)
        {
            if (migrations.GroupBy(m => (m.SourceVersion, m.TargetVersion)).Any(g => g.Count() > 1))
                throw new StorageFormatException("Storage migrations contain duplicate source/target edges.");

            if (migrations.Any(m => m.SourceVersion >= m.TargetVersion))
                throw new StorageFormatException("Storage migrations must move to a higher version.");

            if (migrations.Any(m => m.TargetVersion > StorageFormat.CurrentVersion))
                throw new StorageFormatException("Storage migrations cannot overshoot the current version.");

            for (var version = StorageFormat.LegacyVersion; version < StorageFormat.CurrentVersion; version++)
            {
                if (!CanReachCurrent(version, migrations, new HashSet<int>()))
                    throw new StorageFormatException($"Storage version {version} has no route to the current version.");
            }
        }

        private static bool CanReachCurrent(int version, IReadOnlyCollection<StorageMigration> migrations, HashSet<int> visited)
        {
            if (version == StorageFormat.CurrentVersion)
                return true;

            if (!visited.Add(version))
                return false;

            return migrations
                .Where(m => m.SourceVersion == version && m.TargetVersion <= StorageFormat.CurrentVersion)
                .Any(m => CanReachCurrent(m.TargetVersion, migrations, new HashSet<int>(visited)));
        }
    }

}
