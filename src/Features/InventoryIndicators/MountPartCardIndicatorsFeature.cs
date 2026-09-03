using System;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppCMS.UI.Windows;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class MountPartCardIndicatorsFeature
    {
        private const string RepairIndicatorName = "QMountPartRepairability";
        private const string OwnedIndicatorName = "QMountPartOwned";
        private const string OwnedCountName = "Count";
        private const string TotalCountColor = "FFFFFF";
        private const string PerfectCountColor = "66FF33";
        private const string Condition50To99Color = "FFFF00";
        private const string Condition15To49Color = "FF9900";
        private const string ConditionBelow15Color = "FF0000";

        private static bool IsEnabled {
            get {
                return GlobalState.IsGarageSceneActive &&
                    Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.showMountPartCardIndicators;
            }
        }

        [HarmonyPatch(typeof(PartInspectorWindow), "Prepare")]
        [HarmonyPostfix]
        private static void PreparePostfix(PartInspectorWindow __instance,
            PartScript __0)
        {
            if (__instance == null)
                return;

            gameMode mode = GetCurrentMode();
            if (!IsEnabled || __0 == null || !IsSupportedMode(mode)) {
                HideIndicators(__instance);
                return;
            }

            try {
                UpdateIndicators(__instance, __0);
            } catch (Exception exception) {
                HideIndicators(__instance);
                ModLogger.Log("[MountPartCardIndicators] Failed to update part " +
                    "card indicators." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
            }
        }

        [HarmonyPatch(typeof(PartInspectorWindow),
            nameof(PartInspectorWindow.Hide))]
        [HarmonyPostfix]
        private static void HidePostfix(PartInspectorWindow __instance)
        {
            HideIndicators(__instance);
        }

        private static void UpdateIndicators(PartInspectorWindow inspector,
            PartScript part)
        {
            Transform root = GetInspectorRoot(inspector);
            if (root == null)
                return;

            PartProperty property = part.partProperty;
            string itemId = property != null ? property.ID : string.Empty;
            bool repairable = PartRepairabilityRules.IsRepairable(itemId);
            OwnedPartCache.ConditionBreakdown owned =
                OwnedPartCache.GetConditionBreakdown(itemId, 0, 0, 0, 0);

            UpdateRepairIndicator(root, itemId, repairable);
            UpdateOwnedIndicator(root, owned);
        }

        private static void UpdateRepairIndicator(Transform root,
            string itemId, bool repairable)
        {
            GameObject icon = FindIndicator(root, RepairIndicatorName);
            Sprite sprite = repairable
                ? InventoryIconProvider.GetWhiteRepairWrenchIcon() : null;
            if (sprite == null) {
                if (icon != null)
                    icon.SetActive(false);
                return;
            }

            if (icon == null)
                icon = CreateImageObject(root, RepairIndicatorName);
            if (icon == null)
                return;

            Image image = icon.GetComponent<Image>();
            RectTransform rect = icon.GetComponent<RectTransform>();
            if (image == null || rect == null) {
                icon.SetActive(false);
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            ConfigureRepairIndicatorRect(rect);
            icon.SetActive(true);
            RepairSkillIndicator.Update(icon, itemId,
                root.Find("Condition/ConditionPercentage")?.GetComponent<Text>());
        }

        private static void UpdateOwnedIndicator(Transform root,
            OwnedPartCache.ConditionBreakdown breakdown)
        {
            GameObject icon = FindIndicator(root, OwnedIndicatorName);
            Sprite sprite = breakdown.Total > 0
                ? InventoryIconProvider.GetWhiteWarehouseIcon()
                : InventoryIconProvider.GetRedWarehouseIcon();
            if (sprite == null) {
                if (icon != null)
                    icon.SetActive(false);
                return;
            }

            if (icon == null)
                icon = CreateOwnedIndicator(root);
            if (icon == null)
                return;

            Image image = icon.GetComponent<Image>();
            RectTransform rect = icon.GetComponent<RectTransform>();
            Text text = icon.transform.Find(OwnedCountName)
                ?.GetComponent<Text>();
            if (image == null || rect == null || text == null) {
                icon.SetActive(false);
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            ConfigureOwnedIndicatorRect(rect);
            ConfigureOwnedCountRect(text.GetComponent<RectTransform>());
            text.text = breakdown.Total > 0
                ? BuildOwnedCountText(breakdown)
                : string.Empty;
            text.gameObject.SetActive(breakdown.Total > 0);
            icon.SetActive(true);
        }

        private static GameObject CreateOwnedIndicator(Transform root)
        {
            GameObject icon = CreateImageObject(root, OwnedIndicatorName);
            if (icon == null)
                return null;

            Text source = root.Find("Condition/ConditionPercentage")
                ?.GetComponent<Text>();
            if (source == null) {
                UnityEngine.Object.Destroy(icon);
                return null;
            }

            GameObject countObject = CreateTextObject(icon.transform,
                OwnedCountName);
            if (countObject == null) {
                UnityEngine.Object.Destroy(icon);
                return null;
            }

            Text text = countObject.GetComponent<Text>();
            RectTransform rect = countObject.GetComponent<RectTransform>();
            if (text == null || rect == null) {
                UnityEngine.Object.Destroy(icon);
                return null;
            }

            text.font = source.font;
            text.fontStyle = source.fontStyle;
            text.fontSize = 9;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.lineSpacing = 0.8f;
            text.raycastTarget = false;

            ConfigureOwnedCountRect(rect);
            return icon;
        }

        private static GameObject CreateImageObject(Transform parent,
            string name)
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
            icon.transform.SetParent(parent, false);
            icon.layer = parent.gameObject.layer;
            Image image = icon.GetComponent<Image>();
            if (image != null) {
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            return icon;
        }

        private static GameObject CreateTextObject(Transform parent,
            string name)
        {
#if NET6_0_OR_GREATER
            GameObject textObject = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(3);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            componentTypes[1] = Il2CppType.Of<CanvasRenderer>();
            componentTypes[2] = Il2CppType.Of<Text>();
            GameObject textObject = new GameObject(name, componentTypes);
#endif
            textObject.transform.SetParent(parent, false);
            textObject.layer = parent.gameObject.layer;
            return textObject;
        }

        private static void ConfigureRepairIndicatorRect(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(10f, -10f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(15f, 15f);
        }

        private static void ConfigureOwnedIndicatorRect(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(15f, 15f);
        }

        private static void ConfigureOwnedCountRect(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -7f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(36f, 64f);
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

        private static GameObject FindIndicator(Transform root, string name)
        {
            Transform child = root != null ? root.Find(name) : null;
            return child != null ? child.gameObject : null;
        }

        private static Transform GetInspectorRoot(PartInspectorWindow inspector)
        {
            return inspector != null && inspector.transform != null
                ? inspector.transform.Find("Inspector") : null;
        }

        private static void HideIndicators(PartInspectorWindow inspector)
        {
            Transform root = GetInspectorRoot(inspector);
            if (root == null)
                return;

            GameObject repair = FindIndicator(root, RepairIndicatorName);
            if (repair != null)
                repair.SetActive(false);
            GameObject owned = FindIndicator(root, OwnedIndicatorName);
            if (owned != null)
                owned.SetActive(false);
        }

        private static gameMode GetCurrentMode()
        {
            GameMode gameModeManager = GameMode.Get();
            return gameModeManager != null
                ? gameModeManager.GetCurrentMode() : default(gameMode);
        }

        private static bool IsSupportedMode(gameMode mode)
        {
            return mode == gameMode.PartSelect ||
                mode == gameMode.PartSelectMount;
        }
    }
}
