using System;
using System.Collections;
using System.Text;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;

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
        private const int VisualColumns = 2;
        private const float RepairabilityIndicatorSize = 15f;
        private const float RepairabilityIndicatorGap = 3f;
        private const float OwnedIndicatorSize = 15f;
        private const float OwnedIndicatorGap = 3f;
        private const float OwnedCountMinimumWidth = 40f;
        private const string RepairabilityIndicatorName = "QrepairIcon";
        private const string OwnedIndicatorName = "QownedCount";
        private const string OwnedCountTextName = "Count";
        private const string TotalCountColor = "FFFFFF";
        private const string PerfectCountColor = "66FF33";
        private const string Condition50To99Color = "FFFF00";
        private const string Condition15To49Color = "FF9900";
        private const string ConditionBelow15Color = "FF0000";
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
            int visualColumn = x;
            int visualRow = y;
            int visualIndex = visualColumn + (visualRow * VisualColumns);

            if (UsesTwoColumnLayout(__instance) &&
                HasExpectedNavigation(__instance)) {
                int nativeColumns = ShopListWindow.Columns;
                if (nativeColumns > 0 && nativeColumns != VisualColumns) {
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

        internal static int GetCurrentVisualIndex(ShopListWindow window)
        {
            if (window == null || window.gridNavigationManager == null ||
                window.gridNavigationManager.elements == null ||
                window.shopListItems == null)
                return -1;

            GridNavigationManager manager = window.gridNavigationManager;
            int rowIndex = manager.GetCurrentRow();
            int columnIndex = manager.GetCurrentColumn();
            if (rowIndex < 0 || rowIndex >= manager.elements.Count)
                return -1;

            GridItems row = manager.elements[rowIndex];
            if (row == null || row.items == null || columnIndex < 0 ||
                columnIndex >= row.items.Count)
                return -1;

            GridItem current = row.items[columnIndex];
            if (current == null)
                return -1;

            for (int index = 0; index < window.shopListItems.Count; index++) {
                if (window.shopListItems[index] == current)
                    return index;
            }

            return -1;
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
            int itemCount = Math.Min(window.items.Count, window.shopListItems.Count);
            int requiredRows = Math.Max(visibleRows,
                (itemCount + VisualColumns - 1) / VisualColumns);
            int activeSlots = Math.Min(window.shopListItems.Count,
                requiredRows * VisualColumns);
            bool showRepairability = IsEnabled;
            bool showOwnedCount = IsEnabled;

            for (int index = 0; index < window.shopListItems.Count; index++) {
                ShopListItem item = window.shopListItems[index];
                if (item == null)
                    continue;

                bool shouldBeActive = index < activeSlots;
                if (item.gameObject.activeSelf != shouldBeActive)
                    item.gameObject.SetActive(shouldBeActive);

                if (index < itemCount) {
                    ShopListItemData data = window.items[index];
                    ShoppingListQuantityFeature.UpdateRow(window, item, data);
                    bool showRepairabilityIndicator = showRepairability &&
                        data != null && !string.IsNullOrEmpty(data.ID);
                    float ownedIndicatorWidth = UpdateOwnedPartIndicator(
                        item, data, showOwnedCount);
                    Vector2 repairabilityPosition;
                    if (PrepareIndicatorLayout(item,
                        showRepairabilityIndicator, ownedIndicatorWidth,
                        out repairabilityPosition)) {
                        UpdateRepairabilityIndicator(item, data,
                            showRepairabilityIndicator, repairabilityPosition);
                    } else {
                        HideRepairabilityIndicator(item);
                        HideOwnedPartIndicator(item);
                    }
                } else {
                    ShoppingListQuantityFeature.HideRow(item);
                    HideRepairabilityIndicator(item);
                    HideOwnedPartIndicator(item);
                }

                if (!shouldBeActive || item.background == null)
                    continue;

                bool showBackground = ((index / VisualColumns) & 1) == 0;
                if (item.background.activeSelf != showBackground)
                    item.background.SetActive(showBackground);
            }
        }

        private static void UpdateRepairabilityIndicator(
            ShopListItem row, ShopListItemData data, bool enabled,
            Vector2 indicatorPosition)
        {
            if (row == null)
                return;

            GameObject icon =
                row.transform.Find(RepairabilityIndicatorName)?.gameObject;
            if (!enabled || data == null || string.IsNullOrEmpty(data.ID)) {
                icon?.SetActive(false);
                return;
            }

            bool isRepairable = PartRepairabilityRules.IsRepairable(data.ID);
            Sprite sprite = isRepairable
                ? InventoryIconProvider.GetGreenRepairWrenchIcon()
                : InventoryIconProvider.GetRedRepairWrenchIcon();
            if (sprite == null) {
                icon?.SetActive(false);
                return;
            }

            if (icon == null)
                icon = CreateIndicatorObject(row, RepairabilityIndicatorName);

            UnityEngine.UI.Image image = icon.GetComponent<UnityEngine.UI.Image>();
            RectTransform rect = icon.GetComponent<RectTransform>();
            if (image == null || rect == null) {
                icon.SetActive(false);
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = indicatorPosition;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(
                RepairabilityIndicatorSize, RepairabilityIndicatorSize);
            icon.SetActive(true);
        }

        private static float UpdateOwnedPartIndicator(
            ShopListItem row, ShopListItemData data, bool enabled)
        {
            if (row == null)
                return 0f;

            GameObject icon = row.transform.Find(OwnedIndicatorName)?.gameObject;
            if (!enabled || data == null || string.IsNullOrEmpty(data.ID)) {
                icon?.SetActive(false);
                return 0f;
            }

            OwnedPartCache.ConditionBreakdown breakdown =
                GetOwnedConditionBreakdown(data);
            bool hasOwnedParts = breakdown.Total > 0;
            Sprite sprite = hasOwnedParts
                ? InventoryIconProvider.GetWhiteWarehouseIcon()
                : InventoryIconProvider.GetRedWarehouseIcon();
            if (sprite == null) {
                icon?.SetActive(false);
                return 0f;
            }

            if (icon == null)
                icon = CreateOwnedPartIndicator(row);

            UnityEngine.UI.Image image = icon.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Text countText = icon.transform
                .Find(OwnedCountTextName)?.GetComponent<UnityEngine.UI.Text>();
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            RectTransform countRect = countText != null ?
                countText.GetComponent<RectTransform>() : null;
            if (image == null || countText == null || iconRect == null ||
                countRect == null) {
                icon.SetActive(false);
                return 0f;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = hasOwnedParts;
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.localScale = Vector3.one;
            iconRect.sizeDelta = new Vector2(
                OwnedIndicatorSize, OwnedIndicatorSize);

            countText.text = hasOwnedParts ? BuildOwnedCountText(breakdown) : string.Empty;
            countText.color = Color.white;
            countText.gameObject.SetActive(false);

            float countWidth = hasOwnedParts
                ? Math.Max(OwnedCountMinimumWidth, countText.preferredWidth + 1f)
                : 0f;
            countRect.anchorMin = new Vector2(0f, 0.5f);
            countRect.anchorMax = new Vector2(0f, 0.5f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.anchoredPosition =
                new Vector2(OwnedIndicatorSize + OwnedIndicatorGap, 0f);
            countRect.sizeDelta = new Vector2(
                countWidth, OwnedIndicatorSize);
            countText.raycastTarget = hasOwnedParts;

            ConfigureOwnedIndicatorHover(icon, countText, hasOwnedParts);
            icon.SetActive(true);
            return OwnedIndicatorSize;
        }

        private static OwnedPartCache.ConditionBreakdown
            GetOwnedConditionBreakdown(ShopListItemData data)
        {
            ShopListItemDataEx additional = data != null ?
                data.AdditionalData : null;
            return OwnedPartCache.GetConditionBreakdown(
                data != null ? data.ID : null,
                additional != null ? additional.ET : 0,
                additional != null ? additional.Profile : 0,
                additional != null ? additional.Size : 0,
                additional != null ? additional.Width : 0);
        }

        private static bool PrepareIndicatorLayout(
            ShopListItem row, bool reserveRepairability,
            float ownedIndicatorWidth, out Vector2 repairabilityPosition)
        {
            repairabilityPosition = Vector2.zero;
            RectTransform rowRect = row.GetComponent<RectTransform>();
            RectTransform amountRect =
                row.transform.Find("Amount")?.GetComponent<RectTransform>();
            RectTransform textRect =
                row.transform.Find("TextContainer")?.GetComponent<RectTransform>();
            if (rowRect == null || amountRect == null || textRect == null ||
                rowRect.rect.width <= 0f || amountRect.rect.width <= 0f)
                return false;

            if (textRect.GetComponent<UnityEngine.UI.RectMask2D>() == null)
                textRect.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

            float cursorRight = amountRect.anchoredPosition.x - rowRect.rect.width;
            bool hasIndicator = false;
            if (reserveRepairability) {
                cursorRight -= RepairabilityIndicatorGap;
                repairabilityPosition = new Vector2(cursorRight, 0f);
                cursorRight -= RepairabilityIndicatorSize;
                hasIndicator = true;
            }

            GameObject ownedIcon =
                row.transform.Find(OwnedIndicatorName)?.gameObject;
            if (ownedIndicatorWidth > 0f && ownedIcon != null) {
                RectTransform ownedRect =
                    ownedIcon.GetComponent<RectTransform>();
                if (ownedRect == null)
                    return false;

                cursorRight -= OwnedIndicatorGap;
                ownedRect.anchoredPosition = new Vector2(
                    cursorRight - ownedIndicatorWidth, 0f);
                cursorRight -= ownedIndicatorWidth;
                hasIndicator = true;
            }

            float textRight = amountRect.anchoredPosition.x;
            if (hasIndicator)
                textRight = rowRect.rect.width + cursorRight -
                    RepairabilityIndicatorGap;
            textRect.sizeDelta = new Vector2(
                Math.Max(0f, textRight - textRect.anchoredPosition.x),
                textRect.sizeDelta.y);
            return true;
        }

        private static void ConfigureOwnedIndicatorHover(
            GameObject icon, UnityEngine.UI.Text countText, bool enabled)
        {
            if (icon == null || countText == null)
                return;

            countText.gameObject.SetActive(false);
            if (enabled && icon.GetComponent<EventTrigger>() == null)
                icon.AddComponent<EventTrigger>();
        }

        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerEnter))]
        [HarmonyPostfix]
        private static void OwnedIndicatorPointerEnterPostfix(EventTrigger __instance)
        {
            SetOwnedIndicatorDetailsVisible(__instance, true);
        }

        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerExit))]
        [HarmonyPostfix]
        private static void OwnedIndicatorPointerExitPostfix(EventTrigger __instance)
        {
            SetOwnedIndicatorDetailsVisible(__instance, false);
        }

        private static void SetOwnedIndicatorDetailsVisible(
            EventTrigger trigger, bool visible)
        {
            if (trigger == null || trigger.gameObject == null ||
                trigger.gameObject.name != OwnedIndicatorName)
                return;

            UnityEngine.UI.Text countText = trigger.transform
                .Find(OwnedCountTextName)?.GetComponent<UnityEngine.UI.Text>();
            if (countText == null || string.IsNullOrEmpty(countText.text))
                return;

            if (visible && !countText.gameObject.activeSelf)
                countText.gameObject.SetActive(true);

            ApplyOwnedIndicatorHoverLayout(
                trigger.gameObject, countText, visible);

            if (!visible && countText.gameObject.activeSelf)
                countText.gameObject.SetActive(false);
        }

        private static void ApplyOwnedIndicatorHoverLayout(
            GameObject icon, UnityEngine.UI.Text countText, bool expanded)
        {
            if (icon == null || countText == null || icon.transform.parent == null)
                return;

            Transform row = icon.transform.parent;
            RectTransform rowRect = row.GetComponent<RectTransform>();
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            RectTransform countRect = countText.GetComponent<RectTransform>();
            RectTransform amountRect =
                row.Find("Amount")?.GetComponent<RectTransform>();
            RectTransform textRect =
                row.Find("TextContainer")?.GetComponent<RectTransform>();
            if (rowRect == null || iconRect == null || countRect == null ||
                amountRect == null || textRect == null || rowRect.rect.width <= 0f)
                return;

            float cursorRight = amountRect.anchoredPosition.x - rowRect.rect.width;
            GameObject repairIcon = row.Find(RepairabilityIndicatorName)?.gameObject;
            if (repairIcon != null && repairIcon.activeSelf)
                cursorRight -= RepairabilityIndicatorGap +
                    RepairabilityIndicatorSize;

            cursorRight -= OwnedIndicatorGap;
            float collapsedIconX = cursorRight - OwnedIndicatorSize;
            float countWidth = Math.Max(0f, countRect.sizeDelta.x);
            float iconX = expanded
                ? collapsedIconX - countWidth - OwnedIndicatorGap
                : collapsedIconX;

            iconRect.anchoredPosition = new Vector2(iconX, 0f);

            float textRight = rowRect.rect.width + iconX - OwnedIndicatorGap;
            textRect.sizeDelta = new Vector2(
                Math.Max(0f, textRight - textRect.anchoredPosition.x),
                textRect.sizeDelta.y);
        }

        private static GameObject CreateOwnedPartIndicator(ShopListItem row)
        {
            GameObject icon = CreateIndicatorObject(row, OwnedIndicatorName);
            UnityEngine.UI.Text sourceText =
                row.transform.Find("Amount")?.GetComponent<UnityEngine.UI.Text>();
            if (sourceText == null)
                return icon;

            GameObject countObject =
                GameObject.Instantiate(sourceText.gameObject, icon.transform);
            countObject.name = OwnedCountTextName;
            countObject.transform.localScale = Vector3.one;
            countObject.SetActive(true);

            TextLocalize localize = countObject.GetComponent<TextLocalize>();
            if (localize != null)
                GameObject.Destroy(localize);

            UnityEngine.UI.Text countText =
                countObject.GetComponent<UnityEngine.UI.Text>();
            if (countText != null) {
                countText.text = string.Empty;
                countText.fontSize = 9;
                countText.resizeTextForBestFit = false;
                countText.alignment = TextAnchor.MiddleLeft;
                countText.horizontalOverflow =
                    HorizontalWrapMode.Overflow;
                countText.verticalOverflow =
                    VerticalWrapMode.Overflow;
                countText.supportRichText = true;
                countText.raycastTarget = false;
            }
            return icon;
        }

        private static GameObject CreateIndicatorObject(
            ShopListItem row, string name)
        {
#if NET6_0_OR_GREATER
            GameObject icon = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image));
