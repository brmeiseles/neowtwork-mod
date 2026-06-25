using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;

namespace Neowtwork.NeowtworkCode;

internal static class RunHistoryAnalytics
{
    private static RunHistoryIndex? CachedIndex;

    public static RunHistoryIndex GetIndex()
    {
        return CachedIndex ??= BuildIndex();
    }

    public static RunHistoryIndex Refresh()
    {
        CachedIndex = BuildIndex();
        return CachedIndex;
    }

    public static MetricSummary? GetRelicStats(string relicId)
    {
        return GetIndex().Relics.TryGetValue(relicId, out MetricSummary? stats) ? stats : null;
    }

    public static MetricSummary? GetEventChoiceStats(string table, string key)
    {
        return GetIndex().EventChoices.TryGetValue(EventChoiceKey(table, key), out MetricSummary? stats) ? stats : null;
    }

    public static string BuildDashboardText()
    {
        RunHistoryIndex index = GetIndex();
        StringBuilder builder = new();

        builder.AppendLine("Run History Analytics");
        builder.AppendLine();
        builder.AppendLine($"Runs indexed: {index.TotalRuns} ({index.Wins} wins, {index.Losses} losses, {FormatPercent(index.WinRate)} win rate)");
        builder.AppendLine($"Players indexed: {index.PlayerEntries}");
        builder.AppendLine($"History folders: {index.HistoryFolderCount}; duplicate run files skipped: {index.DuplicateFilesSkipped}; unreadable files skipped: {index.FailedFiles}");
        builder.AppendLine();

        AppendSection(builder, "Characters", index.Characters
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(8)
            .Select(pair => $"{PrettyId(pair.Key)}: {pair.Value.Count} runs, {FormatPercent(pair.Value.WinRate)} WR"));

        AppendSection(builder, "Best Cards by Final Deck Win Rate", index.FinalDeckCards
            .Where(pair => pair.Value.Count >= 5)
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Wins)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyId(pair.Key)}: {FormatPercent(pair.Value.WinRate)} WR ({pair.Value.Wins}-{pair.Value.Losses})"));

        AppendSection(builder, "Most-Picked Card Rewards", index.CardChoices
            .Where(pair => pair.Value.Seen > 0)
            .OrderByDescending(pair => pair.Value.PickRate)
            .ThenByDescending(pair => pair.Value.Picked)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyId(pair.Key)}: {FormatPercent(pair.Value.PickRate)} pick rate ({pair.Value.Picked}/{pair.Value.Seen})"));

