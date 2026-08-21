using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.UI.Description;
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Logic.Warehouse;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS;
using CMS.Containers;
using CMS.UI.Description;
using CMS.UI.Logic.Warehouse;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Moves every item remaining in the active warehouse tab after native search,
    /// category selection and the mod's quick filters have been applied.
    /// </summary>
    [HarmonyPatch]
    public static class FilteredWarehouseTransferFeature
    {
        private static WarehouseWindow activeWindow;
        private static bool bulkMoveInProgress;
        private static NativeUiFactory.FooterHintHandle hint;
        private static int handledFrame = -1;

        private static bool IsEnabled {
            get {
                return GlobalState.IsGarageSceneActive && Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.moveFilteredPartsBetweenInventoryAndWarehouse;
            }
        }

        internal static void TryHandleKeyboardShortcut()
        {
            if (!KeyBindingsConfig.IsFilteredTransferActionPressed() ||
                !KeyBindingsConfig.IsFilteredTransferModifierPressed() ||
                !IsEnabled || bulkMoveInProgress || activeWindow == null ||
                !activeWindow.gameObject.activeInHierarchy ||
                handledFrame == Time.frameCount)
                return;

            TryInvokeActiveMoveAction();
        }

        private static void OnHintAction()
        {
            if (!IsEnabled || bulkMoveInProgress)
                return;
            TryInvokeActiveMoveAction();
        }

        private static bool TryInvokeActiveMoveAction()
        {
            if (activeWindow == null ||
                !activeWindow.gameObject.activeInHierarchy)
                return false;

            BaseInventory inventory =
                InventoryFilterManager.GetActiveWarehouseInventory(activeWindow);
            if (inventory == null || IsSearchFieldFocused(inventory))
                return false;

            if (inventory.TryCast<WarehouseInventoryTab>() != null)
                return TryMoveFilteredItems(true);
            if (inventory.TryCast<WarehouseTab>() != null)
                return TryMoveFilteredItems(false);
            return false;
        }

        public static void Reset()
        {
            activeWindow = null;
            bulkMoveInProgress = false;
            handledFrame = -1;
            ClearHint();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Show))]
        [HarmonyPostfix]
        private static void WarehouseWindowShowPostfix(WarehouseWindow __instance)
        {
            activeWindow = __instance;
            ShowHint();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.Hide))]
        [HarmonyPostfix]
        private static void WarehouseWindowHidePostfix(WarehouseWindow __instance)
        {
            if (activeWindow == __instance) {
                activeWindow = null;
                bulkMoveInProgress = false;
            }
            ClearHint();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchTab))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchTabPostfix(WarehouseWindow __instance)
        {
            activeWindow = __instance;
            ShowHint();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToInventory))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchToInventoryPostfix(WarehouseWindow __instance)
        {
            activeWindow = __instance;
            ShowHint();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchWarehouseTabAction))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchWarehouseTabActionPostfix(
            WarehouseWindow __instance)
        {
            activeWindow = __instance;
            ShowHint();
        }

        [HarmonyPatch(typeof(WarehouseWindow), nameof(WarehouseWindow.SwitchToWarehouse))]
        [HarmonyPostfix]
        private static void WarehouseWindowSwitchToWarehousePostfix(WarehouseWindow __instance)
        {
            activeWindow = __instance;
            ShowHint();
        }

        [HarmonyPatch(typeof(NotificationCenter), "MoveItem",
            new Type[] { typeof(Item), typeof(bool), typeof(string) })]
        [HarmonyPrefix]
        private static bool MoveItemPrefix(NotificationCenter __instance,
            Item __0, bool __1, string __2)
        {
            return HandleNativeMove(__1);
        }

        [HarmonyPatch(typeof(NotificationCenter), "MoveItem",
            new Type[] { typeof(Item), typeof(bool), typeof(string) })]
        [HarmonyPostfix]
        private static void MoveItemPostfix()
        {
            UpdateHintAfterMove();
        }

        [HarmonyPatch(typeof(NotificationCenter), "MoveItem",
            new Type[] { typeof(GroupItem), typeof(bool), typeof(string) })]
        [HarmonyPrefix]
        private static bool MoveGroupPrefix(NotificationCenter __instance,
            GroupItem __0, bool __1, string __2)
        {
            return HandleNativeMove(__1);
        }

        [HarmonyPatch(typeof(NotificationCenter), "MoveItem",
            new Type[] { typeof(GroupItem), typeof(bool), typeof(string) })]
        [HarmonyPostfix]
        private static void MoveGroupPostfix()
        {
            UpdateHintAfterMove();
        }

        private static bool HandleNativeMove(bool toWarehouse)
        {
            if (bulkMoveInProgress || !IsEnabled || activeWindow == null ||
                !KeyBindingsConfig.IsFilteredTransferModifierPressed())
                return true;

            BaseInventory inventory =
                InventoryFilterManager.GetActiveWarehouseInventory(activeWindow);
            if (inventory == null || IsSearchFieldFocused(inventory))
                return true;

            if (handledFrame == Time.frameCount)
                return false;

            handledFrame = Time.frameCount;
            return !TryMoveFilteredItems(toWarehouse);
        }

        private static bool TryMoveFilteredItems(bool toWarehouse)
        {
            if (activeWindow == null || bulkMoveInProgress)
                return false;

            List<BaseItem> candidates =
                InventoryFilterManager.GetFilteredWarehouseTransferCandidates(
                    activeWindow);
            if (candidates.Count == 0)
                return false;

            Inventory inventory = Singleton<Inventory>.Instance;
            Warehouse warehouse = GlobalState.GameManager != null
                ? GlobalState.GameManager.Warehouse : null;
            if (inventory == null || warehouse == null ||
                inventory.items == null || inventory.groups == null ||
                warehouse.warehouseList == null ||
                warehouse.warehouseGroupList == null)
                return false;

            int warehouseIndex = warehouse.SelectedOption;
            if (warehouseIndex < 0 ||
                warehouseIndex >= warehouse.warehouseList.Count ||
                warehouseIndex >= warehouse.warehouseGroupList.Count) {
                ModLogger.Log(
                    "[FilteredWarehouseTransfer] Invalid selected warehouse index: " +
                    warehouseIndex + ".", Types.LoggingLevels.Warning);
                return false;
            }

            Il2CppSystem.Collections.Generic.List<Item> warehouseItems =
                warehouse.warehouseList[warehouseIndex];
            Il2CppSystem.Collections.Generic.List<GroupItem> warehouseGroups =
                warehouse.warehouseGroupList[warehouseIndex];
            if (warehouseItems == null || warehouseGroups == null)
                return false;

            HashSet<long> itemUids = new HashSet<long>();
            HashSet<long> groupUids = new HashSet<long>();
            for (int i = 0; i < candidates.Count; i++) {
                BaseItem baseItem = candidates[i];
                Item item = baseItem != null
                    ? baseItem.TryCast<Item>() : null;
                if (item != null) {
                    itemUids.Add(item.UID);
                    continue;
                }

                GroupItem group = baseItem != null
                    ? baseItem.TryCast<GroupItem>() : null;
                if (group != null)
                    groupUids.Add(group.UID);
            }

            int moved = 0;
            bool mutationStarted = false;
            bulkMoveInProgress = true;
            try {
                Il2CppSystem.Collections.Generic.List<Item> sourceItems =
                    toWarehouse ? inventory.items : warehouseItems;
                Il2CppSystem.Collections.Generic.List<Item> destinationItems =
                    toWarehouse ? warehouseItems : inventory.items;
                Il2CppSystem.Collections.Generic.List<GroupItem> sourceGroups =
                    toWarehouse ? inventory.groups : warehouseGroups;
                Il2CppSystem.Collections.Generic.List<GroupItem> destinationGroups =
                    toWarehouse ? warehouseGroups : inventory.groups;

                Il2CppSystem.Collections.Generic.List<Item> retainedItems =
                    new Il2CppSystem.Collections.Generic.List<Item>();
                Il2CppSystem.Collections.Generic.List<Item> movedItems =
                    new Il2CppSystem.Collections.Generic.List<Item>();
                foreach (Item item in sourceItems) {
                    if (item != null && itemUids.Contains(item.UID))
                        movedItems.Add(item);
                    else
                        retainedItems.Add(item);
                }

                Il2CppSystem.Collections.Generic.List<GroupItem> retainedGroups =
                    new Il2CppSystem.Collections.Generic.List<GroupItem>();
                Il2CppSystem.Collections.Generic.List<GroupItem> movedGroups =
                    new Il2CppSystem.Collections.Generic.List<GroupItem>();
                foreach (GroupItem group in sourceGroups) {
                    if (group != null && groupUids.Contains(group.UID))
                        movedGroups.Add(group);
                    else
                        retainedGroups.Add(group);
                }

                mutationStarted = true;
                sourceItems.Clear();
                foreach (Item item in retainedItems)
                    sourceItems.Add(item);
                sourceGroups.Clear();
                foreach (GroupItem group in retainedGroups)
                    sourceGroups.Add(group);

                foreach (Item item in movedItems) {
                    item.MakeNewUID();
                    destinationItems.Add(item);
                    moved++;
                }
                foreach (GroupItem group in movedGroups) {
                    group.MakeNewUID();
                    destinationGroups.Add(group);
                    moved++;
                }

            } catch (Exception exception) {
                ModLogger.Log(
                    "[FilteredWarehouseTransfer] Bulk move failed." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            } finally {
                bulkMoveInProgress = false;
            }

            if (mutationStarted) {
                try {
                    if (activeWindow != null &&
                        activeWindow.gameObject.activeInHierarchy)
                        activeWindow.Refresh(true);
                } catch (Exception exception) {
                    ModLogger.Log(
                        "[FilteredWarehouseTransfer] Window refresh failed." +
                        Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                }

                try {
                    OwnedPartCache.Refresh();
                } catch (Exception exception) {
                    ModLogger.Log(
                        "[FilteredWarehouseTransfer] Owned-parts cache refresh failed." +
                        Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                }
            }

            UpdateHintAfterMove();
            return moved > 0 || mutationStarted;
        }

        private static bool IsSearchFieldFocused(BaseInventory inventory)
        {
            if (inventory == null)
                return false;

            WarehouseWindow parent = inventory.GetComponentInParent<WarehouseWindow>();
            Transform root = parent != null ? parent.transform : inventory.transform;
            foreach (InputField field in root.GetComponentsInChildren<InputField>(true)) {
                if (field != null && field.gameObject.activeInHierarchy && field.isFocused)
                    return true;
            }

            return false;
        }

        private static void ShowHint()
        {
            if (!IsEnabled || activeWindow == null ||
                activeWindow.uiDescription == null) {
                ClearHint();
                return;
            }

            int currentCount = GetActiveItemCount();
            if (currentCount < 0)
                return;
            if (currentCount == 0) {
                ClearHint();
                return;
            }
            CreateNativeHint(currentCount);
        }

        private static void UpdateHintAfterMove()
        {
            if (activeWindow == null ||
                !activeWindow.gameObject.activeInHierarchy)
                return;
            if (!IsEnabled)
                return;
            ShowHint();
        }

        internal static void OnInventoryRedrawn(BaseInventory inventory)
        {
            if (!IsEnabled || inventory == null || activeWindow == null ||
                !activeWindow.gameObject.activeInHierarchy)
                return;
            int pagesCount;
            if (hint != null && hint.Root != null &&
                (!InventoryFilterManager.TryGetPaginationPageCount(
                    inventory, out pagesCount) || pagesCount > 0))
                return;
            OnFilteredItemsChanged(inventory);
        }

        internal static void OnFilteredItemsChanged(BaseInventory inventory)
        {
            if (inventory == null || activeWindow == null ||
                !activeWindow.gameObject.activeInHierarchy)
                return;

            BaseInventory activeInventory = InventoryFilterManager
                .GetActiveWarehouseInventory(activeWindow);
            if (activeInventory == null ||
                activeInventory.GetInstanceID() != inventory.GetInstanceID())
                return;

            ShowHint();
        }

        private static int GetActiveItemCount()
        {
            if (activeWindow == null)
                return -1;
            return InventoryFilterManager.GetActiveWarehouseItemCount(
                activeWindow);
        }

        private static void CreateNativeHint(int itemCount)
        {
            if (activeWindow == null || activeWindow.uiDescription == null)
                return;

            BaseInventory activeInventory = InventoryFilterManager
                .GetActiveWarehouseInventory(activeWindow);
            bool inventoryTab = activeInventory != null &&
                activeInventory.TryCast<WarehouseInventoryTab>() != null;
            WindowFooterHintController.NativeFooterProfile footerProfile =
                inventoryTab
                    ? WindowFooterHintController.NativeFooterProfile
                        .WarehouseInventoryPopulated
                    : WindowFooterHintController.NativeFooterProfile
                        .WarehouseStoragePopulated;

            string modifierKey = GetConfiguredKeyLabel(
                KeyBindingsConfig.FilteredTransferModifierPrimary,
                KeyBindingsConfig.FilteredTransferModifierSecondary);
            string actionKey = GetConfiguredKeyLabel(
                KeyBindingsConfig.FilteredTransferActionPrimary,
                KeyBindingsConfig.FilteredTransferActionSecondary);

            hint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = "Warehouse",
                    WindowRoot = activeWindow.transform,
                    HintRoot = activeWindow.uiDescription.transform,
                    HintId = "Hint_FilteredTransfer",
                    Keys = new string[] { modifierKey, actionKey },
                    Text = ModLocalization.Get("LOC_MoveFilteredAction"),
                    Action = new Action(OnHintAction),
                    Row = 0,
                    Order = 0,
                    Profile = footerProfile,
                    ItemCount = itemCount,
                });
        }

        private static string GetConfiguredKeyLabel(KeyCode primary,
            KeyCode secondary)
        {
            KeyCode key = primary != KeyCode.None ? primary : secondary;
            if (key == KeyCode.LeftShift)
                return "Shift L";
            if (key == KeyCode.RightShift)
                return "Shift R";
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                return "Enter";
            return key != KeyCode.None ? key.ToString() : "None";
        }

        private static void ClearHint()
        {
            WindowFooterHintController.RemoveHint("Warehouse",
                "Hint_FilteredTransfer");
            hint = null;
        }
    }

    public static partial class InventoryFilterManager
    {
        internal static BaseInventory GetActiveWarehouseInventory(WarehouseWindow window)
        {
            if (window == null)
                return null;

            foreach (BaseInventory inventory in
                window.GetComponentsInChildren<BaseInventory>(true)) {
                if (inventory != null && inventory.gameObject.activeInHierarchy)
                    return inventory;
            }

            return null;
        }

        internal static List<BaseItem> GetFilteredWarehouseTransferCandidates(
            WarehouseWindow window)
        {
            List<BaseItem> result = new List<BaseItem>();
            BaseInventory inventory = GetActiveWarehouseInventory(window);
            if (inventory == null)
                return result;

            ForceRestoreSnapshot(inventory);
            ItemsBinding binding = GetItemsBinding(inventory);
            if (binding == null)
                return result;

            Il2CppSystem.Collections.Generic.List<BaseItem> source;
            try {
                source = binding.Get(inventory);
            } catch (Exception exception) {
                ModLogger.Log("[FilteredWarehouseTransfer] Failed to read active tab items." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
                return result;
            }

            if (source == null)
                return result;

            PartFilterCriteria criteria = IsFeatureEnabled()
                ? CreateCurrentCriteria()
                : null;

            foreach (BaseItem baseItem in source) {
                if (criteria == null || !criteria.HasAnyFilter ||
                    PartFilterRules.Matches(baseItem, criteria))
                    result.Add(baseItem);
            }

            return result;
        }

        internal static int GetActiveWarehouseItemCount(
            WarehouseWindow window)
        {
            BaseInventory inventory = GetActiveWarehouseInventory(window);
            if (inventory == null)
                return -1;
            return GetCurrentFilteredItemCount(inventory);
        }

        internal static bool TryGetPaginationPageCount(
            BaseInventory inventory, out int pagesCount)
        {
            return TryGetIntMember(inventory, "pagesCount", out pagesCount);
        }
    }
}