#else
            UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type>(3);
            componentTypes[0] = UnhollowerRuntimeLib.Il2CppType.Of<RectTransform>();
            componentTypes[1] = UnhollowerRuntimeLib.Il2CppType.Of<CanvasRenderer>();
            componentTypes[2] =
                UnhollowerRuntimeLib.Il2CppType.Of<UnityEngine.UI.Image>();
            GameObject icon = new GameObject(name, componentTypes);
#endif
            icon.transform.SetParent(row.transform, false);
            icon.layer = row.gameObject.layer;

            UnityEngine.UI.Image image = icon.GetComponent<UnityEngine.UI.Image>();
            if (image != null) {
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            return icon;
        }

        private static string BuildOwnedCountText(
            OwnedPartCache.ConditionBreakdown breakdown)
        {
            StringBuilder text = new StringBuilder(128);
            AppendOwnedCount(text, breakdown.Total, TotalCountColor);
            AppendOwnedCount(text, breakdown.Perfect, PerfectCountColor);
            AppendOwnedCount(text, breakdown.Condition50To99,
                Condition50To99Color);
            AppendOwnedCount(text, breakdown.Condition15To49,
                Condition15To49Color);
            AppendOwnedCount(text, breakdown.ConditionBelow15,
                ConditionBelow15Color);
            return text.ToString();
        }

        private static void AppendOwnedCount(
            StringBuilder text, int count, string color)
        {
            if (count <= 0)
                return;

            if (text.Length > 0)
                text.Append(' ');
            text.Append("<color=#");
            text.Append(color);
            text.Append('>');
            text.Append(count);
            text.Append("</color>");
        }

        private static void HideRepairabilityIndicator(ShopListItem row)
        {
            if (row != null)
                row.transform.Find(RepairabilityIndicatorName)?.gameObject.SetActive(false);
        }

        private static void HideOwnedPartIndicator(ShopListItem row)
        {
            if (row != null)
                row.transform.Find(OwnedIndicatorName)?.gameObject.SetActive(false);
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

            int itemCount = Math.Min(window.items.Count, window.shopListItems.Count);
            int visualRowCount =
                (itemCount + VisualColumns - 1) / VisualColumns;
            int navigationColumnCount = Math.Min(VisualColumns, itemCount);
            UnhollowerBaseLib.Il2CppReferenceArray<GridItems> columns =
                new UnhollowerBaseLib.Il2CppReferenceArray<GridItems>(
                    navigationColumnCount);

            for (int visualColumn = 0;
                visualColumn < navigationColumnCount; visualColumn++) {
                int rowsInColumn = visualRowCount;
                if (visualColumn > 0 &&
                    ((visualRowCount * VisualColumns) > itemCount))
                    rowsInColumn--;

                GridItems column = new GridItems();
                column.items =
                    new UnhollowerBaseLib.Il2CppReferenceArray<GridItem>(
                        rowsInColumn);

                for (int visualRow = 0;
                    visualRow < rowsInColumn; visualRow++) {
                    int visualIndex =
                        visualColumn + (visualRow * VisualColumns);
                    column.items[visualRow] =
                        window.shopListItems[visualIndex];
                }

                columns[visualColumn] = column;
            }

            window.gridNavigationManager.SetGridItems(columns);
        }

        private static bool UsesTwoColumnLayout(ShopListWindow window)
        {
            if (!IsEnabled || window == null ||
                window.shopListItemsParent == null)
                return false;

            RectTransform content =
                window.shopListItemsParent.GetComponent<RectTransform>();
            UnityEngine.UI.GridLayoutGroup grid =
                window.shopListItemsParent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (content == null || grid == null || grid.cellSize.x <= 0f)
                return false;

            float availableWidth = content.rect.width -
                grid.padding.left - grid.padding.right;
            float stride = grid.cellSize.x + grid.spacing.x;
            if (availableWidth <= 0f || stride <= 0f)
                return false;

            int columns = Mathf.FloorToInt(
                (availableWidth + grid.spacing.x + 0.01f) / stride);
            return columns == VisualColumns;
        }

        private static bool HasExpectedNavigation(ShopListWindow window)
        {
            if (window == null || window.gridNavigationManager == null ||
                window.gridNavigationManager.elements == null ||
                window.shopListItems == null || window.items == null)
                return false;

            int itemCount = Math.Min(window.items.Count, window.shopListItems.Count);
            int visualRowCount =
                (itemCount + VisualColumns - 1) / VisualColumns;
            int expectedColumnCount = Math.Min(VisualColumns, itemCount);
            UnhollowerBaseLib.Il2CppReferenceArray<GridItems> columns =
                window.gridNavigationManager.elements;
            if (columns.Count != expectedColumnCount)
                return false;

            for (int visualColumn = 0;
                visualColumn < expectedColumnCount; visualColumn++) {
                GridItems column = columns[visualColumn];
                int expectedRows = visualRowCount;
                if (visualColumn > 0 &&
                    ((visualRowCount * VisualColumns) > itemCount))
                    expectedRows--;
                if (column == null || column.items == null ||
                    column.items.Count != expectedRows)
                    return false;
            }

            return true;
        }
    }
}
