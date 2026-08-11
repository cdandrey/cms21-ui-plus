using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
#else
using CMS;
#endif

namespace Cms21UiPlus
{
    public static class QuickMountModeSwitchFeature
    {
        public static void Update()
        {
            if (!IsEnabled || !GlobalState.IsGarageSceneActive)
                return;

            if (!WasBindingReleased())
                return;

            GameMode gameModeManager = GameMode.Get();
            if (gameModeManager == null)
                return;

            switch (gameModeManager.currentMode) {
                case gameMode.InteriorDisassemble:
                    gameModeManager.SetCurrentMode(gameMode.InteriorAssemble);
                    break;
                case gameMode.InteriorAssemble:
                    gameModeManager.SetCurrentMode(gameMode.InteriorDisassemble);
                    break;
                case gameMode.PartSelectMount:
                    gameModeManager.SetCurrentMode(gameMode.PartSelect);
                    break;
                case gameMode.PartSelect:
                    gameModeManager.SetCurrentMode(gameMode.PartSelectMount);
                    break;
                case gameMode.BonusDisassemble:
                    gameModeManager.SetCurrentMode(gameMode.BonusAssemble);
                    break;
                case gameMode.BonusAssemble:
                    gameModeManager.SetCurrentMode(gameMode.BonusDisassemble);
                    break;
                case gameMode.GarageDisassemble:
                    gameModeManager.SetCurrentMode(gameMode.GarageAssemble);
                    break;
                case gameMode.GarageAssemble:
                    gameModeManager.SetCurrentMode(gameMode.GarageDisassemble);
                    break;
            }
        }

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.quickSwitchMountModes;
            }
        }

        private static bool WasBindingReleased()
        {
            KeyCode primary = KeyBindingsConfig.QuickSwitchPrimary;
            KeyCode secondary = KeyBindingsConfig.QuickSwitchSecondary;

            return (primary != KeyCode.None && Input.GetKeyUp(primary)) ||
                (secondary != KeyCode.None && secondary != primary &&
                    Input.GetKeyUp(secondary));
        }
    }
}
