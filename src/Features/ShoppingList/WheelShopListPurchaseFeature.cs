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
    /// <summary>
    /// Shopping-list purchase helper: fills the requested quantity for every part.
    /// Tires and rims additionally receive their requested dimensions; tire parameters
    /// are displayed in Width/Profile/Size order. The shopping list is not overlaid.
    /// </summary>
    [HarmonyPatch]
    public static class WheelShopListPurchaseFeature
    {
        private static string selectedItemID;
        private static int selectedAmount;
        private const float SelectionLifetimeSeconds = 15f;

        private static ShopListItemDataEx selectedData;
        private static float selectedAtTime;
        private static ShopListItemDataEx lastTireOptions = new ShopListItemDataEx();

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.OnGridItemSelect))]
        [HarmonyPostfix]
        public static void GridItemSelectPostfix(ShopListWindow __instance, int x, int y)
        {
            if (!IsEnabled())
                return;

            if (__instance.items == null)
                return;

            int index = x + (y * ShopListWindow.Columns);
            if (index < 0 || index >= __instance.items.Count)
                return;

            ShopListItemData entry = __instance.items[index];
            if (entry == null)
                return;

            selectedItemID = entry.ID;
            selectedAmount = entry.Amount;
            selectedData = entry.AdditionalData;
            selectedAtTime = Time.realtimeSinceStartup;
        }

        [HarmonyPatch(typeof(ShopBuyWindow), nameof(ShopBuyWindow.Show))]
        [HarmonyPostfix]
        public static void ShopBuyWindowShowPostfix(ShopBuyWindow __instance)
        {
            if (!IsEnabled()) {
                ClearSelection();
                return;
            }

            bool isTire = __instance.type == ShopBuyItemType.Tire;
            bool isRim = __instance.type == ShopBuyItemType.Rim;

            if (isTire)
                PrepareTireOptionLayout(__instance);

            if (!HasFreshSelection()) {
                ClearSelection();
                return;
            }

            if (!PartIdentityComparer.IsCompatibleItemID(
                __instance.itemID, selectedItemID, true)) {
                ClearSelection();
                return;
            }

            string itemID = selectedItemID;
            int amount = selectedAmount;
            ShopListItemDataEx data = selectedData;
            ClearSelection();

            MelonCoroutines.Start(ApplySelectedEntryDeferred(
                __instance, itemID, amount, data, isTire, isRim));
        }

        private static IEnumerator ApplySelectedEntryDeferred(ShopBuyWindow window,
            string itemID, int amount, ShopListItemDataEx data,
            bool isTire, bool isRim)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (!IsExpectedWindow(window, itemID, isTire, isRim))
                yield break;

            ApplyRequestedAmount(window, amount);

            if (data == null || (!isTire && !isRim))
                yield break;

            if (isTire)
                ApplyTireData(window, data);
            else
                ApplyRimData(window, data);
        }

        private static bool HasFreshSelection()
        {
            return !string.IsNullOrEmpty(selectedItemID) && selectedAmount > 0 &&
                Time.realtimeSinceStartup - selectedAtTime <= SelectionLifetimeSeconds;
        }

        private static void ClearSelection()
        {
            selectedItemID = null;
            selectedAmount = 0;
            selectedData = null;
            selectedAtTime = 0f;
        }

        private static bool IsEnabled()
        {
            return GlobalState.IsGarageSceneActive && Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.wheelShopListPurchaseHelper;
        }

        private static void PrepareTireOptionLayout(ShopBuyWindow window)
        {
            if (window.shopOptions == null || window.shopOptions.Length < 4)
                return;

            Transform marker = window.transform.Find("QWheelShopListLayoutApplied");
            if (marker != null)
                return;

            GameObject markerObject = new GameObject("QWheelShopListLayoutApplied");
            markerObject.transform.SetParent(window.transform, false);

            Vector3 sizePosition = window.shopOptions[1].transform.localPosition;
            window.shopOptions[1].transform.localPosition =
                window.shopOptions[3].transform.localPosition;
            window.shopOptions[3].transform.localPosition =
                window.shopOptions[2].transform.localPosition;
            window.shopOptions[2].transform.localPosition = sizePosition;

            if (window.listNavigationManager != null &&
                window.listNavigationManager.elements != null &&
                window.listNavigationManager.elements.Count >= 4) {
                ListItem sizeElement = window.listNavigationManager.elements[1];
                window.listNavigationManager.elements[1] =
                    window.listNavigationManager.elements[2];
                window.listNavigationManager.elements[2] =
                    window.listNavigationManager.elements[3];
                window.listNavigationManager.elements[3] = sizeElement;
            }

            Action<ShopOptionType, int> keepTireValues =
                new Action<ShopOptionType, int>((option, response) => {
                    string itemID = window.itemID;
                    if (option == ShopOptionType.Width) {
                        MelonCoroutines.Start(SaveTireOptions(
                            window, itemID, response, 0));
                    } else if (option == ShopOptionType.Profile) {
                        MelonCoroutines.Start(SaveTireOptions(
                            window, itemID, 0, response));
                    } else if (option == ShopOptionType.Size) {
                        MelonCoroutines.Start(RestoreTireOptions(
                            window, itemID, lastTireOptions.Width,
                            lastTireOptions.Profile));
                    }
                });

            window.shopOptions[1].ValueChangedEvent.AddListener(keepTireValues);
            window.shopOptions[2].ValueChangedEvent.AddListener(keepTireValues);
            window.shopOptions[3].ValueChangedEvent.AddListener(keepTireValues);
        }

        private static void ApplyRequestedAmount(ShopBuyWindow window, int requestedAmount)
        {
            if (requestedAmount <= 0 || window.shopOptions == null ||
                window.shopOptions.Length == 0 || window.shopOptions[0] == null)
                return;

            int minimum = window.shopOptions[0].minAmount;
            int maximum = window.shopOptions[0].maxAmount;
            if (maximum < minimum) {
                ModLogger.Log("[ShoppingList] Invalid purchase amount range for '" +
                    window.itemID + "': " + minimum + ".." + maximum + ".",
                    Types.LoggingLevels.Warning);
                return;
            }

            int amount = Math.Max(minimum, Math.Min(requestedAmount, maximum));
            ApplyOptionValue(window, 0, amount);

            ModLogger.Log("[ShoppingList] Purchase amount prefilled for '" +
                window.itemID + "': requested=" + requestedAmount +
                ", applied=" + amount + ".", Types.LoggingLevels.Normal);
        }

        private static void ApplyTireData(ShopBuyWindow window, ShopListItemDataEx data)
        {
            if (window.shopOptions == null || window.shopOptions.Length < 4)
                return;

            lastTireOptions = new ShopListItemDataEx();
            ApplyOptionValue(window, 1, data.Size);
            ApplyOptionValue(window, 2, data.Width);
            ApplyOptionValue(window, 3, data.Profile);
        }

        private static void ApplyRimData(ShopBuyWindow window, ShopListItemDataEx data)
        {
            if (window.shopOptions == null || window.shopOptions.Length < 3)
                return;

            ApplyOptionValue(window, 1, data.Size);
            ApplyOptionValue(window, 2, data.ET);
        }

        private static void ApplyOptionValue(ShopBuyWindow window, int index, int value)
        {
            if (window.shopOptions == null || index < 0 || index >= window.shopOptions.Length)
                return;
            if (value == 0 || value == window.shopOptions[index].currentAmount)
                return;
            if (value < window.shopOptions[index].minAmount ||
                value > window.shopOptions[index].maxAmount)
                return;

            window.shopOptions[index].currentAmount = (short)value;
            window.shopOptions[index].UpdateAmount();
        }

        private static IEnumerator SaveTireOptions(ShopBuyWindow window,
            string itemID, int width, int profile)
        {
            yield return new WaitForEndOfFrame();
            if (!IsExpectedWindow(window, itemID, true, false))
                yield break;

            yield return new WaitForFixedUpdate();
            if (!IsExpectedWindow(window, itemID, true, false))
                yield break;

            if (width != 0)
                lastTireOptions.Width = width;
            if (profile != 0)
                lastTireOptions.Profile = profile;
        }

        private static IEnumerator RestoreTireOptions(ShopBuyWindow window,
            string itemID, int width, int profile)
        {
            if (!IsExpectedWindow(window, itemID, true, false) ||
                window.shopOptions == null || window.shopOptions.Length < 4)
                yield break;

            lastTireOptions = new ShopListItemDataEx();
            yield return new WaitForEndOfFrame();
            if (!IsExpectedWindow(window, itemID, true, false) ||
                window.shopOptions == null || window.shopOptions.Length < 4)
                yield break;

            if (width != 0 && width != window.shopOptions[2].currentAmount &&
                width >= window.shopOptions[2].minAmount &&
                width <= window.shopOptions[2].maxAmount) {
                window.shopOptions[2].currentAmount = (short)width;
                window.shopOptions[2].UpdateAmount();
            }

            if (profile != 0 && profile != window.shopOptions[3].currentAmount &&
                profile >= window.shopOptions[3].minAmount &&
                profile <= window.shopOptions[3].maxAmount) {
                window.shopOptions[3].currentAmount = (short)profile;
                window.shopOptions[3].UpdateAmount();
            }
        }

        private static bool IsExpectedWindow(ShopBuyWindow window, string itemID,
            bool isTire, bool isRim)
        {
            if (!IsEnabled() || window == null || !window.isActiveAndEnabled ||
                !PartIdentityComparer.IsCompatibleItemID(
                    window.itemID, itemID, true))
                return false;

            if (isTire)
                return window.type == ShopBuyItemType.Tire;
            if (isRim)
                return window.type == ShopBuyItemType.Rim;
            return window.type != ShopBuyItemType.Tire &&
                window.type != ShopBuyItemType.Rim;
        }

    }

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

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.FillItems))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void FillItemsPostfix(ShopListWindow __instance)
        {
            EnsureTwoColumnNavigation(__instance);
            ScheduleTwoColumnRowNormalization(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void ShowPostfix(ShopListWindow __instance)
        {
            EnsureTwoColumnNavigation(__instance);
            ScheduleTwoColumnRowNormalization(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.OnGridItemSelect))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void GridItemSelectPrefix(
            ShopListWindow __instance, ref int x, ref int y)
        {
            if (!UsesTwoColumnLayout(__instance) ||
                !HasExpectedNavigation(__instance))
                return;

            int nativeColumns = ShopListWindow.Columns;
            if (nativeColumns <= 0 || nativeColumns == VisualColumns)
                return;

            int index = x + (y * VisualColumns);
            x = index % nativeColumns;
            y = index / nativeColumns;
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
            bool showRepairability = Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.showPartRepairabilityIndicators;
            bool showOwnedCount = Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.showOwnedPartCountIndicators;

            for (int index = 0; index < window.shopListItems.Count; index++) {
                ShopListItem item = window.shopListItems[index];
                if (item == null)
                    continue;

                bool shouldBeActive = index < activeSlots;
                if (item.gameObject.activeSelf != shouldBeActive)
                    item.gameObject.SetActive(shouldBeActive);

                if (index < itemCount) {
                    ShopListItemData data = window.items[index];
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
            int rowCount = (itemCount + VisualColumns - 1) / VisualColumns;
            UnhollowerBaseLib.Il2CppReferenceArray<GridItems> rows =
                new UnhollowerBaseLib.Il2CppReferenceArray<GridItems>(rowCount);

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++) {
                int firstIndex = rowIndex * VisualColumns;
                int columnsInRow = Math.Min(VisualColumns, itemCount - firstIndex);
                GridItems row = new GridItems();
                row.items = new UnhollowerBaseLib.Il2CppReferenceArray<GridItem>(
                    columnsInRow);

                for (int columnIndex = 0;
                    columnIndex < columnsInRow; columnIndex++) {
                    row.items[columnIndex] =
                        window.shopListItems[firstIndex + columnIndex];
                }

                rows[rowIndex] = row;
            }

            window.gridNavigationManager.SetGridItems(rows);
        }

        private static bool UsesTwoColumnLayout(ShopListWindow window)
        {
            if (window == null || window.shopListItemsParent == null)
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
            int rowCount = (itemCount + VisualColumns - 1) / VisualColumns;
            UnhollowerBaseLib.Il2CppReferenceArray<GridItems> rows =
                window.gridNavigationManager.elements;
            if (rows.Count != rowCount)
                return false;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++) {
                GridItems row = rows[rowIndex];
                int expectedColumns = Math.Min(
                    VisualColumns, itemCount - (rowIndex * VisualColumns));
                if (row == null || row.items == null ||
                    row.items.Count != expectedColumns)
                    return false;
            }

            return true;
        }
    }
}
