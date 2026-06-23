using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Saves;

namespace Neowtwork.NeowtworkCode;

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary._Ready))]
internal static class CardLibraryWinRateSortReadyPatch
{
    private static void Postfix(NCardLibrary __instance)
    {
        CardLibraryWinRateSort.InstallButton(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.FilterCards), typeof(Func<CardModel, bool>), typeof(List<SortingOrders>))]
internal static class CardLibraryGridWinRateFilterPatch
{
    private static bool Prefix(NCardLibraryGrid __instance, Func<CardModel, bool> filter)
    {
        if (!CardLibraryWinRateSort.IsWinRateSortActive(__instance))
        {
            return true;
        }

        IReadOnlyList<CardModel> allCards = CardLibraryWinRateSort.GetAllCards(__instance);
        List<CardModel> sortedCards = CardLibraryWinRateSort.SortCards(allCards.Where(filter), __instance);

        CardLibraryWinRateSort.DisplayCardsPreservingOrder(__instance, sortedCards);
        return false;
    }
}

internal static class CardLibraryWinRateSort
{
    private const string DropdownName = "NeowtworkStatsSortDropdown";
    private const string MenuButtonName = "NeowtworkStatsSortMenuButton";
    private const float MenuButtonWidth = 250f;
    private const float MenuButtonHeight = 39f;

    private static readonly AccessTools.FieldRef<NCardLibrary, NCardLibraryGrid> GridRef =
        AccessTools.FieldRefAccess<NCardLibrary, NCardLibraryGrid>("_grid");

    private static readonly AccessTools.FieldRef<NCardLibrary, NCardViewSortButton> AlphabetSorterRef =
        AccessTools.FieldRefAccess<NCardLibrary, NCardViewSortButton>("_alphabetSorter");

    private static readonly AccessTools.FieldRef<NCardLibraryGrid, List<CardModel>> AllCardsRef =
        AccessTools.FieldRefAccess<NCardLibraryGrid, List<CardModel>>("_allCards");

    private static readonly MethodInfo DisplayCardsMethod =
        AccessTools.Method(typeof(NCardLibrary), "DisplayCards");

    private static readonly Dictionary<NCardLibraryGrid, StatSortState> StatSortStateByGrid = [];
    private static readonly Dictionary<ModelId, int> DefaultOrderByCardId = [];

    public static void InstallButton(NCardLibrary library)
    {
        NCardLibraryGrid grid = GridRef(library);
        NCardViewSortButton alphabetSorter = AlphabetSorterRef(library);

        CacheDefaultOrder(grid);

        if (alphabetSorter.GetNodeOrNull<MenuButton>(MenuButtonName) != null)
        {
            return;
        }

        alphabetSorter.GetNodeOrNull<OptionButton>(DropdownName)?.QueueFree();

        MenuButton menuButton = new()
        {
            Name = MenuButtonName,
            Position = new Vector2(0f, 42f),
            Size = new Vector2(MenuButtonWidth, MenuButtonHeight),
            CustomMinimumSize = new Vector2(MenuButtonWidth, MenuButtonHeight),
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };

        PopupMenu popup = menuButton.GetPopup();
        foreach (StatSortMetric metric in Enum.GetValues<StatSortMetric>())
        {
            popup.AddRadioCheckItem(GetMetricLabel(metric), (int)metric);
        }

        StyleDropdown(menuButton);
        UpdateMenuButtonState(menuButton, grid);

        popup.IdPressed += id =>
        {
            StatSortMetric metric = (StatSortMetric)id;
            StatSortState currentState = GetSortState(grid);
            bool isSameMetric = currentState.IsActive && currentState.Metric == metric;

            StatSortStateByGrid[grid] = new StatSortState(
                IsActive: true,
                Metric: metric,
                IsReversed: isSameMetric && !currentState.IsReversed);

            UpdateMenuButtonState(menuButton, grid);
            CacheDefaultOrder(grid);
            DisplayCardsMethod.Invoke(library, null);
        };

        alphabetSorter.AddChild(menuButton);
    }

    public static bool IsWinRateSortActive(NCardLibraryGrid grid)
    {
        return GetSortState(grid).IsActive;
    }

    public static IReadOnlyList<CardModel> GetAllCards(NCardLibraryGrid grid)
    {
        return AllCardsRef(grid);
    }

    public static void DisplayCardsPreservingOrder(NCardLibraryGrid grid, IReadOnlyList<CardModel> sortedCards)
    {
        grid.SetCards(sortedCards, PileType.None, [SortingOrders.Ascending], Task.CompletedTask);
    }

    public static List<CardModel> SortCards(IEnumerable<CardModel> cards, NCardLibraryGrid grid)
    {
        StatSortState state = GetSortState(grid);
        List<CardModel> sortedCards = cards
            .OrderByDescending(card => GetSortValue(card, state.Metric))
            .ThenBy(GetDefaultOrder)
            .ToList();

        if (state.IsReversed)
        {
            sortedCards.Reverse();
        }

        return sortedCards;
    }

    public static double GetWinRate(CardModel card)
    {
        CardStats? stats = GetCardStats(card);
        long victories = stats?.TimesWon ?? 0;
        long losses = stats?.TimesLost ?? 0;
        long finishedRuns = victories + losses;

        if (finishedRuns == 0)
        {
            return 0d;
        }

        return (double)victories / finishedRuns;
    }

