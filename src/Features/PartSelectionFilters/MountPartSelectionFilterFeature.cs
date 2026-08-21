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
    internal static class MountPartSelectionFilterFeature
    {
        internal struct DownWindowShowState
        {
            internal bool IsMountWindow;
            internal bool NeedsEmptyRefresh;
        }

        private static readonly SimplePartSelectionFilterController Controller =
            new SimplePartSelectionFilterController(
                ChoosePartUpWindowType.Mount, IsEnabled, ShouldResetOnExit,
                "MountPartSelection", "Hint_ResetMountPartFilters",
                "QMountPartFilter", "QMountPartEmptyState",
                "MountPartSelectionFilter", "mount-part",
                false, true, false, false);

        internal static void OnUpWindowShowPrefix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type, string overload)
        {
            Controller.OnUpWindowShowPrefix(window, type);
        }

        internal static void OnUpWindowShowPostfix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type, bool result, string overload)
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
            SimplePartSelectionFilterController.DownWindowShowState state =
                Controller.PrepareNativeListForShow(window, ref items,
                    ref selectedIndex);
            return new DownWindowShowState {
                IsMountWindow = state.IsTargetWindow,
                NeedsEmptyRefresh = state.NeedsEmptyRefresh,
            };
        }

        internal static void OnWindowShown(ChoosePartDownWindow window,
            DownWindowShowState state)
        {
            Controller.OnWindowShown(window,
                new SimplePartSelectionFilterController.DownWindowShowState {
                    IsTargetWindow = state.IsMountWindow,
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
                Main.SettingsEntry.Value.resetMountPartSelectionFiltersOnExit;
        }

        private static bool IsEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addMountPartSelectionFilters;
        }
    }
}
