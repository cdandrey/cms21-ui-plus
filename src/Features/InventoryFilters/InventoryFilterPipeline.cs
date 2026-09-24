using System;
using System.Collections.Generic;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Logic.Warehouse;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS;
using CMS.Containers;
using CMS.UI.Logic.Warehouse;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    public static partial class InventoryFilterManager
    {
        private static readonly Dictionary<int, List<BaseItem>> NativeSearchFullOrder =
            new Dictionary<int, List<BaseItem>>();

        public static void PrepareForRedraw(BaseInventory inventory)
        {
            BeginFilterScope(inventory);
        }

        public static void FinishRedraw(BaseInventory inventory)
        {
            EndFilterScope(inventory);
            FilteredWarehouseTransferFeature.OnInventoryRedrawn(inventory);
        }

        public static void PrepareForDraw(BaseInventory inventory)
        {
            BeginFilterScope(inventory);
        }

        public static void FinishDraw(BaseInventory inventory)
        {
            EndFilterScope(inventory);
            EnsureButtons(inventory);
            FilteredWarehouseTransferFeature.OnFilteredItemsChanged(inventory);
        }

        private static void BeginFilterScope(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            int instanceID = inventory.GetInstanceID();
            DrawSnapshot existingSnapshot;
            if (DrawSnapshots.TryGetValue(instanceID, out existingSnapshot)) {
                existingSnapshot.Depth++;
                return;
            }

            if (!ShouldHandleWindow(inventory))
                return;

            ItemsBinding binding = GetItemsBinding(inventory);
            if (binding == null)
                return;

            Il2CppSystem.Collections.Generic.List<BaseItem> original;
            try {
                original = binding.Get(inventory);
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to read " + binding.Name + "." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
                return;
            }

            if (original == null)
                return;

            PartFilterCriteria criteria = IsFeatureEnabled()
                ? CreateCurrentCriteria(inventory) : null;
            bool groupingEnabled = IsInventoryGroupingEnabled() &&
                SupportsInventoryGrouping(inventory);
            if (!groupingEnabled && GetGroupingState(inventory, false) != null)
                ResetInventoryGrouping(inventory);

            Il2CppSystem.Collections.Generic.List<BaseItem> source =
                IncludeSpecialItemsExcludedByNativeSearch(inventory, original);
            bool nativeSearchOverride = source != original;
            if (!groupingEnabled && !nativeSearchOverride &&
                (criteria == null || !criteria.HasAnyFilter)) {
                UpdatePaginationCount(inventory, original.Count);
                return;
            }

            Il2CppSystem.Collections.Generic.List<BaseItem> filtered =
                groupingEnabled
                    ? BuildGroupedDisplay(inventory, source, criteria)
                    : FilterCopy(source, criteria);

            DrawSnapshot snapshot = new DrawSnapshot();
            snapshot.Inventory = inventory;
            snapshot.Binding = binding;
            snapshot.Original = original;
            snapshot.Depth = 1;

            try {
                binding.Set(inventory, filtered);
                UpdatePaginationCount(inventory, filtered.Count);
                DrawSnapshots[instanceID] = snapshot;
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to temporarily replace " +
                    binding.Name + "." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        private static Il2CppSystem.Collections.Generic.List<BaseItem>
            IncludeSpecialItemsExcludedByNativeSearch(BaseInventory inventory,
                Il2CppSystem.Collections.Generic.List<BaseItem> source)
        {
            if (inventory == null || source == null || IsBarnOrJunkyardScene())
                return source;

            InputField searchField = FindSearchField(inventory, true);
            if (searchField == null)
                return source;

            int instanceId = inventory.GetInstanceID();
            if (string.IsNullOrWhiteSpace(searchField.text)) {
                List<BaseItem> fullOrder = new List<BaseItem>(source.Count);
                foreach (BaseItem baseItem in source)
                    fullOrder.Add(baseItem);
                NativeSearchFullOrder[instanceId] = fullOrder;
                return source;
            }

            List<BaseItem> cachedOrder;
            if (!NativeSearchFullOrder.TryGetValue(instanceId, out cachedOrder))
                return source;

            Il2CppSystem.Collections.Generic.List<Item> inventoryItems = null;
            if (inventory.TryCast<WarehouseTab>() != null) {
                var warehouse = GlobalState.GameManager != null
                    ? GlobalState.GameManager.Warehouse : null;
                int index = warehouse != null ? warehouse.SelectedOption : -1;
                if (warehouse != null && warehouse.warehouseList != null &&
                    index >= 0 && index < warehouse.warehouseList.Count)
                    inventoryItems = warehouse.warehouseList[index];
            } else if (inventory.TryCast<InventoryWindow>() != null ||
                inventory.TryCast<WarehouseInventoryTab>() != null) {
                Inventory playerInventory = Singleton<Inventory>.Instance;
                if (playerInventory != null)
                    inventoryItems = playerInventory.items;
            }

            if (inventoryItems == null)
                return source;

            Dictionary<long, BaseItem> visibleItems =
                new Dictionary<long, BaseItem>();
            Dictionary<long, BaseItem> visibleGroups =
                new Dictionary<long, BaseItem>();
            foreach (BaseItem baseItem in source) {
                Item item = baseItem != null ? baseItem.TryCast<Item>() : null;
                if (item != null) {
                    visibleItems[item.UID] = baseItem;
                    continue;
                }

                GroupItem group = baseItem != null
                    ? baseItem.TryCast<GroupItem>() : null;
                if (group != null)
                    visibleGroups[group.UID] = baseItem;
            }

            Dictionary<long, Item> specialItems = new Dictionary<long, Item>();
            foreach (Item item in inventoryItems) {
                if (PartFilterRules.IsSpecialInventoryItem(item))
                    specialItems[item.UID] = item;
            }

            bool hasExcludedSpecialItem = false;
            foreach (long uid in specialItems.Keys) {
                if (!visibleItems.ContainsKey(uid)) {
                    hasExcludedSpecialItem = true;
                    break;
                }
            }
            if (!hasExcludedSpecialItem)
                return source;

            Il2CppSystem.Collections.Generic.List<BaseItem> result =
                new Il2CppSystem.Collections.Generic.List<BaseItem>(
                    source.Count + specialItems.Count);
            HashSet<long> addedItems = new HashSet<long>();
            HashSet<long> addedGroups = new HashSet<long>();

            foreach (BaseItem baseItem in cachedOrder) {
                Item item = baseItem != null ? baseItem.TryCast<Item>() : null;
                if (item != null) {
                    BaseItem visibleItem;
                    if (visibleItems.TryGetValue(item.UID, out visibleItem)) {
                        result.Add(visibleItem);
                        addedItems.Add(item.UID);
                        continue;
                    }

                    Item specialItem;
                    if (specialItems.TryGetValue(item.UID, out specialItem)) {
                        result.Add(specialItem);
                        addedItems.Add(item.UID);
                    }
                    continue;
                }

                GroupItem group = baseItem != null
                    ? baseItem.TryCast<GroupItem>() : null;
                BaseItem visibleGroup;
                if (group != null &&
                    visibleGroups.TryGetValue(group.UID, out visibleGroup)) {
                    result.Add(visibleGroup);
                    addedGroups.Add(group.UID);
                }
            }

            foreach (BaseItem baseItem in source) {
                Item item = baseItem != null ? baseItem.TryCast<Item>() : null;
                if (item != null) {
                    if (addedItems.Add(item.UID))
                        result.Add(baseItem);
                    continue;
                }

                GroupItem group = baseItem != null
                    ? baseItem.TryCast<GroupItem>() : null;
                if (group == null || addedGroups.Add(group.UID))
                    result.Add(baseItem);
            }

            foreach (Item item in specialItems.Values) {
                if (addedItems.Add(item.UID))
                    result.Add(item);
            }

            return result;
        }

        private static PartFilterCriteria CreateCurrentCriteria(
            BaseInventory inventory)
        {
            bool junkyardContext = IsBarnOrJunkyardScene();
            bool expandedPackage = !junkyardContext &&
                IsExpandedInventoryPackage(inventory);
            PartFilterCriteria criteria = new PartFilterCriteria();
            criteria.PreserveSpecialInventoryItems = true;
            criteria.Context = junkyardContext
                ? PartFilterContext.Junkyard
                : PartFilterContext.Garage;
            criteria.GarageConditionMode = garageConditionFilterMode;
            criteria.JunkyardConditionMode = junkyardConditionFilterMode;
            criteria.RepairabilityMode = expandedPackage
                ? RepairabilityQuickFilterMode.Off
                : junkyardContext
                    ? junkyardRepairabilityFilterMode
                    : garageRepairabilityFilterMode;
            RepairSpecializationFilterState repairSpecializations =
                junkyardContext
                    ? junkyardRepairSpecializationFilter
                    : garageRepairSpecializationFilter;
            bool useRepairSpecializations = !expandedPackage &&
                GameplayRepairSkillBridge.RepairSpecializationsEnabled;
            criteria.RequiredRepairSpecializationMask = useRepairSpecializations
                ? repairSpecializations.RequiredMask : 0;
            criteria.ExcludedRepairSpecializationMask = useRepairSpecializations
                ? repairSpecializations.ExcludedMask : 0;
            criteria.QualityMode = junkyardContext
                ? junkyardQualityFilterMode
                : garageQualityFilterMode;
            criteria.OwnedMode = junkyardContext
                ? ownedFilterMode
                : OwnedQuickFilterMode.Off;
            if (junkyardContext)
                criteria.SearchText = GetTravelCollectedSearchText(inventory);
            else {
                // Inventory and warehouse search remains entirely native. The game has
                // already narrowed the list before DrawPage reaches this pipeline.
                criteria.SearchText = string.Empty;
            }
            return criteria;
        }

        private static void EndFilterScope(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            int instanceID = inventory.GetInstanceID();
            DrawSnapshot snapshot;
            if (!DrawSnapshots.TryGetValue(instanceID, out snapshot))
                return;

            snapshot.Depth--;
            if (snapshot.Depth > 0)
                return;

            DrawSnapshots.Remove(instanceID);
            try {
                snapshot.Restore();
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to restore the native inventory list." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }

        private static void ForceRestoreSnapshot(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            int instanceID = inventory.GetInstanceID();
            DrawSnapshot snapshot;
            if (!DrawSnapshots.TryGetValue(instanceID, out snapshot))
                return;

            DrawSnapshots.Remove(instanceID);
            try {
                snapshot.Restore();
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to force-restore the native inventory list." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }

        private static Il2CppSystem.Collections.Generic.List<BaseItem> FilterCopy(
            Il2CppSystem.Collections.Generic.List<BaseItem> source,
            PartFilterCriteria criteria)
        {
            Il2CppSystem.Collections.Generic.List<BaseItem> filtered =
                new Il2CppSystem.Collections.Generic.List<BaseItem>(source.Count);

            foreach (BaseItem baseItem in source) {
                if (PartFilterRules.Matches(baseItem, criteria))
                    filtered.Add(baseItem);
            }

            return filtered;
        }

        internal static int GetCurrentFilteredItemCount(
            BaseInventory inventory)
        {
            if (inventory == null)
                return -1;
            ForceRestoreSnapshot(inventory);
            ItemsBinding binding = GetItemsBinding(inventory);
            if (binding == null)
                return -1;
            try {
                Il2CppSystem.Collections.Generic.List<BaseItem> items =
                    binding.Get(inventory);
                if (items == null)
                    return 0;
                items = IncludeSpecialItemsExcludedByNativeSearch(inventory, items);
                PartFilterCriteria criteria = IsFeatureEnabled()
                    ? CreateCurrentCriteria(inventory) : null;
                if (IsInventoryGroupingEnabled() &&
                    SupportsInventoryGrouping(inventory)) {
                    int expandedCount = GetExpandedFilteredCount(inventory,
                        items, criteria);
                    if (expandedCount >= 0)
                        return expandedCount;
                }
                return criteria != null && criteria.HasAnyFilter
                    ? FilterCopy(items, criteria).Count : items.Count;
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to count the current " +
                    "filtered inventory." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
                return -1;
            }
        }

    }
}
