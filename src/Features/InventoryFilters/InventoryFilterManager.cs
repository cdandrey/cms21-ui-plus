using System;

#if NET6_0_OR_GREATER
using Il2CppCMS;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
using Il2CppCMS.UI.Logic.Warehouse;
#else
using CMS;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
using CMS.UI.Logic.Warehouse;
#endif

namespace Cms21UiPlus
{
    public enum JunkyardConditionFilterMode
    {
        Off = 0,
        RepairThresholdToPerfect = 1,
        Orange = 2,
        Yellow = 3,
        Green = 4,
        Red = 5,
        Perfect = 6,
    }

    public enum GarageConditionFilterMode
    {
        Off = 0,
        Used = 1,
        Perfect = 2,
        Red = 3,
        Orange = 4,
        GreenRing = 5,
        Yellow = 6,
        RepairThresholdToPerfect = 7,
    }

    public enum RepairabilityQuickFilterMode
    {
        Off = 0,
        RepairGroupOnly = 1,
        NonRepairableOnly = 2,
    }

    public enum RepairSpecializationQuickFilterMode
    {
        Off = 0,
        RequiredOnly = 1,
        ExcludedOnly = 2,
    }

    internal sealed class RepairSpecializationFilterState
    {
        internal int RequiredMask;
        internal int ExcludedMask;

        internal bool HasAny {
            get { return RequiredMask != 0 || ExcludedMask != 0; }
        }

        internal RepairSpecializationQuickFilterMode GetMode(int bit)
        {
            if ((RequiredMask & bit) != 0)
                return RepairSpecializationQuickFilterMode.RequiredOnly;
            if ((ExcludedMask & bit) != 0)
                return RepairSpecializationQuickFilterMode.ExcludedOnly;
            return RepairSpecializationQuickFilterMode.Off;
        }

        internal void SetMode(int bit, RepairSpecializationQuickFilterMode mode)
        {
            RequiredMask &= ~bit;
            ExcludedMask &= ~bit;
            if (mode == RepairSpecializationQuickFilterMode.RequiredOnly)
                RequiredMask |= bit;
            else if (mode == RepairSpecializationQuickFilterMode.ExcludedOnly)
                ExcludedMask |= bit;
        }

        internal void Reset()
        {
            RequiredMask = 0;
            ExcludedMask = 0;
        }
    }

    public enum RestorationAvailabilityQuickFilterMode
    {
        Off = 0,
        AvailableOnly = 1,
        UnavailableOnly = 2,
    }

    public enum QualityQuickFilterMode
    {
        Off = 0,
        Improved = 1,
        Quality1 = 2,
        Quality2 = 3,
        Quality3 = 4,
        NonImproved = 5,
    }

    public enum OwnedQuickFilterMode
    {
        Off = 0,
        Owned = 1,
        Missing = 2,
    }

    public enum PackageQuickFilterMode
    {
        Off = 0,
        Packages = 1,
        Singles = 2,
    }

    public static partial class InventoryFilterManager
    {
        private static JunkyardConditionFilterMode junkyardConditionFilterMode =
            JunkyardConditionFilterMode.Off;
        private static GarageConditionFilterMode garageConditionFilterMode =
            GarageConditionFilterMode.Off;
        private static RepairabilityQuickFilterMode junkyardRepairabilityFilterMode =
            RepairabilityQuickFilterMode.Off;
        private static RepairabilityQuickFilterMode garageRepairabilityFilterMode =
            RepairabilityQuickFilterMode.Off;
        private static readonly RepairSpecializationFilterState
            junkyardRepairSpecializationFilter =
                new RepairSpecializationFilterState();
        private static readonly RepairSpecializationFilterState
            garageRepairSpecializationFilter =
                new RepairSpecializationFilterState();
        private static QualityQuickFilterMode junkyardQualityFilterMode =
            QualityQuickFilterMode.Off;
        private static QualityQuickFilterMode garageQualityFilterMode =
            QualityQuickFilterMode.Off;
        private static OwnedQuickFilterMode ownedFilterMode = OwnedQuickFilterMode.Off;
        private static PackageQuickFilterMode packageFilterMode =
            PackageQuickFilterMode.Off;

