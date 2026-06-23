using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using Sts2Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace Neowtwork.NeowtworkCode;

internal static class RunHistoryScanner
{
    public static void ScanAndLog(Sts2Logger logger)
    {
        try
        {
            HistoryScanSummary summary = Scan();

            logger.Info($"History scanner: found {summary.HistoryFolderCount} history folders.");
            logger.Info(
                $"History scanner: loaded {summary.TotalRuns} runs " +
                $"({summary.NormalRuns} normal, {summary.ModdedRuns} modded, {summary.UnknownSourceRuns} unknown source).");
            logger.Info(
                $"History scanner: saw {summary.TotalPlayerEntries} player entries, " +
                $"{summary.MultiplayerRuns} multiplayer runs, {summary.Wins} wins, {summary.Losses} losses, " +
                $"{summary.AbandonedRuns} abandoned runs.");
            logger.Info(
                $"History scanner: indexed {summary.UniqueFinalDeckCards} final-deck cards and " +
                $"{summary.UniqueRelics} relics.");

            if (summary.FailedFiles > 0)
            {
                logger.Warn($"History scanner: skipped {summary.FailedFiles} unreadable run files.");
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"History scanner failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static HistoryScanSummary Scan()
    {
        string userDataDir = OS.GetUserDataDir();
        HistoryScanSummary summary = new(userDataDir);

        if (!Directory.Exists(userDataDir))
        {
            return summary;
        }

        string[] historyFolders = Directory
            .EnumerateDirectories(userDataDir, "history", SearchOption.AllDirectories)
            .Where(IsRunHistoryFolder)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        summary.HistoryFolderCount = historyFolders.Length;

        HashSet<string> seenContentHashes = [];
        HashSet<string> uniqueFinalDeckCards = [];
        HashSet<string> uniqueRelics = [];

        foreach (string historyFolder in historyFolders)
        {
            HistorySourceKind sourceKind = GetHistorySourceKind(historyFolder);

            foreach (string runFile in Directory.EnumerateFiles(historyFolder, "*.run", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(runFile);
                    string contentHash = Convert.ToHexString(SHA256.HashData(bytes));

                    if (!seenContentHashes.Add(contentHash))
                    {
                        summary.DuplicateFilesSkipped++;
                        continue;
                    }

                    using JsonDocument document = JsonDocument.Parse(bytes);
                    JsonElement root = document.RootElement;

                    summary.TotalRuns++;
                    summary.AddSourceRun(sourceKind);

                    bool won = TryGetBoolean(root, "win", out bool win) && win;
                    bool abandoned = TryGetBoolean(root, "was_abandoned", out bool wasAbandoned) && wasAbandoned;

                    if (won)
                    {
                        summary.Wins++;
                    }
                    else if (abandoned)
                    {
                        summary.AbandonedRuns++;
                    }
                    else
                    {
                        summary.Losses++;
                    }

                    if (TryGetArray(root, "players", out JsonElement players))
                    {
                        int playerCount = players.GetArrayLength();
                        summary.TotalPlayerEntries += playerCount;

                        if (playerCount > 1)
                        {
                            summary.MultiplayerRuns++;
                        }

                        foreach (JsonElement player in players.EnumerateArray())
                        {
                            AddPlayerDeckCards(player, uniqueFinalDeckCards);
                            AddPlayerRelics(player, uniqueRelics);
                        }
                    }
                }
                catch
                {
                    summary.FailedFiles++;
                }
            }
        }

        summary.UniqueFinalDeckCards = uniqueFinalDeckCards.Count;
        summary.UniqueRelics = uniqueRelics.Count;

        return summary;
    }

    private static bool IsRunHistoryFolder(string path)
    {
        string normalized = NormalizePath(path);
        return normalized.EndsWith("/saves/history", StringComparison.Ordinal);
    }

    private static HistorySourceKind GetHistorySourceKind(string path)
    {
        string normalized = NormalizePath(path);

        if (normalized.Contains("/modded/profile", StringComparison.Ordinal))
        {
            return HistorySourceKind.Modded;
        }

        if (normalized.Contains("/profile", StringComparison.Ordinal))
        {
            return HistorySourceKind.Normal;
        }

        return HistorySourceKind.Unknown;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static void AddPlayerDeckCards(JsonElement player, HashSet<string> uniqueFinalDeckCards)
    {
        if (!TryGetArray(player, "deck", out JsonElement deck))
        {
            return;
        }

        foreach (JsonElement card in deck.EnumerateArray())
        {
            if (TryGetString(card, "id", out string? cardId) && cardId is not null)
            {
                uniqueFinalDeckCards.Add(cardId);
            }
        }
    }

    private static void AddPlayerRelics(JsonElement player, HashSet<string> uniqueRelics)
    {
        if (!TryGetArray(player, "relics", out JsonElement relics))
        {
            return;
        }

        foreach (JsonElement relic in relics.EnumerateArray())
        {
            if (TryGetString(relic, "id", out string? relicId) && relicId is not null)
            {
                uniqueRelics.Add(relicId);
            }
        }
    }

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out array) &&
            array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement property) &&
            (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            value = property.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private enum HistorySourceKind
    {
        Normal,
        Modded,
        Unknown
    }

    private sealed class HistoryScanSummary(string userDataDir)
    {
        public string UserDataDir { get; } = userDataDir;
        public int HistoryFolderCount { get; set; }
        public int TotalRuns { get; set; }
        public int NormalRuns { get; set; }
        public int ModdedRuns { get; set; }
        public int UnknownSourceRuns { get; set; }
        public int MultiplayerRuns { get; set; }
        public int TotalPlayerEntries { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int AbandonedRuns { get; set; }
        public int UniqueFinalDeckCards { get; set; }
        public int UniqueRelics { get; set; }
        public int FailedFiles { get; set; }
        public int DuplicateFilesSkipped { get; set; }

        public void AddSourceRun(HistorySourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case HistorySourceKind.Normal:
                    NormalRuns++;
                    break;
                case HistorySourceKind.Modded:
                    ModdedRuns++;
                    break;
                default:
                    UnknownSourceRuns++;
                    break;
            }
        }
    }
}
