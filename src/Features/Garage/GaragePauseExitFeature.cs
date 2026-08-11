using System;
using HarmonyLib;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2CppCMS.UI;
using Il2CppCMS.UI.Controls;
using Il2CppCMS.UI.Windows;
#else
using CMS.UI;
using CMS.UI.Controls;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    public static class GaragePauseExitFeature
    {
        private const string ExitButtonPath =
            "Window/Bottom/GenericButtonOutline (5)";
        private const string ContinueButtonPath =
            "Window/Bottom/GenericButtonOutline";

        private static PauseQuitWindow currentPauseWindow;

        public static void OnGarageSceneUnloaded()
        {
            currentPauseWindow = null;
        }

        [HarmonyPatch(typeof(PauseQuitWindow), nameof(PauseQuitWindow.Prepare))]
        [HarmonyPostfix]
        public static void PauseQuitWindowPreparePostfix(PauseQuitWindow __instance)
        {
            if (!IsEnabled || !GlobalState.IsGarageSceneActive || __instance == null)
                return;

            Transform buttonTransform = __instance.transform.Find(ExitButtonPath);
            if (buttonTransform == null) {
                ModLogger.Log("[GaragePauseExit] Reserved pause-menu button was not found.",
                    Types.LoggingLevels.Warning);
                return;
            }

            GenericButtonOutline button =
                buttonTransform.GetComponent<GenericButtonOutline>();
            if (button == null)
                return;

            currentPauseWindow = __instance;
            buttonTransform.gameObject.SetActive(true);
            UnityEventUtility.RemoveAllListeners(button);

            UnityEngine.UI.Text label =
                buttonTransform.GetComponentInChildren<UnityEngine.UI.Text>();
            if (label != null && GlobalState.GameManager != null)
                label.text = TextFormatting.ToTitleCase(
                    GlobalState.GameManager.Localization.GetLocalizedValue(
                        "GUI_Exitgame"));

            button.OnClick.AddListener(new Action(ShowExitConfirmation));
        }

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addExitGameToGaragePauseMenu;
            }
        }

        private static void ShowExitConfirmation()
        {
            if (UIManager.Get() == null || GlobalState.GameManager == null)
                return;

            UIManager.Get().ShowAskWindow(
                GlobalState.GameManager.Localization.GetLocalizedValue(
                    "GUI_ConfirmExitGameTitle"),
                GlobalState.GameManager.Localization.GetLocalizedValue(
                    "GUI_ConfirmExitGame"),
                new Action<bool>(HandleExitConfirmation),
                true);
        }

        private static void HandleExitConfirmation(bool confirmed)
        {
            if (confirmed) {
                Application.Quit();
                return;
            }

            if (currentPauseWindow == null)
                return;

            Transform continueTransform =
                currentPauseWindow.transform.Find(ContinueButtonPath);
            if (continueTransform == null)
                return;

            GenericButtonOutline continueButton =
                continueTransform.GetComponent<GenericButtonOutline>();
            if (continueButton != null)
                continueButton.InvokeAction();
        }
    }
}
