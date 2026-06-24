using BaseLib.Config;
using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace Neowtwork.NeowtworkCode;

internal sealed class NeowtworkConfig : SimpleModConfig
{
    // BaseLib only lists config pages that have at least one visible property.
    // The UI is built manually below, so this exists only to make the Neowtwork page discoverable.
    public static bool ImportTools { get; set; } = true;

    public override void SetupConfigUI(Control optionContainer)
    {
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

        SetupFocusNeighbors(optionContainer);
    }
}
