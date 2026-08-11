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

    public static partial class InventoryFilterManager
    {
        // Revision marker: tri-state repairability filter, 2026-08-01.6

        private static JunkyardConditionFilterMode junkyardConditionFilterMode =
            JunkyardConditionFilterMode.Off;
        private static GarageConditionFilterMode garageConditionFilterMode =
            GarageConditionFilterMode.Off;
        private static RepairabilityQuickFilterMode junkyardRepairabilityFilterMode =
            RepairabilityQuickFilterMode.Off;
        private static RepairabilityQuickFilterMode garageRepairabilityFilterMode =
            RepairabilityQuickFilterMode.Off;
        private static QualityQuickFilterMode junkyardQualityFilterMode =
            QualityQuickFilterMode.Off;
        private static QualityQuickFilterMode garageQualityFilterMode =
            QualityQuickFilterMode.Off;
        private static OwnedQuickFilterMode ownedFilterMode = OwnedQuickFilterMode.Off;

        public static void ResetAll()
        {
            ClearResetHint();
            activeFilteredInventory = null;
            foreach (DrawSnapshot snapshot in DrawSnapshots.Values) {
                try {
                    snapshot.Restore();
                } catch (Exception exception) {
                    ModLogger.Log("[InventoryFilter] Failed to restore a filter snapshot " +
                        "during reset." + Environment.NewLine + exception,
                        Types.LoggingLevels.Debug);
                }
            }

            DrawSnapshots.Clear();
            ItemsBindings.Clear();
            MissingItemsBindings.Clear();
            junkyardConditionFilterMode = JunkyardConditionFilterMode.Off;
            garageConditionFilterMode = GarageConditionFilterMode.Off;
            junkyardRepairabilityFilterMode = RepairabilityQuickFilterMode.Off;
            garageRepairabilityFilterMode = RepairabilityQuickFilterMode.Off;
            junkyardQualityFilterMode = QualityQuickFilterMode.Off;
            garageQualityFilterMode = QualityQuickFilterMode.Off;
            ownedFilterMode = OwnedQuickFilterMode.Off;
        }

        internal static void ResetGarageFiltersOnWindowClose()
        {
            if (!GlobalState.IsGarageSceneActive || IsBarnOrJunkyardScene())
                return;

            ClearResetHint();
            activeFilteredInventory = null;
            garageConditionFilterMode = GarageConditionFilterMode.Off;
            garageRepairabilityFilterMode = RepairabilityQuickFilterMode.Off;
            garageQualityFilterMode = QualityQuickFilterMode.Off;
            ClearSelectedButton();
        }

        private static bool ShouldHandleWindow(BaseInventory inventory)
        {
            if (inventory == null || !IsFeatureEnabled())
                return false;

            if (IsBarnOrJunkyardScene())
                return true;

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
            return Main.SettingsEntry != null && Main.SettingsEntry.Value.addInventoryQuickFilters;
        }

        private static bool IsBarnOrJunkyardScene()
        {
            GameScript gameScript = GameScript.Get();
            return gameScript != null &&
                (gameScript.CurrentSceneType == SceneType.Junkyard ||
                 gameScript.CurrentSceneType == SceneType.Barn);
        }
    }
}
