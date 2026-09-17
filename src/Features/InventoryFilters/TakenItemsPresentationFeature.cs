using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.TakenItems;
using Il2CppCMS.UI.Windows;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Logic.TakenItems;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class TakenItemsPresentationFeature
    {
        private const string CellName = "QTakenItemCard";
        private const string FilterRootName = "QTakenItemFilters";
        private const string SearchFieldName = "QTakenItemsSearchField";
        private const string PriceRangePopupName = "QTakenItemsPriceRange";
        private const float CardScale = 0.75f;
        private const float Spacing = 2f;
        private const float FooterHeight = 19f;
        private const float ButtonSize = 20f;
        private const float ButtonSpacing = 28f;
        private const float ToolbarRightX = 403f;
        private const float ToolbarGap = 8f;
        private const float PriceRangeWidth = 220f;
        private const float PriceRangeHeight = ButtonSize;
        private const float PriceRangeHandleWidth = 24f;
        private static readonly Color32 ActiveButtonColor =
            new Color32(255, 255, 255, 255);
        private static readonly Color32 DisabledButtonColor =
            new Color32(155, 155, 155, 210);
        private static readonly List<CardEntry> Cards = new List<CardEntry>();
        private static InventoryItem inventoryTemplate;
        private static JunkyardConditionFilterMode conditionMode;
        private static RepairabilityQuickFilterMode repairabilityMode;
        private static readonly RepairSpecializationFilterState
            repairSpecializationFilter = new RepairSpecializationFilterState();
        private static QualityQuickFilterMode qualityMode;
        private static OwnedQuickFilterMode ownedMode;
        private static Image conditionButton;
        private static Image repairButton;
        private static Image qualityButton;
        private static Image ownedButton;
        private static Text priceButtonLabel;
        private static TakenItemsWindow activeWindow;
        private static InputField searchField;
        private static GameObject priceRangePopup;
        private static GameObject priceRangeBlocker;
        private static EventTrigger priceRangeBlockerTrigger;
        private static Slider minimumPriceSlider;
        private static Slider maximumPriceSlider;
        private static Text minimumPriceValue;
        private static Text maximumPriceValue;
        private static RectTransform priceRangeSelection;
        private static string searchText = string.Empty;
        private static int minimumPrice;
        private static int maximumPrice;
        private static int maximumAvailablePrice;

        private sealed class CardEntry
        {
            internal GameObject Cell;
            internal BaseItem Item;
            internal TakenListItem NativeRow;
            internal int OriginalIndex;
            internal int Price;
        }

        [HarmonyPatch(typeof(TakenItemsWindow), "FillItems",
            new Type[] { typeof(Il2CppSystem.Collections.Generic.List<BaseItem>) })]
        [HarmonyPostfix]
        private static void FillItemsPostfix(TakenItemsWindow __instance,
            Il2CppSystem.Collections.Generic.List<BaseItem> __0)
        {
            if (__instance == null || __0 == null ||
                Main.SettingsEntry == null ||
                !Main.SettingsEntry.Value.addTakenItemsPresentation)
                return;

            Apply(__instance, __0);
        }

        [HarmonyPatch(typeof(TakenItemsWindow), nameof(TakenItemsWindow.Hide))]
        [HarmonyPostfix]
        private static void HidePostfix()
        {
            InventoryFilterManager.TryCloseQuickFilterMenu();
            ClosePriceRangePopup();
            activeWindow = null;
        }

        internal static void OnSceneUnloaded(string sceneName)
        {
            if (!string.Equals(sceneName, "Junkyard",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sceneName, "Barn",
                    StringComparison.OrdinalIgnoreCase))
                return;

            conditionMode = JunkyardConditionFilterMode.Off;
            repairabilityMode = RepairabilityQuickFilterMode.Off;
            repairSpecializationFilter.Reset();
            qualityMode = QualityQuickFilterMode.Off;
            ownedMode = OwnedQuickFilterMode.Off;
            searchText = string.Empty;
            minimumPrice = 0;
            maximumPrice = 0;
            maximumAvailablePrice = 0;
            InventoryFilterManager.TryCloseQuickFilterMenu();
            ClosePriceRangePopup();
            ClearCards();
            searchField = null;
            conditionButton = null;
            repairButton = null;
            qualityButton = null;
            ownedButton = null;
            priceButtonLabel = null;
            activeWindow = null;
        }

        [HarmonyPatch(typeof(TakenItemsWindow), "BuyPartsAction")]
        [HarmonyPrefix]
        private static void BuyPartsActionPrefix(TakenItemsWindow __instance)
        {
            if (__instance == null || __instance != activeWindow ||
                Cards.Count == 0)
                return;

            List<int> hiddenIndices = new List<int>();
            for (int index = 0; index < Cards.Count; index++) {
                CardEntry entry = Cards[index];
                if (entry.Cell != null && !entry.Cell.activeSelf)
                    hiddenIndices.Add(entry.OriginalIndex);
            }

            for (int index = hiddenIndices.Count - 1; index >= 0; index--) {
                int itemIndex = hiddenIndices[index];
                __instance.OnTrashButtonClick(itemIndex % 2, itemIndex / 2);
            }
        }

        [HarmonyPatch(typeof(TakenItemsWindow), "RemoveCurrentItemAction")]
        [HarmonyPrefix]
        private static bool RemoveCurrentItemActionPrefix(
            TakenItemsWindow __instance)
        {
            if (__instance == null || __instance != activeWindow ||
                Cards.Count == 0)
                return true;
            if (searchField != null && searchField.isFocused)
                return false;

            CardEntry entry = GetSelectedVisibleCard(__instance);
            if (entry == null)
                return false;

            __instance.OnTrashButtonClick(
                entry.OriginalIndex % 2, entry.OriginalIndex / 2);
            return false;
        }

        private static void Apply(TakenItemsWindow window,
            Il2CppSystem.Collections.Generic.List<BaseItem> items)
        {
            try {
                Transform parent = window.takenListItemsParent;
                GridLayoutGroup grid = parent != null
                    ? parent.GetComponent<GridLayoutGroup>() : null;
                RectTransform parentRect = parent != null
                    ? parent.GetComponent<RectTransform>() : null;
                InventoryItem template = GetInventoryTemplate();
                if (grid == null || parentRect == null || template == null)
                    return;

                activeWindow = window;
                ClosePriceRangePopup();
                ClearCards();
                HideNativeRows(window);
                ConfigureGrid(grid, parentRect, template);

                int nativeCount = window.takenListItems != null
                    ? window.takenListItems.Count : 0;
                int count = Math.Min(items.Count, nativeCount);
                for (int index = 0; index < count; index++) {
                    BaseItem item = items[index];
                    TakenListItem nativeRow = window.takenListItems[index];
                    if (item == null || nativeRow == null)
                        continue;

                    GameObject cell = CreateCell(window, parent, template,
                        nativeRow, item, index);
                    if (cell != null) {
                        Cards.Add(new CardEntry {
                            Cell = cell,
                            Item = item,
                            NativeRow = nativeRow,
                            OriginalIndex = index,
                            Price = GetRowPrice(nativeRow),
                        });
                    }
                }

                UpdatePriceBounds();
                if (Main.SettingsEntry.Value.addQuickFilters) {
                    EnsureFilters(window);
                    EnsureSearchField(window);
                    LayoutToolbar(window);
                    ApplyFilters();
                } else {
                    HideFilters(window);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            } catch (Exception exception) {
                ModLogger.Log("[TakenItems] Failed to apply card presentation." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        private static void ConfigureGrid(GridLayoutGroup grid,
            RectTransform parentRect, InventoryItem template)
        {
            RectTransform source = template.GetComponent<RectTransform>();
            if (source == null)
                return;

            float sourceWidth = source.rect.width > 0f
                ? source.rect.width : Mathf.Abs(source.sizeDelta.x);
            float sourceHeight = source.rect.height > 0f
                ? source.rect.height : Mathf.Abs(source.sizeDelta.y);
            float width = sourceWidth * CardScale;
            float height = sourceHeight * CardScale + Spacing + FooterHeight;
            int columns = Math.Max(1, Mathf.FloorToInt(
                (parentRect.rect.width + Spacing) / (width + Spacing)));

            grid.cellSize = new Vector2(width, height);
            grid.spacing = new Vector2(Spacing, Spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        private static GameObject CreateCell(TakenItemsWindow window,
            Transform parent, InventoryItem template, TakenListItem nativeRow,
            BaseItem item, int index)
        {
#if NET6_0_OR_GREATER
            GameObject cell = new GameObject(CellName, typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> types =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            types[0] = Il2CppType.Of<RectTransform>();
            GameObject cell = new GameObject(CellName, types);
#endif
            cell.transform.SetParent(parent, false);
            cell.layer = parent.gameObject.layer;

            GameObject cardObject = GameObject.Instantiate(
                template.gameObject, cell.transform);
            cardObject.name = "Card";
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            InventoryItem card = cardObject.GetComponent<InventoryItem>();
            if (cardRect == null || card == null) {
                GameObject.Destroy(cell);
                return null;
            }

            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = new Vector3(CardScale, CardScale, 1f);
            cardObject.SetActive(true);
            ConfigureCard(card, nativeRow, item);
            ConfigureFooter(window, cell.transform, nativeRow, index);
            return cell;
        }

        private static void ConfigureCard(InventoryItem card,
            TakenListItem nativeRow, BaseItem baseItem)
        {
            Item item = baseItem.TryCast<Item>();
            string id = item != null ? item.ID : string.Empty;

            if (card.inventoryItem != null) {
                GameInventory inventory = Singleton<GameInventory>.Instance;
                card.inventoryItem.sprite = inventory != null &&
                    !string.IsNullOrEmpty(id)
                    ? inventory.GetThumb(id, false) : null;
                card.inventoryItem.color = Color.white;
            }
            if (card.caption != null)
                card.caption.text = baseItem.GetLocalizedName() ?? string.Empty;

            Hide(card.transform, "Price");
            Hide(card.transform, "Brand");
            Hide(card.transform, "LicenseText");
            Hide(card.transform, "InventoryItemGroup");
            Hide(card.transform, "Locked");
            Hide(card.transform, "Color");
            Hide(card.transform, "LiverieIcon");
            Hide(card.transform, "UnbalancedIcon");
            Hide(card.transform, "TuningIcon");
            Hide(card.transform, "Checked");
            Hide(card.transform, "Selected");

            CopyCondition(nativeRow.transform, card.transform);
            ConfigureQuality(card.transform, item != null ? item.Quality : 0);
            PartRowIndicators.UpdateCardIndicators(baseItem, card);

            Graphic[] graphics = card.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        private static void CopyCondition(Transform sourceRow,
            Transform targetCard)
        {
            Transform source = sourceRow.Find("Condition");
            Transform target = targetCard.Find("Condition");
            if (source == null || target == null)
                return;

            Image sourceImage = source.GetComponent<Image>();
            Image targetImage = target.GetComponent<Image>();
            Text sourceText = source.Find("Value")?.GetComponent<Text>();
            Text targetText = target.Find("ConditionPercentage")
                ?.GetComponent<Text>();
            if (sourceImage != null && targetImage != null)
                targetImage.color = sourceImage.color;
            if (sourceText != null && targetText != null)
                targetText.text = sourceText.text;
            target.gameObject.SetActive(source.gameObject.activeSelf);
        }

        private static void ConfigureQuality(Transform card, int quality)
        {
            Transform root = card.Find("Quality");
            if (root == null)
                return;

            bool visible = quality > 0;
            root.gameObject.SetActive(visible);
            if (!visible)
                return;

            for (int index = 0; index < root.childCount; index++)
                root.GetChild(index).gameObject.SetActive(index < quality);
        }

        private static void ConfigureFooter(TakenItemsWindow window,
            Transform cell, TakenListItem nativeRow, int index)
        {
            Text nativePrice = nativeRow.transform.Find("Cash")?.GetComponent<Text>();
            Text price = nativePrice != null
                ? NativeUiFactory.CloneText(cell, "Price", nativePrice)
                : null;
            if (price != null) {
                RectTransform rect = price.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = new Vector2(-18f, FooterHeight);
                price.alignment = TextAnchor.MiddleLeft;
                price.raycastTarget = false;
            }

            Button nativeTrash = nativeRow.transform.Find("Trash")
                ?.GetComponent<Button>();
            if (nativeTrash == null)
                return;

            GameObject trashObject = GameObject.Instantiate(
                nativeTrash.gameObject, cell);
            trashObject.name = "Trash";
            RectTransform trashRect = trashObject.GetComponent<RectTransform>();
            Button trash = trashObject.GetComponent<Button>();
            if (trashRect == null || trash == null) {
                GameObject.Destroy(trashObject);
                return;
            }

            trashRect.anchorMin = new Vector2(1f, 0f);
            trashRect.anchorMax = new Vector2(1f, 0f);
            trashRect.pivot = new Vector2(1f, 0f);
            trashRect.anchoredPosition = Vector2.zero;
            trashRect.sizeDelta = new Vector2(15f, 15f);
            trashRect.localScale = Vector3.one;
            UnityEventUtility.RemoveAllListeners(trash);
            int x = index % 2;
            int y = index / 2;
            Action remove = delegate { window.OnTrashButtonClick(x, y); };
            trash.onClick.AddListener(
                DelegateSupport.ConvertDelegate<UnityAction>(remove));
            trashObject.SetActive(true);
        }

        private static void HideNativeRows(TakenItemsWindow window)
        {
            if (window.takenListItems == null)
                return;
            for (int index = 0; index < window.takenListItems.Count; index++) {
                TakenListItem row = window.takenListItems[index];
                if (row != null)
                    row.gameObject.SetActive(false);
            }
        }

        private static void ClearCards()
        {
            for (int index = 0; index < Cards.Count; index++) {
                GameObject cell = Cards[index].Cell;
                if (cell != null)
                    GameObject.Destroy(cell);
            }
            Cards.Clear();
        }

        private static void HideFilters(TakenItemsWindow window)
        {
            conditionMode = JunkyardConditionFilterMode.Off;
            repairabilityMode = RepairabilityQuickFilterMode.Off;
            repairSpecializationFilter.Reset();
            qualityMode = QualityQuickFilterMode.Off;
            ownedMode = OwnedQuickFilterMode.Off;
            searchText = string.Empty;
            minimumPrice = 0;
            maximumPrice = maximumAvailablePrice;
            InventoryFilterManager.TryCloseQuickFilterMenu();
            ClosePriceRangePopup();

            Transform filterRoot = window.transform.Find(FilterRootName);
            if (filterRoot != null)
                filterRoot.gameObject.SetActive(false);
            Transform searchRoot = window.transform.Find(SearchFieldName);
            if (searchRoot != null) {
                InputField field = searchRoot.GetComponent<InputField>();
                if (field != null)
                    field.SetTextWithoutNotify(string.Empty);
                searchRoot.gameObject.SetActive(false);
            }
            searchField = null;
        }

        private static void EnsureFilters(TakenItemsWindow window)
        {
            Transform existing = window.transform.Find(FilterRootName);
            RectTransform root;
            if (existing != null) {
                root = existing.GetComponent<RectTransform>();
            } else {
#if NET6_0_OR_GREATER
                GameObject rootObject = new GameObject(FilterRootName,
                    typeof(RectTransform));
#else
                Il2CppReferenceArray<Il2CppSystem.Type> types =
                    new Il2CppReferenceArray<Il2CppSystem.Type>(1);
                types[0] = Il2CppType.Of<RectTransform>();
                GameObject rootObject = new GameObject(FilterRootName, types);
#endif
                rootObject.transform.SetParent(window.transform, false);
                rootObject.layer = window.gameObject.layer;
                root = rootObject.GetComponent<RectTransform>();
            }
            if (root == null)
                return;

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(ButtonSpacing * 4f + ButtonSize,
                ButtonSize);
            root.localScale = Vector3.one;

            conditionButton = EnsureFilterButton(root, "Condition", 0,
                InventoryIconProvider.GetWhiteConditionIcon(),
                InventoryFilterManager.CreateJunkyardConditionQuickFilterMenu(
                    SelectCondition,
                    JunkyardConditionFilterMode.Off,
                    JunkyardConditionFilterMode.RepairThresholdToPerfect,
                    JunkyardConditionFilterMode.Red,
                    JunkyardConditionFilterMode.Orange,
                    JunkyardConditionFilterMode.Yellow,
                    JunkyardConditionFilterMode.Green,
                    JunkyardConditionFilterMode.Perfect));
            repairButton = EnsureFilterButton(root, "Repairability", 1,
                InventoryIconProvider.GetWhiteRepairWrenchIcon(),
                InventoryFilterManager.CreateRepairabilityQuickFilterMenu(
                    SelectRepairability,
                    delegate () { return repairabilityMode; },
                    repairSpecializationFilter,
                    delegate () {
                        UpdateFilterVisuals();
                        ApplyFilters();
                    }));
            qualityButton = EnsureFilterButton(root, "Quality", 2,
                InventoryIconProvider.GetQualityIcon(),
                InventoryFilterManager.CreateQualityQuickFilterMenu(
                    SelectQuality));
            ownedButton = EnsureFilterButton(root, "Owned", 3,
                InventoryIconProvider.GetWhiteWarehouseIcon(),
                InventoryFilterManager.CreateOwnedQuickFilterMenu(SelectOwned));
            EnsurePriceRangeButton(root, 4);
            UpdateFilterVisuals();
            root.gameObject.SetActive(true);
        }

        private static void EnsureSearchField(TakenItemsWindow window)
        {
            if (window == null)
                return;

            Transform existing = window.transform.Find(SearchFieldName);
            InputField field = existing != null
                ? existing.GetComponent<InputField>() : null;
            if (field == null) {
                InputField template = FindSearchTemplate(window);
                if (template == null || template.gameObject == null)
                    return;

                GameObject clone = GameObject.Instantiate(
                    template.gameObject, window.transform);
                clone.name = SearchFieldName;
                clone.transform.localScale = Vector3.one;
                field = clone.GetComponent<InputField>();
            }
            if (field == null)
                return;

            RectTransform rect = field.GetComponent<RectTransform>();
            if (rect != null) {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.localScale = Vector3.one;
            }

            UnityEventUtility.RemoveAllListeners(field);
            searchField = field;
            field.SetTextWithoutNotify(searchText);
            Action<string> changed = delegate (string value) {
                searchText = value ?? string.Empty;
                ApplyFilters();
            };
            field.onValueChanged.AddListener(
                DelegateSupport.ConvertDelegate<UnityAction<string>>(changed));
            field.gameObject.SetActive(true);
            field.transform.SetAsLastSibling();
        }

        private static InputField FindSearchTemplate(TakenItemsWindow window)
        {
            Transform root = window.transform.parent != null
                ? window.transform.parent : window.transform.root;
            if (root == null)
                return null;

            foreach (InputField field in
                root.GetComponentsInChildren<InputField>(true)) {
                if (field != null && field.gameObject != null &&
                    field.name != SearchFieldName &&
                    string.Equals(field.name, "SearchField",
                        StringComparison.OrdinalIgnoreCase))
                    return field;
            }
            return null;
        }

        private static void LayoutToolbar(TakenItemsWindow window)
        {
            if (window == null)
                return;

            RectTransform root = window.transform.Find(FilterRootName)
                ?.GetComponent<RectTransform>();
            RectTransform searchRect = searchField != null
                ? searchField.GetComponent<RectTransform>() : null;
            if (root == null)
                return;

            float searchWidth = searchRect != null
                ? Mathf.Max(searchRect.rect.width,
                    Mathf.Abs(searchRect.sizeDelta.x))
                : 0f;
            float rootX = ToolbarRightX - root.sizeDelta.x;
            if (searchRect != null)
                rootX -= searchWidth + ToolbarGap;

            root.anchoredPosition = new Vector2(rootX, 199f);
            if (searchRect != null) {
                searchRect.anchoredPosition = new Vector2(
                    ToolbarRightX - searchWidth, 199f);
                searchField.transform.SetAsLastSibling();
            }
        }

        private static void EnsurePriceRangeButton(RectTransform root, int index)
        {
            Transform existing = root.Find("PriceRange");
            GameObject objectValue;
            if (existing != null) {
                objectValue = existing.gameObject;
            } else {
#if NET6_0_OR_GREATER
                objectValue = new GameObject("PriceRange", typeof(RectTransform));
#else
                Il2CppReferenceArray<Il2CppSystem.Type> types =
                    new Il2CppReferenceArray<Il2CppSystem.Type>(1);
                types[0] = Il2CppType.Of<RectTransform>();
                objectValue = new GameObject("PriceRange", types);
#endif
                objectValue.transform.SetParent(root, false);
                objectValue.layer = root.gameObject.layer;
                Image image = objectValue.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
                objectValue.AddComponent<Button>();
                priceButtonLabel = NativeUiFactory.CreateText(
                    objectValue.transform, "Label", "$", 10,
                    TextAnchor.MiddleCenter, DisabledButtonColor);
            }

            RectTransform rect = objectValue.GetComponent<RectTransform>();
            Button button = objectValue.GetComponent<Button>();
            if (priceButtonLabel == null)
                priceButtonLabel = objectValue.transform.Find("Label")
                    ?.GetComponent<Text>();
            if (rect == null || button == null)
                return;

            if (priceButtonLabel != null) {
                priceButtonLabel.text = "$";
                Text nativePrice = Cards.Count > 0 &&
                    Cards[0].NativeRow != null
                    ? Cards[0].NativeRow.transform.Find("Cash")
                        ?.GetComponent<Text>()
                    : null;
                if (nativePrice != null) {
                    priceButtonLabel.font = nativePrice.font;
                    priceButtonLabel.fontSize = nativePrice.fontSize;
                    priceButtonLabel.fontStyle = nativePrice.fontStyle;
                }
            }

            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(index * ButtonSpacing, 0f);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            if (priceButtonLabel != null) {
                RectTransform labelRect =
                    priceButtonLabel.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            UnityEventUtility.RemoveAllListeners(button);
            Action toggle = TogglePriceRangePopup;
            button.onClick.AddListener(
                DelegateSupport.ConvertDelegate<UnityAction>(toggle));
            objectValue.SetActive(true);
        }

        private static void TogglePriceRangePopup()
        {
            if (priceRangePopup != null) {
                ClosePriceRangePopup();
                return;
            }
            if (activeWindow == null)
                return;

            InventoryFilterManager.TryCloseQuickFilterMenu();
            Transform buttonTransform = activeWindow.transform
                .Find(FilterRootName + "/PriceRange");
            RectTransform buttonRect = buttonTransform != null
                ? buttonTransform.GetComponent<RectTransform>() : null;
            if (buttonRect == null)
                return;

            CreatePriceRangeBlocker();
            priceRangePopup = CreateRectObject(
                PriceRangePopupName, activeWindow.transform);
            RectTransform popupRect =
                priceRangePopup.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(1f, 0f);
            popupRect.sizeDelta = new Vector2(
                PriceRangeWidth, PriceRangeHeight);
            popupRect.position = buttonRect.TransformPoint(new Vector3(
                buttonRect.rect.xMax, buttonRect.rect.yMax, 0f));

            Image background = priceRangePopup.AddComponent<Image>();
            background.color = new Color32(20, 20, 20, 235);
            background.raycastTarget = true;

            GameObject trackObject = CreateRectObject(
                "Track", priceRangePopup.transform);
            RectTransform trackRect = trackObject.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.offsetMin = new Vector2(
                PriceRangeHandleWidth * 0.5f, -1.5f);
            trackRect.offsetMax = new Vector2(
                -PriceRangeHandleWidth * 0.5f, 1.5f);
            Image track = trackObject.AddComponent<Image>();
            track.color = new Color32(90, 90, 90, 255);
            track.raycastTarget = false;

            GameObject selectionObject = CreateRectObject(
                "Selection", trackObject.transform);
            priceRangeSelection =
                selectionObject.GetComponent<RectTransform>();
            priceRangeSelection.anchorMin = Vector2.zero;
            priceRangeSelection.anchorMax = Vector2.one;
            priceRangeSelection.offsetMin = Vector2.zero;
            priceRangeSelection.offsetMax = Vector2.zero;
            Image selection = selectionObject.AddComponent<Image>();
            selection.color = ActiveButtonColor;
            selection.raycastTarget = false;

            minimumPriceSlider = CreatePriceSlider(
                priceRangePopup.transform, "Minimum",
                out minimumPriceValue, MinimumPriceChanged);
            maximumPriceSlider = CreatePriceSlider(
                priceRangePopup.transform, "Maximum",
                out maximumPriceValue, MaximumPriceChanged);
            UpdatePriceRangeControls();
            priceRangePopup.transform.SetAsLastSibling();
        }

        private static Slider CreatePriceSlider(Transform parent, string name,
            out Text valueText, Action<float> changed)
        {
            GameObject root = CreateRectObject(name, parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Slider slider = root.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = Mathf.Max(1, maximumAvailablePrice);
            slider.wholeNumbers = true;

            GameObject handleArea =
                CreateRectObject("Handle Slide Area", root.transform);
            RectTransform handleAreaRect =
                handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(
                PriceRangeHandleWidth * 0.5f, 0f);
            handleAreaRect.offsetMax = new Vector2(
                -PriceRangeHandleWidth * 0.5f, 0f);

            GameObject handleObject =
                CreateRectObject("Handle", handleArea.transform);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(PriceRangeHandleWidth, 0f);
            Image handle = handleObject.AddComponent<Image>();
            handle.color = ActiveButtonColor;
            handle.raycastTarget = true;

            valueText = NativeUiFactory.CreateText(
                handleObject.transform, "Value", string.Empty, 9,
                TextAnchor.MiddleCenter, Color.black);
            if (valueText != null) {
                if (activeWindow != null &&
                    activeWindow.normalPriceText != null &&
                    activeWindow.normalPriceText.font != null)
                    valueText.font = activeWindow.normalPriceText.font;
                valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                valueText.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform valueRect =
                    valueText.GetComponent<RectTransform>();
                valueRect.anchorMin = Vector2.zero;
                valueRect.anchorMax = Vector2.one;
                valueRect.offsetMin = Vector2.zero;
                valueRect.offsetMax = Vector2.zero;
            }

            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.onValueChanged.AddListener(
                DelegateSupport.ConvertDelegate<UnityAction<float>>(changed));
            return slider;
        }

        private static void CreatePriceRangeBlocker()
        {
            if (activeWindow == null)
                return;

            priceRangeBlocker = CreateRectObject(
                "QTakenItemsPriceRangeBlocker", activeWindow.transform);
            RectTransform rect =
                priceRangeBlocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            Image image = priceRangeBlocker.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            priceRangeBlockerTrigger =
                priceRangeBlocker.AddComponent<EventTrigger>();
            priceRangeBlocker.transform.SetAsLastSibling();
        }

        internal static bool TryHandlePriceRangeBlockerEvent(
            EventTrigger trigger, BaseEventData eventData)
        {
            if (priceRangePopup == null || trigger == null ||
                trigger != priceRangeBlockerTrigger)
                return false;

            ClosePriceRangePopup();
            if (eventData != null)
                eventData.Use();
            return true;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
#if NET6_0_OR_GREATER
            GameObject result = new GameObject(name, typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> types =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            types[0] = Il2CppType.Of<RectTransform>();
            GameObject result = new GameObject(name, types);
#endif
            result.transform.SetParent(parent, false);
            result.layer = parent.gameObject.layer;
            return result;
        }

        private static void MinimumPriceChanged(float value)
        {
            minimumPrice = Mathf.Min(Mathf.RoundToInt(value), maximumPrice);
            if (minimumPriceSlider != null)
                minimumPriceSlider.SetValueWithoutNotify(minimumPrice);
            UpdatePriceRangeControls();
            ApplyFilters();
        }

        private static void MaximumPriceChanged(float value)
        {
            maximumPrice = Mathf.Max(Mathf.RoundToInt(value), minimumPrice);
            if (maximumPriceSlider != null)
                maximumPriceSlider.SetValueWithoutNotify(maximumPrice);
            UpdatePriceRangeControls();
            ApplyFilters();
        }

        private static void UpdatePriceBounds()
        {
            int previousMaximum = maximumAvailablePrice;
            bool wasFullRange = maximumPrice >= previousMaximum;
            int newMaximum = 0;
            for (int index = 0; index < Cards.Count; index++)
                newMaximum = Math.Max(newMaximum, Cards[index].Price);

            maximumAvailablePrice = newMaximum;
            if (previousMaximum == 0 || wasFullRange ||
                maximumPrice > maximumAvailablePrice)
                maximumPrice = maximumAvailablePrice;
            minimumPrice = Mathf.Clamp(minimumPrice, 0, maximumPrice);
            UpdatePriceRangeControls();
        }

        private static void UpdatePriceRangeControls()
        {
            if (minimumPriceSlider != null) {
                minimumPriceSlider.maxValue = Mathf.Max(1, maximumAvailablePrice);
                minimumPriceSlider.SetValueWithoutNotify(minimumPrice);
            }
            if (maximumPriceSlider != null) {
                maximumPriceSlider.maxValue = Mathf.Max(1, maximumAvailablePrice);
                maximumPriceSlider.SetValueWithoutNotify(maximumPrice);
            }
            if (minimumPriceValue != null)
                minimumPriceValue.text = minimumPrice.ToString();
            if (maximumPriceValue != null)
                maximumPriceValue.text = maximumPrice.ToString();
            if (priceRangeSelection != null) {
                float scale = Mathf.Max(1, maximumAvailablePrice);
                priceRangeSelection.anchorMin = new Vector2(
                    minimumPrice / scale, 0f);
                priceRangeSelection.anchorMax = new Vector2(
                    maximumPrice / scale, 1f);
                priceRangeSelection.offsetMin = Vector2.zero;
                priceRangeSelection.offsetMax = Vector2.zero;
            }
            if (priceButtonLabel != null)
                priceButtonLabel.color = minimumPrice > 0 ||
                    maximumPrice < maximumAvailablePrice
                    ? ActiveButtonColor : DisabledButtonColor;
        }

        private static void ClosePriceRangePopup()
        {
            if (priceRangePopup != null)
                GameObject.Destroy(priceRangePopup);
            if (priceRangeBlocker != null)
                GameObject.Destroy(priceRangeBlocker);
            priceRangePopup = null;
            priceRangeBlocker = null;
            priceRangeBlockerTrigger = null;
            minimumPriceSlider = null;
            maximumPriceSlider = null;
            minimumPriceValue = null;
            maximumPriceValue = null;
            priceRangeSelection = null;
        }

        private static int GetRowPrice(TakenListItem row)
        {
            Text price = row != null
                ? row.transform.Find("Cash")?.GetComponent<Text>() : null;
            string value = price != null ? price.text : string.Empty;
            int result = 0;
            bool started = false;
            for (int index = 0; index < value.Length; index++) {
                char character = value[index];
                if (character >= '0' && character <= '9') {
                    started = true;
                    result = result * 10 + character - '0';
                } else if (started) {
                    break;
                }
            }
            return result;
        }

        private static Image EnsureFilterButton(RectTransform root,
            string name, int index, Sprite sprite,
            QuickFilterMenuOption[] options)
        {
            Transform existing = root.Find(name);
            GameObject objectValue;
            if (existing != null) {
                objectValue = existing.gameObject;
            } else {
#if NET6_0_OR_GREATER
                objectValue = new GameObject(name, typeof(RectTransform));
#else
                Il2CppReferenceArray<Il2CppSystem.Type> types =
                    new Il2CppReferenceArray<Il2CppSystem.Type>(1);
                types[0] = Il2CppType.Of<RectTransform>();
                objectValue = new GameObject(name, types);
#endif
                objectValue.transform.SetParent(root, false);
                objectValue.layer = root.gameObject.layer;
                objectValue.AddComponent<Image>();
                objectValue.AddComponent<Button>();
            }

            RectTransform rect = objectValue.GetComponent<RectTransform>();
            Image image = objectValue.GetComponent<Image>();
            Button button = objectValue.GetComponent<Button>();
            if (rect == null || image == null || button == null)
                return null;

            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(index * ButtonSpacing, 0f);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            InventoryFilterManager.RegisterQuickFilterMenu(button, options);
            objectValue.SetActive(true);
            return image;
        }

        private static void SelectCondition(JunkyardConditionFilterMode mode)
        {
            conditionMode = mode;
            UpdateFilterVisuals();
            ApplyFilters();
        }

        private static void SelectRepairability(RepairabilityQuickFilterMode mode)
        {
            repairabilityMode = mode;
            UpdateFilterVisuals();
            ApplyFilters();
        }

        private static void SelectQuality(QualityQuickFilterMode mode)
        {
            qualityMode = mode;
            UpdateFilterVisuals();
            ApplyFilters();
        }

        private static void SelectOwned(OwnedQuickFilterMode mode)
        {
            ownedMode = mode;
            UpdateFilterVisuals();
            ApplyFilters();
        }

        private static void ApplyFilters()
        {
            PartFilterCriteria criteria = new PartFilterCriteria {
                Context = PartFilterContext.Junkyard,
                SearchText = searchText,
                JunkyardConditionMode = conditionMode,
                RepairabilityMode = repairabilityMode,
                RequiredRepairSpecializationMask =
                    repairSpecializationFilter.RequiredMask,
                ExcludedRepairSpecializationMask =
                    repairSpecializationFilter.ExcludedMask,
                QualityMode = qualityMode,
                OwnedMode = ownedMode,
            };
            for (int index = 0; index < Cards.Count; index++) {
                CardEntry entry = Cards[index];
                if (entry.Cell != null) {
                    bool inPriceRange = entry.Price >= minimumPrice &&
                        entry.Price <= maximumPrice;
                    entry.Cell.SetActive(inPriceRange &&
                        PartFilterRules.Matches(entry.Item, criteria));
                }
            }
        }

        private static CardEntry GetSelectedVisibleCard(
            TakenItemsWindow window)
        {
            TakenListItem selectedRow = null;
            if (window.gridNavigationManager != null) {
                var selected = window.gridNavigationManager.GetCurrentGridItem();
                if (selected != null)
                    selectedRow = selected.TryCast<TakenListItem>();
            }

            for (int index = 0; index < Cards.Count; index++) {
                CardEntry entry = Cards[index];
                if (entry.Cell != null && entry.Cell.activeSelf &&
                    entry.NativeRow == selectedRow)
                    return entry;
            }
            for (int index = 0; index < Cards.Count; index++) {
                CardEntry entry = Cards[index];
                if (entry.Cell != null && entry.Cell.activeSelf)
                    return entry;
            }
            return null;
        }

        private static void UpdateFilterVisuals()
        {
            if (conditionButton != null) {
                switch (conditionMode) {
                    case JunkyardConditionFilterMode.Red:
                        conditionButton.sprite = InventoryIconProvider.GetRedConditionIcon();
                        break;
                    case JunkyardConditionFilterMode.Orange:
                        conditionButton.sprite = InventoryIconProvider.GetOrangeConditionIcon();
                        break;
                    case JunkyardConditionFilterMode.Yellow:
                        conditionButton.sprite = InventoryIconProvider.GetYellowConditionIcon();
                        break;
                    case JunkyardConditionFilterMode.Green:
                        conditionButton.sprite = InventoryIconProvider.GetGreenRingConditionIcon();
                        break;
                    case JunkyardConditionFilterMode.Perfect:
                        conditionButton.sprite = InventoryIconProvider.GetGreenConditionIcon();
                        break;
                    default:
                        conditionButton.sprite = InventoryIconProvider.GetWhiteConditionIcon();
                        break;
                }
                conditionButton.color = conditionMode == JunkyardConditionFilterMode.Off
                    ? DisabledButtonColor : ActiveButtonColor;
            }
            if (repairButton != null) {
                repairButton.sprite = repairabilityMode ==
                    RepairabilityQuickFilterMode.NonRepairableOnly
                    ? InventoryIconProvider.GetRedRepairWrenchIcon()
                    : InventoryIconProvider.GetWhiteRepairWrenchIcon();
                repairButton.color = repairabilityMode ==
                        RepairabilityQuickFilterMode.Off &&
                        !repairSpecializationFilter.HasAny
                    ? DisabledButtonColor : ActiveButtonColor;
            }
            if (qualityButton != null) {
                switch (qualityMode) {
                    case QualityQuickFilterMode.Quality1:
                        qualityButton.sprite = InventoryIconProvider.GetQuality1Icon();
                        break;
                    case QualityQuickFilterMode.Quality2:
                        qualityButton.sprite = InventoryIconProvider.GetQuality2Icon();
                        break;
                    case QualityQuickFilterMode.Quality3:
                        qualityButton.sprite = InventoryIconProvider.GetQuality3Icon();
                        break;
                    case QualityQuickFilterMode.NonImproved:
                        qualityButton.sprite = InventoryIconProvider.GetQualityNonIcon();
                        break;
                    default:
                        qualityButton.sprite = InventoryIconProvider.GetQualityIcon();
                        break;
                }
                qualityButton.color = qualityMode == QualityQuickFilterMode.Off
                    ? DisabledButtonColor : ActiveButtonColor;
            }
            if (ownedButton != null) {
                ownedButton.sprite = ownedMode == OwnedQuickFilterMode.Missing
                    ? InventoryIconProvider.GetRedWarehouseIcon()
                    : InventoryIconProvider.GetWhiteWarehouseIcon();
                ownedButton.color = ownedMode == OwnedQuickFilterMode.Off
                    ? DisabledButtonColor : ActiveButtonColor;
            }
            UpdatePriceRangeControls();
        }

        private static InventoryItem GetInventoryTemplate()
        {
            if (inventoryTemplate != null && inventoryTemplate.gameObject != null)
                return inventoryTemplate;

            try {
                Il2CppReferenceArray<UnityEngine.Object> loaded =
                    Resources.FindObjectsOfTypeAll(Il2CppType.Of<InventoryItem>());
                foreach (UnityEngine.Object value in loaded) {
                    InventoryItem item = value.TryCast<InventoryItem>();
                    if (item == null || item.inventoryItem == null ||
                        item.caption == null)
                        continue;
                    RectTransform rect = item.GetComponent<RectTransform>();
                    if (rect == null || (rect.rect.width <= 0f &&
                        rect.sizeDelta.x == 0f))
                        continue;
                    inventoryTemplate = item;
                    break;
                }
            } catch {
            }
            return inventoryTemplate;
        }

        private static void Hide(Transform root, string name)
        {
            Transform child = root.Find(name);
            if (child != null)
                child.gameObject.SetActive(false);
        }
    }
}
