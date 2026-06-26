using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

    public static void TryAutoSync(Sts2Logger logger)
    {
        if (!NeowtworkConfig.KeepBaseGameAndModdedProgressInSync)
        {
            return;
        }

        try
        {
            SyncResult result = SyncAllProfiles(logger);
            logger.Info($"Auto progress sync complete: {result.SummaryForLog()}");
        }
        catch (Exception exception)
        {
            logger.Error($"Auto progress sync failed: {exception}");
        }
    }

    public static string GetSyncStatusText()
    {
        try
        {
            SyncPlan plan = BuildSyncPlan();
            if (plan.Pairs.Count == 0)
            {
                return "[b]Progress sync[/b]\n\nNo vanilla/modded profile pairs were found.";
            }

            return "[b]Progress sync[/b]\n\n" +
                   $"Setting: {(NeowtworkConfig.KeepBaseGameAndModdedProgressInSync ? "enabled" : "off")}\n" +
                   $"Profile pairs found: {plan.Pairs.Count}\n" +
                   $"Files to copy vanilla → modded: {plan.TotalVanillaToModded}\n" +
                   $"Files to copy modded → vanilla: {plan.TotalModdedToVanilla}\n" +
                   $"Conflicts needing review: {plan.TotalConflicts}\n\n" +
                   "Sync preserves unique files from both sides and creates backups before writing.\n" +
                   "Steam Cloud is not controlled by Neowtwork; if Steam shows a Cloud Conflict, review it carefully.";
        }
        catch (Exception exception)
        {
            return "[b]Progress sync[/b]\n\n" +
                   $"Could not read sync status.\n\n{exception.Message}";
        }
    }

    public static void ShowManualSyncDialog(Sts2Logger logger)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree sceneTree)
            {
                logger.Info("Skipping manual progress sync dialog: Godot scene tree is not available.");
                return;
            }

            SyncPlan plan = BuildSyncPlan();
            if (plan.Pairs.Count == 0)
            {
                ShowNoSyncCandidateDialog(sceneTree);
                return;
            }

            ConfirmationDialog dialog = new()
            {
                Name = "NeowtworkProgressSyncDialog",
                Title = "Sync base-game and modded progress?",
                DialogText = BuildSyncConfirmationText(plan),
                OkButtonText = "Sync",
                CancelButtonText = "Cancel",
                DialogAutowrap = true,
                DialogCloseOnEscape = true,
                MinSize = new Vector2I(860, 560)
            };

            dialog.Confirmed += () =>
            {
                SyncResult result = SyncAllProfiles(logger);
                ShowSyncResultDialog(sceneTree, result);
                dialog.QueueFree();
            };

            dialog.Canceled += dialog.QueueFree;
            sceneTree.Root.AddChild(dialog);
            dialog.PopupCentered(new Vector2I(900, 580));
        }
        catch (Exception exception)
        {
            logger.Error($"Failed while creating progress sync dialog: {exception}");
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

    private static SyncPlan BuildSyncPlan()
    {
        List<SyncProfilePair> pairs = [];

        foreach (DirectoryInfo steamUserDirectory in FindSteamUserDirectories())
        {
            DirectoryInfo moddedDirectory = new(Path.Combine(steamUserDirectory.FullName, "modded"));
            Dictionary<string, DirectoryInfo> profiles = new(StringComparer.OrdinalIgnoreCase);

            foreach (DirectoryInfo vanillaProfile in FindVanillaProfiles(steamUserDirectory))
            {
                profiles[vanillaProfile.Name] = vanillaProfile;
            }

            if (moddedDirectory.Exists)
            {
                foreach (DirectoryInfo moddedProfile in FindModdedProfiles(moddedDirectory))
                {
                    profiles.TryAdd(moddedProfile.Name, new DirectoryInfo(Path.Combine(steamUserDirectory.FullName, moddedProfile.Name)));
                }
            }

            foreach ((string profileName, DirectoryInfo vanillaProfile) in profiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                DirectoryInfo moddedProfile = new(Path.Combine(moddedDirectory.FullName, profileName));
                SyncProfilePair pair = AnalyzeSyncPair(steamUserDirectory, vanillaProfile, moddedProfile);
                if (pair.HasAnyData)
                {
                    pairs.Add(pair);
                }
            }
        }

        return new SyncPlan(pairs);
    }

    private static SyncProfilePair AnalyzeSyncPair(
        DirectoryInfo steamUserDirectory,
        DirectoryInfo vanillaProfile,
        DirectoryInfo moddedProfile)
    {
        Dictionary<string, FileSnapshot> vanillaFiles = SnapshotFiles(vanillaProfile);
        Dictionary<string, FileSnapshot> moddedFiles = SnapshotFiles(moddedProfile);
        List<SyncFileAction> actions = [];
        List<string> conflicts = [];

        foreach (string relativePath in vanillaFiles.Keys.Union(moddedFiles.Keys, StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            bool hasVanilla = vanillaFiles.TryGetValue(relativePath, out FileSnapshot? vanillaFile);
            bool hasModded = moddedFiles.TryGetValue(relativePath, out FileSnapshot? moddedFile);

            if (hasVanilla && !hasModded)
            {
                actions.Add(SyncFileAction.CopyVanillaToModded(vanillaFile!));
                continue;
            }

            if (!hasVanilla && hasModded)
            {
                actions.Add(SyncFileAction.CopyModdedToVanilla(moddedFile!));
                continue;
            }

            if (!hasVanilla || !hasModded || vanillaFile is null || moddedFile is null || vanillaFile.IsSameContentAs(moddedFile))
            {
                continue;
            }

            if (vanillaFile.LastWriteUtc > moddedFile.LastWriteUtc)
            {
                actions.Add(SyncFileAction.CopyVanillaToModded(vanillaFile));
            }
            else if (moddedFile.LastWriteUtc > vanillaFile.LastWriteUtc)
            {
                actions.Add(SyncFileAction.CopyModdedToVanilla(moddedFile));
            }
            else
            {
                conflicts.Add(relativePath);
            }
        }

        return new SyncProfilePair(
            SteamUserDirectory: steamUserDirectory,
            VanillaProfile: vanillaProfile,
            ModdedProfile: moddedProfile,
            VanillaStats: ProfileStats.FromDirectory(vanillaProfile),
            ModdedStats: ProfileStats.FromDirectory(moddedProfile),
            Actions: actions,
            Conflicts: conflicts);
    }

    private static SyncResult SyncAllProfiles(Sts2Logger logger)
    {
        SyncPlan plan = BuildSyncPlan();
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        List<string> backupPaths = [];
        int copiedVanillaToModded = 0;
        int copiedModdedToVanilla = 0;

        foreach (SyncProfilePair pair in plan.Pairs)
        {
            if (pair.Actions.Count == 0)
            {
                continue;
            }

            DirectoryInfo backupRoot = new(Path.Combine(pair.SteamUserDirectory.FullName, "modded", "_neowtwork_backups", $"sync-{pair.VanillaProfile.Name}-{timestamp}"));
            if (pair.VanillaProfile.Exists)
            {
                CopyDirectory(pair.VanillaProfile.FullName, Path.Combine(backupRoot.FullName, "vanilla"));
            }

            if (pair.ModdedProfile.Exists)
            {
                CopyDirectory(pair.ModdedProfile.FullName, Path.Combine(backupRoot.FullName, "modded"));
            }

            backupPaths.Add(backupRoot.FullName);

            foreach (SyncFileAction action in pair.Actions)
            {
                if (action.Direction == SyncDirection.VanillaToModded)
                {
                    CopyFilePreservingTime(action.Source.FullPath, Path.Combine(pair.ModdedProfile.FullName, action.Source.RelativePath));
                    copiedVanillaToModded++;
                }
                else
                {
                    CopyFilePreservingTime(action.Source.FullPath, Path.Combine(pair.VanillaProfile.FullName, action.Source.RelativePath));
                    copiedModdedToVanilla++;
                }
            }

            logger.Info(
                $"Synced {pair.DisplayName}: vanillaToModded={pair.VanillaToModdedCount}, moddedToVanilla={pair.ModdedToVanillaCount}, conflicts={pair.Conflicts.Count}.");
        }

        return new SyncResult(plan, copiedVanillaToModded, copiedModdedToVanilla, backupPaths);
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

    private static void ShowNoSyncCandidateDialog(SceneTree sceneTree)
    {
        AcceptDialog dialog = new()
        {
            Name = "NeowtworkProgressSyncNoCandidateDialog",
            Title = "No progress profiles found",
            DialogText =
                "Neowtwork could not find vanilla or modded profile folders to sync.\n\n" +
                "Launch Slay the Spire 2 once, then return here and refresh.",
            OkButtonText = "OK",
            DialogAutowrap = true,
            DialogCloseOnEscape = true,
            MinSize = new Vector2I(680, 260)
        };

        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;

        sceneTree.Root.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(720, 300));
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

    private static void ShowSyncResultDialog(SceneTree sceneTree, SyncResult result)
    {
        AcceptDialog dialog = new()
        {
            Name = "NeowtworkProgressSyncResultDialog",
            Title = result.Plan.TotalConflicts == 0 ? "Progress sync complete" : "Progress sync completed with conflicts",
            DialogText =
                $"Copied vanilla → modded: {result.CopiedVanillaToModded}\n" +
                $"Copied modded → vanilla: {result.CopiedModdedToVanilla}\n" +
                $"Conflicts skipped: {result.Plan.TotalConflicts}\n\n" +
                $"Backups created: {result.BackupPaths.Count}\n\n" +
                "Restart Slay the Spire 2 if the compendium or run history does not update immediately.\n\n" +
                "If Steam shows a Cloud Conflict, review it carefully. Neowtwork syncs local files only.",
            OkButtonText = "OK",
            DialogAutowrap = true,
            DialogCloseOnEscape = true,
            MinSize = new Vector2I(720, 360)
        };

        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;

        sceneTree.Root.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(760, 400));
    }

    private static string BuildSyncConfirmationText(SyncPlan plan)
    {
        return
            "Neowtwork can keep your base-game and modded local progress folders aligned.\n\n" +
            $"Profile pairs: {plan.Pairs.Count}\n" +
            $"Vanilla → modded files: {plan.TotalVanillaToModded}\n" +
            $"Modded → vanilla files: {plan.TotalModdedToVanilla}\n" +
            $"Conflicts skipped: {plan.TotalConflicts}\n\n" +
            "Neowtwork will:\n" +
            "- copy missing/newer files in both directions\n" +
            "- create timestamped backups before writing\n" +
            "- preserve unique run-history files from both sides\n" +
            "- skip same-time conflicting files instead of guessing\n" +
            "- never control Steam Cloud directly\n\n" +
            "This is intended for keeping modded and unmodded local progress in lockstep after you opt in.";
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

    private static IEnumerable<DirectoryInfo> FindModdedProfiles(DirectoryInfo moddedDirectory)
    {
        if (!moddedDirectory.Exists)
        {
            yield break;
        }

        foreach (DirectoryInfo directory in moddedDirectory.EnumerateDirectories("profile*"))
        {
            if (directory.Name.Length > "profile".Length &&
                directory.Name["profile".Length..].All(char.IsDigit))
            {
                yield return directory;
            }
        }
    }

    internal static string GetSaveRootPath()
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

    private sealed record SyncPlan(List<SyncProfilePair> Pairs)
    {
        public int TotalVanillaToModded => Pairs.Sum(pair => pair.VanillaToModdedCount);
        public int TotalModdedToVanilla => Pairs.Sum(pair => pair.ModdedToVanillaCount);
        public int TotalConflicts => Pairs.Sum(pair => pair.Conflicts.Count);
    }

    private sealed record SyncProfilePair(
        DirectoryInfo SteamUserDirectory,
        DirectoryInfo VanillaProfile,
        DirectoryInfo ModdedProfile,
        ProfileStats VanillaStats,
        ProfileStats ModdedStats,
        List<SyncFileAction> Actions,
        List<string> Conflicts)
    {
        public string DisplayName => $"{VanillaProfile.Name} ({SteamUserDirectory.Name})";
        public bool HasAnyData => VanillaStats.Exists || ModdedStats.Exists;
        public int VanillaToModdedCount => Actions.Count(action => action.Direction == SyncDirection.VanillaToModded);
        public int ModdedToVanillaCount => Actions.Count(action => action.Direction == SyncDirection.ModdedToVanilla);
    }

    private sealed record SyncResult(
        SyncPlan Plan,
        int CopiedVanillaToModded,
        int CopiedModdedToVanilla,
        List<string> BackupPaths)
    {
        public string SummaryForLog()
        {
            return $"pairs={Plan.Pairs.Count}, vanillaToModded={CopiedVanillaToModded}, moddedToVanilla={CopiedModdedToVanilla}, conflicts={Plan.TotalConflicts}, backups={BackupPaths.Count}";
        }
    }

    private enum SyncDirection
    {
        VanillaToModded,
        ModdedToVanilla
    }

    private sealed record SyncFileAction(SyncDirection Direction, FileSnapshot Source)
    {
        public static SyncFileAction CopyVanillaToModded(FileSnapshot source)
        {
            return new SyncFileAction(SyncDirection.VanillaToModded, source);
        }

        public static SyncFileAction CopyModdedToVanilla(FileSnapshot source)
        {
            return new SyncFileAction(SyncDirection.ModdedToVanilla, source);
        }
    }

    private sealed record FileSnapshot(
        string RootPath,
        string FullPath,
        string RelativePath,
        long Length,
        DateTime LastWriteUtc,
        string Hash)
    {
        public bool IsSameContentAs(FileSnapshot other)
        {
            return Length == other.Length && Hash.Equals(other.Hash, StringComparison.Ordinal);
        }

        public static FileSnapshot FromFile(string rootPath, FileInfo file, string relativePath)
        {
            using FileStream stream = file.OpenRead();
            string hash = Convert.ToHexString(SHA256.HashData(stream));

            return new FileSnapshot(
                RootPath: rootPath,
                FullPath: file.FullName,
                RelativePath: relativePath,
                Length: file.Length,
                LastWriteUtc: file.LastWriteTimeUtc,
                Hash: hash);
        }
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

    private static void CopyFilePreservingTime(string sourceFile, string targetFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
        File.Copy(sourceFile, targetFile, overwrite: true);
        File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
    }

    private static Dictionary<string, FileSnapshot> SnapshotFiles(DirectoryInfo directory)
    {
        Dictionary<string, FileSnapshot> snapshots = new(StringComparer.Ordinal);
        if (!directory.Exists)
        {
            return snapshots;
        }

        foreach (FileInfo file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(directory.FullName, file.FullName)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            if (ShouldExcludeSyncPath(relativePath))
            {
                continue;
            }

            snapshots[relativePath] = FileSnapshot.FromFile(directory.FullName, file, relativePath);
        }

        return snapshots;
    }

    private static bool ShouldExcludeSyncPath(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        return normalized.StartsWith("_neowtwork/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("_neowtwork_backups/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("_neowtwork_tmp/", StringComparison.OrdinalIgnoreCase);
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
