using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
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

        private const string WindowId = "MountPartSelection";
        private const string ResetHintId = "Hint_ResetMountPartFilters";
        private const float PanelVerticalOffset = 8f;

        private static readonly List<ChoosePartDownItem> OriginalItems =
            new List<ChoosePartDownItem>();
        private static readonly PartFilterPanelController Panel =
            new PartFilterPanelController("QMountPartFilter");
        private static readonly List<CurrentDetailObjectState>
            HiddenCurrentDetailObjects = new List<CurrentDetailObjectState>(5);
        private static readonly FieldInfo CurrentItemField =
            typeof(ChoosePartUpWindow).GetField("currentItem",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
        private static readonly PropertyInfo CurrentItemProperty =
            typeof(ChoosePartUpWindow).GetProperty("currentItem",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentSegmentField =
            typeof(ChoosePartUpWindow).GetField("currentActiveSegment",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
        private static readonly PropertyInfo CurrentSegmentProperty =
            typeof(ChoosePartUpWindow).GetProperty("currentActiveSegment",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);

        private struct CurrentDetailObjectState
        {
            internal GameObject Target;
            internal bool WasActive;
        }

        private static ChoosePartUpWindow activeUpWindow;
        private static ChoosePartDownWindow activeDownWindow;
        private static GarageConditionFilterMode conditionMode =
            GarageConditionFilterMode.Off;
        private static QualityQuickFilterMode qualityMode =
            QualityQuickFilterMode.Off;
        private static string searchText = string.Empty;
        private static bool applyingFilteredList;
        private static NativeUiFactory.FooterHintHandle resetHint;
        private static GameObject emptyStateRoot;
        private static int hiddenCurrentSegment = -1;

        internal static void OnUpWindowShowPrefix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type, string overload)
        {
            if (type != ChoosePartUpWindowType.Mount) {
                if (IsActiveUpWindow(window))
                    DeactivateWindow();
                return;
            }

            if (!IsEnabled()) {
                DeactivateWindow();
                return;
            }

            activeUpWindow = window;
            activeDownWindow = window != null
                ? window.choosePartDownWindow : null;
        }

        internal static void OnUpWindowShowPostfix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type, bool result, string overload)
        {
            if (type != ChoosePartUpWindowType.Mount)
                return;

            if (!result && IsActiveUpWindow(window)) {
                DeactivateWindow();
                return;
            }

            if (IsActiveUpWindow(window))
                CreateResetHint();
        }

        internal static void OnUpWindowHidden(ChoosePartUpWindow window)
        {
            if (!IsActiveUpWindow(window))
                return;

            DeactivateWindow();
        }

        internal static DownWindowShowState PrepareNativeListForShow(
            ChoosePartDownWindow window,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items,
            ref int selectedIndex)
        {
            DownWindowShowState state = new DownWindowShowState();
            if (!IsActiveMountDownWindow(window) || applyingFilteredList ||
                items == null)
                return state;

            state.IsMountWindow = true;
            activeDownWindow = window;
            CaptureNativeItems(items);

            PartFilterCriteria criteria = CreateCriteria();
            if (!criteria.HasAnyFilter)
                return state;

            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                CreateFilteredNativeList(criteria);
            if (filtered.Count == 0) {
                state.NeedsEmptyRefresh = true;
                return state;
            }

            items = filtered;
            selectedIndex = 0;
            return state;
        }

        internal static void OnWindowShown(ChoosePartDownWindow window,
            DownWindowShowState state)
        {
            if (!state.IsMountWindow || !IsActiveMountDownWindow(window))
                return;

            activeDownWindow = window;
            bool attached = Panel.AttachWithButtons(window.transform,
                CycleConditionFilter, null, CycleQualityFilter,
                OnSearchChanged, true, false, true);
            if (!attached) {
                RestoreOriginalList();
                DeactivateWindow();
                return;
            }

            Panel.SetVerticalOffset(PanelVerticalOffset);
            Panel.SetSearchText(searchText);
            Panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            CreateResetHint();

            if (state.NeedsEmptyRefresh)
                ApplyCurrentFilters(true);
        }

        internal static void FilterNativeListBeforeRefresh(
            ChoosePartPageManager pageManager,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (!IsActiveMountDownWindow(pageManager) ||
                applyingFilteredList || items == null)
                return;

            CaptureNativeItems(items);
            PartFilterCriteria criteria = CreateCriteria();
            if (!criteria.HasAnyFilter)
                return;

            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                CreateFilteredNativeList(criteria);
            items = filtered;
            PreparePageManagerForRefresh(pageManager, filtered);
        }

        internal static void OnNativeListRefreshed(
            ChoosePartPageManager pageManager,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (!IsActiveMountDownWindow(pageManager) || applyingFilteredList)
                return;

            bool empty = items == null || items.Count == 0;
            if (HasActiveFilters())
                ApplyFilteredEmptyState(empty);
        }

        internal static void OnInputFieldKeyPressed(InputField inputField)
        {
            if (!IsEnabled() || activeDownWindow == null)
                return;
            Panel.HandleKeyPressed(inputField);
        }

        internal static bool ShouldSuppressNativeSelection(
            ChoosePartUpWindow window, ChoosePartDownItem item)
        {
            if (!IsEnabled() || !IsActiveUpWindow(window) ||
                window.choosePartUpWindowType != ChoosePartUpWindowType.Mount ||
                !HasActiveFilters() || item == null)
                return false;

            bool suppress = item.BaseItem == null ||
                !PartFilterRules.Matches(item.BaseItem, CreateCriteria());
            if (suppress)
                ClearCurrentPreviewItem();
            return suppress;
        }

        internal static bool ShouldSuppressSubmit(ChoosePartUpWindow window)
        {
            if (!IsEnabled() || !IsActiveUpWindow(window) ||
                window.choosePartUpWindowType != ChoosePartUpWindowType.Mount ||
                !HasActiveFilters())
                return false;
            if (activeDownWindow == null)
                return true;

            int selectedIndex;
            ChoosePartDownItem item =
                activeDownWindow.GetCurrentItem(out selectedIndex);
            return item == null || item.BaseItem == null ||
                !PartFilterRules.Matches(item.BaseItem, CreateCriteria());
        }

        internal static void ResetAll()
        {
            DeactivateWindow();
            conditionMode = GarageConditionFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            searchText = string.Empty;
            OriginalItems.Clear();
            applyingFilteredList = false;
        }

        internal static bool TryResetFromKeyboardShortcut()
        {
            if (!IsEnabled() || activeUpWindow == null ||
                activeDownWindow == null || activeUpWindow.gameObject == null ||
                !activeUpWindow.gameObject.activeInHierarchy ||
                activeUpWindow.choosePartUpWindowType !=
                    ChoosePartUpWindowType.Mount)
                return false;

            ResetFilters();
            return true;
        }

        private static bool IsEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addMountPartSelectionFilters;
        }

        private static bool HasActiveFilters()
        {
            return conditionMode != GarageConditionFilterMode.Off ||
                qualityMode != QualityQuickFilterMode.Off ||
                !string.IsNullOrEmpty(searchText);
        }

        private static bool IsActiveUpWindow(ChoosePartUpWindow window)
        {
            return window != null && activeUpWindow != null &&
                window.GetInstanceID() == activeUpWindow.GetInstanceID();
        }

        private static bool IsActiveMountDownWindow(
            ChoosePartPageManager window)
        {
            if (!IsEnabled() || window == null || activeUpWindow == null ||
                activeDownWindow == null || activeUpWindow.gameObject == null ||
                !activeUpWindow.gameObject.activeInHierarchy ||
                activeUpWindow.choosePartUpWindowType !=
                    ChoosePartUpWindowType.Mount)
                return false;

            return window.GetInstanceID() == activeDownWindow.GetInstanceID() &&
                activeUpWindow.choosePartDownWindow != null &&
                activeUpWindow.choosePartDownWindow.GetInstanceID() ==
                    activeDownWindow.GetInstanceID();
        }

        private static void CaptureNativeItems(
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            OriginalItems.Clear();
            if (items == null)
                return;

            foreach (ChoosePartDownItem item in items) {
                if (item != null)
                    OriginalItems.Add(item);
            }
        }

        private static void CycleConditionFilter()
        {
            switch (conditionMode) {
                case GarageConditionFilterMode.Off:
                    conditionMode =
                        GarageConditionFilterMode.RepairThresholdToPerfect;
                    break;
                case GarageConditionFilterMode.RepairThresholdToPerfect:
                    conditionMode = GarageConditionFilterMode.Perfect;
                    break;
                case GarageConditionFilterMode.Perfect:
                    conditionMode = GarageConditionFilterMode.GreenRing;
                    break;
                case GarageConditionFilterMode.GreenRing:
                    conditionMode = GarageConditionFilterMode.Yellow;
                    break;
                case GarageConditionFilterMode.Yellow:
                    conditionMode = GarageConditionFilterMode.Orange;
                    break;
                case GarageConditionFilterMode.Orange:
                    conditionMode = GarageConditionFilterMode.Red;
                    break;
                default:
                    conditionMode = GarageConditionFilterMode.Off;
                    break;
            }

            PartFilterPanelController.ClearSelectedControl();
            Panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CycleQualityFilter()
        {
            qualityMode = InventoryFilterManager.GetNextQualityMode(qualityMode);
            PartFilterPanelController.ClearSelectedControl();
            Panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void OnSearchChanged(string value)
        {
            searchText = value ?? string.Empty;
            ApplyCurrentFilters(true);
        }

        private static void ResetFilters()
        {
            conditionMode = GarageConditionFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            searchText = string.Empty;
            Panel.ResetSearch();
            Panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CreateResetHint()
        {
            if (resetHint != null && resetHint.Root != null)
                return;
            if (activeUpWindow == null || activeUpWindow.uiDescription == null)
                return;

            resetHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = WindowId,
                    WindowRoot = activeUpWindow.transform,
                    HintRoot = activeUpWindow.uiDescription.transform,
                    HintId = ResetHintId,
                    Keys = new string[] { "LeftAlt" },
                    Text = ModLocalization.Get("LOC_ResetFiltersAction"),
                    Action = new Action(ResetFilters),
                    Row = 0,
                    Order = 10,
                    Profile = WindowFooterHintController.NativeFooterProfile
                        .Automatic,
                    ItemCount = OriginalItems.Count,
                });
        }

        private static void DestroyResetHint()
        {
            WindowFooterHintController.RemoveHint(WindowId, ResetHintId);
            resetHint = null;
        }

        private static PartFilterCriteria CreateCriteria()
        {
            PartFilterCriteria criteria = new PartFilterCriteria();
            criteria.Context = PartFilterContext.Garage;
            criteria.SearchText = searchText;
            criteria.GarageConditionMode = conditionMode;
            criteria.RepairabilityMode = RepairabilityQuickFilterMode.Off;
            criteria.QualityMode = qualityMode;
            criteria.OwnedMode = OwnedQuickFilterMode.Off;
            return criteria;
        }

        private static Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
            CreateFilteredNativeList()
        {
            return CreateFilteredNativeList(CreateCriteria());
        }

        private static Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
            CreateFilteredNativeList(PartFilterCriteria criteria)
        {
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                new Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>();
            foreach (ChoosePartDownItem item in OriginalItems) {
                if (item != null && item.BaseItem != null &&
                    PartFilterRules.Matches(item.BaseItem, criteria))
                    filtered.Add(item);
            }
            return filtered;
        }

        private static void ApplyCurrentFilters(bool resetPage)
        {
            if (activeDownWindow == null || applyingFilteredList)
                return;

            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                CreateFilteredNativeList();
            try {
                applyingFilteredList = true;
                PreparePageManagerForRefresh(activeDownWindow, filtered);
                activeDownWindow.Refresh(filtered);
                ApplyFilteredEmptyState(filtered.Count == 0);
                if (resetPage) {
                    while (activeDownWindow.currentPage > 0)
                        activeDownWindow.PreviousPage(true);
                }
            } catch (Exception exception) {
                ModLogger.Log("[MountPartSelectionFilter] Failed to refresh the " +
                    "filtered mount-part list." + Environment.NewLine +
                    exception, Types.LoggingLevels.Warning);
            } finally {
                applyingFilteredList = false;
            }
        }

        private static void RestoreOriginalList()
        {
            if (activeDownWindow == null || OriginalItems.Count == 0)
                return;

            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> original =
                new Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>();
            for (int i = 0; i < OriginalItems.Count; i++)
                original.Add(OriginalItems[i]);

            try {
                applyingFilteredList = true;
                PreparePageManagerForRefresh(activeDownWindow, original);
                activeDownWindow.Refresh(original);
            } catch (Exception exception) {
                ModLogger.Log("[MountPartSelectionFilter] Failed to restore the " +
                    "native mount-part list." + Environment.NewLine +
                    exception, Types.LoggingLevels.Warning);
            } finally {
                applyingFilteredList = false;
            }
        }

        private static void PreparePageManagerForRefresh(
            ChoosePartPageManager pageManager,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (pageManager == null)
                return;

            pageManager.items = items;
            bool isEmpty = items != null && items.Count == 0;
            bool isActiveDownWindow = activeDownWindow != null &&
                pageManager.GetInstanceID() == activeDownWindow.GetInstanceID();
            if (isActiveDownWindow && HasActiveFilters()) {
                activeDownWindow.DeselectCurrentItem();
                ClearCurrentPreviewItem();
            }
            if (isEmpty) {
                pageManager.currentPage = 0;
                pageManager.currentPageItemsCount = 0;
            }

            if (isActiveDownWindow)
                ApplyFilteredEmptyState(isEmpty);
        }

        private static void ApplyFilteredEmptyState(bool isEmpty)
        {
            if (!isEmpty) {
                RestoreCurrentDetail();
                if (emptyStateRoot != null && emptyStateRoot.activeSelf)
                    emptyStateRoot.SetActive(false);
                return;
            }

            ClearCurrentPreviewItem();
            Transform currentDetail = GetCurrentDetail();
            HideCurrentDetail(currentDetail);
            EnsureEmptyState(currentDetail);
            if (emptyStateRoot != null && !emptyStateRoot.activeSelf)
                emptyStateRoot.SetActive(true);
        }

        private static void EnsureEmptyState(Transform currentDetail)
        {
            if (currentDetail == null)
                return;

            if (emptyStateRoot != null &&
                emptyStateRoot.transform.parent != currentDetail) {
                UnityEngine.Object.Destroy(emptyStateRoot);
                emptyStateRoot = null;
            }
            if (emptyStateRoot != null)
                return;

            emptyStateRoot = NativeUiFactory.CreateNativeNoItemsPage(
                currentDetail);
            if (emptyStateRoot == null) {
                return;
            }

            emptyStateRoot.name = "QMountPartEmptyState";
            RectTransform emptyRect =
                emptyStateRoot.GetComponent<RectTransform>();
            NativeUiFactory.Stretch(emptyRect, 0f, 0f, 0f, 0f);
            emptyStateRoot.transform.SetAsLastSibling();
        }

        private static void ClearCurrentPreviewItem()
        {
            if (activeUpWindow == null)
                return;

            if (CurrentItemField != null)
                CurrentItemField.SetValue(activeUpWindow, null);
            else if (CurrentItemProperty != null && CurrentItemProperty.CanWrite)
                CurrentItemProperty.SetValue(activeUpWindow, null, null);
        }

        private static Transform GetCurrentDetail()
        {
            if (activeUpWindow == null || activeUpWindow.transform == null)
                return null;

            int segment = GetCurrentActiveSegment();
            if (segment < 0 || segment > 2)
                return null;

            Transform detailsRoot = activeUpWindow.transform.Find("ItemsDetail");
            if (detailsRoot == null)
                return null;

            string detailName = segment == 0
                ? "InventoryItemDetails"
                : "InventoryItemDetails (" + segment + ")";
            return detailsRoot.Find(detailName);
        }

        private static void HideCurrentDetail(Transform detail)
        {
            if (detail == null)
                return;

            int segment = GetCurrentActiveSegment();
            if (segment < 0 || segment > 2)
                return;

            if (hiddenCurrentSegment != segment) {
                RestoreCurrentDetail();
                hiddenCurrentSegment = segment;
            }

            for (int i = 0; i < detail.childCount; i++) {
                Transform child = detail.GetChild(i);
                if (child == null || child.gameObject == null ||
                    IsCurrentDetailBackground(child) ||
                    (emptyStateRoot != null && child.gameObject == emptyStateRoot))
                    continue;
                RememberAndHideCurrentDetail(child);
            }
        }

        private static bool IsCurrentDetailBackground(Transform target)
        {
            if (target == null || target.gameObject == null)
                return false;

            string name = target.gameObject.name;
            return string.Equals(name, "BGSolid",
                       StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "BGSolid2",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "BGImg",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void RememberAndHideCurrentDetail(Transform target)
        {
            if (target == null || target.gameObject == null)
                return;

            for (int i = 0; i < HiddenCurrentDetailObjects.Count; i++) {
                CurrentDetailObjectState existing =
                    HiddenCurrentDetailObjects[i];
                if (existing.Target != target.gameObject)
                    continue;
                if (target.gameObject.activeSelf)
                    target.gameObject.SetActive(false);
                return;
            }

            CurrentDetailObjectState state = new CurrentDetailObjectState();
            state.Target = target.gameObject;
            state.WasActive = target.gameObject.activeSelf;
            HiddenCurrentDetailObjects.Add(state);
            if (state.WasActive)
                target.gameObject.SetActive(false);
        }

        private static int GetCurrentActiveSegment()
        {
            if (activeUpWindow == null)
                return -1;

            object value = null;
            if (CurrentSegmentField != null)
                value = CurrentSegmentField.GetValue(activeUpWindow);
            else if (CurrentSegmentProperty != null &&
                CurrentSegmentProperty.CanRead)
                value = CurrentSegmentProperty.GetValue(activeUpWindow, null);
            if (value == null)
                return -1;
            if (value is int)
                return (int)value;

            int segment;
            return int.TryParse(value.ToString(), out segment)
                ? segment : -1;
        }

        private static void RestoreCurrentDetail()
        {
            for (int i = 0; i < HiddenCurrentDetailObjects.Count; i++) {
                CurrentDetailObjectState state = HiddenCurrentDetailObjects[i];
                if (state.Target == null)
                    continue;
                if (state.Target.activeSelf != state.WasActive)
                    state.Target.SetActive(state.WasActive);
            }
            HiddenCurrentDetailObjects.Clear();
            hiddenCurrentSegment = -1;
        }

        private static void DeactivateWindow()
        {
            DestroyResetHint();
            RestoreCurrentDetail();
            if (emptyStateRoot != null) {
                UnityEngine.Object.Destroy(emptyStateRoot);
                emptyStateRoot = null;
            }
            Panel.Detach();
            OriginalItems.Clear();
            activeDownWindow = null;
            activeUpWindow = null;
            applyingFilteredList = false;
        }
    }
}
