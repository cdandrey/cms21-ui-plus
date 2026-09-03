using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.ChoosePartDown;
using Il2CppCMS.UI.Windows;
#else
using CMS.Containers;
using CMS.UI.Logic;
using CMS.UI.Logic.ChoosePartDown;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    internal static class WheelBalancerInventoryFilterFeature
    {
        internal struct DownWindowShowState
        {
            internal bool IsWheelBalancerWindow;
            internal bool NeedsEmptyRefresh;
        }

        private static bool selectionActive;
        private static int activeSelectionDownWindowId;

        private static readonly SimplePartSelectionFilterController Controller =
            new SimplePartSelectionFilterController(
                ChoosePartUpWindowType.WheelBalance, IsEnabled, ShouldResetOnExit,
                "WheelBalancer", "Hint_ResetWheelBalancerFilters",
                "QWheelBalancerFilter", "QWheelBalancerEmptyState",
                "WheelBalancerInventoryFilter", "wheel-balancer",
                true, true, true, true);

        internal static void OnUpWindowShowPrefix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type)
        {
            selectionActive = type == ChoosePartUpWindowType.WheelBalance;
            activeSelectionDownWindowId = selectionActive && window != null &&
                window.choosePartDownWindow != null
                    ? window.choosePartDownWindow.GetInstanceID() : 0;
            Controller.OnUpWindowShowPrefix(window, type);
        }

        internal static void OnUpWindowShowPostfix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type, bool result)
        {
            if (type == ChoosePartUpWindowType.WheelBalance) {
                selectionActive = result;
                activeSelectionDownWindowId = result && window != null &&
                    window.choosePartDownWindow != null
                        ? window.choosePartDownWindow.GetInstanceID() : 0;
            }
            Controller.OnUpWindowShowPostfix(window, type, result);
        }

        internal static void OnUpWindowHidden(ChoosePartUpWindow window)
        {
            if (window != null && window.choosePartUpWindowType ==
                ChoosePartUpWindowType.WheelBalance) {
                selectionActive = false;
                activeSelectionDownWindowId = 0;
            }
            Controller.OnUpWindowHidden(window);
        }

        internal static DownWindowShowState PrepareNativeListForShow(
            ChoosePartDownWindow window,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items,
            ref int selectedIndex)
        {
            SimplePartSelectionFilterController.DownWindowShowState state =
                Controller.PrepareNativeListForShow(window, ref items,
                    ref selectedIndex);
            return new DownWindowShowState {
                IsWheelBalancerWindow = state.IsTargetWindow,
                NeedsEmptyRefresh = state.NeedsEmptyRefresh,
            };
        }

        internal static void OnWindowShown(ChoosePartDownWindow window,
            DownWindowShowState state)
        {
            Controller.OnWindowShown(window,
                new SimplePartSelectionFilterController.DownWindowShowState {
                    IsTargetWindow = state.IsWheelBalancerWindow,
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
            selectionActive = false;
            activeSelectionDownWindowId = 0;
            Controller.ResetAll();
        }

        internal static bool IsSelectionWindow(ChoosePartDownWindow window)
        {
            return window != null && selectionActive &&
                (activeSelectionDownWindowId == 0 ||
                 window.GetInstanceID() == activeSelectionDownWindowId);
        }

        internal static bool TryResetFromKeyboardShortcut()
        {
            return Controller.TryResetFromKeyboardShortcut();
        }

        private static bool ShouldResetOnExit()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.resetWheelBalancerInventoryFiltersOnExit;
        }

        private static bool IsEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addWheelBalancerInventoryFilters;
        }
    }
}
