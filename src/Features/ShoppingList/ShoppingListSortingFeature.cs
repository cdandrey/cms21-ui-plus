using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class ShoppingListSortingFeature
    {
        private const string FooterWindowId = "ShoppingList";
        private const string SortHintId = "Hint_ShoppingListSorting";

        private enum ShoppingListSortType
        {
            Name,
            Availability,
            Price,
            Repairability,
        }

        private sealed class ShoppingListEntry
        {
            public ShopListItemData Data;
            public int OriginalIndex;
            public string Name;
            public bool Available;
            public int Price;
            public bool Repairable;
        }

        private static readonly MethodInfo FillItemsMethod =
            AccessTools.Method(typeof(ShopListWindow), "FillItems");
        private static ShopListWindow activeWindow;
        private static NativeUiFactory.FooterHintHandle sortHint;
        private static NativeUiFactory.SortingWindowHandle sortingWindow;
        private static RectTransform sortHintBackground;
        private static Vector2 sortHintBackgroundNativeAnchorMin;
        private static Vector2 sortHintBackgroundNativeAnchorMax;
        private static Vector2 sortHintBackgroundNativePivot;
        private static Vector2 sortHintBackgroundNativePosition;
        private static Vector2 sortHintBackgroundNativeSize;

        private static bool IsEnabled {
            get {
                return GlobalState.IsGarageSceneActive &&
                    Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addShoppingListSorting;
            }
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        private static void ShowPostfix(ShopListWindow __instance,
            bool __result)
        {
            if (!__result || !IsEnabled || __instance == null)
                return;

            activeWindow = __instance;
            CreateSortHint();
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Hide))]
        [HarmonyPrefix]
        private static void HidePrefix(ShopListWindow __instance)
        {
            if (__instance != activeWindow)
                return;

            DestroySortHint();
            DestroySortingWindow();
            activeWindow = null;
        }

        [HarmonyPatch(typeof(ShopListWindow), "HandleInput")]
        [HarmonyPostfix]
        private static void HandleInputPostfix(ShopListWindow __instance)
        {
            if (!Input.GetKeyDown(KeyCode.C))
                return;
            if (!IsEnabled || __instance == null ||
                __instance != activeWindow || !CanSort(__instance))
                return;

            ToggleSortingWindow();
        }

        private static void CreateSortHint()
        {
            if (!IsEnabled || activeWindow == null ||
                activeWindow.uiDescription == null)
                return;

            if (sortHint != null && sortHint.Root != null) {
                UpdateSortHintText();
                return;
            }

            sortHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = FooterWindowId,
                    WindowRoot = activeWindow.transform,
                    HintRoot = activeWindow.uiDescription.transform,
                    HintId = SortHintId,
                    Keys = new string[] { "C" },
                    Text = ModLocalization.Get(
                        "LOC_ShoppingListSortAction"),
                    Action = new Action(ToggleSortingWindow),
                    Row = 0,
                    AllowAutomaticRowWrap = false,
                    Order = 5,
                    Profile = WindowFooterHintController
                        .NativeFooterProfile.Automatic,
                });
            if (sortHint == null || sortHint.Root == null ||
                sortHint.Rect == null)
                return;

            Vector2 pivot = sortHint.Rect.pivot;
            if (Mathf.Abs(pivot.x) > 0.001f) {
                Vector2 position = sortHint.Rect.anchoredPosition;
                position.x -= pivot.x * sortHint.Rect.rect.width;
                pivot.x = 0f;
                sortHint.Rect.pivot = pivot;
                sortHint.Rect.anchoredPosition = position;
            }
            ConfigureSortHintGeometry();
            UpdateSortHintText();
            EnsureSortHintFooterBackground();
        }

        private static string GetSortHintText()
        {
            return ModLocalization.Get(IsSortingWindowOpen()
                ? "LOC_ShoppingListCloseSortAction"
                : "LOC_ShoppingListSortAction");
        }

        private static void ConfigureSortHintGeometry()
        {
            if (sortHint == null || sortHint.Rect == null ||
                sortHint.Description == null ||
                sortHint.Description.texts == null ||
                sortHint.Description.texts.Length == 0 ||
                sortHint.Description.texts[0] == null)
                return;

            Text label = sortHint.Description.texts[0];
            RectTransform labelRect = label.rectTransform;
            if (labelRect == null)
                return;

            string currentText = label.text;
            label.text = ModLocalization.Get(
                "LOC_ShoppingListSortAction");
            float normalWidth = label.preferredWidth;
            label.text = ModLocalization.Get(
                "LOC_ShoppingListCloseSortAction");
            float closeWidth = label.preferredWidth;
            label.text = currentText;

            float labelWidth = Mathf.Max(normalWidth, closeWidth);
            float labelLeft = 18.4f;
            if (sortHint.Description.buttonImage != null) {
                Bounds buttonBounds;
                if (NativeUiFactory.TryGetRectTransformBounds(
                        sortHint.Description.buttonImage.rectTransform,
                        sortHint.Rect, out buttonBounds))
                    labelLeft = buttonBounds.max.x + 4f;
            }

            LayoutElement layout = label.GetComponent<LayoutElement>();
            if (layout == null)
                layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = labelWidth;
            layout.preferredWidth = labelWidth;
            layout.flexibleWidth = 0f;

            Vector2 anchorMin = labelRect.anchorMin;
            Vector2 anchorMax = labelRect.anchorMax;
            Vector2 pivot = labelRect.pivot;
            Vector2 position = labelRect.anchoredPosition;
            Vector2 size = labelRect.sizeDelta;
            anchorMin.x = 0f;
            anchorMax.x = 0f;
            pivot.x = 0f;
            position.x = labelLeft;
            size.x = labelWidth;
            labelRect.anchorMin = anchorMin;
            labelRect.anchorMax = anchorMax;
            labelRect.pivot = pivot;
            labelRect.anchoredPosition = position;
            labelRect.sizeDelta = size;
            label.alignment = TextAnchor.MiddleLeft;

            float hintWidth = labelLeft + labelWidth;
            sortHint.Rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, hintWidth);
            sortHint.Width = hintWidth;
            sortHint.Label = label;
        }

        private static void UpdateSortHintText()
        {
            if (sortHint == null || sortHint.Root == null)
                return;

            string text = GetSortHintText();
            sortHint.Text = text;
            if (sortHint.Label != null)
                sortHint.Label.text = text;
            else if (sortHint.Description != null &&
                sortHint.Description.texts != null &&
                sortHint.Description.texts.Length > 0 &&
                sortHint.Description.texts[0] != null) {
                sortHint.Label = sortHint.Description.texts[0];
                sortHint.Label.text = text;
            }
        }

        private static void DestroySortHint()
        {
            WindowFooterHintController.RemoveHint(
                FooterWindowId, SortHintId);
            sortHint = null;
            RestoreSortHintFooterBackground();
        }

        private static void EnsureSortHintFooterBackground()
        {
            if (sortHint == null || sortHint.Root == null ||
                sortHint.Rect == null || sortHint.Rect.parent == null ||
                activeWindow == null)
                return;

            RectTransform parent = sortHint.Rect.parent
                .GetComponent<RectTransform>();
            if (parent == null)
                return;

            Transform backgroundTransform = parent.Find("BG");
            RectTransform background = backgroundTransform != null
                ? backgroundTransform.GetComponent<RectTransform>() : null;
            Transform windowBackgroundTransform =
                activeWindow.transform.Find("BG");
            RectTransform windowBackground = windowBackgroundTransform != null
                ? windowBackgroundTransform.GetComponent<RectTransform>()
                : null;
            if (background == null || windowBackground == null)
                return;

            if (sortHintBackground != background) {
                RestoreSortHintFooterBackground();
                sortHintBackground = background;
                sortHintBackgroundNativeAnchorMin = background.anchorMin;
                sortHintBackgroundNativeAnchorMax = background.anchorMax;
                sortHintBackgroundNativePivot = background.pivot;
                sortHintBackgroundNativePosition =
                    background.anchoredPosition;
                sortHintBackgroundNativeSize = background.sizeDelta;
            }

            Bounds windowBounds;
            if (!NativeUiFactory.TryGetRectTransformBounds(
                    windowBackground, parent, out windowBounds))
                return;

            Vector2 anchorMin = background.anchorMin;
            Vector2 anchorMax = background.anchorMax;
            Vector2 pivot = background.pivot;
            Vector2 position = background.anchoredPosition;
            Vector2 size = background.sizeDelta;
            anchorMin.x = 0f;
            anchorMax.x = 0f;
            pivot.x = 0f;
            position.x = windowBounds.min.x;
            size.x = windowBounds.size.x;
            background.anchorMin = anchorMin;
            background.anchorMax = anchorMax;
            background.pivot = pivot;
            background.anchoredPosition = position;
            background.sizeDelta = size;
        }

        private static void RestoreSortHintFooterBackground()
        {
            if (sortHintBackground != null) {
                sortHintBackground.anchorMin =
                    sortHintBackgroundNativeAnchorMin;
                sortHintBackground.anchorMax =
                    sortHintBackgroundNativeAnchorMax;
                sortHintBackground.pivot = sortHintBackgroundNativePivot;
                sortHintBackground.anchoredPosition =
                    sortHintBackgroundNativePosition;
                sortHintBackground.sizeDelta =
                    sortHintBackgroundNativeSize;
            }
            sortHintBackground = null;
            sortHintBackgroundNativeAnchorMin = Vector2.zero;
            sortHintBackgroundNativeAnchorMax = Vector2.zero;
            sortHintBackgroundNativePivot = Vector2.zero;
            sortHintBackgroundNativePosition = Vector2.zero;
            sortHintBackgroundNativeSize = Vector2.zero;
        }

        private static bool IsSortingWindowOpen()
        {
            return sortingWindow != null && sortingWindow.Root != null;
        }

        private static void ToggleSortingWindow()
        {
            if (IsSortingWindowOpen()) {
                DestroySortingWindow();
                UpdateSortHintText();
                return;
            }

            OpenSortingWindow();
        }

        private static void OpenSortingWindow()
        {
            if (!IsEnabled || activeWindow == null ||
                !CanSort(activeWindow))
                return;
            if (sortingWindow != null && sortingWindow.Root != null)
                return;

            WindowManager manager = WindowManager.Instance;
            SortingWindow source = manager != null
                ? manager.GetWindowByID<SortingWindow>(WindowID.Sorting)
                : null;
            if (source == null)
                return;

            string[] captions = {
                ModLocalization.Get("LOC_ShoppingListSortNameDescending"),
                ModLocalization.Get("LOC_ShoppingListSortNameAscending"),
                ModLocalization.Get("LOC_ShoppingListSortOwnedDescending"),
                ModLocalization.Get("LOC_ShoppingListSortOwnedAscending"),
                ModLocalization.Get("LOC_ShoppingListSortPriceDescending"),
                ModLocalization.Get("LOC_ShoppingListSortPriceAscending"),
                ModLocalization.Get("LOC_ShoppingListSortRepairabilityDescending"),
                ModLocalization.Get("LOC_ShoppingListSortRepairabilityAscending"),
            };
            sortingWindow = NativeUiFactory.CreateSortingWindow(
                source, "CMS21UIPlus.ShoppingListSortingWindow",
                ModLocalization.Get("LOC_ShoppingListSortWindowTitle"),
                captions, new Action<int>(HandleSortSelection));
            UpdateSortHintText();
        }

        private static void HandleSortSelection(int index)
        {
            ShoppingListSortType sortType;
            bool ascending;
            switch (index) {
                case 0:
                    sortType = ShoppingListSortType.Name;
                    ascending = false;
                    break;
                case 1:
                    sortType = ShoppingListSortType.Name;
                    ascending = true;
                    break;
                case 2:
                    sortType = ShoppingListSortType.Availability;
                    ascending = false;
                    break;
                case 3:
                    sortType = ShoppingListSortType.Availability;
                    ascending = true;
                    break;
                case 4:
                    sortType = ShoppingListSortType.Price;
                    ascending = false;
                    break;
                case 5:
                    sortType = ShoppingListSortType.Price;
                    ascending = true;
                    break;
                case 6:
                    sortType = ShoppingListSortType.Repairability;
                    ascending = false;
                    break;
                case 7:
                    sortType = ShoppingListSortType.Repairability;
                    ascending = true;
                    break;
                default:
                    return;
            }

            DestroySortingWindow();
            ApplySort(sortType, ascending);
            UpdateSortHintText();
        }

        private static void DestroySortingWindow()
        {
            if (sortingWindow == null)
                return;
            NativeUiFactory.DestroySortingWindow(sortingWindow);
            sortingWindow = null;
        }

        private static void ApplySort(ShoppingListSortType sortType,
            bool ascending)
        {
            ShopListWindow window = activeWindow;
            if (window == null || window.items == null ||
                window.items.Count <= 1)
                return;

            try {
                if (sortType == ShoppingListSortType.Availability)
                    OwnedPartCache.Refresh();

                List<ShoppingListEntry> entries =
                    BuildEntries(window, sortType);
                if (entries.Count <= 1)
                    return;

                entries.Sort(delegate(ShoppingListEntry left,
                    ShoppingListEntry right) {
                    return CompareEntries(left, right, sortType, ascending);
                });

                for (int i = 0; i < entries.Count; i++)
                    window.items[i] = entries[i].Data;
                RefreshItems(window);
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Sorting failed." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
            }
        }

        private static List<ShoppingListEntry> BuildEntries(
            ShopListWindow window, ShoppingListSortType sortType)
        {
            int count = window != null && window.items != null
                ? window.items.Count : 0;
            List<ShoppingListEntry> entries =
                new List<ShoppingListEntry>(count);
            GameInventory inventory = Singleton<GameInventory>.Instance;

            for (int i = 0; i < count; i++) {
                ShopListItemData data = window.items[i];
                PartProperty property = GetPartProperty(inventory, data);
                ShoppingListEntry entry = new ShoppingListEntry {
                    Data = data,
                    OriginalIndex = i,
                    Name = GetDisplayName(window, i, inventory, data,
                        property),
                    Price = sortType == ShoppingListSortType.Price &&
                        property != null ? property.Price : int.MaxValue,
                    Repairable = sortType ==
                        ShoppingListSortType.Repairability &&
                        data != null &&
                        PartRepairabilityRules.IsRepairable(data.ID),
                };
                if (sortType == ShoppingListSortType.Availability)
                    entry.Available = HasOwnedPart(data);
                entries.Add(entry);
            }
            return entries;
        }

        private static PartProperty GetPartProperty(GameInventory inventory,
            ShopListItemData data)
        {
            if (inventory == null || data == null ||
                string.IsNullOrEmpty(data.ID) ||
                !inventory.ExistsInPartProperty(data.ID))
                return null;
            return inventory.GetItemProperty(data.ID);
        }

        private static string GetDisplayName(ShopListWindow window,
            int index, GameInventory inventory, ShopListItemData data,
            PartProperty property)
        {
            if (window != null && window.shopListItems != null &&
                index >= 0 && index < window.shopListItems.Count) {
                ShopListItem item = window.shopListItems[index];
                if (item != null && item.itemName != null &&
                    !string.IsNullOrEmpty(item.itemName.text))
                    return item.itemName.text;
            }
            if (property != null && !string.IsNullOrEmpty(
                    property.LocalizedName))
                return property.LocalizedName;
            if (inventory != null && data != null &&
                !string.IsNullOrEmpty(data.ID)) {
                string localized = inventory.GetItemLocalizeName(data.ID);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            return data != null ? data.ID ?? string.Empty : string.Empty;
        }

        private static bool HasOwnedPart(ShopListItemData data)
        {
            if (data == null)
                return false;
            ShopListItemDataEx additional = data.AdditionalData;
            return OwnedPartCache.GetConditionBreakdown(
                data.ID,
                additional != null ? additional.ET : 0,
                additional != null ? additional.Profile : 0,
                additional != null ? additional.Size : 0,
                additional != null ? additional.Width : 0).Total > 0;
        }

        private static int CompareEntries(ShoppingListEntry left,
            ShoppingListEntry right, ShoppingListSortType sortType,
            bool ascending)
        {
            int result;
            switch (sortType) {
                case ShoppingListSortType.Availability:
                    result = left.Available.CompareTo(right.Available);
                    break;
                case ShoppingListSortType.Price:
                    result = left.Price.CompareTo(right.Price);
                    break;
                case ShoppingListSortType.Repairability:
                    result = left.Repairable.CompareTo(right.Repairable);
                    break;
                default:
                    result = CompareNames(left, right);
                    if (!ascending)
                        result = -result;
                    return result != 0
                        ? result
                        : left.OriginalIndex.CompareTo(right.OriginalIndex);
            }

            if (!ascending)
                result = -result;
            if (result == 0)
                result = CompareNames(left, right);
            return result != 0
                ? result
                : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static int CompareNames(ShoppingListEntry left,
            ShoppingListEntry right)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(
                left != null ? left.Name : string.Empty,
                right != null ? right.Name : string.Empty);
        }

        private static void RefreshItems(ShopListWindow window)
        {
            if (window == null || FillItemsMethod == null)
                return;
            FillItemsMethod.Invoke(window, null);
            ShoppingListTwoColumnNavigationFeature.RefreshRowsNow(window);
        }

        private static bool CanSort(ShopListWindow window)
        {
            return window != null && window.items != null &&
                window.items.Count > 1;
        }
    }


}
