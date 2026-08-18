using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.SceneLoaders;
#else
using CMS;
using CMS.Containers;
using CMS.UI.Windows;
using CMS.SceneLoaders;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Cached owned-part counts across inventory and all unlocked warehouses.
    /// The full condition breakdown is retained for row indicators, while owned/missing
    /// filters continue to treat copies at 50% condition or higher as usable.
    /// </summary>
    public static class OwnedPartCache
    {
        public const float MinimumCondition = 0.50f;
        private const float PerfectCondition = 1.00f;
        private const int MaximumGarageLoaderWaitFrames = 600;

        public struct ConditionBreakdown
        {
            public int Total;
            public int Perfect;
            public int Condition50To99;
            public int Condition15To49;
            public int ConditionBelow15;

            public int UsableCount {
                get { return Perfect + Condition50To99; }
            }

            internal void Adjust(float condition, int delta)
            {
                Total += delta;
                if (condition >= PerfectCondition)
                    Perfect += delta;
                else if (condition >= MinimumCondition)
                    Condition50To99 += delta;
                else if (condition >= GlobalData.JunkCondition)
                    Condition15To49 += delta;
                else
                    ConditionBelow15 += delta;
            }

            internal void Add(ConditionBreakdown other)
            {
                Total += other.Total;
                Perfect += other.Perfect;
                Condition50To99 += other.Condition50To99;
                Condition15To49 += other.Condition15To49;
                ConditionBelow15 += other.ConditionBelow15;
            }
        }

        private sealed class WheelVariantCount
        {
            public int ET;
            public int Profile;
            public int Size;
            public int Width;
            public ConditionBreakdown Breakdown;

            public bool HasDimensions(int et, int profile, int size, int width)
            {
                return ET == et && Profile == profile && Size == size &&
                    Width == width;
            }

            public bool Matches(int et, int profile, int size, int width)
            {
                return (et == 0 || et == ET) &&
                    (profile == 0 || profile == Profile) &&
                    (size == 0 || size == Size) &&
                    (width == 0 || width == Width);
            }
        }

        private static readonly Dictionary<string, ConditionBreakdown> Counts =
            new Dictionary<string, ConditionBreakdown>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ConditionBreakdown> GroupCounts =
            new Dictionary<string, ConditionBreakdown>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<WheelVariantCount>>
            WheelCountsByItemID =
                new Dictionary<string, List<WheelVariantCount>>(
                    StringComparer.Ordinal);
        private static bool refreshScheduled;

        public static bool IsRequiredByConfiguration()
        {
            if (Main.SettingsEntry == null)
                return false;

            Settings settings = Main.SettingsEntry.Value;
            return settings.showOwnedPartCountIndicators ||
                settings.addInventoryQuickFilters;
        }

        public static void BeginRefreshAfterGarageLoad()
        {
            if (!IsRequiredByConfiguration())
                return;

            MelonCoroutines.Start(RefreshAfterGarageLoad());
        }

        private static IEnumerator RefreshAfterGarageLoad()
        {
            GarageLoader garageLoader = null;
            int waitedFrames = 0;
            while (garageLoader == null &&
                waitedFrames < MaximumGarageLoaderWaitFrames) {
                if (!GlobalState.IsGarageSceneActive)
                    yield break;
                garageLoader =
                    UnityEngine.Object.FindObjectOfType<GarageLoader>();
                if (garageLoader == null) {
                    waitedFrames++;
                    yield return new WaitForFixedUpdate();
                }
            }

            while (garageLoader != null && !garageLoader.isReady &&
                waitedFrames < MaximumGarageLoaderWaitFrames) {
                if (!GlobalState.IsGarageSceneActive)
                    yield break;
                waitedFrames++;
                yield return new WaitForFixedUpdate();
            }

            if (!GlobalState.IsGarageSceneActive)
                yield break;
            if (garageLoader == null || !garageLoader.isReady) {
                ModLogger.Log("[OwnedParts] Garage loader did not become ready " +
                    "within the wait limit.", Types.LoggingLevels.Warning);
                yield break;
            }

            yield return new WaitForSeconds(1f);
            if (!GlobalState.IsGarageSceneActive)
                yield break;

            for (int attempt = 1; attempt <= 5; attempt++) {
                if (Refresh())
                    yield break;

                yield return new WaitForSeconds(1f);
                if (!GlobalState.IsGarageSceneActive)
                    yield break;
            }

            ModLogger.Log("[OwnedParts] Cache was not initialized after garage load.",
                Types.LoggingLevels.Error);
        }

        public static bool Refresh()
        {
            if (!GlobalState.IsGarageSceneActive)
                return false;

            Inventory inventory = Singleton<Inventory>.Instance;
            Warehouse warehouse = GlobalState.GameManager != null
                ? GlobalState.GameManager.Warehouse
                : null;

            if (inventory == null || inventory.items == null || warehouse == null)
                return false;

            Dictionary<string, ConditionBreakdown> refreshed =
                new Dictionary<string, ConditionBreakdown>(StringComparer.Ordinal);
            Dictionary<string, ConditionBreakdown> refreshedGroupCounts =
                new Dictionary<string, ConditionBreakdown>(StringComparer.Ordinal);
            Dictionary<string, List<WheelVariantCount>> refreshedWheelCounts =
                new Dictionary<string, List<WheelVariantCount>>(
                    StringComparer.Ordinal);

            foreach (Item item in inventory.items)
                AddTo(refreshed, refreshedWheelCounts, item);

            if (inventory.groups != null) {
                foreach (GroupItem group in inventory.groups) {
                    if (group == null || group.ItemList == null)
                        continue;

                    AddGroupTo(refreshedGroupCounts, group);
                }
            }

            try {
                int unlockedWarehouseCount =
                    Math.Max(0, Warehouse.amountOfUnlockedWarehouses);
                var warehouseItemsByIndex = warehouse.warehouseList;
                var warehouseGroupsByIndex = warehouse.warehouseGroupList;

                for (int warehouseID = 0;
                    warehouseID < unlockedWarehouseCount;
                    warehouseID++) {
                    if (warehouseItemsByIndex != null &&
                        warehouseID < warehouseItemsByIndex.Count) {
                        var warehouseItems = warehouseItemsByIndex[warehouseID];
                        if (warehouseItems != null) {
                            foreach (Item item in warehouseItems) {
                                if (item == null)
                                    continue;

                                AddTo(refreshed, refreshedWheelCounts, item);
                            }
                        }
                    }

                    if (warehouseGroupsByIndex == null ||
                        warehouseID >= warehouseGroupsByIndex.Count)
                        continue;

                    var warehouseGroups = warehouseGroupsByIndex[warehouseID];
                    if (warehouseGroups == null)
                        continue;

                    foreach (GroupItem group in warehouseGroups) {
                        if (group == null || group.ItemList == null)
                            continue;

                        AddGroupTo(refreshedGroupCounts, group);
                    }
                }
            } catch (Exception exception) {
                ModLogger.Log("[OwnedParts] Cache refresh failed while reading warehouses." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
                return false;
            }

            Counts.Clear();
            foreach (KeyValuePair<string, ConditionBreakdown> entry in refreshed)
                Counts.Add(entry.Key, entry.Value);

            GroupCounts.Clear();
            foreach (KeyValuePair<string, ConditionBreakdown> entry in
                refreshedGroupCounts)
                GroupCounts.Add(entry.Key, entry.Value);

            WheelCountsByItemID.Clear();
            foreach (KeyValuePair<string, List<WheelVariantCount>> entry in
                refreshedWheelCounts)
                WheelCountsByItemID.Add(entry.Key, entry.Value);

            return true;
        }

        public static int GetCount(Item item)
        {
            return GetConditionBreakdown(item).UsableCount;
        }

        public static int GetCount(GroupItem group)
        {
            return GetConditionBreakdown(group).UsableCount;
        }

        public static int GetCount(string itemID, int et, int profile,
            int size, int width)
        {
            return GetConditionBreakdown(itemID, et, profile, size, width)
                .UsableCount;
        }

        public static ConditionBreakdown GetConditionBreakdown(Item item)
        {
            return item == null
                ? default(ConditionBreakdown)
                : GetConditionBreakdownByKey(Counts,
                    PartIdentityComparer.GetKey(item));
        }

        public static ConditionBreakdown GetConditionBreakdown(GroupItem group)
        {
            return group == null
                ? default(ConditionBreakdown)
                : GetConditionBreakdownByKey(GroupCounts,
                    PartIdentityComparer.GetKey(group));
        }

        public static ConditionBreakdown GetConditionBreakdown(string itemID,
            int et, int profile, int size, int width)
        {
            if (string.IsNullOrEmpty(itemID))
                return default(ConditionBreakdown);
            if (!PartIdentityComparer.HasWheelParameters(itemID))
                return GetConditionBreakdownByKey(Counts, itemID);

            List<WheelVariantCount> variants;
            if (!WheelCountsByItemID.TryGetValue(itemID, out variants))
                return default(ConditionBreakdown);

            ConditionBreakdown total = default(ConditionBreakdown);
            foreach (WheelVariantCount variant in variants) {
                if (variant.Matches(et, profile, size, width))
                    total.Add(variant.Breakdown);
            }
            return total;
        }

        public static bool Has(Item item)
        {
            return GetCount(item) > 0;
        }

        public static bool Has(GroupItem group)
        {
            return GetCount(group) > 0;
        }

        public static void NotifyItemAdded(Item item)
        {
            if (IsRequiredByConfiguration())
                NotifyChanged(item, 1);
        }

        public static void NotifyItemRemoved(Item item)
        {
            if (IsRequiredByConfiguration())
                NotifyChanged(item, -1);
        }

        public static void NotifyConditionChanged()
        {
            if (IsRequiredByConfiguration() && GlobalState.IsGarageSceneActive)
                ScheduleRefresh();
        }

        public static void NotifyGroupAdded(GroupItem group)
        {
            NotifyGroupChanged(group, 1);
        }

        public static void NotifyGroupRemoved(GroupItem group)
        {
            NotifyGroupChanged(group, -1);
        }

        public static void NotifyGroupCollectionChanged()
        {
            if (IsRequiredByConfiguration() && GlobalState.IsGarageSceneActive)
                ScheduleRefresh();
        }

        private static void NotifyGroupChanged(GroupItem group, int delta)
        {
            if (!IsRequiredByConfiguration() || group == null ||
                group.ItemList == null)
                return;

            if (GlobalState.IsGarageSceneActive) {
                ScheduleRefresh();
                return;
            }

            Adjust(group, delta);
        }

        private static ConditionBreakdown GetConditionBreakdownByKey(
            Dictionary<string, ConditionBreakdown> source, string key)
        {
            if (string.IsNullOrEmpty(key))
                return default(ConditionBreakdown);

            ConditionBreakdown breakdown;
            return source.TryGetValue(key, out breakdown)
                ? breakdown
                : default(ConditionBreakdown);
        }

        private static void AddTo(
            Dictionary<string, ConditionBreakdown> target,
            Dictionary<string, List<WheelVariantCount>> wheelTarget, Item item)
        {
            if (item == null)
                return;

            string key = PartIdentityComparer.GetKey(item);
            if (string.IsNullOrEmpty(key))
                return;

            ConditionBreakdown breakdown;
            target.TryGetValue(key, out breakdown);
            breakdown.Adjust(item.ConditionToShow, 1);
            target[key] = breakdown;
            AdjustWheelIndex(wheelTarget, item, 1);
        }

        private static void AddGroupTo(
            Dictionary<string, ConditionBreakdown> target, GroupItem group)
        {
            if (group == null || group.ItemList == null ||
                group.ItemList.Count == 0)
                return;

            string key = PartIdentityComparer.GetKey(group);
            if (string.IsNullOrEmpty(key))
                return;

            ConditionBreakdown breakdown;
            target.TryGetValue(key, out breakdown);
            breakdown.Adjust(group.GetCondition(), 1);
            target[key] = breakdown;
        }

        private static void NotifyChanged(Item item, int delta)
        {
            if (item == null)
                return;

            if (GlobalState.IsGarageSceneActive) {
                ScheduleRefresh();
                return;
            }

            Adjust(item, delta);
        }

        private static void Adjust(Item item, int delta)
        {
            if (item == null)
                return;

            string key = PartIdentityComparer.GetKey(item);
            if (string.IsNullOrEmpty(key))
                return;

            ConditionBreakdown breakdown;
            Counts.TryGetValue(key, out breakdown);
            breakdown.Adjust(item.ConditionToShow, delta);

            if (breakdown.Total > 0)
                Counts[key] = breakdown;
            else
                Counts.Remove(key);

            AdjustWheelIndex(WheelCountsByItemID, item, delta);
        }

        private static void Adjust(GroupItem group, int delta)
        {
            if (group == null || group.ItemList == null ||
                group.ItemList.Count == 0)
                return;

            string key = PartIdentityComparer.GetKey(group);
            if (string.IsNullOrEmpty(key))
                return;

            ConditionBreakdown breakdown;
            GroupCounts.TryGetValue(key, out breakdown);
            breakdown.Adjust(group.GetCondition(), delta);

            if (breakdown.Total > 0)
                GroupCounts[key] = breakdown;
            else
                GroupCounts.Remove(key);
        }

        private static void AdjustWheelIndex(
            Dictionary<string, List<WheelVariantCount>> target, Item item,
            int delta)
        {
            if (item == null || !PartIdentityComparer.HasWheelParameters(item.ID))
                return;

            List<WheelVariantCount> variants;
            if (!target.TryGetValue(item.ID, out variants)) {
                if (delta <= 0)
                    return;
                variants = new List<WheelVariantCount>();
                target[item.ID] = variants;
            }

            int et = item.WheelData.ET;
            int profile = item.WheelData.Profile;
            int size = item.WheelData.Size;
            int width = item.WheelData.Width;
            for (int i = 0; i < variants.Count; i++) {
                WheelVariantCount variant = variants[i];
                if (!variant.HasDimensions(et, profile, size, width))
                    continue;

                ConditionBreakdown breakdown = variant.Breakdown;
                breakdown.Adjust(item.ConditionToShow, delta);
                variant.Breakdown = breakdown;
                if (breakdown.Total <= 0) {
                    variants.RemoveAt(i);
                    if (variants.Count == 0)
                        target.Remove(item.ID);
                }
                return;
            }

            if (delta > 0) {
                ConditionBreakdown breakdown = default(ConditionBreakdown);
                breakdown.Adjust(item.ConditionToShow, delta);
                variants.Add(new WheelVariantCount {
                    ET = et,
                    Profile = profile,
                    Size = size,
                    Width = width,
                    Breakdown = breakdown
                });
            }
        }

        private static void ScheduleRefresh()
        {
            if (refreshScheduled)
                return;

            refreshScheduled = true;
            MelonCoroutines.Start(RefreshDeferred());
        }

        private static IEnumerator RefreshDeferred()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            refreshScheduled = false;
            if (GlobalState.IsGarageSceneActive)
                Refresh();
        }
    }

    [HarmonyPatch]
    internal static class OwnedPartCachePatches
    {
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Delete), new Type[] { typeof(Item) })]
        [HarmonyPostfix]
        private static void InventoryDeletePostfix(Item item)
        {
            OwnedPartCache.NotifyItemRemoved(item);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Add), new Type[] { typeof(Item), typeof(bool) })]
        [HarmonyPostfix]
        private static void InventoryAddPostfix(Item item)
        {
            OwnedPartCache.NotifyItemAdded(item);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddGroup))]
        [HarmonyPostfix]
        private static void InventoryAddGroupPostfix(GroupItem group)
        {
            OwnedPartCache.NotifyGroupAdded(group);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DeleteGroup))]
        [HarmonyPostfix]
        private static void InventoryDeleteGroupPostfix()
        {
            // The game removes an assembled wheel/shock absorber through
            // DeleteGroup(long). During separation its component items are then
            // exposed as regular inventory entries. Rebuild after the complete
            // operation so neither the old group nor intermediate state remains
            // in the cache.
            OwnedPartCache.NotifyGroupCollectionChanged();
        }

        [HarmonyPatch(typeof(RepairPartWindow), nameof(RepairPartWindow.ProcessGameResult))]
        [HarmonyPostfix]
        private static void RepairResultPostfix()
        {
            OwnedPartCache.NotifyConditionChanged();
        }
    }

}
