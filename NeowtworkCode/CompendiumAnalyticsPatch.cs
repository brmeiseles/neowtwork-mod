using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace Neowtwork.NeowtworkCode;

[HarmonyPatch(typeof(NCompendiumSubmenu), nameof(NCompendiumSubmenu._Ready))]
internal static class CompendiumAnalyticsReadyPatch
{
    private static readonly HashSet<ulong> ConnectedButtons = [];

    private static void Postfix(NCompendiumSubmenu __instance)
    {
        NCompendiumBottomButton? button = GetAnalyticsButton(__instance);
        if (button is null)
        {
            return;
        }

        ApplyButtonText(button);

        ulong instanceId = button.GetInstanceId();
        if (ConnectedButtons.Add(instanceId))
        {
            button.Connect(
                NClickableControl.SignalName.Released,
                Callable.From<NButton>(_ => CompendiumAnalyticsNavigation.OpenAnalytics(__instance)));
        }
    }

    internal static NCompendiumBottomButton? GetAnalyticsButton(NCompendiumSubmenu submenu)
    {
        return submenu.GetNodeOrNull<NCompendiumBottomButton>("%LeaderboardsButton");
    }

    internal static void ApplyButtonText(NCompendiumBottomButton button)
    {
        button.Visible = true;
        MegaLabel? label = button.GetNodeOrNull<MegaLabel>("Label");
        label?.SetTextAutoSize("Neowtwork");
    }
}

[HarmonyPatch(typeof(NCompendiumSubmenu), nameof(NCompendiumSubmenu.OnSubmenuOpened))]
internal static class CompendiumAnalyticsOpenedPatch
{
    private static void Postfix(NCompendiumSubmenu __instance)
    {
        NCompendiumBottomButton? button = CompendiumAnalyticsReadyPatch.GetAnalyticsButton(__instance);
        if (button is null)
        {
            return;
        }

        CompendiumAnalyticsReadyPatch.ApplyButtonText(button);
    }
}

internal static class CompendiumAnalyticsNavigation
{
    private static readonly AccessTools.FieldRef<NSubmenu, NSubmenuStack> StackRef =
        AccessTools.FieldRefAccess<NSubmenu, NSubmenuStack>("_stack");

    public static void OpenAnalytics(NCompendiumSubmenu compendium)
    {
        NSubmenuStack stack = StackRef(compendium);
        NeowtworkAnalyticsScreen? screen = stack.GetNodeOrNull<NeowtworkAnalyticsScreen>(NeowtworkAnalyticsScreen.ScreenNodeName);
        if (screen is null)
        {
            screen = new NeowtworkAnalyticsScreen
            {
                Name = NeowtworkAnalyticsScreen.ScreenNodeName,
                Visible = false
            };
            stack.AddChild(screen);
        }

        stack.Push(screen);
    }
}
