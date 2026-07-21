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
    private const string NativeSortButtonName = "NeowtworkStatsNativeSortButton";
    private const string NativeSortHitboxName = "NeowtworkStatsNativeSortHitbox";
    private const float DefaultMenuButtonWidth = 280f;
    private const float MenuButtonVerticalOffset = 43f;

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

        Control? sidebar = alphabetSorter.GetParent() as Control;
        Control menuParent = sidebar ?? alphabetSorter;

        if (menuParent.GetNodeOrNull<MenuButton>(MenuButtonName) != null ||
            menuParent.GetNodeOrNull<NCardViewSortButton>(NativeSortButtonName) != null)
        {
            return;
        }

        alphabetSorter.GetNodeOrNull<OptionButton>(DropdownName)?.QueueFree();
        menuParent.GetNodeOrNull<OptionButton>(DropdownName)?.QueueFree();
        menuParent.GetNodeOrNull<MenuButton>(MenuButtonName)?.QueueFree();
        menuParent.GetNodeOrNull<NCardViewSortButton>(NativeSortButtonName)?.QueueFree();

        float menuButtonWidth = alphabetSorter.Size.X > 0f ? alphabetSorter.Size.X : DefaultMenuButtonWidth;
        float menuButtonHeight = alphabetSorter.Size.Y > 0f ? alphabetSorter.Size.Y : 42f;
        Vector2 controlPosition = sidebar == null
            ? new Vector2(0f, MenuButtonVerticalOffset)
            : alphabetSorter.Position + new Vector2(0f, MenuButtonVerticalOffset);

        NCardViewSortButton visualSortButton = (NCardViewSortButton)alphabetSorter.Duplicate(
            (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation));
        visualSortButton.Name = NativeSortButtonName;
        visualSortButton.Position = controlPosition;
        visualSortButton.Size = new Vector2(menuButtonWidth, menuButtonHeight);
        visualSortButton.CustomMinimumSize = new Vector2(menuButtonWidth, menuButtonHeight);
        visualSortButton.FocusMode = Control.FocusModeEnum.None;
        visualSortButton.MouseFilter = Control.MouseFilterEnum.Ignore;
        visualSortButton.ZIndex = alphabetSorter.ZIndex + 2;
        menuParent.AddChild(visualSortButton);

        MenuButton menuButton = new()
        {
            Name = MenuButtonName,
            Position = controlPosition,
            Size = new Vector2(menuButtonWidth, menuButtonHeight),
            CustomMinimumSize = new Vector2(menuButtonWidth, menuButtonHeight),
            FocusMode = Control.FocusModeEnum.None,
            ZIndex = alphabetSorter.ZIndex + 3,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        PopupMenu popup = menuButton.GetPopup();
        foreach (StatSortMetric metric in Enum.GetValues<StatSortMetric>())
        {
            popup.AddRadioCheckItem(GetMetricLabel(metric), (int)metric);
        }

        StyleTransparentOverlay(menuButton);
        StyleDropdownPopup(menuButton);
        UpdateMenuButtonState(menuButton, visualSortButton, grid);

        Button hitbox = new()
        {
            Name = NativeSortHitboxName,
            Position = Vector2.Zero,
            Size = new Vector2(menuButtonWidth, menuButtonHeight),
            CustomMinimumSize = new Vector2(menuButtonWidth, menuButtonHeight),
            FocusMode = Control.FocusModeEnum.None,
            ZIndex = 100,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        StyleTransparentButton(hitbox);
        hitbox.Pressed += () => menuButton.ShowPopup();
        visualSortButton.AddChild(hitbox);

        popup.IdPressed += id =>
        {
            StatSortMetric metric = (StatSortMetric)id;
            StatSortState currentState = GetSortState(grid);
            bool isSameMetric = currentState.IsActive && currentState.Metric == metric;

            StatSortStateByGrid[grid] = new StatSortState(
                IsActive: true,
                Metric: metric,
                IsReversed: isSameMetric && !currentState.IsReversed);

            UpdateMenuButtonState(menuButton, visualSortButton, grid);
            CacheDefaultOrder(grid);
            DisplayCardsMethod.Invoke(library, null);
        };

        menuParent.AddChild(menuButton);
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

    private static void UpdateMenuButtonState(MenuButton menuButton, NCardViewSortButton visualSortButton, NCardLibraryGrid grid)
    {
        StatSortState state = GetSortState(grid);
        visualSortButton.SetLabel(state.IsActive ? GetMetricLabel(state.Metric) : "Card Stats");
        visualSortButton.IsDescending = !state.IsReversed;
        menuButton.Text = "";

        PopupMenu popup = menuButton.GetPopup();
        for (int index = 0; index < popup.ItemCount; index++)
        {
            popup.SetItemChecked(index, state.IsActive && popup.GetItemId(index) == (int)state.Metric);
        }
    }

    private static void StyleTransparentOverlay(MenuButton dropdown)
    {
        StyleBoxEmpty empty = new();
        dropdown.Flat = true;
        dropdown.Alignment = HorizontalAlignment.Center;
        dropdown.AddThemeStyleboxOverride("normal", empty);
        dropdown.AddThemeStyleboxOverride("hover", empty);
        dropdown.AddThemeStyleboxOverride("pressed", empty);
        dropdown.AddThemeStyleboxOverride("disabled", empty);
        dropdown.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static void StyleTransparentButton(Button button)
    {
        StyleBoxEmpty empty = new();
        button.Flat = true;
        button.Text = "";
        button.AddThemeStyleboxOverride("normal", empty);
        button.AddThemeStyleboxOverride("hover", empty);
        button.AddThemeStyleboxOverride("pressed", empty);
        button.AddThemeStyleboxOverride("disabled", empty);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static void StyleDropdownPopup(MenuButton dropdown)
    {
        PopupMenu popup = dropdown.GetPopup();
        popup.AddThemeFontSizeOverride("font_size", 18);
        popup.AddThemeColorOverride("font_color", new Color(0.96f, 0.93f, 0.80f));
        popup.AddThemeColorOverride("font_hover_color", new Color(1f, 0.93f, 0.58f));
        popup.AddThemeColorOverride("font_outline_color", new Color(0.09f, 0.14f, 0.15f));
        popup.AddThemeConstantOverride("outline_size", 3);
        popup.AddThemeStyleboxOverride("panel", CreateSortHeaderStyle(new Color(0.10f, 0.27f, 0.30f), 0f));
        popup.AddThemeStyleboxOverride("hover", CreateSortHeaderStyle(new Color(0.16f, 0.47f, 0.50f), 0f));
    }

    private static StyleBoxFlat CreateSortHeaderStyle(Color color, float contentOffsetY)
    {
        StyleBoxFlat style = new()
        {
            BgColor = color,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.04f, 0.25f, 0.27f),
            ShadowColor = new Color(0f, 0f, 0f, 0.45f),
            ShadowSize = 3,
            ContentMarginTop = contentOffsetY,
            ContentMarginLeft = 12f,
            ContentMarginRight = 12f
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
