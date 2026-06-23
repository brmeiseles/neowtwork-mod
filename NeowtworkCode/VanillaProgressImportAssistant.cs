using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Sts2Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace Neowtwork.NeowtworkCode;

internal static class VanillaProgressImportAssistant
{
    private const string DialogName = "NeowtworkVanillaProgressImportDialog";
    private const string MarkerFileName = "vanilla_import_markers.txt";
    private const long MinimumMeaningfulProgressBytes = 4096;
    private const long MinimumMeaningfulProfileBytes = 32768;

    private static readonly HashSet<string> DismissedThisSession = [];

    public static void TryOfferImport(Sts2Logger logger)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree sceneTree)
            {
                logger.Info("Skipping vanilla progress import prompt: Godot scene tree is not available.");
                return;
            }

            TryCreateDialog(sceneTree, logger);
        }
        catch (Exception exception)
        {
            logger.Error($"Failed while scheduling vanilla progress import prompt: {exception}");
        }
    }

    private static void TryCreateDialog(SceneTree sceneTree, Sts2Logger logger)
    {
        try
        {
            ImportCandidate? candidate = FindBestImportCandidate(logger);
            if (candidate == null)
            {
                return;
            }

            if (sceneTree.Root.GetNodeOrNull<ConfirmationDialog>(DialogName) != null)
            {
                return;
            }

            ConfirmationDialog dialog = new()
            {
                Name = DialogName,
                Title = "Import base-game data?",
                DialogText =
                    "Your modded profile appears to be missing base-game progress.\n\n" +
                    "Neowtwork can copy your vanilla progress and run history into the modded profile so the compendium and card stats match your base game.\n\n" +
                    "A backup will be created first.",
                OkButtonText = "Import",
                CancelButtonText = "Not Now",
                DialogAutowrap = true,
                DialogCloseOnEscape = true,
                MinSize = new Vector2I(720, 340)
            };

            dialog.Confirmed += () =>
            {
                ImportResult result = ImportVanillaProfile(candidate, logger);
                ShowResultDialog(sceneTree, result);
                dialog.QueueFree();
            };

            dialog.Canceled += () =>
            {
                DismissedThisSession.Add(candidate.MarkerKey);
                logger.Info($"Vanilla progress import skipped for {candidate.DisplayName}.");
                dialog.QueueFree();
            };

            sceneTree.Root.AddChild(dialog);
            dialog.PopupCentered(new Vector2I(760, 360));

            logger.Info(
                $"Offering vanilla progress import for {candidate.DisplayName}: " +
                $"vanilla files={candidate.SourceStats.FileCount}, modded files={candidate.TargetStats.FileCount}.");
        }
        catch (Exception exception)
        {
            logger.Error($"Failed while creating vanilla progress import prompt: {exception}");
        }
    }

    private static ImportCandidate? FindBestImportCandidate(Sts2Logger logger)
    {
        foreach (DirectoryInfo steamUserDirectory in FindSteamUserDirectories())
        {
            DirectoryInfo moddedDirectory = new(Path.Combine(steamUserDirectory.FullName, "modded"));
            List<ImportCandidate> candidates = [];

            foreach (DirectoryInfo vanillaProfile in FindVanillaProfiles(steamUserDirectory))
            {
                string profileName = vanillaProfile.Name;
                DirectoryInfo moddedProfile = new(Path.Combine(moddedDirectory.FullName, profileName));
                ProfileStats sourceStats = ProfileStats.FromDirectory(vanillaProfile);
                ProfileStats targetStats = ProfileStats.FromDirectory(moddedProfile);
                string markerKey = $"{steamUserDirectory.Name}/{profileName}";

                if (!sourceStats.HasMeaningfulData)
                {
                    continue;
                }

                if (DismissedThisSession.Contains(markerKey) || HasImportMarker(steamUserDirectory, markerKey))
                {
                    continue;
                }

                if (!ShouldOfferImport(sourceStats, targetStats))
                {
                    continue;
                }

                candidates.Add(new ImportCandidate(
                    SteamUserDirectory: steamUserDirectory,
                    SourceProfile: vanillaProfile,
                    TargetProfile: moddedProfile,
                    SourceStats: sourceStats,
                    TargetStats: targetStats,
                    MarkerKey: markerKey,
                    DisplayName: $"{profileName} ({steamUserDirectory.Name})"));
            }

            ImportCandidate? bestCandidate = candidates
                .OrderByDescending(candidate => candidate.SourceStats.HistoryFileCount)
                .ThenByDescending(candidate => candidate.SourceStats.TotalBytes)
                .FirstOrDefault();

            if (bestCandidate != null)
            {
                return bestCandidate;
            }
        }

        logger.Info("No vanilla progress import candidate found.");
        return null;
    }

    private static bool ShouldOfferImport(ProfileStats sourceStats, ProfileStats targetStats)
    {
        if (!targetStats.Exists || !targetStats.HasMeaningfulData)
        {
            return true;
        }

        if (sourceStats.HistoryFileCount > targetStats.HistoryFileCount)
        {
            return true;
        }

        return targetStats.TotalBytes < sourceStats.TotalBytes / 2;
    }

    private static ImportResult ImportVanillaProfile(ImportCandidate candidate, Sts2Logger logger)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            DirectoryInfo backupRoot = new(Path.Combine(candidate.SteamUserDirectory.FullName, "modded", "_neowtwork_backups"));
            DirectoryInfo backupDirectory = new(Path.Combine(backupRoot.FullName, $"{candidate.TargetProfile.Name}-{timestamp}"));
            DirectoryInfo tempDirectory = new(Path.Combine(candidate.SteamUserDirectory.FullName, "modded", "_neowtwork_tmp", $"{candidate.TargetProfile.Name}-{timestamp}"));

            if (tempDirectory.Exists)
            {
                Directory.Delete(tempDirectory.FullName, recursive: true);
            }

            CopyDirectory(candidate.SourceProfile.FullName, tempDirectory.FullName);

            if (candidate.TargetProfile.Exists)
            {
                Directory.CreateDirectory(backupRoot.FullName);
                CopyDirectory(candidate.TargetProfile.FullName, backupDirectory.FullName);
                logger.Info($"Backed up modded profile to {backupDirectory.FullName}.");
                Directory.Delete(candidate.TargetProfile.FullName, recursive: true);
            }
            else
            {
                logger.Info($"No existing modded profile found at {candidate.TargetProfile.FullName}; backup was not needed.");
            }

            Directory.CreateDirectory(candidate.TargetProfile.Parent!.FullName);
            Directory.Move(tempDirectory.FullName, candidate.TargetProfile.FullName);
            WriteImportMarker(candidate.SteamUserDirectory, candidate.MarkerKey);

            logger.Info(
                $"Imported vanilla progress for {candidate.DisplayName}: " +
                $"{candidate.SourceStats.FileCount} files copied from {candidate.SourceProfile.FullName} to {candidate.TargetProfile.FullName}.");

            return ImportResult.Success(candidate, backupDirectory.Exists ? backupDirectory.FullName : null);
        }
        catch (Exception exception)
        {
            logger.Error($"Vanilla progress import failed for {candidate.DisplayName}: {exception}");
            return ImportResult.Failure(candidate, exception.Message);
        }
    }

    private static void ShowResultDialog(SceneTree sceneTree, ImportResult result)
    {
        AcceptDialog dialog = new()
        {
            Name = "NeowtworkVanillaProgressImportResultDialog",
            Title = result.WasSuccessful ? "Import complete" : "Import failed",
            DialogText = result.WasSuccessful
                ? "Base-game progress was copied into your modded profile.\n\nRestart Slay the Spire 2 if the compendium does not update immediately."
                : $"Neowtwork could not import base-game progress.\n\nNo import was completed.\n\n{result.Message}",
            OkButtonText = "OK",
            DialogAutowrap = true,
            DialogCloseOnEscape = true,
            MinSize = new Vector2I(640, 260)
        };

        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;

        sceneTree.Root.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(680, 280));
    }

    private static IEnumerable<DirectoryInfo> FindSteamUserDirectories()
    {
        DirectoryInfo saveRoot = new(GetSaveRootPath());
        DirectoryInfo steamRoot = new(Path.Combine(saveRoot.FullName, "steam"));

        if (!steamRoot.Exists)
        {
            yield break;
        }

        foreach (DirectoryInfo directory in steamRoot.EnumerateDirectories())
        {
            if (directory.Name.All(char.IsDigit))
            {
                yield return directory;
            }
        }
    }

    private static IEnumerable<DirectoryInfo> FindVanillaProfiles(DirectoryInfo steamUserDirectory)
    {
        return steamUserDirectory
            .EnumerateDirectories("profile*")
            .Where(directory => directory.Name.Length > "profile".Length &&
                                directory.Name["profile".Length..].All(char.IsDigit))
            .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSaveRootPath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "SlayTheSpire2");
        }

        if (OperatingSystem.IsWindows())
        {
            string roamingPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "SlayTheSpire2");

            if (Directory.Exists(roamingPath))
            {
                return roamingPath;
            }

            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "SlayTheSpire2");
        }

        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "SlayTheSpire2");
    }

    private static bool HasImportMarker(DirectoryInfo steamUserDirectory, string markerKey)
    {
        FileInfo markerFile = GetMarkerFile(steamUserDirectory);

        return markerFile.Exists &&
               File.ReadLines(markerFile.FullName).Any(line =>
                   line.Trim().Equals(markerKey, StringComparison.Ordinal) ||
                   line.StartsWith($"{markerKey}|", StringComparison.Ordinal));
    }

    private static void WriteImportMarker(DirectoryInfo steamUserDirectory, string markerKey)
    {
        FileInfo markerFile = GetMarkerFile(steamUserDirectory);
        Directory.CreateDirectory(markerFile.DirectoryName!);
        File.AppendAllLines(markerFile.FullName, [$"{markerKey}|{DateTimeOffset.Now:O}"]);
    }

    private static FileInfo GetMarkerFile(DirectoryInfo steamUserDirectory)
    {
        return new FileInfo(Path.Combine(steamUserDirectory.FullName, "modded", "_neowtwork", MarkerFileName));
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string targetFile = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }

    private sealed record ImportCandidate(
        DirectoryInfo SteamUserDirectory,
        DirectoryInfo SourceProfile,
        DirectoryInfo TargetProfile,
        ProfileStats SourceStats,
        ProfileStats TargetStats,
        string MarkerKey,
        string DisplayName);

    private sealed record ImportResult(bool WasSuccessful, ImportCandidate Candidate, string? Message)
    {
        public static ImportResult Success(ImportCandidate candidate, string? backupPath)
        {
            string message = backupPath == null
                ? "Import completed. No previous modded profile backup was needed."
                : $"Import completed. Backup created at {backupPath}.";

            return new ImportResult(true, candidate, message);
        }

        public static ImportResult Failure(ImportCandidate candidate, string message)
        {
            return new ImportResult(false, candidate, message);
        }
    }

    private sealed record ProfileStats(bool Exists, int FileCount, int HistoryFileCount, long TotalBytes, long ProgressBytes)
    {
        public bool HasMeaningfulData =>
            Exists &&
            (HistoryFileCount > 0 ||
             ProgressBytes >= MinimumMeaningfulProgressBytes ||
             TotalBytes >= MinimumMeaningfulProfileBytes);

        public static ProfileStats FromDirectory(DirectoryInfo directory)
        {
            if (!directory.Exists)
            {
                return new ProfileStats(false, 0, 0, 0, 0);
            }

            FileInfo[] files = directory
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}_neowtwork_backups{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToArray();

            int historyFileCount = files.Count(file =>
                file.DirectoryName?.Split(Path.DirectorySeparatorChar).Contains("history", StringComparer.OrdinalIgnoreCase) == true);
            long progressBytes = files
                .Where(file => file.Name.Equals("progress.save", StringComparison.OrdinalIgnoreCase))
                .Sum(file => file.Length);

            return new ProfileStats(
                Exists: true,
                FileCount: files.Length,
                HistoryFileCount: historyFileCount,
                TotalBytes: files.Sum(file => file.Length),
                ProgressBytes: progressBytes);
        }
    }
}
