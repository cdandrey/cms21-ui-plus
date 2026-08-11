using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class InventoryFilterPatches
    {
        [HarmonyPatch(typeof(InventoryWindow), nameof(InventoryWindow.Show))]
        [HarmonyPostfix]
        private static void InventoryWindowShowPostfix(InventoryWindow __instance)
        {
            InventoryFilterManager.EnsureButtons(__instance);
        }

        [HarmonyPatch(typeof(InventoryWindow), nameof(InventoryWindow.Hide))]
        [HarmonyPostfix]
        private static void InventoryWindowHidePostfix()
        {
            InventoryFilterManager.ResetGarageFiltersOnWindowClose();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Show))]
        [HarmonyPostfix]
        private static void WarehouseWindowShowPostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Hide))]
        [HarmonyPostfix]
        private static void WarehouseWindowHidePostfix()
        {
            InventoryFilterManager.ResetGarageFiltersOnWindowClose();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchTab))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchTabPostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToInventory))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchToInventoryPostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchWarehouseTabAction))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchWarehouseTabActionPostfix(
            WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToWarehouse))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchToWarehousePostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(BaseInventory), nameof(BaseInventory.DrawPage))]
        [HarmonyPrefix]
        private static void BaseInventoryDrawPagePrefix(BaseInventory __instance)
        {
            InventoryFilterManager.PrepareForDraw(__instance);
        }

        [HarmonyPatch(typeof(BaseInventory), nameof(BaseInventory.DrawPage))]
        [HarmonyPostfix]
        private static void BaseInventoryDrawPagePostfix(BaseInventory __instance)
        {
            InventoryFilterManager.FinishDraw(__instance);
        }

        [HarmonyPatch(typeof(BaseInventory), nameof(BaseInventory.RedrawCurrentPage))]
        [HarmonyPrefix]
        private static void BaseInventoryRedrawCurrentPagePrefix(BaseInventory __instance)
        {
            InventoryFilterManager.PrepareForRedraw(__instance);
        }

        [HarmonyPatch(typeof(BaseInventory), nameof(BaseInventory.RedrawCurrentPage))]
        [HarmonyPostfix]
        private static void BaseInventoryRedrawCurrentPagePostfix(BaseInventory __instance)
        {
            InventoryFilterManager.FinishRedraw(__instance);
            InventoryFilterManager.EnsureButtons(__instance);
        }


    }
}
