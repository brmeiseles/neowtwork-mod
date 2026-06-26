using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using System.Reflection;

namespace Neowtwork.NeowtworkCode;

internal sealed class NeowtworkConfig : SimpleModConfig
{
    public enum ChoiceCardStatsDisplayMode
    {
        Off,
        Show,
        Hover
    }

    public static ChoiceCardStatsDisplayMode CardStatsDuringChoices { get; set; } = ChoiceCardStatsDisplayMode.Off;

    // BaseLib only lists config pages that have at least one visible property.
    // The UI is built manually below, so this exists only to make the Neowtwork page discoverable.
    [ConfigHideInUI]
    public static bool ImportTools { get; set; } = true;

    public override void SetupConfigUI(Control optionContainer)
    {
        optionContainer.AddChild(CreateSectionHeader("Card Stats", alignToTop: false));
        optionContainer.AddChild(CreateChoiceStatsModeRow());

        optionContainer.AddChild(CreateSectionHeader("Progress Import", alignToTop: false));

        MegaRichTextLabel statusLabel = CreateRawLabelControl(
            VanillaProgressImportAssistant.GetImportStatusText(),
            24);
        statusLabel.FitContent = true;
        statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statusLabel.CustomMinimumSize = new Vector2(0f, 180f);
        optionContainer.AddChild(statusLabel);

        optionContainer.AddChild(CreateButton(
            "Base Game Progress",
            "Import Base Game Progress",
            () => VanillaProgressImportAssistant.ShowManualImportDialog(MainFile.Logger)));

        optionContainer.AddChild(CreateButton(
            "Save Status",
            "Refresh Status",
            () =>
            {
                statusLabel.Text = VanillaProgressImportAssistant.GetImportStatusText();
            }));

        optionContainer.AddChild(CreateSectionHeader("Run Analytics Dump", alignToTop: false));

        MegaRichTextLabel analyticsLabel = CreateRawLabelControl(
            RunHistoryAnalytics.BuildDashboardText(),
            20);
        analyticsLabel.FitContent = true;
        analyticsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        analyticsLabel.CustomMinimumSize = new Vector2(0f, 960f);
        optionContainer.AddChild(analyticsLabel);

        optionContainer.AddChild(CreateButton(
            "Run Analytics",
            "Refresh Analytics",
            () =>
            {
                RunHistoryAnalytics.Refresh();
                analyticsLabel.Text = RunHistoryAnalytics.BuildDashboardText();
            }));

        SetupFocusNeighbors(optionContainer);
    }

    private NConfigOptionRow CreateChoiceStatsModeRow()
    {
        PropertyInfo property = typeof(NeowtworkConfig).GetProperty(nameof(CardStatsDuringChoices))!;
        Control dropdown = CreateRawDropdownControl(property);
        MegaRichTextLabel label = CreateRawLabelControl("Card Stats During Choices", 28);

        return new NConfigOptionRow(ModPrefix, nameof(CardStatsDuringChoices), label, dropdown);
    }
}
