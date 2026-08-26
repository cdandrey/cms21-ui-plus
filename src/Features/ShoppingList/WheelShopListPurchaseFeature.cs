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
using Il2CppCMS.UI.Windows;
#else
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Controls;
using CMS.UI.Logic;
using CMS.UI.Logic.Shop;
using CMS.UI.Logic.Tune;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    internal static class ShoppingListPurchaseController
    {
        private static ShoppingListEntrySnapshot activeEntry;
        private static bool listSearchInProgress;

        internal static bool IsEnabled {
            get {
                if (!GlobalState.IsGarageSceneActive || Main.SettingsEntry == null)
                    return false;

                Settings settings = Main.SettingsEntry.Value;
                return settings != null &&
                    (settings.wheelShopListPurchaseHelper ||
                     settings.removePartsFromShoppingList);
            }
        }

        internal static void Invalidate()
        {
            activeEntry = null;
            listSearchInProgress = false;
        }

        internal static bool BeginListSearch(ShoppingListBackendEntry entry)
        {
            if (!IsEnabled || entry == null)
                return false;

            ShoppingListEntrySnapshot snapshot = entry.CreateSnapshot();
            activeEntry = snapshot != null ? Clone(snapshot) : null;
            listSearchInProgress = activeEntry != null;
            return listSearchInProgress;
        }

        internal static void EndListSearch()
        {
            listSearchInProgress = false;
        }

        internal static void OnManualSearch()
        {
            if (!listSearchInProgress)
                Invalidate();
        }

        internal static void OnShopTransition()
        {
            if (!listSearchInProgress)
                Invalidate();
        }

        internal static bool TryGetEntryForPurchase(ShopBuyWindow window,
            out ShoppingListEntrySnapshot entry)
        {
            entry = null;
            if (!IsEnabled || window == null)
                return false;

            if (activeEntry != null) {
                entry = ShoppingListBackend.FindForPurchase(activeEntry);
                return entry != null;
            }

            UIManager manager = UIManager.Get();
            ShopListWindow listWindow = manager != null
                ? manager.ShopListWindow : null;
            bool wheel = window.type == ShopBuyItemType.Tire ||
                window.type == ShopBuyItemType.Rim;
            entry = ShoppingListBackend.FindForPurchase(
                listWindow, window.itemID, wheel);
            return entry != null;
        }

        internal static ShoppingListEntrySnapshot CreatePurchasedEntry(
            ShopBuyWindow window, int amount)
        {
            if (window == null || string.IsNullOrEmpty(window.itemID) || amount <= 0)
                return null;

            ShoppingListEntrySnapshot selected = activeEntry != null
                ? ShoppingListBackend.FindForPurchase(activeEntry) : null;
            ShoppingListEntrySnapshot entry = new ShoppingListEntrySnapshot {
                ID = window.itemID,
                Name = selected != null ? selected.Name : window.itemID,
                Amount = amount,
                Tire = window.type == ShopBuyItemType.Tire,
                Rim = window.type == ShopBuyItemType.Rim,
            };

            if (entry.Tire) {
                entry.Size = GetOptionAmount(window, 1);
                entry.Width = GetOptionAmount(window, 2);
                entry.Profile = GetOptionAmount(window, 3);
            } else if (entry.Rim) {
                entry.Size = GetOptionAmount(window, 1);
                entry.ET = GetOptionAmount(window, 2);
            }
            return entry;
        }

        private static ShoppingListEntrySnapshot Clone(
            ShoppingListEntrySnapshot source)
        {
            if (source == null)
                return null;

            return new ShoppingListEntrySnapshot {
                ID = source.ID,
                Name = source.Name,
                Amount = source.Amount,
                Tire = source.Tire,
                Rim = source.Rim,
                LicensePlate = source.LicensePlate,
                LicensePlateName = source.LicensePlateName,
                Size = source.Size,
                Width = source.Width,
                Profile = source.Profile,
                ET = source.ET,
            };
        }

        private static int GetOptionAmount(ShopBuyWindow window, int index)
        {
            return window != null && window.shopOptions != null &&
                index >= 0 && index < window.shopOptions.Length &&
                window.shopOptions[index] != null
                    ? window.shopOptions[index].currentAmount : 0;
        }
    }

    /// <summary>
    /// Applies shopping-list quantity and wheel dimensions to the native purchase
    /// window. The purchase controller owns the currently valid list entry.
    /// </summary>
    [HarmonyPatch]
    public static class WheelShopListPurchaseFeature
    {
        private static ShopListItemDataEx lastTireOptions =
            new ShopListItemDataEx();


        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        private static void ShopListWindowShowPostfix(
            ShopListWindow __instance, bool __result)
        {
            if (!__result)
                return;

            ShoppingListPurchaseController.Invalidate();
        }

        [HarmonyPatch(typeof(ShopListWindow), "OnItemClick")]
        [HarmonyPrefix]
        private static bool ShopListItemClickPrefix(ShopListWindow __instance)
        {
            if (__instance == null || !ShoppingListPurchaseController.IsEnabled ||
                !ShoppingListBackend.IsOpen(__instance) ||
                !__instance.isShopActive || !__instance.canSearchInShop ||
                __instance.shopWindow == null) {
                return true;
            }

            ShoppingListBackendEntry entry =
                ShoppingListBackend.GetCurrentSelectedEntry(__instance);
            if (entry == null || string.IsNullOrEmpty(entry.ID) ||
                !ShoppingListPurchaseController.BeginListSearch(entry)) {
                return true;
            }

            string searchText = !string.IsNullOrEmpty(entry.Name)
                ? entry.Name : entry.ID;
            try {
                __instance.shopWindow.SearchForItem(searchText);
            } finally {
                ShoppingListPurchaseController.EndListSearch();
            }
            return false;
        }

        [HarmonyPatch(typeof(PartsShopPage), "SubmitSearchAction")]
        [HarmonyPrefix]
        private static void ManualShopSearchPrefix()
        {
            ShoppingListPurchaseController.OnManualSearch();
        }

        [HarmonyPatch(typeof(ShopWindow), "OpenShop")]
        [HarmonyPrefix]
        private static void OpenShopPrefix()
        {
            ShoppingListPurchaseController.OnShopTransition();
        }

        [HarmonyPatch(typeof(ShopWindow), "OpenLastShop")]
        [HarmonyPrefix]
        private static void OpenLastShopPrefix()
        {
            ShoppingListPurchaseController.OnShopTransition();
        }

        [HarmonyPatch(typeof(ShopWindow), nameof(ShopWindow.Hide))]
        [HarmonyPrefix]
        private static void ShopWindowHidePrefix()
        {
            ShoppingListPurchaseController.OnShopTransition();
        }

        [HarmonyPatch(typeof(ShopBuyWindow), nameof(ShopBuyWindow.Show))]
        [HarmonyPostfix]
        public static void ShopBuyWindowShowPostfix(ShopBuyWindow __instance)
        {
            if (__instance == null || !ShoppingListPurchaseController.IsEnabled)
                return;

            ShoppingListEntrySnapshot entry;
            if (!ShoppingListPurchaseController.TryGetEntryForPurchase(
                    __instance, out entry)) {
                return;
            }

            if (!IsEnabled()) {
                return;
            }

            bool isTire = __instance.type == ShopBuyItemType.Tire;
            bool isRim = __instance.type == ShopBuyItemType.Rim;
            if (isTire)
                PrepareTireOptionLayout(__instance);

            string itemID = entry.ID;
            int amount = entry.Amount;
            MelonCoroutines.Start(ApplySelectedEntryDeferred(
                __instance, itemID, amount, entry, isTire, isRim));
        }

        private static IEnumerator ApplySelectedEntryDeferred(ShopBuyWindow window,
            string itemID, int amount, ShoppingListEntrySnapshot data,
            bool isTire, bool isRim)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (!IsExpectedWindow(window, itemID, isTire, isRim)) {
                yield break;
            }

            if (data != null && (isTire || isRim)) {
                if (isTire)
                    ApplyTireData(window, data);
                else
                    ApplyRimData(window, data);
            }

            ApplyRequestedAmount(window, amount);

            yield return new WaitForFixedUpdate();
            if (!IsExpectedWindow(window, itemID, isTire, isRim)) {
                yield break;
            }
            ApplyRequestedAmount(window, amount);

            yield return new WaitForEndOfFrame();
            if (!IsExpectedWindow(window, itemID, isTire, isRim)) {
                yield break;
            }
            ApplyRequestedAmount(window, amount);
        }

        internal static ShoppingListEntrySnapshot CreatePurchasedEntryForPurchase(
            ShopBuyWindow window, int amount)
        {
            return ShoppingListPurchaseController.CreatePurchasedEntry(
                window, amount);
        }

        internal static int GetCurrentPurchaseAmount(ShopBuyWindow window)
        {
            if (window == null)
                return 0;

            int optionAmount = GetOptionAmount(window, 0);
            return optionAmount > 0 ? optionAmount : window.currentAmount;
        }

        private static int GetOptionAmount(ShopBuyWindow window, int index)
        {
            return window != null && window.shopOptions != null &&
                index >= 0 && index < window.shopOptions.Length &&
                window.shopOptions[index] != null
                    ? window.shopOptions[index].currentAmount : 0;
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
                var sizeElement = window.listNavigationManager.elements[1];
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

        private static void ApplyRequestedAmount(ShopBuyWindow window,
            int requestedAmount)
        {
            if (requestedAmount <= 0 || window.shopOptions == null ||
                window.shopOptions.Length == 0 || window.shopOptions[0] == null) {
                return;
            }

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
        }

        private static void ApplyTireData(ShopBuyWindow window,
            ShoppingListEntrySnapshot data)
        {
            if (window.shopOptions == null || window.shopOptions.Length < 4)
                return;

            lastTireOptions = new ShopListItemDataEx();
            ApplyOptionValue(window, 1, data.Size);
            ApplyOptionValue(window, 2, data.Width);
            ApplyOptionValue(window, 3, data.Profile);
        }

        private static void ApplyRimData(ShopBuyWindow window,
            ShoppingListEntrySnapshot data)
        {
            if (window.shopOptions == null || window.shopOptions.Length < 3)
                return;

            ApplyOptionValue(window, 1, data.Size);
            ApplyOptionValue(window, 2, data.ET);
        }

        private static void ApplyOptionValue(ShopBuyWindow window, int index,
            int value)
        {
            if (window.shopOptions == null || index < 0 ||
                index >= window.shopOptions.Length)
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
