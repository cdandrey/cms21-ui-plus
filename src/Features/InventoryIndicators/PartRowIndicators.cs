using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.Warehouse;
using Il2CppCMS.UI.Helpers;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Logic.Warehouse;
using CMS.UI.Helpers;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    /// <summary>Renders repairability and owned-condition markers on inventory rows.</summary>
    public static class PartRowIndicators
    {
        private const string TotalCountColor = "FFFFFF";
        private const string PerfectCountColor = "66FF33";
        private const string Condition50To99Color = "FFFF00";
        private const string Condition15To49Color = "FF9900";
        private const string ConditionBelow15Color = "FF0000";
        private static readonly HashSet<int> PendingUpdates = new HashSet<int>();
        private const string PackageStackName = "QpackageStack";

        private sealed class PackageVisualState
        {
            public RectTransform Source;
            public Transform Stack;
            public Transform Back;
            public Transform Middle;
            public bool IsApplied;
            public bool SourceIsGroup;
            public long SourceUid;
            public int SourceHash;
            public Vector3 Scale;
            public Vector2 Position;
        }

        private static readonly Dictionary<int, PackageVisualState>
            PackageVisualStates = new Dictionary<int, PackageVisualState>();

        public static void ScheduleUpdate(Transform inventoryWindow)
        {
            if (inventoryWindow == null)
                return;

            UpdateSafely(inventoryWindow);

            int instanceId = inventoryWindow.GetInstanceID();
            if (PendingUpdates.Add(instanceId))
                MelonCoroutines.Start(UpdateDeferred(inventoryWindow, instanceId));
        }

        /// <summary>
        /// Applies the same indicators to rows populated by ChoosePartDownWindow.
        /// These rows are shared by repair, scrap, upgrade and workshop tools.
        /// </summary>
        public static void UpdateChoosePartDownRow(ChoosePartDownItem entry,
            InventoryItem row)
        {
            if (row == null)
                return;

            HideNativePaintColorBadge(row);

            try {
                if (!IsSupportedChoosePartDownContext(row) || entry == null ||
                    entry.BaseItem == null || Main.SettingsEntry == null) {
                    HideIndicators(row);
                    return;
                }

                Settings settings = Main.SettingsEntry.Value;
                bool showRepairability =
                    settings.showPartRepairabilityIndicators &&
                    !ShouldSuppressRepairIndicators(row);
                bool showOwnedCount = settings.showOwnedPartCountIndicators;

                GroupItem group = entry.BaseItem.TryCast<GroupItem>();
                Item item = group == null
                    ? entry.BaseItem.TryCast<Item>() : null;
                if (!showRepairability && !showOwnedCount) {
                    HideIndicators(row);
                    return;
                }

                GameObject repairIcon = showRepairability
                    ? PrepareRepairIcon(row) : null;
                if (!showRepairability)
                    HideRepairabilityIndicator(row);
                HideOwnedCountIndicator(row);

                if (group != null) {
                    if (showOwnedCount) {
                        UpdateOwnedIcon(row,
                            OwnedPartCache.GetConditionBreakdown(group),
                            InventoryIconProvider.GetWhiteWarehouseIcon());
                    }
                    return;
                }

                if (item == null)
                    return;

                if (showOwnedCount) {
                    UpdateOwnedIcon(row,
                        OwnedPartCache.GetConditionBreakdown(item),
                        InventoryIconProvider.GetWhiteWarehouseIcon());
                }

                if (!showRepairability || repairIcon == null ||
                    !PartRepairabilityRules.IsRepairable(item))
                    return;

                Image repairImage = repairIcon.GetComponent<Image>();
                if (repairImage == null)
                    return;

                repairImage.sprite =
                    InventoryIconProvider.GetWhiteRepairWrenchIcon();
                if (repairImage.sprite == null) {
                    repairIcon.SetActive(false);
                    return;
                }

                repairImage.color = Color.white;
                repairIcon.SetActive(true);
                Text textTemplate = row.GetComponentInChildren<Text>();
                RepairSkillIndicator.Update(repairIcon, item.ID, textTemplate);
            } catch (Exception exception) {
                HideIndicators(row);
                ModLogger.Log("[InventoryIndicators] Choose-part row update " +
                    "failed." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
            }
        }

        internal static void UpdateInventoryRepairabilityRow(BaseItem baseItem,
            InventoryItem row)
        {
            if (baseItem == null || row == null || Main.SettingsEntry == null)
                return;

            if (!Main.SettingsEntry.Value.showPartRepairabilityIndicators) {
                HideRepairabilityIndicator(row);
                return;
            }

            Item item = baseItem.TryCast<Item>();
            if (item == null || !PartRepairabilityRules.IsRepairable(item)) {
                HideRepairabilityIndicator(row);
                return;
            }

            GameObject repairIcon = PrepareRepairIcon(row);
            Image repairImage = repairIcon != null
                ? repairIcon.GetComponent<Image>() : null;
            if (repairImage == null)
                return;

            repairImage.sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
            if (repairImage.sprite == null) {
                repairIcon.SetActive(false);
                return;
            }

            repairImage.color = Color.white;
            repairIcon.SetActive(true);
            RepairSkillIndicator.Update(repairIcon, item.ID,
                row.GetComponentInChildren<Text>());
        }

        public static void ResetInventoryRowGroupingPresentation(
            InventoryItem row)
        {
            if (row == null)
                return;

            Transform condition = row.transform.Find("Condition");
            if (condition != null)
                condition.gameObject.SetActive(true);
            Transform quality = row.transform.Find("Quality");
            if (quality != null)
                quality.gameObject.SetActive(true);
            ResetPackageCascade(row);
        }

        public static void UpdateInventoryRow(BaseInventory inventory,
            BaseItem baseItem, InventoryItem row)
        {
            if (inventory == null || baseItem == null || row == null)
                return;

            ApplyGroupingPresentation(inventory, baseItem, row);
        }

        private static bool ApplyGroupingPresentation(BaseInventory inventory,
            BaseItem baseItem, InventoryItem row)
        {
            ResetPackageCascade(row);
            if (!InventoryFilterManager.IsInventoryGroupingEnabled() ||
                !InventoryFilterManager.SupportsInventoryGrouping(inventory))
                return false;

            int packageCount;
            bool packageRepairable;
            bool isPackage = InventoryFilterManager.TryGetPackageInfo(
                inventory, baseItem, out packageCount, out packageRepairable);

            if (isPackage) {
                Transform condition = row.transform.Find("Condition");
                if (condition != null)
                    condition.gameObject.SetActive(false);
                Transform quality = row.transform.Find("Quality");
                if (quality != null)
                    quality.gameObject.SetActive(false);
                ApplyPackageCascade(baseItem, row);
            }

            bool expanded =
                InventoryFilterManager.IsExpandedInventoryPackage(inventory);
            if (expanded)
                HideOwnedCountIndicator(row);
            return expanded;
        }

        private static void ResetPackageCascade(InventoryItem row)
        {
            if (row == null)
                return;

            PackageVisualState state;
            if (!PackageVisualStates.TryGetValue(row.GetInstanceID(), out state))
                return;

            if (state.IsApplied && state.Source != null) {
                state.Source.localScale = state.Scale;
                state.Source.anchoredPosition = state.Position;
            }
            state.IsApplied = false;
            if (state.Stack != null)
                state.Stack.gameObject.SetActive(false);
        }

        private static void ApplyPackageCascade(BaseItem baseItem,
            InventoryItem row)
        {
            if (baseItem == null || row == null)
                return;

            bool isGroup = baseItem.TryCast<GroupItem>() != null;
            Transform sourceTransform = row.transform.Find(isGroup
                ? "InventoryItemGroup" : "InventoryItem");
            RectTransform source = sourceTransform != null
                ? sourceTransform.GetComponent<RectTransform>() : null;
            if (source == null)
                return;

            int rowId = row.GetInstanceID();
            long sourceUid = GetBaseItemUid(baseItem);
            int sourceHash = sourceUid == 0L ? baseItem.GetHashCode() : 0;
            PackageVisualState state;
            bool sourceChanged = !PackageVisualStates.TryGetValue(rowId,
                out state) || state.Source != source;
            bool itemChanged = sourceChanged || state.SourceUid != sourceUid ||
                state.SourceHash != sourceHash ||
                state.SourceIsGroup != isGroup;
            if (sourceChanged) {
                if (state != null)
                    DestroyPackageStack(state);
                state = new PackageVisualState();
                state.Source = source;
                PackageVisualStates[rowId] = state;
            }
            if (itemChanged) {
                state.SourceUid = sourceUid;
                state.SourceHash = sourceHash;
                state.SourceIsGroup = isGroup;
                state.Scale = source.localScale;
                state.Position = source.anchoredPosition;
            }

            if (!EnsurePackageStack(row, state))
                return;

            Vector3 packageScale = state.Scale * 0.58f;
            ConfigurePackageStackCopy(state.Back, source,
                state.Position + new Vector2(-18f, 18f), packageScale);
            ConfigurePackageStackCopy(state.Middle, source,
                state.Position + new Vector2(-9f, 9f), packageScale);

            source.localScale = packageScale;
            source.anchoredPosition = state.Position;
            state.Stack.gameObject.SetActive(true);
            state.IsApplied = true;
        }

        private static bool EnsurePackageStack(InventoryItem row,
            PackageVisualState state)
        {
            if (row == null || state == null || state.Source == null)
                return false;

            if (state.Stack != null && state.Back != null &&
                state.Middle != null) {
                bool backHasVisual;
                bool middleHasVisual;
                if (SyncPackageVisualNode(state.Source, state.Back,
                        out backHasVisual) &&
                    SyncPackageVisualNode(state.Source, state.Middle,
                        out middleHasVisual)) {
                    state.Back.SetSiblingIndex(0);
                    state.Middle.SetSiblingIndex(1);
                    return backHasVisual || middleHasVisual;
                }
                DestroyPackageStack(state);
            }

            Transform stack = RecreatePackageStack(row, state.Source);
            if (stack == null)
                return false;

            state.Stack = stack;
            state.Back = stack.Find("Back");
            state.Middle = stack.Find("Middle");
            return state.Back != null && state.Middle != null;
        }

        private static void DestroyPackageStack(PackageVisualState state)
        {
            if (state == null)
                return;

            if (state.IsApplied && state.Source != null) {
                state.Source.localScale = state.Scale;
                state.Source.anchoredPosition = state.Position;
            }
            state.IsApplied = false;
            if (state.Stack != null) {
                state.Stack.gameObject.SetActive(false);
                state.Stack.name = PackageStackName + "_old";
                state.Stack.SetParent(null, false);
                UnityEngine.Object.Destroy(state.Stack.gameObject);
            }
            state.Stack = null;
            state.Back = null;
            state.Middle = null;
        }

        private static bool SyncPackageVisualNode(RectTransform source,
            Transform target, out bool hasVisual)
        {
            hasVisual = false;
            if (source == null || target == null)
                return false;

            RectTransform targetRect = target.GetComponent<RectTransform>();
            if (targetRect == null)
                return false;

            Image sourceImage = source.GetComponent<Image>();
            RawImage sourceRawImage = sourceImage == null
                ? source.GetComponent<RawImage>() : null;
            Image targetImage = target.GetComponent<Image>();
            RawImage targetRawImage = targetImage == null
                ? target.GetComponent<RawImage>() : null;
            if ((sourceImage != null) != (targetImage != null) ||
                (sourceRawImage != null) != (targetRawImage != null))
                return false;

            CopyRectTransform(source, targetRect);
            if (sourceImage != null) {
                CopyImage(sourceImage, targetImage);
                hasVisual = sourceImage.sprite != null ||
                    sourceImage.overrideSprite != null;
            } else if (sourceRawImage != null) {
                CopyRawImage(sourceRawImage, targetRawImage);
                hasVisual = sourceRawImage.texture != null;
            }

            int targetChildIndex = 0;
            for (int i = 0; i < source.childCount; i++) {
                RectTransform childSource = source.GetChild(i)
                    .GetComponent<RectTransform>();
                if (childSource == null)
                    continue;
                if (targetChildIndex >= target.childCount)
                    return false;

                Transform childTarget = target.GetChild(targetChildIndex++);
                if (!string.Equals(childTarget.name, childSource.name,
                        StringComparison.Ordinal))
                    return false;

                bool childHasVisual;
                if (!SyncPackageVisualNode(childSource, childTarget,
                        out childHasVisual))
                    return false;
                childTarget.gameObject.SetActive(
                    childSource.gameObject.activeSelf);
                hasVisual |= childHasVisual;
            }

            return targetChildIndex == target.childCount;
        }

        private static GameObject CreatePackageStack(Transform parent)
        {
#if NET6_0_OR_GREATER
            GameObject stack = new GameObject(PackageStackName,
                typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            GameObject stack = new GameObject(PackageStackName, componentTypes);
#endif
            stack.transform.SetParent(parent, false);
            stack.layer = parent.gameObject.layer;
            return stack;
        }

        private static Transform RecreatePackageStack(InventoryItem row,
            RectTransform source)
        {
            if (row == null || source == null)
                return null;

            Transform stack = CreatePackageStack(row.transform).transform;
            stack.gameObject.SetActive(false);
            stack.SetSiblingIndex(source.GetSiblingIndex());
            RectTransform stackRect = stack.GetComponent<RectTransform>();
            if (stackRect != null) {
                stackRect.anchorMin = Vector2.zero;
                stackRect.anchorMax = Vector2.one;
                stackRect.offsetMin = Vector2.zero;
                stackRect.offsetMax = Vector2.zero;
                stackRect.localScale = Vector3.one;
            }

            if (!CreatePackageStackCopy(stack, "Back", source) ||
                !CreatePackageStackCopy(stack, "Middle", source)) {
                stack.SetParent(null, false);
                UnityEngine.Object.Destroy(stack.gameObject);
                return null;
            }
            return stack;
        }

        private static void ConfigurePackageStackCopy(Transform copy,
            RectTransform source, Vector2 position, Vector3 scale)
        {
            if (copy == null || source == null)
                return;

            RectTransform rect = copy.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = position;
            rect.localRotation = source.localRotation;
            rect.localScale = scale;
            copy.gameObject.SetActive(true);
        }

        private static bool CreatePackageStackCopy(Transform parent,
            string name, RectTransform source)
        {
            if (parent == null || source == null)
                return false;

            bool hasVisual;
            Transform copy = CreatePackageVisualNode(parent, name, source,
                out hasVisual);
            if (copy == null || !hasVisual) {
                if (copy != null)
                    UnityEngine.Object.Destroy(copy.gameObject);
                return false;
            }

            copy.gameObject.SetActive(false);
            return true;
        }

        private static Transform CreatePackageVisualNode(Transform parent,
            string name, RectTransform source, out bool hasVisual)
        {
            hasVisual = false;
            if (parent == null || source == null)
                return null;

            Image sourceImage = source.GetComponent<Image>();
            RawImage sourceRawImage = sourceImage == null
                ? source.GetComponent<RawImage>() : null;
            GameObject copy = CreatePackageVisualObject(name, sourceImage,
                sourceRawImage);
            if (copy == null)
                return null;

            copy.transform.SetParent(parent, false);
            copy.layer = parent.gameObject.layer;
            RectTransform rect = copy.GetComponent<RectTransform>();
            CopyRectTransform(source, rect);

            if (sourceImage != null) {
                Image image = copy.GetComponent<Image>();
                CopyImage(sourceImage, image);
                hasVisual = image != null &&
                    (image.sprite != null || image.overrideSprite != null);
            } else if (sourceRawImage != null) {
                RawImage rawImage = copy.GetComponent<RawImage>();
                CopyRawImage(sourceRawImage, rawImage);
                hasVisual = rawImage != null && rawImage.texture != null;
            }

            for (int i = 0; i < source.childCount; i++) {
                RectTransform childSource = source.GetChild(i)
                    .GetComponent<RectTransform>();
                if (childSource == null)
                    continue;

                bool childHasVisual;
                Transform childCopy = CreatePackageVisualNode(copy.transform,
                    childSource.name, childSource, out childHasVisual);
                if (childCopy != null)
                    childCopy.gameObject.SetActive(
                        childSource.gameObject.activeSelf);
                hasVisual |= childHasVisual;
            }

            return copy.transform;
        }

        private static GameObject CreatePackageVisualObject(string name,
            Image sourceImage, RawImage sourceRawImage)
        {
#if NET6_0_OR_GREATER
            if (sourceImage != null)
                return new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
            if (sourceRawImage != null)
                return new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(RawImage));
            return new GameObject(name, typeof(RectTransform));
#else
            int componentCount = sourceImage != null || sourceRawImage != null
                ? 3 : 1;
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(componentCount);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            if (componentCount == 3) {
                componentTypes[1] = Il2CppType.Of<CanvasRenderer>();
                componentTypes[2] = sourceImage != null
                    ? Il2CppType.Of<Image>() : Il2CppType.Of<RawImage>();
            }
            return new GameObject(name, componentTypes);
#endif
        }

        private static void CopyRectTransform(RectTransform source,
            RectTransform target)
        {
            if (source == null || target == null)
                return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void CopyImage(Image source, Image target)
        {
            if (source == null || target == null)
                return;

            target.sprite = source.sprite;
            target.overrideSprite = source.overrideSprite;
            target.color = source.color;
            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
            target.fillMethod = source.fillMethod;
            target.fillAmount = source.fillAmount;
            target.fillClockwise = source.fillClockwise;
            target.fillOrigin = source.fillOrigin;
            target.enabled = source.enabled;
            target.raycastTarget = false;
        }

        private static void CopyRawImage(RawImage source, RawImage target)
        {
            if (source == null || target == null)
                return;

            target.texture = source.texture;
            target.uvRect = source.uvRect;
            target.color = source.color;
            target.material = source.material;
            target.enabled = source.enabled;
            target.raycastTarget = false;
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

        private static BaseItem GetRowBaseItem(InventoryItem row)
        {
            if (row == null || row.ButtonAction == null ||
                row.ButtonAction.hash == null)
                return null;

            Il2CppSystem.Object value = row.ButtonAction.hash.GetFromKey("Item");
            if (value == null)
                return null;
            Item item = value.TryCast<Item>();
            if (item != null)
                return item;
            return value.TryCast<GroupItem>();
        }

        private static bool ShouldSuppressRepairIndicators(InventoryItem row)
        {
            if (row == null)
                return true;

            ChoosePartDownWindow window =
                row.GetComponentInParent<ChoosePartDownWindow>();
            return window != null &&
                (TireChangerInventoryFilterFeature.IsSelectionWindow(window) ||
                 WheelBalancerInventoryFilterFeature.IsSelectionWindow(window));
        }

        private static bool IsSupportedChoosePartDownContext(
            InventoryItem row)
        {
            if (row == null)
                return false;
            if (row.GetComponentInParent<ChoosePartDownWindow>() != null ||
                row.GetComponentInParent<ScrapWindow>() != null ||
                row.GetComponentInParent<RepairPartWindow>() != null)
                return true;

            WindowManager windowManager = WindowManager.Instance;
            return windowManager != null &&
                (windowManager.IsWindowActive(WindowID.Scrap) ||
                 windowManager.IsWindowActive(WindowID.RepairPart));
        }

        private static void HideIndicators(InventoryItem row)
        {
            HideRepairabilityIndicator(row);
            HideOwnedCountIndicator(row);
        }

        private static void HideRepairabilityIndicator(InventoryItem row)
        {
            if (row != null)
                row.transform.Find("QrepairIcon")?.gameObject.SetActive(false);
        }

        private static void HideOwnedCountIndicator(InventoryItem row)
        {
            if (row != null)
                row.transform.Find("QownedCount")?.gameObject.SetActive(false);
        }

        private static void HideNativePaintColorBadge(InventoryItem row)
        {
            if (row == null || Main.SettingsEntry == null ||
                !Main.SettingsEntry.Value.hideBodyPartPaintColorBadges)
                return;

            Image colorBadge = row.color;
            if (colorBadge != null)
                colorBadge.gameObject.SetActive(false);
        }

        private static IEnumerator UpdateDeferred(Transform inventoryWindow,
            int instanceId)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            PendingUpdates.Remove(instanceId);
            UpdateSafely(inventoryWindow);
        }

        private static void UpdateSafely(Transform inventoryWindow)
        {
            if (inventoryWindow == null)
                return;

            try {
                Update(inventoryWindow);
            } catch (Exception exception) {
                ModLogger.Log("[InventoryIndicators] Row update failed." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
            }
        }

        public static void Update(Transform inventoryWindow)
        {
            if (inventoryWindow == null || Main.SettingsEntry == null)
                return;

            Settings settings = Main.SettingsEntry.Value;
            bool showRepairability = settings.showPartRepairabilityIndicators;
            bool showOwnedCount = settings.showOwnedPartCountIndicators;
            bool hidePaintColorBadges =
                settings.hideBodyPartPaintColorBadges;
            if (!showRepairability && !showOwnedCount &&
                !hidePaintColorBadges)
                return;

            Sprite ownedSprite = showOwnedCount
                ? InventoryIconProvider.GetWhiteWarehouseIcon() : null;
            Dictionary<string, OwnedPartCache.ConditionBreakdown> pageCounts =
                showOwnedCount
                    ? new Dictionary<string,
                        OwnedPartCache.ConditionBreakdown>(
                            StringComparer.Ordinal)
                    : null;

            foreach (InventoryItem row in
                inventoryWindow.GetComponentsInChildren<InventoryItem>(false)) {
                if (row == null || !row.gameObject.activeInHierarchy)
                    continue;

                if (hidePaintColorBadges)
                    HideNativePaintColorBadge(row);

                BaseInventory baseInventory =
                    row.GetComponentInParent<BaseInventory>();
                BaseItem baseItem = GetRowBaseItem(row);
                bool suppressOwnedCount = baseInventory != null &&
                    baseItem != null && ApplyGroupingPresentation(
                        baseInventory, baseItem, row);

                GameObject repairIcon = showRepairability
                    ? PrepareRepairIcon(row) : null;
                if (!showRepairability)
                    HideRepairabilityIndicator(row);
                HideOwnedCountIndicator(row);

                if (row.IsGroup) {
                    GroupItem group = row.ButtonAction?.hash?.GetFromKey("Item")
                        ?.TryCast<GroupItem>();
                    if (group == null || !showOwnedCount ||
                        suppressOwnedCount)
                        continue;

                    try {
                        string groupKey = PartIdentityComparer.GetKey(group);
                        OwnedPartCache.ConditionBreakdown groupBreakdown;
                        if (!pageCounts.TryGetValue(groupKey,
                            out groupBreakdown)) {
                            groupBreakdown =
                                OwnedPartCache.GetConditionBreakdown(group);
                            pageCounts[groupKey] = groupBreakdown;
                        }
                        if (groupBreakdown.Total != 1 ||
                            !InventoryFilterManager.ShouldHideSingleOwnedCountBadge(
                                baseInventory))
                            UpdateOwnedIcon(row, groupBreakdown, ownedSprite);
                    } catch (Exception exception) {
                        UpdateOwnedIcon(row,
                            default(OwnedPartCache.ConditionBreakdown),
                            ownedSprite);
                        ModLogger.Log("[OwnedParts] Count failed for assembled item." +
                            Environment.NewLine + exception,
                            Types.LoggingLevels.Error);
                    }
                    continue;
                }

                Item item = row.ButtonAction?.hash?.GetFromKey("Item")?.TryCast<Item>();
                if (item == null)
                    continue;

                if (showOwnedCount && !suppressOwnedCount) {
                    try {
                        string key = PartIdentityComparer.GetKey(item);
                        OwnedPartCache.ConditionBreakdown breakdown;
                        if (!pageCounts.TryGetValue(key, out breakdown)) {
                            breakdown = OwnedPartCache.GetConditionBreakdown(item);
                            pageCounts[key] = breakdown;
                        }
                        if (breakdown.Total != 1 ||
                            !InventoryFilterManager.ShouldHideSingleOwnedCountBadge(
                                baseInventory))
                            UpdateOwnedIcon(row, breakdown, ownedSprite);
                    } catch (Exception exception) {
                        UpdateOwnedIcon(row,
                            default(OwnedPartCache.ConditionBreakdown),
                            ownedSprite);
                        ModLogger.Log("[OwnedParts] Count failed for item '" +
                            item.ID + "'." + Environment.NewLine + exception,
                            Types.LoggingLevels.Error);
                    }
                }

                if (!showRepairability || repairIcon == null ||
                    !PartRepairabilityRules.IsRepairable(item))
                    continue;

                Image repairImage = repairIcon.GetComponent<Image>();
                if (repairImage == null)
                    continue;

                repairImage.sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
                if (repairImage.sprite == null) {
                    repairIcon.SetActive(false);
                    continue;
                }

                repairImage.color = Color.white;
                repairIcon.SetActive(true);
                RepairSkillIndicator.Update(repairIcon, item.ID,
                    row.GetComponentInChildren<Text>());
            }
        }

        private static GameObject PrepareRepairIcon(InventoryItem row)
        {
            GameObject icon = row.transform.Find("QrepairIcon")?.gameObject;
            if (icon == null) {
                icon = CreateIconObject(row, "QrepairIcon");
                Image image = icon.GetComponent<Image>();
                if (image != null)
                    image.sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
            }

            RectTransform rect = icon.GetComponent<RectTransform>();
            if (rect != null) {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(10f, -10f);
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(15f, 15f);
            }
            icon.SetActive(false);
            return icon;
        }

        private static GameObject CreateIconObject(InventoryItem row, string name)
        {
            if (row == null)
                return null;
            Image image = NativeUiFactory.CreateImage(row.transform, name,
                null, Color.white, false);
            if (image == null)
                return null;
            image.preserveAspect = true;
            return image.gameObject;
        }

        private static void UpdateOwnedIcon(InventoryItem row,
            OwnedPartCache.ConditionBreakdown breakdown, Sprite sprite)
        {
            GameObject icon = row.transform.Find("QownedCount")?.gameObject;
            if (breakdown.Total <= 0 || sprite == null) {
                icon?.SetActive(false);
                return;
            }

            if (icon == null) {
                icon = CreateIconObject(row, "QownedCount");

                Image image = icon.GetComponent<Image>();
                if (image == null) {
                    icon.SetActive(false);
                    return;
                }
                image.raycastTarget = false;

                Text sourceText = row.GetComponentInChildren<Text>();
                Text countText = NativeUiFactory.CloneText(icon.transform,
                    "Count", sourceText);
                if (countText == null) {
                    icon.SetActive(false);
                    return;
                }

                TextLocalize localize = countText.GetComponent<TextLocalize>();
                if (localize != null)
                    GameObject.Destroy(localize);

                countText.text = string.Empty;
                countText.fontSize = 9;
                countText.resizeTextForBestFit = false;
                countText.alignment = TextAnchor.UpperCenter;
                countText.horizontalOverflow = HorizontalWrapMode.Overflow;
                countText.verticalOverflow = VerticalWrapMode.Overflow;
                countText.supportRichText = true;
                countText.lineSpacing = 0.8f;
                countText.raycastTarget = false;
            }

            Text text = icon.transform.Find("Count")?.GetComponent<Text>();
            Image ownedImage = icon.GetComponent<Image>();
            if (text == null || ownedImage == null) {
                icon.SetActive(false);
                return;
            }

            ownedImage.sprite = sprite;
            ownedImage.color = Color.white;
            text.color = Color.white;

            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 1f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-10f, -10f);
            iconRect.localScale = Vector3.one;
            iconRect.sizeDelta = new Vector2(15f, 15f);

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -7f);
            textRect.sizeDelta = new Vector2(36f, 64f);

            text.text = BuildOwnedCountText(breakdown);
            icon.SetActive(true);
        }

        private static string BuildOwnedCountText(
            OwnedPartCache.ConditionBreakdown breakdown)
        {
            StringBuilder text = new StringBuilder(96);
            AppendCountLine(text, breakdown.Total, TotalCountColor);

            AppendCountLine(text, breakdown.Perfect, PerfectCountColor);
            AppendCountLine(text, breakdown.Condition50To99,
                Condition50To99Color);
            AppendCountLine(text, breakdown.Condition15To49,
                Condition15To49Color);
            AppendCountLine(text, breakdown.ConditionBelow15,
                ConditionBelow15Color);
            return text.ToString();
        }

        private static void AppendCountLine(StringBuilder text, int count,
            string color)
        {
            if (count <= 0)
                return;

            if (text.Length > 0)
                text.Append('\n');
            text.Append("<color=#");
            text.Append(color);
            text.Append('>');
            text.Append(count);
            text.Append("</color>");
        }
    }

    [HarmonyPatch]
    internal static class PartRowIndicatorPatches
    {
        [HarmonyPatch(typeof(BaseInventory), "FillItem",
            new Type[] { typeof(BaseItem), typeof(InventoryItem) })]
        [HarmonyPostfix]
        private static void BaseInventoryFillItemIndicatorsPostfix(
            BaseItem __0, InventoryItem __1)
        {
            PartRowIndicators.UpdateInventoryRepairabilityRow(__0, __1);
        }

        [HarmonyPatch(typeof(BaseInventory), nameof(BaseInventory.DrawPage))]
        [HarmonyPostfix]
        private static void BaseInventoryDrawPagePostfix(BaseInventory __instance)
        {
            PartRowIndicators.ScheduleUpdate(__instance.transform);
        }

        [HarmonyPatch(typeof(BaseInventory), nameof(BaseInventory.RedrawCurrentPage))]
        [HarmonyPostfix]
        private static void BaseInventoryRedrawCurrentPagePostfix(BaseInventory __instance)
        {
            PartRowIndicators.ScheduleUpdate(__instance.transform);
        }

        [HarmonyPatch(typeof(ItemHelper),
            nameof(ItemHelper.FillChoosePartDownItem))]
        [HarmonyPostfix]
        private static void ItemHelperFillChoosePartDownItemPostfix(
            ChoosePartDownItem __0, InventoryItem __1)
        {
            PartRowIndicators.UpdateChoosePartDownRow(__0, __1);
        }
    }

}
