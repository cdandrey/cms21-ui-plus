using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.ChoosePartDown;
using Il2CppCMS.UI.Logic.Paging;
using Il2CppCMS.UI.Windows;
#else
using CMS.Containers;
using CMS.UI.Logic;
using CMS.UI.Logic.ChoosePartDown;
using CMS.UI.Logic.Paging;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    internal static class TireChangerInventoryFilterFeature
    {
        internal struct DownWindowShowState
        {
            internal bool IsTireChangerWindow;
            internal bool NeedsEmptyRefresh;
        }

        private static readonly AssemblyPartSelectionFilterController Controller =
            new AssemblyPartSelectionFilterController(
                ChoosePartUpWindowType.WheelConnect,
                ChoosePartUpWindowType.WheelSeparate, IsEnabled, ShouldResetOnExit,
                "TireChanger", "Hint_ResetTireChangerFilters", "QTireChangerFilter",
                "QTireChangerSeparateEmptyState", "QTireChangerSelectionPrompt",
                "QTireChangerAssemblyEmptyState", "TireChangerInventoryFilter",
                "tire-changer", false);

        internal static void OnUpWindowShowPrefix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type)
        {
            Controller.OnUpWindowShowPrefix(window, type);
        }

        internal static void OnUpWindowShowPostfix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type, bool result)
        {
            Controller.OnUpWindowShowPostfix(window, type, result);
        }

        internal static void OnUpWindowHidden(ChoosePartUpWindow window)
        {
            Controller.OnUpWindowHidden(window);
        }

        internal static DownWindowShowState PrepareNativeListForShow(
            ChoosePartDownWindow window,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items,
            ref int selectedIndex)
        {
            AssemblyPartSelectionFilterController.DownWindowShowState state =
                Controller.PrepareNativeListForShow(window, ref items,
                    ref selectedIndex);
            return new DownWindowShowState {
                IsTireChangerWindow = state.IsTargetWindow,
                NeedsEmptyRefresh = state.NeedsEmptyRefresh,
            };
        }

        internal static void OnWindowShown(ChoosePartDownWindow window,
            DownWindowShowState state)
        {
            Controller.OnWindowShown(window,
                new AssemblyPartSelectionFilterController.DownWindowShowState {
                    IsTargetWindow = state.IsTireChangerWindow,
                    NeedsEmptyRefresh = state.NeedsEmptyRefresh,
                });
        }

        internal static void FilterNativeListBeforeRefresh(
            ChoosePartPageManager pageManager,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            Controller.FilterNativeListBeforeRefresh(pageManager, ref items);
        }

        internal static void OnNativeListRefreshed(
            ChoosePartPageManager pageManager,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            Controller.OnNativeListRefreshed(pageManager, items);
        }

        internal static void OnInputFieldKeyPressed(InputField inputField)
        {
            Controller.OnInputFieldKeyPressed(inputField);
        }

        internal static bool ShouldSuppressNativeSelection(
            ChoosePartUpWindow window, ChoosePartDownItem item)
        {
            return Controller.ShouldSuppressNativeSelection(window, item);
        }

        internal static bool ShouldSuppressSubmit(ChoosePartUpWindow window)
        {
            return Controller.ShouldSuppressSubmit(window);
        }

        internal static void ResetAll()
        {
            Controller.ResetAll();
        }

        internal static bool TryResetFromKeyboardShortcut()
        {
            return Controller.TryResetFromKeyboardShortcut();
        }

        private static bool ShouldResetOnExit()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.resetTireChangerInventoryFiltersOnExit;
        }

        private static bool IsEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addTireChangerInventoryFilters;
        }
    }
}
