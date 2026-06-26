using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace Neowtwork.NeowtworkCode;

internal partial class NeowtworkAnalyticsScreen : NSubmenu
{
    public const string ScreenNodeName = "NeowtworkAnalyticsScreen";

    private readonly List<Button> tabButtons = [];
    private readonly List<string> characterValues = [];
    private readonly List<int?> ascensionValues = [];

    private Button? backButton;
    private Label? titleLabel;
    private Label? bodyLabel;
    private OptionButton? characterFilter;
    private OptionButton? ascensionFilter;
    private OptionButton? playerModeFilter;
    private OptionButton? resultFilter;
    private RunHistoryAnalytics.DashboardTab selectedTab = RunHistoryAnalytics.DashboardTab.Overview;

    protected override Control? InitialFocusedControl => tabButtons.Count > 0 ? tabButtons[0] : backButton;

    public override void _Ready()
    {
        Name = ScreenNodeName;
        AnchorRight = 1f;
        AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Stop;

        ColorRect shade = new()
        {
            Name = "Shade",
            Color = new Color(0f, 0f, 0f, 0.62f),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(shade);

        backButton = new Button
        {
            Name = "BackButton",
            Text = "←",
            Position = new Vector2(34f, 790f),
            CustomMinimumSize = new Vector2(130f, 72f)
        };
        backButton.Pressed += () => _stack?.Pop();
        AddChild(backButton);

        titleLabel = new Label
        {
            Text = "Neowtwork",
            Position = new Vector2(300f, 86f),
            Size = new Vector2(900f, 62f)
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 48);
        AddChild(titleLabel);

        HBoxContainer filters = new()
        {
            Name = "Filters",
            Position = new Vector2(300f, 160f),
            Size = new Vector2(1330f, 52f)
        };
        filters.AddThemeConstantOverride("separation", 14);
        AddChild(filters);

        characterFilter = CreateOptionButton(220f, RefreshBody);
        ascensionFilter = CreateOptionButton(170f, RefreshBody);
        playerModeFilter = CreateOptionButton(210f, RefreshBody);
        resultFilter = CreateOptionButton(170f, RefreshBody);
        filters.AddChild(characterFilter);
        filters.AddChild(ascensionFilter);
        filters.AddChild(playerModeFilter);
        filters.AddChild(resultFilter);

        HBoxContainer tabs = new()
        {
            Name = "Tabs",
            Position = new Vector2(300f, 228f),
            Size = new Vector2(1370f, 56f)
        };
        tabs.AddThemeConstantOverride("separation", 8);
        AddChild(tabs);

        foreach (RunHistoryAnalytics.DashboardTab tab in Enum.GetValues<RunHistoryAnalytics.DashboardTab>())
        {
            Button button = new()
            {
                Text = TabLabel(tab),
                ToggleMode = true,
                CustomMinimumSize = new Vector2(tab == RunHistoryAnalytics.DashboardTab.Combos ? 178f : 130f, 46f)
            };
            button.Pressed += () => SelectTab(tab);
            tabButtons.Add(button);
            tabs.AddChild(button);
        }

        ScrollContainer scroll = new()
        {
            Name = "Scroll",
            Position = new Vector2(300f, 304f),
            Size = new Vector2(1390f, 620f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        AddChild(scroll);

        bodyLabel = new Label
        {
            Name = "Body",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        bodyLabel.AddThemeFontSizeOverride("font_size", 24);
        scroll.AddChild(bodyLabel);

        PopulateFilters();
        RefreshTabs();
        RefreshBody();
    }

    public override void OnSubmenuOpened()
    {
        base.OnSubmenuOpened();
        RunHistoryAnalytics.GetIndex();
        PopulateFilters();
        RefreshTabs();
        RefreshBody();
    }

    private static OptionButton CreateOptionButton(float width, Action onChanged)
    {
        OptionButton button = new()
        {
            CustomMinimumSize = new Vector2(width, 44f)
        };
        button.ItemSelected += _ => onChanged();
        return button;
    }

    private void PopulateFilters()
    {
        int previousCharacter = characterFilter?.Selected ?? 0;
        int previousAscension = ascensionFilter?.Selected ?? 0;
        int previousPlayerMode = playerModeFilter?.Selected ?? 0;
        int previousResult = resultFilter?.Selected ?? 0;

        characterValues.Clear();
        characterFilter?.Clear();
        characterFilter?.AddItem("All Characters");
        characterValues.Add("");
        foreach (string character in RunHistoryAnalytics.GetFilterCharacters())
        {
            characterFilter?.AddItem(PrettyId(character));
            characterValues.Add(character);
        }

        ascensionValues.Clear();
        ascensionFilter?.Clear();
        ascensionFilter?.AddItem("All Ascensions");
        ascensionValues.Add(null);
        foreach (int ascension in RunHistoryAnalytics.GetFilterAscensions())
        {
            ascensionFilter?.AddItem($"A{ascension}");
            ascensionValues.Add(ascension);
        }

        playerModeFilter?.Clear();
        playerModeFilter?.AddItem("All Modes");
        playerModeFilter?.AddItem("Singleplayer");
        playerModeFilter?.AddItem("Multiplayer");

        resultFilter?.Clear();
        resultFilter?.AddItem("Win + Loss");
        resultFilter?.AddItem("Wins");
        resultFilter?.AddItem("Losses");

        SetSelectedSafely(characterFilter, previousCharacter);
        SetSelectedSafely(ascensionFilter, previousAscension);
        SetSelectedSafely(playerModeFilter, previousPlayerMode);
        SetSelectedSafely(resultFilter, previousResult);
    }

    private void SelectTab(RunHistoryAnalytics.DashboardTab tab)
    {
        selectedTab = tab;
        RefreshTabs();
        RefreshBody();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            RunHistoryAnalytics.DashboardTab tab = (RunHistoryAnalytics.DashboardTab)i;
            tabButtons[i].ButtonPressed = tab == selectedTab;
        }
    }

    private void RefreshBody()
    {
        if (bodyLabel is null)
        {
            return;
        }

        string? character = null;
        if (characterFilter is not null &&
            characterFilter.Selected > 0 &&
            characterFilter.Selected < characterValues.Count)
        {
            character = characterValues[characterFilter.Selected];
        }

        int? ascension = null;
        if (ascensionFilter is not null &&
            ascensionFilter.Selected >= 0 &&
            ascensionFilter.Selected < ascensionValues.Count)
        {
            ascension = ascensionValues[ascensionFilter.Selected];
        }

        RunHistoryAnalytics.PlayerModeFilter playerMode = playerModeFilter?.Selected switch
        {
            1 => RunHistoryAnalytics.PlayerModeFilter.Singleplayer,
            2 => RunHistoryAnalytics.PlayerModeFilter.Multiplayer,
            _ => RunHistoryAnalytics.PlayerModeFilter.All
        };

        RunHistoryAnalytics.RunResultFilter result = resultFilter?.Selected switch
        {
            1 => RunHistoryAnalytics.RunResultFilter.Wins,
            2 => RunHistoryAnalytics.RunResultFilter.Losses,
            _ => RunHistoryAnalytics.RunResultFilter.All
        };

        RunHistoryAnalytics.DashboardFilter filter = new(character, ascension, playerMode, result);
        bodyLabel.Text = RunHistoryAnalytics.BuildDashboardText(selectedTab, filter);
    }

    private static void SetSelectedSafely(OptionButton? button, int selected)
    {
        if (button is null || button.ItemCount <= 0)
        {
            return;
        }

        button.Selected = Math.Clamp(selected, 0, button.ItemCount - 1);
    }

    private static string TabLabel(RunHistoryAnalytics.DashboardTab tab)
    {
        return tab switch
        {
            RunHistoryAnalytics.DashboardTab.Overview => "Overview",
            RunHistoryAnalytics.DashboardTab.Cards => "Cards",
            RunHistoryAnalytics.DashboardTab.Relics => "Relics",
            RunHistoryAnalytics.DashboardTab.Combos => "Combos",
            RunHistoryAnalytics.DashboardTab.Ancients => "Ancients",
            RunHistoryAnalytics.DashboardTab.Events => "Events",
            RunHistoryAnalytics.DashboardTab.Monsters => "Monsters",
            RunHistoryAnalytics.DashboardTab.Shops => "Shops",
            _ => tab.ToString()
        };
    }

    private static string PrettyId(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? "Unknown"
            : id.Split('/')[^1].Replace("_", " ", StringComparison.Ordinal).Replace("-", " ", StringComparison.Ordinal);
    }
}
