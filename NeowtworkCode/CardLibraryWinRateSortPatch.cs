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
    private const string SortPadName = "NeowtworkStatsSortPad";
    private const float ButtonSize = 34f;
    private const float ButtonGap = 5f;

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

        if (alphabetSorter.GetNodeOrNull<Control>(SortPadName) != null)
        {
            return;
        }

        Control sortPad = new()
        {
            Name = SortPadName,
            Position = new Vector2(156f, -1f),
            Size = new Vector2((ButtonSize * 2) + ButtonGap, (ButtonSize * 3) + (ButtonGap * 2)),
            MouseFilter = Control.MouseFilterEnum.Pass
        };

        alphabetSorter.AddChild(sortPad);

        AddSortButton(sortPad, library, grid, "WR", StatSortMetric.WinRate, 0, 0);
        AddSortButton(sortPad, library, grid, "V", StatSortMetric.Victories, 1, 0);
        AddSortButton(sortPad, library, grid, "L", StatSortMetric.Losses, 0, 1);
        AddSortButton(sortPad, library, grid, "P", StatSortMetric.Picked, 1, 1);
        AddSortButton(sortPad, library, grid, "S", StatSortMetric.Skipped, 0, 2);
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

        return metric switch
        {
            StatSortMetric.WinRate => GetWinRate(card),
            StatSortMetric.Victories => stats?.TimesWon ?? 0,
            StatSortMetric.Losses => stats?.TimesLost ?? 0,
            StatSortMetric.Picked => stats?.TimesPicked ?? 0,
            StatSortMetric.Skipped => stats?.TimesSkipped ?? 0,
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

    private static void AddSortButton(
        Control sortPad,
        NCardLibrary library,
        NCardLibraryGrid grid,
        string label,
        StatSortMetric metric,
        int column,
        int row)
    {
        Button button = new()
        {
            Text = label,
            Position = new Vector2(column * (ButtonSize + ButtonGap), row * (ButtonSize + ButtonGap)),
            Size = new Vector2(ButtonSize, ButtonSize),
            CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };

        StyleSortButton(button);
        button.Pressed += () =>
        {
            StatSortState currentState = GetSortState(grid);
            StatSortStateByGrid[grid] = new StatSortState(
                IsActive: true,
                Metric: metric,
                IsReversed: currentState.IsActive && currentState.Metric == metric && !currentState.IsReversed);

            CacheDefaultOrder(grid);
            DisplayCardsMethod.Invoke(library, null);
        };

        sortPad.AddChild(button);
    }

    private static void StyleSortButton(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", button.Text == "WR" ? 14 : 18);
        button.AddThemeColorOverride("font_color", new Color(0.96f, 0.93f, 0.80f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.96f, 0.70f));
        button.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.96f, 0.70f));
        button.AddThemeColorOverride("font_outline_color", new Color(0.09f, 0.14f, 0.15f));
        button.AddThemeConstantOverride("outline_size", 4);

        button.AddThemeStyleboxOverride("normal", CreateTileStyle(new Color(0.20f, 0.44f, 0.50f), 0f));
        button.AddThemeStyleboxOverride("hover", CreateTileStyle(new Color(0.25f, 0.55f, 0.62f), 0f));
        button.AddThemeStyleboxOverride("pressed", CreateTileStyle(new Color(0.13f, 0.32f, 0.37f), 1f));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static StyleBoxFlat CreateTileStyle(Color color, float contentOffsetY)
    {
        StyleBoxFlat style = new()
        {
            BgColor = color,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.08f, 0.26f, 0.30f),
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

    public enum StatSortMetric
    {
        WinRate,
        Victories,
        Losses,
        Picked,
        Skipped
    }

    private readonly record struct StatSortState(bool IsActive, StatSortMetric Metric, bool IsReversed);
}
