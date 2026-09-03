using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    /// <summary>Renders the improved shopping list as compact inventory-style cards.</summary>
    [HarmonyPatch]
    internal static class ShoppingListPresentationFeature
    {
        internal const string CardRootName = "QShoppingCard";

        private const string CardBackgroundName = "Background";
        private const string CardPhotoName = "Photo";
        private const string CardCaptionName = "Caption";
        private const string CardParametersName = "Parameters";
        private const string RepairIndicatorName = "Repairability";
        private const string OwnedIndicatorName = "Owned";
        private const string OwnedCountName = "Count";
        private const string SelectionBorderName = "QSelectionBorder";
        private const float InventoryCardScale = 0.75f;
        private const float MinimumCardWidth = 80f;
        private const float MinimumCardSpacing = 2f;
        private const float ControlsGap = 2f;
        private const float ControlsHeight = 19f;
        private const float SelectionBorderThickness = 2f;
        private const float IndicatorSize = 15f;
        private const float IndicatorInset = 10f;
        private const float WheelParametersHeight = 11f;
        private const string TotalCountColor = "FFFFFF";
        private const string PerfectCountColor = "66FF33";
        private const string Condition50To99Color = "FFFF00";
        private const string Condition15To49Color = "FF9900";
        private const string ConditionBelow15Color = "FF0000";
        private static readonly Color SelectionBorderColor =
            new Color(1f, 0.65f, 0.04f, 1f);

        private static InventoryItem inventoryTemplate;
        private static GridLayoutGroup configuredGrid;
        private static int originalPaddingLeft;
        private static int originalPaddingRight;

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addShoppingListSorting;
            }
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        private static void ShopListWindowShowPostfix(ShopListWindow __instance,
            bool __result)
        {
            if (__result && IsEnabled)
                ApplyCardPresentation(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), "FillItems")]
        [HarmonyPostfix]
        private static void ShopListWindowFillItemsPostfix(ShopListWindow __instance)
        {
            if (IsEnabled)
                ApplyCardPresentation(__instance);
        }

        internal static bool IsCardPresentationActive(ShopListItem row)
        {
            return row != null && row.transform.Find(CardRootName) != null;
        }

        internal static void UpdateRowCard(ShopListItem row,
            ShopListItemData data)
        {
            if (!IsEnabled || row == null || data == null)
                return;

            RectTransform rowRect = row.GetComponent<RectTransform>();
            if (rowRect == null || rowRect.rect.width <= 0f ||
                rowRect.rect.height <= ControlsHeight)
                return;

            float cardHeight = rowRect.rect.height - ControlsGap - ControlsHeight;
            ConfigureCard(row, data, rowRect.rect.width, cardHeight,
                GetCurrentCardScale(rowRect.rect.width, cardHeight));
        }

        internal static void HideRowCard(ShopListItem row)
        {
            if (row == null)
                return;

            Transform card = row.transform.Find(CardRootName);
            if (card != null)
                card.gameObject.SetActive(false);
        }

        internal static void UpdateQuantityText(ShopListItem row,
            ShopListItemData data)
        {
            if (row == null || row.amount == null || data == null)
                return;

            row.amount.text = "x" + Math.Max(1, data.Amount);
        }

        private static void ApplyCardPresentation(ShopListWindow window)
        {
            try {
                if (window == null || window.shopListItemsParent == null)
                    return;

                Transform parent = window.shopListItemsParent;
                GridLayoutGroup grid = parent.GetComponent<GridLayoutGroup>();
                if (grid == null)
                    grid = parent.GetComponentInChildren<GridLayoutGroup>(true);
                if (grid == null)
                    return;

                RectTransform gridRect = grid.GetComponent<RectTransform>();
                if (gridRect == null)
                    return;

                CaptureOriginalHorizontalPadding(grid);

                InventoryItem template = GetInventoryTemplate();
                Vector2 sourceSize = GetInventoryCardSize(template);
                RectTransform viewport = gridRect.parent != null
                    ? gridRect.parent.GetComponent<RectTransform>() : null;
                float layoutWidth = viewport != null && viewport.rect.width > 0f
                    ? viewport.rect.width : gridRect.rect.width;
                float contentWidth = layoutWidth - originalPaddingLeft -
                    originalPaddingRight;
                if (contentWidth <= 0f)
                    return;

                float scale = InventoryCardScale;
                float cardWidth = sourceSize.x > 0f
                    ? sourceSize.x * scale
                    : Math.Max(MinimumCardWidth,
                        (contentWidth - (MinimumCardSpacing * 4f)) / 5f);
                if (cardWidth > contentWidth && sourceSize.x > 0f) {
                    scale = contentWidth / sourceSize.x;
                    cardWidth = contentWidth;
                }

                int visualColumns = Math.Max(1, Mathf.FloorToInt(
                    (contentWidth + MinimumCardSpacing) /
                    (cardWidth + MinimumCardSpacing)));
                int visibleItemCount =
                    ShoppingListShopFilterFeature.GetVisibleItemCount(window);
                bool singleVisualRow = visibleItemCount > 0 &&
                    visibleItemCount <= visualColumns;

                int leftExtra = 0;
                int rightExtra = 0;
                if (singleVisualRow) {
                    leftExtra = Mathf.RoundToInt(MinimumCardSpacing);
                } else {
                    float occupiedWidth = (cardWidth * visualColumns) +
                        (MinimumCardSpacing * (visualColumns - 1));
                    float remainingWidth = Math.Max(0f,
                        contentWidth - occupiedWidth);
                    leftExtra = Mathf.FloorToInt(remainingWidth * 0.5f);
                    rightExtra = Mathf.CeilToInt(remainingWidth * 0.5f);
                }

                float cardHeight = sourceSize.y > 0f
                    ? sourceSize.y * scale : cardWidth;
                float cellHeight = cardHeight + ControlsGap + ControlsHeight;

                grid.padding.left = originalPaddingLeft + leftExtra;
                grid.padding.right = originalPaddingRight + rightExtra;
                grid.spacing = new Vector2(MinimumCardSpacing,
                    MinimumCardSpacing);
                grid.cellSize = new Vector2(cardWidth, cellHeight);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = singleVisualRow
                    ? Math.Max(1, visibleItemCount) : visualColumns;
                grid.childAlignment = singleVisualRow
                    ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;

                int rowCount = window.shopListItems != null
                    ? window.shopListItems.Count : 0;
                int itemCount = window.items != null
                    ? Math.Min(visibleItemCount, rowCount) : 0;
                for (int index = 0; index < rowCount; index++) {
                    ShopListItem row = window.shopListItems[index];
                    if (row == null)
                        continue;

                    if (index < itemCount) {
                        ShopListItemData data = window.items[index];
                        if (data != null)
                            ConfigureCard(row, data, cardWidth,
                                cardHeight, scale);
                        else
                            HideRowCard(row);
                    } else {
                        HideRowCard(row);
                    }
                }
            } catch (Exception exception) {
                ModLogger.Log(
                    "[ShoppingList] Failed to apply card presentation." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        private static void ConfigureCard(ShopListItem row,
            ShopListItemData data, float cardWidth, float cardHeight,
            float scale)
        {
            if (row == null || data == null)
                return;

            GameObject card = EnsureCardRoot(row);
            if (card == null)
                return;

            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null)
                return;

            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = Vector3.one;
            cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);

            InventoryItem template = GetInventoryTemplate();
            ConfigureBackground(card.transform, template, scale);
            ConfigurePhoto(card.transform, data, template, scale);
            ConfigureCaption(card.transform, row, data, template, scale);
            ConfigureParameters(card.transform, row, data, template, scale);
            ConfigureRepairability(card.transform, data);
            ConfigureOwned(card.transform, data);
            ConfigureNativeRow(row, cardHeight);
            UpdateQuantityText(row, data);
            card.SetActive(true);
        }

        private static GameObject EnsureCardRoot(ShopListItem row)
        {
            Transform existing = row.transform.Find(CardRootName);
            if (existing != null)
                return existing.gameObject;

            GameObject card = CreateRectObject(CardRootName, row.transform);
            if (card == null)
                return null;

            card.transform.SetAsFirstSibling();
            return card;
        }

        private static void ConfigureBackground(Transform card,
            InventoryItem template, float scale)
        {
            GameObject background = EnsureImageObject(card,
                CardBackgroundName);
            if (background == null)
                return;

            Image target = background.GetComponent<Image>();
            RectTransform targetRect = background.GetComponent<RectTransform>();
            Image source = template != null ? template.bgSolid : null;
            if (target == null || targetRect == null)
                return;

            if (source != null) {
                CopyImageStyle(source, target);
                CopyScaledRect(source.GetComponent<RectTransform>(),
                    targetRect, scale);
            } else {
                Stretch(targetRect);
                target.sprite = null;
                target.color = new Color(0f, 0f, 0f, 0.28f);
            }
            target.raycastTarget = false;
            background.SetActive(true);
        }

        private static void ConfigurePhoto(Transform card,
            ShopListItemData data, InventoryItem template, float scale)
        {
            GameObject photo = EnsureImageObject(card, CardPhotoName);
            if (photo == null)
                return;

            Image target = photo.GetComponent<Image>();
            RectTransform targetRect = photo.GetComponent<RectTransform>();
            Image source = template != null ? template.inventoryItem : null;
            if (target == null || targetRect == null)
                return;

            if (source != null) {
                CopyImageStyle(source, target);
                CopyScaledRect(source.GetComponent<RectTransform>(),
                    targetRect, scale);
            } else {
                targetRect.anchorMin = new Vector2(0.08f, 0.20f);
                targetRect.anchorMax = new Vector2(0.92f, 0.94f);
                targetRect.offsetMin = Vector2.zero;
                targetRect.offsetMax = Vector2.zero;
                target.preserveAspect = true;
            }

            GameInventory inventory = Singleton<GameInventory>.Instance;
            target.sprite = inventory != null && !string.IsNullOrEmpty(data.ID)
                ? inventory.GetThumb(data.ID, false) : null;
            target.color = Color.white;
            target.preserveAspect = true;
            target.raycastTarget = false;
            photo.SetActive(true);
        }

        private static void ConfigureCaption(Transform card,
            ShopListItem row, ShopListItemData data, InventoryItem template,
            float scale)
        {
            GameObject captionObject = EnsureTextObject(card,
                CardCaptionName);
            if (captionObject == null)
                return;

            Text target = captionObject.GetComponent<Text>();
            RectTransform targetRect = captionObject.GetComponent<RectTransform>();
            Text source = template != null ? template.caption : null;
            if (target == null || targetRect == null)
                return;

            if (source != null) {
                CopyTextStyle(source, target);
                CopyScaledRect(source.GetComponent<RectTransform>(),
                    targetRect, scale);
                target.fontSize = Math.Max(7,
                    Mathf.RoundToInt(source.fontSize * scale));
            } else if (row.itemName != null) {
                CopyTextStyle(row.itemName, target);
                targetRect.anchorMin = new Vector2(0.04f, 0f);
                targetRect.anchorMax = new Vector2(0.96f, 0.20f);
                targetRect.offsetMin = Vector2.zero;
                targetRect.offsetMax = Vector2.zero;
                target.fontSize = Math.Max(7,
                    Mathf.RoundToInt(row.itemName.fontSize * 0.75f));
            }

            if (data != null &&
                PartIdentityComparer.HasWheelParameters(data.ID))
                targetRect.anchoredPosition +=
                    new Vector2(0f, WheelParametersHeight * 0.5f);

            target.text = GetDisplayName(row, data);
            target.alignment = TextAnchor.MiddleCenter;
            target.resizeTextForBestFit = true;
            target.resizeTextMinSize = Math.Max(6, target.fontSize - 3);
            target.resizeTextMaxSize = target.fontSize;
            target.horizontalOverflow = HorizontalWrapMode.Wrap;
            target.verticalOverflow = VerticalWrapMode.Truncate;
            target.raycastTarget = false;
            captionObject.SetActive(true);
        }

        private static void ConfigureParameters(Transform card,
            ShopListItem row, ShopListItemData data, InventoryItem template,
            float scale)
        {
            GameObject parametersObject = EnsureTextObject(card,
                CardParametersName);
            if (parametersObject == null)
                return;

            Text target = parametersObject.GetComponent<Text>();
            RectTransform targetRect =
                parametersObject.GetComponent<RectTransform>();
            if (target == null || targetRect == null || data == null ||
                !PartIdentityComparer.HasWheelParameters(data.ID)) {
                parametersObject.SetActive(false);
                return;
            }

            string parameters = row != null ? row.GetBonusText() : null;
            if (string.IsNullOrEmpty(parameters)) {
                parametersObject.SetActive(false);
                return;
            }

            Text source = template != null ? template.caption : null;
            if (source != null)
                CopyTextStyle(source, target);
            else if (row != null && row.itemName != null)
                CopyTextStyle(row.itemName, target);

            int sourceFontSize = source != null ? source.fontSize :
                (row != null && row.itemName != null
                    ? row.itemName.fontSize : 10);
            target.fontSize = Math.Max(6,
                Mathf.RoundToInt(sourceFontSize * scale * 0.72f));
            target.text = parameters.Trim();
            target.color = Color.white;
            target.alignment = TextAnchor.MiddleCenter;
            target.resizeTextForBestFit = true;
            target.resizeTextMinSize = 6;
            target.resizeTextMaxSize = target.fontSize;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.verticalOverflow = VerticalWrapMode.Truncate;
            target.raycastTarget = false;

            targetRect.anchorMin = new Vector2(0.04f, 0f);
            targetRect.anchorMax = new Vector2(0.96f, 0f);
            targetRect.pivot = new Vector2(0.5f, 0f);
            targetRect.anchoredPosition = new Vector2(0f, 1f);
            targetRect.sizeDelta = new Vector2(0f, WheelParametersHeight);
            targetRect.localScale = Vector3.one;
            parametersObject.SetActive(true);
        }

        private static string GetDisplayName(ShopListItem row,
            ShopListItemData data)
        {
            GameInventory inventory = Singleton<GameInventory>.Instance;
            if (inventory != null && data != null &&
                !string.IsNullOrEmpty(data.ID)) {
                string localized = inventory.GetLocalizedName(data.ID);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }

            return row != null && row.itemName != null
                ? row.itemName.text : string.Empty;
        }

        private static void ConfigureRepairability(Transform card,
            ShopListItemData data)
        {
            GameObject icon = EnsureImageObject(card, RepairIndicatorName);
            if (icon == null)
                return;

            Image image = icon.GetComponent<Image>();
            RectTransform rect = icon.GetComponent<RectTransform>();
            if (image == null || rect == null || data == null ||
                string.IsNullOrEmpty(data.ID)) {
                icon.SetActive(false);
                return;
            }

            if (!PartRepairabilityRules.IsRepairable(data.ID)) {
                icon.SetActive(false);
                return;
            }

            Sprite sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
            if (sprite == null) {
                icon.SetActive(false);
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(IndicatorInset,
                -IndicatorInset);
            rect.sizeDelta = new Vector2(IndicatorSize, IndicatorSize);
            rect.localScale = Vector3.one;
            icon.SetActive(true);
            RepairSkillIndicator.Update(icon, data.ID,
                card.Find(CardCaptionName)?.GetComponent<Text>());
        }

        private static void ConfigureOwned(Transform card,
            ShopListItemData data)
        {
            GameObject icon = EnsureOwnedIndicator(card);
            if (icon == null)
                return;

            OwnedPartCache.ConditionBreakdown breakdown =
                GetOwnedConditionBreakdown(data);
            Image image = icon.GetComponent<Image>();
            RectTransform rect = icon.GetComponent<RectTransform>();
            Text count = icon.transform.Find(OwnedCountName)?.GetComponent<Text>();
            if (image == null || rect == null || count == null) {
                icon.SetActive(false);
                return;
            }

            Sprite sprite = breakdown.Total > 0
                ? InventoryIconProvider.GetWhiteWarehouseIcon()
                : InventoryIconProvider.GetRedWarehouseIcon();
            if (sprite == null) {
                icon.SetActive(false);
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-IndicatorInset,
                -IndicatorInset);
            rect.sizeDelta = new Vector2(IndicatorSize, IndicatorSize);
            rect.localScale = Vector3.one;

            count.text = breakdown.Total > 0
                ? BuildOwnedCountText(breakdown) : string.Empty;
            count.gameObject.SetActive(breakdown.Total > 0);
            icon.SetActive(true);
        }

        private static GameObject EnsureOwnedIndicator(Transform card)
        {
            GameObject icon = EnsureImageObject(card, OwnedIndicatorName);
            if (icon == null)
                return null;

            Transform existing = icon.transform.Find(OwnedCountName);
            if (existing != null)
                return icon;

            GameObject countObject = EnsureTextObject(icon.transform,
                OwnedCountName);
            if (countObject == null)
                return icon;

            Text text = countObject.GetComponent<Text>();
            RectTransform rect = countObject.GetComponent<RectTransform>();
            InventoryItem template = GetInventoryTemplate();
            Text source = template != null ? template.caption : null;
            if (text == null || rect == null)
                return icon;

            if (source != null)
                CopyTextStyle(source, text);
            text.fontSize = 9;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.lineSpacing = 0.8f;
            text.raycastTarget = false;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -7f);
            rect.sizeDelta = new Vector2(36f, 64f);
            rect.localScale = Vector3.one;
            return icon;
        }

        private static OwnedPartCache.ConditionBreakdown
            GetOwnedConditionBreakdown(ShopListItemData data)
        {
            ShopListItemDataEx additional = data != null
                ? data.AdditionalData : null;
            return OwnedPartCache.GetConditionBreakdown(
                data != null ? data.ID : null,
                additional != null ? additional.ET : 0,
                additional != null ? additional.Profile : 0,
                additional != null ? additional.Size : 0,
                additional != null ? additional.Width : 0);
        }

        private static string BuildOwnedCountText(
            OwnedPartCache.ConditionBreakdown breakdown)
        {
            System.Text.StringBuilder text =
                new System.Text.StringBuilder(96);
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

        private static void AppendCountLine(System.Text.StringBuilder text,
            int count, string color)
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

        private static void ConfigureNativeRow(ShopListItem row,
            float cardHeight)
        {
            if (row == null)
                return;

            if (row.background != null)
                row.background.SetActive(false);
            if (row.textContainer != null)
                row.textContainer.gameObject.SetActive(false);
            if (row.itemName != null)
                row.itemName.gameObject.SetActive(false);
            if (row.amount != null)
                row.amount.gameObject.SetActive(true);

            ConfigureSelectionBorder(row.selected, cardHeight);
            ConfigureSelectionBorder(row.selectedBox, cardHeight);
        }

        private static void ConfigureSelectionBorder(GameObject selected,
            float cardHeight)
        {
            if (selected == null || cardHeight <= 0f)
                return;

            RectTransform selectedRect = selected.GetComponent<RectTransform>();
            if (selectedRect != null)
                Stretch(selectedRect);

            Transform existingBorder = selected.transform.Find(
                SelectionBorderName);
            Image[] nativeImages =
                selected.GetComponentsInChildren<Image>(true);
            foreach (Image image in nativeImages) {
                if (image == null || (existingBorder != null &&
                    image.transform.IsChildOf(existingBorder)))
                    continue;

                image.enabled = false;
                image.raycastTarget = false;
            }

            GameObject border = existingBorder != null
                ? existingBorder.gameObject
                : CreateRectObject(SelectionBorderName, selected.transform);
            if (border == null)
                return;

            RectTransform borderRect = border.GetComponent<RectTransform>();
            if (borderRect == null)
                return;

            borderRect.anchorMin = new Vector2(0f, 1f);
            borderRect.anchorMax = new Vector2(1f, 1f);
            borderRect.pivot = new Vector2(0.5f, 1f);
            borderRect.anchoredPosition = Vector2.zero;
            borderRect.sizeDelta = new Vector2(0f, cardHeight);
            borderRect.localScale = Vector3.one;

            ConfigureBorderEdge(border.transform, "Top",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, SelectionBorderThickness));
            ConfigureBorderEdge(border.transform, "Bottom",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, SelectionBorderThickness));
            ConfigureBorderEdge(border.transform, "Left",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(SelectionBorderThickness, 0f));
            ConfigureBorderEdge(border.transform, "Right",
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(SelectionBorderThickness, 0f));
            border.transform.SetAsLastSibling();
            border.SetActive(true);
        }

        private static void ConfigureBorderEdge(Transform border, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 sizeDelta)
        {
            GameObject edge = EnsureImageObject(border, name);
            if (edge == null)
                return;

            Image image = edge.GetComponent<Image>();
            RectTransform rect = edge.GetComponent<RectTransform>();
            if (image == null || rect == null)
                return;

            image.enabled = true;
            image.sprite = null;
            image.color = SelectionBorderColor;
            image.raycastTarget = false;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            edge.SetActive(true);
        }

        private static void CaptureOriginalHorizontalPadding(
            GridLayoutGroup grid)
        {
            if (grid == null || configuredGrid == grid)
                return;

            configuredGrid = grid;
            originalPaddingLeft = grid.padding.left;
            originalPaddingRight = grid.padding.right;
        }

        private static InventoryItem GetInventoryTemplate()
        {
            if (IsUsableTemplate(inventoryTemplate))
                return inventoryTemplate;

            inventoryTemplate = null;
            try {
                Il2CppReferenceArray<UnityEngine.Object> windows =
                    Resources.FindObjectsOfTypeAll(
                        Il2CppType.Of<InventoryWindow>());
                foreach (UnityEngine.Object value in windows) {
                    InventoryWindow window = value.TryCast<InventoryWindow>();
                    if (window == null || window.itemObjects == null)
                        continue;

                    foreach (InventoryItem item in window.itemObjects) {
                        if (!IsUsableTemplate(item))
                            continue;

                        inventoryTemplate = item;
                        return inventoryTemplate;
                    }
                }

                Il2CppReferenceArray<UnityEngine.Object> loaded =
                    Resources.FindObjectsOfTypeAll(
                        Il2CppType.Of<InventoryItem>());
                foreach (UnityEngine.Object value in loaded) {
                    InventoryItem item = value.TryCast<InventoryItem>();
                    if (!IsUsableTemplate(item))
                        continue;

                    inventoryTemplate = item;
                    break;
                }
            } catch {
            }
            return inventoryTemplate;
        }

        private static bool IsUsableTemplate(InventoryItem item)
        {
            if (item == null || item.gameObject == null ||
                item.inventoryItem == null || item.caption == null)
                return false;

            RectTransform rect = item.GetComponent<RectTransform>();
            return rect != null &&
                (rect.rect.width > 0f || rect.sizeDelta.x > 0f);
        }

        private static Vector2 GetInventoryCardSize(InventoryItem template)
        {
            if (template == null)
                return Vector2.zero;

            RectTransform rect = template.GetComponent<RectTransform>();
            if (rect == null)
                return Vector2.zero;

            float width = rect.rect.width > 0f
                ? rect.rect.width : Mathf.Abs(rect.sizeDelta.x);
            float height = rect.rect.height > 0f
                ? rect.rect.height : Mathf.Abs(rect.sizeDelta.y);
            return new Vector2(width, height);
        }

        private static float GetCurrentCardScale(float cardWidth,
            float cardHeight)
        {
            Vector2 source = GetInventoryCardSize(GetInventoryTemplate());
            if (source.x <= 0f || source.y <= 0f)
                return InventoryCardScale;

            return Mathf.Min(cardWidth / source.x, cardHeight / source.y);
        }

        private static GameObject EnsureImageObject(Transform parent,
            string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null)
                return existing.gameObject;

