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
    internal static class WheelBalancerInventoryFilterFeature
    {
        internal struct DownWindowShowState
        {
            internal bool IsWheelBalancerWindow;
            internal bool NeedsEmptyRefresh;
        }

        private const string WindowId = "WheelBalancer";
        private const string ResetHintId = "Hint_ResetWheelBalancerFilters";
        private const float PanelVerticalOffset = 8f;

        private static readonly List<ChoosePartDownItem> OriginalItems =
            new List<ChoosePartDownItem>();
        private static readonly PartFilterPanelController Panel =
            new PartFilterPanelController("QWheelBalancerFilter");
        private static readonly FieldInfo CurrentItemField =
            typeof(ChoosePartUpWindow).GetField("currentItem",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
        private static readonly PropertyInfo CurrentItemProperty =
            typeof(ChoosePartUpWindow).GetProperty("currentItem",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);


        private static ChoosePartUpWindow activeUpWindow;
        private static ChoosePartUpWindow knownUpWindow;
        private static ChoosePartDownWindow activeDownWindow;
        private static GarageConditionFilterMode conditionMode =
            GarageConditionFilterMode.Off;
        private static QualityQuickFilterMode qualityMode =
            QualityQuickFilterMode.Off;
        private static string searchText = string.Empty;
        private static bool applyingFilteredList;
        private static bool awaitingFilteredSelection;
        private static NativeUiFactory.FooterHintHandle resetHint;
        private static GameObject hiddenItemsDetailRoot;
        private static bool hiddenItemsDetailWasActive;
        private static GameObject filteredEmptyStateRoot;

        internal static void OnUpWindowShowPrefix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type)
        {
            knownUpWindow = window;
            if (type != ChoosePartUpWindowType.WheelBalance) {
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
            ChoosePartUpWindowType type, bool result)
        {
            if (type != ChoosePartUpWindowType.WheelBalance)
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
            if (!EnsureActiveWheelBalancerDownWindow(window) ||
                applyingFilteredList || items == null)
                return state;

            state.IsWheelBalancerWindow = true;
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
            if (!state.IsWheelBalancerWindow || !IsActiveWheelBalancerDownWindow(window))
                return;

            activeDownWindow = window;
            bool attached = Panel.AttachWithButtons(window.transform,
                CycleConditionFilter, null, CycleQualityFilter,
                OnSearchChanged, true, false, true,
                CycleConditionFilterReverse, null,
                CycleQualityFilterReverse);
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
            if (!IsActiveWheelBalancerDownWindow(pageManager) ||
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
            if (!IsActiveWheelBalancerDownWindow(pageManager) || applyingFilteredList)
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
                window.choosePartUpWindowType != ChoosePartUpWindowType.WheelBalance ||
                !HasActiveFilters() || item == null)
                return false;

            bool suppress = item.BaseItem == null ||
                !MatchesActiveFilter(item.BaseItem);
            if (suppress) {
                ClearCurrentPreviewItem();
                awaitingFilteredSelection = true;
                HideItemsDetail();
            } else {
                awaitingFilteredSelection = false;
                RestoreItemsDetail();
            }
            return suppress;
        }

        internal static bool ShouldSuppressSubmit(ChoosePartUpWindow window)
        {
            if (!IsEnabled() || !IsActiveUpWindow(window) ||
                window.choosePartUpWindowType != ChoosePartUpWindowType.WheelBalance ||
                !HasActiveFilters())
                return false;

            ChoosePartDownItem item = GetCurrentPreviewItem();
            return item == null || item.BaseItem == null ||
                !MatchesActiveFilter(item.BaseItem);
        }

        internal static void ResetAll()
        {
            DeactivateWindow();
            conditionMode = GarageConditionFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            searchText = string.Empty;
            OriginalItems.Clear();
            knownUpWindow = null;
            applyingFilteredList = false;
        }

        internal static bool TryResetFromKeyboardShortcut()
        {
            if (!IsEnabled() || activeUpWindow == null ||
                activeDownWindow == null || activeUpWindow.gameObject == null ||
                !activeUpWindow.gameObject.activeInHierarchy ||
                activeUpWindow.choosePartUpWindowType !=
                    ChoosePartUpWindowType.WheelBalance)
                return false;

            ResetFilters();
            return true;
        }

        private static bool IsEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addWheelBalancerInventoryFilters;
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

        private static bool EnsureActiveWheelBalancerDownWindow(
            ChoosePartDownWindow window)
        {
            if (!IsEnabled() || window == null)
                return false;

            if (IsMatchingWheelBalancerUpWindow(activeUpWindow, window)) {
                activeDownWindow = window;
                return true;
            }

            ChoosePartUpWindow candidate = knownUpWindow;
            if (candidate == null) {
                candidate = UnityEngine.Object
                    .FindObjectOfType<ChoosePartUpWindow>();
                knownUpWindow = candidate;
            }
            if (!IsMatchingWheelBalancerUpWindow(candidate, window))
                return false;

            activeUpWindow = candidate;
            activeDownWindow = window;
            return true;
        }

        private static bool IsMatchingWheelBalancerUpWindow(
            ChoosePartUpWindow upWindow, ChoosePartDownWindow downWindow)
        {
            return upWindow != null && downWindow != null &&
                upWindow.gameObject != null &&
                upWindow.gameObject.activeInHierarchy &&
                upWindow.choosePartUpWindowType ==
                    ChoosePartUpWindowType.WheelBalance &&
                upWindow.choosePartDownWindow != null &&
                upWindow.choosePartDownWindow.GetInstanceID() ==
                    downWindow.GetInstanceID();
        }

        private static bool IsActiveWheelBalancerDownWindow(
            ChoosePartPageManager window)
        {
            if (!IsEnabled() || window == null || activeUpWindow == null ||
                activeDownWindow == null)
                return false;

            return window.GetInstanceID() == activeDownWindow.GetInstanceID() &&
                IsMatchingWheelBalancerUpWindow(activeUpWindow, activeDownWindow);
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

        private static void CycleConditionFilterReverse()
        {
            switch (conditionMode) {
                case GarageConditionFilterMode.Off:
                    conditionMode = GarageConditionFilterMode.Red;
                    break;
                case GarageConditionFilterMode.Red:
                    conditionMode = GarageConditionFilterMode.Orange;
                    break;
                case GarageConditionFilterMode.Orange:
                    conditionMode = GarageConditionFilterMode.Yellow;
                    break;
                case GarageConditionFilterMode.Yellow:
                    conditionMode = GarageConditionFilterMode.GreenRing;
                    break;
                case GarageConditionFilterMode.GreenRing:
                    conditionMode = GarageConditionFilterMode.Perfect;
                    break;
                case GarageConditionFilterMode.Perfect:
                    conditionMode =
                        GarageConditionFilterMode.RepairThresholdToPerfect;
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

        private static void CycleQualityFilterReverse()
        {
            qualityMode = InventoryFilterManager.GetPreviousQualityMode(qualityMode);
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
            PartFilterCriteria groupCriteria = null;
            PartFilterCriteria qualityCriteria = null;
            if (criteria != null && criteria.UsesQualityFilter) {
                groupCriteria = CreateCriteria();
                groupCriteria.QualityMode = QualityQuickFilterMode.Off;
                qualityCriteria = new PartFilterCriteria {
                    Context = PartFilterContext.Garage,
                    QualityMode = criteria.QualityMode,
                };
            }

            foreach (ChoosePartDownItem item in OriginalItems) {
                if (item != null && item.BaseItem != null &&
                    MatchesWheelBalancerItem(item.BaseItem, criteria,
                        groupCriteria, qualityCriteria))
                    filtered.Add(item);
            }
            return filtered;
        }

        private static bool MatchesWheelBalancerItem(BaseItem baseItem,
            PartFilterCriteria criteria, PartFilterCriteria groupCriteria,
            PartFilterCriteria qualityCriteria)
        {
            if (baseItem == null || criteria == null)
                return false;

            GroupItem group = baseItem.TryCast<GroupItem>();
            if (group == null || !criteria.UsesQualityFilter)
                return PartFilterRules.Matches(baseItem, criteria);

            if (!PartFilterRules.Matches(group, groupCriteria) ||
                group.ItemList == null)
                return false;

            foreach (Item groupItem in group.ItemList) {
                if (groupItem != null &&
                    PartFilterRules.Matches(groupItem, qualityCriteria))
                    return true;
            }
            return false;
        }

        private static bool MatchesActiveFilter(BaseItem baseItem)
        {
            PartFilterCriteria criteria = CreateCriteria();
            PartFilterCriteria groupCriteria = null;
            PartFilterCriteria qualityCriteria = null;
            if (criteria.UsesQualityFilter) {
                groupCriteria = CreateCriteria();
                groupCriteria.QualityMode = QualityQuickFilterMode.Off;
                qualityCriteria = new PartFilterCriteria {
                    Context = PartFilterContext.Garage,
                    QualityMode = criteria.QualityMode,
                };
            }

            return MatchesWheelBalancerItem(baseItem, criteria, groupCriteria,
                qualityCriteria);
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
                ModLogger.Log("[WheelBalancerInventoryFilter] Failed to refresh the " +
                    "filtered wheel-balancer list." + Environment.NewLine +
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
                ModLogger.Log("[WheelBalancerInventoryFilter] Failed to restore the " +
                    "native wheel-balancer list." + Environment.NewLine +
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
                awaitingFilteredSelection = true;
                HideItemsDetail();
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
            if (!HasActiveFilters()) {
                awaitingFilteredSelection = false;
                RestoreItemsDetail();
                HideFilteredEmptyState();
                return;
            }

            if (isEmpty || awaitingFilteredSelection) {
                if (isEmpty)
                    ClearCurrentPreviewItem();
                HideItemsDetail();
                if (isEmpty)
                    ShowFilteredEmptyState();
                else
                    HideFilteredEmptyState();
                return;
            }

            RestoreItemsDetail();
            HideFilteredEmptyState();
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

        private static ChoosePartDownItem GetCurrentPreviewItem()
        {
            if (activeUpWindow == null)
                return null;

            object value = null;
            if (CurrentItemField != null)
                value = CurrentItemField.GetValue(activeUpWindow);
            else if (CurrentItemProperty != null && CurrentItemProperty.CanRead)
                value = CurrentItemProperty.GetValue(activeUpWindow, null);
            return value as ChoosePartDownItem;
        }

        private static void HideItemsDetail()
        {
            if (activeUpWindow == null || activeUpWindow.transform == null)
                return;

            Transform itemsDetail = activeUpWindow.transform.Find("ItemsDetail");
            if (itemsDetail == null || itemsDetail.gameObject == null)
                return;

            if (hiddenItemsDetailRoot != itemsDetail.gameObject) {
                RestoreItemsDetail();
                hiddenItemsDetailRoot = itemsDetail.gameObject;
                hiddenItemsDetailWasActive = hiddenItemsDetailRoot.activeSelf;
            }

            if (hiddenItemsDetailRoot.activeSelf)
                hiddenItemsDetailRoot.SetActive(false);
        }

        private static void RestoreItemsDetail()
        {
            if (hiddenItemsDetailRoot != null &&
                hiddenItemsDetailRoot.activeSelf != hiddenItemsDetailWasActive)
                hiddenItemsDetailRoot.SetActive(hiddenItemsDetailWasActive);
            hiddenItemsDetailRoot = null;
            hiddenItemsDetailWasActive = false;
        }

        private static void ShowFilteredEmptyState()
        {
            if (activeUpWindow == null || activeUpWindow.transform == null)
                return;

            if (filteredEmptyStateRoot != null &&
                filteredEmptyStateRoot.transform.parent != activeUpWindow.transform) {
                UnityEngine.Object.Destroy(filteredEmptyStateRoot);
                filteredEmptyStateRoot = null;
            }
            if (filteredEmptyStateRoot == null) {
                filteredEmptyStateRoot = NativeUiFactory.CreateNativeNoItemsPage(
                    activeUpWindow.transform);
                if (filteredEmptyStateRoot == null)
                    return;

                filteredEmptyStateRoot.name = "QWheelBalancerEmptyState";
                RectTransform rect =
                    filteredEmptyStateRoot.GetComponent<RectTransform>();
                NativeUiFactory.Stretch(rect, 0f, 0f, 0f, 0f);
                filteredEmptyStateRoot.transform.SetAsLastSibling();
            }

            if (!filteredEmptyStateRoot.activeSelf)
                filteredEmptyStateRoot.SetActive(true);
        }

        private static void HideFilteredEmptyState()
        {
            if (filteredEmptyStateRoot != null && filteredEmptyStateRoot.activeSelf)
                filteredEmptyStateRoot.SetActive(false);
        }

        private static void DeactivateWindow()
        {
            DestroyResetHint();
            RestoreItemsDetail();
            if (filteredEmptyStateRoot != null) {
                UnityEngine.Object.Destroy(filteredEmptyStateRoot);
                filteredEmptyStateRoot = null;
            }
            Panel.Detach();
            OriginalItems.Clear();
            activeDownWindow = null;
            activeUpWindow = null;
            applyingFilteredList = false;
            awaitingFilteredSelection = false;
        }
    }
}
