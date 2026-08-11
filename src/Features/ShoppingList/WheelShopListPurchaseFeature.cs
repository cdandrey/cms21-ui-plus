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
}