        AppendSection(builder, "Relics in Winning Decks", index.Relics
            .Where(pair => pair.Value.Count >= 3)
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Wins)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyId(pair.Key)}: {FormatPercent(pair.Value.WinRate)} WR ({pair.Value.Wins}-{pair.Value.Losses})"));

        AppendSection(builder, "Event Choices", index.EventChoices
            .Where(pair => pair.Value.Count >= 2)
            .OrderByDescending(pair => pair.Value.Count)
            .ThenByDescending(pair => pair.Value.WinRate)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyEventChoice(pair.Key)}: chosen {pair.Value.Count} times, {FormatPercent(pair.Value.WinRate)} WR"));

        AppendSection(builder, "Monster Trouble", index.DeathMonsters
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyId(pair.Key)}: present in {pair.Value.Count} death encounters"));

        AppendSection(builder, "Pathing Snapshot", index.RoomTypes
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(10)
            .Select(pair => $"{PrettyId(pair.Key)}: {pair.Value.Count} rooms, {FormatPercent(pair.Value.WinRate)} run WR when visited"));

        AppendSection(builder, "Shop Snapshot", BuildShopLines(index));

        AppendSection(builder, "Card/Relic Combos in Winning Runs", index.CardRelicCombos
            .Where(pair => pair.Value.Count >= 3)
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyCombo(pair.Key)}: {pair.Value.Count} winning runs"));

        return builder.ToString().TrimEnd();
    }

    private static RunHistoryIndex BuildIndex()
    {
        string scanRoot = Directory.Exists(VanillaProgressImportAssistant.GetSaveRootPath())
            ? VanillaProgressImportAssistant.GetSaveRootPath()
            : OS.GetUserDataDir();
        RunHistoryIndex index = new(scanRoot);

        if (!Directory.Exists(index.UserDataDir))
        {
            return index;
        }

        string[] historyFolders = Directory
            .EnumerateDirectories(index.UserDataDir, "history", SearchOption.AllDirectories)
            .Where(IsRunHistoryFolder)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        index.HistoryFolderCount = historyFolders.Length;
        HashSet<string> seenContentHashes = [];

        foreach (string historyFolder in historyFolders)
        {
            foreach (string runFile in Directory.EnumerateFiles(historyFolder, "*.run", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(runFile);
                    string contentHash = Convert.ToHexString(SHA256.HashData(bytes));

                    if (!seenContentHashes.Add(contentHash))
                    {
                        index.DuplicateFilesSkipped++;
                        continue;
                    }

                    using JsonDocument document = JsonDocument.Parse(bytes);
                    ParseRun(index, document.RootElement);
                }
                catch
                {
                    index.FailedFiles++;
                }
            }
        }

        return index;
    }

    private static void ParseRun(RunHistoryIndex index, JsonElement root)
    {
        bool won = TryGetBoolean(root, "win", out bool win) && win;
        bool abandoned = TryGetBoolean(root, "was_abandoned", out bool wasAbandoned) && wasAbandoned;
        bool countedAsLoss = !won && !abandoned;

        index.TotalRuns++;
        if (won)
        {
            index.Wins++;
        }
        else if (countedAsLoss)
        {
            index.Losses++;
        }
        else
        {
            index.AbandonedRuns++;
        }

        HashSet<string> runFinalCardIds = [];
        HashSet<string> runRelicIds = [];
        HashSet<string> lastEncounterMonsterIds = [];
        Dictionary<string, int> roomCounts = [];
        decimal runShopGoldSpent = 0;
        int runBoughtRelics = 0;
        int runBoughtPotions = 0;
        int runBoughtColorless = 0;
        int runShopCardsGained = 0;

        if (TryGetArray(root, "players", out JsonElement players))
        {
            index.PlayerEntries += players.GetArrayLength();

            foreach (JsonElement player in players.EnumerateArray())
            {
                string character = TryGetString(player, "character", out string? characterId) ? characterId! : "Unknown";
                index.Characters.GetOrAdd(character).AddResult(won, countedAsLoss);

                foreach (string cardId in EnumerateIds(player, "deck"))
                {
                    runFinalCardIds.Add(cardId);
                    index.FinalDeckCards.GetOrAdd(cardId).AddResult(won, countedAsLoss);
                }

                foreach (string relicId in EnumerateIds(player, "relics"))
                {
                    runRelicIds.Add(relicId);
                    index.Relics.GetOrAdd(relicId).AddResult(won, countedAsLoss);
                }
            }
        }

        if (TryGetArray(root, "map_point_history", out JsonElement mapPointHistory))
        {
            foreach (JsonElement mapPoint in EnumerateMapPoints(mapPointHistory))
            {
                if (!TryGetArray(mapPoint, "rooms", out JsonElement rooms))
                {
                    continue;
                }

                bool isShopMapPoint = false;

                foreach (JsonElement room in rooms.EnumerateArray())
                {
                    string roomType = TryGetString(room, "room_type", out string? rawRoomType) ? rawRoomType! : "unknown";
                    roomCounts[roomType] = roomCounts.GetValueOrDefault(roomType) + 1;
                    isShopMapPoint |= string.Equals(roomType, "shop", StringComparison.OrdinalIgnoreCase);

                    HashSet<string> roomMonsterIds = EnumerateStringArray(room, "monster_ids").ToHashSet(StringComparer.Ordinal);
                    if (roomMonsterIds.Count > 0)
                    {
                        lastEncounterMonsterIds = roomMonsterIds;
                    }
                }

                if (TryGetArray(mapPoint, "player_stats", out JsonElement playerStats))
                {
                    foreach (JsonElement stats in playerStats.EnumerateArray())
                    {
                        ParseCardChoices(index, stats, won, countedAsLoss);
                        ParseEventChoices(index, stats, won, countedAsLoss);

                        if (isShopMapPoint)
                        {
                            runShopGoldSpent += TryGetDecimal(stats, "gold_spent", out decimal goldSpent) ? goldSpent : 0;
                            runBoughtRelics += CountArray(stats, "bought_relics");
                            runBoughtPotions += CountArray(stats, "bought_potions");
                            runBoughtColorless += CountArray(stats, "bought_colorless");
                            runShopCardsGained += CountArray(stats, "cards_gained");
                        }
                    }
                }
            }
        }

        foreach ((string roomType, int count) in roomCounts)
        {
            index.RoomTypes.GetOrAdd(roomType).AddWeightedResult(count, won, countedAsLoss);
        }

        if (won)
        {
            foreach (string comboKey in BuildCardRelicCombos(runFinalCardIds, runRelicIds))
            {
                index.CardRelicCombos.GetOrAdd(comboKey).AddWinOnly();
            }
        }

        if (countedAsLoss)
        {
            if (TryGetString(root, "killed_by_encounter", out string? killedByEncounter) && killedByEncounter is not null)
            {
                index.DeathEncounters.GetOrAdd(killedByEncounter).AddCountOnly();
            }

            foreach (string monsterId in lastEncounterMonsterIds)
            {
                index.DeathMonsters.GetOrAdd(monsterId).AddCountOnly();
            }
        }

        if (runShopGoldSpent > 0 || runBoughtRelics > 0 || runBoughtPotions > 0 || runBoughtColorless > 0 || runShopCardsGained > 0)
        {
            index.ShopRuns.GetOrAdd("shop_visits").AddResult(won, countedAsLoss);
            index.ShopGoldSpent += runShopGoldSpent;
            index.ShopBoughtRelics += runBoughtRelics;
            index.ShopBoughtPotions += runBoughtPotions;
            index.ShopBoughtColorless += runBoughtColorless;
            index.ShopCardsGained += runShopCardsGained;
        }
    }

    private static void ParseCardChoices(RunHistoryIndex index, JsonElement stats, bool won, bool countedAsLoss)
    {
        if (!TryGetArray(stats, "card_choices", out JsonElement choices))
        {
            return;
        }

        foreach (JsonElement choice in choices.EnumerateArray())
        {
            if (!TryGetObject(choice, "card", out JsonElement card) ||
                !TryGetString(card, "id", out string? cardId) ||
                cardId is null)
            {
                continue;
            }

            bool picked = TryGetBoolean(choice, "was_picked", out bool wasPicked) && wasPicked;
            index.CardChoices.GetOrAdd(cardId).AddChoice(picked, won, countedAsLoss);
        }
    }

    private static void ParseEventChoices(RunHistoryIndex index, JsonElement stats, bool won, bool countedAsLoss)
    {
        if (!TryGetArray(stats, "event_choices", out JsonElement choices))
        {
            return;
        }

        foreach (JsonElement choice in choices.EnumerateArray())
        {
            if (!TryGetObject(choice, "title", out JsonElement title) ||
                !TryGetString(title, "table", out string? table) ||
                !TryGetString(title, "key", out string? key) ||
                table is null ||
                key is null)
            {
                continue;
            }

            index.EventChoices.GetOrAdd(EventChoiceKey(table, key)).AddResult(won, countedAsLoss);
        }
    }

    private static IEnumerable<string> BuildCardRelicCombos(HashSet<string> cardIds, HashSet<string> relicIds)
    {
        foreach (string cardId in cardIds.OrderBy(id => id, StringComparer.Ordinal).Take(50))
        {
            foreach (string relicId in relicIds.OrderBy(id => id, StringComparer.Ordinal).Take(50))
            {
                yield return $"{cardId}|{relicId}";
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateMapPoints(JsonElement mapPointHistory)
    {
        foreach (JsonElement actOrMapPoint in mapPointHistory.EnumerateArray())
        {
            if (actOrMapPoint.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement mapPoint in actOrMapPoint.EnumerateArray())
                {
                    if (mapPoint.ValueKind == JsonValueKind.Object)
                    {
                        yield return mapPoint;
                    }
                }
            }
            else if (actOrMapPoint.ValueKind == JsonValueKind.Object)
            {
                yield return actOrMapPoint;
            }
        }
    }

    private static IEnumerable<string> BuildShopLines(RunHistoryIndex index)
    {
        MetricSummary shopVisits = index.ShopRuns.GetValueOrDefault("shop_visits") ?? new MetricSummary();
        yield return $"Shop purchase runs: {shopVisits.Count}, {FormatPercent(shopVisits.WinRate)} WR";
        yield return $"Gold spent in shops: {index.ShopGoldSpent:0}";
        yield return $"Bought relics: {index.ShopBoughtRelics}; bought potions: {index.ShopBoughtPotions}; bought colorless cards: {index.ShopBoughtColorless}";
        yield return $"Cards gained in shops: {index.ShopCardsGained} (includes normal/card purchases inferred from shop-room card gains)";
    }

    private static void AppendSection(StringBuilder builder, string title, IEnumerable<string> lines)
    {
        builder.AppendLine(title);
        foreach (string line in lines)
        {
            builder.AppendLine($"• {line}");
        }
        builder.AppendLine();
    }

    private static string EventChoiceKey(string table, string key)
    {
        return $"{table}/{key}";
    }

    private static string PrettyCombo(string key)
    {
        string[] parts = key.Split('|', 2);
        return parts.Length == 2 ? $"{PrettyId(parts[0])} + {PrettyId(parts[1])}" : PrettyId(key);
    }

    private static string PrettyEventChoice(string key)
    {
        string[] parts = key.Split('/', 2);
        if (parts.Length != 2)
        {
            return PrettyId(key);
        }

        string entry = parts[1].Replace(".title", "", StringComparison.Ordinal);
        string[] entryParts = entry.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string eventName = entryParts.Length > 0 ? PrettyId(entryParts[0]) : PrettyId(entry);
        int optionIndex = Array.IndexOf(entryParts, "options");
        string optionName = optionIndex >= 0 && optionIndex < entryParts.Length - 1
            ? PrettyId(entryParts[optionIndex + 1])
            : PrettyId(entryParts.LastOrDefault() ?? entry);

        return $"{eventName}: {optionName}";
    }

    private static string PrettyId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "Unknown";
        }

        string entry = id.Split('/').Last();
        if (entry.Contains('.', StringComparison.Ordinal))
        {
            entry = entry.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? entry;
        }
        StringBuilder builder = new(entry.Length + 8);

        for (int index = 0; index < entry.Length; index++)
        {
            char c = entry[index];
            if (index > 0 && char.IsUpper(c) && !char.IsWhiteSpace(entry[index - 1]) && entry[index - 1] != '_' && entry[index - 1] != '-')
            {
                builder.Append(' ');
            }
            else if (c is '_' or '-')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string FormatPercent(double value)
    {
        return double.IsNaN(value) ? "—" : value.ToString("P0");
    }

    private static bool IsRunHistoryFolder(string path)
    {
        string normalized = path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        return normalized.EndsWith("/saves/history", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateIds(JsonElement element, string propertyName)
    {
        if (!TryGetArray(element, propertyName, out JsonElement array))
        {
            yield break;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (TryGetString(item, "id", out string? id) && id is not null)
            {
                yield return id;
            }
        }
    }

    private static IEnumerable<string> EnumerateStringArray(JsonElement element, string propertyName)
    {
        if (!TryGetArray(element, propertyName, out JsonElement array))
        {
            yield break;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }

    private static int CountArray(JsonElement element, string propertyName)
    {
        return TryGetArray(element, propertyName, out JsonElement array) ? array.GetArrayLength() : 0;
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

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement obj)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out obj) &&
            obj.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        obj = default;
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

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement property) &&
            property.TryGetDecimal(out decimal parsed))
        {
            value = parsed;
            return true;
        }

        value = 0;
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

    public sealed class RunHistoryIndex(string userDataDir)
    {
        public string UserDataDir { get; } = userDataDir;
        public int HistoryFolderCount { get; set; }
        public int TotalRuns { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int AbandonedRuns { get; set; }
        public int PlayerEntries { get; set; }
        public int FailedFiles { get; set; }
        public int DuplicateFilesSkipped { get; set; }
        public decimal ShopGoldSpent { get; set; }
        public int ShopBoughtRelics { get; set; }
        public int ShopBoughtPotions { get; set; }
        public int ShopBoughtColorless { get; set; }
        public int ShopCardsGained { get; set; }
        public double WinRate => Wins + Losses == 0 ? 0d : (double)Wins / (Wins + Losses);
        public Dictionary<string, MetricSummary> Characters { get; } = [];
        public Dictionary<string, MetricSummary> FinalDeckCards { get; } = [];
        public Dictionary<string, ChoiceMetricSummary> CardChoices { get; } = [];
        public Dictionary<string, MetricSummary> Relics { get; } = [];
        public Dictionary<string, MetricSummary> EventChoices { get; } = [];
        public Dictionary<string, MetricSummary> DeathEncounters { get; } = [];
        public Dictionary<string, MetricSummary> DeathMonsters { get; } = [];
        public Dictionary<string, MetricSummary> RoomTypes { get; } = [];
        public Dictionary<string, MetricSummary> ShopRuns { get; } = [];
        public Dictionary<string, MetricSummary> CardRelicCombos { get; } = [];
    }

    public class MetricSummary
    {
        public int Count { get; private set; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public double WinRate => Wins + Losses == 0 ? 0d : (double)Wins / (Wins + Losses);

        public void AddResult(bool won, bool countedAsLoss)
        {
            Count++;
            if (won)
            {
                Wins++;
            }
            else if (countedAsLoss)
            {
                Losses++;
            }
        }

        public void AddWeightedResult(int count, bool won, bool countedAsLoss)
        {
            Count += count;
            if (won)
            {
                Wins++;
            }
            else if (countedAsLoss)
            {
                Losses++;
            }
        }

        public void AddWinOnly()
        {
            Count++;
            Wins++;
        }

        public void AddCountOnly()
        {
            Count++;
        }
    }

    public sealed class ChoiceMetricSummary : MetricSummary
    {
        public int Seen { get; private set; }
        public int Picked { get; private set; }
        public int Skipped => Seen - Picked;
        public double PickRate => Seen == 0 ? 0d : (double)Picked / Seen;

        public void AddChoice(bool picked, bool won, bool countedAsLoss)
        {
            Seen++;
            if (picked)
            {
                Picked++;
                AddResult(won, countedAsLoss);
            }
        }
    }
}

internal static class RunHistoryAnalyticsDictionaryExtensions
{
    public static T GetOrAdd<T>(this Dictionary<string, T> dictionary, string key) where T : new()
    {
        if (!dictionary.TryGetValue(key, out T? value))
        {
            value = new T();
            dictionary[key] = value;
        }

        return value;
    }
}
