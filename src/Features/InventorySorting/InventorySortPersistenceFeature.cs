using System;
using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
using Il2CppCMS.UI.Logic.Warehouse;
#else
using UnhollowerRuntimeLib;
using CMS;
using CMS.Containers;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
using CMS.UI.Logic.Warehouse;
#endif

namespace Cms21UiPlus
{
    /// <summary>Persists and restores inventory and warehouse sort selections.</summary>
    [HarmonyPatch]
    public static class InventorySortPersistenceFeature
    {
        private static bool IsEnabled {
            get {
                return GlobalState.IsGarageSceneActive && Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.rememberInventorySorting;
            }
        }

        [HarmonyPatch(typeof(BaseInventory), nameof(BaseInventory.SetSortType),
            new Type[] { typeof(SortType) })]
        [HarmonyPostfix]
        public static void BaseInventorySetSortTypePostfix(SortType newSortType,
            BaseInventory __instance)
        {
            if (!IsEnabled)
                return;

            if (__instance.TryCast<InventoryWindow>() != null) {
                if (CurrentProfile.inventorySortType != newSortType) {
                    CurrentProfile.inventorySortType = newSortType;
                    Main.MarkProfileMemoryDirty();
                }
                return;
            }
            if (__instance.TryCast<WarehouseInventoryTab>() != null) {
                if (CurrentProfile.warehouseInventorySortType != newSortType) {
                    CurrentProfile.warehouseInventorySortType = newSortType;
                    Main.MarkProfileMemoryDirty();
                }
                return;
            }
            if (__instance.TryCast<WarehouseTab>() != null) {
                if (CurrentProfile.warehouseSortType != newSortType) {
                    CurrentProfile.warehouseSortType = newSortType;
                    Main.MarkProfileMemoryDirty();
                }
                return;
            }

            ModLogger.Log("Unknown sortable inventory: " + __instance +
                ", sort=" + newSortType, Types.LoggingLevels.Debug);
        }

        [HarmonyPatch(typeof(InventoryWindow), nameof(InventoryWindow.Show))]
        [HarmonyPostfix]
        public static void InventoryWindowShowPostfix(InventoryWindow __instance)
        {
            if (!IsEnabled)
                return;

            if (__instance.GetSortType() != CurrentProfile.inventorySortType)
                __instance.SetSortType(CurrentProfile.inventorySortType);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Show))]
        [HarmonyPostfix]
        public static void WarehouseWindowShowPostfix(WarehouseWindow __instance)
        {
            RestoreWarehouseSortOrder(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchTab))]
        [HarmonyPostfix]
        public static void WarehouseWindowSwitchTabPostfix(WarehouseWindow __instance)
        {
            RestoreWarehouseSortOrder(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToInventory))]
        [HarmonyPostfix]
        public static void WarehouseWindowSwitchToInventoryPostfix(
            WarehouseWindow __instance)
        {
            RestoreWarehouseSortOrder(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow),
            nameof(WarehouseWindow.SwitchWarehouseTabAction))]
        [HarmonyPostfix]
        public static void WarehouseWindowSwitchWarehouseTabActionPostfix(
            WarehouseWindow __instance)
        {
            RestoreWarehouseSortOrder(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToWarehouse))]
        [HarmonyPostfix]
        public static void WarehouseWindowSwitchToWarehousePostfix(
            WarehouseWindow __instance)
        {
            RestoreWarehouseSortOrder(__instance);
        }

        private static void RestoreWarehouseSortOrder(WarehouseWindow window)
        {
            if (!IsEnabled || window == null)
                return;

            SortType requiredSort;
            if (window.currentTab == 0)
                requiredSort = CurrentProfile.warehouseInventorySortType;
            else if (window.currentTab == 1)
                requiredSort = CurrentProfile.warehouseSortType;
            else
                return;

            if (window.GetSortType() != requiredSort)
                window.SetSortType(requiredSort);
        }

        private static Types.ProfileState CurrentProfile {
            get {
                return Main.ProfileMemory.profileStates[
                    GlobalState.LoadedProfileId];
            }
        }
    }
}
