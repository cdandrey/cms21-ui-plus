using System;
using System.Reflection;
using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.Tune;
using Il2CppCMS.UI.Windows;
#else
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Logic.Tune;
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
            public int ET;
            public int Profile;
            public int Size;
            public int Width;
            public string LicensePlateName;
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
                Il2CppSystem.Collections.Generic.List<ShopListItemData> entries =
                    UIManager.Get().ShopListWindow.items;
                ShopListItemData matchingEntry = null;

                foreach (ShopListItemData entry in entries) {
                    if (entry == null || entry.AdditionalData == null)
                        continue;
                    if (!string.IsNullOrEmpty(__state.ItemID) &&
                        !string.Equals(entry.ID, __state.ItemID,
                            StringComparison.Ordinal))
                        continue;
                    if (!string.Equals(entry.AdditionalData.LicensePlateName,
                        __state.LicensePlateName, StringComparison.Ordinal))
                        continue;

                    matchingEntry = entry;
                    break;
                }

                if (matchingEntry != null)
                    RemovePurchasedAmount(entries, matchingEntry, __state.Amount);
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Failed to update licence-plate entry." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
            }
        }

        [HarmonyPatch(typeof(ShopBuyWindow), nameof(ShopBuyWindow.BuyItem))]
        [HarmonyPrefix]
        private static void BuyPrefix(ShopBuyWindow __instance,
            out PurchaseState __state)
        {
            __state = new PurchaseState {
                Enabled = IsEnabled() && __instance != null,
                PlayerMoney = GlobalData.PlayerMoney,
                Amount = __instance != null ? __instance.currentAmount : 0,
                ItemID = __instance != null ? __instance.itemID : null,
                ET = __instance != null ? __instance.currentET : 0,
                Profile = __instance != null ? __instance.currentProfile : 0,
                Size = __instance != null ? __instance.currentSize : 0,
                Width = __instance != null ? __instance.currentWidth : 0
            };
        }

        [HarmonyPatch(typeof(ShopBuyWindow), nameof(ShopBuyWindow.BuyItem))]
        [HarmonyPostfix]
        private static void BuyPostfix(PurchaseState __state)
        {
            if (!WasPurchaseCompleted(__state))
                return;

            try {
                Il2CppSystem.Collections.Generic.List<ShopListItemData> entries =
                    UIManager.Get().ShopListWindow.items;

                foreach (ShopListItemData entry in entries) {
                    if (entry == null || entry.AdditionalData == null)
                        continue;
                    if (!PartIdentityComparer.MatchesPurchase(
                        __state.ItemID,
                        __state.ET,
                        __state.Profile,
                        __state.Size,
                        __state.Width,
                        entry.ID,
                        entry.AdditionalData.ET,
                        entry.AdditionalData.Profile,
                        entry.AdditionalData.Size,
                        entry.AdditionalData.Width,
                        true))
                        continue;

                    RemovePurchasedAmount(entries, entry, __state.Amount);
                    break;
                }
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Failed to update purchased entry." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
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

        private static void RemovePurchasedAmount(
            Il2CppSystem.Collections.Generic.List<ShopListItemData> entries,
            ShopListItemData entry, int purchasedAmount)
        {
            if (purchasedAmount >= entry.Amount) {
                entries.Remove(entry);
                return;
            }

            for (int i = 0; i < purchasedAmount; i++)
                UIManager.Get().ShopListWindow.RemoveFromShopList(
                    entry.ID, entry.AdditionalData, false);
        }
    }
}
