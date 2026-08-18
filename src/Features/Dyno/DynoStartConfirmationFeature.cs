using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.UI;
#else
using CMS;
using CMS.UI;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    public static class DynoStartConfirmationFeature
    {
        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.autoConfirmDynoStart &&
                    GlobalState.IsGarageSceneActive;
            }
        }

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.ShowAskWindow))]
        [HarmonyPrefix]
        public static void ShowAskWindowPrefix(string description, ref bool withSound)
        {
            if (IsDynoStartConfirmation(description))
                withSound = false;
        }

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.ShowAskWindow))]
        [HarmonyPostfix]
        public static void ShowAskWindowPostfix(UIManager __instance, string description)
        {
            if (!IsDynoStartConfirmation(description) || __instance == null ||
                __instance.AskWindow == null)
                return;

            __instance.AskWindow.withSound = false;
            __instance.AskWindow.AcceptAction();
        }

        private static bool IsDynoStartConfirmation(string description)
        {
            if (!IsEnabled || GlobalState.GameManager == null)
                return false;

            return description == GlobalState.GameManager.Localization.GetLocalizedValue(
                "GUI_PotwierdzenieRozpoczeciaDyno");
        }
    }
}
