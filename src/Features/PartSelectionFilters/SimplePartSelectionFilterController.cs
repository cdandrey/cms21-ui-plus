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
    internal sealed class SimplePartSelectionFilterController
    {
        internal struct DownWindowShowState
        {
            internal bool IsTargetWindow;
            internal bool NeedsEmptyRefresh;
        }

        private const float PanelVerticalOffset = 8f;

        private static readonly FieldInfo CurrentItemField =
            typeof(ChoosePartUpWindow).GetField("currentItem",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
        private static readonly PropertyInfo CurrentItemProperty =
            typeof(ChoosePartUpWindow).GetProperty("currentItem",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);

        private readonly ChoosePartUpWindowType windowType;
        private readonly Func<bool> isEnabled;
        private readonly Func<bool> resetOnExit;
        private readonly bool matchGroupQualityByContents;
        private readonly string windowId;
        private readonly string resetHintId;
        private readonly string emptyStateName;
        private readonly string logName;
        private readonly string itemListName;
        private readonly bool recoverUpWindow;
        private readonly bool includePerfectCondition;
        private readonly bool submitUsesPreviewItem;
        private readonly List<ChoosePartDownItem> originalItems =
            new List<ChoosePartDownItem>();
        private readonly PartFilterPanelController panel;

        private ChoosePartUpWindow activeUpWindow;
        private ChoosePartUpWindow knownUpWindow;
        private ChoosePartDownWindow activeDownWindow;
        private GarageConditionFilterMode conditionMode =
            GarageConditionFilterMode.Off;
        private QualityQuickFilterMode qualityMode = QualityQuickFilterMode.Off;
        private string searchText = string.Empty;
        private bool applyingFilteredList;
        private bool awaitingFilteredSelection;
        private NativeUiFactory.FooterHintHandle resetHint;
        private GameObject hiddenItemsDetailRoot;
        private bool hiddenItemsDetailWasActive;
        private GameObject filteredEmptyStateRoot;

        internal SimplePartSelectionFilterController(
            ChoosePartUpWindowType windowType, Func<bool> isEnabled,
            Func<bool> resetOnExit, string windowId, string resetHintId,
            string panelName,
            string emptyStateName, string logName, string itemListName,
            bool recoverUpWindow, bool includePerfectCondition,
            bool submitUsesPreviewItem, bool matchGroupQualityByContents)
        {
            this.windowType = windowType;
            this.isEnabled = isEnabled;
            this.resetOnExit = resetOnExit;
            this.windowId = windowId;
            this.resetHintId = resetHintId;
            this.emptyStateName = emptyStateName;
            this.logName = logName;
            this.itemListName = itemListName;
            this.recoverUpWindow = recoverUpWindow;
            this.includePerfectCondition = includePerfectCondition;
            this.submitUsesPreviewItem = submitUsesPreviewItem;
            this.matchGroupQualityByContents = matchGroupQualityByContents;
            panel = new PartFilterPanelController(panelName);
        }

        internal void OnUpWindowShowPrefix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type)
        {
            if (recoverUpWindow)
                knownUpWindow = window;
            if (type != windowType) {
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

        internal void OnUpWindowShowPostfix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type, bool result)
        {
            if (type != windowType)
                return;

            if (!result && IsActiveUpWindow(window)) {
                DeactivateWindow();
                return;
            }

            if (IsActiveUpWindow(window))
                CreateResetHint();
        }

        internal void OnUpWindowHidden(ChoosePartUpWindow window)
        {
            if (!IsActiveUpWindow(window))
                return;

            if (ShouldResetOnExit())
                ResetFilterState();
            DeactivateWindow();
        }

        internal DownWindowShowState PrepareNativeListForShow(
            ChoosePartDownWindow window,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items,
            ref int selectedIndex)
        {
            DownWindowShowState state = new DownWindowShowState();
            if (!EnsureActiveDownWindow(window) || applyingFilteredList ||
                items == null)
                return state;

            state.IsTargetWindow = true;
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

        internal void OnWindowShown(ChoosePartDownWindow window,
            DownWindowShowState state)
        {
            if (!state.IsTargetWindow || !IsActiveDownWindow(window))
                return;

            activeDownWindow = window;
            bool attached = panel.AttachWithButtons(window.transform,
                CycleConditionFilter, null, CycleQualityFilter,
                OnSearchChanged, true, false, true,
                CycleConditionFilterReverse, null,
                CycleQualityFilterReverse);
            if (!attached) {
                RestoreOriginalList();
                DeactivateWindow();
                return;
            }

            panel.SetVerticalOffset(PanelVerticalOffset);
            panel.SetSearchText(searchText);
            panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            CreateResetHint();

            if (state.NeedsEmptyRefresh)
                ApplyCurrentFilters(true);
        }

        internal void FilterNativeListBeforeRefresh(
            ChoosePartPageManager pageManager,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (!IsActiveDownWindow(pageManager) || applyingFilteredList ||
                items == null)
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

        internal void OnNativeListRefreshed(ChoosePartPageManager pageManager,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (!IsActiveDownWindow(pageManager) || applyingFilteredList)
                return;

            bool empty = items == null || items.Count == 0;
            if (HasActiveFilters())
                ApplyFilteredEmptyState(empty);
        }

        internal void OnInputFieldKeyPressed(InputField inputField)
        {
            if (!IsEnabled() || activeDownWindow == null)
                return;
            panel.HandleKeyPressed(inputField);
        }

        internal bool ShouldSuppressNativeSelection(ChoosePartUpWindow window,
            ChoosePartDownItem item)
        {
            if (!IsEnabled() || !IsActiveUpWindow(window) ||
                window.choosePartUpWindowType != windowType ||
                !HasActiveFilters() || item == null)
                return false;

            bool suppress = item.BaseItem == null ||
                !Matches(item.BaseItem, CreateCriteria());
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

        internal bool ShouldSuppressSubmit(ChoosePartUpWindow window)
        {
            if (!IsEnabled() || !IsActiveUpWindow(window) ||
                window.choosePartUpWindowType != windowType ||
                !HasActiveFilters())
                return false;

            ChoosePartDownItem item;
            if (submitUsesPreviewItem) {
                item = GetCurrentPreviewItem();
            } else {
                if (activeDownWindow == null)
                    return true;
                int selectedIndex;
                item = activeDownWindow.GetCurrentItem(out selectedIndex);
            }

            return item == null || item.BaseItem == null ||
                !Matches(item.BaseItem, CreateCriteria());
        }

        internal void ResetAll()
        {
            DeactivateWindow();
            ResetFilterState();
            originalItems.Clear();
            knownUpWindow = null;
            applyingFilteredList = false;
        }

        internal bool TryResetFromKeyboardShortcut()
        {
            if (!IsEnabled() || activeUpWindow == null ||
                activeDownWindow == null || activeUpWindow.gameObject == null ||
                !activeUpWindow.gameObject.activeInHierarchy ||
                activeUpWindow.choosePartUpWindowType != windowType)
                return false;

            ResetFilters();
            return true;
        }

        private bool IsEnabled()
        {
            return isEnabled != null && isEnabled();
        }

        private bool ShouldResetOnExit()
        {
            return resetOnExit != null && resetOnExit();
        }

        private bool HasActiveFilters()
        {
            return conditionMode != GarageConditionFilterMode.Off ||
                qualityMode != QualityQuickFilterMode.Off ||
                !string.IsNullOrEmpty(searchText);
        }

        private bool IsActiveUpWindow(ChoosePartUpWindow window)
        {
            return window != null && activeUpWindow != null &&
                window.GetInstanceID() == activeUpWindow.GetInstanceID();
        }

        private bool EnsureActiveDownWindow(ChoosePartDownWindow window)
        {
            if (!IsEnabled() || window == null)
                return false;
            if (!recoverUpWindow)
                return IsActiveDownWindow(window);

            if (IsMatchingUpWindow(activeUpWindow, window)) {
                activeDownWindow = window;
                return true;
            }

            ChoosePartUpWindow candidate = knownUpWindow;
            if (candidate == null) {
                candidate = UnityEngine.Object
                    .FindObjectOfType<ChoosePartUpWindow>();
                knownUpWindow = candidate;
            }
            if (!IsMatchingUpWindow(candidate, window))
                return false;

            activeUpWindow = candidate;
            activeDownWindow = window;
            return true;
        }

        private bool IsMatchingUpWindow(ChoosePartUpWindow upWindow,
            ChoosePartDownWindow downWindow)
        {
            return upWindow != null && downWindow != null &&
                upWindow.gameObject != null &&
                upWindow.gameObject.activeInHierarchy &&
                upWindow.choosePartUpWindowType == windowType &&
                upWindow.choosePartDownWindow != null &&
                upWindow.choosePartDownWindow.GetInstanceID() ==
                    downWindow.GetInstanceID();
        }

        private bool IsActiveDownWindow(ChoosePartPageManager window)
        {
            if (!IsEnabled() || window == null || activeUpWindow == null ||
                activeDownWindow == null)
                return false;

            return window.GetInstanceID() == activeDownWindow.GetInstanceID() &&
                IsMatchingUpWindow(activeUpWindow, activeDownWindow);
        }

        private void CaptureNativeItems(
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            originalItems.Clear();
            if (items == null)
                return;

            foreach (ChoosePartDownItem item in items) {
                if (item != null)
                    originalItems.Add(item);
            }
        }

        private void CycleConditionFilter()
        {
            switch (conditionMode) {
                case GarageConditionFilterMode.Off:
                    conditionMode =
                        GarageConditionFilterMode.RepairThresholdToPerfect;
                    break;
                case GarageConditionFilterMode.RepairThresholdToPerfect:
                    conditionMode = includePerfectCondition
                        ? GarageConditionFilterMode.Perfect
                        : GarageConditionFilterMode.GreenRing;
                    break;
                case GarageConditionFilterMode.Perfect:
                    conditionMode = includePerfectCondition
                        ? GarageConditionFilterMode.GreenRing
                        : GarageConditionFilterMode.Off;
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

            RefreshFilterPanel();
        }

        private void CycleConditionFilterReverse()
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
                    conditionMode = includePerfectCondition
                        ? GarageConditionFilterMode.Perfect
                        : GarageConditionFilterMode.RepairThresholdToPerfect;
                    break;
                case GarageConditionFilterMode.Perfect:
                    conditionMode = includePerfectCondition
                        ? GarageConditionFilterMode.RepairThresholdToPerfect
                        : GarageConditionFilterMode.Off;
                    break;
                default:
                    conditionMode = GarageConditionFilterMode.Off;
                    break;
            }

            RefreshFilterPanel();
        }

        private void CycleQualityFilter()
        {
            qualityMode = InventoryFilterManager.GetNextQualityMode(qualityMode);
            RefreshFilterPanel();
        }

        private void CycleQualityFilterReverse()
        {
            qualityMode = InventoryFilterManager.GetPreviousQualityMode(
                qualityMode);
            RefreshFilterPanel();
        }

        private void RefreshFilterPanel()
        {
            PartFilterPanelController.ClearSelectedControl();
            panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private void OnSearchChanged(string value)
        {
            searchText = value ?? string.Empty;
            ApplyCurrentFilters(true);
        }

        private void ResetFilterState()
        {
            conditionMode = GarageConditionFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            searchText = string.Empty;
        }

        private void ResetFilters()
        {
            ResetFilterState();
            panel.ResetSearch();
            panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private void CreateResetHint()
        {
            if (resetHint != null && resetHint.Root != null)
                return;
            if (activeUpWindow == null || activeUpWindow.uiDescription == null)
                return;

            resetHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = windowId,
                    WindowRoot = activeUpWindow.transform,
                    HintRoot = activeUpWindow.uiDescription.transform,
                    HintId = resetHintId,
                    Keys = new string[] { "LeftAlt" },
                    Text = ModLocalization.Get("LOC_ResetFiltersAction"),
                    Action = new Action(ResetFilters),
                    Row = 0,
                    Order = 10,
                    Profile = WindowFooterHintController.NativeFooterProfile
                        .Automatic,
                    ItemCount = originalItems.Count,
                });
        }

        private void DestroyResetHint()
        {
            WindowFooterHintController.RemoveHint(windowId, resetHintId);
            resetHint = null;
        }

        private PartFilterCriteria CreateCriteria()
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

        private bool Matches(BaseItem baseItem, PartFilterCriteria criteria)
        {
            if (!matchGroupQualityByContents || criteria == null ||
                !criteria.UsesQualityFilter)
                return PartFilterRules.Matches(baseItem, criteria);

            PartFilterCriteria groupCriteria;
            PartFilterCriteria qualityCriteria;
            CreateGroupQualityCriteria(criteria, out groupCriteria,
                out qualityCriteria);
            return MatchesGroupQuality(baseItem, criteria, groupCriteria,
                qualityCriteria);
        }

        private static void CreateGroupQualityCriteria(
            PartFilterCriteria criteria, out PartFilterCriteria groupCriteria,
            out PartFilterCriteria qualityCriteria)
        {
            groupCriteria = new PartFilterCriteria {
                Context = criteria.Context,
                SearchText = criteria.SearchText,
                GarageConditionMode = criteria.GarageConditionMode,
                RepairabilityMode = criteria.RepairabilityMode,
                QualityMode = QualityQuickFilterMode.Off,
                OwnedMode = criteria.OwnedMode,
            };
            qualityCriteria = new PartFilterCriteria {
                Context = PartFilterContext.Garage,
                QualityMode = criteria.QualityMode,
            };
        }

        private static bool MatchesGroupQuality(BaseItem baseItem,
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

        private Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
            CreateFilteredNativeList()
        {
            return CreateFilteredNativeList(CreateCriteria());
        }

        private Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
            CreateFilteredNativeList(PartFilterCriteria criteria)
        {
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                new Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>();
            PartFilterCriteria groupCriteria = null;
            PartFilterCriteria qualityCriteria = null;
            if (matchGroupQualityByContents && criteria != null &&
                criteria.UsesQualityFilter)
                CreateGroupQualityCriteria(criteria, out groupCriteria,
                    out qualityCriteria);

            foreach (ChoosePartDownItem item in originalItems) {
                if (item == null || item.BaseItem == null)
                    continue;

                bool matches = groupCriteria != null
                    ? MatchesGroupQuality(item.BaseItem, criteria, groupCriteria,
                        qualityCriteria)
                    : PartFilterRules.Matches(item.BaseItem, criteria);
                if (matches)
                    filtered.Add(item);
            }
            return filtered;
        }

        private void ApplyCurrentFilters(bool resetPage)
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
                ModLogger.Log("[" + logName + "] Failed to refresh the " +
                    "filtered " + itemListName + " list." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            } finally {
                applyingFilteredList = false;
            }
        }

        private void RestoreOriginalList()
        {
            if (activeDownWindow == null || originalItems.Count == 0)
                return;

            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> original =
                new Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>();
            for (int i = 0; i < originalItems.Count; i++)
                original.Add(originalItems[i]);

            try {
                applyingFilteredList = true;
                PreparePageManagerForRefresh(activeDownWindow, original);
                activeDownWindow.Refresh(original);
            } catch (Exception exception) {
                ModLogger.Log("[" + logName + "] Failed to restore the " +
                    "native " + itemListName + " list." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            } finally {
                applyingFilteredList = false;
            }
        }

        private void PreparePageManagerForRefresh(
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

        private void ApplyFilteredEmptyState(bool isEmpty)
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

        private void ClearCurrentPreviewItem()
        {
            if (activeUpWindow == null)
                return;

            if (CurrentItemField != null)
                CurrentItemField.SetValue(activeUpWindow, null);
            else if (CurrentItemProperty != null && CurrentItemProperty.CanWrite)
                CurrentItemProperty.SetValue(activeUpWindow, null, null);
        }

        private ChoosePartDownItem GetCurrentPreviewItem()
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

        private void HideItemsDetail()
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

        private void RestoreItemsDetail()
        {
            if (hiddenItemsDetailRoot != null &&
                hiddenItemsDetailRoot.activeSelf != hiddenItemsDetailWasActive)
                hiddenItemsDetailRoot.SetActive(hiddenItemsDetailWasActive);
            hiddenItemsDetailRoot = null;
            hiddenItemsDetailWasActive = false;
        }

        private void ShowFilteredEmptyState()
        {
            if (activeUpWindow == null || activeUpWindow.transform == null)
                return;

            if (filteredEmptyStateRoot != null &&
                filteredEmptyStateRoot.transform.parent !=
                    activeUpWindow.transform) {
                UnityEngine.Object.Destroy(filteredEmptyStateRoot);
                filteredEmptyStateRoot = null;
            }
            if (filteredEmptyStateRoot == null) {
                filteredEmptyStateRoot = NativeUiFactory.CreateNativeNoItemsPage(
                    activeUpWindow.transform);
                if (filteredEmptyStateRoot == null)
                    return;

                filteredEmptyStateRoot.name = emptyStateName;
                RectTransform rect =
                    filteredEmptyStateRoot.GetComponent<RectTransform>();
                NativeUiFactory.Stretch(rect, 0f, 0f, 0f, 0f);
                filteredEmptyStateRoot.transform.SetAsLastSibling();
            }

            if (!filteredEmptyStateRoot.activeSelf)
                filteredEmptyStateRoot.SetActive(true);
        }

        private void HideFilteredEmptyState()
        {
            if (filteredEmptyStateRoot != null &&
                filteredEmptyStateRoot.activeSelf)
                filteredEmptyStateRoot.SetActive(false);
        }

        private void DeactivateWindow()
        {
            DestroyResetHint();
            RestoreItemsDetail();
            if (filteredEmptyStateRoot != null) {
                UnityEngine.Object.Destroy(filteredEmptyStateRoot);
                filteredEmptyStateRoot = null;
            }
            panel.Detach();
            originalItems.Clear();
            activeDownWindow = null;
            activeUpWindow = null;
            applyingFilteredList = false;
            awaitingFilteredSelection = false;
        }
    }
}
