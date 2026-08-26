using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppCMS;
using Il2CppCMS.Containers;
using Il2CppCMS.Helpers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Helpers;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.Shop;
using Il2CppCMS.UI.Windows;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS;
using CMS.Containers;
using CMS.Helpers;
using CMS.UI;
using CMS.UI.Helpers;
using CMS.UI.Logic;
using CMS.UI.Logic.Shop;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Adds a shop-specific hold-Space action that buys compatible shopping-list
    /// items sold by the current shop after a native confirmation. Prices use the
    /// same wheel and discount helpers as the stock shop purchase flow.
    /// </summary>
    [HarmonyPatch]
    internal static class ShoppingListBulkPurchaseFeature
    {
        private const string FooterWindowId = "ShoppingList";
        private const string BuyAllHintId = "Hint_ShoppingListBuyAll";
        private const float HoldDurationSeconds = 1f;
        private const int AskWindowWaitFrames = 120;

        private sealed class PurchaseEntry
        {
            public ShoppingListBackendEntry BackendEntry;
            public ShopListItemData Data;
            public string PurchaseItemID;
            public int Amount;
            public int UnitPrice;
            public int StackPrice;
        }

        private sealed class PurchasePlan
        {
            public readonly List<PurchaseEntry> Entries =
                new List<PurchaseEntry>();
            public float Discount;
            public int TotalAmount;
            public int TotalCost;
        }

        private static readonly MethodInfo TakenItemsGetDiscountMethod =
            AccessTools.Method(typeof(TakenItemsWindow), "GetDiscount");
        private static readonly PropertyInfo PartPropertyShopNameProperty =
            AccessTools.Property(typeof(PartProperty), "ShopName");
        private static readonly FieldInfo PartPropertyShopNameField =
            AccessTools.Field(typeof(PartProperty), "ShopName");

        private static ShopListWindow activeWindow;
        private static NativeUiFactory.FooterHintHandle buyAllHint;
        private static PurchasePlan pendingPlan;
        private static float spaceHoldStartedAt = -1f;
        private static bool spaceHoldTriggered;
        private static bool confirmationOpen;
        private static bool executionScheduled;

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addShoppingListSorting;
            }
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        private static void ShowPostfix(ShopListWindow __instance, bool __result)
        {
            if (!__result || __instance == null || !IsEnabled)
                return;

            ResetInteractionState();
            activeWindow = __instance;
            UpdateHint();
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Hide))]
        [HarmonyPrefix]
        private static void HidePrefix(ShopListWindow __instance)
        {
            if (__instance != activeWindow)
                return;

            DestroyHint();
            ResetInteractionState();
            activeWindow = null;
        }

        [HarmonyPatch(typeof(ShopListWindow), "HandleInput")]
        [HarmonyPostfix]
        private static void HandleInputPostfix(ShopListWindow __instance)
        {
            if (__instance == null || __instance != activeWindow ||
                confirmationOpen || executionScheduled ||
                !CanBuyAll(__instance)) {
                ResetSpaceHold();
                return;
            }

            if (!Input.GetKey(KeyCode.Space)) {
                ResetSpaceHold();
                return;
            }

            if (spaceHoldTriggered)
                return;

            if (spaceHoldStartedAt < 0f) {
                spaceHoldStartedAt = Time.unscaledTime;
                UpdateHoldVisual(0f);
                return;
            }

            float elapsed = Time.unscaledTime - spaceHoldStartedAt;
            UpdateHoldVisual(Mathf.Clamp01(elapsed / HoldDurationSeconds));
            if (elapsed < HoldDurationSeconds)
                return;

            spaceHoldTriggered = true;
            TryOpenConfirmation();
            Input.ResetInputAxes();
        }

        private static void UpdateHint()
        {
            if (activeWindow == null || activeWindow.uiDescription == null ||
                !CanBuyAll(activeWindow)) {
                DestroyHint();
                return;
            }

            buyAllHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = FooterWindowId,
                    WindowRoot = activeWindow.transform,
                    HintRoot = activeWindow.uiDescription.transform,
                    HintId = BuyAllHintId,
                    Keys = new string[] { "Space" },
                    Text = ModLocalization.Get("LOC_ShoppingListBuyAllAction"),
                    Action = new Action(TryOpenConfirmation),
                    CanHold = true,
                    TimeToHold = HoldDurationSeconds,
                    OnlyHandleMouseClickInput = true,
                    Row = 1,
                    AllowAutomaticRowWrap = false,
                    ExtendFooterBackground = true,
                    HoldSuffixText = ModLocalization.Get("LOC_HoldAction"),
                    Order = 4,
                    Profile = WindowFooterHintController
                        .NativeFooterProfile.Automatic,
                });
        }

        private static void DestroyHint()
        {
            WindowFooterHintController.RemoveHint(FooterWindowId, BuyAllHintId);
            buyAllHint = null;
        }

        private static bool CanBuyAll(ShopListWindow window)
        {
            return IsEnabled && GlobalState.IsGarageSceneActive &&
                window != null &&
                ShoppingListBackend.DisplayCount > 0 &&
                window.isShopActive && window.canSearchInShop &&
                window.shopWindow != null &&
                window.shopWindow.gameObject != null &&
                window.shopWindow.gameObject.activeInHierarchy;
        }

        private static void TryOpenConfirmation()
        {
            if (confirmationOpen || executionScheduled ||
                activeWindow == null || !CanBuyAll(activeWindow))
                return;

            PurchasePlan plan;
            string errorKey;
            if (!TryBuildPlan(activeWindow, out plan, out errorKey)) {
                ShopListWindow window = activeWindow;
                if (string.Equals(errorKey,
                    "LOC_ShoppingListBuyAllNoItemsInShop",
                    StringComparison.Ordinal))
                    CloseShoppingList(window);
                ShowInfo(errorKey);
                return;
            }

            if (GlobalData.PlayerMoney < plan.TotalCost) {
                ShowInfo("LOC_ShoppingListBuyAllInsufficientFunds");
                return;
            }

            UIManager uiManager = UIManager.Get();
            if (uiManager == null) {
                ShowInfo("LOC_ShoppingListBuyAllUnavailable");
                return;
            }

            string description = string.Format(
                ModLocalization.Get("LOC_ShoppingListBuyAllConfirmation"),
                plan.TotalAmount, plan.TotalCost);

            try {
                pendingPlan = plan;
                confirmationOpen = true;
                uiManager.ShowAskWindow(
                    ModLocalization.Get("LOC_ShoppingListBuyAllTitle"),
                    description, new Action<bool>(OnConfirmationResult), true);
            } catch (Exception exception) {
                pendingPlan = null;
                confirmationOpen = false;
                ModLogger.Log("[ShoppingList] Failed to open Buy All confirmation." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
            }
        }

        private static void OnConfirmationResult(bool accepted)
        {
            confirmationOpen = false;
            if (!accepted) {
                ShopListWindow listWindow = activeWindow;
                pendingPlan = null;
                ResetSpaceHold();
                CloseShoppingList(listWindow);
                return;
            }

            if (executionScheduled || pendingPlan == null)
                return;

            ShopListWindow window = activeWindow;
            PurchasePlan plan = pendingPlan;
            pendingPlan = null;
            CloseShoppingList(window);

            executionScheduled = true;
            MelonCoroutines.Start(ExecutePurchaseAfterConfirmation(
                window, plan));
        }

        private static IEnumerator ExecutePurchaseAfterConfirmation(
            ShopListWindow window, PurchasePlan plan)
        {
            int frames = AskWindowWaitFrames;
            while (WindowManager.Instance != null &&
                WindowManager.Instance.IsWindowActive(WindowID.AskWindow) &&
                frames-- > 0)
                yield return null;

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (window == null || plan == null) {
                executionScheduled = false;
                yield break;
            }

            if (GlobalData.PlayerMoney < plan.TotalCost) {
                executionScheduled = false;
                ShowInfo("LOC_ShoppingListBuyAllInsufficientFunds");
                yield break;
            }

            Inventory inventory = Singleton<Inventory>.Instance;
            if (inventory == null) {
                executionScheduled = false;
                ShowInfo("LOC_ShoppingListBuyAllUnavailable");
                yield break;
            }

            int spent = 0;
            bool changed = false;
            bool failed = false;
            try {
                for (int i = 0; i < plan.Entries.Count; i++) {
                    PurchaseEntry entry = plan.Entries[i];
                    int added = AddEntryItems(inventory, entry);
                    if (added <= 0)
                        continue;

                    int entryCost = added >= entry.Amount
                        ? entry.StackPrice
                        : CalculateStackPrice(entry.UnitPrice, added,
                            plan.Discount);
                    spent += entryCost;

                    if (added != entry.Amount) {
                        ModLogger.Log("[ShoppingList] Bulk purchase for '" +
                            entry.Data.ID + "' completed only " + added + "/" +
                            entry.Amount + " item(s); backend entry was kept.",
                            Types.LoggingLevels.Warning);
                        continue;
                    }

                    ShoppingListEntrySnapshot snapshot =
                        entry.BackendEntry != null
                            ? entry.BackendEntry.CreateSnapshot() : null;
                    if (snapshot == null || !ShoppingListBackend.Remove(
                            window, snapshot, false)) {
                        ModLogger.Log("[ShoppingList] Purchased entry could not " +
                            "be removed from backend: '" + entry.Data.ID + "'.",
                            Types.LoggingLevels.Warning);
                        continue;
                    }

                    changed = true;
                }
            } catch (Exception exception) {
                failed = true;
                ModLogger.Log("[ShoppingList] Buy All failed." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
            } finally {
                try {
                    if (spent > 0)
                        GlobalData.AddPlayerMoney(-spent);
                    if (spent > 0 || changed)
                        inventory.Save();
                    if (changed)
                        ShoppingListBackend.PersistState(window);
                    if (changed)
                        ShoppingListPurchaseController.Invalidate();
                    if (spent > 0 || changed)
                        CloseShoppingList(window);
                } catch (Exception exception) {
                    failed = true;
                    ModLogger.Log("[ShoppingList] Buy All finalization failed." +
                        Environment.NewLine + exception,
                        Types.LoggingLevels.Error);
                } finally {
                    if (failed)
                        ShowInfo("LOC_ShoppingListBuyAllUnavailable");
                    executionScheduled = false;
                    ResetSpaceHold();
                }
            }
        }

        private static int AddEntryItems(Inventory inventory,
            PurchaseEntry entry)
        {
            if (inventory == null || entry == null || entry.Data == null ||
                entry.Amount <= 0)
                return 0;

            int added = 0;
            for (int i = 0; i < entry.Amount; i++) {
                try {
                    Item item = CreatePurchasedItem(
                        entry.Data, entry.PurchaseItemID);
                    if (item == null)
                        break;
                    inventory.Add(item, false);
                    added++;
                } catch (Exception exception) {
                    ModLogger.Log("[ShoppingList] Failed to buy '" +
                        entry.Data.ID + "' at item " + (i + 1) + "/" +
                        entry.Amount + "." + Environment.NewLine + exception,
                        Types.LoggingLevels.Error);
                    break;
                }
            }
            return added;
        }

        private static Item CreatePurchasedItem(ShopListItemData data,
            string purchaseItemID)
        {
            if (data == null || string.IsNullOrEmpty(purchaseItemID))
                return null;

            Item item = new Item(purchaseItemID);
            item.Condition = 1f;

            ShopListItemDataEx additional = data.AdditionalData;
            if (additional != null && (additional.Tire || additional.Rim)) {
                var wheelData = item.WheelData;
                wheelData.Size = additional.Size;
                wheelData.Width = additional.Width;
                wheelData.Profile = additional.Profile;
                wheelData.ET = additional.ET;
                item.WheelData = wheelData;
            }
            return item;
        }

        private static bool TryBuildPlan(ShopListWindow window,
            out PurchasePlan plan, out string errorKey)
        {
            plan = null;
            errorKey = "LOC_ShoppingListBuyAllUnavailable";
            if (window == null || ShoppingListBackend.DisplayCount == 0 ||
                window.shopWindow == null) {
                return false;
            }

            GameInventory gameInventory = Singleton<GameInventory>.Instance;
            if (gameInventory == null) {
                return false;
            }

            string currentShopName;
            if (!TryGetCurrentShopName(window, out currentShopName)) {
                return false;
            }

            float discount;
            if (!TryGetCurrentDiscount(out discount)) {
                return false;
            }

            PurchasePlan result = new PurchasePlan { Discount = discount };
            long totalAmount = 0;
            long totalCost = 0;

            List<ShoppingListBackendEntry> displayEntries =
                ShoppingListBackend.GetDisplayEntriesSnapshot();
            for (int i = 0; i < displayEntries.Count; i++) {
                ShoppingListBackendEntry backendEntry = displayEntries[i];
                ShopListItemData data = backendEntry != null
                    ? backendEntry.Data : null;
                if (data == null || string.IsNullOrEmpty(data.ID) ||
                    backendEntry.Amount <= 0) {
                    continue;
                }

                string purchaseItemID = ResolvePurchaseItemIDForShop(
                    gameInventory, data, window.shopWindow.currentShopType,
                    currentShopName);
                if (string.IsNullOrEmpty(purchaseItemID)) {
                    continue;
                }

                ShopListItemDataEx additional = data.AdditionalData;
                if (additional != null && additional.LicensePlate) {
                    continue;
                }

                int unitPrice;
                if (!TryGetUnitPrice(gameInventory, data,
                    purchaseItemID, out unitPrice)) {
                    return false;
                }

                int stackPrice = CalculateStackPrice(unitPrice,
                    backendEntry.Amount, discount);
                totalAmount += backendEntry.Amount;
                totalCost += stackPrice;
                if (totalAmount > int.MaxValue || totalCost > int.MaxValue) {
                    return false;
                }

                result.Entries.Add(new PurchaseEntry {
                    BackendEntry = backendEntry,
                    Data = data,
                    PurchaseItemID = purchaseItemID,
                    Amount = backendEntry.Amount,
                    UnitPrice = unitPrice,
                    StackPrice = stackPrice,
                });
            }

            if (result.Entries.Count == 0) {
                errorKey = "LOC_ShoppingListBuyAllNoItemsInShop";
                return false;
            }

            result.TotalAmount = (int)totalAmount;
            result.TotalCost = (int)totalCost;
            plan = result;
            return true;
        }

        private static bool TryGetCurrentShopName(ShopListWindow window,
            out string shopName)
        {
            shopName = window != null && window.shopWindow != null
                ? ShopHelper.ShopToShopName(window.shopWindow.currentShopType)
                : null;
            return !string.IsNullOrEmpty(shopName);
        }

        private static string ResolvePurchaseItemIDForShop(
            GameInventory inventory, ShopListItemData data, ShopType shopType,
            string currentShopName)
        {
            if (inventory == null || data == null ||
                string.IsNullOrEmpty(data.ID) ||
                string.IsNullOrEmpty(currentShopName))
                return null;

            string purchaseItemID = data.ID;
            if (shopType == ShopType.Tuning &&
                !purchaseItemID.StartsWith("t_", StringComparison.Ordinal)) {
                string tuningItemID = "t_" + purchaseItemID;
                if (!inventory.ExistsInPartProperty(tuningItemID)) {
                    return null;
                }
                purchaseItemID = tuningItemID;
            }

            if (!inventory.ExistsInPartProperty(purchaseItemID)) {
                return null;
            }

            PartProperty property = inventory.GetItemProperty(purchaseItemID);
            string itemShopName = GetPartPropertyShopName(property);
            bool matches = !string.IsNullOrEmpty(itemShopName) &&
                string.Equals(itemShopName, currentShopName,
                    StringComparison.OrdinalIgnoreCase);
            return matches ? purchaseItemID : null;
        }

        private static string GetPartPropertyShopName(PartProperty property)
        {
            if (property == null)
                return null;

            try {
                object value = PartPropertyShopNameProperty != null
                    ? PartPropertyShopNameProperty.GetValue(property, null)
                    : PartPropertyShopNameField != null
                        ? PartPropertyShopNameField.GetValue(property)
                        : null;
                return value != null ? value.ToString() : null;
            } catch {
                return null;
            }
        }

        private static bool TryGetUnitPrice(GameInventory inventory,
            ShopListItemData data, string purchaseItemID,
            out int unitPrice)
        {
            unitPrice = 0;
            if (inventory == null || data == null ||
                string.IsNullOrEmpty(purchaseItemID)) {
                return false;
            }

            ShopListItemDataEx additional = data.AdditionalData;
            try {
                if (additional != null && additional.Tire) {
                    unitPrice = Helper.GetTirePrice(purchaseItemID,
                        additional.Width, additional.Profile,
                        additional.Size);
                    return unitPrice >= 0;
                }

                if (additional != null && additional.Rim) {
                    unitPrice = Helper.GetRimPrice(purchaseItemID,
                        additional.Size, additional.ET);
                    return unitPrice >= 0;
                }

                bool exists = inventory.ExistsInPartProperty(purchaseItemID);
                if (!exists) {
                    return false;
                }
                PartProperty property = inventory.GetItemProperty(purchaseItemID);
                if (property == null) {
                    return false;
                }

                unitPrice = property.Price;
                return unitPrice >= 0;
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Failed to calculate price for '" +
                    purchaseItemID + "'." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                return false;
            }
        }

        private static int CalculateStackPrice(int unitPrice, int amount,
            float discount)
        {
            if (unitPrice <= 0 || amount <= 0)
                return 0;

            float basePrice = (float)unitPrice * amount;
            float discounted = UIHelper.GetDiscountPrice(discount, basePrice);
            return Mathf.Max(0, Mathf.FloorToInt(discounted));
        }

        private static bool TryGetCurrentDiscount(out float discount)
        {
            discount = 0f;

            if (TakenItemsGetDiscountMethod != null) {
                TakenItemsWindow takenItems = FindTakenItemsWindow();
                if (takenItems != null) {
                    try {
                        object value = TakenItemsGetDiscountMethod.Invoke(
                            takenItems, null);
                        discount = Mathf.Clamp01(Convert.ToSingle(value));
                        return true;
                    } catch (Exception exception) {
                        ModLogger.Log("[ShoppingList] Native discount lookup failed." +
                            Environment.NewLine + exception,
                            Types.LoggingLevels.Warning);
                    }
                }
            }

            ShopBuyWindow shopBuy = FindShopBuyWindow();
            if (shopBuy != null && !string.IsNullOrEmpty(shopBuy.itemID)) {
                discount = Mathf.Clamp01(shopBuy.currentDiscount);
                return true;
            }

            if (shopBuy != null && TryInitializeShopBuyDiscount(shopBuy,
                out discount))
                return true;

            return false;
        }

        private static bool TryInitializeShopBuyDiscount(
            ShopBuyWindow shopBuy, out float discount)
        {
            discount = 0f;
            if (shopBuy == null || activeWindow == null ||
                activeWindow.shopWindow == null)
                return false;

            ShopItem candidate = FindShopItemForDiscount(
                activeWindow.shopWindow);
            if (candidate == null) {
                return false;
            }

            try {
                shopBuy.PrepareForItem(candidate,
                    activeWindow.shopWindow.currentShopType);
                discount = Mathf.Clamp01(shopBuy.currentDiscount);
                return !string.IsNullOrEmpty(shopBuy.itemID);
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Failed to initialize native " +
                    "shop discount." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
                return false;
            }
        }

        private static ShopItem FindShopItemForDiscount(ShopWindow shopWindow)
        {
            ShopItem activeChild = null;
            ShopItem activeAny = null;
            ShopItem any = null;

            try {
                Il2CppReferenceArray<UnityEngine.Object> loaded =
                    Resources.FindObjectsOfTypeAll(Il2CppType.Of<ShopItem>());
                foreach (UnityEngine.Object value in loaded) {
                    ShopItem typed = value.TryCast<ShopItem>();
                    if (typed == null || typed.gameObject == null ||
                        string.IsNullOrEmpty(typed.ID))
                        continue;

                    if (any == null)
                        any = typed;

                    bool active = typed.gameObject.activeInHierarchy;
                    if (active && activeAny == null)
                        activeAny = typed;

                    if (active && shopWindow != null &&
                        typed.transform.IsChildOf(shopWindow.transform)) {
                        activeChild = typed;
                        break;
                    }
                }
            } catch {
            }

            ShopItem result = activeChild ?? activeAny ?? any;
            return result;
        }

        private static TakenItemsWindow FindTakenItemsWindow()
        {
            try {
                WindowManager manager = WindowManager.Instance;
                if (manager != null) {
                    TakenItemsWindow registered = manager
                        .GetWindowByID<TakenItemsWindow>(WindowID.TakenItems);
                    if (registered != null)
                        return registered;
                }
            } catch {
            }

            try {
                Il2CppReferenceArray<UnityEngine.Object> loaded =
                    Resources.FindObjectsOfTypeAll(
                        Il2CppType.Of<TakenItemsWindow>());
                foreach (UnityEngine.Object value in loaded) {
                    TakenItemsWindow typed =
                        value.TryCast<TakenItemsWindow>();
                    if (typed != null)
                        return typed;
                }
            } catch {
            }
            return null;
        }

        private static ShopBuyWindow FindShopBuyWindow()
        {
            try {
                Il2CppReferenceArray<UnityEngine.Object> loaded =
                    Resources.FindObjectsOfTypeAll(
                        Il2CppType.Of<ShopBuyWindow>());
                foreach (UnityEngine.Object value in loaded) {
                    ShopBuyWindow typed = value.TryCast<ShopBuyWindow>();
                    if (typed != null)
                        return typed;
                }
            } catch {
            }
            return null;
        }

        private static void ShowInfo(string key)
        {
            UIManager uiManager = UIManager.Get();
            if (uiManager != null)
                uiManager.ShowInfoWindow(ModLocalization.Get(key), true);
        }

        private static void CloseShoppingList(ShopListWindow window)
        {
            if (window == null || window.gameObject == null ||
                !window.gameObject.activeInHierarchy)
                return;

            window.Hide(false);
        }

        private static void ResetInteractionState()
        {
            pendingPlan = null;
            confirmationOpen = false;
            executionScheduled = false;
            ResetSpaceHold();
        }

        private static void ResetSpaceHold()
        {
            spaceHoldStartedAt = -1f;
            spaceHoldTriggered = false;
            UpdateHoldVisual(0f);
        }

        private static void UpdateHoldVisual(float progress)
        {
            if (buyAllHint == null || buyAllHint.Description == null)
                return;

            var description = buyAllHint.Description;
            description.holdTime = Mathf.Clamp01(progress) *
                HoldDurationSeconds;
            if (description.buttonFill != null)
                description.buttonFill.fillAmount = Mathf.Clamp01(progress);
        }

    }
}
