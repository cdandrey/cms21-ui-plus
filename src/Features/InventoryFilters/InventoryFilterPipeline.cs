using System;

#if NET6_0_OR_GREATER
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS.Containers;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    public static partial class InventoryFilterManager
    {
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
                ? CreateCurrentCriteria() : null;
            bool groupingEnabled = IsInventoryGroupingEnabled() &&
                SupportsInventoryGrouping(inventory);
            if (!groupingEnabled && GetGroupingState(inventory, false) != null)
                ResetInventoryGrouping(inventory);
            if (!groupingEnabled && (criteria == null || !criteria.HasAnyFilter)) {
                UpdatePaginationCount(inventory, original.Count);
                return;
            }

            Il2CppSystem.Collections.Generic.List<BaseItem> filtered =
                groupingEnabled
                    ? BuildGroupedDisplay(inventory, original, criteria)
                    : FilterCopy(original, criteria);

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

        private static PartFilterCriteria CreateCurrentCriteria()
        {
            bool junkyardContext = IsBarnOrJunkyardScene();
            PartFilterCriteria criteria = new PartFilterCriteria();
            criteria.Context = junkyardContext
                ? PartFilterContext.Junkyard
                : PartFilterContext.Garage;
            criteria.GarageConditionMode = garageConditionFilterMode;
            criteria.JunkyardConditionMode = junkyardConditionFilterMode;
            criteria.RepairabilityMode = junkyardContext
                ? junkyardRepairabilityFilterMode
                : garageRepairabilityFilterMode;
            criteria.QualityMode = junkyardContext
                ? junkyardQualityFilterMode
                : garageQualityFilterMode;
            criteria.OwnedMode = junkyardContext
                ? ownedFilterMode
                : OwnedQuickFilterMode.Off;
            // Inventory and warehouse search remains entirely native. The game has
            // already narrowed the list before DrawPage reaches this pipeline.
            criteria.SearchText = string.Empty;
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
                PartFilterCriteria criteria = IsFeatureEnabled()
                    ? CreateCurrentCriteria() : null;
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
