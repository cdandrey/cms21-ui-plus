using System;
using HarmonyLib;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class InventoryFilterPatches
    {
        [HarmonyPatch(typeof(InventoryWindow), nameof(InventoryWindow.Show))]
        [HarmonyPrefix]
        private static void InventoryWindowShowGroupingPrefix(InventoryWindow __instance)
        {
            InventoryFilterManager.ResetInventoryGrouping(__instance);
        }

        [HarmonyPatch(typeof(InventoryWindow), nameof(InventoryWindow.Show))]
        [HarmonyPostfix]
        private static void InventoryWindowShowPostfix(InventoryWindow __instance)
        {
            InventoryFilterManager.EnsureButtons(__instance);
        }

        [HarmonyPatch(typeof(InventoryWindow), nameof(InventoryWindow.Hide))]
        [HarmonyPostfix]
        private static void InventoryWindowHidePostfix(InventoryWindow __instance)
        {
            InventoryFilterManager.ResetInventoryGrouping(__instance);
            InventoryFilterManager.ResetGarageFiltersOnWindowClose();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Show))]
        [HarmonyPrefix]
        private static void WarehouseWindowShowGroupingPrefix(WarehouseWindow __instance)
        {
            InventoryFilterManager.ResetWarehouseGrouping(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Show))]
        [HarmonyPostfix]
        private static void WarehouseWindowShowPostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Hide))]
        [HarmonyPostfix]
        private static void WarehouseWindowHidePostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.ResetWarehouseGrouping(__instance);
            InventoryFilterManager.ResetGarageFiltersOnWindowClose();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchTab))]
        [HarmonyPrefix]
        private static void WarehouseWindowSwitchTabGroupingPrefix(WarehouseWindow __instance)
        {
            InventoryFilterManager.ResetWarehouseGrouping(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchTab))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchTabPostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToInventory))]
        [HarmonyPrefix]
        private static void WarehouseWindowSwitchToInventoryGroupingPrefix(WarehouseWindow __instance)
        {
            InventoryFilterManager.ResetWarehouseGrouping(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToInventory))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchToInventoryPostfix(WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchWarehouseTabAction))]
        [HarmonyPrefix]
        private static void WarehouseWindowSwitchWarehouseTabActionGroupingPrefix(WarehouseWindow __instance)
        {
            InventoryFilterManager.ResetWarehouseGrouping(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchWarehouseTabAction))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchWarehouseTabActionPostfix(
            WarehouseWindow __instance)
        {
            InventoryFilterManager.EnsureWarehouseWindowButtons(__instance);
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToWarehouse))]
        [HarmonyPrefix]
        private static void WarehouseWindowSwitchToWarehouseGroupingPrefix(WarehouseWindow __instance)
        {
            InventoryFilterManager.ResetWarehouseGrouping(__instance);
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


        [HarmonyPatch(typeof(BetterButtonAction), "OnPointerClick",
            new Type[] { typeof(PointerEventData) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool BetterButtonActionOnPointerClickPrefix(
            BetterButtonAction __instance, PointerEventData __0)
        {
            if (__0 == null ||
                __0.button != PointerEventData.InputButton.Right)
                return true;
            if (InventoryFilterManager.ConsumeSuppressedBetterButtonRightClick(
                __instance.GetInstanceID()))
                return false;

            InventoryItem row = __instance.GetComponentInParent<InventoryItem>();
            return !InventoryFilterManager.TryHandleInventoryRowRightClick(
                row, __0);
        }

        [HarmonyPatch(typeof(BetterButtonAction), "RightClickInternal",
            new Type[] { typeof(bool) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool BetterButtonActionRightClickInternalPrefix(
            BetterButtonAction __instance)
        {
            InventoryItem row = __instance.GetComponentInParent<InventoryItem>();
            return !InventoryFilterManager.ShouldSuppressInventoryRowRightClick(row);
        }

        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerClick))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool EventTriggerOnPointerClickGroupingPrefix(
            EventTrigger __instance, PointerEventData __0)
        {
            return !InventoryFilterManager.TryHandleGroupingListRightClick(
                __instance, __0);
        }

        [HarmonyPatch(typeof(Button), nameof(Button.OnPointerClick))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool ButtonOnPointerClickPrefix(
            Button __instance, PointerEventData __0)
        {
            if (__0 == null ||
                __0.button != PointerEventData.InputButton.Right)
                return true;

            InventoryItem row = __instance.GetComponentInParent<InventoryItem>();
            if (InventoryFilterManager.TryHandleInventoryRowRightClick(row, __0))
                return false;

            return !InventoryFilterManager.TryHandleReverseQuickFilterClick(
                __instance);
        }


    }
}
