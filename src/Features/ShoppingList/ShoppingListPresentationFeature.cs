using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
#else
using CMS.Containers;
using CMS.UI.Logic;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Presents the native shopping list in two visual columns while preserving
    /// its native three-column selection logic.
    /// </summary>
    [HarmonyPatch]
    internal static class ShoppingListPresentationFeature
    {
        private const int TargetVisualColumns = 2;
        private static readonly Dictionary<int, float> NativeGridCellWidths =
            new Dictionary<int, float>();
        private static bool nativeRowLayoutCaptured;
        private static RowLayoutState nativeRowLayout;

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addShoppingListSorting;
            }
        }

        private struct RowLayoutState
        {
            internal float SelectedWidth;
            internal float TextContainerWidth;
            internal float AmountX;
            internal bool HasSelected;
            internal bool HasTextContainer;
            internal bool HasAmount;
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        private static void ShopListWindowShowPostfix(ShopListWindow __instance,
            bool __result)
        {
            if (__result && IsEnabled)
                ApplyTwoColumnPresentation(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), "FillItems")]
        [HarmonyPostfix]
        private static void ShopListWindowFillItemsPostfix(ShopListWindow __instance)
        {
            if (IsEnabled)
                ApplyTwoColumnPresentation(__instance);
        }

        private static void ApplyTwoColumnPresentation(ShopListWindow window)
        {
            try {
                if (window == null || window.shopListItemsParent == null)
                    return;

                Transform parent = window.shopListItemsParent;
                GridLayoutGroup grid = parent.GetComponent<GridLayoutGroup>();
                if (grid == null)
                    grid = parent.GetComponentInChildren<GridLayoutGroup>(true);
                if (grid == null)
                    return;

                RectTransform gridRect = grid.GetComponent<RectTransform>();
                if (gridRect == null)
                    return;

                int gridId = grid.GetInstanceID();
                float nativeCellWidth;
                if (!NativeGridCellWidths.TryGetValue(gridId,
                    out nativeCellWidth)) {
                    nativeCellWidth = grid.cellSize.x;
                    if (nativeCellWidth <= 0f)
                        return;
                    NativeGridCellWidths.Add(gridId, nativeCellWidth);
                }

                if (!nativeRowLayoutCaptured)
                    CaptureNativeRowLayout(parent);

                int nativeColumns = Math.Max(1, ShopListWindow.Columns);
                float nativeOccupiedWidth = nativeCellWidth * nativeColumns +
                    grid.spacing.x * Math.Max(0, nativeColumns - 1);
                float targetWidth = (nativeOccupiedWidth -
                    grid.spacing.x * (TargetVisualColumns - 1)) /
                    TargetVisualColumns;

                float maximumWidth = gridRect.rect.width - grid.padding.left -
                    grid.padding.right -
                    grid.spacing.x * (TargetVisualColumns - 1);
                if (maximumWidth > 0f)
                    targetWidth = Mathf.Min(targetWidth,
                        maximumWidth / TargetVisualColumns);
                if (targetWidth <= 0f)
                    return;

                Vector2 cellSize = grid.cellSize;
                cellSize.x = targetWidth;
                grid.cellSize = cellSize;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = TargetVisualColumns;
                grid.childAlignment = TextAnchor.UpperLeft;

                ResizeRows(parent, nativeCellWidth, targetWidth);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[ShoppingList] Failed to apply two-column presentation." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        private static void CaptureNativeRowLayout(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = 0; i < parent.childCount; i++) {
                Transform row = parent.GetChild(i);
                if (row == null)
                    continue;

                RowLayoutState state = new RowLayoutState();
                Transform selected = row.Find("Selected");
                RectTransform selectedRect = selected != null
                    ? selected.GetComponent<RectTransform>() : null;
                if (selectedRect != null) {
                    state.SelectedWidth = selectedRect.sizeDelta.x;
                    state.HasSelected = true;
                }

                Transform textContainer = row.Find("TextContainer");
                RectTransform textRect = textContainer != null
                    ? textContainer.GetComponent<RectTransform>() : null;
                if (textRect != null) {
                    state.TextContainerWidth = textRect.sizeDelta.x;
                    state.HasTextContainer = true;
                }

                Transform amount = row.Find("Amount");
                RectTransform amountRect = amount != null
                    ? amount.GetComponent<RectTransform>() : null;
                if (amountRect != null) {
                    state.AmountX = amountRect.anchoredPosition.x;
                    state.HasAmount = true;
                }

                if (!state.HasSelected && !state.HasTextContainer &&
                    !state.HasAmount)
                    continue;

                nativeRowLayout = state;
                nativeRowLayoutCaptured = true;
                return;
            }
        }

        private static void ResizeRows(Transform parent, float nativeWidth,
            float targetWidth)
        {
            if (parent == null || !nativeRowLayoutCaptured)
                return;

            float delta = targetWidth - nativeWidth;
            for (int i = 0; i < parent.childCount; i++) {
                Transform row = parent.GetChild(i);
                if (row == null)
                    continue;

                if (nativeRowLayout.HasSelected) {
                    Transform selected = row.Find("Selected");
                    RectTransform rect = selected != null
                        ? selected.GetComponent<RectTransform>() : null;
                    if (rect != null) {
                        Vector2 size = rect.sizeDelta;
                        size.x = nativeRowLayout.SelectedWidth + delta;
                        rect.sizeDelta = size;
                    }
                }

                if (nativeRowLayout.HasTextContainer) {
                    Transform textContainer = row.Find("TextContainer");
                    RectTransform rect = textContainer != null
                        ? textContainer.GetComponent<RectTransform>() : null;
                    if (rect != null) {
                        Vector2 size = rect.sizeDelta;
                        size.x = nativeRowLayout.TextContainerWidth + delta;
                        rect.sizeDelta = size;
                    }
                }

                if (nativeRowLayout.HasAmount) {
                    Transform amount = row.Find("Amount");
                    RectTransform rect = amount != null
                        ? amount.GetComponent<RectTransform>() : null;
                    if (rect != null) {
                        Vector2 position = rect.anchoredPosition;
                        position.x = nativeRowLayout.AmountX + delta;
                        rect.anchoredPosition = position;
                    }
                }
            }
        }
    }
}
