using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Controls;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.Shop;
using Il2CppCMS.UI.Logic.Tune;
using Il2CppCMS.UI.Logic.Navigation;
using Il2CppCMS.UI.Windows;
#else
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Controls;
using CMS.UI.Logic;
using CMS.UI.Logic.Shop;
using CMS.UI.Logic.Tune;
using CMS.UI.Logic.Navigation;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class ShoppingListTwoColumnNavigationFeature
    {
        private static bool rowNormalizationScheduled;
        private static ShopListWindow rowNormalizationWindow;

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addShoppingListSorting;
            }
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.FillItems))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void FillItemsPostfix(ShopListWindow __instance)
        {
            if (!IsEnabled)
                return;

            EnsureTwoColumnNavigation(__instance);
            ScheduleTwoColumnRowNormalization(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void ShowPostfix(ShopListWindow __instance)
        {
            if (!IsEnabled)
                return;

            EnsureTwoColumnNavigation(__instance);
            ScheduleTwoColumnRowNormalization(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.OnGridItemSelect))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void GridItemSelectPrefix(
            ShopListWindow __instance, ref int x, ref int y)
        {
            int visualColumns = GetVisualColumns(__instance);
            if (visualColumns <= 0)
                return;

            int visualColumn = x;
            int visualRow = y;
            int visualIndex = visualColumn + (visualRow * visualColumns);

            if (UsesTwoColumnLayout(__instance) &&
                HasExpectedNavigation(__instance)) {
                int nativeColumns = ShopListWindow.Columns;
                if (nativeColumns > 0 && nativeColumns != visualColumns) {
                    x = visualIndex % nativeColumns;
                    y = visualIndex / nativeColumns;
                }
            }

        }

        [HarmonyPatch(typeof(GridNavigationManager), "FindRightItem",
            new Type[] { })]
        [HarmonyPrefix]
        private static bool FindRightItemPrefix(
            GridNavigationManager __instance, ref Vector2Int __result)
        {
            if (!ShouldClampHorizontalNavigation(__instance, true))
                return true;

            __result = __instance.GetCurrentPos();
            return false;
        }

        [HarmonyPatch(typeof(GridNavigationManager), "FindLeftItem",
            new Type[] { })]
        [HarmonyPrefix]
        private static bool FindLeftItemPrefix(
            GridNavigationManager __instance, ref Vector2Int __result)
        {
            if (!ShouldClampHorizontalNavigation(__instance, false))
                return true;

            __result = __instance.GetCurrentPos();
            return false;
        }

        private static bool ShouldClampHorizontalNavigation(
            GridNavigationManager manager, bool movingRight)
        {
            if (manager == null)
                return false;

            UIManager uiManager = UIManager.Get();
            ShopListWindow window = uiManager != null
                ? uiManager.ShopListWindow : null;
            if (window == null || window.gridNavigationManager != manager ||
                !UsesTwoColumnLayout(window) || !HasExpectedNavigation(window) ||
                manager.elements == null)
                return false;

            int visualColumn = manager.GetCurrentRow();
            int visualRow = manager.GetCurrentColumn();
            if (visualColumn < 0 || visualColumn >= manager.elements.Count)
                return false;

            int targetColumn = visualColumn + (movingRight ? 1 : -1);
            if (targetColumn < 0 || targetColumn >= manager.elements.Count)
                return true;

            GridItems target = manager.elements[targetColumn];
            return target == null || target.items == null ||
                visualRow < 0 || visualRow >= target.items.Count;
        }

        private static void ScheduleTwoColumnRowNormalization(ShopListWindow window)
        {
            rowNormalizationWindow = window;
            if (rowNormalizationScheduled)
                return;

            rowNormalizationScheduled = true;
            MelonCoroutines.Start(NormalizeTwoColumnRowsDeferred());
        }

        private static IEnumerator NormalizeTwoColumnRowsDeferred()
        {
            yield return new WaitForEndOfFrame();

            ShopListWindow window = rowNormalizationWindow;
            rowNormalizationWindow = null;
            rowNormalizationScheduled = false;
            NormalizeTwoColumnRows(window);
        }

        internal static void RefreshRowsNow(ShopListWindow window)
        {
            if (!IsEnabled)
                return;

            EnsureTwoColumnNavigation(window);
            NormalizeTwoColumnRows(window);
        }

        private static void NormalizeTwoColumnRows(ShopListWindow window)
        {
            if (!UsesTwoColumnLayout(window) || window == null ||
                window.shopListItems == null || window.items == null ||
                window.shopListItemsParent == null)
                return;

            RectTransform content =
                window.shopListItemsParent.GetComponent<RectTransform>();
            RectTransform viewport = content != null && content.parent != null ?
                content.parent.GetComponent<RectTransform>() : null;
            UnityEngine.UI.GridLayoutGroup grid =
                window.shopListItemsParent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (content == null || viewport == null || grid == null)
                return;

            float rowStride = grid.cellSize.y + grid.spacing.y;
            if (rowStride <= 0f || viewport.rect.height <= 0f)
                return;

            int visibleRows = Math.Max(1, Mathf.FloorToInt(
                (viewport.rect.height + grid.spacing.y + 0.01f) / rowStride));
            int itemCount = Math.Min(
                ShoppingListShopFilterFeature.GetVisibleItemCount(window),
                window.shopListItems.Count);
            int visualColumns = GetVisualColumns(window);
            if (visualColumns <= 0)
                return;

            int requiredRows = Math.Max(visibleRows,
                (itemCount + visualColumns - 1) / visualColumns);
            bool singleVisualRow = itemCount > 0 &&
                itemCount <= visualColumns;
            int activeSlots = singleVisualRow ||
                ShoppingListShopFilterFeature.IsFiltering(window)
                    ? itemCount
                    : Math.Min(window.shopListItems.Count,
                        requiredRows * visualColumns);
            for (int index = 0; index < window.shopListItems.Count; index++) {
                ShopListItem item = window.shopListItems[index];
                if (item == null)
                    continue;

                bool shouldBeActive = index < activeSlots;
                if (item.gameObject.activeSelf != shouldBeActive)
                    item.gameObject.SetActive(shouldBeActive);

                if (index < itemCount) {
                    ShopListItemData data = window.items[index];
                    if (data != null) {
                        ShoppingListPresentationFeature.UpdateRowCard(item, data);
                        ShoppingListQuantityFeature.UpdateRow(window, item, data);
                    } else {
                        HideUnusedRow(item);
                    }
                } else {
                    HideUnusedRow(item);
                }

                if (item.background != null && item.background.activeSelf)
                    item.background.SetActive(false);
            }
        }

        private static void HideUnusedRow(ShopListItem row)
        {
            if (row == null)
                return;

            ShoppingListQuantityFeature.HideRow(row);
            ShoppingListPresentationFeature.HideRowCard(row);
            if (row.itemName != null)
                row.itemName.gameObject.SetActive(false);
            if (row.amount != null)
                row.amount.gameObject.SetActive(false);
            if (row.background != null)
                row.background.SetActive(false);
            Transform selected = row.transform.Find("Selected");
            if (selected != null)
                selected.gameObject.SetActive(false);
        }

        private static void EnsureTwoColumnNavigation(ShopListWindow window)
        {
            if (!UsesTwoColumnLayout(window) || HasExpectedNavigation(window))
                return;

            RebuildNavigation(window);
        }

        private static void RebuildNavigation(ShopListWindow window)
        {
            if (window == null || window.gridNavigationManager == null ||
                window.shopListItems == null || window.items == null)
                return;

            int itemCount = Math.Min(
                ShoppingListShopFilterFeature.GetVisibleItemCount(window),
                window.shopListItems.Count);
            int visualColumns = GetVisualColumns(window);
            if (visualColumns <= 0)
                return;

            int navigationColumnCount = Math.Min(visualColumns, itemCount);
            UnhollowerBaseLib.Il2CppReferenceArray<GridItems> columns =
                new UnhollowerBaseLib.Il2CppReferenceArray<GridItems>(
                    navigationColumnCount);

            for (int visualColumn = 0;
                visualColumn < navigationColumnCount; visualColumn++) {
                int rowsInColumn = itemCount > visualColumn
                    ? ((itemCount - 1 - visualColumn) / visualColumns) + 1
                    : 0;

                GridItems column = new GridItems();
                column.items =
                    new UnhollowerBaseLib.Il2CppReferenceArray<GridItem>(
                        rowsInColumn);

                for (int visualRow = 0;
                    visualRow < rowsInColumn; visualRow++) {
                    int visualIndex =
                        visualColumn + (visualRow * visualColumns);
                    column.items[visualRow] =
                        window.shopListItems[visualIndex];
                }

                columns[visualColumn] = column;
            }

            window.gridNavigationManager.SetGridItems(columns);
        }

        private static int GetVisualColumns(ShopListWindow window)
        {
            if (window == null || window.shopListItemsParent == null)
                return 0;

            UnityEngine.UI.GridLayoutGroup grid =
                window.shopListItemsParent.GetComponent<
                    UnityEngine.UI.GridLayoutGroup>();
            if (grid == null || grid.constraint !=
                UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount)
                return 0;

            return Math.Max(1, grid.constraintCount);
        }

        private static bool UsesTwoColumnLayout(ShopListWindow window)
        {
            if (!IsEnabled || window == null ||
                window.shopListItemsParent == null)
                return false;

            UnityEngine.UI.GridLayoutGroup grid =
                window.shopListItemsParent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            return grid != null && grid.cellSize.x > 0f &&
                grid.constraint ==
                    UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount &&
                grid.constraintCount > 0;
        }

        private static bool HasExpectedNavigation(ShopListWindow window)
        {
            if (window == null || window.gridNavigationManager == null ||
                window.gridNavigationManager.elements == null ||
                window.shopListItems == null || window.items == null)
                return false;

            int itemCount = Math.Min(
                ShoppingListShopFilterFeature.GetVisibleItemCount(window),
                window.shopListItems.Count);
            int visualColumns = GetVisualColumns(window);
            if (visualColumns <= 0)
                return false;

            int expectedColumnCount = Math.Min(visualColumns, itemCount);
            UnhollowerBaseLib.Il2CppReferenceArray<GridItems> columns =
                window.gridNavigationManager.elements;
            if (columns.Count != expectedColumnCount)
                return false;

            for (int visualColumn = 0;
                visualColumn < expectedColumnCount; visualColumn++) {
                GridItems column = columns[visualColumn];
                int expectedRows = itemCount > visualColumn
                    ? ((itemCount - 1 - visualColumn) / visualColumns) + 1
                    : 0;
                if (column == null || column.items == null ||
                    column.items.Count != expectedRows)
                    return false;
            }

            return true;
        }
    }
}
