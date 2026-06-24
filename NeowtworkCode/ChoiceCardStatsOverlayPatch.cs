using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace Neowtwork.NeowtworkCode;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals), typeof(PileType), typeof(CardPreviewMode))]
internal static class ChoiceCardStatsNCardUpdateVisualsPatch
{
    private static void Postfix(NCard __instance)
    {
        ChoiceCardStatsOverlay.Apply(__instance);
    }
}

[HarmonyPatch(typeof(NCard), "OnFreedToPool")]
internal static class ChoiceCardStatsNCardFreedPatch
{
    private static void Postfix(NCard __instance)
    {
        ChoiceCardStatsOverlay.Forget(__instance);
    }
}

[HarmonyPatch(typeof(NCardHolder), "DoCardHoverEffects")]
internal static class ChoiceCardStatsHolderHoverPatch
{
    private static void Postfix(NCardHolder __instance, bool isHovered)
    {
        ChoiceCardStatsOverlay.SetHovered(__instance.CardNode, isHovered);
    }
}

[HarmonyPatch(typeof(NPreviewCardHolder), "OnFocus")]
internal static class ChoiceCardStatsPreviewHolderFocusPatch
{
    private static void Postfix(NPreviewCardHolder __instance)
    {
        ChoiceCardStatsOverlay.SetHovered(__instance.CardNode, true);
    }
}

[HarmonyPatch(typeof(NPreviewCardHolder), "OnUnfocus")]
internal static class ChoiceCardStatsPreviewHolderUnfocusPatch
{
    private static void Postfix(NPreviewCardHolder __instance)
    {
        ChoiceCardStatsOverlay.SetHovered(__instance.CardNode, false);
    }
}

[HarmonyPatch(typeof(NMerchantCard), "UpdateVisual")]
internal static class ChoiceCardStatsMerchantCardUpdatePatch
{
    private static readonly AccessTools.FieldRef<NMerchantCard, NCard?> CardNodeRef =
        AccessTools.FieldRefAccess<NMerchantCard, NCard?>("_cardNode");

    private static void Postfix(NMerchantCard __instance)
    {
        ChoiceCardStatsOverlay.Apply(CardNodeRef(__instance));
    }
}

[HarmonyPatch(typeof(NMerchantSlot), "OnFocus")]
internal static class ChoiceCardStatsMerchantSlotFocusPatch
{
    private static readonly AccessTools.FieldRef<NMerchantCard, NCard?> CardNodeRef =
        AccessTools.FieldRefAccess<NMerchantCard, NCard?>("_cardNode");

    private static void Postfix(NMerchantSlot __instance)
    {
        if (__instance is NMerchantCard merchantCard)
        {
            ChoiceCardStatsOverlay.SetHovered(CardNodeRef(merchantCard), true);
        }
    }
}

[HarmonyPatch(typeof(NMerchantSlot), "OnUnfocus")]
internal static class ChoiceCardStatsMerchantSlotUnfocusPatch
{
    private static readonly AccessTools.FieldRef<NMerchantCard, NCard?> CardNodeRef =
        AccessTools.FieldRefAccess<NMerchantCard, NCard?>("_cardNode");

    private static void Postfix(NMerchantSlot __instance)
    {
        if (__instance is NMerchantCard merchantCard)
        {
            ChoiceCardStatsOverlay.SetHovered(CardNodeRef(merchantCard), false);
        }
    }
}

internal static class ChoiceCardStatsOverlay
{
    private const string OverlayName = "NeowtworkChoiceCardStatsOverlay";
    private const float OverlayWidth = 270f;
    private const float OverlayHeight = 172f;
    private const float OverlayX = 15f;
    private const float OverlayY = -165f;
    private const float LabelTopPadding = 8f;

    private static readonly HashSet<NCard> HoveredCards = [];

