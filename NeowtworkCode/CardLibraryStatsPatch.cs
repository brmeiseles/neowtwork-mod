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
        LabelRef(__instance).Text = CardStatsText.Build(card);

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
}
