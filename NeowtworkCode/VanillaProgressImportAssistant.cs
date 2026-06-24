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

    public static string GetImportStatusText()
    {
        try
        {
            ImportScan scan = ScanImportCandidates();
            if (scan.SteamUsers.Count == 0)
            {
                return "[b]Base-game progress import[/b]\n\n" +
                       $"No Slay the Spire 2 Steam save folders were found under:\n{scan.SaveRootPath}\n\n" +
                       "If your progress is missing, launch the unmodded game once and make sure local save data exists.";
            }

            ImportCandidate? bestManualCandidate = SelectBestCandidate(scan, ImportMode.Manual);
            string summary = "[b]Base-game progress import[/b]\n\n" +
                             $"Save root:\n{scan.SaveRootPath}\n\n" +
                             $"Steam save folders found: {scan.SteamUsers.Count}\n" +
                             $"Vanilla profiles found: {scan.VanillaProfileCount}\n";

            if (bestManualCandidate == null)
            {
                return summary +
                       "\nNo meaningful vanilla profile was found to import.\n" +
                       "If this looks wrong, launch the unmodded game once, then return here and refresh.";
            }

            return summary +
                   $"\nReady to import: {bestManualCandidate.DisplayName}\n" +
                   $"Vanilla: {FormatProfileStats(bestManualCandidate.SourceStats)}\n" +
                   $"Modded: {FormatProfileStats(bestManualCandidate.TargetStats)}\n\n" +
                   "Use Import Base Game Progress to copy vanilla progress into modded after creating a backup.";
        }
        catch (Exception exception)
        {
            return "[b]Base-game progress import[/b]\n\n" +
                   $"Could not read save status.\n\n{exception.Message}";
        }
    }

    public static void ShowManualImportDialog(Sts2Logger logger)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree sceneTree)
            {
                logger.Info("Skipping manual vanilla progress import dialog: Godot scene tree is not available.");
                return;
            }

            ImportScan scan = ScanImportCandidates(logger);
            ImportCandidate? candidate = SelectBestCandidate(scan, ImportMode.Manual);
            if (candidate == null)
            {
                ShowNoCandidateDialog(sceneTree, scan);
                return;
            }

            ShowImportConfirmationDialog(sceneTree, candidate, logger, ImportMode.Manual);
        }
        catch (Exception exception)
        {
            logger.Error($"Failed while creating manual vanilla progress import dialog: {exception}");
        }
    }

    private static void TryCreateDialog(SceneTree sceneTree, Sts2Logger logger)
    {
        try
        {
            ImportCandidate? candidate = FindBestImportCandidate(logger, ImportMode.Auto);
            if (candidate == null)
            {
                return;
            }

            if (sceneTree.Root.GetNodeOrNull<ConfirmationDialog>(DialogName) != null)
            {
                return;
            }

            ShowImportConfirmationDialog(sceneTree, candidate, logger, ImportMode.Auto);
        }
        catch (Exception exception)
        {
            logger.Error($"Failed while creating vanilla progress import prompt: {exception}");
        }
    }

    private static ImportCandidate? FindBestImportCandidate(Sts2Logger logger, ImportMode mode)
    {
        return SelectBestCandidate(ScanImportCandidates(logger), mode);
    }

    private static ImportScan ScanImportCandidates(Sts2Logger? logger = null)
    {
        string saveRootPath = GetSaveRootPath();
        List<DirectoryInfo> steamUsers = [];
        List<ImportCandidate> candidates = [];
        int vanillaProfileCount = 0;

        foreach (DirectoryInfo steamUserDirectory in FindSteamUserDirectories())
        {
            steamUsers.Add(steamUserDirectory);
            DirectoryInfo moddedDirectory = new(Path.Combine(steamUserDirectory.FullName, "modded"));

            foreach (DirectoryInfo vanillaProfile in FindVanillaProfiles(steamUserDirectory))
            {
                vanillaProfileCount++;
                string profileName = vanillaProfile.Name;
                DirectoryInfo moddedProfile = new(Path.Combine(moddedDirectory.FullName, profileName));
                ProfileStats sourceStats = ProfileStats.FromDirectory(vanillaProfile);
                ProfileStats targetStats = ProfileStats.FromDirectory(moddedProfile);
                string markerKey = $"{steamUserDirectory.Name}/{profileName}";

                if (!sourceStats.HasMeaningfulData)
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
        }

        logger?.Info(
            $"Scanned vanilla progress import candidates: saveRoot={saveRootPath}, " +
            $"steamUsers={steamUsers.Count}, vanillaProfiles={vanillaProfileCount}, meaningfulCandidates={candidates.Count}.");

        return new ImportScan(saveRootPath, steamUsers, candidates, vanillaProfileCount);
    }

    private static ImportCandidate? SelectBestCandidate(ImportScan scan, ImportMode mode)
    {
        IEnumerable<ImportCandidate> candidates = scan.Candidates;

        if (mode == ImportMode.Auto)
        {
            candidates = candidates.Where(candidate =>
                !DismissedThisSession.Contains(candidate.MarkerKey) &&
                !HasImportMarker(candidate.SteamUserDirectory, candidate.MarkerKey) &&
                ShouldOfferImport(candidate.SourceStats, candidate.TargetStats));
        }

        ImportCandidate? bestCandidate = candidates
            .OrderByDescending(candidate => candidate.SourceStats.HistoryFileCount)
            .ThenByDescending(candidate => candidate.SourceStats.TotalBytes)
            .FirstOrDefault();

        if (bestCandidate == null && mode == ImportMode.Auto)
        {
            MainFile.Logger.Info("No vanilla progress import candidate found.");
        }

        return bestCandidate;
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

    private static void ShowImportConfirmationDialog(
        SceneTree sceneTree,
        ImportCandidate candidate,
        Sts2Logger logger,
        ImportMode mode)
    {
        ConfirmationDialog dialog = new()
        {
            Name = DialogName,
            Title = "Import base-game data?",
            DialogText = BuildConfirmationText(candidate, mode),
            OkButtonText = "Import",
            CancelButtonText = mode == ImportMode.Auto ? "Not Now" : "Cancel",
            DialogAutowrap = true,
            DialogCloseOnEscape = true,
            MinSize = new Vector2I(820, 520)
        };

        dialog.Confirmed += () =>
        {
            ImportResult result = ImportVanillaProfile(candidate, logger);
            ShowResultDialog(sceneTree, result);
            dialog.QueueFree();
        };

        dialog.Canceled += () =>
        {
            if (mode == ImportMode.Auto)
            {
                DismissedThisSession.Add(candidate.MarkerKey);
            }

            logger.Info($"Vanilla progress import skipped for {candidate.DisplayName}.");
            dialog.QueueFree();
        };

        sceneTree.Root.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(860, 540));

        logger.Info(
            $"Offering {mode.ToString().ToLowerInvariant()} vanilla progress import for {candidate.DisplayName}: " +
            $"vanilla={FormatProfileStats(candidate.SourceStats)}, modded={FormatProfileStats(candidate.TargetStats)}.");
    }

    private static string BuildConfirmationText(ImportCandidate candidate, ImportMode mode)
    {
        string firstLine = mode == ImportMode.Auto
            ? "Your modded profile appears to be missing base-game progress."
            : "Neowtwork can copy your base-game progress into your modded profile.";

        string replacementWarning = candidate.TargetStats.HasMeaningfulData
            ? "\n\nYour current modded profile already has data. Neowtwork will back it up first, then replace it with the base-game profile."
            : string.Empty;

        return
            $"{firstLine}\n\n" +
            $"Profile: {candidate.DisplayName}\n" +
            $"Vanilla: {FormatProfileStats(candidate.SourceStats)}\n" +
            $"Modded: {FormatProfileStats(candidate.TargetStats)}" +
            replacementWarning +
            "\n\nNeowtwork will:\n" +
            "- copy vanilla/base-game progress and run history into the matching modded profile\n" +
            "- create a timestamped backup of the current modded profile first\n" +
            "- never modify vanilla/base-game saves\n" +
            "- never control Steam Cloud directly\n\n" +
            "If Steam later shows a Cloud Conflict after this intentional import, choose the local save if you want to keep the imported modded progress.";
    }

    private static void ShowNoCandidateDialog(SceneTree sceneTree, ImportScan scan)
    {
        AcceptDialog dialog = new()
        {
            Name = "NeowtworkVanillaProgressImportNoCandidateDialog",
            Title = "No base-game data found",
            DialogText =
                "Neowtwork could not find a meaningful vanilla/base-game profile to import.\n\n" +
                $"Save root checked:\n{scan.SaveRootPath}\n\n" +
                $"Steam save folders found: {scan.SteamUsers.Count}\n" +
                $"Vanilla profiles found: {scan.VanillaProfileCount}\n\n" +
                "Try launching the unmodded game once, then return here and refresh.",
            OkButtonText = "OK",
            DialogAutowrap = true,
            DialogCloseOnEscape = true,
            MinSize = new Vector2I(720, 360)
        };

        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;

        sceneTree.Root.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(760, 380));
    }

    private static void ShowResultDialog(SceneTree sceneTree, ImportResult result)
    {
        AcceptDialog dialog = new()
        {
            Name = "NeowtworkVanillaProgressImportResultDialog",
            Title = result.WasSuccessful ? "Import complete" : "Import failed",
            DialogText = result.WasSuccessful
                ? "Base-game progress was copied into your modded profile.\n\nRestart Slay the Spire 2 if the compendium does not update immediately.\n\nIf Steam shows a Cloud Conflict after this import, choose the local save if you want to keep the imported modded progress."
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

    private static string FormatProfileStats(ProfileStats stats)
    {
        if (!stats.Exists)
        {
            return "not found";
        }

        return $"{stats.FileCount} files, {stats.HistoryFileCount} history runs, {stats.TotalBytes / 1024} KB";
    }

    private enum ImportMode
    {
        Auto,
        Manual
    }

    private sealed record ImportScan(
        string SaveRootPath,
        List<DirectoryInfo> SteamUsers,
        List<ImportCandidate> Candidates,
        int VanillaProfileCount);

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
