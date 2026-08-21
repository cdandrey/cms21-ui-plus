using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
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
    internal sealed class AssemblyPartSelectionFilterController
    {
        internal struct DownWindowShowState
        {
            internal bool IsTargetWindow;
            internal bool NeedsEmptyRefresh;
        }

        private const float PanelVerticalOffset = 8f;

        private readonly ChoosePartUpWindowType connectType;
        private readonly ChoosePartUpWindowType separateType;
        private readonly Func<bool> isEnabled;
        private readonly Func<bool> resetOnExit;
        private readonly string windowId;
        private readonly string resetHintId;
        private readonly string separateEmptyStateName;
        private readonly string selectionPromptName;
        private readonly string assemblyEmptyStateName;
        private readonly string logName;
        private readonly string itemListName;
        private readonly bool springClampLayout;
        private readonly List<ChoosePartDownItem> originalItems =
            new List<ChoosePartDownItem>();
        private readonly PartFilterPanelController panel;

        private ChoosePartUpWindow activeUpWindow;
        private ChoosePartUpWindow knownUpWindow;
        private ChoosePartDownWindow activeDownWindow;
        private GarageConditionFilterMode conditionMode =
            GarageConditionFilterMode.Off;
        private QualityQuickFilterMode qualityMode =
            QualityQuickFilterMode.Off;
        private string searchText = string.Empty;
        private bool applyingFilteredList;
        private bool awaitingFilteredSelection;
        private NativeUiFactory.FooterHintHandle resetHint;
        private GameObject emptyStateRoot;
        private GameObject separateItemsDetailRoot;
        private bool separateItemsDetailWasActive;
        private GameObject nativeDownEmptyStateRoot;
        private bool nativeDownEmptyStateWasActive;
        private GameObject separateWindowEmptyStateRoot;
        private GameObject hiddenItemsDetailRoot;
        private bool hiddenItemsDetailWasActive;
        private GameObject hiddenCurrentDetailRoot;
        private bool hiddenCurrentDetailWasActive;
        private int hiddenCurrentDetailSegment = -1;
        private GameObject hiddenArrow1Root;
        private bool hiddenArrow1WasActive;
        private GameObject hiddenArrow2Root;
        private bool hiddenArrow2WasActive;
        private GameObject hiddenFollowingDetailRoot;
        private bool hiddenFollowingDetailWasActive;
        private int emptyStateSegment = -1;
        private bool emptyStateShowsNoItems;
        private GameObject assemblyWindowEmptyStateRoot;
        private bool assemblyWindowEmptyStateShowsNoItems;
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

        internal AssemblyPartSelectionFilterController(
            ChoosePartUpWindowType connectType,
            ChoosePartUpWindowType separateType, Func<bool> isEnabled,
            Func<bool> resetOnExit, string windowId, string resetHintId,
            string panelName,
            string separateEmptyStateName, string selectionPromptName,
            string assemblyEmptyStateName, string logName,
            string itemListName, bool springClampLayout)
        {
            this.connectType = connectType;
            this.separateType = separateType;
            this.isEnabled = isEnabled;
            this.resetOnExit = resetOnExit;
            this.windowId = windowId;
            this.resetHintId = resetHintId;
            this.separateEmptyStateName = separateEmptyStateName;
            this.selectionPromptName = selectionPromptName;
            this.assemblyEmptyStateName = assemblyEmptyStateName;
            this.logName = logName;
            this.itemListName = itemListName;
            this.springClampLayout = springClampLayout;
            panel = new PartFilterPanelController(panelName);
        }

        internal void OnUpWindowShowPrefix(ChoosePartUpWindow window,
            ChoosePartUpWindowType type)
        {
            knownUpWindow = window;
            if (!IsTargetType(type)) {
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
            if (!IsTargetType(type))
                return;
            if (!result) {
                if (IsActiveUpWindow(window))
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
            if (!panel.AttachWithButtons(window.transform,
                    CycleConditionFilter, null, CycleQualityFilter,
                    OnSearchChanged, true, false, true,
                    CycleConditionFilterReverse, null,
                    CycleQualityFilterReverse)) {
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
            if (!IsActiveDownWindow(pageManager) ||
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

        internal void OnNativeListRefreshed(
            ChoosePartPageManager pageManager,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (!IsActiveDownWindow(pageManager) ||
                applyingFilteredList || !HasActiveFilters())
                return;

            ApplyFilteredEmptyState(items == null || items.Count == 0);
        }

        internal void OnInputFieldKeyPressed(InputField inputField)
        {
            if (!IsEnabled() || activeDownWindow == null)
                return;
            panel.HandleKeyPressed(inputField);
        }

        internal bool TryResetFromKeyboardShortcut()
        {
            if (!IsEnabled() || activeUpWindow == null ||
                activeDownWindow == null || activeUpWindow.gameObject == null ||
                !activeUpWindow.gameObject.activeInHierarchy ||
                !IsTargetType(activeUpWindow.choosePartUpWindowType))
                return false;

            ResetFilters();
            return true;
        }

        internal bool ShouldSuppressNativeSelection(
            ChoosePartUpWindow window, ChoosePartDownItem item)
        {
            if (!IsEnabled() || !IsActiveUpWindow(window) ||
                !IsTargetType(window.choosePartUpWindowType) ||
                !HasActiveFilters() || item == null)
                return false;

            bool suppress = !MatchesActiveFilter(item.BaseItem);
            if (suppress) {
                ClearCurrentPreviewItem();
                awaitingFilteredSelection = true;
            } else {
                awaitingFilteredSelection = false;
                RestoreFilteredSelectionUi();
                RestoreSeparateEmptyState();
            }
            return suppress;
        }

        internal bool ShouldSuppressSubmit(ChoosePartUpWindow window)
        {
            if (!IsEnabled() || !IsActiveUpWindow(window) ||
                !IsTargetType(window.choosePartUpWindowType) ||
                !HasActiveFilters())
                return false;
            if (activeDownWindow == null)
                return true;

            int selectedIndex;
            ChoosePartDownItem item =
                activeDownWindow.GetCurrentItem(out selectedIndex);
            return item == null || !MatchesActiveFilter(item.BaseItem);
        }

        internal void ResetAll()
        {
            DeactivateWindow();
            ResetFilterState();
            originalItems.Clear();
            knownUpWindow = null;
            applyingFilteredList = false;
        }

        private bool HasActiveFilters()
        {
            return conditionMode != GarageConditionFilterMode.Off ||
                qualityMode != QualityQuickFilterMode.Off ||
                !string.IsNullOrEmpty(searchText);
        }

        private bool IsEnabled()
        {
            return isEnabled != null && isEnabled();
        }

        private bool ShouldResetOnExit()
        {
            return resetOnExit != null && resetOnExit();
        }

        private bool IsTargetType(ChoosePartUpWindowType type)
        {
            return type == connectType || type == separateType;
        }

        private bool IsActiveUpWindow(ChoosePartUpWindow window)
        {
            return window != null && activeUpWindow != null &&
                window.GetInstanceID() == activeUpWindow.GetInstanceID();
        }

        private bool EnsureActiveDownWindow(
            ChoosePartDownWindow window)
        {
            if (!IsEnabled() || window == null)
                return false;

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

        private bool IsMatchingUpWindow(
            ChoosePartUpWindow upWindow, ChoosePartDownWindow downWindow)
        {
            return upWindow != null && downWindow != null &&
                upWindow.gameObject != null &&
                upWindow.gameObject.activeInHierarchy &&
                IsTargetType(upWindow.choosePartUpWindowType) &&
                upWindow.choosePartDownWindow != null &&
                upWindow.choosePartDownWindow.GetInstanceID() ==
                    downWindow.GetInstanceID();
        }

        private bool IsActiveDownWindow(
            ChoosePartPageManager window)
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
                    conditionMode = GarageConditionFilterMode.RepairThresholdToPerfect;
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
            panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
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
            panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private void CycleQualityFilter()
        {
            qualityMode = InventoryFilterManager.GetNextQualityMode(qualityMode);
            PartFilterPanelController.ClearSelectedControl();
            panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private void CycleQualityFilterReverse()
        {
            qualityMode = InventoryFilterManager.GetPreviousQualityMode(qualityMode);
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
            if (criteria != null && criteria.UsesQualityFilter) {
                groupCriteria = CreateCriteria();
                groupCriteria.QualityMode = QualityQuickFilterMode.Off;
                qualityCriteria = new PartFilterCriteria {
                    Context = PartFilterContext.Garage,
                    QualityMode = criteria.QualityMode,
                };
            }

            foreach (ChoosePartDownItem item in originalItems) {
                if (item != null && item.BaseItem != null &&
                    MatchesItem(item.BaseItem, criteria,
                        groupCriteria, qualityCriteria))
                    filtered.Add(item);
            }

            return filtered;
        }

        private bool MatchesItem(BaseItem baseItem,
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

        private bool MatchesActiveFilter(BaseItem baseItem)
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

            return MatchesItem(baseItem, criteria, groupCriteria,
                qualityCriteria);
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
                    "filtered " + itemListName + " list." + Environment.NewLine +
                    exception, Types.LoggingLevels.Warning);
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
                    "native " + itemListName + " list." + Environment.NewLine +
                    exception, Types.LoggingLevels.Warning);
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
                RestoreFilteredSelectionUi();
                RestoreSeparateEmptyState();
                return;
            }

            if (!isEmpty && !awaitingFilteredSelection) {
                RestoreFilteredSelectionUi();
                RestoreSeparateEmptyState();
                return;
            }

            if (isEmpty)
                ClearCurrentPreviewItem();

            if (activeUpWindow != null &&
                activeUpWindow.choosePartUpWindowType ==
                    separateType) {
                RestoreFilteredSelectionUi();
                if (isEmpty)
                    ApplySeparateEmptyState();
                else {
                    awaitingFilteredSelection = false;
                    RestoreSeparateEmptyState();
                }
                return;
            }

            RestoreSeparateEmptyState();
            ApplyAssemblySelectionState(isEmpty);
        }

        private void ApplyAssemblySelectionState(bool isEmpty)
        {
            int segment = GetCurrentActiveSegment();
            if (segment <= 0) {
                HideAssemblyArrows(true, springClampLayout);
                RestoreCurrentDetail();
                if (springClampLayout)
                    RestoreFollowingDetail();
                HideEmptyState();
                HideItemsDetail();
                if (isEmpty)
                    EnsureAssemblyWindowEmptyState(true);
                else
                    HideAssemblyWindowEmptyState();
                return;
            }

            HideAssemblyWindowEmptyState();
            RestoreItemsDetail();
            if (springClampLayout) {
                RestoreCurrentDetail();
                RestoreFollowingDetail();
                if (segment == 1)
                    HideAssemblyArrows(true, true);
                else if (segment == 2)
                    HideAssemblyArrows(false, true);
                else
                    RestoreAssemblyArrows();
            } else {
                HideAssemblyArrows(true, false);
            }

            Transform currentDetail = GetCurrentDetail();
            HideCurrentDetail(currentDetail, segment);
            if (springClampLayout && segment == 1)
                HideFollowingDetail(GetDetail(2));
            EnsureEmptyState(currentDetail, segment, isEmpty);
        }

        private void ApplySeparateEmptyState()
        {
            if (activeUpWindow == null || activeDownWindow == null ||
                activeUpWindow.transform == null ||
                activeDownWindow.transform == null)
                return;

            Transform itemsDetail = activeUpWindow.transform.Find("ItemsDetail");
            Transform noItemsPage =
                activeDownWindow.transform.Find("NoItemsPage");
            if (itemsDetail == null || itemsDetail.gameObject == null)
                return;

            if (separateItemsDetailRoot != itemsDetail.gameObject) {
                RestoreSeparateItemsDetail();
                separateItemsDetailRoot = itemsDetail.gameObject;
                separateItemsDetailWasActive =
                    separateItemsDetailRoot.activeSelf;
            }
            if (separateItemsDetailRoot.activeSelf)
                separateItemsDetailRoot.SetActive(false);

            if (noItemsPage != null && noItemsPage.gameObject != null) {
                if (nativeDownEmptyStateRoot != noItemsPage.gameObject) {
                    RestoreNativeDownEmptyState();
                    nativeDownEmptyStateRoot = noItemsPage.gameObject;
                    nativeDownEmptyStateWasActive =
                        nativeDownEmptyStateRoot.activeSelf;
                }
                if (nativeDownEmptyStateRoot.activeSelf)
                    nativeDownEmptyStateRoot.SetActive(false);
            }

            EnsureSeparateWindowEmptyState();
            if (separateWindowEmptyStateRoot != null &&
                !separateWindowEmptyStateRoot.activeSelf)
                separateWindowEmptyStateRoot.SetActive(true);
        }

        private void RestoreSeparateEmptyState()
        {
            RestoreSeparateItemsDetail();
            RestoreNativeDownEmptyState();
            RestoreSeparateWindowEmptyState();
        }

        private void RestoreSeparateItemsDetail()
        {
            if (separateItemsDetailRoot != null &&
                separateItemsDetailRoot.activeSelf !=
                    separateItemsDetailWasActive)
                separateItemsDetailRoot.SetActive(
                    separateItemsDetailWasActive);
            separateItemsDetailRoot = null;
            separateItemsDetailWasActive = false;
        }

        private void RestoreNativeDownEmptyState()
        {
            if (nativeDownEmptyStateRoot != null &&
                nativeDownEmptyStateRoot.activeSelf !=
                    nativeDownEmptyStateWasActive)
                nativeDownEmptyStateRoot.SetActive(
                    nativeDownEmptyStateWasActive);
            nativeDownEmptyStateRoot = null;
            nativeDownEmptyStateWasActive = false;
        }

        private void EnsureSeparateWindowEmptyState()
        {
            if (activeUpWindow == null || activeUpWindow.transform == null)
                return;

            if (separateWindowEmptyStateRoot != null &&
                separateWindowEmptyStateRoot.transform.parent !=
                    activeUpWindow.transform) {
                UnityEngine.Object.Destroy(separateWindowEmptyStateRoot);
                separateWindowEmptyStateRoot = null;
            }
            if (separateWindowEmptyStateRoot != null)
                return;

            separateWindowEmptyStateRoot =
                NativeUiFactory.CreateNativeNoItemsPage(
                    activeUpWindow.transform);
            if (separateWindowEmptyStateRoot == null)
                return;

            separateWindowEmptyStateRoot.name =
                separateEmptyStateName;
            RectTransform rect =
                separateWindowEmptyStateRoot.GetComponent<RectTransform>();
            NativeUiFactory.Stretch(rect, 0f, 0f, 0f, 0f);
            separateWindowEmptyStateRoot.transform.SetAsLastSibling();
        }

        private void RestoreSeparateWindowEmptyState()
        {
            if (separateWindowEmptyStateRoot != null &&
                separateWindowEmptyStateRoot.activeSelf)
                separateWindowEmptyStateRoot.SetActive(false);
        }

        private void EnsureEmptyState(Transform currentDetail,
            int segment, bool showNoItems)
        {
            if (currentDetail == null || currentDetail.parent == null)
                return;

            if (emptyStateRoot != null &&
                (emptyStateRoot.transform.parent != currentDetail.parent ||
                 emptyStateSegment != segment ||
                 emptyStateShowsNoItems != showNoItems)) {
                UnityEngine.Object.Destroy(emptyStateRoot);
                emptyStateRoot = null;
                emptyStateSegment = -1;
                emptyStateShowsNoItems = false;
            }
            if (emptyStateRoot == null) {
                emptyStateRoot = NativeUiFactory.CreateNativeNoItemsPage(
                    currentDetail.parent);
                if (emptyStateRoot == null)
                    return;

                emptyStateRoot.name = selectionPromptName;
                RectTransform sourceRect =
                    currentDetail.GetComponent<RectTransform>();
                RectTransform emptyRect =
                    emptyStateRoot.GetComponent<RectTransform>();
                NativeUiFactory.CopyRect(sourceRect, emptyRect);
                emptyStateRoot.transform.SetAsLastSibling();

                Graphic[] graphics =
                    emptyStateRoot.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++) {
                    if (graphics[i] != null)
                        graphics[i].raycastTarget = false;
                }

                if (!showNoItems) {
                    Text text = emptyStateRoot.GetComponentInChildren<Text>(true);
                    if (text != null)
                        text.text = ModLocalization.Get("LOC_SelectPartPrompt");
                }
                emptyStateSegment = segment;
                emptyStateShowsNoItems = showNoItems;
            }

            if (!emptyStateRoot.activeSelf)
                emptyStateRoot.SetActive(true);
        }

        private void HideEmptyState()
        {
            if (emptyStateRoot != null && emptyStateRoot.activeSelf)
                emptyStateRoot.SetActive(false);
        }

        private void EnsureAssemblyWindowEmptyState(bool showNoItems)
        {
            if (activeUpWindow == null || activeUpWindow.transform == null)
                return;

            if (assemblyWindowEmptyStateRoot != null &&
                (assemblyWindowEmptyStateRoot.transform.parent !=
                    activeUpWindow.transform ||
                 assemblyWindowEmptyStateShowsNoItems != showNoItems)) {
                UnityEngine.Object.Destroy(assemblyWindowEmptyStateRoot);
                assemblyWindowEmptyStateRoot = null;
            }
            if (assemblyWindowEmptyStateRoot == null) {
                assemblyWindowEmptyStateRoot =
                    NativeUiFactory.CreateNativeNoItemsPage(
                        activeUpWindow.transform);
                if (assemblyWindowEmptyStateRoot == null)
                    return;

                assemblyWindowEmptyStateRoot.name =
                    assemblyEmptyStateName;
                RectTransform rect = assemblyWindowEmptyStateRoot
                    .GetComponent<RectTransform>();
                NativeUiFactory.Stretch(rect, 0f, 0f, 0f, 0f);
                assemblyWindowEmptyStateRoot.transform.SetAsLastSibling();

                Graphic[] graphics = assemblyWindowEmptyStateRoot
                    .GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++) {
                    if (graphics[i] != null)
                        graphics[i].raycastTarget = false;
                }

                if (!showNoItems) {
                    Text text = assemblyWindowEmptyStateRoot
                        .GetComponentInChildren<Text>(true);
                    if (text != null)
                        text.text = ModLocalization.Get("LOC_SelectPartPrompt");
                }
                assemblyWindowEmptyStateShowsNoItems = showNoItems;
            }

            if (!assemblyWindowEmptyStateRoot.activeSelf)
                assemblyWindowEmptyStateRoot.SetActive(true);
        }

        private void HideAssemblyWindowEmptyState()
        {
            if (assemblyWindowEmptyStateRoot != null &&
                assemblyWindowEmptyStateRoot.activeSelf)
                assemblyWindowEmptyStateRoot.SetActive(false);
        }

        private void ClearCurrentPreviewItem()
        {
            if (activeUpWindow == null)
                return;

            if (CurrentItemField != null)
                CurrentItemField.SetValue(activeUpWindow, null);
            else if (CurrentItemProperty != null &&
                CurrentItemProperty.CanWrite)
                CurrentItemProperty.SetValue(activeUpWindow, null, null);
        }

        private Transform GetCurrentDetail()
        {
            return GetDetail(GetCurrentActiveSegment());
        }

        private Transform GetDetail(int segment)
        {
            if (activeUpWindow == null || activeUpWindow.transform == null ||
                segment < 0 || segment > 2)
                return null;

            Transform detailsRoot = activeUpWindow.transform.Find("ItemsDetail");
            if (detailsRoot == null)
                return null;

            string detailName = segment == 0
                ? "InventoryItemDetails"
                : "InventoryItemDetails (" + segment + ")";
            return detailsRoot.Find(detailName);
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

        private void HideCurrentDetail(Transform detail, int segment)
        {
            if (detail == null || detail.gameObject == null)
                return;

            if (hiddenCurrentDetailRoot != detail.gameObject ||
                hiddenCurrentDetailSegment != segment) {
                RestoreCurrentDetail();
                hiddenCurrentDetailRoot = detail.gameObject;
                hiddenCurrentDetailWasActive = hiddenCurrentDetailRoot.activeSelf;
                hiddenCurrentDetailSegment = segment;
            }
            if (hiddenCurrentDetailRoot.activeSelf)
                hiddenCurrentDetailRoot.SetActive(false);
        }

        private void HideFollowingDetail(Transform detail)
        {
            if (detail == null || detail.gameObject == null)
                return;

            if (hiddenFollowingDetailRoot != detail.gameObject) {
                RestoreFollowingDetail();
                hiddenFollowingDetailRoot = detail.gameObject;
                hiddenFollowingDetailWasActive =
                    hiddenFollowingDetailRoot.activeSelf;
            }
            if (hiddenFollowingDetailRoot.activeSelf)
                hiddenFollowingDetailRoot.SetActive(false);
        }

        private void RestoreFollowingDetail()
        {
            if (hiddenFollowingDetailRoot != null &&
                hiddenFollowingDetailRoot.activeSelf !=
                    hiddenFollowingDetailWasActive)
                hiddenFollowingDetailRoot.SetActive(
                    hiddenFollowingDetailWasActive);
            hiddenFollowingDetailRoot = null;
            hiddenFollowingDetailWasActive = false;
        }

        private int GetCurrentActiveSegment()
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

        private void RestoreCurrentDetail()
        {
            if (hiddenCurrentDetailRoot != null &&
                hiddenCurrentDetailRoot.activeSelf != hiddenCurrentDetailWasActive)
                hiddenCurrentDetailRoot.SetActive(hiddenCurrentDetailWasActive);
            hiddenCurrentDetailRoot = null;
            hiddenCurrentDetailWasActive = false;
            hiddenCurrentDetailSegment = -1;
        }

        private void HideAssemblyArrows(bool hideArrow1,
            bool hideArrow2)
        {
            RestoreAssemblyArrows();
            if (hideArrow1)
                HideAssemblyArrow(0, ref hiddenArrow1Root,
                    ref hiddenArrow1WasActive);
            if (hideArrow2)
                HideAssemblyArrow(1, ref hiddenArrow2Root,
                    ref hiddenArrow2WasActive);
        }

        private void HideAssemblyArrow(int arrowIndex,
            ref GameObject hiddenRoot, ref bool wasActive)
        {
            GameObject root = GetArrowSpacer(arrowIndex);
            if (root == null)
                return;

            hiddenRoot = root;
            wasActive = root.activeSelf;
            if (root.activeSelf)
                root.SetActive(false);
        }

        private GameObject GetArrowSpacer(int arrowIndex)
        {
            if (activeUpWindow == null || activeUpWindow.transform == null)
                return null;

            Transform itemsDetail = activeUpWindow.transform.Find("ItemsDetail");
            if (itemsDetail == null)
                return null;

            int currentIndex = 0;
            for (int i = 0; i < itemsDetail.childCount; i++) {
                Transform child = itemsDetail.GetChild(i);
                if (child == null || child.name != "ArrowSpacer")
                    continue;
                if (currentIndex == arrowIndex)
                    return child.gameObject;
                currentIndex++;
            }
            return null;
        }

        private void RestoreAssemblyArrows()
        {
            RestoreAssemblyArrow(ref hiddenArrow1Root,
                ref hiddenArrow1WasActive);
            RestoreAssemblyArrow(ref hiddenArrow2Root,
                ref hiddenArrow2WasActive);
        }

        private void RestoreAssemblyArrow(ref GameObject hiddenRoot,
            ref bool wasActive)
        {
            if (hiddenRoot != null && hiddenRoot.activeSelf != wasActive)
                hiddenRoot.SetActive(wasActive);
            hiddenRoot = null;
            wasActive = false;
        }

        private void RestoreFilteredSelectionUi()
        {
            RestoreAssemblyArrows();
            RestoreItemsDetail();
            RestoreCurrentDetail();
            if (springClampLayout)
                RestoreFollowingDetail();
            HideEmptyState();
            HideAssemblyWindowEmptyState();
        }

        private void CreateResetHint()
        {
            if (resetHint != null && resetHint.Root != null)
                return;
            if (panel.SearchField == null || activeUpWindow == null ||
                activeUpWindow.uiDescription == null || activeDownWindow == null)
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

        private void DeactivateWindow()
        {
            RestoreFilteredSelectionUi();
            RestoreSeparateEmptyState();
            if (emptyStateRoot != null) {
                UnityEngine.Object.Destroy(emptyStateRoot);
                emptyStateRoot = null;
                emptyStateSegment = -1;
                emptyStateShowsNoItems = false;
            }
            if (separateWindowEmptyStateRoot != null) {
                UnityEngine.Object.Destroy(separateWindowEmptyStateRoot);
                separateWindowEmptyStateRoot = null;
            }
            if (assemblyWindowEmptyStateRoot != null) {
                UnityEngine.Object.Destroy(assemblyWindowEmptyStateRoot);
                assemblyWindowEmptyStateRoot = null;
                assemblyWindowEmptyStateShowsNoItems = false;
            }
            DestroyResetHint();
            panel.Detach();
            originalItems.Clear();
            activeDownWindow = null;
            activeUpWindow = null;
            applyingFilteredList = false;
            awaitingFilteredSelection = false;
        }
    }
}
