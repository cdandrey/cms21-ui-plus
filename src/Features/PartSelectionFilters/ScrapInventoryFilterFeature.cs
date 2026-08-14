using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppCMS;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Logic.ChoosePartDown;
using Il2CppCMS.UI.Logic.Scrap;
#else
using UnhollowerRuntimeLib;
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Description;
using CMS.UI.Logic;
using CMS.UI.Windows;
using CMS.UI.Logic.ChoosePartDown;
using CMS.UI.Logic.Scrap;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Provides independently configurable filters and bulk scrapping for the scrap
    /// inventory window. Holding Space opens a normal yes/no confirmation and limits
    /// the game's bulk-scrap operation to the current visible result. The stock
    /// timed-release description keeps the native single-part action, while a paired
    /// stock hold description handles bulk scrapping and cancels when released early.
    /// With no active filters, that result is the complete unlocked loose-part
    /// inventory.
    /// </summary>
    public static class ScrapInventoryFilterFeature
    {
        private const string NativeBulkDescriptionObjectName =
            "QScrapFilteredBulkDescription";
        private const float BulkScrapAllCondition = 101f;
        private const float NativeBulkHoldDurationSeconds = 1f;
        private const int BulkInputSuppressionFrames = 8;

        private static readonly List<ChoosePartDownItem> OriginalItems =
            new List<ChoosePartDownItem>();
        private static readonly List<ChoosePartDownItem> FilteredItems =
            new List<ChoosePartDownItem>();
        private static readonly HashSet<long> PendingBulkItemUids =
            new HashSet<long>();
        private static readonly PartFilterPanelController Panel =
            new PartFilterPanelController("QScrapInventoryFilter");
        private static readonly List<ScrapMiniGameObjectState>
            HiddenMiniGameObjects = new List<ScrapMiniGameObjectState>();
        private static readonly List<ScrapMiniGameObjectState>
            HiddenUpgradeObjects = new List<ScrapMiniGameObjectState>();

        private static ChoosePartDownWindow activeWindow;
        private static ScrapWindow activeScrapWindow;
        private static ScrapProduction activeScrapProduction;
        private static Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
            preFilteredUpgradeList;
        private static UIDescription nativeBulkDescriptionOwner;
        private static ControlDescription nativeSingleDescription;
        private static DescriptionInputHandlingMethod
            nativeSingleInputHandlingMethod;
        private static bool nativeSingleInputHandlingMethodCaptured;
        private static ControlDescription nativeBulkDescription;
        private static GameObject nativeBulkDescriptionObject;
        private static NativeUiFactory.FooterHintHandle nativeBulkHint;
        private static NativeUiFactory.FooterHintHandle resetHint;
        private static string nativeBulkBaseLabel;
        private static GarageConditionFilterMode conditionMode =
            GarageConditionFilterMode.Off;
        private static RepairabilityQuickFilterMode repairabilityMode =
            RepairabilityQuickFilterMode.Off;
        private static QualityQuickFilterMode qualityMode =
            QualityQuickFilterMode.Off;
        private static bool applyingFilteredList;
        private static bool pendingFilteredBulkScrap;
        private static bool filteredBulkScrapInProgress;
        private static bool bulkConfirmationOpen;
        private static bool bulkExecutionScheduled;
        private static int shortcutConsumedFrame = -1;
        private static int suppressNativeScrapInputUntilFrame = -1;
        private static bool scrapMiniGameHidden;
        private static bool scrapUpgradeUiHidden;
        private static bool upgradeTabTransitionPending;
        private static bool scrapResultPending;
        private static bool nativeBulkInitializationPending;
        private static readonly Canvas.WillRenderCanvases
            NativeBulkInitializationHandler =
                DelegateSupport.ConvertDelegate<Canvas.WillRenderCanvases>(
                    new Action(OnNativeBulkInitializationRender));

        private sealed class ScrapMiniGameObjectState
        {
            internal GameObject Target;
            internal bool WasActive;
        }

        private sealed class BulkScrapInventoryState
        {
            internal Inventory Inventory;
            internal readonly List<Item> OriginalOrder = new List<Item>();
            internal readonly List<GroupItem> OriginalGroups =
                new List<GroupItem>();
            internal readonly HashSet<long> SelectedUids = new HashSet<long>();
            internal bool Restored;
        }

        public static bool IsApplyingFilteredList {
            get { return applyingFilteredList; }
        }

        public static void OnWindowShown(ChoosePartDownWindow window,
            object itemsToShowArgument)
        {
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> itemsToShow =
                itemsToShowArgument as
                    Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>;
            if (!IsAnyScrapInventoryFeatureEnabled() ||
                !IsScrapWindowActive()) {
                DeactivateWindow();
                return;
            }
            if (window == null)
                return;

            activeWindow = window;
            ResolveScrapWindow();
            ResolveScrapProduction();
            bool enteringUpgrade = upgradeTabTransitionPending;
            upgradeTabTransitionPending = false;
            bool includeConditionFilter = !enteringUpgrade &&
                !IsScrapUpgradeMode();
            bool preFilteredUpgrade = IsPreFilteredUpgradeList(itemsToShow);
            if (!preFilteredUpgrade)
                CaptureNativeItems(itemsToShow);

            if (AreScrapFiltersEnabled()) {
                if (Panel.AttachWithButtons(window.transform,
                    CycleConditionFilter, CycleRepairabilityFilter,
                    CycleQualityFilter, OnSearchChanged,
                    includeConditionFilter, true, true,
                    CycleConditionFilterReverse,
                    CycleRepairabilityFilterReverse,
                    CycleQualityFilterReverse)) {
                    Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
                    if (!enteringUpgrade)
                        CreateResetHint();
                    ApplyCurrentFilters(true);
                } else {
                    Panel.Detach();
                    SyncCurrentResultToOriginal();
                }
            } else {
                Panel.Detach();
                SyncCurrentResultToOriginal();
            }

            if (enteringUpgrade)
                RebuildUpgradeFooterHints(
                    itemsToShow != null ? itemsToShow.Count : 0);
            else
                UpdateNativeBulkDescriptionForCurrentMode();
            if (preFilteredUpgrade)
                preFilteredUpgradeList = null;
        }

        public static void FilterNativeListBeforeRefresh(
            ChoosePartPageManager pageManager,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (activeWindow == null ||
                !IsAnyScrapInventoryFeatureEnabled() ||
                applyingFilteredList || !IsScrapWindowActive() ||
                items == null)
                return;

            if (IsPreFilteredUpgradeList(items)) {
                PreparePageManagerBeforeFilteredRefresh(pageManager, items);
                preFilteredUpgradeList = null;
            } else {
                CaptureNativeItems(items);

                if (AreScrapFiltersEnabled()) {
                    items = CreateFilteredNativeList();
                    PreparePageManagerBeforeFilteredRefresh(pageManager, items);
                } else {
                    SyncCurrentResultToOriginal();
                }
            }

            UpdateNativeBulkDescriptionForCurrentMode();
        }

        public static void FilterUpgradeItemsBeforeNativeSelection(
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (activeWindow == null || !IsScrapWindowActive()) {
                preFilteredUpgradeList = null;
                return;
            }

            upgradeTabTransitionPending = true;
            if (items != null) {
                CaptureNativeItems(items);
                preFilteredUpgradeList = items;
            } else {
                preFilteredUpgradeList = null;
            }
            if (AreScrapFiltersEnabled())
                ResetFilterState();
            RebuildUpgradeFooterHints(items != null ? items.Count : 0);
        }

        public static void OnScrapWindowHidden()
        {
            ResetFilterState();
            DeactivateWindow();
        }

        public static void OnNativeListRefreshed(
            ChoosePartPageManager pageManager,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (activeWindow == null || applyingFilteredList ||
                !IsAnyScrapInventoryFeatureEnabled() ||
                !IsScrapWindowActive())
                return;

            int itemCount = items != null ? items.Count : 0;
            ApplyFilteredEmptyState(pageManager, itemCount == 0, itemCount);

            UpdateNativeBulkDescriptionForCurrentMode();
        }

        internal static void OnNativeScrapItemsBuilt(
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (activeWindow == null || !IsScrapWindowActive() ||
                !IsScrapProductionMode() || items == null)
                return;
            CaptureNativeItems(items);
        }

        public static void OnInputFieldKeyPressed(InputField inputField)
        {
            if (!AreScrapFiltersEnabled() || activeWindow == null ||
                !IsScrapWindowActive())
                return;

            Panel.HandleKeyPressed(inputField);
        }

        public static void OnScrapProcessed(ScrapProduction scrapProduction)
        {
            if (!IsAnyScrapInventoryFeatureEnabled() ||
                activeWindow == null)
                return;

            scrapResultPending = true;
            MelonCoroutines.Start(RefreshAfterScrapAction(scrapProduction));
        }

        public static void OnScrapStarted()
        {
            if (activeWindow != null && IsScrapWindowActive())
                WindowFooterHintController.SuspendWindow("Scrap");
        }

        public static void OnScrapCancelled()
        {
            if (!scrapResultPending && activeWindow != null &&
                IsScrapWindowActive())
                WindowFooterHintController.ResumeWindow("Scrap");
        }

        public static void Update()
        {
            if (activeWindow == null) {
                DestroyResetHint();
                CancelNativeBulkInteraction();
                return;
            }

            if (!IsScrapWindowActive()) {
                DestroyResetHint();
                CancelNativeBulkInteraction();
                SetNativeBulkDescriptionActive(false);
                return;
            }

            if (AreScrapFiltersEnabled()) {
                if (Input.GetKeyDown(KeyCode.LeftAlt))
                    ResetFilters();
            } else {
                DestroyResetHint();
            }

            if (!IsBulkScrapShortcutEnabled()) {
                CancelNativeBulkInitialization();
                DestroyNativeBulkDescription();
            }
        }

        private static void ResetFilterState()
        {
            conditionMode = GarageConditionFilterMode.Off;
            repairabilityMode = RepairabilityQuickFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            Panel.ResetSearch();
            Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
        }

        private static void ResetFilters()
        {
            ResetFilterState();
            ApplyCurrentFilters(true);
        }

        private static void CreateResetHint()
        {
            CreateResetHint(GetScrapFooterProfile(false), OriginalItems.Count);
        }

        private static void CreateResetHint(
            WindowFooterHintController.NativeFooterProfile profile,
            int itemCount)
        {
            if (resetHint != null && resetHint.Root != null)
                return;
            if (activeScrapProduction == null ||
                activeScrapProduction.uiDescription == null)
                return;

            resetHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = "Scrap",
                    WindowRoot = activeWindow.transform,
                    HintRoot = activeScrapProduction.uiDescription.transform,
                    HintId = "Hint_ResetScrapFilters",
                    Keys = new string[] { "LeftAlt" },
                    Text = ModLocalization.Get("LOC_ResetFiltersAction"),
                    Action = new Action(ResetFilters),
                    Row = 0,
                    Order = 10,
                    Profile = profile,
                    ItemCount = itemCount,
                });
        }

        private static void RebuildUpgradeFooterHints(int itemCount)
        {
            CancelNativeBulkInitialization();
            DestroyNativeBulkDescription();
            DestroyResetHint();
            if (!AreScrapFiltersEnabled() || itemCount == 0)
                return;

            CreateResetHint(
                WindowFooterHintController.NativeFooterProfile
                    .ScrapUpgradePopulated,
                itemCount);
        }

        private static void DestroyResetHint()
        {
            WindowFooterHintController.RemoveHint("Scrap",
                "Hint_ResetScrapFilters");
            resetHint = null;
        }

        public static bool ShouldSuppressNativeScrapStart()
        {
            if (shortcutConsumedFrame == Time.frameCount ||
                Time.frameCount <= suppressNativeScrapInputUntilFrame ||
                bulkConfirmationOpen || bulkExecutionScheduled ||
                pendingFilteredBulkScrap || filteredBulkScrapInProgress)
                return true;

            if (AreScrapFiltersEnabled() && activeWindow != null &&
                IsScrapWindowActive()) {
                ResolveScrapProduction();
                if (FilteredItems.Count == 0 ||
                    activeScrapProduction == null ||
                    activeScrapProduction.currentItem == null ||
                    !PartFilterRules.Matches(activeScrapProduction.currentItem,
                        CreateCriteria()))
                    return true;
            }

            return false;
        }

        public static bool ShouldDeferInventorySave()
        {
            return filteredBulkScrapInProgress;
        }

        private static bool PrepareFilteredBulkScrap(Inventory inventory,
            ref float condition, out BulkScrapInventoryState state)
        {
            state = null;
            if (!pendingFilteredBulkScrap)
                return true;
            if (inventory == null || PendingBulkItemUids.Count == 0) {
                CancelPendingFilteredBulkScrap();
                return false;
            }

            pendingFilteredBulkScrap = false;

            Il2CppSystem.Collections.Generic.List<Item> nativeItems =
                inventory.items;
            if (nativeItems == null) {
                CancelPendingFilteredBulkScrap();
                return false;
            }

            BulkScrapInventoryState newState = new BulkScrapInventoryState();
            newState.Inventory = inventory;
            foreach (long uid in PendingBulkItemUids)
                newState.SelectedUids.Add(uid);

            Il2CppSystem.Collections.Generic.List<GroupItem> nativeGroups =
                inventory.groups;
            if (nativeGroups != null) {
                foreach (GroupItem group in nativeGroups) {
                    if (group != null)
                        newState.OriginalGroups.Add(group);
                }
            }

            Il2CppSystem.Collections.Generic.List<Item> selectedItems =
                new Il2CppSystem.Collections.Generic.List<Item>();
            foreach (Item item in nativeItems) {
                if (item == null)
                    continue;

                newState.OriginalOrder.Add(item);
                if (newState.SelectedUids.Contains(item.UID))
                    selectedItems.Add(item);
            }

            PendingBulkItemUids.Clear();
            if (selectedItems.Count == 0) {
                ModLogger.Log("[ScrapInventoryFilter] Filtered bulk scrap was " +
                    "cancelled because none of the selected entries still exists in " +
                    "the player inventory.", Types.LoggingLevels.Warning);
                return false;
            }

            state = newState;
            filteredBulkScrapInProgress = true;
            try {
                inventory.items = selectedItems;
                inventory.groups =
                    new Il2CppSystem.Collections.Generic.List<GroupItem>();
                condition = BulkScrapAllCondition;

                ModLogger.Log("[ScrapInventoryFilter] Native bulk scrap restricted to " +
                    selectedItems.Count + " selected loose part(s).",
                    Types.LoggingLevels.Normal);
                return true;
            } catch (Exception exception) {
                ModLogger.Log("[ScrapInventoryFilter] Failed to prepare the " +
                    "temporary filtered inventory." + Environment.NewLine +
                    exception, Types.LoggingLevels.Error);
                RestoreInventoryAfterFilteredBulkScrap(newState);
                state = null;
                return false;
            }
        }

        private static void RestoreInventoryAfterFilteredBulkScrap(
            BulkScrapInventoryState state)
        {
            if (state == null || state.Restored || state.Inventory == null)
                return;

            state.Restored = true;
            Inventory inventory = state.Inventory;
            HashSet<long> survivingSelectedUids = new HashSet<long>();
            Il2CppSystem.Collections.Generic.List<Item> currentItems =
                inventory.items;
            if (currentItems != null) {
                foreach (Item item in currentItems) {
                    if (item != null)
                        survivingSelectedUids.Add(item.UID);
                }
            }

            Il2CppSystem.Collections.Generic.List<Item> restoredItems =
                new Il2CppSystem.Collections.Generic.List<Item>();
            foreach (Item item in state.OriginalOrder) {
                if (item == null)
                    continue;

                if (!state.SelectedUids.Contains(item.UID) ||
                    survivingSelectedUids.Contains(item.UID))
                    restoredItems.Add(item);
            }

            Il2CppSystem.Collections.Generic.List<GroupItem> restoredGroups =
                new Il2CppSystem.Collections.Generic.List<GroupItem>();
            foreach (GroupItem group in state.OriginalGroups) {
                if (group != null)
                    restoredGroups.Add(group);
            }

            inventory.items = restoredItems;
            inventory.groups = restoredGroups;
            filteredBulkScrapInProgress = false;
            try {
                inventory.Save();
            } catch (Exception exception) {
                ModLogger.Log("[ScrapInventoryFilter] Failed to save the restored " +
                    "inventory after filtered bulk scrap." + Environment.NewLine +
                    exception, Types.LoggingLevels.Warning);
            }

            MelonCoroutines.Start(RefreshAfterFilteredBulkScrap());
        }

        public static void CancelPendingFilteredBulkScrap()
        {
            pendingFilteredBulkScrap = false;
            bulkConfirmationOpen = false;
            bulkExecutionScheduled = false;
            PendingBulkItemUids.Clear();
        }

        public static void ResetAll()
        {
            DeactivateWindow();
            OriginalItems.Clear();
            FilteredItems.Clear();
            conditionMode = GarageConditionFilterMode.Off;
            repairabilityMode = RepairabilityQuickFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            applyingFilteredList = false;
            filteredBulkScrapInProgress = false;
            suppressNativeScrapInputUntilFrame = -1;
            CancelNativeBulkInteraction();
            CancelPendingFilteredBulkScrap();
        }

        private static bool AreScrapFiltersEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addScrapInventoryFilters;
        }

        private static bool IsScrapFilterPanelVisible()
        {
            InputField searchField = Panel.SearchField;
            return searchField != null && searchField.gameObject != null &&
                searchField.gameObject.activeInHierarchy;
        }

        private static bool IsBulkScrapShortcutEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addBulkScrapShortcut;
        }

        private static bool IsAnyScrapInventoryFeatureEnabled()
        {
            return AreScrapFiltersEnabled() ||
                IsBulkScrapShortcutEnabled();
        }

        private static bool IsScrapSearchFocused()
        {
            return AreScrapFiltersEnabled() && Panel.IsSearchFocused;
        }

        private static bool IsScrapWindowActive()
        {
            return WindowManager.Instance != null &&
                WindowManager.Instance.IsWindowActive(WindowID.Scrap);
        }

        private static bool IsAskWindowActive()
        {
            return WindowManager.Instance != null &&
                WindowManager.Instance.IsWindowActive(WindowID.AskWindow);
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

        private static bool IsPreFilteredUpgradeList(
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            return preFilteredUpgradeList != null && items != null &&
                items.Pointer == preFilteredUpgradeList.Pointer;
        }

        private static void SyncCurrentResultToOriginal()
        {
            FilteredItems.Clear();
            foreach (ChoosePartDownItem item in OriginalItems) {
                if (item != null)
                    FilteredItems.Add(item);
            }
        }

        private static void CycleConditionFilter()
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
                default:
                    conditionMode = GarageConditionFilterMode.Off;
                    break;
            }

            PartFilterPanelController.ClearSelectedControl();
            Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CycleConditionFilterReverse()
        {
            switch (conditionMode) {
                case GarageConditionFilterMode.Off:
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
            Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CycleRepairabilityFilter()
        {
            switch (repairabilityMode) {
                case RepairabilityQuickFilterMode.Off:
                    repairabilityMode =
                        RepairabilityQuickFilterMode.RepairGroupOnly;
                    break;
                case RepairabilityQuickFilterMode.RepairGroupOnly:
                    repairabilityMode =
                        RepairabilityQuickFilterMode.NonRepairableOnly;
                    break;
                default:
                    repairabilityMode = RepairabilityQuickFilterMode.Off;
                    break;
            }

            PartFilterPanelController.ClearSelectedControl();
            Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CycleRepairabilityFilterReverse()
        {
            repairabilityMode = InventoryFilterManager
                .GetPreviousRepairabilityMode(repairabilityMode);
            PartFilterPanelController.ClearSelectedControl();
            Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CycleQualityFilter()
        {
            qualityMode = InventoryFilterManager.GetNextQualityMode(qualityMode);
            PartFilterPanelController.ClearSelectedControl();
            Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CycleQualityFilterReverse()
        {
            qualityMode = InventoryFilterManager.GetPreviousQualityMode(qualityMode);
            PartFilterPanelController.ClearSelectedControl();
            Panel.UpdateVisuals(conditionMode, repairabilityMode, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void OnSearchChanged(string searchText)
        {
            ApplyCurrentFilters(true);
        }

        private static PartFilterCriteria CreateCriteria()
        {
            PartFilterCriteria criteria = new PartFilterCriteria();
            criteria.Context = PartFilterContext.Garage;
            criteria.SearchText = Panel.SearchText;
            criteria.GarageConditionMode = conditionMode;
            criteria.RepairabilityMode = repairabilityMode;
            criteria.QualityMode = qualityMode;
            criteria.OwnedMode = OwnedQuickFilterMode.Off;
            return criteria;
        }

        private static Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
            CreateFilteredNativeList()
        {
            PartFilterCriteria criteria = CreateCriteria();
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                new Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>();
            FilteredItems.Clear();

            foreach (ChoosePartDownItem item in OriginalItems) {
                if (item == null || item.BaseItem == null ||
                    !PartFilterRules.Matches(item.BaseItem, criteria))
                    continue;

                FilteredItems.Add(item);
                filtered.Add(item);
            }

            return filtered;
        }

        private static void PreparePageManagerBeforeFilteredRefresh(
            ChoosePartPageManager pageManager,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (pageManager == null || items == null)
                return;

            // ChoosePartPageManager.Refresh reads its existing items collection
            // before replacing it. Preloading the filtered collection prevents
            // the native method from drawing and selecting stale unfiltered rows.
            pageManager.items = items;
            ApplyFilteredEmptyState(pageManager, items.Count == 0,
                items.Count);
        }

        private static void ApplyFilteredEmptyState(
            ChoosePartPageManager pageManager, bool isEmpty, int itemCount)
        {
            ResolveScrapProduction();

            WindowFooterHintController.SetNativeProfile("Scrap",
                GetScrapFooterProfile(isEmpty), itemCount);
            bool upgradeMode = IsScrapUpgradeMode();
            if (!AreScrapFiltersEnabled() || !IsScrapFilterPanelVisible())
                DestroyResetHint();
            else
                CreateResetHint(GetScrapFooterProfile(isEmpty), itemCount);

            if (isEmpty && pageManager != null) {
                pageManager.currentPage = 0;
                pageManager.currentPageItemsCount = 0;
            }

            if (upgradeMode) {
                ApplyScrapUpgradeEmptyState(isEmpty);
                return;
            }

            RestoreScrapUpgradeUi();

            if (activeScrapProduction == null)
                return;

            if (isEmpty)
                activeScrapProduction.currentItem = null;

            InventoryItemDetails itemDetails =
                activeScrapProduction.inventoryItemDetails;
            if (itemDetails != null && itemDetails.gameObject != null &&
                itemDetails.gameObject.activeSelf == isEmpty)
                itemDetails.gameObject.SetActive(!isEmpty);

            if (activeScrapProduction.noItemsPage != null &&
                activeScrapProduction.noItemsPage.activeSelf != isEmpty)
                activeScrapProduction.noItemsPage.SetActive(isEmpty);

            SetScrapMiniGameHidden(isEmpty);
        }

        private static void ApplyScrapUpgradeEmptyState(bool isEmpty)
        {
            ResolveScrapWindow();
            if (activeScrapWindow == null ||
                activeScrapWindow.scrapUpgrade == null)
                return;

            var scrapUpgrade = activeScrapWindow.scrapUpgrade;
            if (!isEmpty) {
                RestoreScrapUpgradeUi();
                if (scrapUpgrade.noItemsPage != null &&
                    scrapUpgrade.noItemsPage.activeSelf)
                    scrapUpgrade.noItemsPage.SetActive(false);
                return;
            }

            scrapUpgrade.currentItem = null;
            if (!scrapUpgradeUiHidden) {
                HiddenUpgradeObjects.Clear();
                RememberAndHideScrapUpgradeObject(
                    FindScrapUpgradeInformationBlock(scrapUpgrade.transform,
                        scrapUpgrade.currentValue, scrapUpgrade.upgradedValue,
                        scrapUpgrade.noItemsPage));
                RememberAndHideScrapUpgradeObject(
                    FindScrapUpgradeInformationBlock(scrapUpgrade.transform,
                        scrapUpgrade.upgradedValue, scrapUpgrade.currentValue,
                        scrapUpgrade.noItemsPage));
                RememberAndHideScrapUpgradeObject(
                    scrapUpgrade.inventoryItemDetails);
                RememberAndHideScrapUpgradeObject(scrapUpgrade.cost);
                RememberAndHideScrapUpgradeObject(scrapUpgrade.costTitle);
                RememberAndHideScrapUpgradeObject(scrapUpgrade.costLine);
                RememberAndHideScrapUpgradeObject(scrapUpgrade.arrow);
                if (scrapUpgrade.currentQualityStars != null) {
                    foreach (Image star in scrapUpgrade.currentQualityStars)
                        RememberAndHideScrapUpgradeObject(star);
                }
                RememberAndHideScrapUpgradeObject(scrapUpgrade.currentValue);
                RememberAndHideScrapUpgradeObject(scrapUpgrade.currentTuning);
                if (scrapUpgrade.upgradedQualityStars != null) {
                    foreach (Image star in scrapUpgrade.upgradedQualityStars)
                        RememberAndHideScrapUpgradeObject(star);
                }
                RememberAndHideScrapUpgradeObject(scrapUpgrade.upgradedValue);
                RememberAndHideScrapUpgradeObject(scrapUpgrade.upgradedTuning);
                RememberAndHideScrapUpgradeObject(
                    scrapUpgrade.upgradedCanvasGroup);
                scrapUpgradeUiHidden = true;
            }

            if (scrapUpgrade.noItemsPage != null &&
                !scrapUpgrade.noItemsPage.activeSelf)
                scrapUpgrade.noItemsPage.SetActive(true);
        }

        private static Transform FindScrapUpgradeInformationBlock(
            Transform root, Component anchor, Component oppositeAnchor,
            GameObject noItemsPage)
        {
            if (root == null || anchor == null || oppositeAnchor == null)
                return null;

            Transform opposite = oppositeAnchor.transform;
            Transform common = anchor.transform;
            while (common != null && common != root &&
                opposite != common && !opposite.IsChildOf(common))
                common = common.parent;
            if (common == null)
                return null;

            Transform candidate = anchor.transform;
            while (candidate.parent != null && candidate.parent != common)
                candidate = candidate.parent;

            if (candidate == common || candidate == root ||
                (noItemsPage != null &&
                    noItemsPage.transform.IsChildOf(candidate)) ||
                Panel.IsUiUnder(candidate))
                return null;
            return candidate;
        }

        private static void RememberAndHideScrapUpgradeObject(
            Component component)
        {
            GameObject gameObject = component != null
                ? component.gameObject : null;
            if (gameObject == null || Panel.IsUiUnder(gameObject.transform))
                return;

            foreach (ScrapMiniGameObjectState state in HiddenUpgradeObjects) {
                if (state != null && state.Target == gameObject)
                    return;
            }

            HiddenUpgradeObjects.Add(new ScrapMiniGameObjectState {
                Target = gameObject,
                WasActive = gameObject.activeSelf,
            });
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private static void RestoreScrapUpgradeUi()
        {
            if (!scrapUpgradeUiHidden)
                return;

            foreach (ScrapMiniGameObjectState state in HiddenUpgradeObjects) {
                if (state == null || state.Target == null)
                    continue;
                if (state.Target.activeSelf != state.WasActive)
                    state.Target.SetActive(state.WasActive);
            }

            HiddenUpgradeObjects.Clear();
            scrapUpgradeUiHidden = false;
        }

        private static void SetScrapMiniGameHidden(bool hidden)
        {
            if (!hidden) {
                RestoreScrapMiniGameUi();
                return;
            }
            if (scrapMiniGameHidden || activeScrapProduction == null ||
                activeScrapProduction.transform == null)
                return;

            HiddenMiniGameObjects.Clear();
            Transform root = activeScrapProduction.transform;
            RememberAndHideMiniGameObject(root.Find("InstructionText"));
            RememberAndHideMiniGameObject(
                activeScrapProduction.barsRectTransform);
            RememberAndHideMiniGameObject(activeScrapProduction.currentBar);
            RememberAndHideMiniGameObject(root.Find("BoxUp"));
            RememberAndHideMiniGameObject(root.Find("BoxDown"));
            if (activeScrapProduction.startButton != null)
                RememberAndHideMiniGameObject(
                    activeScrapProduction.startButton.transform);
            RememberAndHideMiniGameObject(root.Find("OptionalText"));
            if (activeScrapProduction.infoBoxBG != null)
                RememberAndHideMiniGameObject(
                    activeScrapProduction.infoBoxBG.transform);
            if (activeScrapProduction.infoBox != null)
                RememberAndHideMiniGameObject(
                    activeScrapProduction.infoBox.transform);
            RememberAndHideMiniGameObject(root.Find("ScrapsText"));
            RememberAndHideMiniGameObject(root.Find("Normal_TAV_Line"));
            RememberAndHideMiniGameObject(root.Find("Bonus_TAV_Line"));
            RememberAndHideMiniGameObject(root.Find("Bonus2_TAV_Line"));

            scrapMiniGameHidden = true;
        }

        private static void RememberAndHideMiniGameObject(
            Transform transform)
        {
            if (transform == null || transform.gameObject == null)
                return;

            GameObject target = transform.gameObject;
            foreach (ScrapMiniGameObjectState state in
                HiddenMiniGameObjects) {
                if (state != null && state.Target == target)
                    return;
            }

            ScrapMiniGameObjectState newState =
                new ScrapMiniGameObjectState();
            newState.Target = target;
            newState.WasActive = target.activeSelf;
            HiddenMiniGameObjects.Add(newState);
            if (target.activeSelf)
                target.SetActive(false);
        }

        private static void RestoreScrapMiniGameUi()
        {
            if (!scrapMiniGameHidden)
                return;

            foreach (ScrapMiniGameObjectState state in
                HiddenMiniGameObjects) {
                if (state == null || state.Target == null)
                    continue;
                if (state.Target.activeSelf != state.WasActive)
                    state.Target.SetActive(state.WasActive);
            }

            HiddenMiniGameObjects.Clear();
            scrapMiniGameHidden = false;
        }

        private static void ApplyCurrentFilters(bool resetPage)
        {
            if (activeWindow == null || applyingFilteredList ||
                !IsScrapWindowActive())
                return;

            if (!AreScrapFiltersEnabled()) {
                SyncCurrentResultToOriginal();
                UpdateNativeBulkDescription();
                return;
            }

            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                CreateFilteredNativeList();

            try {
                applyingFilteredList = true;
                activeWindow.Refresh(filtered);
                ApplyFilteredEmptyState(activeWindow,
                    filtered == null || filtered.Count == 0,
                    filtered != null ? filtered.Count : 0);

                if (resetPage) {
                    while (activeWindow.currentPage > 0)
                        activeWindow.PreviousPage(true);
                }
            } catch (Exception exception) {
                ModLogger.Log("[ScrapInventoryFilter] Failed to refresh the filtered " +
                    "scrap list." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            } finally {
                applyingFilteredList = false;
            }

            UpdateNativeBulkDescription();
        }

        private static bool BeginFilteredBulkScrap()
        {
            PendingBulkItemUids.Clear();
            foreach (ChoosePartDownItem entry in FilteredItems) {
                if (entry == null || entry.IsLocked || entry.BaseItem == null)
                    continue;

                Item item = entry.BaseItem.TryCast<Item>();
                if (item != null)
                    PendingBulkItemUids.Add(item.UID);
            }

            if (PendingBulkItemUids.Count == 0) {
                ModLogger.Log("[ScrapInventoryFilter] Space hold ignored because " +
                    "the current list contains no unlocked loose parts.",
                    Types.LoggingLevels.Normal);
                return false;
            }

            UIManager uiManager = UIManager.Get();
            if (uiManager == null) {
                CancelPendingFilteredBulkScrap();
                ModLogger.Log("[ScrapInventoryFilter] Bulk scrap is " +
                    "unavailable because UIManager was not found.",
                    Types.LoggingLevels.Warning);
                return false;
            }

            string title = ModLocalization.Get("LOC_ScrapPartsTitle");
            string description = GetBulkConfirmationText(
                PendingBulkItemUids.Count);

            try {
                bulkConfirmationOpen = true;
                suppressNativeScrapInputUntilFrame = Time.frameCount +
                    BulkInputSuppressionFrames;
                uiManager.ShowAskWindow(title, description,
                    new Action<bool>(OnBulkConfirmationResult), true);

                ModLogger.Log("[ScrapInventoryFilter] Opened confirmation for " +
                    PendingBulkItemUids.Count + " selected loose part(s).",
                    Types.LoggingLevels.Normal);
                return true;
            } catch (Exception exception) {
                CancelPendingFilteredBulkScrap();
                ModLogger.Log("[ScrapInventoryFilter] Failed to open the " +
                    "bulk-scrap confirmation." + Environment.NewLine +
                    exception, Types.LoggingLevels.Error);
                return false;
            }
        }

        private static void OnBulkConfirmationResult(bool accepted)
        {
            bulkConfirmationOpen = false;
            suppressNativeScrapInputUntilFrame = Time.frameCount +
                BulkInputSuppressionFrames;

            if (!accepted) {
                PendingBulkItemUids.Clear();
                ModLogger.Log("[ScrapInventoryFilter] Bulk scrap confirmation " +
                    "was cancelled.", Types.LoggingLevels.Normal);
                return;
            }

            if (bulkExecutionScheduled)
                return;

            bulkExecutionScheduled = true;
            MelonCoroutines.Start(ExecuteFilteredBulkScrap());
        }

        private static IEnumerator ExecuteFilteredBulkScrap()
        {
            int askWindowWaitFrames = 120;
            while (IsAskWindowActive() && askWindowWaitFrames-- > 0)
                yield return null;

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            suppressNativeScrapInputUntilFrame = Time.frameCount +
                BulkInputSuppressionFrames;

            Inventory inventory = Singleton<Inventory>.Instance;
            if (inventory == null || PendingBulkItemUids.Count == 0) {
                CancelPendingFilteredBulkScrap();
                ModLogger.Log("[ScrapInventoryFilter] Bulk scrap was " +
                    "cancelled because the player inventory or selected set was " +
                    "not available.", Types.LoggingLevels.Warning);
                yield break;
            }

            float condition = BulkScrapAllCondition;
            BulkScrapInventoryState state;
            pendingFilteredBulkScrap = true;
            if (!PrepareFilteredBulkScrap(inventory, ref condition, out state)) {
                bulkExecutionScheduled = false;
                yield break;
            }

            try {
                int beforeCount = inventory.items != null
                    ? inventory.items.Count
                    : 0;
                inventory.ScrapPerCondition(condition);
                int afterCount = inventory.items != null
                    ? inventory.items.Count
                    : 0;
                int removedCount = Math.Max(0, beforeCount - afterCount);

                if (removedCount == 0) {
                    ModLogger.Log("[ScrapInventoryFilter] Native bulk scrap " +
                        "removed no selected parts. Requested threshold=" +
                        condition + ", selected=" + beforeCount + ".",
                        Types.LoggingLevels.Warning);
                } else {
                    ModLogger.Log("[ScrapInventoryFilter] Bulk scrap " +
                        "removed " + removedCount + " part(s) through " +
                        "Inventory.ScrapPerCondition.",
                        Types.LoggingLevels.Normal);
                }
            } catch (Exception exception) {
                ModLogger.Log("[ScrapInventoryFilter] Bulk scrap failed." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
            } finally {
                RestoreInventoryAfterFilteredBulkScrap(state);
                bulkExecutionScheduled = false;
                suppressNativeScrapInputUntilFrame = Time.frameCount + 2;
            }
        }

        private static bool CanStartFilteredBulkScrap()
        {
            return IsBulkScrapShortcutEnabled() && activeWindow != null &&
                IsScrapWindowActive() && !bulkConfirmationOpen &&
                !bulkExecutionScheduled && !pendingFilteredBulkScrap &&
                !IsScrapSearchFocused() &&
                GetFilteredBulkItemCount() > 0;
        }

        private static void OnNativeBulkAction()
        {
            shortcutConsumedFrame = Time.frameCount;

            if (CanStartFilteredBulkScrap() && BeginFilteredBulkScrap())
                Input.ResetInputAxes();
        }

        private static void CancelNativeBulkInteraction()
        {
            ResetNativeBulkDescriptionInput();
        }

        private static void ResetNativeBulkDescriptionInput()
        {
            if (nativeBulkDescription == null)
                return;

            nativeBulkDescription.holdTime = 0f;
            nativeBulkDescription.eventInvoked = false;
            nativeBulkDescription.eventInvoking = false;
            nativeBulkDescription.mouseDown = false;
            if (nativeBulkDescription.buttonFill != null)
                nativeBulkDescription.buttonFill.fillAmount = 0f;
        }

        private static string GetBulkConfirmationText(int count)
        {
            return ModLocalization.FormatCount(count,
                "LOC_ScrapPartConfirmationOne", "LOC_ScrapPartConfirmationFew",
                "LOC_ScrapPartConfirmationMany");
        }

        private static void ResolveScrapProduction()
        {
            if (activeScrapProduction != null)
                return;

            if (activeWindow != null && activeWindow.transform != null)
                activeScrapProduction = activeWindow.transform.root
                    .GetComponentInChildren<ScrapProduction>(true);
            if (activeScrapProduction == null)
                activeScrapProduction =
                    UnityEngine.Object.FindObjectOfType<ScrapProduction>();
        }

        private static void ResolveScrapWindow()
        {
            if (activeScrapWindow != null)
                return;
            if (activeWindow != null && activeWindow.transform != null)
                activeScrapWindow = activeWindow.transform.root
                    .GetComponentInChildren<ScrapWindow>(true);
            if (activeScrapWindow == null)
                activeScrapWindow =
                    UnityEngine.Object.FindObjectOfType<ScrapWindow>();
        }

        private static bool IsScrapProductionMode()
        {
            ResolveScrapProduction();
            return activeScrapProduction != null &&
                activeScrapProduction.gameObject != null &&
                activeScrapProduction.gameObject.activeInHierarchy;
        }

        private static bool IsScrapUpgradeMode()
        {
            ResolveScrapWindow();
            return activeScrapWindow != null &&
                activeScrapWindow.scrapUpgrade != null &&
                activeScrapWindow.scrapUpgrade.gameObject != null &&
                activeScrapWindow.scrapUpgrade.gameObject.activeInHierarchy;
        }

        private static void UpdateNativeBulkDescriptionForCurrentMode()
        {
            if (!IsBulkScrapShortcutEnabled()) {
                CancelNativeBulkInitialization();
                DestroyNativeBulkDescription();
                return;
            }

            if (IsScrapProductionMode()) {
                CancelNativeBulkInitialization();
                UpdateNativeBulkDescription();
                return;
            }

            DestroyNativeBulkDescription();
            if (activeWindow != null && IsScrapWindowActive() &&
                !IsScrapUpgradeMode())
                ScheduleNativeBulkInitialization();
            else
                CancelNativeBulkInitialization();
        }

        private static void ScheduleNativeBulkInitialization()
        {
            if (nativeBulkInitializationPending)
                return;
            Canvas.add_willRenderCanvases(
                NativeBulkInitializationHandler);
            nativeBulkInitializationPending = true;
        }

        private static void CancelNativeBulkInitialization()
        {
            if (!nativeBulkInitializationPending)
                return;
            Canvas.remove_willRenderCanvases(
                NativeBulkInitializationHandler);
            nativeBulkInitializationPending = false;
        }

        private static void OnNativeBulkInitializationRender()
        {
            if (!nativeBulkInitializationPending)
                return;
            if (activeWindow == null || !IsScrapWindowActive() ||
                !IsBulkScrapShortcutEnabled()) {
                CancelNativeBulkInitialization();
                return;
            }
            if (IsScrapProductionMode()) {
                CancelNativeBulkInitialization();
                UpdateNativeBulkDescription();
                return;
            }
            if (IsScrapUpgradeMode())
                CancelNativeBulkInitialization();
        }

        private static WindowFooterHintController.NativeFooterProfile
            GetScrapFooterProfile(bool isEmpty)
        {
            if (IsScrapUpgradeMode())
                return isEmpty
                    ? WindowFooterHintController.NativeFooterProfile
                        .ScrapUpgradeEmpty
                    : WindowFooterHintController.NativeFooterProfile
                        .ScrapUpgradePopulated;
            return isEmpty
                ? WindowFooterHintController.NativeFooterProfile
                    .ScrapProductionEmpty
                : WindowFooterHintController.NativeFooterProfile
                    .ScrapProductionPopulated;
        }

        private static void CreateNativeBulkDescription()
        {
            if (!IsBulkScrapShortcutEnabled() ||
                nativeBulkDescription != null)
                return;

            ResolveScrapProduction();
            UIDescription owner = activeScrapProduction != null
                ? activeScrapProduction.uiDescription : null;
            ControlDescription source = FindNativeSpaceDescription(owner);
            if (owner == null || source == null ||
                source.gameObject == null)
                return;

            ControlDescription alternativeVariantSource =
                FindAlternativeActionDescription(owner);
            NativeUiFactory.FooterHintHandle created =
                WindowFooterHintController.RequestNativeHint(
                    new WindowFooterHintController.NativeHintRequest {
                        WindowId = "Scrap",
                        WindowRoot = activeWindow.transform,
                        HintRoot = owner.transform,
                        HintId = NativeBulkDescriptionObjectName,
                        Source = source,
                        VariantSource = alternativeVariantSource,
                        Text = GetBulkShortcutBaseLabel(),
                        Action = new Action(OnNativeBulkAction),
                        InputHandlingMethod =
                            DescriptionInputHandlingMethod.ButtonDown,
                        CanHold = true,
                        TimeToHold = NativeBulkHoldDurationSeconds,
                        OnlyHandleMouseClickInput = false,
                        Row = 0,
                        Order = 0,
                        Profile = GetScrapFooterProfile(false),
                        ItemCount = FilteredItems.Count,
                    });
            if (created == null || created.Description == null ||
                created.Root == null)
                return;

            ControlDescription description = created.Description;
            GameObject clone = created.Root;

            nativeBulkDescriptionOwner = owner;
            nativeSingleDescription = source;
            nativeSingleInputHandlingMethod = source.inputHandlingMethod;
            nativeSingleInputHandlingMethodCaptured = true;
            source.inputHandlingMethod =
                DescriptionInputHandlingMethod.ButtonTimedPressUp;
            nativeBulkHint = created;
            nativeBulkDescription = description;
            nativeBulkDescriptionObject = clone;
            nativeBulkBaseLabel = GetBulkShortcutBaseLabel();
            ApplyNativeBulkDescriptionText();
            ResetNativeBulkDescriptionInput();

            RebuildNativeDescriptionLayout();
        }

        private static ControlDescription FindNativeSpaceDescription(
            UIDescription owner)
        {
            if (owner == null || owner.descriptions == null)
                return null;

            ControlDescription fallback = null;
            for (int i = 0; i < owner.descriptions.Length; i++) {
                ControlDescription candidate = owner.descriptions[i];
                if (candidate == null || candidate.buttonImage == null ||
                    candidate.buttonImage.sprite == null)
                    continue;

                string spriteName = candidate.buttonImage.sprite.name ??
                    string.Empty;
                if (spriteName.IndexOf("space", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (string.Equals(candidate.actionName,
                    "UISubmitMiniGame", StringComparison.Ordinal))
                    return candidate;
                if (fallback == null)
                    fallback = candidate;
            }

            return fallback;
        }

        private static ControlDescription FindAlternativeActionDescription(
            UIDescription owner)
        {
            if (owner == null || owner.descriptions == null)
                return null;

            for (int i = 0; i < owner.descriptions.Length; i++) {
                ControlDescription candidate = owner.descriptions[i];
                if (candidate == null)
                    continue;

                string variant = candidate.descriptionVariant.ToString();
                if (string.Equals(variant, "AlternativeAction",
                    StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private static string GetBulkShortcutBaseLabel()
        {
            return ModLocalization.Get("LOC_ScrapAllAction");
        }

        private static void ApplyNativeBulkDescriptionText()
        {
            if (nativeBulkDescription == null ||
                string.IsNullOrEmpty(nativeBulkBaseLabel))
                return;

            nativeBulkDescription.SetText(nativeBulkBaseLabel);

            var texts = nativeBulkDescription.texts;
            if (texts == null || texts.Length == 0 || texts[0] == null)
                return;

            Text label = texts[0];
            Color holdColor = nativeBulkDescription.buttonFill != null
                ? nativeBulkDescription.buttonFill.color
                : nativeBulkDescription.hoverColor;
            string holdColorHex = ColorUtility.ToHtmlStringRGB(holdColor);
            string holdLabel = ModLocalization.Get("LOC_HoldAction");

            label.supportRichText = true;
            label.text = nativeBulkBaseLabel +
                " <color=#" + holdColorHex + ">[" + holdLabel +
                "]</color>";
            nativeBulkDescription.RefreshLayout();
        }

        private static void UpdateNativeBulkDescription()
        {
            if (!IsScrapProductionMode()) {
                DestroyNativeBulkDescription();
                return;
            }
            int availableItemCount = GetFilteredBulkItemCount();
            if (availableItemCount == 0) {
                if (nativeBulkDescription != null)
                    DestroyNativeBulkDescription();
                if (AreScrapFiltersEnabled() && !scrapResultPending)
                    WindowFooterHintController.ResumeWindow("Scrap");
                return;
            }
            if (!scrapResultPending && activeScrapProduction != null &&
                !activeScrapProduction.isGameInProgress &&
                !activeScrapProduction.isAnimationInProgress)
                WindowFooterHintController.ResumeWindow("Scrap");
            if (nativeBulkDescription == null) {
                CreateNativeBulkDescription();
                return;
            }
            if (nativeBulkDescription == null ||
                nativeBulkDescriptionObject == null)
                return;

            bool visible = IsScrapWindowActive() && !IsAskWindowActive() &&
                activeWindow != null &&
                activeWindow.gameObject.activeInHierarchy &&
                activeScrapProduction != null &&
                !activeScrapProduction.isGameInProgress &&
                !activeScrapProduction.isAnimationInProgress;
            SetNativeBulkDescriptionActive(visible);
            if (!visible) {
                CancelNativeBulkInteraction();
                return;
            }

            string label = GetBulkShortcutBaseLabel();
            if (!string.Equals(nativeBulkBaseLabel, label,
                StringComparison.Ordinal)) {
                nativeBulkBaseLabel = label;
                ApplyNativeBulkDescriptionText();
            }

            bool enabled = CanStartFilteredBulkScrap();
            nativeBulkDescription.blockInput = !enabled;
            nativeBulkDescription.blockKeyboardInput =
                !enabled || IsScrapSearchFocused();
            nativeBulkDescription.blockMouseInput = !enabled;
            if (!enabled) {
                CancelNativeBulkInteraction();
                return;
            }

        }

        private static void SetNativeBulkDescriptionActive(bool active)
        {
            if (nativeBulkDescriptionObject == null ||
                nativeBulkDescriptionObject.activeSelf == active)
                return;

            NativeUiFactory.SetFooterHintActive(nativeBulkHint, active);
            if (active && nativeBulkDescription != null)
                ApplyNativeBulkDescriptionText();
            RebuildNativeDescriptionLayout();
        }

        private static void RebuildNativeDescriptionLayout()
        {
            if (nativeBulkDescriptionOwner == null)
                return;

            RectTransform rect = nativeBulkDescriptionOwner
                .GetComponent<RectTransform>();
            if (rect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private static int GetFilteredBulkItemCount()
        {
            HashSet<long> uniqueItems = new HashSet<long>();
            List<ChoosePartDownItem> source = AreScrapFiltersEnabled()
                ? FilteredItems : OriginalItems;
            foreach (ChoosePartDownItem entry in source) {
                if (entry == null || entry.IsLocked || entry.BaseItem == null)
                    continue;

                Item item = entry.BaseItem.TryCast<Item>();
                if (item != null)
                    uniqueItems.Add(item.UID);
            }

            return uniqueItems.Count;
        }

        private static IEnumerator RefreshAfterScrapAction(
            ScrapProduction scrapProduction)
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();

            while (scrapProduction != null &&
                scrapProduction.isAnimationInProgress)
                yield return new WaitForEndOfFrame();

            yield return new WaitForEndOfFrame();
            scrapResultPending = false;
            if (activeWindow != null && IsScrapWindowActive()) {
                ApplyCurrentFilters(false);
            }
        }

        private static IEnumerator RefreshAfterFilteredBulkScrap()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            ScrapWindow scrapWindow =
                UnityEngine.Object.FindObjectOfType<ScrapWindow>();
            if (scrapWindow != null)
                scrapWindow.UpdateItemsForProduction();

            yield return new WaitForEndOfFrame();
            if (activeWindow != null && IsScrapWindowActive())
                ApplyCurrentFilters(false);
        }

        private static void DestroyNativeBulkDescription()
        {
            CancelNativeBulkInteraction();
            if (nativeSingleDescription != null &&
                nativeSingleInputHandlingMethodCaptured) {
                nativeSingleDescription.inputHandlingMethod =
                    nativeSingleInputHandlingMethod;
            }
            WindowFooterHintController.RemoveHint("Scrap",
                NativeBulkDescriptionObjectName);

            nativeBulkDescriptionOwner = null;
            nativeSingleDescription = null;
            nativeSingleInputHandlingMethodCaptured = false;
            nativeBulkHint = null;
            nativeBulkDescription = null;
            nativeBulkDescriptionObject = null;
            nativeBulkBaseLabel = null;
        }

        private static void DeactivateWindow()
        {
            CancelNativeBulkInitialization();
            RestoreScrapMiniGameUi();
            RestoreScrapUpgradeUi();
            Panel.Detach();
            DestroyResetHint();
            DestroyNativeBulkDescription();
            activeWindow = null;
            activeScrapWindow = null;
            activeScrapProduction = null;
            preFilteredUpgradeList = null;
            upgradeTabTransitionPending = false;
            OriginalItems.Clear();
            FilteredItems.Clear();
            applyingFilteredList = false;
            suppressNativeScrapInputUntilFrame = -1;
            scrapResultPending = false;
            CancelNativeBulkInteraction();
            CancelPendingFilteredBulkScrap();
        }
    }
}