#if NET6_0_OR_GREATER
            GameObject value = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(3);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            componentTypes[1] = Il2CppType.Of<CanvasRenderer>();
            componentTypes[2] = Il2CppType.Of<Image>();
            GameObject value = new GameObject(name, componentTypes);
#endif
            value.transform.SetParent(parent, false);
            value.layer = parent.gameObject.layer;
            return value;
        }

        private static GameObject EnsureTextObject(Transform parent,
            string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null)
                return existing.gameObject;

#if NET6_0_OR_GREATER
            GameObject value = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(3);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            componentTypes[1] = Il2CppType.Of<CanvasRenderer>();
            componentTypes[2] = Il2CppType.Of<Text>();
            GameObject value = new GameObject(name, componentTypes);
#endif
            value.transform.SetParent(parent, false);
            value.layer = parent.gameObject.layer;
            return value;
        }

        private static GameObject CreateRectObject(string name,
            Transform parent)
        {
#if NET6_0_OR_GREATER
            GameObject value = new GameObject(name, typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            GameObject value = new GameObject(name, componentTypes);
#endif
            value.transform.SetParent(parent, false);
            value.layer = parent.gameObject.layer;
            return value;
        }

        private static void CopyScaledRect(RectTransform source,
            RectTransform target, float scale)
        {
            if (source == null || target == null)
                return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition * scale;
            target.sizeDelta = source.sizeDelta * scale;
            target.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CopyImageStyle(Image source, Image target)
        {
            if (source == null || target == null)
                return;

            target.sprite = source.sprite;
            target.color = source.color;
            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
        }

        private static void CopyTextStyle(Text source, Text target)
        {
            if (source == null || target == null)
                return;

            target.font = source.font;
            target.fontSize = source.fontSize;
            target.fontStyle = source.fontStyle;
            target.color = source.color;
            target.material = source.material;
            target.lineSpacing = source.lineSpacing;
            target.alignment = source.alignment;
            target.horizontalOverflow = source.horizontalOverflow;
            target.verticalOverflow = source.verticalOverflow;
            target.resizeTextForBestFit = source.resizeTextForBestFit;
            target.resizeTextMinSize = source.resizeTextMinSize;
            target.resizeTextMaxSize = source.resizeTextMaxSize;
            target.supportRichText = source.supportRichText;
        }
    }
}
