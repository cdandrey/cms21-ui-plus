using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic.Warehouse;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Logic.Warehouse;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    public static partial class InventoryFilterManager
    {
        private sealed class InventoryPackageInfo
        {
            public int MatchedCount;
            public long RepresentativeUid;
            public bool RepresentativeIsGroup;
            public bool Repairable;
        }

        private sealed class InventoryGroupingState
        {
            public BaseInventory Inventory;
            public string ExpandedKey;
            public readonly Dictionary<string, InventoryPackageInfo> Packages =
                new Dictionary<string, InventoryPackageInfo>(StringComparer.Ordinal);
        }

        private sealed class PackageAccumulator
        {
            public string Key;
            public int MatchedCount;
            public BaseItem Representative;
        }

        private sealed class PackageRowInfo
        {
            public int InventoryInstanceId;
            public string Key;
        }

        private sealed class PackageSaleContext
        {
            public BaseInventory Inventory;
            public readonly List<BaseItem> Members = new List<BaseItem>();
            public string DisplayName;
            public long RepresentativeUid;
            public bool RepresentativeIsGroup;
            public int TotalPrice;
        }

        private static readonly Dictionary<int, InventoryGroupingState> GroupingStates =
            new Dictionary<int, InventoryGroupingState>();
        private static readonly Dictionary<int, PackageRowInfo> PackageRows =
            new Dictionary<int, PackageRowInfo>();
        private static readonly Dictionary<int, BaseInventory> GroupingListTriggers =
            new Dictionary<int, BaseInventory>();
        private static int suppressedBetterButtonActionId;
        private static readonly Dictionary<Type, MemberInfo> GroupIdMembers =
            new Dictionary<Type, MemberInfo>();
        private static readonly HashSet<Type> MissingGroupIdMembers =
            new HashSet<Type>();
        private static readonly Dictionary<long, string> ItemGroupingKeys =
            new Dictionary<long, string>();
        private static readonly Dictionary<long, string> GroupGroupingKeys =
            new Dictionary<long, string>();
        private const string GroupingHintName = "Hint_InventoryGrouping";
        private const string WarehouseGroupingClickSurfaceName =
            "QInventoryGroupingClickSurface";
        private static BaseInventory groupingHintInventory;
        private static string groupingHintWindowId;
        private static PackageSaleContext currentPackageSaleCandidate;
        private static PackageSaleContext pendingPackageSale;
        private static int pendingPackageSaleMoney;
        private static string pendingPackageSalePopupName;
        private static int pendingPackageSalePopupCount;
        private static bool packageSaleInProgress;
        private static readonly MethodInfo SellItemMethod = AccessTools.Method(
            typeof(NotificationCenter), "SellItem",
            new Type[] { typeof(Item), typeof(bool), typeof(bool) });
        private static readonly MethodInfo SellGroupMethod = AccessTools.Method(
            typeof(NotificationCenter), "SellItem",
            new Type[] { typeof(GroupItem), typeof(bool), typeof(bool) });

        internal static bool IsInventoryGroupingEnabled()
        {
            return Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.groupInventoryParts;
        }

        internal static bool SupportsInventoryGrouping(BaseInventory inventory)
        {
            if (inventory == null || IsBarnOrJunkyardScene())
                return false;

            if (inventory.TryCast<InventoryWindow>() != null ||
                inventory.TryCast<WarehouseInventoryTab>() != null ||
                inventory.TryCast<WarehouseTab>() != null)
                return true;

            return inventory.GetComponentInParent<WarehouseWindow>() != null;
        }

        internal static bool ShouldHideSingleOwnedCountBadge(
            BaseInventory inventory)
        {
            if (inventory == null || IsBarnOrJunkyardScene())
                return false;

            if (inventory.TryCast<InventoryWindow>() != null ||
                inventory.TryCast<WarehouseInventoryTab>() != null ||
                inventory.TryCast<WarehouseTab>() != null)
                return true;

            return inventory.GetComponentInParent<WarehouseWindow>() != null;
        }

        private static Il2CppSystem.Collections.Generic.List<BaseItem>
            BuildGroupedDisplay(BaseInventory inventory,
                Il2CppSystem.Collections.Generic.List<BaseItem> source,
                PartFilterCriteria criteria)
        {
            InventoryGroupingState state = GetGroupingState(inventory, true);
            state.Packages.Clear();

            if (!string.IsNullOrEmpty(state.ExpandedKey))
                return BuildExpandedDisplay(source, state.ExpandedKey, criteria);

            Dictionary<string, PackageAccumulator> packages =
                new Dictionary<string, PackageAccumulator>(StringComparer.Ordinal);
            List<PackageAccumulator> order =
                new List<PackageAccumulator>(source.Count);

            foreach (BaseItem baseItem in source) {
                string key = GetInventoryGroupingKey(baseItem);
                if (string.IsNullOrEmpty(key))
                    continue;

                PackageAccumulator package;
                if (!packages.TryGetValue(key, out package)) {
                    package = new PackageAccumulator();
                    package.Key = key;
                    packages.Add(key, package);
                    order.Add(package);
                }

                if (criteria != null && criteria.HasAnyFilter &&
                    !PartFilterRules.Matches(baseItem, criteria))
                    continue;

                package.MatchedCount++;
                if (package.Representative == null)
                    package.Representative = baseItem;
            }

            Il2CppSystem.Collections.Generic.List<BaseItem> display =
                new Il2CppSystem.Collections.Generic.List<BaseItem>(order.Count);
            for (int i = 0; i < order.Count; i++) {
                PackageAccumulator package = order[i];
                if (package.MatchedCount <= 0 || package.Representative == null ||
                    !MatchesPackageQuickFilter(package.MatchedCount))
                    continue;

                display.Add(package.Representative);
                if (package.MatchedCount <= 1)
                    continue;

                InventoryPackageInfo info = new InventoryPackageInfo();
                info.MatchedCount = package.MatchedCount;
                info.RepresentativeUid = GetBaseItemUid(package.Representative);
                info.RepresentativeIsGroup =
                    package.Representative.TryCast<GroupItem>() != null;
                Item item = package.Representative.TryCast<Item>();
                info.Repairable = item != null &&
                    PartRepairabilityRules.IsRepairable(item);
                state.Packages[package.Key] = info;
            }

            return display;
        }

        private static Il2CppSystem.Collections.Generic.List<BaseItem>
            BuildExpandedDisplay(
                Il2CppSystem.Collections.Generic.List<BaseItem> source,
                string expandedKey, PartFilterCriteria criteria)
        {
            Il2CppSystem.Collections.Generic.List<BaseItem> display =
                new Il2CppSystem.Collections.Generic.List<BaseItem>();

            foreach (BaseItem baseItem in source) {
                if (!string.Equals(GetInventoryGroupingKey(baseItem), expandedKey,
                    StringComparison.Ordinal))
                    continue;
                if (criteria != null && criteria.HasAnyFilter &&
                    !PartFilterRules.Matches(baseItem, criteria))
                    continue;
                display.Add(baseItem);
            }

            return display;
        }


        private static int GetExpandedFilteredCount(BaseInventory inventory,
            Il2CppSystem.Collections.Generic.List<BaseItem> source,
            PartFilterCriteria criteria)
        {
            InventoryGroupingState state = GetGroupingState(inventory, false);
            if (state == null || string.IsNullOrEmpty(state.ExpandedKey))
                return -1;

            int count = 0;
            foreach (BaseItem baseItem in source) {
                if (!string.Equals(GetInventoryGroupingKey(baseItem),
                    state.ExpandedKey, StringComparison.Ordinal))
                    continue;
                if (criteria != null && criteria.HasAnyFilter &&
                    !PartFilterRules.Matches(baseItem, criteria))
                    continue;
                count++;
            }
            return count;
        }

        internal static bool MatchesExpandedInventoryGroup(
            BaseInventory inventory, BaseItem baseItem)
        {
            InventoryGroupingState state = GetGroupingState(inventory, false);
            return state == null || string.IsNullOrEmpty(state.ExpandedKey) ||
                string.Equals(GetInventoryGroupingKey(baseItem), state.ExpandedKey,
                    StringComparison.Ordinal);
        }

        internal static bool IsExpandedInventoryPackage(BaseInventory inventory)
        {
            InventoryGroupingState state = GetGroupingState(inventory, false);
            return state != null && !string.IsNullOrEmpty(state.ExpandedKey);
        }

        internal static bool TryGetPackageInfo(BaseInventory inventory,
            BaseItem baseItem, out int count, out bool repairable)
        {
            count = 0;
            repairable = false;
            if (inventory == null || baseItem == null)
                return false;

            InventoryGroupingState state = GetGroupingState(inventory, false);
            if (state == null || !string.IsNullOrEmpty(state.ExpandedKey))
                return false;

            string key = GetInventoryGroupingKey(baseItem);
            InventoryPackageInfo info;
            if (string.IsNullOrEmpty(key) ||
                !state.Packages.TryGetValue(key, out info) ||
                info.RepresentativeUid != GetBaseItemUid(baseItem) ||
                info.RepresentativeIsGroup !=
                    (baseItem.TryCast<GroupItem>() != null))
                return false;

            count = info.MatchedCount;
            repairable = info.Repairable;
            return true;
        }

        internal static bool TryGetPackageMembersForTransfer(
            BaseInventory inventory, BaseItem representative,
            out List<BaseItem> members)
        {
            members = null;
            int packageCount;
            bool repairable;
            if (!TryGetPackageInfo(inventory, representative, out packageCount,
                out repairable) || packageCount <= 1)
                return false;

            ForceRestoreSnapshot(inventory);
            ItemsBinding binding = GetItemsBinding(inventory);
            if (binding == null)
                return false;

            Il2CppSystem.Collections.Generic.List<BaseItem> source;
            try {
                source = binding.Get(inventory);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[InventoryGrouping] Failed to read package for transfer." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
                return false;
            }
            if (source == null)
                return false;

            string key = GetInventoryGroupingKey(representative);
            if (string.IsNullOrEmpty(key))
                return false;

            PartFilterCriteria criteria = IsFeatureEnabled()
                ? CreateCurrentCriteria() : null;
            List<BaseItem> result = new List<BaseItem>(packageCount);
            foreach (BaseItem baseItem in source) {
                if (!string.Equals(GetInventoryGroupingKey(baseItem), key,
                    StringComparison.Ordinal))
                    continue;
                if (criteria != null && criteria.HasAnyFilter &&
                    !PartFilterRules.Matches(baseItem, criteria))
                    continue;
                result.Add(baseItem);
            }

            if (result.Count != packageCount)
                return false;
            members = result;
            return true;
        }

        private static void RegisterPackageRow(BaseInventory inventory,
            BaseItem baseItem, InventoryItem row)
        {
            if (row == null)
                return;

            EnsureGroupingListTrigger(inventory, row);
            int rowId = row.GetInstanceID();
            int count;
            bool repairable;
            if (!TryGetPackageInfo(inventory, baseItem, out count,
                out repairable)) {
                PackageRows.Remove(rowId);
                return;
            }

            PackageRowInfo rowInfo = new PackageRowInfo();
            rowInfo.InventoryInstanceId = inventory.GetInstanceID();
            rowInfo.Key = GetInventoryGroupingKey(baseItem);
            PackageRows[rowId] = rowInfo;
        }

        private static bool TryExpandPackageFromRow(BaseInventory inventory,
            InventoryItem row)
        {
            if (inventory == null || row == null ||
                !IsInventoryGroupingEnabled() ||
                !SupportsInventoryGrouping(inventory))
                return false;

            string key;
            if (!TryResolvePackageKeyFromRow(inventory, row, out key))
                return false;

            InventoryGroupingState state = GetGroupingState(inventory, true);
            if (state == null)
                return false;

            state.ExpandedKey = key;
            state.Packages.Clear();
            ForceRestoreSnapshot(inventory);
            ResetCurrentPage(inventory);
            inventory.RedrawCurrentPage();
            EnsureInventoryGroupingHint(inventory);
            return true;
        }

        private static bool TryResolvePackageKeyFromRow(BaseInventory inventory,
            InventoryItem row, out string key)
        {
            key = null;
            PackageRowInfo rowInfo;
            if (PackageRows.TryGetValue(row.GetInstanceID(), out rowInfo) &&
                rowInfo.InventoryInstanceId == inventory.GetInstanceID() &&
                !string.IsNullOrEmpty(rowInfo.Key)) {
                key = rowInfo.Key;
                return true;
            }

            BaseItem baseItem = GetInventoryRowBaseItem(row);
            key = GetInventoryGroupingKey(baseItem);
            if (string.IsNullOrEmpty(key))
                return false;

            int count;
            bool repairable;
            if (TryGetPackageInfo(inventory, baseItem, out count,
                out repairable) && count > 1)
                return true;

            InventoryGroupingState state = GetGroupingState(inventory, false);
            InventoryPackageInfo packageInfo;
            if (state != null && string.IsNullOrEmpty(state.ExpandedKey) &&
                state.Packages.TryGetValue(key, out packageInfo) &&
                packageInfo.MatchedCount > 1)
                return true;

            if (inventory.GetComponentInParent<WarehouseWindow>() == null) {
                key = null;
                return false;
            }

            ForceRestoreSnapshot(inventory);
            ItemsBinding binding = GetItemsBinding(inventory);
            if (binding == null)
                return false;

            Il2CppSystem.Collections.Generic.List<BaseItem> source;
            try {
                source = binding.Get(inventory);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[InventoryGrouping] Failed to resolve package row." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
                return false;
            }
            if (source == null)
                return false;

            PartFilterCriteria criteria = IsFeatureEnabled()
                ? CreateCurrentCriteria() : null;
            int matchedCount = 0;
            foreach (BaseItem candidate in source) {
                if (!string.Equals(GetInventoryGroupingKey(candidate), key,
                    StringComparison.Ordinal))
                    continue;
                if (criteria != null && criteria.HasAnyFilter &&
                    !PartFilterRules.Matches(candidate, criteria))
                    continue;
                if (++matchedCount > 1)
                    return true;
            }

            key = null;
            return false;
        }

        private static BaseItem GetInventoryRowBaseItem(InventoryItem row)
        {
            if (row == null || row.ButtonAction == null ||
                row.ButtonAction.hash == null)
                return null;

            Il2CppSystem.Object value = row.ButtonAction.hash.GetFromKey("Item");
            if (value == null)
                return null;

            BaseItem baseItem = value.TryCast<Item>();
            return baseItem ?? value.TryCast<GroupItem>();
        }

        private static bool TryCloseExpandedPackage(BaseInventory inventory)
        {
            if (inventory == null || inventory.gameObject == null ||
                !inventory.gameObject.activeInHierarchy)
                return false;

            InventoryGroupingState state = GetGroupingState(inventory, false);
            if (state == null || string.IsNullOrEmpty(state.ExpandedKey))
                return false;

            state.ExpandedKey = null;
            state.Packages.Clear();
            RemovePackageRows(inventory.GetInstanceID());
            ForceRestoreSnapshot(inventory);
            ResetCurrentPage(inventory);
            inventory.RedrawCurrentPage();
            EnsureInventoryGroupingHint(inventory);
            return true;
        }

        private static void CollapseExpandedPackageWithoutRedraw(
            BaseInventory inventory)
        {
            InventoryGroupingState state = GetGroupingState(inventory, false);
            if (state == null || string.IsNullOrEmpty(state.ExpandedKey))
                return;

            state.ExpandedKey = null;
            state.Packages.Clear();
            RemovePackageRows(inventory.GetInstanceID());
        }

        internal static bool ShouldSuppressInventoryRowRightClick(
            InventoryItem row)
        {
            if (row == null || !IsInventoryGroupingEnabled())
                return false;

            BaseInventory inventory = row.GetComponentInParent<BaseInventory>();
            return inventory != null && SupportsInventoryGrouping(inventory);
        }

        internal static bool ConsumeSuppressedBetterButtonRightClick(
            int actionInstanceId)
        {
            if (suppressedBetterButtonActionId == 0 ||
                actionInstanceId != suppressedBetterButtonActionId)
                return false;

            suppressedBetterButtonActionId = 0;
            return true;
        }

        private static void SuppressBetterButtonRightClick(InventoryItem row)
        {
            suppressedBetterButtonActionId = row != null &&
                row.ButtonAction != null
                    ? row.ButtonAction.GetInstanceID()
                    : 0;
        }

        internal static bool TryHandleInventoryRowRightClick(
            InventoryItem row, PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button != PointerEventData.InputButton.Right ||
                !ShouldSuppressInventoryRowRightClick(row))
                return false;

            SuppressBetterButtonRightClick(row);
            BaseInventory inventory = row.GetComponentInParent<BaseInventory>();
            EnsureGroupingListTrigger(inventory, row);
            bool closed = TryCloseExpandedPackage(inventory);
            if (!closed)
                TryExpandPackageFromRow(inventory, row);
            eventData.Use();
            return true;
        }

        internal static bool TryHandleGroupingListRightClick(
            EventTrigger trigger, PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button != PointerEventData.InputButton.Right ||
                trigger == null || !IsInventoryGroupingEnabled())
                return false;

            BaseInventory inventory;
            if (!GroupingListTriggers.TryGetValue(trigger.GetInstanceID(),
                    out inventory) || inventory == null ||
                !SupportsInventoryGrouping(inventory))
                return false;

            GameObject raycastObject =
                eventData.pointerCurrentRaycast.gameObject;
            if (raycastObject != null &&
                raycastObject.transform.GetComponentInParent<InventoryItem>() != null)
                return false;

            if (!TryCloseExpandedPackage(inventory))
                return false;
            eventData.Use();
            return true;
        }

        private static void EnsureGroupingListTrigger(BaseInventory inventory,
            InventoryItem row)
        {
            if (inventory == null || row == null)
                return;

            foreach (KeyValuePair<int, BaseInventory> pair in
                GroupingListTriggers) {
                if (pair.Value == inventory)
                    return;
            }

            Transform target = inventory.GetComponentInParent<WarehouseWindow>() != null
                ? EnsureWarehouseGroupingClickSurface(inventory, row)
                : row.transform.parent;
            if (target == null || target.GetComponent<RectTransform>() == null) {
                ScrollRect scrollRect = row.GetComponentInParent<ScrollRect>();
                if (scrollRect != null && scrollRect.viewport != null)
                    target = scrollRect.viewport;
            }
            if (target == null)
                return;

            Graphic targetGraphic = target.GetComponent<Graphic>();
            if (targetGraphic == null) {
                Image targetImage = target.gameObject.AddComponent<Image>();
                targetImage.color = Color.clear;
                targetImage.raycastTarget = true;
            } else {
                targetGraphic.raycastTarget = true;
            }

            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = target.gameObject.AddComponent<EventTrigger>();
            GroupingListTriggers[trigger.GetInstanceID()] = inventory;
        }

        private static Transform EnsureWarehouseGroupingClickSurface(
            BaseInventory inventory, InventoryItem row)
        {
            if (inventory == null || row == null || row.transform.parent == null)
                return null;

            Transform inventoryRoot = inventory.transform;
            Transform contentRoot = row.transform.parent;
            while (contentRoot.parent != null &&
                contentRoot.parent != inventoryRoot)
                contentRoot = contentRoot.parent;
            if (contentRoot.parent != inventoryRoot)
                return null;

            Transform existing = inventoryRoot.Find(
                WarehouseGroupingClickSurfaceName);
            if (existing != null)
                return existing;

#if NET6_0_OR_GREATER
            GameObject surfaceObject = new GameObject(
                WarehouseGroupingClickSurfaceName, typeof(RectTransform));
#else
            UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type>(1);
            componentTypes[0] =
                UnhollowerRuntimeLib.Il2CppType.Of<RectTransform>();
            GameObject surfaceObject = new GameObject(
                WarehouseGroupingClickSurfaceName, componentTypes);
#endif
            surfaceObject.transform.SetParent(inventoryRoot, false);
            surfaceObject.layer = inventory.gameObject.layer;
            surfaceObject.transform.SetSiblingIndex(contentRoot.GetSiblingIndex());

            RectTransform sourceRect = contentRoot.GetComponent<RectTransform>();
            RectTransform surfaceRect =
                surfaceObject.GetComponent<RectTransform>();
            if (sourceRect == null || surfaceRect == null) {
                UnityEngine.Object.Destroy(surfaceObject);
                return null;
            }

            surfaceRect.anchorMin = sourceRect.anchorMin;
            surfaceRect.anchorMax = sourceRect.anchorMax;
            surfaceRect.pivot = sourceRect.pivot;
            surfaceRect.anchoredPosition = sourceRect.anchoredPosition;
            surfaceRect.sizeDelta = sourceRect.sizeDelta;
            surfaceRect.localScale = sourceRect.localScale;
            surfaceRect.localRotation = sourceRect.localRotation;

            LayoutElement layoutElement = surfaceObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            Image image = surfaceObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            return surfaceObject.transform;
        }

        private static bool MatchesPackageQuickFilter(int matchedCount)
        {
            switch (packageFilterMode) {
                case PackageQuickFilterMode.Packages:
                    return matchedCount > 1;
                case PackageQuickFilterMode.Singles:
                    return matchedCount == 1;
                default:
                    return true;
            }
        }

        internal static void ResetInventoryGrouping(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            int instanceId = inventory.GetInstanceID();
            GroupingStates.Remove(instanceId);
            RemovePackageRows(instanceId);
            RemoveGroupingListTriggers(inventory);
            suppressedBetterButtonActionId = 0;
            if (groupingHintInventory == inventory)
                ClearInventoryGroupingHint();
        }

        internal static void ResetWarehouseGrouping(WarehouseWindow window)
        {
            if (window == null)
                return;

            foreach (BaseInventory inventory in
                window.GetComponentsInChildren<BaseInventory>(true)) {
                ResetInventoryGrouping(inventory);
            }
        }

        private static void RemoveGroupingListTriggers(BaseInventory inventory)
        {
            if (inventory == null || GroupingListTriggers.Count == 0)
                return;

            int triggerId = 0;
            bool found = false;
            foreach (KeyValuePair<int, BaseInventory> pair in
                GroupingListTriggers) {
                if (pair.Value != inventory)
                    continue;
                triggerId = pair.Key;
                found = true;
                break;
            }

            if (found)
                GroupingListTriggers.Remove(triggerId);
        }

        private static InventoryGroupingState GetGroupingState(
            BaseInventory inventory, bool create)
        {
            if (inventory == null)
                return null;

            int instanceId = inventory.GetInstanceID();
            InventoryGroupingState state;
            if (GroupingStates.TryGetValue(instanceId, out state) || !create)
                return state;

            state = new InventoryGroupingState();
            state.Inventory = inventory;
            GroupingStates[instanceId] = state;
            return state;
        }

        private static void RemovePackageRows(int inventoryInstanceId)
        {
            List<int> staleRows = null;
            foreach (KeyValuePair<int, PackageRowInfo> pair in PackageRows) {
                if (pair.Value.InventoryInstanceId != inventoryInstanceId)
                    continue;
                if (staleRows == null)
                    staleRows = new List<int>();
                staleRows.Add(pair.Key);
            }

            if (staleRows == null)
                return;
            for (int i = 0; i < staleRows.Count; i++)
                PackageRows.Remove(staleRows[i]);
        }

        internal static void EnsureInventoryGroupingHint(
            BaseInventory inventory)
        {
            inventory = ResolveActiveInventory(inventory);
            if (inventory == null || !IsInventoryGroupingEnabled() ||
                !SupportsInventoryGrouping(inventory)) {
                ClearInventoryGroupingHint();
                return;
            }

            InventoryGroupingState state = GetGroupingState(inventory, false);
            bool expanded = state != null &&
                !string.IsNullOrEmpty(state.ExpandedKey);
            bool hasPackages = state != null && state.Packages.Count > 0;
            if (!expanded && !hasPackages) {
                ClearInventoryGroupingHint();
                return;
            }

            string windowId;
            Transform windowRoot;
            Transform descriptionRoot;
            if (!TryResolveInventoryFooter(inventory, out windowId,
                    out windowRoot, out descriptionRoot)) {
                ClearInventoryGroupingHint();
                return;
            }

            if (groupingHintInventory != inventory ||
                !string.Equals(groupingHintWindowId, windowId,
                    StringComparison.Ordinal))
                ClearInventoryGroupingHint();

            int itemCount = GetCurrentFilteredItemCount(inventory);
            groupingHintInventory = inventory;
            groupingHintWindowId = windowId;
            WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = windowId,
                    WindowRoot = windowRoot,
                    HintRoot = descriptionRoot,
                    HintId = GroupingHintName,
                    Keys = new string[] { "MouseRight" },
                    Text = ModLocalization.Get(expanded
                        ? "LOC_CloseInventoryPackageAction"
                        : "LOC_OpenInventoryPackageAction"),
                    Action = null,
                    Row = 0,
                    Order = 5,
                    Profile = ResolveFooterProfile(inventory, itemCount == 0),
                    ItemCount = itemCount,
                });
        }

        internal static void ClearInventoryGroupingHint()
        {
            if (!string.IsNullOrEmpty(groupingHintWindowId))
                WindowFooterHintController.RemoveHint(groupingHintWindowId,
                    GroupingHintName);
            groupingHintWindowId = null;
            groupingHintInventory = null;
        }

        private static PackageSaleContext CreatePackageSaleContext(
            BaseItem representative)
        {
            if (representative == null || !IsInventoryGroupingEnabled())
                return null;

            BaseInventory inventory = FindPackageInventory(representative);
            if (inventory == null)
                return null;

            int packageCount;
            bool repairable;
            if (!TryGetPackageInfo(inventory, representative, out packageCount,
                out repairable) || packageCount <= 1)
                return null;

            ForceRestoreSnapshot(inventory);
            ItemsBinding binding = GetItemsBinding(inventory);
            if (binding == null)
                return null;

            Il2CppSystem.Collections.Generic.List<BaseItem> source;
            try {
                source = binding.Get(inventory);
            } catch (Exception exception) {
                ModLogger.Log("[InventoryGrouping] Failed to read package for sale." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
                return null;
            }
            if (source == null)
                return null;

            string key = GetInventoryGroupingKey(representative);
            if (string.IsNullOrEmpty(key))
                return null;

            PartFilterCriteria criteria = IsFeatureEnabled()
                ? CreateCurrentCriteria() : null;
            PackageSaleContext context = new PackageSaleContext();
            context.Inventory = inventory;
            context.DisplayName = representative.GetLocalizedName();
            context.RepresentativeUid = GetBaseItemUid(representative);
            context.RepresentativeIsGroup =
                representative.TryCast<GroupItem>() != null;

            long totalPrice = 0L;
            foreach (BaseItem baseItem in source) {
                if (!string.Equals(GetInventoryGroupingKey(baseItem), key,
                    StringComparison.Ordinal))
                    continue;
                if (criteria != null && criteria.HasAnyFilter &&
                    !PartFilterRules.Matches(baseItem, criteria))
                    continue;

                int price = GetNativeSalePrice(baseItem);
                if (price < 0 || totalPrice + price > int.MaxValue)
                    return null;

                context.Members.Add(baseItem);
                totalPrice += price;
            }

            if (context.Members.Count <= 1 ||
                context.Members.Count != packageCount)
                return null;

            context.TotalPrice = (int)totalPrice;
            return context;
        }

        private static BaseInventory FindPackageInventory(BaseItem representative)
        {
            foreach (InventoryGroupingState state in GroupingStates.Values) {
                BaseInventory inventory = state != null ? state.Inventory : null;
                if (inventory == null || inventory.gameObject == null ||
                    !inventory.gameObject.activeInHierarchy)
                    continue;

                int count;
                bool repairable;
                if (TryGetPackageInfo(inventory, representative, out count,
                    out repairable))
                    return inventory;
            }
            return null;
        }

        private static void PreparePackageSalePopup(PackageSaleContext context)
        {
            pendingPackageSalePopupName = context != null
                ? context.DisplayName : null;
            pendingPackageSalePopupCount = context != null
                ? context.Members.Count : 0;
        }

        private static void ClearPackageSalePopup()
        {
            pendingPackageSalePopupName = null;
            pendingPackageSalePopupCount = 0;
        }

        private static int GetNativeSalePrice(BaseItem baseItem)
        {
            GroupItem group = baseItem != null
                ? baseItem.TryCast<GroupItem>() : null;
            if (group != null)
                return Helper.GetPrice(group) / 2;

            Item item = baseItem != null ? baseItem.TryCast<Item>() : null;
            return item != null ? Helper.GetPrice(item, 0.5f) : -1;
        }

        private static bool MatchesPackageSaleRepresentative(
            PackageSaleContext context, BaseItem baseItem)
        {
            if (context == null || baseItem == null || context.Inventory == null ||
                !context.Inventory.gameObject.activeInHierarchy)
                return false;

            int count;
            bool repairable;
            return context.RepresentativeUid == GetBaseItemUid(baseItem) &&
                context.RepresentativeIsGroup ==
                    (baseItem.TryCast<GroupItem>() != null) &&
                TryGetPackageInfo(context.Inventory, baseItem, out count,
                    out repairable) && count == context.Members.Count;
        }

        private static PackageSaleContext GetActivePackageSaleContext(
            BaseItem baseItem)
        {
            if (MatchesPackageSaleRepresentative(currentPackageSaleCandidate,
                baseItem))
                return currentPackageSaleCandidate;
            if (MatchesPackageSaleRepresentative(pendingPackageSale, baseItem))
                return pendingPackageSale;
            return null;
        }

        private static bool TryRemoveAdditionalPackageMembers(
            PackageSaleContext context)
        {
            if (context == null || context.Inventory == null ||
                context.Members.Count <= 1)
                return false;

            Inventory inventory = Singleton<Inventory>.Instance;
            if (inventory == null || inventory.items == null ||
                inventory.groups == null)
                return false;

            Il2CppSystem.Collections.Generic.List<Item> sourceItems;
            Il2CppSystem.Collections.Generic.List<GroupItem> sourceGroups;
            if (!TryResolvePackageSaleSource(context, inventory,
                    out sourceItems, out sourceGroups))
                return false;

            HashSet<long> itemUids = new HashSet<long>();
            HashSet<long> groupUids = new HashSet<long>();
            for (int i = 0; i < context.Members.Count; i++) {
                BaseItem baseItem = context.Members[i];
                if (baseItem == null ||
                    (GetBaseItemUid(baseItem) == context.RepresentativeUid &&
                     (baseItem.TryCast<GroupItem>() != null) ==
                        context.RepresentativeIsGroup))
                    continue;

                Item item = baseItem.TryCast<Item>();
                if (item != null) {
                    itemUids.Add(item.UID);
                    continue;
                }

                GroupItem group = baseItem.TryCast<GroupItem>();
                if (group != null)
                    groupUids.Add(group.UID);
            }

            Il2CppSystem.Collections.Generic.List<Item> retainedItems =
                new Il2CppSystem.Collections.Generic.List<Item>();
            int removedItems = 0;
            foreach (Item item in sourceItems) {
                if (item != null && itemUids.Contains(item.UID))
                    removedItems++;
                else
                    retainedItems.Add(item);
            }

            Il2CppSystem.Collections.Generic.List<GroupItem> retainedGroups =
                new Il2CppSystem.Collections.Generic.List<GroupItem>();
            int removedGroups = 0;
            foreach (GroupItem group in sourceGroups) {
                if (group != null && groupUids.Contains(group.UID))
                    removedGroups++;
                else
                    retainedGroups.Add(group);
            }

            if (removedItems != itemUids.Count ||
                removedGroups != groupUids.Count)
                return false;

            sourceItems.Clear();
            foreach (Item item in retainedItems)
                sourceItems.Add(item);
            sourceGroups.Clear();
            foreach (GroupItem group in retainedGroups)
                sourceGroups.Add(group);
            return true;
        }

        private static bool TryResolvePackageSaleSource(
            PackageSaleContext context, Inventory inventory,
            out Il2CppSystem.Collections.Generic.List<Item> sourceItems,
            out Il2CppSystem.Collections.Generic.List<GroupItem> sourceGroups)
        {
            sourceItems = null;
            sourceGroups = null;
            if (context == null || inventory == null)
                return false;

            if (ContainsPackageMembers(context, inventory.items, inventory.groups)) {
                sourceItems = inventory.items;
                sourceGroups = inventory.groups;
                return true;
            }

            Warehouse warehouse = GlobalState.GameManager != null
                ? GlobalState.GameManager.Warehouse : null;
            if (warehouse == null || warehouse.warehouseList == null ||
                warehouse.warehouseGroupList == null)
                return false;

            int warehouseIndex = warehouse.SelectedOption;
            if (warehouseIndex < 0 ||
                warehouseIndex >= warehouse.warehouseList.Count ||
                warehouseIndex >= warehouse.warehouseGroupList.Count)
                return false;

            Il2CppSystem.Collections.Generic.List<Item> warehouseItems =
                warehouse.warehouseList[warehouseIndex];
            Il2CppSystem.Collections.Generic.List<GroupItem> warehouseGroups =
                warehouse.warehouseGroupList[warehouseIndex];
            if (!ContainsPackageMembers(context, warehouseItems, warehouseGroups))
                return false;

            sourceItems = warehouseItems;
            sourceGroups = warehouseGroups;
            return true;
        }

        private static bool ContainsPackageMembers(PackageSaleContext context,
            Il2CppSystem.Collections.Generic.List<Item> items,
            Il2CppSystem.Collections.Generic.List<GroupItem> groups)
        {
            if (context == null || items == null || groups == null)
                return false;

            HashSet<long> itemUids = new HashSet<long>();
            foreach (Item item in items) {
                if (item != null)
                    itemUids.Add(item.UID);
            }

            HashSet<long> groupUids = new HashSet<long>();
            foreach (GroupItem group in groups) {
                if (group != null)
                    groupUids.Add(group.UID);
            }

            for (int i = 0; i < context.Members.Count; i++) {
                BaseItem baseItem = context.Members[i];
                Item item = baseItem != null ? baseItem.TryCast<Item>() : null;
                if (item != null) {
                    if (!itemUids.Contains(item.UID))
                        return false;
                    continue;
                }

                GroupItem group = baseItem != null
                    ? baseItem.TryCast<GroupItem>() : null;
                if (group == null || !groupUids.Contains(group.UID))
                    return false;
            }

            return true;
        }

        private static int SellAdditionalPackageMembers(NotificationCenter center,
            PackageSaleContext context, BaseItem representative,
            bool firstFlag, bool secondFlag)
        {
            int soldValue = GetNativeSalePrice(representative);
            if (center == null || context == null || soldValue < 0)
                return soldValue;

            packageSaleInProgress = true;
            try {
                for (int i = 0; i < context.Members.Count; i++) {
                    BaseItem baseItem = context.Members[i];
                    if (baseItem == null ||
                        (GetBaseItemUid(baseItem) == context.RepresentativeUid &&
                         (baseItem.TryCast<GroupItem>() != null) ==
                            context.RepresentativeIsGroup))
                        continue;

                    int price = GetNativeSalePrice(baseItem);
                    try {
                        Item item = baseItem.TryCast<Item>();
                        if (item != null) {
                            if (SellItemMethod == null)
                                continue;
                            SellItemMethod.Invoke(center,
                                new object[] { item, firstFlag, secondFlag });
                        } else {
                            GroupItem group = baseItem.TryCast<GroupItem>();
                            if (group == null || SellGroupMethod == null)
                                continue;
                            SellGroupMethod.Invoke(center,
                                new object[] { group, firstFlag, secondFlag });
                        }
                        if (price >= 0 && soldValue <= int.MaxValue - price)
                            soldValue += price;
                    } catch (Exception exception) {
                        ModLogger.Log("[InventoryGrouping] Package member sale failed." +
                            Environment.NewLine + exception,
                            Types.LoggingLevels.Error);
                    }
                }
            } finally {
                packageSaleInProgress = false;
            }
            return soldValue;
        }

        private static string GetInventoryGroupingKey(BaseItem baseItem)
        {
            if (baseItem == null)
                return string.Empty;

            Item item = baseItem.TryCast<Item>();
            if (item != null) {
                string key;
                if (item.UID != 0L &&
                    ItemGroupingKeys.TryGetValue(item.UID, out key))
                    return key;

                key = PartIdentityComparer.GetKey(item);
                if (item.UID != 0L && !string.IsNullOrEmpty(key))
                    ItemGroupingKeys[item.UID] = key;
                return key;
            }

            GroupItem group = baseItem.TryCast<GroupItem>();
            if (group != null) {
                string key;
                if (group.UID != 0L &&
                    GroupGroupingKeys.TryGetValue(group.UID, out key))
                    return key;

                key = "group|" + GetGroupOwnId(group) + "|" +
                    PartIdentityComparer.GetKey(group);
                if (group.UID != 0L && !string.IsNullOrEmpty(key))
                    GroupGroupingKeys[group.UID] = key;
                return key;
            }

            return "base|" + (baseItem.GetType().FullName ??
                baseItem.GetType().Name) + "|" + baseItem.GetHashCode();
        }

        private static long GetBaseItemUid(BaseItem baseItem)
        {
            Item item = baseItem != null ? baseItem.TryCast<Item>() : null;
            if (item != null)
                return item.UID;
            GroupItem group = baseItem != null
                ? baseItem.TryCast<GroupItem>() : null;
            return group != null ? group.UID : 0L;
        }

        private static string GetGroupOwnId(GroupItem group)
        {
            if (group == null)
                return string.Empty;

            Type type = group.GetType();
            MemberInfo member = null;
            if (!GroupIdMembers.TryGetValue(type, out member) &&
                !MissingGroupIdMembers.Contains(type)) {
                PropertyInfo property = type.GetProperty("ID",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (property != null && property.CanRead)
                    member = property;
                else
                    member = type.GetField("ID",
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (member != null)
                    GroupIdMembers[type] = member;
                else
                    MissingGroupIdMembers.Add(type);
            }

            try {
                object value = null;
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                    value = property.GetValue(group, null);
                else {
                    FieldInfo field = member as FieldInfo;
                    if (field != null)
                        value = field.GetValue(group);
                }
                if (value != null)
                    return value.ToString();
            } catch (Exception exception) {
                ModLogger.Log("[InventoryGrouping] Failed to read GroupItem ID." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }

            return group.GetLocalizedName() ?? string.Empty;
        }

        [HarmonyPatch(typeof(BaseInventory), "FillItem",
            new Type[] { typeof(BaseItem), typeof(InventoryItem) })]
        [HarmonyPrefix]
        private static void BaseInventoryFillItemGroupingPrefix(
            InventoryItem __1)
        {
            PartRowIndicators.ResetInventoryRowGroupingPresentation(__1);
        }

        [HarmonyPatch(typeof(BaseInventory), "FillItem",
            new Type[] { typeof(BaseItem), typeof(InventoryItem) })]
        [HarmonyPostfix]
        private static void BaseInventoryFillItemGroupingPostfix(
            BaseInventory __instance, BaseItem __0, InventoryItem __1)
        {
            if (!IsInventoryGroupingEnabled() ||
                !SupportsInventoryGrouping(__instance))
                return;

            RegisterPackageRow(__instance, __0, __1);
            PartRowIndicators.UpdateInventoryRow(__instance, __0, __1);
        }

        [HarmonyPatch(typeof(InventoryItem), "OnPointerClick",
            new Type[] { typeof(PointerEventData) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool InventoryItemOnPointerClickGroupingPrefix(
            InventoryItem __instance, PointerEventData __0)
        {
            return !TryHandleInventoryRowRightClick(__instance, __0);
        }

        [HarmonyPatch(typeof(NotificationCenter), nameof(NotificationCenter.NewButtonAccept),
            new Type[] { typeof(NewHash), typeof(bool) })]
        [HarmonyPrefix]
        private static void NotificationCenterNewButtonAcceptPackagePrefix(NewHash __0)
        {
            currentPackageSaleCandidate = null;
            if (__0 == null)
                return;

            Il2CppSystem.Object value = __0.GetFromKey("Item");
            if (value == null)
                return;
            BaseItem baseItem = value.TryCast<Item>();
            if (baseItem == null)
                baseItem = value.TryCast<GroupItem>();
            currentPackageSaleCandidate = CreatePackageSaleContext(baseItem);
            if (currentPackageSaleCandidate == null)
                pendingPackageSale = null;
        }

        [HarmonyPatch(typeof(NotificationCenter), nameof(NotificationCenter.NewButtonAccept),
            new Type[] { typeof(NewHash), typeof(bool) })]
        [HarmonyPostfix]
        private static void NotificationCenterNewButtonAcceptPackagePostfix()
        {
            currentPackageSaleCandidate = null;
        }

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.ShowAskWindow))]
        [HarmonyPrefix]
        private static void ShowPackageSaleAskPrefix(ref string description)
        {
            PackageSaleContext context = currentPackageSaleCandidate;
            if (context == null)
                return;

            description = string.Format(
                ModLocalization.Get("LOC_InventoryPackageSaleConfirmation"),
                context.Members.Count, context.TotalPrice);
            pendingPackageSale = context;
        }

        [HarmonyPatch(typeof(NotificationCenter), "SellItem",
            new Type[] { typeof(Item), typeof(bool), typeof(bool) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool SellPackageItemsPrefix(NotificationCenter __instance,
            Item __0, bool __1, bool __2)
        {
            if (packageSaleInProgress)
                return true;

            PackageSaleContext context = GetActivePackageSaleContext(__0);
            if (context == null)
                return true;

            currentPackageSaleCandidate = null;
            pendingPackageSale = null;
            PreparePackageSalePopup(context);
            pendingPackageSaleMoney = TryRemoveAdditionalPackageMembers(context)
                ? context.TotalPrice
                : SellAdditionalPackageMembers(__instance, context, __0, __1, __2);
            if (pendingPackageSaleMoney <= 0)
                ClearPackageSalePopup();
            return true;
        }

        [HarmonyPatch(typeof(NotificationCenter), "SellItem",
            new Type[] { typeof(GroupItem), typeof(bool), typeof(bool) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool SellPackageGroupsPrefix(NotificationCenter __instance,
            GroupItem __0, bool __1, bool __2)
        {
            if (packageSaleInProgress)
                return true;

            PackageSaleContext context = GetActivePackageSaleContext(__0);
            if (context == null)
                return true;

            currentPackageSaleCandidate = null;
            pendingPackageSale = null;
            PreparePackageSalePopup(context);
            pendingPackageSaleMoney = TryRemoveAdditionalPackageMembers(context)
                ? context.TotalPrice
                : SellAdditionalPackageMembers(__instance, context, __0, __1, __2);
            if (pendingPackageSaleMoney <= 0)
                ClearPackageSalePopup();
            return true;
        }

        [HarmonyPatch(typeof(PopupManager), nameof(PopupManager.ShowPopup),
            new Type[] { typeof(string), typeof(string) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool PopupManagerShowPackageSalePrefix(ref string __1)
        {
            if (packageSaleInProgress)
                return false;
            if (pendingPackageSalePopupCount <= 0)
                return true;

            __1 = string.Format(
                ModLocalization.Get("LOC_WarehouseTransferPopupBody"),
                pendingPackageSalePopupName ?? string.Empty,
                pendingPackageSalePopupCount);
            ClearPackageSalePopup();
            return true;
        }

        [HarmonyPatch(typeof(GlobalData), nameof(GlobalData.AddPlayerMoney),
            new Type[] { typeof(int) })]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void AddPackageSaleMoneyPrefix(ref int __0)
        {
            if (pendingPackageSaleMoney <= 0)
                return;

            __0 = pendingPackageSaleMoney;
            pendingPackageSaleMoney = 0;
        }
    }
}
