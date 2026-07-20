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
    private static readonly HashSet<string> StarterCardIds = new(StringComparer.Ordinal)
    {
        "CARD.ASCENDERS_BANE",
        "CARD.BASH",
        "CARD.BODYGUARD",
        "CARD.DEFEND_DEFECT",
        "CARD.DEFEND_IRONCLAD",
        "CARD.DEFEND_NECROBINDER",
        "CARD.DEFEND_REGENT",
        "CARD.DEFEND_SILENT",
        "CARD.DUALCAST",
        "CARD.FALLING_STAR",
        "CARD.NEUTRALIZE",
        "CARD.STRIKE_DEFECT",
        "CARD.STRIKE_IRONCLAD",
        "CARD.STRIKE_NECROBINDER",
        "CARD.STRIKE_REGENT",
        "CARD.STRIKE_SILENT",
        "CARD.SURVIVOR",
        "CARD.UNLEASH",
        "CARD.VENERATE",
        "CARD.ZAP"
    };
    private static readonly HashSet<string> StarterRelicIds = new(StringComparer.Ordinal)
    {
        "RELIC.BOUND_PHYLACTERY",
        "RELIC.BURNING_BLOOD",
        "RELIC.CRACKED_CORE",
        "RELIC.DIVINE_RIGHT",
        "RELIC.RING_OF_THE_SNAKE"
    };

    public enum DashboardTab
    {
        Overview,
        Cards,
        Relics,
        Combos,
        Ancients,
        Events,
        Monsters,
        Shops
    }

    public enum PlayerModeFilter
    {
        All,
        Singleplayer,
        Multiplayer
    }

    public enum RunResultFilter
    {
        All,
        Wins,
        Losses
    }

    public sealed record DashboardFilter(
        string? Character = null,
        int? Ascension = null,
        PlayerModeFilter PlayerMode = PlayerModeFilter.All,
        RunResultFilter Result = RunResultFilter.All)
    {
        public bool Matches(RunRecord run)
        {
            if (Character is not null && !run.Characters.Contains(Character, StringComparer.Ordinal))
            {
                return false;
            }

            if (Ascension.HasValue && run.Ascension != Ascension)
            {
                return false;
            }

            if (PlayerMode == PlayerModeFilter.Singleplayer && run.PlayerCount != 1)
            {
                return false;
            }

            if (PlayerMode == PlayerModeFilter.Multiplayer && run.PlayerCount <= 1)
            {
                return false;
            }

            if (Result == RunResultFilter.Wins && !run.Won)
            {
                return false;
            }

            if (Result == RunResultFilter.Losses && !run.CountedAsLoss)
            {
                return false;
            }

            return true;
        }
    }

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
        return BuildDashboardText(DashboardTab.Overview, new DashboardFilter(), compact: false);
    }

    public static string BuildDashboardText(DashboardTab tab, DashboardFilter filter, bool compact = true)
    {
        RunHistoryIndex index = GetIndex();
        List<RunRecord> runs = index.Runs.Where(filter.Matches).ToList();
        StringBuilder builder = new();

        builder.AppendLine(compact ? $"{DashboardTabTitle(tab)}" : "Run History Analytics");
        builder.AppendLine();
        builder.AppendLine(BuildRunSummaryLine(runs));
        builder.AppendLine($"Filters: {BuildFilterSummary(filter)}");
        if (!compact)
        {
            builder.AppendLine($"All files indexed: {index.TotalRuns} runs; {index.PlayerEntries} player entries");
            builder.AppendLine($"History folders: {index.HistoryFolderCount}; duplicate run files skipped: {index.DuplicateFilesSkipped}; unreadable files skipped: {index.FailedFiles}");
        }
        builder.AppendLine();

        switch (tab)
        {
            case DashboardTab.Cards:
                AppendCardsDashboard(builder, runs);
                break;
            case DashboardTab.Relics:
                AppendRelicsDashboard(builder, runs);
                break;
            case DashboardTab.Combos:
                AppendCombosDashboard(builder, runs);
                break;
            case DashboardTab.Ancients:
                AppendAncientsDashboard(builder, runs);
                break;
            case DashboardTab.Events:
                AppendEventsDashboard(builder, runs);
                break;
            case DashboardTab.Monsters:
                AppendMonstersDashboard(builder, runs);
                break;
            case DashboardTab.Shops:
                AppendShopsDashboard(builder, runs);
                break;
            case DashboardTab.Overview:
            default:
                AppendOverviewDashboard(builder, runs, index);
                break;
        }

        return builder.ToString().TrimEnd();
    }

    public static DashboardModel BuildDashboardModel(DashboardTab tab, DashboardFilter filter)
    {
        RunHistoryIndex index = GetIndex();
        List<RunRecord> runs = index.Runs.Where(filter.Matches).ToList();
        int wins = runs.Count(run => run.Won);
        int losses = runs.Count(run => run.CountedAsLoss);
        int abandoned = runs.Count(run => run.Abandoned);

        List<DashboardCard> cards =
        [
            new("Runs", runs.Count.ToString(), $"{wins} wins · {losses} losses · {abandoned} abandoned"),
            new("Win Rate", FormatPercent(Rate(wins, losses)), "wins / completed runs"),
            new("Filters", FilterPill(filter), BuildFilterSummary(filter))
        ];

        List<DashboardSection> sections = tab switch
        {
            DashboardTab.Cards => BuildCardSections(runs),
            DashboardTab.Relics => BuildRelicSections(runs),
            DashboardTab.Combos => BuildComboSections(runs),
            DashboardTab.Ancients => BuildAncientSections(runs),
            DashboardTab.Events => BuildEventSections(runs),
            DashboardTab.Monsters => BuildMonsterSections(runs),
            DashboardTab.Shops => BuildShopSections(runs),
            _ => BuildOverviewSections(runs, index)
        };

        return new DashboardModel(DashboardTabTitle(tab), cards, sections);
    }

    public static IReadOnlyList<string> GetFilterCharacters()
    {
        return GetIndex().Runs
            .SelectMany(run => run.Characters)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(PrettyId)
            .ToArray();
    }

    public static IReadOnlyList<int> GetFilterAscensions()
    {
        return GetIndex().Runs
            .Where(run => run.Ascension.HasValue)
            .Select(run => run.Ascension!.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
    }

    public static string PrettyName(string id)
    {
        return PrettyId(id);
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
        int? ascension = TryGetInt(root, "ascension", out int parsedAscension) ? parsedAscension : null;

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
        HashSet<string> runNonStarterFinalCardIds = [];
        HashSet<string> runRelicIds = [];
        HashSet<string> runNonStarterRelicIds = [];
        HashSet<string> runCharacters = [];
        List<CardChoiceRecord> runCardChoices = [];
        List<string> runEventChoiceKeys = [];
        HashSet<string> runUpgradedCardIds = [];
        HashSet<string> runEnchantedCardIds = [];
        HashSet<string> runEnchantmentIds = [];
        List<AncientChoiceRecord> runAncientChoices = [];
        HashSet<string> lastEncounterMonsterIds = [];
        string? deathEncounterId = null;
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
                runCharacters.Add(character);
                index.Characters.GetOrAdd(character).AddResult(won, countedAsLoss);

                foreach ((string cardId, bool isStarter) in EnumerateDeckCards(player))
                {
                    runFinalCardIds.Add(cardId);
                    if (!isStarter)
                    {
                        runNonStarterFinalCardIds.Add(cardId);
                    }

                    index.FinalDeckCards.GetOrAdd(cardId).AddResult(won, countedAsLoss);
                }

                foreach ((string relicId, bool isStarter) in EnumerateRelicsWithStarterFlag(player))
                {
                    runRelicIds.Add(relicId);
                    if (!isStarter)
                    {
                        runNonStarterRelicIds.Add(relicId);
                    }

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
                        ParseCardChoices(index, stats, won, countedAsLoss, runCardChoices);
                        ParseEventChoices(index, stats, won, countedAsLoss, runEventChoiceKeys);
                        ParseUpgradesAndEnchantments(stats, runUpgradedCardIds, runEnchantedCardIds, runEnchantmentIds);
                        ParseAncientChoices(stats, runAncientChoices);

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
            foreach (string comboKey in BuildCardRelicCombos(runNonStarterFinalCardIds, runNonStarterRelicIds))
            {
                index.CardRelicCombos.GetOrAdd(comboKey).AddWinOnly();
            }
        }

        if (countedAsLoss)
        {
            if (TryGetString(root, "killed_by_encounter", out string? killedByEncounter) && killedByEncounter is not null)
            {
                deathEncounterId = killedByEncounter;
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

        index.Runs.Add(new RunRecord(
            Won: won,
            CountedAsLoss: countedAsLoss,
            Abandoned: abandoned,
            Ascension: ascension,
            PlayerCount: players.ValueKind == JsonValueKind.Array ? players.GetArrayLength() : 1,
            Characters: [.. runCharacters],
            FinalCardIds: [.. runFinalCardIds],
            NonStarterFinalCardIds: [.. runNonStarterFinalCardIds],
            RelicIds: [.. runRelicIds],
            NonStarterRelicIds: [.. runNonStarterRelicIds],
            CardChoices: runCardChoices,
            EventChoiceKeys: runEventChoiceKeys,
            DeathEncounterId: deathEncounterId,
            DeathMonsterIds: [.. lastEncounterMonsterIds],
            RoomCounts: new Dictionary<string, int>(roomCounts, StringComparer.Ordinal),
            ShopGoldSpent: runShopGoldSpent,
            ShopBoughtRelics: runBoughtRelics,
            ShopBoughtPotions: runBoughtPotions,
            ShopBoughtColorless: runBoughtColorless,
            ShopCardsGained: runShopCardsGained,
            UpgradedCardIds: [.. runUpgradedCardIds],
            EnchantedCardIds: [.. runEnchantedCardIds],
            EnchantmentIds: [.. runEnchantmentIds],
            AncientChoices: runAncientChoices));
    }

    private static void ParseCardChoices(
        RunHistoryIndex index,
        JsonElement stats,
        bool won,
        bool countedAsLoss,
        List<CardChoiceRecord> runCardChoices)
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
            runCardChoices.Add(new CardChoiceRecord(cardId, picked));
            index.CardChoices.GetOrAdd(cardId).AddChoice(picked, won, countedAsLoss);
        }
    }

    private static void ParseEventChoices(
        RunHistoryIndex index,
        JsonElement stats,
        bool won,
        bool countedAsLoss,
        List<string> runEventChoiceKeys)
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

            string choiceKey = EventChoiceKey(table, key);
            runEventChoiceKeys.Add(choiceKey);
            index.EventChoices.GetOrAdd(choiceKey).AddResult(won, countedAsLoss);
        }
    }

    private static void ParseUpgradesAndEnchantments(
        JsonElement stats,
        HashSet<string> upgradedCardIds,
        HashSet<string> enchantedCardIds,
        HashSet<string> enchantmentIds)
    {
        foreach (string cardId in EnumerateFlexibleCardIds(stats, "upgraded_cards"))
        {
            upgradedCardIds.Add(cardId);
        }

        foreach (string cardId in EnumerateFlexibleCardIds(stats, "cards_enchanted"))
        {
            enchantedCardIds.Add(cardId);
        }

        foreach (string enchantmentId in EnumerateFlexibleIds(stats, "enchantments"))
        {
            enchantmentIds.Add(enchantmentId);
        }
    }

    private static void ParseAncientChoices(JsonElement stats, List<AncientChoiceRecord> runAncientChoices)
    {
        if (!TryGetArray(stats, "ancient_choice", out JsonElement choices))
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

            bool picked = TryGetBoolean(choice, "was_chosen", out bool wasChosen) && wasChosen;
            runAncientChoices.Add(new AncientChoiceRecord(EventChoiceKey(table, key), picked));
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

    private static List<DashboardSection> BuildOverviewSections(IReadOnlyCollection<RunRecord> runs, RunHistoryIndex index)
    {
        Dictionary<string, MetricSummary> characters = SummarizeMetric(runs, run => run.Characters);
        Dictionary<string, MetricSummary> cards = SummarizeMetric(runs, run => run.NonStarterFinalCardIds);
        Dictionary<string, MetricSummary> relics = SummarizeMetric(runs, run => run.NonStarterRelicIds);
        Dictionary<string, MetricSummary> events = SummarizeMetric(runs, run => run.EventChoiceKeys);

        List<DashboardSection> sections =
        [
            new("Character Performance", "Your current filtered spread by character.", BuildMetricRows(characters
                .OrderByDescending(pair => pair.Value.Count)
                .ThenByDescending(pair => pair.Value.WinRate)
                .ThenBy(pair => pair.Key)
                .Take(8), "runs")),

            new("Top Signals", "Non-starter cards and relics only, so starter decks stop eating the leaderboard.", [
                BuildTopMetricRow("Best Card", cards, 5),
                BuildTopMetricRow("Best Relic", relics, 3),
                BuildTopCountRow("Most Lethal Encounter", SummarizeCount(runs.Select(run => run.DeathEncounterId).OfType<string>())),
                BuildTopMetricRow("Event Choice", events, 2, PrettyEventChoice)
            ]),

            new("Pathing Snapshot", "How often each room type appears in filtered runs, and the run win rate when it appears.", BuildMetricRows(SummarizeWeightedMetric(runs, run => run.RoomCounts)
                .OrderByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key)
                .Take(10), "rooms"))
        ];

        if (runs.Count == index.TotalRuns)
        {
            sections.Add(new DashboardSection("Index Health", "Scanner diagnostics.", [
                new("History folders", index.HistoryFolderCount.ToString(), "folders scanned", null),
                new("Duplicates skipped", index.DuplicateFilesSkipped.ToString(), "same run seen more than once", null),
                new("Unreadable files", index.FailedFiles.ToString(), "files skipped", null)
            ]));
        }

        return sections;
    }

    private static List<DashboardSection> BuildCardSections(IReadOnlyCollection<RunRecord> runs)
    {
        return
        [
            new("Best Non-Starter Final Deck Cards", "Cards in your final deck, excluding starter cards.", BuildMetricRows(SummarizeMetric(runs, run => run.NonStarterFinalCardIds)
                .Where(pair => pair.Value.Count >= 5)
                .OrderByDescending(pair => pair.Value.WinRate)
                .ThenByDescending(pair => pair.Value.Wins)
                .ThenBy(pair => pair.Key)
                .Take(12), "final decks")),

            new("Most-Picked Card Rewards", "How often you take a card when it appears in reward screens.", BuildChoiceRows(SummarizeCardChoices(runs)
                .Where(pair => pair.Value.Seen > 0 && !StarterCardIds.Contains(pair.Key))
                .OrderByDescending(pair => pair.Value.PickRate)
                .ThenByDescending(pair => pair.Value.Picked)
                .ThenBy(pair => pair.Key)
                .Take(12))),

            new("Best Upgrades", "Win rate in runs where this card was upgraded.", BuildMetricRows(SummarizeMetric(runs, run => run.UpgradedCardIds)
                .Where(pair => pair.Value.Count >= 2 && !StarterCardIds.Contains(pair.Key))
                .OrderByDescending(pair => pair.Value.WinRate)
                .ThenByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key)
                .Take(10), "upgraded runs", suffix: "+")),

            new("Best Enchantments", "Win rate in runs where this card was enchanted.", BuildMetricRows(SummarizeMetric(runs, run => run.EnchantedCardIds)
                .Where(pair => pair.Value.Count >= 2 && !StarterCardIds.Contains(pair.Key))
                .OrderByDescending(pair => pair.Value.WinRate)
                .ThenByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key)
                .Take(10), "enchanted runs"))
        ];
    }

    private static List<DashboardSection> BuildRelicSections(IReadOnlyCollection<RunRecord> runs)
    {
        return
        [
            new("Best Non-Starter Relics", "Relics in final builds, excluding starter relics.", BuildMetricRows(SummarizeMetric(runs, run => run.NonStarterRelicIds)
                .Where(pair => pair.Value.Count >= 3)
                .OrderByDescending(pair => pair.Value.WinRate)
                .ThenByDescending(pair => pair.Value.Wins)
                .ThenBy(pair => pair.Key)
                .Take(16), "final builds")),

            new("Most Common Non-Starter Relics", "Relics that appear most often in your filtered final builds.", BuildMetricRows(SummarizeMetric(runs, run => run.NonStarterRelicIds)
                .OrderByDescending(pair => pair.Value.Count)
                .ThenByDescending(pair => pair.Value.WinRate)
                .ThenBy(pair => pair.Key)
                .Take(16), "final builds"))
        ];
    }

    private static List<DashboardSection> BuildComboSections(IReadOnlyCollection<RunRecord> runs)
    {
        Dictionary<string, MetricSummary> comboStats = [];
        foreach (RunRecord run in runs)
        {
            foreach (string comboKey in BuildCardRelicCombos(run.NonStarterFinalCardIds.ToHashSet(StringComparer.Ordinal), run.NonStarterRelicIds.ToHashSet(StringComparer.Ordinal)))
            {
                comboStats.GetOrAdd(comboKey).AddResult(run.Won, run.CountedAsLoss);
            }
        }

        return
        [
            new("Card + Relic Combos", "Non-starter cards paired with non-starter relics in the same final build.", BuildMetricRows(comboStats
                .Where(pair => pair.Value.Count >= 3)
                .OrderByDescending(pair => pair.Value.WinRate)
                .ThenByDescending(pair => pair.Value.Wins)
                .ThenByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key)
                .Take(18), "runs", PrettyCombo))
        ];
    }

    private static List<DashboardSection> BuildAncientSections(IReadOnlyCollection<RunRecord> runs)
    {
        Dictionary<string, ChoiceMetricSummary> ancientStats = [];
        foreach (RunRecord run in runs)
        {
            foreach (AncientChoiceRecord choice in run.AncientChoices)
            {
                ancientStats.GetOrAdd(choice.ChoiceKey).AddChoice(choice.Picked, run.Won, run.CountedAsLoss);
            }
        }

        return
        [
            new("Ancient Offers", "Offers seen at the start of acts: how often you picked them and how those runs ended.", BuildChoiceRows(ancientStats
                .Where(pair => pair.Value.Seen > 0)
                .OrderByDescending(pair => pair.Value.Seen)
                .ThenByDescending(pair => pair.Value.PickRate)
                .ThenBy(pair => pair.Key)
                .Take(18), PrettyEventChoice))
        ];
    }

    private static List<DashboardSection> BuildEventSections(IReadOnlyCollection<RunRecord> runs)
    {
        return
        [
            new("Event Choices", "Choices you made in event rooms and the run result afterward.", BuildMetricRows(SummarizeMetric(runs, run => run.EventChoiceKeys)
                .Where(pair => pair.Value.Count >= 2)
                .OrderByDescending(pair => pair.Value.Count)
                .ThenByDescending(pair => pair.Value.WinRate)
                .ThenBy(pair => pair.Key)
                .Take(18), "chosen", PrettyEventChoice))
        ];
    }

    private static List<DashboardSection> BuildMonsterSections(IReadOnlyCollection<RunRecord> runs)
    {
        Dictionary<string, int> deaths = SummarizeCount(runs.Select(run => run.DeathEncounterId).OfType<string>());
        int max = deaths.Count == 0 ? 0 : deaths.Values.Max();
        return
        [
            new("Monster Trouble", "The encounter that actually killed you, from the run file's death record.", deaths
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .Take(18)
                .Select(pair => new DashboardRow(PrettyId(pair.Key), $"{pair.Value}", "deaths", max == 0 ? null : (double)pair.Value / max))
                .ToList())
        ];
    }

    private static List<DashboardSection> BuildShopSections(IReadOnlyCollection<RunRecord> runs)
    {
        int shopRuns = runs.Count(HasShopActivity);
        int shopWins = runs.Count(run => run.Won && HasShopActivity(run));
        int shopLosses = runs.Count(run => run.CountedAsLoss && HasShopActivity(run));

        return
        [
            new("Shop Snapshot", "Early shop analytics from the run file. More precise purchase categories are a future pass.", [
                new("Runs with Purchases", shopRuns.ToString(), $"{FormatPercent(Rate(shopWins, shopLosses))} win rate", Rate(shopWins, shopLosses)),
                new("Gold Spent", runs.Sum(run => run.ShopGoldSpent).ToString("0"), "total gold", null),
                new("Bought Relics", runs.Sum(run => run.ShopBoughtRelics).ToString(), "shop relic purchases", null),
                new("Bought Potions", runs.Sum(run => run.ShopBoughtPotions).ToString(), "shop potion purchases", null),
                new("Bought Colorless", runs.Sum(run => run.ShopBoughtColorless).ToString(), "colorless purchases", null),
                new("Cards Gained", runs.Sum(run => run.ShopCardsGained).ToString(), "cards gained in shop rooms", null)
            ])
        ];
    }

    private static bool HasShopActivity(RunRecord run)
    {
        return run.ShopGoldSpent > 0 ||
               run.ShopBoughtRelics > 0 ||
               run.ShopBoughtPotions > 0 ||
               run.ShopBoughtColorless > 0 ||
               run.ShopCardsGained > 0;
    }

    private static List<DashboardRow> BuildMetricRows(
        IEnumerable<KeyValuePair<string, MetricSummary>> metrics,
        string countLabel,
        Func<string, string>? labelFormatter = null,
        string suffix = "")
    {
        return metrics
            .Select(pair => new DashboardRow(
                $"{(labelFormatter ?? PrettyId)(pair.Key)}{suffix}",
                FormatPercent(pair.Value.WinRate),
                $"{pair.Value.Wins}W · {pair.Value.Losses}L · {pair.Value.Count} {countLabel}",
                pair.Value.WinRate))
            .ToList();
    }

    private static List<DashboardRow> BuildChoiceRows(
        IEnumerable<KeyValuePair<string, ChoiceMetricSummary>> choices,
        Func<string, string>? labelFormatter = null)
    {
        return choices
            .Select(pair => new DashboardRow(
                (labelFormatter ?? PrettyId)(pair.Key),
                FormatPercent(pair.Value.PickRate),
                $"{pair.Value.Picked}/{pair.Value.Seen} picked · {FormatPercent(pair.Value.WinRate)} WR after pick",
                pair.Value.PickRate))
            .ToList();
    }

    private static DashboardRow BuildTopMetricRow(
        string label,
        Dictionary<string, MetricSummary> summary,
        int minimumCount,
        Func<string, string>? labelFormatter = null)
    {
        KeyValuePair<string, MetricSummary> top = summary
            .Where(pair => pair.Value.Count >= minimumCount)
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Wins)
            .ThenBy(pair => pair.Key)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(top.Key)
            ? new DashboardRow(label, "Not enough data", $"needs {minimumCount}+ samples", null)
            : new DashboardRow(label, (labelFormatter ?? PrettyId)(top.Key), $"{FormatPercent(top.Value.WinRate)} · {top.Value.Wins}W · {top.Value.Losses}L", top.Value.WinRate);
    }

    private static DashboardRow BuildTopCountRow(string label, Dictionary<string, int> summary)
    {
        KeyValuePair<string, int> top = summary
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(top.Key)
            ? new DashboardRow(label, "Not enough data", "", null)
            : new DashboardRow(label, PrettyId(top.Key), $"{top.Value} death encounters", null);
    }

    private static string FilterPill(DashboardFilter filter)
    {
        string character = filter.Character is null ? "All" : PrettyId(filter.Character);
        string ascension = filter.Ascension is null ? "Any A" : $"A{filter.Ascension}";
        string result = filter.Result switch
        {
            RunResultFilter.Wins => "Wins",
            RunResultFilter.Losses => "Losses",
            _ => "W+L"
        };
        return $"{character} · {ascension} · {result}";
    }

    private static void AppendOverviewDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs, RunHistoryIndex index)
    {
        Dictionary<string, MetricSummary> characters = SummarizeMetric(runs, run => run.Characters);

        AppendSection(builder, "Characters", characters
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(8)
            .Select(pair => $"{PrettyId(pair.Key)}: {pair.Value.Count} runs, {FormatPercent(pair.Value.WinRate)} win rate"));

        AppendSection(builder, "Top Signals", [
            $"Best card: {FormatTopMetric(SummarizeMetric(runs, run => run.NonStarterFinalCardIds), minimumCount: 5)}",
            $"Best relic: {FormatTopMetric(SummarizeMetric(runs, run => run.NonStarterRelicIds), minimumCount: 3)}",
            $"Most common lethal encounter: {FormatTopCount(SummarizeCount(runs.Select(run => run.DeathEncounterId).OfType<string>()))}",
            $"Most common event choice: {FormatTopMetric(SummarizeMetric(runs, run => run.EventChoiceKeys), minimumCount: 2)}"
        ]);

        AppendSection(builder, "Pathing Snapshot", SummarizeWeightedMetric(runs, run => run.RoomCounts)
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(10)
            .Select(pair => $"{PrettyId(pair.Key)}: {pair.Value.Count} rooms, {FormatPercent(pair.Value.WinRate)} run win rate when visited"));

        if (runs.Count == index.TotalRuns)
        {
            AppendSection(builder, "Index Health", [
                $"History folders: {index.HistoryFolderCount}",
                $"Duplicate run files skipped: {index.DuplicateFilesSkipped}",
                $"Unreadable files skipped: {index.FailedFiles}"
            ]);
        }
    }

    private static void AppendCardsDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs)
    {
        AppendSection(builder, "Best Non-Starter Cards in Final Decks", SummarizeMetric(runs, run => run.NonStarterFinalCardIds)
            .Where(pair => pair.Value.Count >= 5)
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Wins)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyId(pair.Key)}: {FormatPercent(pair.Value.WinRate)} win rate ({pair.Value.Wins}-{pair.Value.Losses}), {pair.Value.Count} final decks"));

        AppendSection(builder, "Most-Picked Card Rewards", SummarizeCardChoices(runs)
            .Where(pair => pair.Value.Seen > 0 && !StarterCardIds.Contains(pair.Key))
            .OrderByDescending(pair => pair.Value.PickRate)
            .ThenByDescending(pair => pair.Value.Picked)
            .ThenBy(pair => pair.Key)
            .Take(12)
            .Select(pair => $"{PrettyId(pair.Key)}: {FormatPercent(pair.Value.PickRate)} pick rate ({pair.Value.Picked}/{pair.Value.Seen}), {FormatPercent(pair.Value.WinRate)} win rate when picked"));

        AppendSection(builder, "Best Upgrades", SummarizeMetric(runs, run => run.UpgradedCardIds)
            .Where(pair => pair.Value.Count >= 2 && !StarterCardIds.Contains(pair.Key))
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(10)
            .Select(pair => $"{PrettyId(pair.Key)}+: {FormatPercent(pair.Value.WinRate)} win rate ({pair.Value.Wins}-{pair.Value.Losses}), upgraded in {pair.Value.Count} runs"));

        AppendSection(builder, "Best Enchanted Cards", SummarizeMetric(runs, run => run.EnchantedCardIds)
            .Where(pair => pair.Value.Count >= 2 && !StarterCardIds.Contains(pair.Key))
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(10)
            .Select(pair => $"{PrettyId(pair.Key)}: {FormatPercent(pair.Value.WinRate)} win rate ({pair.Value.Wins}-{pair.Value.Losses}), enchanted in {pair.Value.Count} runs"));
    }

    private static void AppendRelicsDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs)
    {
        AppendSection(builder, "Best Non-Starter Relics", SummarizeMetric(runs, run => run.NonStarterRelicIds)
            .Where(pair => pair.Value.Count >= 3)
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Wins)
            .ThenBy(pair => pair.Key)
            .Take(16)
            .Select(pair => $"{PrettyId(pair.Key)}: {FormatPercent(pair.Value.WinRate)} win rate ({pair.Value.Wins}-{pair.Value.Losses}), seen in {pair.Value.Count} final builds"));

        AppendSection(builder, "Most Common Non-Starter Relics", SummarizeMetric(runs, run => run.NonStarterRelicIds)
            .OrderByDescending(pair => pair.Value.Count)
            .ThenByDescending(pair => pair.Value.WinRate)
            .ThenBy(pair => pair.Key)
            .Take(16)
            .Select(pair => $"{PrettyId(pair.Key)}: {pair.Value.Count} final builds, {FormatPercent(pair.Value.WinRate)} win rate"));
    }

    private static void AppendCombosDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs)
    {
        Dictionary<string, MetricSummary> comboStats = [];
        foreach (RunRecord run in runs)
        {
            foreach (string comboKey in BuildCardRelicCombos(run.NonStarterFinalCardIds.ToHashSet(StringComparer.Ordinal), run.NonStarterRelicIds.ToHashSet(StringComparer.Ordinal)))
            {
                comboStats.GetOrAdd(comboKey).AddResult(run.Won, run.CountedAsLoss);
            }
        }

        AppendSection(builder, "Card + Relic Combos", comboStats
            .Where(pair => pair.Value.Count >= 3)
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Wins)
            .ThenByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key)
            .Take(18)
            .Select(pair => $"{PrettyCombo(pair.Key)}: {FormatPercent(pair.Value.WinRate)} win rate ({pair.Value.Wins}-{pair.Value.Losses}), {pair.Value.Count} runs"));
    }

    private static void AppendAncientsDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs)
    {
        Dictionary<string, ChoiceMetricSummary> ancientStats = [];
        foreach (RunRecord run in runs)
        {
            foreach (AncientChoiceRecord choice in run.AncientChoices)
            {
                ancientStats.GetOrAdd(choice.ChoiceKey).AddChoice(choice.Picked, run.Won, run.CountedAsLoss);
            }
        }

        AppendSection(builder, "Ancient Offers", ancientStats
            .Where(pair => pair.Value.Seen > 0)
            .OrderByDescending(pair => pair.Value.Seen)
            .ThenByDescending(pair => pair.Value.PickRate)
            .ThenBy(pair => pair.Key)
            .Take(18)
            .Select(pair => $"{PrettyEventChoice(pair.Key)}: offered {pair.Value.Seen}, picked {pair.Value.Picked} ({FormatPercent(pair.Value.PickRate)}), {FormatPercent(pair.Value.WinRate)} win rate after picking"));
    }

    private static void AppendEventsDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs)
    {
        AppendSection(builder, "Event Choices", SummarizeMetric(runs, run => run.EventChoiceKeys)
            .Where(pair => pair.Value.Count >= 2)
            .OrderByDescending(pair => pair.Value.Count)
            .ThenByDescending(pair => pair.Value.WinRate)
            .ThenBy(pair => pair.Key)
            .Take(18)
            .Select(pair => $"{PrettyEventChoice(pair.Key)}: chosen {pair.Value.Count} times, {FormatPercent(pair.Value.WinRate)} win rate ({pair.Value.Wins}-{pair.Value.Losses})"));
    }

    private static void AppendMonstersDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs)
    {
        AppendSection(builder, "Monster Trouble", SummarizeCount(runs.Select(run => run.DeathEncounterId).OfType<string>())
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Take(18)
            .Select(pair => $"{PrettyId(pair.Key)}: killed you {pair.Value} times"));
    }

    private static void AppendShopsDashboard(StringBuilder builder, IReadOnlyCollection<RunRecord> runs)
    {
        int shopRuns = runs.Count(run => run.ShopGoldSpent > 0 || run.ShopBoughtRelics > 0 || run.ShopBoughtPotions > 0 || run.ShopBoughtColorless > 0 || run.ShopCardsGained > 0);
        int shopWins = runs.Count(run => run.Won && (run.ShopGoldSpent > 0 || run.ShopBoughtRelics > 0 || run.ShopBoughtPotions > 0 || run.ShopBoughtColorless > 0 || run.ShopCardsGained > 0));
        int shopLosses = runs.Count(run => run.CountedAsLoss && (run.ShopGoldSpent > 0 || run.ShopBoughtRelics > 0 || run.ShopBoughtPotions > 0 || run.ShopBoughtColorless > 0 || run.ShopCardsGained > 0));

        AppendSection(builder, "Shop Snapshot", [
            $"Runs with shop purchases: {shopRuns}, {FormatPercent(Rate(shopWins, shopLosses))} win rate",
            $"Gold spent in shops: {runs.Sum(run => run.ShopGoldSpent):0}",
            $"Bought relics: {runs.Sum(run => run.ShopBoughtRelics)}",
            $"Bought potions: {runs.Sum(run => run.ShopBoughtPotions)}",
            $"Bought colorless cards: {runs.Sum(run => run.ShopBoughtColorless)}",
            $"Cards gained in shop rooms: {runs.Sum(run => run.ShopCardsGained)}"
        ]);
    }

    private static string BuildRunSummaryLine(IReadOnlyCollection<RunRecord> runs)
    {
        int wins = runs.Count(run => run.Won);
        int losses = runs.Count(run => run.CountedAsLoss);
        int abandoned = runs.Count(run => run.Abandoned);
        return $"Runs: {runs.Count} ({wins} wins, {losses} losses, {abandoned} abandoned, {FormatPercent(Rate(wins, losses))} win rate)";
    }

    private static string BuildFilterSummary(DashboardFilter filter)
    {
        List<string> parts = [];
        parts.Add(filter.Character is null ? "all characters" : PrettyId(filter.Character));
        parts.Add(filter.Ascension is null ? "all ascensions" : $"A{filter.Ascension}");
        parts.Add(filter.PlayerMode switch
        {
            PlayerModeFilter.Singleplayer => "singleplayer",
            PlayerModeFilter.Multiplayer => "multiplayer",
            _ => "single + multiplayer"
        });
        parts.Add(filter.Result switch
        {
            RunResultFilter.Wins => "wins only",
            RunResultFilter.Losses => "losses only",
            _ => "wins + losses"
        });
        return string.Join(" · ", parts);
    }

    private static string DashboardTabTitle(DashboardTab tab)
    {
        return tab switch
        {
            DashboardTab.Cards => "Card Analytics",
            DashboardTab.Relics => "Relic Analytics",
            DashboardTab.Combos => "Card + Relic Combos",
            DashboardTab.Ancients => "Ancient Offers",
            DashboardTab.Events => "Event Choices",
            DashboardTab.Monsters => "Monster Trouble",
            DashboardTab.Shops => "Shop Patterns",
            _ => "Neowtwork Dashboard"
        };
    }

    private static Dictionary<string, MetricSummary> SummarizeMetric(
        IEnumerable<RunRecord> runs,
        Func<RunRecord, IEnumerable<string>> selector)
    {
        Dictionary<string, MetricSummary> summary = [];
        foreach (RunRecord run in runs)
        {
            foreach (string id in selector(run).Distinct(StringComparer.Ordinal))
            {
                summary.GetOrAdd(id).AddResult(run.Won, run.CountedAsLoss);
            }
        }

        return summary;
    }

    private static Dictionary<string, MetricSummary> SummarizeWeightedMetric(
        IEnumerable<RunRecord> runs,
        Func<RunRecord, IReadOnlyDictionary<string, int>> selector)
    {
        Dictionary<string, MetricSummary> summary = [];
        foreach (RunRecord run in runs)
        {
            foreach ((string id, int count) in selector(run))
            {
                summary.GetOrAdd(id).AddWeightedResult(count, run.Won, run.CountedAsLoss);
            }
        }

        return summary;
    }

    private static Dictionary<string, ChoiceMetricSummary> SummarizeCardChoices(IEnumerable<RunRecord> runs)
    {
        Dictionary<string, ChoiceMetricSummary> summary = [];
        foreach (RunRecord run in runs)
        {
            foreach (CardChoiceRecord choice in run.CardChoices)
            {
                summary.GetOrAdd(choice.CardId).AddChoice(choice.Picked, run.Won, run.CountedAsLoss);
            }
        }

        return summary;
    }

    private static Dictionary<string, int> SummarizeCount(IEnumerable<string> ids)
    {
        Dictionary<string, int> summary = [];
        foreach (string id in ids)
        {
            summary[id] = summary.GetValueOrDefault(id) + 1;
        }

        return summary;
    }

    private static string FormatTopMetric(Dictionary<string, MetricSummary> summary, int minimumCount)
    {
        KeyValuePair<string, MetricSummary> top = summary
            .Where(pair => pair.Value.Count >= minimumCount)
            .OrderByDescending(pair => pair.Value.WinRate)
            .ThenByDescending(pair => pair.Value.Wins)
            .ThenBy(pair => pair.Key)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(top.Key)
            ? "not enough data yet"
            : $"{PrettyId(top.Key)} ({FormatPercent(top.Value.WinRate)}, {top.Value.Wins}-{top.Value.Losses})";
    }

    private static string FormatTopCount(Dictionary<string, int> summary)
    {
        KeyValuePair<string, int> top = summary
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(top.Key)
            ? "not enough data yet"
            : $"{PrettyId(top.Key)} ({top.Value})";
    }

    private static double Rate(int wins, int losses)
    {
        return wins + losses == 0 ? 0d : (double)wins / (wins + losses);
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

        entry = entry.Replace("_", " ", StringComparison.Ordinal).Replace("-", " ", StringComparison.Ordinal);
        if (entry.All(c => !char.IsLetter(c) || char.IsUpper(c) || char.IsWhiteSpace(c)))
        {
            return TitleCaseWords(entry);
        }

        StringBuilder builder = new(entry.Length + 8);

        for (int index = 0; index < entry.Length; index++)
        {
            char c = entry[index];
            if (index > 0 && char.IsUpper(c) && char.IsLower(entry[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return TitleCaseWords(builder.ToString());
    }

    private static string TitleCaseWords(string value)
    {
        string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "Unknown";
        }

        return string.Join(" ", words.Select(word =>
        {
            if (word.Length == 0)
            {
                return word;
            }

            if (word.Length <= 2 && word.All(char.IsUpper))
            {
                return word;
            }

            return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
        }));
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

    private static IEnumerable<(string Id, bool IsStarter)> EnumerateDeckCards(JsonElement player)
    {
        if (!TryGetArray(player, "deck", out JsonElement array))
        {
            yield break;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (!TryGetString(item, "id", out string? id) || id is null)
            {
                continue;
            }

            bool isStarter = StarterCardIds.Contains(id);

            if (TryGetObject(item, "enchantment", out JsonElement enchantment) &&
                TryGetString(enchantment, "id", out string? enchantmentId) &&
                enchantmentId is not null)
            {
                // Enchantment details are summarized separately from the card list.
            }

            yield return (id, isStarter);
        }
    }

    private static IEnumerable<(string Id, bool IsStarter)> EnumerateRelicsWithStarterFlag(JsonElement player)
    {
        if (!TryGetArray(player, "relics", out JsonElement array))
        {
            yield break;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (!TryGetString(item, "id", out string? id) || id is null)
            {
                continue;
            }

            bool isStarter = StarterRelicIds.Contains(id);

            yield return (id, isStarter);
        }
    }

    private static IEnumerable<string> EnumerateFlexibleIds(JsonElement element, string propertyName)
    {
        if (!TryGetArray(element, propertyName, out JsonElement array))
        {
            yield break;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? raw = item.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    yield return raw;
                }
            }
            else if (TryGetString(item, "id", out string? id) && id is not null)
            {
                yield return id;
            }
            else if (TryGetObject(item, "card", out JsonElement card) &&
                     TryGetString(card, "id", out string? cardId) &&
                     cardId is not null)
            {
                yield return cardId;
            }
            else if (TryGetObject(item, "enchantment", out JsonElement enchantment) &&
                     TryGetString(enchantment, "id", out string? enchantmentId) &&
                     enchantmentId is not null)
            {
                yield return enchantmentId;
            }
        }
    }

    private static IEnumerable<string> EnumerateFlexibleCardIds(JsonElement element, string propertyName)
    {
        if (!TryGetArray(element, propertyName, out JsonElement array))
        {
            yield break;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? raw = item.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    yield return raw;
                }
            }
            else if (TryGetString(item, "id", out string? id) && id is not null)
            {
                yield return id;
            }
            else if (TryGetObject(item, "card", out JsonElement card) &&
                     TryGetString(card, "id", out string? cardId) &&
                     cardId is not null)
            {
                yield return cardId;
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

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out JsonElement property) &&
            property.TryGetInt32(out int parsed))
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

    public sealed record DashboardModel(
        string Title,
        IReadOnlyList<DashboardCard> Cards,
        IReadOnlyList<DashboardSection> Sections);

    public sealed record DashboardCard(string Label, string Value, string Detail);

    public sealed record DashboardSection(
        string Title,
        string Subtitle,
        IReadOnlyList<DashboardRow> Rows);

    public sealed record DashboardRow(
        string Label,
        string Value,
        string Detail,
        double? ChartValue);

    public sealed record RunRecord(
        bool Won,
        bool CountedAsLoss,
        bool Abandoned,
        int? Ascension,
        int PlayerCount,
        IReadOnlyList<string> Characters,
        IReadOnlyList<string> FinalCardIds,
        IReadOnlyList<string> NonStarterFinalCardIds,
        IReadOnlyList<string> RelicIds,
        IReadOnlyList<string> NonStarterRelicIds,
        IReadOnlyList<CardChoiceRecord> CardChoices,
        IReadOnlyList<string> EventChoiceKeys,
        string? DeathEncounterId,
        IReadOnlyList<string> DeathMonsterIds,
        IReadOnlyDictionary<string, int> RoomCounts,
        decimal ShopGoldSpent,
        int ShopBoughtRelics,
        int ShopBoughtPotions,
        int ShopBoughtColorless,
        int ShopCardsGained,
        IReadOnlyList<string> UpgradedCardIds,
        IReadOnlyList<string> EnchantedCardIds,
        IReadOnlyList<string> EnchantmentIds,
        IReadOnlyList<AncientChoiceRecord> AncientChoices);

    public sealed record CardChoiceRecord(string CardId, bool Picked);

    public sealed record AncientChoiceRecord(string ChoiceKey, bool Picked);

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
        public List<RunRecord> Runs { get; } = [];
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
