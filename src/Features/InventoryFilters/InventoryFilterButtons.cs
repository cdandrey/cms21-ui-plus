using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppInterop.Runtime;
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic.Warehouse;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using UnhollowerRuntimeLib;
using CMS.UI.Description;
using CMS.UI.Logic.Warehouse;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    internal sealed class QuickFilterMenuOption
    {
        internal readonly Sprite Sprite;
        internal readonly Color Color;
        internal readonly Action Select;
        internal readonly string Label;

        internal QuickFilterMenuOption(Sprite sprite, Color color, Action select,
            string label = null)
        {
            Sprite = sprite;
            Color = color;
            Select = select;
            Label = label;
        }
    }

    public static partial class InventoryFilterManager
    {
        private const string ConditionButtonName = "QInventoryQuickFilterCondition";
        private const string RepairButtonName = "QInventoryQuickFilterRepair";
        private const string QualityButtonName = "QInventoryQuickFilterQuality";
        private const string OwnedButtonName = "QInventoryQuickFilterOwned";
        private const string PackageButtonName = "QInventoryQuickFilterPackage";
        private const string ResetHintName = "Hint_ResetInventoryFilters";
        private const string SelectFilterHintName =
            "Hint_SelectInventoryFilter";
        private const string TravelCollectedSearchFieldName =
            "QTravelCollectedSearchField";


        private const float ButtonSize = 20f;
        private const float ButtonSpacing = 28f;
        private const float SearchRowYOffset = 0f;
        private const float FallbackY = -96f;
        private const float FallbackConditionX = -328f;
        private const float FallbackRepairX = -300f;
        private const float FallbackQualityX = -272f;
        private const float FallbackOwnedX = -244f;
        private const float FallbackPackageX = -244f;
        private const float QuickFilterMenuPadding = 4f;
        private const float QuickFilterMenuColumnWidth = 32f;
        private const float QuickFilterMenuLegendLineHeight = 11f;
        private const float QuickFilterMenuLegendHeight =
            QuickFilterMenuLegendLineHeight * 2f;
        private const float QuickFilterMenuColumnSpacing = 1f;
        private const int QuickFilterMenuLabelFontSize = 10;

        private static readonly Dictionary<int, QuickFilterMenuOption[]>
            QuickFilterMenus = new Dictionary<int, QuickFilterMenuOption[]>();
        private static readonly Dictionary<int, Button> QuickFilterMenuButtons =
            new Dictionary<int, Button>();

        private static readonly Color32 ActiveButtonColor =
            new Color32(255, 255, 255, 255);
        private static readonly Color32 DisabledButtonColor =
            new Color32(155, 155, 155, 210);
        private static readonly Color32 QuickFilterMenuBackgroundColor =
            new Color32(20, 20, 20, 235);
        private static readonly Color32 QuickFilterMenuSelectedColor =
            new Color32(58, 58, 58, 235);
        private static GameObject activeQuickFilterMenuRoot;
        private static GameObject activeQuickFilterBlockerRoot;
        private static Font quickFilterMenuFallbackFont;
        private static EventTrigger activeQuickFilterBlockerTrigger;
        private static int activeQuickFilterSourceId;
        private static BaseInventory resetHintInventory;
        private static BaseInventory activeFilteredInventory;
        private static NativeUiFactory.FooterHintHandle resetHint;
        private static NativeUiFactory.FooterHintHandle selectFilterHint;
        private static string resetHintWindowId;
        private static InputField travelCollectedSearchField;
        private static BaseInventory travelCollectedSearchInventory;
        private static string travelCollectedSearchText = string.Empty;

        private enum QuickFilterButtonKind
        {
            Condition,
            Repairability,
            Quality,
            Owned,
            Package,
        }

        public static void EnsureButtons(BaseInventory inventory)
        {
            try {
                EnsureButtonsUnsafe(inventory);
                SetActiveFilteredInventory(inventory);
                EnsureInventoryGroupingHint(inventory);
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

        internal static bool TryResetFromKeyboardShortcut()
        {
            BaseInventory inventory = activeFilteredInventory;
            if (inventory == null || inventory.gameObject == null ||
                !inventory.gameObject.activeInHierarchy)
                return false;

            ResetActiveFilters(inventory);
            return true;
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
                packageFilterMode = PackageQuickFilterMode.Off;
            }

            InputField searchField = FindSearchField(inventory, true);
            if (searchField != null) {
                if (IsTravelCollectedInventory(inventory)) {
                    travelCollectedSearchText = string.Empty;
                    searchField.SetTextWithoutNotify(string.Empty);
                } else {
                    searchField.text = string.Empty;
                    searchField.SendOnSubmit();
                }
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void CreateResetHint(BaseInventory inventory)
        {
            if (resetHint != null && resetHint.Root != null &&
                selectFilterHint != null && selectFilterHint.Root != null) {
                int currentCount = GetCurrentFilteredItemCount(inventory);
                WindowFooterHintController.SetNativeProfile(
                    resetHintWindowId,
                    ResolveFooterProfile(inventory, currentCount == 0),
                    currentCount);
                return;
            }

            string windowId;
            Transform windowRoot;
            Transform descriptionRoot;
            if (!TryResolveInventoryFooter(inventory, out windowId,
                    out windowRoot, out descriptionRoot))
                return;

            resetHintWindowId = windowId;
            int itemCount = GetCurrentFilteredItemCount(inventory);
            WindowFooterHintController.NativeFooterProfile footerProfile =
                ResolveFooterProfile(inventory, itemCount == 0);
            selectFilterHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = resetHintWindowId,
                    WindowRoot = windowRoot,
                    HintRoot = descriptionRoot,
                    HintId = SelectFilterHintName,
                    Keys = new string[] { "MouseLeft" },
                    Text = ModLocalization.Get("LOC_SetFilterAction"),
                    Action = null,
                    Row = 0,
                    Order = 9,
                    Profile = footerProfile,
                    ItemCount = itemCount,
                });
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

        private static bool TryResolveInventoryFooter(
            BaseInventory inventory, out string windowId,
            out Transform windowRoot, out Transform descriptionRoot)
        {
            windowId = null;
            windowRoot = null;
            descriptionRoot = null;
            if (inventory == null)
                return false;

            WarehouseWindow warehouse =
                inventory.GetComponentInParent<WarehouseWindow>();
            if (warehouse != null) {
                windowRoot = warehouse.transform;
                if (warehouse.uiDescription != null)
                    descriptionRoot = warehouse.uiDescription.transform;
            }

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
                    descriptionRoot = inventoryWindow.uiDescription.transform;
                    windowRoot = inventoryWindow.transform;
                }
            }

            if (descriptionRoot == null || windowRoot == null) {
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
                    return false;
            }

            windowId = IsBarnOrJunkyardScene()
                ? "TravelInventory"
                : warehouse != null ? "Warehouse" : "Inventory";
            return true;
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
            if (!string.IsNullOrEmpty(resetHintWindowId)) {
                WindowFooterHintController.RemoveHint(resetHintWindowId,
                    SelectFilterHintName);
                WindowFooterHintController.RemoveHint(resetHintWindowId,
                    ResetHintName);
            }
            selectFilterHint = null;
            resetHint = null;
            resetHintWindowId = null;
            resetHintInventory = null;
        }

        private static void EnsureButtonsUnsafe(BaseInventory inventory)
        {
            inventory = ResolveActiveInventory(inventory);
            if (!ShouldHandleWindow(inventory))
                return;

            EnsureTravelCollectedSearch(inventory);
            Transform buttonRoot = GetButtonRoot(inventory);
            bool junkyardContext = IsBarnOrJunkyardScene();
            bool packageContext = !junkyardContext &&
                IsInventoryGroupingEnabled() && SupportsInventoryGrouping(inventory) &&
                !IsExpandedInventoryPackage(inventory);
            Transform conditionButton = FindSingleButton(buttonRoot, ConditionButtonName);
            Transform repairButton = FindSingleButton(buttonRoot, RepairButtonName);
            Transform qualityButton = FindSingleButton(buttonRoot, QualityButtonName);
            Transform ownedButton = FindSingleButton(buttonRoot, OwnedButtonName);
            Transform packageButton = FindSingleButton(buttonRoot, PackageButtonName);

            if (conditionButton == null)
                conditionButton = CreateButton(inventory, buttonRoot,
                    ConditionButtonName, QuickFilterButtonKind.Condition);
            if (repairButton == null)
                repairButton = CreateButton(inventory, buttonRoot,
                    RepairButtonName, QuickFilterButtonKind.Repairability);
            if (qualityButton == null)
                qualityButton = CreateButton(inventory, buttonRoot,
                    QualityButtonName, QuickFilterButtonKind.Quality);

            if (packageContext) {
                if (packageButton == null)
                    packageButton = CreateButton(inventory, buttonRoot,
                        PackageButtonName, QuickFilterButtonKind.Package);
            } else if (packageButton != null) {
                UnregisterQuickFilterMenu(
                    packageButton.GetComponent<Button>());
                packageButton.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(packageButton.gameObject);
                packageButton = null;
            }

            if (junkyardContext) {
                if (ownedButton == null)
                    ownedButton = CreateButton(inventory, buttonRoot,
                        OwnedButtonName, QuickFilterButtonKind.Owned);
            } else if (ownedButton != null) {
                UnregisterQuickFilterMenu(
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
            if (packageContext)
                ConfigureButton(packageButton, inventory, QuickFilterButtonKind.Package);

            ApplyButtonLayout(inventory, conditionButton, repairButton,
                qualityButton, ownedButton, packageButton, junkyardContext,
                packageContext);
            UpdateButtonVisuals(conditionButton, repairButton, qualityButton,
                ownedButton, packageButton, junkyardContext, packageContext);
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
                case QuickFilterButtonKind.Package:
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

            QuickFilterMenuOption[] menuOptions;
            if (kind == QuickFilterButtonKind.Condition) {
                if (IsBarnOrJunkyardScene()) {
                    menuOptions = CreateJunkyardConditionQuickFilterMenu(
                        delegate (JunkyardConditionFilterMode mode) {
                            SelectJunkyardConditionFilter(
                                ResolveActiveInventory(inventory), mode);
                        },
                        JunkyardConditionFilterMode.Off,
                        JunkyardConditionFilterMode.RepairThresholdToPerfect,
                        JunkyardConditionFilterMode.Red,
                        JunkyardConditionFilterMode.Orange,
                        JunkyardConditionFilterMode.Yellow,
                        JunkyardConditionFilterMode.Green,
                        JunkyardConditionFilterMode.Perfect);
                } else {
                    menuOptions = CreateGarageConditionQuickFilterMenu(
                        delegate (GarageConditionFilterMode mode) {
                            SelectGarageConditionFilter(
                                ResolveActiveInventory(inventory), mode);
                        },
                        GarageConditionFilterMode.Off,
                        GarageConditionFilterMode.RepairThresholdToPerfect,
                        GarageConditionFilterMode.Red,
                        GarageConditionFilterMode.Orange,
                        GarageConditionFilterMode.Yellow,
                        GarageConditionFilterMode.GreenRing,
                        GarageConditionFilterMode.Perfect);
                }
            } else if (kind == QuickFilterButtonKind.Repairability) {
                menuOptions = CreateRepairabilityQuickFilterMenu(
                    delegate (RepairabilityQuickFilterMode mode) {
                        SelectRepairabilityFilter(
                            ResolveActiveInventory(inventory), mode);
                    });
            } else if (kind == QuickFilterButtonKind.Quality) {
                menuOptions = CreateQualityQuickFilterMenu(
                    delegate (QualityQuickFilterMode mode) {
                        SelectQualityFilter(
                            ResolveActiveInventory(inventory), mode);
                    });
            } else if (kind == QuickFilterButtonKind.Owned) {
                menuOptions = CreateOwnedQuickFilterMenu(
                    delegate (OwnedQuickFilterMode mode) {
                        SelectOwnedFilter(ResolveActiveInventory(inventory), mode);
                    });
            } else {
                menuOptions = CreatePackageQuickFilterMenu(
                    delegate (PackageQuickFilterMode mode) {
                        SelectPackageFilter(ResolveActiveInventory(inventory), mode);
                    });
            }
            RegisterQuickFilterMenu(button, menuOptions);
        }

        private static void ApplyButtonLayout(BaseInventory inventory,
            Transform conditionButton, Transform repairButton,
            Transform qualityButton, Transform ownedButton,
            Transform packageButton, bool junkyardContext, bool packageContext)
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
                    float packageOffset = 18f;
                    float qualityOffset = packageContext
                        ? packageOffset + ButtonSpacing : packageOffset;
                    float repairOffset = qualityOffset + ButtonSpacing;
                    float conditionOffset = repairOffset + ButtonSpacing;

                    Vector3 packageWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - packageOffset, centerY, 0f));
                    Vector3 qualityWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - qualityOffset, centerY, 0f));
                    Vector3 repairWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - repairOffset, centerY, 0f));
                    Vector3 conditionWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - conditionOffset, centerY, 0f));

                    SetButtonWorldPosition(conditionButton, parent, conditionWorld);
                    SetButtonWorldPosition(repairButton, parent, repairWorld);
                    SetButtonWorldPosition(qualityButton, parent, qualityWorld);
                    if (packageContext)
                        SetButtonWorldPosition(packageButton, parent, packageWorld);
                    return;
                }

                if (junkyardContext && IsTravelCollectedInventory(inventory)) {
                    float centerY = searchRect.rect.center.y + SearchRowYOffset;
                    float ownedOffset = 18f;
                    float qualityOffset = ownedOffset + ButtonSpacing;
                    float repairOffset = qualityOffset + ButtonSpacing;
                    float conditionOffset = repairOffset + ButtonSpacing;

                    Vector3 ownedWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - ownedOffset, centerY, 0f));
                    Vector3 qualityWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - qualityOffset, centerY, 0f));
                    Vector3 repairWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - repairOffset, centerY, 0f));
                    Vector3 conditionWorld = searchRect.TransformPoint(new Vector3(
                        searchRect.rect.xMin - conditionOffset, centerY, 0f));

                    SetButtonWorldPosition(conditionButton, inventory.transform,
                        conditionWorld);
                    SetButtonWorldPosition(repairButton, inventory.transform,
                        repairWorld);
                    SetButtonWorldPosition(qualityButton, inventory.transform,
                        qualityWorld);
                    SetButtonWorldPosition(ownedButton, inventory.transform,
                        ownedWorld);
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
                } else if (packageContext) {
                    SetButtonPosition(conditionButton, normalParent, searchRect,
                        rightButtonX - (ButtonSpacing * 3f), y);
                    SetButtonPosition(repairButton, normalParent, searchRect,
                        rightButtonX - (ButtonSpacing * 2f), y);
                    SetButtonPosition(qualityButton, normalParent, searchRect,
                        rightButtonX - ButtonSpacing, y);
                    SetButtonPosition(packageButton, normalParent, searchRect,
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
            else if (packageContext)
                SetFallbackPosition(packageButton, inventory.transform,
                    FallbackPackageX, FallbackY);
        }

        private static InputField FindSearchField(BaseInventory inventory, bool activeOnly)
        {
            if (inventory == null)
                return null;

            Transform searchRoot = inventory.transform;
            WarehouseWindow parentWarehouse = inventory.GetComponentInParent<WarehouseWindow>();
            if (parentWarehouse != null)
                searchRoot = parentWarehouse.transform;
            else if (IsTravelCollectedInventory(inventory)) {
                Transform exchangeRoot = FindItemsExchangeWindow(inventory.transform);
                if (exchangeRoot != null)
                    searchRoot = exchangeRoot;
            }

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

        internal static string GetTravelCollectedSearchText(
            BaseInventory inventory)
        {
            return IsTravelCollectedInventory(inventory)
                ? travelCollectedSearchText : string.Empty;
        }

        internal static void ResetTravelCollectedSearch()
        {
            travelCollectedSearchText = string.Empty;
            travelCollectedSearchInventory = null;
            travelCollectedSearchField = null;
        }

        private static bool IsTravelCollectedInventory(BaseInventory inventory)
        {
            return inventory != null && IsBarnOrJunkyardScene() &&
                (string.Equals(inventory.name, "Collected",
                     StringComparison.Ordinal) ||
                 string.Equals(inventory.name, "Found",
                     StringComparison.Ordinal)) &&
                FindItemsExchangeWindow(inventory.transform) != null;
        }

        private static Transform FindItemsExchangeWindow(Transform transform)
        {
            Transform current = transform;
            while (current != null) {
                if (string.Equals(current.name, "ItemsExchangeWindow",
                        StringComparison.Ordinal))
                    return current;
                current = current.parent;
            }
            return null;
        }

        private static void EnsureTravelCollectedSearch(BaseInventory inventory)
        {
            if (!IsBarnOrJunkyardScene())
                return;

            if (!IsTravelCollectedInventory(inventory)) {
                if (travelCollectedSearchField != null &&
                    travelCollectedSearchField.gameObject != null)
                    travelCollectedSearchField.gameObject.SetActive(false);
                return;
            }

            if (travelCollectedSearchField != null &&
                travelCollectedSearchField.gameObject != null &&
                travelCollectedSearchInventory == inventory) {
                travelCollectedSearchField.gameObject.SetActive(true);
                return;
            }

            Transform exchangeRoot = FindItemsExchangeWindow(inventory.transform);
            if (exchangeRoot == null)
                return;

            Transform existing = FindDeepChild(exchangeRoot,
                TravelCollectedSearchFieldName);
            InputField field = existing != null
                ? existing.GetComponent<InputField>() : null;
            if (field == null) {
                InputField template = FindTravelSearchTemplate(exchangeRoot);
                if (template == null || template.gameObject == null)
                    return;

                GameObject clone = GameObject.Instantiate(
                    template.gameObject, exchangeRoot);
                clone.name = TravelCollectedSearchFieldName;
                clone.transform.localScale = Vector3.one;
                RectTransform sourceRect =
                    template.GetComponent<RectTransform>();
                RectTransform cloneRect = clone.GetComponent<RectTransform>();
                if (sourceRect != null && cloneRect != null) {
                    cloneRect.position = sourceRect.position;
                    cloneRect.rotation = sourceRect.rotation;
                    cloneRect.sizeDelta = sourceRect.sizeDelta;
                }
                field = clone.GetComponent<InputField>();
            }
            if (field == null)
                return;

            UnityEventUtility.RemoveAllListeners(field);
            travelCollectedSearchField = field;
            travelCollectedSearchInventory = inventory;
            field.SetTextWithoutNotify(travelCollectedSearchText);
            Action<string> changed = delegate (string value) {
                OnTravelCollectedSearchChanged(inventory, value);
            };
            UnityAction<string> changedAction =
                DelegateSupport.ConvertDelegate<UnityAction<string>>(changed);
            field.onValueChanged.AddListener(changedAction);
            field.gameObject.SetActive(true);
            field.transform.SetAsLastSibling();
        }

        private static InputField FindTravelSearchTemplate(Transform exchangeRoot)
        {
            Transform searchRoot = exchangeRoot.parent != null
                ? exchangeRoot.parent : exchangeRoot.root;
            if (searchRoot == null)
                return null;

            foreach (InputField field in
                searchRoot.GetComponentsInChildren<InputField>(true)) {
                if (field == null || field.gameObject == null ||
                    field.name == TravelCollectedSearchFieldName)
                    continue;
                if (string.Equals(field.name, "SearchField",
                        StringComparison.OrdinalIgnoreCase))
                    return field;
            }
            return null;
        }

        private static void OnTravelCollectedSearchChanged(
            BaseInventory inventory, string value)
        {
            if (inventory == null)
                return;

            travelCollectedSearchText = value ?? string.Empty;
            ClearSelectedButton();
            RedrawInventory(inventory);
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

        private static void CyclePackageFilter(BaseInventory inventory)
        {
            if (inventory == null || !IsInventoryGroupingEnabled() ||
                !SupportsInventoryGrouping(inventory))
                return;

            CollapseExpandedPackageWithoutRedraw(inventory);
            switch (packageFilterMode) {
                case PackageQuickFilterMode.Off:
                    packageFilterMode = PackageQuickFilterMode.Packages;
                    break;
                case PackageQuickFilterMode.Packages:
                    packageFilterMode = PackageQuickFilterMode.Singles;
                    break;
                default:
                    packageFilterMode = PackageQuickFilterMode.Off;
                    break;
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void SelectGarageConditionFilter(BaseInventory inventory,
            GarageConditionFilterMode mode)
        {
            if (inventory == null || garageConditionFilterMode == mode)
                return;

            garageConditionFilterMode = mode;
            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void SelectJunkyardConditionFilter(BaseInventory inventory,
            JunkyardConditionFilterMode mode)
        {
            if (inventory == null || junkyardConditionFilterMode == mode)
                return;

            junkyardConditionFilterMode = mode;
            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void SelectRepairabilityFilter(BaseInventory inventory,
            RepairabilityQuickFilterMode mode)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene()) {
                if (junkyardRepairabilityFilterMode == mode)
                    return;
                junkyardRepairabilityFilterMode = mode;
            } else {
                if (garageRepairabilityFilterMode == mode)
                    return;
                garageRepairabilityFilterMode = mode;
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void SelectQualityFilter(BaseInventory inventory,
            QualityQuickFilterMode mode)
        {
            if (inventory == null)
                return;

            if (IsBarnOrJunkyardScene()) {
                if (junkyardQualityFilterMode == mode)
                    return;
                junkyardQualityFilterMode = mode;
            } else {
                if (garageQualityFilterMode == mode)
                    return;
                garageQualityFilterMode = mode;
            }

            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void SelectOwnedFilter(BaseInventory inventory,
            OwnedQuickFilterMode mode)
        {
            if (!SupportsOwnedFilter(inventory) || ownedFilterMode == mode)
                return;

            ownedFilterMode = mode;
            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        private static void SelectPackageFilter(BaseInventory inventory,
            PackageQuickFilterMode mode)
        {
            if (inventory == null || !IsInventoryGroupingEnabled() ||
                !SupportsInventoryGrouping(inventory) || packageFilterMode == mode)
                return;

            CollapseExpandedPackageWithoutRedraw(inventory);
            packageFilterMode = mode;
            ClearSelectedButton();
            RedrawInventory(inventory);
        }

        internal static QuickFilterMenuOption[] CreateGarageConditionQuickFilterMenu(
            Action<GarageConditionFilterMode> select,
            params GarageConditionFilterMode[] modes)
        {
            if (select == null || modes == null || modes.Length == 0)
                return null;

            QuickFilterMenuOption[] options =
                new QuickFilterMenuOption[modes.Length];
            for (int i = 0; i < modes.Length; i++) {
                GarageConditionFilterMode mode = modes[i];
                options[i] = new QuickFilterMenuOption(
                    GetGarageConditionMenuSprite(mode),
                    mode == GarageConditionFilterMode.Off
                        ? DisabledButtonColor : ActiveButtonColor,
                    delegate () { select(mode); },
                    GetGarageConditionMenuLabel(mode));
            }
            return options;
        }

        internal static QuickFilterMenuOption[] CreateJunkyardConditionQuickFilterMenu(
            Action<JunkyardConditionFilterMode> select,
            params JunkyardConditionFilterMode[] modes)
        {
            if (select == null || modes == null || modes.Length == 0)
                return null;

            QuickFilterMenuOption[] options =
                new QuickFilterMenuOption[modes.Length];
            for (int i = 0; i < modes.Length; i++) {
                JunkyardConditionFilterMode mode = modes[i];
                options[i] = new QuickFilterMenuOption(
                    GetJunkyardConditionMenuSprite(mode),
                    mode == JunkyardConditionFilterMode.Off
                        ? DisabledButtonColor : ActiveButtonColor,
                    delegate () { select(mode); },
                    GetJunkyardConditionMenuLabel(mode));
            }
            return options;
        }

        internal static QuickFilterMenuOption[] CreateRepairabilityQuickFilterMenu(
            Action<RepairabilityQuickFilterMode> select)
        {
            if (select == null)
                return null;

            return new[] {
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetWhiteRepairWrenchIcon(),
                    DisabledButtonColor,
                    delegate () { select(RepairabilityQuickFilterMode.Off); }),
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetWhiteRepairWrenchIcon(),
                    ActiveButtonColor,
                    delegate () {
                        select(RepairabilityQuickFilterMode.RepairGroupOnly);
                    }),
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetRedRepairWrenchIcon(),
                    ActiveButtonColor,
                    delegate () {
                        select(RepairabilityQuickFilterMode.NonRepairableOnly);
                    }),
            };
        }

        internal static QuickFilterMenuOption[] CreateRestorationAvailabilityQuickFilterMenu(
            Action<RestorationAvailabilityQuickFilterMode> select)
        {
            if (select == null)
                return null;

            Sprite available = InventoryIconProvider.GetExternalIcon(
                GameplayRepairSkillBridge.GetRepairAvailabilityIndicatorPath(true));
            Sprite unavailable = InventoryIconProvider.GetExternalIcon(
                GameplayRepairSkillBridge.GetRepairAvailabilityIndicatorPath(false));
            return new[] {
                new QuickFilterMenuOption(available, DisabledButtonColor,
                    delegate () {
                        select(RestorationAvailabilityQuickFilterMode.Off);
                    }),
                new QuickFilterMenuOption(available, ActiveButtonColor,
                    delegate () {
                        select(RestorationAvailabilityQuickFilterMode.AvailableOnly);
                    }),
                new QuickFilterMenuOption(unavailable, ActiveButtonColor,
                    delegate () {
                        select(RestorationAvailabilityQuickFilterMode.UnavailableOnly);
                    }),
            };
        }

        internal static QuickFilterMenuOption[] CreateQualityQuickFilterMenu(
            Action<QualityQuickFilterMode> select)
        {
            if (select == null)
                return null;

            return new[] {
                new QuickFilterMenuOption(InventoryIconProvider.GetQualityIcon(),
                    DisabledButtonColor,
                    delegate () { select(QualityQuickFilterMode.Off); }),
                new QuickFilterMenuOption(InventoryIconProvider.GetQualityIcon(),
                    ActiveButtonColor,
                    delegate () { select(QualityQuickFilterMode.Improved); },
                    "1 ... 3"),
                new QuickFilterMenuOption(InventoryIconProvider.GetQuality1Icon(),
                    ActiveButtonColor,
                    delegate () { select(QualityQuickFilterMode.Quality1); },
                    "1"),
                new QuickFilterMenuOption(InventoryIconProvider.GetQuality2Icon(),
                    ActiveButtonColor,
                    delegate () { select(QualityQuickFilterMode.Quality2); },
                    "2"),
                new QuickFilterMenuOption(InventoryIconProvider.GetQuality3Icon(),
                    ActiveButtonColor,
                    delegate () { select(QualityQuickFilterMode.Quality3); },
                    "3"),
                new QuickFilterMenuOption(InventoryIconProvider.GetQualityNonIcon(),
                    ActiveButtonColor,
                    delegate () { select(QualityQuickFilterMode.NonImproved); },
                    "0"),
            };
        }

        internal static QuickFilterMenuOption[] CreateOwnedQuickFilterMenu(
            Action<OwnedQuickFilterMode> select)
        {
            if (select == null)
                return null;

            return new[] {
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetWhiteWarehouseIcon(),
                    DisabledButtonColor,
                    delegate () { select(OwnedQuickFilterMode.Off); }),
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetWhiteWarehouseIcon(),
                    ActiveButtonColor,
                    delegate () { select(OwnedQuickFilterMode.Owned); }),
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetRedWarehouseIcon(),
                    ActiveButtonColor,
                    delegate () { select(OwnedQuickFilterMode.Missing); }),
            };
        }

        internal static QuickFilterMenuOption[] CreatePackageQuickFilterMenu(
            Action<PackageQuickFilterMode> select)
        {
            if (select == null)
                return null;

            return new[] {
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetWhiteWarehouseIcon(),
                    DisabledButtonColor,
                    delegate () { select(PackageQuickFilterMode.Off); }),
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetWhiteWarehouseIcon(),
                    ActiveButtonColor,
                    delegate () { select(PackageQuickFilterMode.Packages); }),
                new QuickFilterMenuOption(
                    InventoryIconProvider.GetRedWarehouseIcon(),
                    ActiveButtonColor,
                    delegate () { select(PackageQuickFilterMode.Singles); }),
            };
        }

        internal static void RegisterQuickFilterMenu(
            Button button, QuickFilterMenuOption[] options)
        {
            if (button == null)
                return;

            int id = button.GetInstanceID();
            if (options == null || options.Length == 0) {
                QuickFilterMenus.Remove(id);
                QuickFilterMenuButtons.Remove(id);
                return;
            }

            QuickFilterMenus[id] = options;
            QuickFilterMenuButtons[id] = button;
            Action openMenuAction = delegate () {
                TryHandleQuickFilterMenuClick(button);
            };
            button.onClick.AddListener(openMenuAction);
        }

        internal static void UnregisterQuickFilterMenu(Button button)
        {
            if (button == null)
                return;

            int id = button.GetInstanceID();
            QuickFilterMenus.Remove(id);
            QuickFilterMenuButtons.Remove(id);
            if (activeQuickFilterSourceId == id)
                CloseQuickFilterMenu();
        }

        internal static bool TryHandleQuickFilterMenuClick(Button button)
        {
            if (button == null)
                return false;

            QuickFilterMenuOption[] options;
            if (!QuickFilterMenus.TryGetValue(button.GetInstanceID(), out options) ||
                options == null || options.Length == 0) {
                if (activeQuickFilterMenuRoot != null)
                    CloseQuickFilterMenu();
                return false;
            }

            OpenQuickFilterMenu(button, options);
            return true;
        }

        internal static void CloseQuickFilterMenu()
        {
            activeQuickFilterSourceId = 0;
            activeQuickFilterBlockerTrigger = null;

            if (activeQuickFilterMenuRoot != null)
                UnityEngine.Object.Destroy(activeQuickFilterMenuRoot);
            if (activeQuickFilterBlockerRoot != null)
                UnityEngine.Object.Destroy(activeQuickFilterBlockerRoot);

            activeQuickFilterMenuRoot = null;
            activeQuickFilterBlockerRoot = null;
        }

        internal static bool TryCloseQuickFilterMenu()
        {
            if (activeQuickFilterSourceId == 0)
                return false;

            if (activeQuickFilterMenuRoot == null) {
                CloseQuickFilterMenu();
                return false;
            }

            CloseQuickFilterMenu();
            return true;
        }

        internal static bool TryHandleQuickFilterBlockerEvent(
            EventTrigger trigger, BaseEventData eventData)
        {
            if (activeQuickFilterMenuRoot == null || trigger == null ||
                trigger != activeQuickFilterBlockerTrigger)
                return false;

            CloseQuickFilterMenu();
            if (eventData != null)
                eventData.Use();
            return true;
        }

        internal static bool TryHandleQuickFilterButtonPointerClick(
            Button button, PointerEventData eventData)
        {
            if (activeQuickFilterMenuRoot == null || button == null ||
                eventData == null)
                return false;

            Transform buttonTransform = button.transform;
            bool isMenuOption = buttonTransform != null &&
                buttonTransform.IsChildOf(activeQuickFilterMenuRoot.transform);
            bool isSwitchButton = buttonTransform != null &&
                activeQuickFilterBlockerRoot != null &&
                buttonTransform.IsChildOf(activeQuickFilterBlockerRoot.transform);
            if ((isMenuOption || isSwitchButton) &&
                eventData.button == PointerEventData.InputButton.Left)
                return false;

            CloseQuickFilterMenu();
            eventData.Use();
            return true;
        }

        private static IEnumerator SelectQuickFilterOptionDeferred(Action select)
        {
            yield return null;
            if (select != null)
                select();
        }

        private static void ApplyQuickFilterOptionVisual(Button sourceButton,
            QuickFilterMenuOption option)
        {
            if (sourceButton == null || option == null)
                return;

            Image image = sourceButton.GetComponent<Image>();
            if (image == null)
                return;

            image.sprite = option.Sprite;
            image.color = option.Color;
            image.preserveAspect = true;
        }

        private static Font FindQuickFilterMenuFont(Canvas canvas)
        {
            Font nativeFont = NativeUiFactory.Font;
            if (nativeFont != null)
                return nativeFont;
            if (quickFilterMenuFallbackFont != null)
                return quickFilterMenuFallbackFont;
            if (canvas == null)
                return null;

            foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) {
                if (text == null || text.font == null)
                    continue;

                quickFilterMenuFallbackFont = text.font;
                return quickFilterMenuFallbackFont;
            }
            return null;
        }

        private static void OpenQuickFilterMenu(Button sourceButton,
            QuickFilterMenuOption[] options)
        {
            CloseQuickFilterMenu();
            if (sourceButton == null || options == null || options.Length == 0)
                return;

            Canvas canvas = sourceButton.GetComponentInParent<Canvas>();
            RectTransform sourceRect = sourceButton.GetComponent<RectTransform>();
            if (canvas == null || sourceRect == null)
                return;

            GameObject blockerObject = CreateQuickFilterRectObject(
                "QQuickFilterMenuBlocker");
            blockerObject.transform.SetParent(canvas.transform, false);
            blockerObject.layer = sourceButton.gameObject.layer;
            RectTransform blockerRect = blockerObject.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blockerRect.localScale = Vector3.one;

            Image blockerImage = blockerObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0f);
            blockerImage.raycastTarget = true;
            EventTrigger blockerTrigger =
                blockerObject.AddComponent<EventTrigger>();
            blockerObject.transform.SetAsLastSibling();
            CreateQuickFilterSwitchButtons(blockerObject.transform, canvas,
                sourceButton);

            bool hasLabels = false;
            for (int i = 0; i < options.Length; i++) {
                if (options[i] != null && !string.IsNullOrEmpty(options[i].Label)) {
                    hasLabels = true;
                    break;
                }
            }

            Font menuFont = hasLabels ? FindQuickFilterMenuFont(canvas) : null;
            float optionHeight = ButtonSize +
                (hasLabels ? QuickFilterMenuLegendHeight : 0f);
            float menuWidth = (options.Length * QuickFilterMenuColumnWidth) +
                ((options.Length - 1) * QuickFilterMenuColumnSpacing);
            float menuHeight = optionHeight +
                (QuickFilterMenuPadding * 2f);

            GameObject menuObject = CreateQuickFilterRectObject(
                "QQuickFilterMenu");
            menuObject.transform.SetParent(canvas.transform, false);
            menuObject.layer = sourceButton.gameObject.layer;
            RectTransform menuRect = menuObject.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.5f, 0.5f);
            menuRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuRect.pivot = new Vector2(1f, 0f);
            menuRect.sizeDelta = new Vector2(menuWidth, menuHeight);
            menuRect.localScale = Vector3.one;
            menuRect.position = sourceRect.TransformPoint(new Vector3(
                sourceRect.rect.xMax, sourceRect.rect.yMax, 0f));

            Image menuImage = menuObject.AddComponent<Image>();
            menuImage.color = QuickFilterMenuBackgroundColor;
            menuImage.raycastTarget = false;

            Image[] optionBackgrounds = new Image[options.Length];
            for (int i = 0; i < options.Length; i++) {
                QuickFilterMenuOption option = options[i];
                if (option == null)
                    continue;

                GameObject optionObject = CreateQuickFilterRectObject(
                    "QQuickFilterMenuOption" + i);
                optionObject.transform.SetParent(menuObject.transform, false);
                optionObject.layer = sourceButton.gameObject.layer;
                RectTransform optionRect =
                    optionObject.GetComponent<RectTransform>();
                optionRect.anchorMin = new Vector2(0f, 0f);
                optionRect.anchorMax = new Vector2(0f, 0f);
                optionRect.pivot = new Vector2(0f, 0f);
                optionRect.anchoredPosition = new Vector2(
                    i * (QuickFilterMenuColumnWidth +
                        QuickFilterMenuColumnSpacing), 0f);
                optionRect.sizeDelta = new Vector2(
                    QuickFilterMenuColumnWidth, menuHeight);
                optionRect.localScale = Vector3.one;

                Image optionBackground = optionObject.AddComponent<Image>();
                optionBackground.color = IsQuickFilterOptionSelected(
                    sourceButton, option)
                    ? QuickFilterMenuSelectedColor
                    : new Color32(0, 0, 0, 0);
                optionBackground.raycastTarget = true;
                optionBackgrounds[i] = optionBackground;

                GameObject iconObject = CreateQuickFilterRectObject(
                    "QQuickFilterMenuOptionIcon" + i);
                iconObject.transform.SetParent(optionObject.transform, false);
                iconObject.layer = sourceButton.gameObject.layer;
                RectTransform iconRect =
                    iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0f);
                iconRect.anchorMax = new Vector2(0.5f, 0f);
                iconRect.pivot = new Vector2(0.5f, 0f);
                iconRect.anchoredPosition = new Vector2(0f,
                    QuickFilterMenuPadding);
                iconRect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
                iconRect.localScale = Vector3.one;

                Image optionImage = iconObject.AddComponent<Image>();
                optionImage.sprite = option.Sprite;
                optionImage.color = option.Color;
                optionImage.preserveAspect = true;
                optionImage.raycastTarget = false;

                if (!string.IsNullOrEmpty(option.Label))
                    CreateQuickFilterMenuOptionLabel(optionObject.transform,
                        i, option.Label, menuFont);

                Button optionButton = optionObject.AddComponent<Button>();
                optionButton.targetGraphic = optionBackground;
                optionButton.transition = Selectable.Transition.None;
                Navigation optionNavigation = optionButton.navigation;
                optionNavigation.mode = Navigation.Mode.None;
                optionButton.navigation = optionNavigation;
                int optionIndex = i;
                Action selectAction = delegate () {
                    if (IsQuickFilterOptionSelected(sourceButton, option)) {
                        CloseQuickFilterMenu();
                        return;
                    }

                    ApplyQuickFilterOptionVisual(sourceButton, option);
                    UpdateQuickFilterMenuSelection(optionBackgrounds,
                        optionIndex);
                    if (option.Select != null)
                        MelonCoroutines.Start(
                            SelectQuickFilterOptionDeferred(option.Select));
                };
                UnityAction selectUnityAction =
                    DelegateSupport.ConvertDelegate<UnityAction>(selectAction);
                optionButton.onClick.AddListener(selectUnityAction);
            }

            menuObject.transform.SetAsLastSibling();
            activeQuickFilterSourceId = sourceButton.GetInstanceID();
            activeQuickFilterBlockerRoot = blockerObject;
            activeQuickFilterBlockerTrigger = blockerTrigger;
            activeQuickFilterMenuRoot = menuObject;
        }

        private static void CreateQuickFilterSwitchButtons(
            Transform blocker, Canvas canvas, Button sourceButton)
        {
            if (blocker == null || canvas == null || sourceButton == null)
                return;

            foreach (KeyValuePair<int, Button> pair in QuickFilterMenuButtons) {
                Button targetButton = pair.Value;
                if (targetButton == null || targetButton == sourceButton ||
                    !targetButton.gameObject.activeInHierarchy ||
                    targetButton.GetComponentInParent<Canvas>() != canvas)
                    continue;

                QuickFilterMenuOption[] targetOptions;
                if (!QuickFilterMenus.TryGetValue(pair.Key, out targetOptions) ||
                    targetOptions == null || targetOptions.Length == 0)
                    continue;

                RectTransform targetRect =
                    targetButton.GetComponent<RectTransform>();
                if (targetRect == null)
                    continue;

                GameObject proxyObject = CreateQuickFilterRectObject(
                    "QQuickFilterSwitch" + pair.Key);
                proxyObject.transform.SetParent(blocker, false);
                proxyObject.layer = sourceButton.gameObject.layer;
                RectTransform proxyRect =
                    proxyObject.GetComponent<RectTransform>();
                proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
                proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
                proxyRect.pivot = new Vector2(0.5f, 0.5f);
                proxyRect.position = targetRect.TransformPoint(
                    targetRect.rect.center);
                proxyRect.sizeDelta = targetRect.rect.size;
                proxyRect.localScale = Vector3.one;

                Image proxyImage = proxyObject.AddComponent<Image>();
                proxyImage.color = new Color32(0, 0, 0, 0);
                proxyImage.raycastTarget = true;

                Button proxyButton = proxyObject.AddComponent<Button>();
                proxyButton.targetGraphic = proxyImage;
                proxyButton.transition = Selectable.Transition.None;
                Navigation proxyNavigation = proxyButton.navigation;
                proxyNavigation.mode = Navigation.Mode.None;
                proxyButton.navigation = proxyNavigation;
                Button capturedButton = targetButton;
                QuickFilterMenuOption[] capturedOptions = targetOptions;
                Action switchAction = delegate () {
                    OpenQuickFilterMenu(capturedButton, capturedOptions);
                };
                UnityAction switchUnityAction =
                    DelegateSupport.ConvertDelegate<UnityAction>(switchAction);
                proxyButton.onClick.AddListener(switchUnityAction);
            }
        }

        private static void CreateQuickFilterMenuOptionLabel(
            Transform parent, int optionIndex, string value, Font menuFont)
        {
            const string rangeSeparator = " ... ";
            float legendLeft = (QuickFilterMenuColumnWidth - ButtonSize) * 0.5f;
            int separatorIndex = value.IndexOf(rangeSeparator,
                StringComparison.Ordinal);
            if (separatorIndex <= 0) {
                CreateQuickFilterMenuOptionText(parent, optionIndex,
                    "Value", value, TextAnchor.MiddleCenter, menuFont,
                    legendLeft, QuickFilterMenuLegendLineHeight * 0.5f,
                    ButtonSize, QuickFilterMenuLegendLineHeight);
                return;
            }

            string startValue = value.Substring(0, separatorIndex);
            string endValue = value.Substring(separatorIndex +
                rangeSeparator.Length);
            CreateQuickFilterMenuOptionText(parent, optionIndex,
                "End", endValue, TextAnchor.MiddleCenter, menuFont,
                legendLeft, QuickFilterMenuLegendLineHeight,
                ButtonSize, QuickFilterMenuLegendLineHeight);
            CreateQuickFilterMenuOptionText(parent, optionIndex,
                "Start", startValue, TextAnchor.MiddleCenter, menuFont,
                legendLeft, 0f, ButtonSize, QuickFilterMenuLegendLineHeight);
        }

        private static bool IsQuickFilterOptionSelected(Button sourceButton,
            QuickFilterMenuOption option)
        {
            if (sourceButton == null || option == null)
                return false;

            Image image = sourceButton.GetComponent<Image>();
            if (image == null)
                return false;

            return image.sprite == option.Sprite && image.color == option.Color;
        }

        private static void UpdateQuickFilterMenuSelection(
            Image[] optionBackgrounds, int selectedIndex)
        {
            if (optionBackgrounds == null)
                return;

            for (int i = 0; i < optionBackgrounds.Length; i++) {
                Image background = optionBackgrounds[i];
                if (background == null)
                    continue;

                background.color = i == selectedIndex
                    ? QuickFilterMenuSelectedColor
                    : new Color32(0, 0, 0, 0);
            }
        }

        private static void CreateQuickFilterMenuOptionText(Transform parent,
            int optionIndex, string suffix, string value,
            TextAnchor alignment, Font menuFont, float x, float y,
            float width, float height)
        {
            Text text = NativeUiFactory.CreateText(parent,
                "QQuickFilterMenuOptionLabel" + optionIndex + suffix,
                value, QuickFilterMenuLabelFontSize, alignment, Color.white);
            if (text == null)
                return;

            if (text.font == null && menuFont != null)
                text.font = menuFont;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rect = text.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(x,
                ButtonSize + QuickFilterMenuPadding + y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static GameObject CreateQuickFilterRectObject(string name)
        {
#if NET6_0_OR_GREATER
            return new GameObject(name, typeof(RectTransform));
#else
            UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type>
                componentTypes =
                    new UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type>(1);
            componentTypes[0] =
                UnhollowerRuntimeLib.Il2CppType.Of<RectTransform>();
            return new GameObject(name, componentTypes);
#endif
        }

        private static Sprite GetGarageConditionMenuSprite(
            GarageConditionFilterMode mode)
        {
            switch (mode) {
                case GarageConditionFilterMode.Red:
                    return InventoryIconProvider.GetRedConditionIcon();
                case GarageConditionFilterMode.Orange:
                    return InventoryIconProvider.GetOrangeConditionIcon();
                case GarageConditionFilterMode.Yellow:
                    return InventoryIconProvider.GetYellowConditionIcon();
                case GarageConditionFilterMode.GreenRing:
                    return InventoryIconProvider.GetGreenRingConditionIcon();
                case GarageConditionFilterMode.Perfect:
                    return InventoryIconProvider.GetGreenConditionIcon();
                default:
                    return InventoryIconProvider.GetWhiteConditionIcon();
            }
        }

        private static Sprite GetJunkyardConditionMenuSprite(
            JunkyardConditionFilterMode mode)
        {
            switch (mode) {
                case JunkyardConditionFilterMode.Red:
                    return InventoryIconProvider.GetRedConditionIcon();
                case JunkyardConditionFilterMode.Orange:
                    return InventoryIconProvider.GetOrangeConditionIcon();
                case JunkyardConditionFilterMode.Yellow:
                    return InventoryIconProvider.GetYellowConditionIcon();
                case JunkyardConditionFilterMode.Green:
                    return InventoryIconProvider.GetGreenRingConditionIcon();
                case JunkyardConditionFilterMode.Perfect:
                    return InventoryIconProvider.GetGreenConditionIcon();
                default:
                    return InventoryIconProvider.GetWhiteConditionIcon();
            }
        }

        private static string GetGarageConditionMenuLabel(
            GarageConditionFilterMode mode)
        {
            switch (mode) {
                case GarageConditionFilterMode.RepairThresholdToPerfect:
                    return "15 ... 100%";
                case GarageConditionFilterMode.Red:
                    return "0 ... 14%";
                case GarageConditionFilterMode.Orange:
                    return "15 ... 49%";
                case GarageConditionFilterMode.Yellow:
                    return "50 ... 79%";
                case GarageConditionFilterMode.GreenRing:
                    return "80 ... 99%";
                case GarageConditionFilterMode.Perfect:
                    return "100%";
                default:
                    return null;
            }
        }

        private static string GetJunkyardConditionMenuLabel(
            JunkyardConditionFilterMode mode)
        {
            switch (mode) {
                case JunkyardConditionFilterMode.RepairThresholdToPerfect:
                    return "15 ... 100%";
                case JunkyardConditionFilterMode.Red:
                    return "0 ... 14%";
                case JunkyardConditionFilterMode.Orange:
                    return "15 ... 49%";
                case JunkyardConditionFilterMode.Yellow:
                    return "50 ... 79%";
                case JunkyardConditionFilterMode.Green:
                    return "80 ... 99%";
                case JunkyardConditionFilterMode.Perfect:
                    return "100%";
                default:
                    return null;
            }
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
                    FindDeepChild(buttonRoot, PackageButtonName),
                    IsBarnOrJunkyardScene(),
                    IsInventoryGroupingEnabled() &&
                        SupportsInventoryGrouping(inventory));

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
            Transform packageButton, bool junkyardContext, bool packageContext)
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
                            case JunkyardConditionFilterMode.Perfect:
                                conditionImage.sprite =
                                    InventoryIconProvider.GetGreenConditionIcon();
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

            if (packageContext && packageButton != null) {
                Image packageImage = packageButton.GetComponent<Image>();
                if (packageImage != null) {
                    switch (packageFilterMode) {
                        case PackageQuickFilterMode.Packages:
                            packageImage.sprite =
                                InventoryIconProvider.GetWhiteWarehouseIcon();
                            packageImage.color = ActiveButtonColor;
                            break;
                        case PackageQuickFilterMode.Singles:
                            packageImage.sprite =
                                InventoryIconProvider.GetRedWarehouseIcon();
                            packageImage.color = ActiveButtonColor;
                            break;
                        default:
                            packageImage.sprite =
                                InventoryIconProvider.GetWhiteWarehouseIcon();
                            packageImage.color = DisabledButtonColor;
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

                UnregisterQuickFilterMenu(
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
