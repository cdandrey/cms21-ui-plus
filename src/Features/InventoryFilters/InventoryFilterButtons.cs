using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic.Warehouse;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS.UI.Description;
using CMS.UI.Logic.Warehouse;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    public static partial class InventoryFilterManager
    {
        private const string ConditionButtonName = "QInventoryQuickFilterCondition";
        private const string RepairButtonName = "QInventoryQuickFilterRepair";
        private const string QualityButtonName = "QInventoryQuickFilterQuality";
        private const string OwnedButtonName = "QInventoryQuickFilterOwned";
        private const string ResetHintName = "Hint_ResetInventoryFilters";


        private const float ButtonSize = 20f;
        private const float ButtonSpacing = 28f;
        private const float SearchRowYOffset = 0f;
        private const float FallbackY = -96f;
        private const float FallbackConditionX = -328f;
        private const float FallbackRepairX = -300f;
        private const float FallbackQualityX = -272f;
        private const float FallbackOwnedX = -244f;

        private static readonly Dictionary<int, Action> ReverseQuickFilterClicks =
            new Dictionary<int, Action>();

        private static readonly Color32 ActiveButtonColor =
            new Color32(255, 255, 255, 255);
        private static readonly Color32 DisabledButtonColor =
            new Color32(155, 155, 155, 210);
        private static BaseInventory resetHintInventory;
        private static BaseInventory activeFilteredInventory;
        private static NativeUiFactory.FooterHintHandle resetHint;
        private static string resetHintWindowId;

        private enum QuickFilterButtonKind
        {
            Condition,
            Repairability,
            Quality,
            Owned,
        }

        public static void EnsureButtons(BaseInventory inventory)
        {
            try {
                EnsureButtonsUnsafe(inventory);
                SetActiveFilteredInventory(inventory);
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to create or position quick-filter buttons." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }

        public static void EnsureWarehouseWindowButtons(WarehouseWindow warehouseWindow)
        {
            if (warehouseWindow == null)
                return;

            try {
                BaseInventory activeInventory = null;

                foreach (BaseInventory inventory in
                    warehouseWindow.GetComponentsInChildren<BaseInventory>(true)) {
                    if (inventory != null && activeInventory == null &&
                        inventory.gameObject.activeInHierarchy)
                        activeInventory = inventory;
                }

                if (activeInventory != null)
                    EnsureButtons(activeInventory);
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to inspect WarehouseWindow children." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }

        public static void UpdateResetShortcut()
        {
            BaseInventory inventory = activeFilteredInventory;
            if (inventory == null || inventory.gameObject == null ||
                !inventory.gameObject.activeInHierarchy) {
                ClearResetHint();
                activeFilteredInventory = null;
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftAlt))
                ResetActiveFilters(inventory);
        }

        private static void SetActiveFilteredInventory(
            BaseInventory inventory)
        {
            inventory = ResolveActiveInventory(inventory);
            if (inventory == null || !ShouldHandleWindow(inventory) ||
                !inventory.gameObject.activeInHierarchy)
                return;
            if (activeFilteredInventory == inventory &&
                resetHintInventory == inventory && resetHint != null &&
                resetHint.Root != null)
                return;

            ClearResetHint();
            activeFilteredInventory = inventory;
            resetHintInventory = inventory;
            CreateResetHint(inventory);
        }

        private static void ResetActiveFilters(BaseInventory inventory)
        {
            inventory = ResolveActiveInventory(inventory);
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene()) {
                junkyardConditionFilterMode =
                    JunkyardConditionFilterMode.Off;
                junkyardRepairabilityFilterMode =
                    RepairabilityQuickFilterMode.Off;
                junkyardQualityFilterMode = QualityQuickFilterMode.Off;
                ownedFilterMode = OwnedQuickFilterMode.Off;
            } else {
                garageConditionFilterMode = GarageConditionFilterMode.Off;
                garageRepairabilityFilterMode =
                    RepairabilityQuickFilterMode.Off;
                garageQualityFilterMode = QualityQuickFilterMode.Off;
            }

            InputField searchField = FindSearchField(inventory, true);
            if (searchField != null) {
                searchField.text = string.Empty;
                searchField.SendOnSubmit();
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void CreateResetHint(BaseInventory inventory)
        {
            if (resetHint != null && resetHint.Root != null) {
                int currentCount = GetCurrentFilteredItemCount(inventory);
                WindowFooterHintController.SetNativeProfile(
                    resetHintWindowId,
                    ResolveFooterProfile(inventory, currentCount == 0),
                    currentCount);
                return;
            }

            WarehouseWindow warehouse =
                inventory.GetComponentInParent<WarehouseWindow>();
            Transform windowRoot = warehouse != null
                ? warehouse.transform : null;
            Transform descriptionRoot = warehouse != null &&
                warehouse.uiDescription != null
                    ? warehouse.uiDescription.transform : null;
            if (descriptionRoot == null) {
                InventoryWindow inventoryWindow =
                    inventory.GetComponentInParent<InventoryWindow>();
                if (inventoryWindow == null) {
                    InventoryWindow discovered = UnityEngine.Object
                        .FindObjectOfType<InventoryWindow>();
                    if (discovered != null && discovered.gameObject != null &&
                        discovered.gameObject.activeInHierarchy)
                        inventoryWindow = discovered;
                }
                if (inventoryWindow != null &&
                    inventoryWindow.uiDescription != null) {
                    descriptionRoot =
                        inventoryWindow.uiDescription.transform;
                    windowRoot = inventoryWindow.transform;
                }
            }
            if (descriptionRoot == null || windowRoot == null)
            {
                if (IsBarnOrJunkyardScene()) {
                    Transform current = inventory.transform;
                    while (current != null &&
                            current.name != "ItemsExchangeWindow")
                        current = current.parent;
                    UIDescription[] descriptions = UnityEngine.Object
                        .FindObjectsOfType<UIDescription>();
                    for (int i = 0; i < descriptions.Length; i++) {
                        UIDescription candidate = descriptions[i];
                        if (candidate != null && candidate.gameObject != null &&
                            candidate.gameObject.activeInHierarchy &&
                            candidate.name == "ItemsExchangeWindow") {
                            descriptionRoot = candidate.transform;
                            break;
                        }
                    }
                    windowRoot = current;
                }
                if (descriptionRoot == null || windowRoot == null)
                    return;
            }

            resetHintWindowId = IsBarnOrJunkyardScene()
                ? "TravelInventory"
                : warehouse != null ? "Warehouse" : "Inventory";
            int itemCount = GetCurrentFilteredItemCount(inventory);
            WindowFooterHintController.NativeFooterProfile footerProfile =
                ResolveFooterProfile(inventory, itemCount == 0);
            resetHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = resetHintWindowId,
                    WindowRoot = windowRoot,
                    HintRoot = descriptionRoot,
                    HintId = ResetHintName,
                    Keys = new string[] { "LeftAlt" },
                    Text = ModLocalization.Get("LOC_ResetFiltersAction"),
                    Action = new Action(delegate {
                        ResetActiveFilters(inventory);
                    }),
                    Row = 0,
                    Order = 10,
                    Profile = footerProfile,
                    ItemCount = itemCount,
                });
        }

        private static WindowFooterHintController.NativeFooterProfile
            ResolveFooterProfile(BaseInventory inventory, bool isEmpty)
        {
            if (IsBarnOrJunkyardScene())
                return isEmpty
                    ? WindowFooterHintController.NativeFooterProfile.TravelEmpty
                    : WindowFooterHintController.NativeFooterProfile.TravelPopulated;
            if (inventory != null &&
                inventory.TryCast<WarehouseInventoryTab>() != null)
                return isEmpty
                    ? WindowFooterHintController.NativeFooterProfile
                        .WarehouseInventoryEmpty
                    : WindowFooterHintController.NativeFooterProfile
                        .WarehouseInventoryPopulated;
            if (inventory != null &&
                inventory.TryCast<WarehouseTab>() != null)
                return isEmpty
                    ? WindowFooterHintController.NativeFooterProfile
                        .WarehouseStorageEmpty
                    : WindowFooterHintController.NativeFooterProfile
                        .WarehouseStoragePopulated;
            return isEmpty
                ? WindowFooterHintController.NativeFooterProfile.InventoryEmpty
                : WindowFooterHintController.NativeFooterProfile
                    .InventoryPopulated;
        }

        private static void ClearResetHint()
        {
            if (!string.IsNullOrEmpty(resetHintWindowId))
                WindowFooterHintController.RemoveHint(resetHintWindowId,
                    ResetHintName);
            resetHint = null;
            resetHintWindowId = null;
            resetHintInventory = null;
        }

        private static void EnsureButtonsUnsafe(BaseInventory inventory)
        {
            inventory = ResolveActiveInventory(inventory);
            if (!ShouldHandleWindow(inventory))
                return;

            Transform buttonRoot = GetButtonRoot(inventory);
            bool junkyardContext = IsBarnOrJunkyardScene();
            Transform conditionButton = FindSingleButton(buttonRoot, ConditionButtonName);
            Transform repairButton = FindSingleButton(buttonRoot, RepairButtonName);
            Transform qualityButton = FindSingleButton(buttonRoot, QualityButtonName);
            Transform ownedButton = FindSingleButton(buttonRoot, OwnedButtonName);

            if (conditionButton == null)
                conditionButton = CreateButton(inventory, buttonRoot,
                    ConditionButtonName, QuickFilterButtonKind.Condition);
            if (repairButton == null)
                repairButton = CreateButton(inventory, buttonRoot,
                    RepairButtonName, QuickFilterButtonKind.Repairability);
            if (qualityButton == null)
                qualityButton = CreateButton(inventory, buttonRoot,
                    QualityButtonName, QuickFilterButtonKind.Quality);

            if (junkyardContext) {
                if (ownedButton == null)
                    ownedButton = CreateButton(inventory, buttonRoot,
                        OwnedButtonName, QuickFilterButtonKind.Owned);
            } else if (ownedButton != null) {
                UnregisterReverseQuickFilterClick(
                    ownedButton.GetComponent<Button>());
                ownedButton.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(ownedButton.gameObject);
                ownedButton = null;
            }

            ConfigureButton(conditionButton, inventory, QuickFilterButtonKind.Condition);
            ConfigureButton(repairButton, inventory, QuickFilterButtonKind.Repairability);
            ConfigureButton(qualityButton, inventory, QuickFilterButtonKind.Quality);
            if (junkyardContext)
                ConfigureButton(ownedButton, inventory, QuickFilterButtonKind.Owned);

            ApplyButtonLayout(inventory, conditionButton, repairButton,
                qualityButton, ownedButton, junkyardContext);
            UpdateButtonVisuals(conditionButton, repairButton, qualityButton,
                ownedButton, junkyardContext);
        }

        private static Transform CreateButton(BaseInventory inventory,
            Transform buttonRoot, string name, QuickFilterButtonKind kind)
        {
            Sprite initialSprite = GetInitialSprite(kind);
            if (initialSprite == null) {
                ModLogger.Log("[InventoryFilter] Sprite for '" + name + "' was not found.",
                    Types.LoggingLevels.Warning);
                return null;
            }

#if NET6_0_OR_GREATER
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
#else
            UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type>(1);
            componentTypes[0] = UnhollowerRuntimeLib.Il2CppType.Of<RectTransform>();
            GameObject buttonObject = new GameObject(name, componentTypes);
#endif
            buttonObject.transform.SetParent(
                buttonRoot != null ? buttonRoot : inventory.transform, false);
            buttonObject.layer = inventory.gameObject.layer;
            buttonObject.transform.SetAsLastSibling();

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            rect.localScale = Vector3.one;

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = initialSprite;
            image.color = DisabledButtonColor;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            ConfigureButton(buttonObject.transform, inventory, kind);
            buttonObject.SetActive(true);
            return buttonObject.transform;
        }

        private static Sprite GetInitialSprite(QuickFilterButtonKind kind)
        {
            switch (kind) {
                case QuickFilterButtonKind.Condition:
                    return InventoryIconProvider.GetWhiteConditionIcon();
                case QuickFilterButtonKind.Quality:
                    return InventoryIconProvider.GetQualityIcon();
                case QuickFilterButtonKind.Owned:
                    return InventoryIconProvider.GetWhiteWarehouseIcon();
                default:
                    return InventoryIconProvider.GetWhiteRepairWrenchIcon();
            }
        }

        private static void ConfigureButton(Transform buttonTransform,
            BaseInventory inventory, QuickFilterButtonKind kind)
        {
            if (buttonTransform == null || inventory == null)
                return;

            Button button = buttonTransform.GetComponent<Button>();
            if (button == null)
                button = buttonTransform.gameObject.AddComponent<Button>();

            Image image = buttonTransform.GetComponent<Image>();
            if (image != null)
                button.targetGraphic = image;

            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            UnityEventUtility.RemoveAllListeners(button);

            Action reverseClickAction;
            if (kind == QuickFilterButtonKind.Condition) {
                Action clickAction = delegate () {
                    CycleConditionFilter(ResolveActiveInventory(inventory));
                };
                button.onClick.AddListener(clickAction);
                reverseClickAction = delegate () {
                    CycleConditionFilterReverse(ResolveActiveInventory(inventory));
                };
            } else if (kind == QuickFilterButtonKind.Repairability) {
                Action clickAction = delegate () {
                    CycleRepairabilityFilter(ResolveActiveInventory(inventory));
                };
                button.onClick.AddListener(clickAction);
                reverseClickAction = delegate () {
                    CycleRepairabilityFilterReverse(ResolveActiveInventory(inventory));
                };
            } else if (kind == QuickFilterButtonKind.Quality) {
                Action clickAction = delegate () {
                    CycleQualityFilter(ResolveActiveInventory(inventory));
                };
                button.onClick.AddListener(clickAction);
                reverseClickAction = delegate () {
                    CycleQualityFilterReverse(ResolveActiveInventory(inventory));
                };
            } else {
                Action clickAction = delegate () {
                    CycleOwnedFilter(ResolveActiveInventory(inventory));
                };
                button.onClick.AddListener(clickAction);
                reverseClickAction = delegate () {
                    CycleOwnedFilterReverse(ResolveActiveInventory(inventory));
                };
            }
            RegisterReverseQuickFilterClick(button, reverseClickAction);
        }

        private static void ApplyButtonLayout(BaseInventory inventory,
            Transform conditionButton, Transform repairButton,
            Transform qualityButton, Transform ownedButton, bool junkyardContext)
        {
            if (conditionButton == null || repairButton == null ||
                qualityButton == null)
                return;

            InputField searchField = FindSearchField(inventory, false);
            RectTransform searchRect = searchField != null
                ? searchField.GetComponent<RectTransform>()
                : null;

            if (searchRect != null && searchRect.transform.parent != null) {
                WarehouseWindow parentWarehouse =
                    inventory.GetComponentInParent<WarehouseWindow>();
                float searchWidth = searchRect.rect.width;
                if (searchWidth <= 1f)
                    searchWidth = 220f;

                if (parentWarehouse != null) {
                    Transform parent = parentWarehouse.transform;
                    float centerY = searchRect.rect.center.y + SearchRowYOffset;
                    float qualityOffset = 18f;
                    float repairOffset = qualityOffset + ButtonSpacing;
                    float conditionOffset = repairOffset + ButtonSpacing;

                    Vector3 qualityWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - qualityOffset, centerY, 0f));
                    Vector3 repairWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - repairOffset, centerY, 0f));
                    Vector3 conditionWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - conditionOffset, centerY, 0f));

                    SetButtonWorldPosition(conditionButton, parent, conditionWorld);
                    SetButtonWorldPosition(repairButton, parent, repairWorld);
                    SetButtonWorldPosition(qualityButton, parent, qualityWorld);
                    return;
                }

                Transform normalParent = searchRect.transform.parent;
                float searchLeftX = searchRect.anchoredPosition.x -
                    (searchWidth * searchRect.pivot.x);
                float rightButtonX = searchLeftX - 18f;
                float y = searchRect.anchoredPosition.y + SearchRowYOffset;

                if (junkyardContext) {
                    SetButtonPosition(conditionButton, normalParent, searchRect,
                        rightButtonX - (ButtonSpacing * 3f), y);
                    SetButtonPosition(repairButton, normalParent, searchRect,
                        rightButtonX - (ButtonSpacing * 2f), y);
                    SetButtonPosition(qualityButton, normalParent, searchRect,
                        rightButtonX - ButtonSpacing, y);
                    SetButtonPosition(ownedButton, normalParent, searchRect,
                        rightButtonX, y);
                } else {
                    SetButtonPosition(conditionButton, normalParent, searchRect,
                        rightButtonX - (ButtonSpacing * 2f), y);
                    SetButtonPosition(repairButton, normalParent, searchRect,
                        rightButtonX - ButtonSpacing, y);
                    SetButtonPosition(qualityButton, normalParent, searchRect,
                        rightButtonX, y);
                }
                return;
            }

            SetFallbackPosition(conditionButton, inventory.transform,
                FallbackConditionX, FallbackY);
            SetFallbackPosition(repairButton, inventory.transform,
                FallbackRepairX, FallbackY);
            SetFallbackPosition(qualityButton, inventory.transform,
                FallbackQualityX, FallbackY);
            if (junkyardContext)
                SetFallbackPosition(ownedButton, inventory.transform,
                    FallbackOwnedX, FallbackY);
        }

        private static InputField FindSearchField(BaseInventory inventory, bool activeOnly)
        {
            if (inventory == null)
                return null;

            Transform searchRoot = inventory.transform;
            WarehouseWindow parentWarehouse = inventory.GetComponentInParent<WarehouseWindow>();
            if (parentWarehouse != null)
                searchRoot = parentWarehouse.transform;

            InputField[] fields = searchRoot.GetComponentsInChildren<InputField>(true);
            InputField bestActive = null;
            InputField bestAny = null;
            float bestActiveWidth = -1f;
            float bestAnyWidth = -1f;

            foreach (InputField field in fields) {
                if (field == null)
                    continue;

                RectTransform rect = field.GetComponent<RectTransform>();
                if (rect == null)
                    continue;

                float width = rect.rect.width;
                if (width > bestAnyWidth) {
                    bestAny = field;
                    bestAnyWidth = width;
                }

                if (field.gameObject.activeInHierarchy && width > bestActiveWidth) {
                    bestActive = field;
                    bestActiveWidth = width;
                }
            }

            if (activeOnly)
                return bestActive;
            return bestActive != null ? bestActive : bestAny;
        }

        private static void SetButtonPosition(Transform buttonTransform, Transform parent,
            RectTransform referenceRect, float x, float y)
        {
            if (buttonTransform == null || parent == null || referenceRect == null)
                return;

            buttonTransform.SetParent(parent, false);
            buttonTransform.gameObject.layer = parent.gameObject.layer;
            buttonTransform.SetAsLastSibling();

            RectTransform rect = buttonTransform.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = referenceRect.anchorMin;
            rect.anchorMax = referenceRect.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            rect.localScale = Vector3.one;
        }

        private static void SetButtonWorldPosition(Transform buttonTransform, Transform parent,
            Vector3 worldPosition)
        {
            if (buttonTransform == null || parent == null)
                return;

            buttonTransform.SetParent(parent, false);
            buttonTransform.gameObject.layer = parent.gameObject.layer;
            buttonTransform.SetAsLastSibling();

            RectTransform rect = buttonTransform.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            rect.localScale = Vector3.one;
            rect.position = worldPosition;
        }

        private static void SetFallbackPosition(Transform buttonTransform, Transform parent,
            float x, float y)
        {
            if (buttonTransform == null || parent == null)
                return;

            buttonTransform.SetParent(parent, false);
            buttonTransform.gameObject.layer = parent.gameObject.layer;
            buttonTransform.SetAsLastSibling();

            RectTransform rect = buttonTransform.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            rect.localScale = Vector3.one;
        }

        private static void CycleConditionFilter(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene()) {
                switch (junkyardConditionFilterMode) {
                    case JunkyardConditionFilterMode.Off:
                        junkyardConditionFilterMode =
                            JunkyardConditionFilterMode.RepairThresholdToPerfect;
                        break;
                    case JunkyardConditionFilterMode.RepairThresholdToPerfect:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Red;
                        break;
                    case JunkyardConditionFilterMode.Red:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Orange;
                        break;
                    case JunkyardConditionFilterMode.Orange:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Yellow;
                        break;
                    case JunkyardConditionFilterMode.Yellow:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Green;
                        break;
                    default:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Off;
                        break;
                }
            } else {
                switch (garageConditionFilterMode) {
                    case GarageConditionFilterMode.Off:
                        garageConditionFilterMode = GarageConditionFilterMode.Red;
                        break;
                    case GarageConditionFilterMode.Red:
                        garageConditionFilterMode = GarageConditionFilterMode.Orange;
                        break;
                    case GarageConditionFilterMode.Orange:
                        garageConditionFilterMode = GarageConditionFilterMode.Yellow;
                        break;
                    case GarageConditionFilterMode.Yellow:
                        garageConditionFilterMode = GarageConditionFilterMode.GreenRing;
                        break;
                    case GarageConditionFilterMode.GreenRing:
                        garageConditionFilterMode = GarageConditionFilterMode.Perfect;
                        break;
                    default:
                        garageConditionFilterMode = GarageConditionFilterMode.Off;
                        break;
                }
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void CycleConditionFilterReverse(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene()) {
                switch (junkyardConditionFilterMode) {
                    case JunkyardConditionFilterMode.Off:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Green;
                        break;
                    case JunkyardConditionFilterMode.Green:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Yellow;
                        break;
                    case JunkyardConditionFilterMode.Yellow:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Orange;
                        break;
                    case JunkyardConditionFilterMode.Orange:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Red;
                        break;
                    case JunkyardConditionFilterMode.Red:
                        junkyardConditionFilterMode =
                            JunkyardConditionFilterMode.RepairThresholdToPerfect;
                        break;
                    default:
                        junkyardConditionFilterMode = JunkyardConditionFilterMode.Off;
                        break;
                }
            } else {
                switch (garageConditionFilterMode) {
                    case GarageConditionFilterMode.Off:
                        garageConditionFilterMode = GarageConditionFilterMode.Perfect;
                        break;
                    case GarageConditionFilterMode.Perfect:
                        garageConditionFilterMode = GarageConditionFilterMode.GreenRing;
                        break;
                    case GarageConditionFilterMode.GreenRing:
                        garageConditionFilterMode = GarageConditionFilterMode.Yellow;
                        break;
                    case GarageConditionFilterMode.Yellow:
                        garageConditionFilterMode = GarageConditionFilterMode.Orange;
                        break;
                    case GarageConditionFilterMode.Orange:
                        garageConditionFilterMode = GarageConditionFilterMode.Red;
                        break;
                    default:
                        garageConditionFilterMode = GarageConditionFilterMode.Off;
                        break;
                }
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void CycleRepairabilityFilter(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene()) {
                junkyardRepairabilityFilterMode =
                    GetNextRepairabilityMode(junkyardRepairabilityFilterMode);
            } else {
                garageRepairabilityFilterMode =
                    GetNextRepairabilityMode(garageRepairabilityFilterMode);
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static RepairabilityQuickFilterMode GetNextRepairabilityMode(
            RepairabilityQuickFilterMode current)
        {
            switch (current) {
                case RepairabilityQuickFilterMode.Off:
                    return RepairabilityQuickFilterMode.RepairGroupOnly;
                case RepairabilityQuickFilterMode.RepairGroupOnly:
                    return RepairabilityQuickFilterMode.NonRepairableOnly;
                default:
                    return RepairabilityQuickFilterMode.Off;
            }
        }

        private static void CycleRepairabilityFilterReverse(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene())
                junkyardRepairabilityFilterMode =
                    GetPreviousRepairabilityMode(junkyardRepairabilityFilterMode);
            else
                garageRepairabilityFilterMode =
                    GetPreviousRepairabilityMode(garageRepairabilityFilterMode);

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        internal static RepairabilityQuickFilterMode GetPreviousRepairabilityMode(
            RepairabilityQuickFilterMode current)
        {
            switch (current) {
                case RepairabilityQuickFilterMode.Off:
                    return RepairabilityQuickFilterMode.NonRepairableOnly;
                case RepairabilityQuickFilterMode.NonRepairableOnly:
                    return RepairabilityQuickFilterMode.RepairGroupOnly;
                default:
                    return RepairabilityQuickFilterMode.Off;
            }
        }

        private static void CycleQualityFilter(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene())
                junkyardQualityFilterMode = GetNextQualityMode(junkyardQualityFilterMode);
            else
                garageQualityFilterMode = GetNextQualityMode(garageQualityFilterMode);

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        internal static QualityQuickFilterMode GetNextQualityMode(
            QualityQuickFilterMode current)
        {
            switch (current) {
                case QualityQuickFilterMode.Off:
                    return QualityQuickFilterMode.Improved;
                case QualityQuickFilterMode.Improved:
                    return QualityQuickFilterMode.Quality1;
                case QualityQuickFilterMode.Quality1:
                    return QualityQuickFilterMode.Quality2;
                case QualityQuickFilterMode.Quality2:
                    return QualityQuickFilterMode.Quality3;
                case QualityQuickFilterMode.Quality3:
                    return QualityQuickFilterMode.NonImproved;
                default:
                    return QualityQuickFilterMode.Off;
            }
        }

        private static void CycleQualityFilterReverse(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene())
                junkyardQualityFilterMode = GetPreviousQualityMode(junkyardQualityFilterMode);
            else
                garageQualityFilterMode = GetPreviousQualityMode(garageQualityFilterMode);

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        internal static QualityQuickFilterMode GetPreviousQualityMode(
            QualityQuickFilterMode current)
        {
            switch (current) {
                case QualityQuickFilterMode.Off:
                    return QualityQuickFilterMode.NonImproved;
                case QualityQuickFilterMode.NonImproved:
                    return QualityQuickFilterMode.Quality3;
                case QualityQuickFilterMode.Quality3:
                    return QualityQuickFilterMode.Quality2;
                case QualityQuickFilterMode.Quality2:
                    return QualityQuickFilterMode.Quality1;
                case QualityQuickFilterMode.Quality1:
                    return QualityQuickFilterMode.Improved;
                default:
                    return QualityQuickFilterMode.Off;
            }
        }

        private static void CycleOwnedFilter(BaseInventory inventory)
        {
            if (!SupportsOwnedFilter(inventory))
                return;

            switch (ownedFilterMode) {
                case OwnedQuickFilterMode.Off:
                    ownedFilterMode = OwnedQuickFilterMode.Owned;
                    break;
                case OwnedQuickFilterMode.Owned:
                    ownedFilterMode = OwnedQuickFilterMode.Missing;
                    break;
                default:
                    ownedFilterMode = OwnedQuickFilterMode.Off;
                    break;
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void CycleOwnedFilterReverse(BaseInventory inventory)
        {
            if (!SupportsOwnedFilter(inventory))
                return;

            switch (ownedFilterMode) {
                case OwnedQuickFilterMode.Off:
                    ownedFilterMode = OwnedQuickFilterMode.Missing;
                    break;
                case OwnedQuickFilterMode.Missing:
                    ownedFilterMode = OwnedQuickFilterMode.Owned;
                    break;
                default:
                    ownedFilterMode = OwnedQuickFilterMode.Off;
                    break;
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        internal static void RegisterReverseQuickFilterClick(
            Button button, Action action)
        {
            if (button == null)
                return;

            int id = button.GetInstanceID();
            if (action == null)
                ReverseQuickFilterClicks.Remove(id);
            else
                ReverseQuickFilterClicks[id] = action;
        }

        internal static void UnregisterReverseQuickFilterClick(Button button)
        {
            if (button != null)
                ReverseQuickFilterClicks.Remove(button.GetInstanceID());
        }

        internal static bool TryHandleReverseQuickFilterClick(Button button)
        {
            if (button == null)
                return false;

            Action action;
            if (!ReverseQuickFilterClicks.TryGetValue(button.GetInstanceID(), out action) ||
                action == null)
                return false;

            action();
            return true;
        }

        private static void RedrawInventory(BaseInventory inventory)
        {
            inventory = ResolveActiveInventory(inventory);
            if (inventory == null)
                return;

            try {
                ForceRestoreSnapshot(inventory);
                ResetCurrentPage(inventory);
                Transform buttonRoot = GetButtonRoot(inventory);
                UpdateButtonVisuals(
                    FindDeepChild(buttonRoot, ConditionButtonName),
                    FindDeepChild(buttonRoot, RepairButtonName),
                    FindDeepChild(buttonRoot, QualityButtonName),
                    FindDeepChild(buttonRoot, OwnedButtonName),
                    IsBarnOrJunkyardScene());

                InputField activeSearchField = FindSearchField(inventory, true);
                if (activeSearchField != null && !IsBarnOrJunkyardScene()) {
                    // This is the exact public method already used by the mod to emulate Enter.
                    // The game rebuilds its normal text/category list first. Our DrawPage prefix
                    // applies repair filtering only after that native work is finished.
                    activeSearchField.SendOnSubmit();
                    inventory.RedrawCurrentPage();
                    return;
                }

                inventory.RedrawCurrentPage();
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to redraw filtered inventory." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }

        private static void UpdateButtonVisuals(Transform conditionButton,
            Transform repairButton, Transform qualityButton, Transform ownedButton,
            bool junkyardContext)
        {
            if (conditionButton != null) {
                Image conditionImage = conditionButton.GetComponent<Image>();
                if (conditionImage != null) {
                    bool enabled;
                    if (junkyardContext) {
                        enabled = junkyardConditionFilterMode !=
                            JunkyardConditionFilterMode.Off;
                        switch (junkyardConditionFilterMode) {
                            case JunkyardConditionFilterMode.RepairThresholdToPerfect:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetWhiteConditionIcon();
                                break;
                            case JunkyardConditionFilterMode.Orange:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetOrangeConditionIcon();
                                break;
                            case JunkyardConditionFilterMode.Yellow:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetYellowConditionIcon();
                                break;
                            case JunkyardConditionFilterMode.Green:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetGreenRingConditionIcon();
                                break;
                            case JunkyardConditionFilterMode.Red:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetRedConditionIcon();
                                break;
                            default:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetWhiteConditionIcon();
                                break;
                        }
                    } else {
                        enabled = garageConditionFilterMode !=
                            GarageConditionFilterMode.Off;
                        switch (garageConditionFilterMode) {
                            case GarageConditionFilterMode.Red:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetRedConditionIcon();
                                break;
                            case GarageConditionFilterMode.Orange:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetOrangeConditionIcon();
                                break;
                            case GarageConditionFilterMode.Yellow:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetYellowConditionIcon();
                                break;
                            case GarageConditionFilterMode.GreenRing:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetGreenRingConditionIcon();
                                break;
                            case GarageConditionFilterMode.Perfect:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetGreenConditionIcon();
                                break;
                            default:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetWhiteConditionIcon();
                                break;
                        }
                    }
                    if (enabled)
                        conditionImage.color = ActiveButtonColor;
                    else
                        conditionImage.color = DisabledButtonColor;
                }
            }

            if (repairButton != null) {
                Image repairImage = repairButton.GetComponent<Image>();
                if (repairImage != null) {
                    RepairabilityQuickFilterMode mode = junkyardContext
                        ? junkyardRepairabilityFilterMode
                        : garageRepairabilityFilterMode;

                    switch (mode) {
                        case RepairabilityQuickFilterMode.NonRepairableOnly:
                            repairImage.sprite = InventoryIconProvider.GetRedRepairWrenchIcon();
                            repairImage.color = ActiveButtonColor;
                            break;
                        case RepairabilityQuickFilterMode.RepairGroupOnly:
                            repairImage.sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
                            repairImage.color = ActiveButtonColor;
                            break;
                        default:
                            repairImage.sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
                            repairImage.color = DisabledButtonColor;
                            break;
                    }
                }
            }

            if (qualityButton != null) {
                Image qualityImage = qualityButton.GetComponent<Image>();
                if (qualityImage != null) {
                    QualityQuickFilterMode mode = junkyardContext
                        ? junkyardQualityFilterMode : garageQualityFilterMode;
                    switch (mode) {
                        case QualityQuickFilterMode.Improved:
                            qualityImage.sprite = InventoryIconProvider.GetQualityIcon();
                            qualityImage.color = ActiveButtonColor;
                            break;
                        case QualityQuickFilterMode.Quality1:
                            qualityImage.sprite = InventoryIconProvider.GetQuality1Icon();
                            qualityImage.color = ActiveButtonColor;
                            break;
                        case QualityQuickFilterMode.Quality2:
                            qualityImage.sprite = InventoryIconProvider.GetQuality2Icon();
                            qualityImage.color = ActiveButtonColor;
                            break;
                        case QualityQuickFilterMode.Quality3:
                            qualityImage.sprite = InventoryIconProvider.GetQuality3Icon();
                            qualityImage.color = ActiveButtonColor;
                            break;
                        case QualityQuickFilterMode.NonImproved:
                            qualityImage.sprite = InventoryIconProvider.GetQualityNonIcon();
                            qualityImage.color = ActiveButtonColor;
                            break;
                        default:
                            qualityImage.sprite = InventoryIconProvider.GetQualityIcon();
                            qualityImage.color = DisabledButtonColor;
                            break;
                    }
                }
            }

            if (junkyardContext && ownedButton != null) {
                Image ownedImage = ownedButton.GetComponent<Image>();
                if (ownedImage != null) {
                    switch (ownedFilterMode) {
                        case OwnedQuickFilterMode.Owned:
                            ownedImage.sprite =
                                InventoryIconProvider.GetWhiteWarehouseIcon();
                            ownedImage.color = ActiveButtonColor;
                            break;
                        case OwnedQuickFilterMode.Missing:
                            ownedImage.sprite =
                                InventoryIconProvider.GetRedWarehouseIcon();
                            ownedImage.color = ActiveButtonColor;
                            break;
                        default:
                            ownedImage.sprite =
                                InventoryIconProvider.GetWhiteWarehouseIcon();
                            ownedImage.color = DisabledButtonColor;
                            break;
                    }
                }
            }
        }

        private static Transform GetButtonRoot(BaseInventory inventory)
        {
            if (inventory == null)
                return null;

            WarehouseWindow parentWarehouse = inventory.GetComponentInParent<WarehouseWindow>();
            return parentWarehouse != null ? parentWarehouse.transform : inventory.transform;
        }

        private static BaseInventory ResolveActiveInventory(BaseInventory inventory)
        {
            if (inventory == null)
                return null;

            WarehouseWindow parentWarehouse = inventory.GetComponentInParent<WarehouseWindow>();
            if (parentWarehouse == null)
                return inventory;

            foreach (BaseInventory candidate in
                parentWarehouse.GetComponentsInChildren<BaseInventory>(true)) {
                if (candidate != null && candidate.gameObject.activeInHierarchy)
                    return candidate;
            }

            return inventory;
        }

        private static Transform FindSingleButton(Transform root, string name)
        {
            if (root == null)
                return null;

            Transform keep = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
                if (child == null || child.name != name)
                    continue;

                if (keep == null) {
                    keep = child;
                    continue;
                }

                UnregisterReverseQuickFilterClick(
                    child.GetComponent<Button>());
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }

            return keep;
        }


        private static void ClearSelectedButton()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms) {
                if (child != null && child.name == name)
                    return child;
            }
            return null;
        }
    }
}
