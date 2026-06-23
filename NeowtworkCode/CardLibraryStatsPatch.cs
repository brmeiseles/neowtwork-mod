using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;

namespace Neowtwork.NeowtworkCode;

[HarmonyPatch(typeof(NCardLibraryStats), nameof(NCardLibraryStats._Ready))]
internal static class CardLibraryStatsReadyPatch
{
    private static void Postfix(NCardLibraryStats __instance)
    {
        CardLibraryStatsPatch.ConfigureOverlay(__instance);
    }
}

[HarmonyPatch(typeof(NCardLibraryStats), nameof(NCardLibraryStats.UpdateStats))]
internal static class CardLibraryStatsPatch
{
    private const float OverlayHeight = 172f;
    private const float LabelTopPadding = 8f;

    private static readonly AccessTools.FieldRef<NCardLibraryStats, MegaRichTextLabel> LabelRef =
        AccessTools.FieldRefAccess<NCardLibraryStats, MegaRichTextLabel>("_label");

    private static bool Prefix(NCardLibraryStats __instance, CardModel card)
    {
        SaveManager.Instance.Progress.CardStats.TryGetValue(card.Id, out CardStats? stats);

        long victories = stats?.TimesWon ?? 0;
        long losses = stats?.TimesLost ?? 0;
        long picked = stats?.TimesPicked ?? 0;
        long skipped = stats?.TimesSkipped ?? 0;
        long finishedRuns = victories + losses;
        long seen = picked + skipped;
        string winRate = finishedRuns == 0 ? "— Win Rate" : $"{(double)victories / finishedRuns:P0} Win Rate";
        string pickRate = seen == 0 ? "— Pick Rate" : $"{(double)picked / seen:P0} Pick Rate";

        LabelRef(__instance).Text =
            $"{winRate}\n" +
            $"({victories} {Pluralize(victories, "victory", "victories")} - {losses} {Pluralize(losses, "loss", "losses")})\n\n" +
            $"{pickRate}\n" +
            $"({picked} {Pluralize(picked, "pick", "picks")} / {seen} seen)";

        ConfigureOverlay(__instance);

        return false;
    }

    internal static void ConfigureOverlay(NCardLibraryStats statsNode)
    {
        MegaRichTextLabel label = LabelRef(statsNode);
        label.AddThemeFontSizeOverride("normal_font_size", 22);
        label.Position = new Vector2(label.Position.X, 0f);
        label.CustomMinimumSize = new Vector2(label.CustomMinimumSize.X, OverlayHeight - LabelTopPadding);
        label.Size = new Vector2(label.Size.X, OverlayHeight - LabelTopPadding);

        ConfigureBackground(statsNode);
    }

    private static void ConfigureBackground(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is ColorRect colorRect)
            {
                Color color = colorRect.Color;
                colorRect.Color = new Color(color.R, color.G, color.B, 1f);
                colorRect.Size = new Vector2(colorRect.Size.X, OverlayHeight);
            }

            ConfigureBackground(child);
        }
    }

    private static string Pluralize(long count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }
}
