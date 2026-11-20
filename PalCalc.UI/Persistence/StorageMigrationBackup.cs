using Serilog;
using System;
using System.IO;

namespace PalCalc.UI.Persistence
{
    internal sealed class StorageMigrationBackup(string dataPath)
    {
        private static readonly ILogger logger = Log.ForContext<StorageMigrationBackup>();
        private readonly string backupRoot = dataPath + "-backups";
        private readonly string pendingPath = Path.Combine(dataPath, ".migration-backup");

        public bool IsPending => File.Exists(pendingPath);

        public FileStream LockMigration()
        {
            var lockPath = dataPath + ".migration-lock";
            try
            {
                var lockFile = new FileInfo(lockPath);
                if (lockFile.Exists)
                    RejectLink(lockFile);
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None,
                    bufferSize: 1, FileOptions.DeleteOnClose);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new StorageMigrationBackupException(
                    $"PalCalc couldn't prepare the data folder for migration at:\n{dataPath}\n\n" +
                    "Close any other copies of PalCalc and check that you can write to this location, then try again.", ex);
            }
        }

        public void EnsureCreated(int sourceVersion)
        {
            try
            {
                var source = new DirectoryInfo(dataPath);
                RejectLink(source);
                var root = Directory.CreateDirectory(backupRoot);
                RejectLink(root);

                // Keep using the original snapshot even if an earlier migration advanced the manifest.
                if (IsPending)
                {
                    var name = File.ReadAllText(pendingPath);
                    if (string.IsNullOrWhiteSpace(name) || name is "." or ".."
                        || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                        || !Directory.Exists(Path.Combine(backupRoot, name)))
                        throw new IOException("The backup recorded for this migration is missing or invalid.");

                    RejectLink(new DirectoryInfo(Path.Combine(backupRoot, name)));
                    return;
                }

                var number = 1;
                var snapshotName = $"backup-pre-{App.Version}__{number}";
                var snapshotPath = Path.Combine(backupRoot, snapshotName);
                while (Directory.Exists(snapshotPath) || File.Exists(snapshotPath))
                {
                    number++;
                    snapshotName = $"backup-pre-{App.Version}__{number}";
                    snapshotPath = Path.Combine(backupRoot, snapshotName);
                }
                var partialPath = Path.Combine(backupRoot, $".partial-{Guid.NewGuid():N}");
                try
                {
                    CopyDirectory(source, partialPath);
                    Directory.Move(partialPath, snapshotPath);
                    StorageFile.WriteAtomic(pendingPath, snapshotName);
                    logger.Information("Backed up storage format {Version} to {BackupPath}", sourceVersion, snapshotPath);
                }
                finally
                {
                    if (Directory.Exists(partialPath))
                    {
                        try { Directory.Delete(partialPath, recursive: true); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            logger.Warning(ex, "Unable to remove incomplete data backup {BackupPath}", partialPath);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new StorageMigrationBackupException(
                    $"PalCalc couldn't prepare the data backup at:\n{backupRoot}\n\n" +
                    $"No migration ran on this launch. {ex.Message}", ex);
            }
        }

        public void Complete()
        {
            try { File.Delete(pendingPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new StorageMigrationBackupException(
                    $"PalCalc migrated the data but couldn't remove the migration marker at:\n{pendingPath}\n\n{ex.Message}", ex);
            }
        }

        private static void CopyDirectory(DirectoryInfo source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var entry in source.EnumerateFileSystemInfos())
            {
                RejectLink(entry);
                var copyPath = Path.Combine(destination, entry.Name);
                if (entry is DirectoryInfo directory)
                    CopyDirectory(directory, copyPath);
                else
                    File.Copy(entry.FullName, copyPath);
            }
        }

        private static void RejectLink(FileSystemInfo entry)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"The backup cannot follow a link at '{entry.FullName}'.");
        }
    }

    internal sealed class StorageMigrationBackupException(string message, Exception innerException)
        : Exception(message, innerException);
}