        public static void ResetAll()
        {
            CloseQuickFilterMenu();
            ClearResetHint();
            ClearInventoryGroupingHint();
            activeFilteredInventory = null;
            ResetTravelCollectedSearch();
            PartRowIndicators.ResetRowBaseItems();
            GroupingStates.Clear();
            PackageRows.Clear();
            GroupingListTriggers.Clear();
            suppressedBetterButtonActionId = 0;
            currentPackageSaleCandidate = null;
            pendingPackageSale = null;
            pendingPackageSaleMoney = 0;
            ClearPackageSalePopup();
            packageSaleInProgress = false;
            ItemGroupingKeys.Clear();
            GroupGroupingKeys.Clear();
            foreach (DrawSnapshot snapshot in DrawSnapshots.Values) {
                try {
                    snapshot.Restore();
                } catch {
                }
            }

            DrawSnapshots.Clear();
            ItemsBindings.Clear();
            MissingItemsBindings.Clear();
            junkyardConditionFilterMode = JunkyardConditionFilterMode.Off;
            garageConditionFilterMode = GarageConditionFilterMode.Off;
            junkyardRepairabilityFilterMode = RepairabilityQuickFilterMode.Off;
            garageRepairabilityFilterMode = RepairabilityQuickFilterMode.Off;
            junkyardRepairSpecializationFilter.Reset();
            garageRepairSpecializationFilter.Reset();
            junkyardQualityFilterMode = QualityQuickFilterMode.Off;
            garageQualityFilterMode = QualityQuickFilterMode.Off;
            ownedFilterMode = OwnedQuickFilterMode.Off;
            packageFilterMode = PackageQuickFilterMode.Off;
        }

        internal static void ResetGarageFiltersOnWindowClose()
        {
            CloseQuickFilterMenu();
            ClearResetHint();
            activeFilteredInventory = null;
            if (!GlobalState.IsGarageSceneActive || IsBarnOrJunkyardScene())
                return;

            if (Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.resetInventoryQuickFiltersOnExit) {
                garageConditionFilterMode = GarageConditionFilterMode.Off;
                garageRepairabilityFilterMode = RepairabilityQuickFilterMode.Off;
                garageRepairSpecializationFilter.Reset();
                garageQualityFilterMode = QualityQuickFilterMode.Off;
                packageFilterMode = PackageQuickFilterMode.Off;
            }
            ClearSelectedButton();
        }

        private static bool ShouldHandleWindow(BaseInventory inventory)
        {
            if (inventory == null)
                return false;

            bool filtersEnabled = IsFeatureEnabled();
            bool groupingEnabled = IsInventoryGroupingEnabled() &&
                SupportsInventoryGrouping(inventory);
            if (!filtersEnabled && !groupingEnabled)
                return false;

            if (IsBarnOrJunkyardScene())
                return filtersEnabled;

            if (inventory.TryCast<InventoryWindow>() != null)
                return true;
            if (inventory.TryCast<WarehouseInventoryTab>() != null)
                return true;
            if (inventory.TryCast<WarehouseTab>() != null)
                return true;

            // Some game builds expose the two warehouse pages only as BaseInventory.
            return inventory.GetComponentInParent<WarehouseWindow>() != null;
        }

        private static bool SupportsOwnedFilter(BaseInventory inventory)
        {
            return inventory != null && IsBarnOrJunkyardScene();
        }

        private static bool IsFeatureEnabled()
        {
            return Main.SettingsEntry != null && Main.SettingsEntry.Value.addQuickFilters;
        }

        internal static bool IsBarnOrJunkyardScene()
        {
            GameScript gameScript = GameScript.Get();
            return gameScript != null &&
                (gameScript.CurrentSceneType == SceneType.Junkyard ||
                 gameScript.CurrentSceneType == SceneType.Barn);
        }
    }
}