    public static long GetVictories(CardModel card)
    {
        return GetCardStats(card)?.TimesWon ?? 0;
    }

    public static double GetSortValue(CardModel card, StatSortMetric metric)
    {
        CardStats? stats = GetCardStats(card);
        long picked = stats?.TimesPicked ?? 0;
        long skipped = stats?.TimesSkipped ?? 0;
        long seen = picked + skipped;

        return metric switch
        {
            StatSortMetric.WinRate => GetWinRate(card),
            StatSortMetric.PickRate => seen == 0 ? 0d : (double)picked / seen,
            StatSortMetric.Victories => stats?.TimesWon ?? 0,
            StatSortMetric.Losses => stats?.TimesLost ?? 0,
            StatSortMetric.Picked => picked,
            StatSortMetric.Skipped => skipped,
            StatSortMetric.Seen => seen,
            _ => 0
        };
    }

    public static int GetDefaultOrder(CardModel card)
    {
        if (DefaultOrderByCardId.TryGetValue(card.Id, out int order))
        {
            return order;
        }

        return int.MaxValue;
    }

    private static CardStats? GetCardStats(CardModel card)
    {
        SaveManager.Instance.Progress.CardStats.TryGetValue(card.Id, out CardStats? stats);
        return stats;
    }

    private static void CacheDefaultOrder(NCardLibraryGrid grid)
    {
        List<CardModel> allCards = AllCardsRef(grid);

        for (int index = 0; index < allCards.Count; index++)
        {
            DefaultOrderByCardId.TryAdd(allCards[index].Id, index);
        }
    }

    private static void UpdateMenuButtonState(MenuButton menuButton, NCardLibraryGrid grid)
    {
        StatSortState state = GetSortState(grid);
        menuButton.Text = state.IsActive
            ? $"{GetMetricLabel(state.Metric)} {(state.IsReversed ? "↑" : "↓")}"
            : "Card Stats";

        PopupMenu popup = menuButton.GetPopup();
        for (int index = 0; index < popup.ItemCount; index++)
        {
            popup.SetItemChecked(index, state.IsActive && popup.GetItemId(index) == (int)state.Metric);
        }
    }

    private static void StyleDropdown(MenuButton dropdown)
    {
        dropdown.AddThemeFontSizeOverride("font_size", 20);
        dropdown.AddThemeColorOverride("font_color", new Color(0.95f, 0.83f, 0.35f));
        dropdown.AddThemeColorOverride("font_hover_color", new Color(1f, 0.93f, 0.58f));
        dropdown.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.93f, 0.58f));
        dropdown.AddThemeColorOverride("font_outline_color", new Color(0.09f, 0.14f, 0.15f));
        dropdown.AddThemeConstantOverride("outline_size", 4);

        dropdown.AddThemeStyleboxOverride("normal", CreatePanelStyle(new Color(0.13f, 0.39f, 0.42f), 0f));
        dropdown.AddThemeStyleboxOverride("hover", CreatePanelStyle(new Color(0.16f, 0.47f, 0.50f), 0f));
        dropdown.AddThemeStyleboxOverride("pressed", CreatePanelStyle(new Color(0.10f, 0.32f, 0.35f), 1f));
        dropdown.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        PopupMenu popup = dropdown.GetPopup();
        popup.AddThemeFontSizeOverride("font_size", 18);
        popup.AddThemeColorOverride("font_color", new Color(0.96f, 0.93f, 0.80f));
        popup.AddThemeColorOverride("font_hover_color", new Color(1f, 0.93f, 0.58f));
        popup.AddThemeColorOverride("font_outline_color", new Color(0.09f, 0.14f, 0.15f));
        popup.AddThemeConstantOverride("outline_size", 3);
        popup.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.10f, 0.27f, 0.30f), 0f));
        popup.AddThemeStyleboxOverride("hover", CreatePanelStyle(new Color(0.16f, 0.47f, 0.50f), 0f));
    }

    private static StyleBoxFlat CreatePanelStyle(Color color, float contentOffsetY)
    {
        StyleBoxFlat style = new()
        {
            BgColor = color,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.05f, 0.25f, 0.28f),
            ShadowColor = new Color(0f, 0f, 0f, 0.45f),
            ShadowSize = 3,
            ContentMarginTop = contentOffsetY
        };

        return style;
    }

    private static StatSortState GetSortState(NCardLibraryGrid grid)
    {
        return StatSortStateByGrid.TryGetValue(grid, out StatSortState state)
            ? state
            : new StatSortState(IsActive: false, Metric: StatSortMetric.WinRate, IsReversed: false);
    }

    private static string GetMetricLabel(StatSortMetric metric)
    {
        return metric switch
        {
            StatSortMetric.WinRate => "Win Rate",
            StatSortMetric.PickRate => "Pick Rate",
            StatSortMetric.Victories => "Victories",
            StatSortMetric.Losses => "Losses",
            StatSortMetric.Picked => "Picked",
            StatSortMetric.Skipped => "Skipped",
            StatSortMetric.Seen => "Seen",
            _ => "Card Stats"
        };
    }

    public enum StatSortMetric
    {
        WinRate,
        PickRate,
        Victories,
        Losses,
        Picked,
        Skipped,
        Seen
    }

    private readonly record struct StatSortState(bool IsActive, StatSortMetric Metric, bool IsReversed);
}
