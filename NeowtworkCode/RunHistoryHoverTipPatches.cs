using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace Neowtwork.NeowtworkCode;

[HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton._Ready))]
internal static class EventOptionRunStatsPatch
{
    private static void Postfix(NEventOptionButton __instance)
    {
        EventOption option = __instance.Option;

        if (option == null || option.IsLocked)
        {
            return;
        }

        RunHistoryAnalytics.MetricSummary? stats =
            RunHistoryAnalytics.GetEventChoiceStats(option.HistoryName.LocTable, option.HistoryName.LocEntryKey);

        if (stats == null || stats.Count == 0)
        {
            return;
        }

        HoverTip tip = NeowtworkHoverTips.Create(
            $"Chosen {stats.Count} {Pluralize(stats.Count, "time", "times")}\n" +
            $"{FormatPercent(stats.WinRate)} win rate after choosing\n" +
            $"({stats.Wins} {Pluralize(stats.Wins, "victory", "victories")} - {stats.Losses} {Pluralize(stats.Losses, "loss", "losses")})",
            $"neowtwork:event-choice:{option.HistoryName.LocTable}/{option.HistoryName.LocEntryKey}");

        option.HoverTips = AppendUnique(option.HoverTips, tip);
    }

    private static IEnumerable<IHoverTip> AppendUnique(IEnumerable<IHoverTip> existingTips, IHoverTip tip)
    {
        List<IHoverTip> tips = existingTips.ToList();
        if (tips.All(existing => existing.Id != tip.Id))
        {
            tips.Add(tip);
        }

        return tips;
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("P0");
    }

    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTips), MethodType.Getter)]
internal static class RelicRunStatsHoverTipPatch
{
    private static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        RunHistoryAnalytics.MetricSummary? stats = RunHistoryAnalytics.GetRelicStats(__instance.Id.ToString());

        if (stats == null || stats.Count == 0)
        {
            return;
        }

        HoverTip tip = NeowtworkHoverTips.Create(
            $"Seen in {stats.Count} final {Pluralize(stats.Count, "deck", "decks")}\n" +
            $"{FormatPercent(stats.WinRate)} win rate with this relic\n" +
            $"({stats.Wins} {Pluralize(stats.Wins, "victory", "victories")} - {stats.Losses} {Pluralize(stats.Losses, "loss", "losses")})",
            $"neowtwork:relic:{__instance.Id}");

        List<IHoverTip> tips = __result.ToList();
        if (tips.All(existing => existing.Id != tip.Id))
        {
            tips.Add(tip);
        }

        __result = tips;
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("P0");
    }

    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }
}

internal static class NeowtworkHoverTips
{
    private static readonly LocString Title = new("main_menu_ui", "STATISTICS.title");

    public static HoverTip Create(string description, string id)
    {
        return new HoverTip(Title, description)
        {
            Id = id,
            ShouldOverrideTextOverflow = true
        };
    }
}
