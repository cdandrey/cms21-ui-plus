using System;
using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.Paintshop.Tabs;
#else
using CMS;
using CMS.UI.Logic;
using CMS.UI.Logic.Paintshop.Tabs;
#endif

namespace Cms21UiPlus
{
    /// <summary>Replaces numeric livery entries with normalized livery file names.</summary>
    [HarmonyPatch]
    public static class LiveryNamesFeature
    {
        [HarmonyPatch(typeof(LiveriesTab), nameof(LiveriesTab.PrepareLiveries))]
        [HarmonyPostfix]
        public static void PrepareLiveriesPostfix(LiveriesTab __instance)
        {
            if (Main.SettingsEntry == null || !Main.SettingsEntry.Value.showLiveryFileNames ||
                __instance == null)
                return;

            int availableOptions = __instance.liverySelector.options.Count - 1;
            int count = Math.Min(__instance.liveries.Count, availableOptions);
            for (int i = 0; i < count; i++) {
                string name = __instance.liveries[i].Name;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                __instance.liverySelector.options[i + 1] =
                    TextFormatting.ToTitleCase(name).Replace("_", " ");
            }
        }
    }
}
