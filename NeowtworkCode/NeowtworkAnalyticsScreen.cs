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
    private VBoxContainer? bodyContainer;
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

        bodyContainer = new VBoxContainer
        {
            Name = "Body",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        bodyContainer.AddThemeConstantOverride("separation", 18);
        scroll.AddChild(bodyContainer);

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
        if (bodyContainer is null)
        {
            return;
        }

        foreach (Node child in bodyContainer.GetChildren())
        {
            bodyContainer.RemoveChild(child);
            child.QueueFree();
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
        RunHistoryAnalytics.DashboardModel model = RunHistoryAnalytics.BuildDashboardModel(selectedTab, filter);
        BuildDashboard(model);
    }

    private void BuildDashboard(RunHistoryAnalytics.DashboardModel model)
    {
        if (bodyContainer is null)
        {
            return;
        }

        Label heading = CreateLabel(model.Title, 30, new Color(1f, 0.9f, 0.5f));
        heading.CustomMinimumSize = new Vector2(0f, 38f);
        bodyContainer.AddChild(heading);

        HBoxContainer cards = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 100f)
        };
        cards.AddThemeConstantOverride("separation", 14);
        bodyContainer.AddChild(cards);

        foreach (RunHistoryAnalytics.DashboardCard card in model.Cards)
        {
            cards.AddChild(CreateMetricCard(card));
        }

        foreach (RunHistoryAnalytics.DashboardSection section in model.Sections)
        {
            bodyContainer.AddChild(CreateSection(section));
        }
    }

    private static Control CreateMetricCard(RunHistoryAnalytics.DashboardCard card)
    {
        PanelContainer panel = CreatePanel(new Color(0.05f, 0.11f, 0.12f, 0.88f));
        panel.CustomMinimumSize = new Vector2(310f, 96f);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        box.AddChild(CreateLabel(card.Label, 18, new Color(0.95f, 0.78f, 0.28f)));
        box.AddChild(CreateLabel(card.Value, 26, Colors.White));
        box.AddChild(CreateLabel(card.Detail, 15, new Color(0.74f, 0.77f, 0.77f)));
        return panel;
    }

    private static Control CreateSection(RunHistoryAnalytics.DashboardSection section)
    {
        PanelContainer panel = CreatePanel(new Color(0.02f, 0.03f, 0.035f, 0.76f));
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        box.AddChild(CreateLabel(section.Title, 25, new Color(1f, 0.9f, 0.5f)));
        if (!string.IsNullOrWhiteSpace(section.Subtitle))
        {
            Label subtitle = CreateLabel(section.Subtitle, 16, new Color(0.72f, 0.76f, 0.76f));
            subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(subtitle);
        }

        if (section.Rows.Count == 0)
        {
            box.AddChild(CreateLabel("Not enough data yet.", 20, new Color(0.75f, 0.75f, 0.72f)));
            return panel;
        }

        foreach (RunHistoryAnalytics.DashboardRow row in section.Rows)
        {
            box.AddChild(CreateDashboardRow(row));
        }

        return panel;
    }

    private static Control CreateDashboardRow(RunHistoryAnalytics.DashboardRow row)
    {
        VBoxContainer outer = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, row.ChartValue.HasValue ? 56f : 42f)
        };
        outer.AddThemeConstantOverride("separation", 3);

        HBoxContainer line = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        outer.AddChild(line);

        Label label = CreateLabel(row.Label, 19, Colors.White);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.AddChild(label);

        Label value = CreateLabel(row.Value, 19, new Color(1f, 0.9f, 0.5f));
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.CustomMinimumSize = new Vector2(220f, 0f);
        line.AddChild(value);

        Label detail = CreateLabel(row.Detail, 14, new Color(0.72f, 0.76f, 0.76f));
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        outer.AddChild(detail);

        if (row.ChartValue.HasValue)
        {
            ProgressBar bar = new()
            {
                MinValue = 0d,
                MaxValue = 1d,
                Value = Math.Clamp(row.ChartValue.Value, 0d, 1d),
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(0f, 9f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            };
            outer.AddChild(bar);
        }

        return outer;
    }

    private static Label CreateLabel(string text, int fontSize, Color color)
    {
        Label label = new()
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static PanelContainer CreatePanel(Color background)
    {
        PanelContainer panel = new();
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = new Color(0.1f, 0.44f, 0.48f, 0.85f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14f,
            ContentMarginTop = 12f,
            ContentMarginRight = 14f,
            ContentMarginBottom = 12f
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
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
        return RunHistoryAnalytics.PrettyName(id);
    }
}