    public static void Apply(NCard? card)
    {
        if (card == null || card.Model == null || !GodotObject.IsInstanceValid(card))
        {
            return;
        }

        NeowtworkConfig.ChoiceCardStatsDisplayMode mode = NeowtworkConfig.CardStatsDuringChoices;
        if (mode == NeowtworkConfig.ChoiceCardStatsDisplayMode.Off || !IsSupportedChoiceCard(card))
        {
            Remove(card);
            return;
        }

        Control overlay = EnsureOverlay(card);
        ConfigureOverlay(overlay);
        Label label = overlay.GetNode<Label>("Label");
        label.Text = CardStatsText.Build(card.Model);
        overlay.Visible = mode == NeowtworkConfig.ChoiceCardStatsDisplayMode.Show ||
                          HoveredCards.Contains(card);
    }

    public static void SetHovered(NCard? card, bool isHovered)
    {
        if (card == null || !GodotObject.IsInstanceValid(card))
        {
            return;
        }

        if (isHovered)
        {
            HoveredCards.Add(card);
        }
        else
        {
            HoveredCards.Remove(card);
        }

        Apply(card);
    }

    public static void Forget(NCard card)
    {
        HoveredCards.Remove(card);
        Remove(card);
    }

    private static Control EnsureOverlay(NCard card)
    {
        Control? existing = card.GetNodeOrNull<Control>(OverlayName);
        if (existing != null)
        {
            ConfigureOverlay(existing);
            return existing;
        }

        Control overlay = new()
        {
            Name = OverlayName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 200
        };

        ColorRect background = new()
        {
            Name = "Background",
            Color = Colors.Black,
            Position = Vector2.Zero,
            Size = overlay.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        overlay.AddChild(background);

        Label label = new()
        {
            Name = "Label",
            Position = new Vector2(4f, 0f),
            Size = new Vector2(OverlayWidth - 8f, OverlayHeight - LabelTopPadding),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color(0.98f, 0.94f, 0.84f));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        overlay.AddChild(label);

        ConfigureOverlay(overlay);
        card.AddChild(overlay);
        return overlay;
    }

    private static void ConfigureOverlay(Control overlay)
    {
        overlay.Position = new Vector2(OverlayX, OverlayY);
        overlay.Size = new Vector2(OverlayWidth, OverlayHeight);
        overlay.CustomMinimumSize = new Vector2(OverlayWidth, OverlayHeight);

        if (overlay.GetNodeOrNull<ColorRect>("Background") is { } background)
        {
            background.Position = Vector2.Zero;
            background.Size = overlay.Size;
        }

        if (overlay.GetNodeOrNull<Label>("Label") is { } label)
        {
            label.Position = new Vector2(4f, 0f);
            label.Size = new Vector2(OverlayWidth - 8f, OverlayHeight - LabelTopPadding);
            label.AddThemeFontSizeOverride("font_size", 22);
        }
    }

    private static void Remove(NCard card)
    {
        card.GetNodeOrNull<Control>(OverlayName)?.QueueFree();
        HoveredCards.Remove(card);
    }

    private static bool IsSupportedChoiceCard(NCard card)
    {
        if (card.Model == null ||
            card.Visibility != ModelVisibility.Visible)
        {
            return false;
        }

        // This feature is for "should I take/buy/choose this card?" moments.
        // Do not add stats to cards the player already owns or is using, such as combat hand,
        // deck, draw pile, discard pile, exhaust pile, or Card Library views.
        if (card.DisplayingPile != PileType.None ||
            card.Model.Pile != null)
        {
            return false;
        }

        if (HasAncestor<NHandCardHolder>(card) ||
            HasAncestor<NCardLibrary>(card) ||
            HasAncestor<NCardLibraryGrid>(card))
        {
            return false;
        }

        return HasAncestorByName(card, "NCardRewardSelectionScreen") ||
               HasAncestorByName(card, "NCardGridSelectionScreen") ||
               HasAncestorByName(card, "NCardsViewScreen") ||
               HasAncestorByName(card, "NSimpleCardsViewScreen") ||
               HasAncestorByName(card, "NMerchantCard") ||
               HasAncestorByName(card, "NCardPreviewContainer") ||
               HasAncestorByName(card, "NEvent");
    }

    private static bool HasAncestor<T>(Node node) where T : Node
    {
        for (Node? current = node.GetParent(); current != null; current = current.GetParent())
        {
            if (current is T)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAncestorByName(Node node, string typeName)
    {
        for (Node? current = node.GetParent(); current != null; current = current.GetParent())
        {
            if (current.GetType().Name.Contains(typeName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
