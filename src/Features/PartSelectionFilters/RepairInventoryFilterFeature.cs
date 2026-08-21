using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Logic.ChoosePartDown;
using Il2CppCMS.UI.Logic.Scrap;
#else
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
    /// Adapts the shared part-filter rules and controls to both repair-table
    /// inventories: mechanical parts and body panels.
    /// </summary>
    public static class RepairInventoryFilterFeature
    {
        private static readonly List<ChoosePartDownItem> OriginalItems =
            new List<ChoosePartDownItem>();
        private static readonly PartFilterPanelController Panel =
            new PartFilterPanelController("QRepairInventoryFilter");

        private static ChoosePartDownWindow activeWindow;
        private static RepairPartWindow activeRepairWindow;
        private static GarageConditionFilterMode conditionMode =
            GarageConditionFilterMode.Off;
        private static QualityQuickFilterMode qualityMode =
            QualityQuickFilterMode.Off;
        private static string searchText = string.Empty;
        private static bool applyingFilteredList;
        private static bool filteredEmptyState;
        private static int activationGeneration;
        private static GameObject emptyStateRoot;
        private static Text emptyStateText;
        private static NativeUiFactory.FooterHintHandle resetHint;
        private static bool repairResultPending;
        private static readonly List<RepairUiObjectState> HiddenRepairUi =
            new List<RepairUiObjectState>();

        private sealed class RepairUiObjectState
        {
            public GameObject Target;
            public bool WasActive;
        }

        public static void OnWindowShown(ChoosePartDownWindow window,
            object itemsToShowArgument)
        {
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> itemsToShow =
                itemsToShowArgument as
                    Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>;
            if (!IsEnabled()) {
                DeactivateWindow();
                return;
            }
            if (window == null)
                return;

            CaptureNativeItems(itemsToShow);
            int generation = ++activationGeneration;
            MelonCoroutines.Start(AttachAfterWindowActivation(window,
                generation));
        }

        public static void FilterNativeListBeforeRefresh(
            ChoosePartPageManager pageManager,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items)
        {
            if (!IsEnabled() || activeWindow == null ||
                applyingFilteredList || !IsRepairWindowActive() ||
                items == null)
                return;

            CaptureNativeItems(items);
            items = CreateFilteredNativeList();

            // ChoosePartPageManager.Refresh reads its existing items collection
            // before replacing it. Preloading the filtered collection prevents
            // the native refresh from selecting a row hidden by the active
            // repair filters.
            if (pageManager != null) {
                pageManager.items = items;
                if (items.Count == 0) {
                    pageManager.currentPage = 0;
                    pageManager.currentPageItemsCount = 0;
                }
            }

        }

        public static void OnNativeListRefreshed(object itemsArgument)
        {
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> items =
                itemsArgument as
                    Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>;
            if (!IsEnabled() || activeWindow == null ||
                applyingFilteredList || !IsRepairWindowActive())
                return;

        }

        public static void OnInputFieldKeyPressed(InputField inputField)
        {
            if (!IsEnabled() || activeWindow == null ||
                !IsRepairWindowActive())
                return;

            Panel.HandleKeyPressed(inputField);
        }

        public static void OnRepairWindowShown(RepairPartWindow repairWindow)
        {
            if (!IsEnabled())
                return;

            activeRepairWindow = repairWindow;
        }

        public static void OnRepairWindowHidden()
        {
            if (ShouldResetOnExit())
                ResetFilterState();
            DeactivateWindow();
        }

        public static void OnRepairProcessed(RepairPartWindow repairWindow)
        {
            if (!IsEnabled() || activeWindow == null ||
                !IsRepairWindowActive())
                return;

            repairResultPending = true;
            MelonCoroutines.Start(RefreshAfterRepairAction(repairWindow));
        }

        public static void OnRepairStarted()
        {
            if (!IsEnabled() || filteredEmptyState ||
                !IsRepairWindowActive())
                return;
            WindowFooterHintController.SuspendWindow("Repair");
        }

        public static void OnRepairCancelled()
        {
            if (!repairResultPending && IsEnabled() &&
                IsRepairWindowActive())
                WindowFooterHintController.ResumeWindow("Repair");
        }

        public static bool ShouldSuppressRepairAction()
        {
            return IsEnabled() && filteredEmptyState &&
                IsRepairWindowActive();
        }

        public static void ResetAll()
        {
            DeactivateWindow();
            OriginalItems.Clear();
            conditionMode = GarageConditionFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            searchText = string.Empty;
            applyingFilteredList = false;
        }

        private static bool ShouldResetOnExit()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.resetRepairInventoryFiltersOnExit;
        }

        private static bool IsEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.addRepairInventoryFilters;
        }

        private static bool IsRepairWindowActive()
        {
            return WindowManager.Instance != null &&
                WindowManager.Instance.IsWindowActive(WindowID.RepairPart);
        }

        private static IEnumerator AttachAfterWindowActivation(
            ChoosePartDownWindow window, int generation)
        {
            const int MaximumActivationFrames = 8;

            for (int frame = 0; frame < MaximumActivationFrames; frame++) {
                yield return new WaitForEndOfFrame();

                if (generation != activationGeneration || window == null)
                    yield break;
                if (!IsRepairWindowActive())
                    continue;

                activeWindow = window;
                if (!Panel.AttachWithButtons(window.transform,
                    CycleConditionFilter, null, CycleQualityFilter,
                    OnSearchChanged, true, false, true,
                    CycleConditionFilterReverse, null,
                    CycleQualityFilterReverse)) {
                    DeactivateWindow();
                    yield break;
                }

                Panel.SetSearchText(searchText);
                Panel.UpdateVisuals(conditionMode,
                    RepairabilityQuickFilterMode.Off, qualityMode);
                CreateResetHint();
                ApplyCurrentFilters(true);
                yield break;
            }

            if (generation == activationGeneration)
                DeactivateWindow();
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
            Panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
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

        internal static bool TryResetFromKeyboardShortcut()
        {
            if (activeRepairWindow == null ||
                activeRepairWindow.gameObject == null ||
                !activeRepairWindow.gameObject.activeInHierarchy)
                return false;

            ResetFilters();
            return true;
        }

        private static void ResetFilterState()
        {
            conditionMode = GarageConditionFilterMode.Off;
            qualityMode = QualityQuickFilterMode.Off;
            searchText = string.Empty;
        }

        private static void ResetFilters()
        {
            ResetFilterState();
            Panel.ResetSearch();
            Panel.UpdateVisuals(conditionMode,
                RepairabilityQuickFilterMode.Off, qualityMode);
            ApplyCurrentFilters(true);
        }

        private static void CreateResetHint()
        {
            if (resetHint != null && resetHint.Root != null)
                return;
            if (activeRepairWindow == null ||
                activeRepairWindow.uiDescription == null)
                return;

            resetHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = "Repair",
                    WindowRoot = activeRepairWindow.transform,
                    HintRoot = activeRepairWindow.uiDescription.transform,
                    HintId = "Hint_ResetRepairFilters",
                    Keys = new string[] { "LeftAlt" },
                    Text = ModLocalization.Get("LOC_ResetFiltersAction"),
                    Action = new Action(ResetFilters),
                    Row = 0,
                    Order = 0,
                    Profile = WindowFooterHintController
                        .NativeFooterProfile.RepairPopulated,
                    ItemCount = OriginalItems.Count,
                });
        }

        private static void OnSearchChanged(string value)
        {
            searchText = value ?? string.Empty;
            ApplyCurrentFilters(true);
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
            PartFilterCriteria criteria = CreateCriteria();
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
            if (activeWindow == null || applyingFilteredList ||
                !IsRepairWindowActive())
                return;

            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> filtered =
                CreateFilteredNativeList();


            try {
                applyingFilteredList = true;
                if (filtered.Count == 0) {
                    activeWindow.currentPage = 0;
                    activeWindow.currentPageItemsCount = 0;
                }
                activeWindow.Refresh(filtered);
                if (activeRepairWindow != null)
                    activeRepairWindow.CheckIfThereAreItems(filtered);
                ApplyFilteredEmptyState(filtered.Count == 0 &&
                    CreateCriteria().HasAnyFilter, filtered.Count);

                if (resetPage) {
                    while (activeWindow.currentPage > 0)
                        activeWindow.PreviousPage(true);
                }
            } catch (Exception exception) {
                ModLogger.Log("[RepairInventoryFilter] Failed to refresh the " +
                    "filtered repair list." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            } finally {
                applyingFilteredList = false;
            }
        }

        private static IEnumerator RefreshAfterRepairAction(
            RepairPartWindow repairWindow)
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();

            while (repairWindow != null && repairWindow.isAnimationInProgress)
                yield return new WaitForEndOfFrame();

            yield return new WaitForEndOfFrame();
            if (activeWindow != null && IsRepairWindowActive()) {
                ApplyCurrentFilters(false);
                WindowFooterHintController.ResumeWindow("Repair");
            }
            repairResultPending = false;
        }

        private static void ApplyFilteredEmptyState(bool isEmpty,
            int itemCount)
        {
            WindowFooterHintController.SetNativeProfile("Repair",
                isEmpty
                    ? WindowFooterHintController.NativeFooterProfile
                        .RepairEmpty
                    : WindowFooterHintController.NativeFooterProfile
                        .RepairPopulated, itemCount);
            if (!isEmpty) {
                RestoreRepairUi();
                return;
            }
            if (filteredEmptyState || activeRepairWindow == null)
                return;

            if (activeWindow != null)
                activeWindow.DeselectCurrentItem();

            HiddenRepairUi.Clear();
            RememberAndHide(activeRepairWindow.transform.Find("BoxUp"));
            RememberAndHide(activeRepairWindow.transform.Find("BoxDown"));
            RememberAndHide(activeRepairWindow.inventoryItemDetails);
            RememberAndHide(activeRepairWindow.current);
            RememberAndHide(activeRepairWindow.onSuccess);
            RememberAndHide(activeRepairWindow.onFail);
            RememberAndHide(activeRepairWindow.price);
            RememberAndHide(activeRepairWindow.instantRepairText);
            RememberAndHide(activeRepairWindow.instantRepairValue);
            RememberAndHide(activeRepairWindow.barsRectTransform);
            RememberAndHide(activeRepairWindow.currentBar);
            RememberAndHide(activeRepairWindow.startButton);
            RememberAndHide(activeRepairWindow.startButtonText);
            RememberAndHide(activeRepairWindow.locked);
            RememberAndHide(activeRepairWindow.stats);
            RememberAndHide(activeRepairWindow.lockedText);

            if (activeRepairWindow.bars != null) {
                foreach (var bar in activeRepairWindow.bars)
                    RememberAndHide(bar);
            }
            if (activeRepairWindow.infoBoxes != null) {
                foreach (var infoBox in activeRepairWindow.infoBoxes)
                    RememberAndHide(infoBox);
            }

            EnsureEmptyStateText();
            if (emptyStateRoot != null)
                emptyStateRoot.SetActive(true);
            filteredEmptyState = true;
        }

        private static void EnsureEmptyStateText()
        {
            if (emptyStateText != null || activeWindow == null)
                return;

            if (activeRepairWindow == null ||
                activeRepairWindow.transform == null)
                return;

            emptyStateRoot = NativeUiFactory.CreateNativeNoItemsPage(
                activeRepairWindow.transform);
            if (emptyStateRoot == null)
                return;

            emptyStateRoot.name = "QRepairInventoryEmptyState";
            emptyStateText =
                emptyStateRoot.GetComponentInChildren<Text>(true);

            RectTransform rect = emptyStateRoot.GetComponent<RectTransform>();
            NativeUiFactory.Stretch(rect, 0f, 0f, 0f, 0f);
            emptyStateRoot.transform.SetAsLastSibling();
        }

        private static void RememberAndHide(Component component)
        {
            if (component != null)
                RememberAndHide(component.gameObject);
        }

        private static void RememberAndHide(GameObject target)
        {
            if (target == null)
                return;
            foreach (RepairUiObjectState state in HiddenRepairUi) {
                if (state != null && state.Target == target)
                    return;
            }

            RepairUiObjectState stateToAdd = new RepairUiObjectState();
            stateToAdd.Target = target;
            stateToAdd.WasActive = target.activeSelf;
            HiddenRepairUi.Add(stateToAdd);
            if (target.activeSelf)
                target.SetActive(false);
        }

        private static void RestoreRepairUi()
        {
            if (!filteredEmptyState)
                return;
            foreach (RepairUiObjectState state in HiddenRepairUi) {
                if (state == null || state.Target == null)
                    continue;
                if (state.Target.activeSelf != state.WasActive)
                    state.Target.SetActive(state.WasActive);
            }

            HiddenRepairUi.Clear();
            if (emptyStateRoot != null)
                emptyStateRoot.SetActive(false);
            filteredEmptyState = false;
        }

        private static void DeactivateWindow()
        {
            RestoreRepairUi();
            WindowFooterHintController.RemoveHint("Repair",
                "Hint_ResetRepairFilters");
            resetHint = null;
            if (emptyStateRoot != null) {
                UnityEngine.Object.Destroy(emptyStateRoot);
                emptyStateRoot = null;
                emptyStateText = null;
            }
            activationGeneration++;
            Panel.Detach();
            activeWindow = null;
            activeRepairWindow = null;
            repairResultPending = false;
            OriginalItems.Clear();
            applyingFilteredList = false;
        }
    }
}
