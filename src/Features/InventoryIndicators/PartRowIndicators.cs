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
        /// These rows are used by scrap production, scrap upgrades and repair tables.
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
                    settings.showPartRepairabilityIndicators;
                bool showOwnedCount = settings.showOwnedPartCountIndicators;
                if (!showRepairability && !showOwnedCount) {
                    HideIndicators(row);
                    return;
                }

                GameObject repairIcon = showRepairability
                    ? PrepareRepairIcon(row) : null;
                if (!showRepairability)
                    HideRepairabilityIndicator(row);
                HideOwnedCountIndicator(row);

                GroupItem group = entry.BaseItem.TryCast<GroupItem>();
                if (group != null) {
                    if (showOwnedCount) {
                        UpdateOwnedIcon(row,
                            OwnedPartCache.GetConditionBreakdown(group),
                            InventoryIconProvider.GetWhiteWarehouseIcon());
                    }
                    return;
                }

                Item item = entry.BaseItem.TryCast<Item>();
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
                    InventoryIconProvider.GetRepairWrenchIconForCondition(
                        item.ConditionToShow);
                if (repairImage.sprite == null) {
                    repairIcon.SetActive(false);
                    return;
                }

                repairImage.color = Color.white;
                repairIcon.SetActive(true);
            } catch (Exception exception) {
                HideIndicators(row);
                ModLogger.Log("[InventoryIndicators] Choose-part row update " +
                    "failed." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
            }
        }

        private static bool IsSupportedChoosePartDownContext(
            InventoryItem row)
        {
            if (row.GetComponentInParent<ScrapWindow>() != null ||
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

                GameObject repairIcon = showRepairability
                    ? PrepareRepairIcon(row) : null;
                if (!showRepairability)
                    HideRepairabilityIndicator(row);
                HideOwnedCountIndicator(row);

                if (row.IsGroup) {
                    GroupItem group = row.ButtonAction?.hash?.GetFromKey("Item")
                        ?.TryCast<GroupItem>();
                    if (group == null || !showOwnedCount)
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

                if (showOwnedCount) {
                    try {
                        string key = PartIdentityComparer.GetKey(item);
                        OwnedPartCache.ConditionBreakdown breakdown;
                        if (!pageCounts.TryGetValue(key, out breakdown)) {
                            breakdown = OwnedPartCache.GetConditionBreakdown(item);
                            pageCounts[key] = breakdown;
                        }
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

                repairImage.sprite = InventoryIconProvider.GetRepairWrenchIconForCondition(
                    item.ConditionToShow);
                if (repairImage.sprite == null) {
                    repairIcon.SetActive(false);
                    continue;
                }

                repairImage.color = Color.white;
                repairIcon.SetActive(true);
            }
        }

        private static GameObject PrepareRepairIcon(InventoryItem row)
        {
            GameObject icon = row.transform.Find("QrepairIcon")?.gameObject;
            if (icon == null) {
                icon = CreateIconObject(row, "QrepairIcon");
                Image image = icon.GetComponent<Image>();
                if (image != null)
                    image.sprite = InventoryIconProvider.GetGreenRepairWrenchIcon();
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
#if NET6_0_OR_GREATER
            GameObject icon = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(3);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            componentTypes[1] = Il2CppType.Of<CanvasRenderer>();
            componentTypes[2] = Il2CppType.Of<Image>();
            GameObject icon = new GameObject(name, componentTypes);
#endif
            icon.transform.SetParent(row.transform, false);
            icon.layer = row.gameObject.layer;
            Image image = icon.GetComponent<Image>();
            if (image != null) {
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            return icon;
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
                if (sourceText == null) {
                    icon.SetActive(false);
                    return;
                }

                GameObject countObject = GameObject.Instantiate(sourceText.gameObject, icon.transform);
                countObject.name = "Count";
                countObject.transform.localScale = Vector3.one;

                TextLocalize localize = countObject.GetComponent<TextLocalize>();
                if (localize != null)
                    GameObject.Destroy(localize);

                Text countText = countObject.GetComponent<Text>();
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
            int populatedConditionGroups = 0;
            if (breakdown.Perfect > 0)
                populatedConditionGroups++;
            if (breakdown.Condition50To99 > 0)
                populatedConditionGroups++;
            if (breakdown.Condition15To49 > 0)
                populatedConditionGroups++;
            if (breakdown.ConditionBelow15 > 0)
                populatedConditionGroups++;

            StringBuilder text = new StringBuilder(96);
            if (populatedConditionGroups > 1)
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
