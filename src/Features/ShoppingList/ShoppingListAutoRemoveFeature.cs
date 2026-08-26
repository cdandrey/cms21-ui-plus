using System;
using System.Reflection;
using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
#else
using CMS;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    /// <summary>Removes or reduces a matching shopping-list entry after a successful purchase.</summary>
    [HarmonyPatch]
    public static class ShoppingListAutoRemoveFeature
    {
        private sealed class PurchaseState
        {
            public bool Enabled;
            public int PlayerMoney;
            public int Amount;
            public string ItemID;
            public string LicensePlateName;
            public bool RemoveFromList;
            public ShoppingListEntrySnapshot PurchasedEntry;
        }

        [HarmonyPatch(typeof(ShopLicenseBuyWindow), nameof(ShopLicenseBuyWindow.BuyItem))]
        [HarmonyPrefix]
        private static void LicenseBuyPrefix(ShopLicenseBuyWindow __instance,
            out PurchaseState __state)
        {
            __state = CaptureLicensePurchase(__instance);
        }

        [HarmonyPatch(typeof(ShopLicenseBuyWindow), nameof(ShopLicenseBuyWindow.BuyItem))]
        [HarmonyPostfix]
        private static void LicenseBuyPostfix(PurchaseState __state)
        {
            if (!WasPurchaseCompleted(__state))
                return;

            try {
                UIManager manager = UIManager.Get();
                ShopListWindow window = manager != null
                    ? manager.ShopListWindow : null;
                ShoppingListBackend.ApplyLicensePurchase(window,
                    __state.ItemID, __state.LicensePlateName, __state.Amount);
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Failed to update licence-plate entry." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
            } finally {
                ShoppingListPurchaseController.Invalidate();
            }
        }

        [HarmonyPatch(typeof(ShopBuyWindow), nameof(ShopBuyWindow.BuyItem))]
        [HarmonyPrefix]
        private static void BuyPrefix(ShopBuyWindow __instance,
            out PurchaseState __state)
        {
            bool controllerEnabled = ShoppingListPurchaseController.IsEnabled;
            bool removeFromList = IsEnabled();
            int amount = __instance != null
                ? WheelShopListPurchaseFeature.GetCurrentPurchaseAmount(
                    __instance) : 0;
            __state = new PurchaseState {
                Enabled = __instance != null &&
                    (controllerEnabled || removeFromList),
                RemoveFromList = removeFromList,
                PlayerMoney = GlobalData.PlayerMoney,
                Amount = amount,
                ItemID = __instance != null ? __instance.itemID : null,
                PurchasedEntry = removeFromList && __instance != null
                    ? WheelShopListPurchaseFeature.CreatePurchasedEntryForPurchase(
                        __instance, amount) : null,
            };
        }

        [HarmonyPatch(typeof(ShopBuyWindow), nameof(ShopBuyWindow.BuyItem))]
        [HarmonyPostfix]
        private static void BuyPostfix(PurchaseState __state)
        {
            if (!WasPurchaseCompleted(__state))
                return;

            try {
                if (__state.RemoveFromList && __state.PurchasedEntry != null) {
                    UIManager manager = UIManager.Get();
                    ShopListWindow window = manager != null
                        ? manager.ShopListWindow : null;
                    ShoppingListBackend.ApplyPurchasedAmount(window,
                        __state.PurchasedEntry, __state.Amount);
                }
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Failed to update purchased entry." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
            } finally {
                ShoppingListPurchaseController.Invalidate();
            }
        }

        private static PurchaseState CaptureLicensePurchase(
            ShopLicenseBuyWindow window)
        {
            PurchaseState state = new PurchaseState {
                Enabled = IsEnabled() && window != null,
                PlayerMoney = GlobalData.PlayerMoney,
                Amount = window != null ? window.currentAmount : 0
            };

            if (!state.Enabled)
                return state;

            state.ItemID = GetStringMember(window, "itemID", "currentItemID");
            state.LicensePlateName = GetStringMember(window,
                "licensePlateName", "currentLicensePlateName", "plateName");
            if (string.IsNullOrEmpty(state.LicensePlateName))
                state.Enabled = false;
            return state;
        }

        private static string GetStringMember(object target, params string[] names)
        {
            if (target == null)
                return null;

            Type type = target.GetType();
            foreach (string name in names) {
                FieldInfo field = type.GetField(name, BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.IgnoreCase);
                if (field != null && field.FieldType == typeof(string))
                    return field.GetValue(target) as string;

                PropertyInfo property = type.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (property != null && property.CanRead &&
                    property.PropertyType == typeof(string) &&
                    property.GetIndexParameters().Length == 0)
                    return property.GetValue(target, null) as string;
            }
            return null;
        }

        private static bool WasPurchaseCompleted(PurchaseState state)
        {
            return state != null && state.Enabled && state.Amount > 0 &&
                GlobalData.PlayerMoney < state.PlayerMoney;
        }

        private static bool IsEnabled()
        {
            return GlobalState.IsGarageSceneActive && Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.removePartsFromShoppingList;
        }

    }
}
