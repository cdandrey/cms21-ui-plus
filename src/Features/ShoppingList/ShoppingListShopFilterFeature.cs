using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS;
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Helpers;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
using Il2CppInterop.Runtime;
#else
using CMS;
using CMS.Containers;
using CMS.UI.Helpers;
using CMS.UI.Logic;
using CMS.UI.Windows;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class ShoppingListShopFilterFeature
    {
        private const string FilterRootName = "QShoppingListShopFilters";
        private const string FooterWindowId = "ShoppingList";
        private const string ResetHintId = "Hint_ShoppingListShopFilters";
        private const string FilterIconDirectory =
            @"Mods\CMS21UIPlus\ShoppingListIndicators\";
        private const float ButtonWidth = 24f;
        private const float ButtonHeight = 18f;
        private const float ButtonSpacing = 4f;
        private const float HeaderHorizontalMargin = 10f;
        private const float FallbackX = 391f;
        private const float FallbackY = 28.6f;

        private static readonly Color32 EnabledColor =
            new Color32(255, 255, 255, 255);
        private static readonly Color32 DisabledColor =
            new Color32(155, 155, 155, 210);
        private static readonly ShopType[] FilterOrder = {
            ShopType.Main,
            ShopType.Body,
            ShopType.Tire,
            ShopType.Interior,
            ShopType.LicensePlate,
            ShopType.Gearbox,
            ShopType.Tuning,
            ShopType.BodyTuning,
            ShopType.Rims,
            ShopType.Electronics,
            ShopType.Community,
            ShopType.Addons,
        };
        private static readonly bool[] FilterEnabled =
            new bool[FilterOrder.Length];
        private static readonly Image[] FilterImages =
            new Image[FilterOrder.Length];
        private static readonly Sprite[] FilterSprites =
            new Sprite[FilterOrder.Length];
        private static readonly bool[] FilterSpriteLoadAttempted =
            new bool[FilterOrder.Length];
        private static readonly List<ShopListItemData> FullOrder =
            new List<ShopListItemData>();
        private static readonly List<ShopListItemData> OrderedItems =
            new List<ShopListItemData>();
        private static readonly List<ShopListItemData> ReconciledOrder =
            new List<ShopListItemData>();
        private static readonly Dictionary<string, ShopType> ShopNameToType =
            new Dictionary<string, ShopType>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> ItemShopMaskCache =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly MethodInfo FillItemsMethod =
            AccessTools.Method(typeof(ShopListWindow), "FillItems");
        private static readonly PropertyInfo PartPropertyShopNameProperty =
            AccessTools.Property(typeof(PartProperty), "ShopName");
        private static readonly FieldInfo PartPropertyShopNameField =
            AccessTools.Field(typeof(PartProperty), "ShopName");

        private static ShopListWindow activeWindow;
        private static GameObject filterRoot;
        private static NativeUiFactory.FooterHintHandle resetHint;
        private static int visibleItemCount;
        private static bool shopNamesInitialized;

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addShoppingListSorting;
            }
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void ShowPostfix(ShopListWindow __instance,
            bool __result)
        {
            if (!__result || !IsEnabled || __instance == null)
                return;

            try {
                activeWindow = __instance;
                ResetFilterState(__instance);
                CaptureFullOrder(__instance);
                ApplyFilters(__instance);
                CreateFilterUi(__instance);
                CreateResetHint(__instance);
                if (visibleItemCount < __instance.items.Count)
                    RefreshItems(__instance);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[ShoppingList] Failed to initialize shop filters." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Hide))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void HidePrefix(ShopListWindow __instance)
        {
            if (__instance == null || __instance != activeWindow)
                return;

            try {
                RestoreFullOrder(__instance);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[ShoppingList] Failed to restore shopping-list order." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }

            DestroyResetHint();
            DestroyFilterUi();
            FullOrder.Clear();
            OrderedItems.Clear();
            ReconciledOrder.Clear();
            visibleItemCount = 0;
            activeWindow = null;
        }

        [HarmonyPatch(typeof(ShopListWindow), "FillItems")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void FillItemsPostfix(ShopListWindow __instance)
        {
            if (!IsEnabled || __instance == null ||
                __instance != activeWindow)
                return;

            UpdateVisibleItemCount(__instance);
            ShoppingListTwoColumnNavigationFeature.RefreshRowsNow(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), "HandleInput")]
        [HarmonyPostfix]
        private static void HandleInputPostfix(ShopListWindow __instance)
        {
            if (!IsEnabled || __instance == null ||
                __instance != activeWindow || !IsAltPressed())
                return;

            ToggleAllFilters(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Save),
            new Type[] { })]
        [HarmonyPrefix]
        private static void SavePrefix(ShopListWindow __instance,
            out bool __state)
        {
            __state = PrepareFullOrderForSave(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Save),
            new Type[] { })]
        [HarmonyPostfix]
        private static void SavePostfix(ShopListWindow __instance,
            bool __state)
        {
            if (__state)
                PartitionFromFullOrder(__instance);
        }

        internal static int GetVisibleItemCount(ShopListWindow window)
        {
            if (!IsEnabled || window == null || window != activeWindow ||
                window.items == null)
                return window != null && window.items != null
                    ? window.items.Count : 0;

            return Math.Min(visibleItemCount, window.items.Count);
        }

        internal static bool IsFiltering(ShopListWindow window)
        {
            return IsEnabled && window != null &&
                window == activeWindow && HasDisabledFilters();
        }

        internal static void CaptureSortedOrderAndApplyFilters(
            ShopListWindow window)
        {
            if (!IsEnabled || window == null || window != activeWindow ||
                window.items == null)
                return;

            CaptureFullOrder(window);
            PartitionFromFullOrder(window);
        }

        private static void CreateResetHint(ShopListWindow window)
        {
            if (window == null || window.uiDescription == null)
                return;

            resetHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = FooterWindowId,
                    WindowRoot = window.transform,
                    HintRoot = window.uiDescription.transform,
                    HintId = ResetHintId,
                    Keys = new string[] { "LeftAlt" },
                    Text = ModLocalization.Get("LOC_ResetFiltersAction"),
                    Action = new Action(delegate {
                        if (activeWindow != null)
                            ToggleAllFilters(activeWindow);
                    }),
                    OnlyHandleMouseClickInput = true,
                    Row = 1,
                    AllowAutomaticRowWrap = false,
                    ExtendFooterBackground = true,
                    Order = 3,
                    Profile = WindowFooterHintController
                        .NativeFooterProfile.Automatic,
                });
        }

        private static void DestroyResetHint()
        {
            WindowFooterHintController.RemoveHint(
                FooterWindowId, ResetHintId);
            resetHint = null;
        }

        private static void ResetFilterState(ShopListWindow window)
        {
            bool filterCurrentShop = Main.SettingsEntry != null &&
                Main.SettingsEntry.Value.filterShoppingListByCurrentShopOnOpen &&
                window != null && window.isShopActive && window.canSearchInShop &&
                window.shopWindow != null &&
                window.shopWindow.gameObject != null &&
                window.shopWindow.gameObject.activeInHierarchy;
            SetAllFilters(!filterCurrentShop);
            if (!filterCurrentShop)
                return;

            int filterIndex = GetFilterIndex(
                window.shopWindow.currentShopType);
            if (filterIndex >= 0)
                FilterEnabled[filterIndex] = true;
            else
                SetAllFilters(true);
        }

        private static void CreateFilterUi(ShopListWindow window)
        {
            DestroyFilterUi();
            if (window == null)
                return;

#if NET6_0_OR_GREATER
            filterRoot = new GameObject(FilterRootName,
                typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> rootTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            rootTypes[0] = Il2CppType.Of<RectTransform>();
            filterRoot = new GameObject(FilterRootName, rootTypes);
#endif
            filterRoot.transform.SetParent(window.transform, false);
            filterRoot.layer = window.gameObject.layer;
            filterRoot.transform.SetAsLastSibling();

            RectTransform rootRect =
                filterRoot.GetComponent<RectTransform>();
            float totalWidth = FilterOrder.Length * ButtonWidth +
                (FilterOrder.Length - 1) * ButtonSpacing;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.anchoredPosition = new Vector2(
                GetHeaderRightX(window), GetHeaderY(window));
            rootRect.sizeDelta = new Vector2(totalWidth, ButtonHeight);
            rootRect.localScale = Vector3.one;

            Transform sourceGrid = FindNativeShopGrid(window);
            for (int i = 0; i < FilterOrder.Length; i++) {
                FilterImages[i] = CreateFilterButton(rootRect,
                    sourceGrid, FilterOrder[i], i);
            }

            UpdateButtonVisuals();
            filterRoot.SetActive(true);
        }

        private static Image CreateFilterButton(RectTransform parent,
            Transform sourceGrid, ShopType shopType, int buttonIndex)
        {
            Sprite sprite = GetFilterSprite(sourceGrid, shopType,
                buttonIndex);
            if (sprite == null)
                return null;

            string name = "ShopFilter_" + shopType;
#if NET6_0_OR_GREATER
            GameObject buttonObject = new GameObject(name,
                typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            GameObject buttonObject = new GameObject(name, componentTypes);
#endif
            buttonObject.transform.SetParent(parent, false);
            buttonObject.layer = parent.gameObject.layer;
            buttonObject.transform.localScale = Vector3.one;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(
                buttonIndex * (ButtonWidth + ButtonSpacing), 0f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            int capturedIndex = buttonIndex;
            Action clickAction = delegate () {
                ToggleFilter(capturedIndex);
            };
            UnityAction unityAction =
                DelegateSupport.ConvertDelegate<UnityAction>(clickAction);
            button.onClick.AddListener(unityAction);
            buttonObject.SetActive(true);
            return image;
        }

        private static float GetHeaderRightX(ShopListWindow window)
        {
            if (window != null) {
                Transform background = window.transform.Find("BG");
                RectTransform backgroundRect = background != null
                    ? background.GetComponent<RectTransform>() : null;
                if (backgroundRect != null)
                    return backgroundRect.anchoredPosition.x +
                        backgroundRect.rect.xMax - HeaderHorizontalMargin;
            }
            return FallbackX;
        }

        private static float GetHeaderY(ShopListWindow window)
        {
            if (window != null) {
                Transform label = window.transform.Find("MainLabel");
                RectTransform labelRect = label != null
                    ? label.GetComponent<RectTransform>() : null;
                if (labelRect != null)
                    return labelRect.anchoredPosition.y;
            }
            return FallbackY;
        }

        private static Transform FindNativeShopGrid(ShopListWindow window)
        {
            if (window == null || window.transform.parent == null)
                return null;

            Transform shopSelection =
                window.transform.parent.Find("Shop/ShopSelectionMenu");
            return shopSelection != null
                ? shopSelection.Find("ShopsGrid") : null;
        }

        private static Sprite GetFilterSprite(Transform sourceGrid,
            ShopType shopType, int buttonIndex)
        {
            if (buttonIndex >= 0 && buttonIndex < FilterSprites.Length) {
                if (FilterSprites[buttonIndex] != null)
                    return FilterSprites[buttonIndex];

                if (!FilterSpriteLoadAttempted[buttonIndex]) {
                    FilterSpriteLoadAttempted[buttonIndex] = true;
                    string fileName = GetFilterIconFileName(shopType);
                    if (!string.IsNullOrEmpty(fileName)) {
                        string path = FilterIconDirectory + fileName;
                        if (System.IO.File.Exists(path)) {
                            try {
                                FilterSprites[buttonIndex] =
                                    TextureLoader.LoadSpriteFromFile(path, false);
                            } catch (Exception exception) {
                                ModLogger.Log(
                                    "[ShoppingList] Failed to load filter icon " +
                                    fileName + "." + Environment.NewLine + exception,
                                    Types.LoggingLevels.Warning);
                            }
                        }
                    }
                }

                if (FilterSprites[buttonIndex] != null)
                    return FilterSprites[buttonIndex];
            }

            return GetShopSprite(sourceGrid, shopType);
        }

        private static string GetFilterIconFileName(ShopType shopType)
        {
            switch (shopType) {
                case ShopType.Main:
                    return "SL_Main.png";
                case ShopType.Body:
                    return "SL_Body.png";
                case ShopType.Tire:
                    return "SL_Tires.png";
                case ShopType.Interior:
                    return "SL_Interior.png";
                case ShopType.LicensePlate:
                    return "SL_LicensePlate.png";
                case ShopType.Gearbox:
                    return "SL_Gearbox.png";
                case ShopType.Tuning:
                    return "SL_Tuning.png";
                case ShopType.BodyTuning:
                    return "SL_BodyTuning.png";
                case ShopType.Rims:
                    return "SL_Rims.png";
                case ShopType.Electronics:
                    return "SL_Electronics.png";
                case ShopType.Community:
                    return "SL_Community.png";
                case ShopType.Addons:
                    return "SL_Addons.png";
                default:
                    return null;
            }
        }

        private static Sprite GetShopSprite(Transform sourceGrid,
            ShopType shopType)
        {
            if (sourceGrid == null)
                return null;

            int value = (int)shopType;
            string avatarName = value == 0
                ? "ShopAvatar" : "ShopAvatar (" + value + ")";
            Transform avatar = sourceGrid.Find(avatarName);
            Transform imageTransform = avatar != null
                ? avatar.Find("Img") : null;
            Image image = imageTransform != null
                ? imageTransform.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        private static bool IsAltPressed()
        {
            return Input.GetKeyDown(KeyCode.LeftAlt) ||
                Input.GetKeyDown(KeyCode.RightAlt);
        }

        private static void ToggleAllFilters(ShopListWindow window)
        {
            try {
                ReconcileFullOrder(window);
                SetAllFilters(!AreAllFiltersEnabled());
                ApplyFilters(window);
                UpdateButtonVisuals();
                RefreshItems(window);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[ShoppingList] Failed to toggle all shop filters." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        private static void SetAllFilters(bool enabled)
        {
            for (int i = 0; i < FilterEnabled.Length; i++)
                FilterEnabled[i] = enabled;
        }

        private static bool AreAllFiltersEnabled()
        {
            for (int i = 0; i < FilterEnabled.Length; i++) {
                if (!FilterEnabled[i])
                    return false;
            }
            return true;
        }

        private static void ToggleFilter(int filterIndex)
        {
            ShopListWindow window = activeWindow;
            if (!IsEnabled || window == null || window.items == null ||
                filterIndex < 0 || filterIndex >= FilterEnabled.Length)
                return;

            try {
                ReconcileFullOrder(window);
                FilterEnabled[filterIndex] = !FilterEnabled[filterIndex];
                ApplyFilters(window);
                UpdateButtonVisuals();
                RefreshItems(window);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[ShoppingList] Failed to apply shop filter." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        private static void UpdateButtonVisuals()
        {
            for (int i = 0; i < FilterImages.Length; i++) {
                Image image = FilterImages[i];
                if (image != null)
                    image.color = FilterEnabled[i]
                        ? EnabledColor : DisabledColor;
            }
        }

        private static void DestroyFilterUi()
        {
            if (filterRoot != null)
                UnityEngine.Object.Destroy(filterRoot);
            filterRoot = null;
            for (int i = 0; i < FilterImages.Length; i++)
                FilterImages[i] = null;
        }

        private static void ApplyFilters(ShopListWindow window)
        {
            if (window == null || window.items == null)
                return;

            RestoreFullOrder(window);
            PartitionFromFullOrder(window);
        }

        private static void PartitionFromFullOrder(ShopListWindow window)
        {
            if (window == null || window.items == null ||
                FullOrder.Count != window.items.Count) {
                UpdateVisibleItemCount(window);
                return;
            }

            OrderedItems.Clear();
            int count = FullOrder.Count;
            for (int i = 0; i < count; i++) {
                ShopListItemData data = FullOrder[i];
                if (IsEntryVisible(data))
                    OrderedItems.Add(data);
            }
            visibleItemCount = OrderedItems.Count;
            for (int i = 0; i < count; i++) {
                ShopListItemData data = FullOrder[i];
                if (!IsEntryVisible(data))
                    OrderedItems.Add(data);
            }

            for (int i = 0; i < count; i++)
                window.items[i] = OrderedItems[i];
        }

        private static void UpdateVisibleItemCount(ShopListWindow window)
        {
            visibleItemCount = 0;
            if (window == null || window.items == null)
                return;

            for (int i = 0; i < window.items.Count; i++) {
                if (IsEntryVisible(window.items[i]))
                    visibleItemCount++;
            }
        }

        private static bool PrepareFullOrderForSave(ShopListWindow window)
        {
            if (!IsEnabled || window == null || window != activeWindow ||
                window.items == null || !HasDisabledFilters())
                return false;

            RestoreFullOrder(window);
            return true;
        }

        private static void CaptureFullOrder(ShopListWindow window)
        {
            FullOrder.Clear();
            if (window == null || window.items == null)
                return;

            for (int i = 0; i < window.items.Count; i++)
                FullOrder.Add(window.items[i]);
            visibleItemCount = window.items.Count;
        }

        private static void RestoreFullOrder(ShopListWindow window)
        {
            if (window == null || window.items == null)
                return;

            ReconcileFullOrder(window);
            if (FullOrder.Count != window.items.Count)
                return;

            for (int i = 0; i < FullOrder.Count; i++)
                window.items[i] = FullOrder[i];
            visibleItemCount = window.items.Count;
        }

        private static void ReconcileFullOrder(ShopListWindow window)
        {
            if (window == null || window.items == null)
                return;
            if (FullOrder.Count == 0) {
                CaptureFullOrder(window);
                return;
            }

            int currentCount = window.items.Count;
            bool[] used = new bool[currentCount];
            ReconciledOrder.Clear();

            for (int i = 0; i < FullOrder.Count; i++) {
                ShopListItemData previous = FullOrder[i];
                int match = FindMatchingEntry(window, previous, used);
                if (match < 0)
                    continue;
                ReconciledOrder.Add(window.items[match]);
                used[match] = true;
            }

            for (int i = 0; i < currentCount; i++) {
                if (!used[i])
                    ReconciledOrder.Add(window.items[i]);
            }

            FullOrder.Clear();
            FullOrder.AddRange(ReconciledOrder);
        }

        private static int FindMatchingEntry(ShopListWindow window,
            ShopListItemData target, bool[] used)
        {
            if (window == null || window.items == null || used == null)
                return -1;

            for (int i = 0; i < window.items.Count && i < used.Length; i++) {
                if (!used[i] && EntriesMatch(target, window.items[i]))
                    return i;
            }
            return -1;
        }

        private static bool EntriesMatch(ShopListItemData left,
            ShopListItemData right)
        {
            if (object.ReferenceEquals(left, right))
                return true;
            if (left == null || right == null ||
                !string.Equals(left.ID, right.ID, StringComparison.Ordinal))
                return false;

            ShopListItemDataEx leftAdditional = left.AdditionalData;
            ShopListItemDataEx rightAdditional = right.AdditionalData;
            return GetTireFlag(leftAdditional) ==
                    GetTireFlag(rightAdditional) &&
                GetRimFlag(leftAdditional) == GetRimFlag(rightAdditional) &&
                GetLicensePlateFlag(leftAdditional) ==
                    GetLicensePlateFlag(rightAdditional) &&
                GetSize(leftAdditional) == GetSize(rightAdditional) &&
                GetWidth(leftAdditional) == GetWidth(rightAdditional) &&
                GetProfile(leftAdditional) == GetProfile(rightAdditional) &&
                GetEt(leftAdditional) == GetEt(rightAdditional);
        }

        private static bool GetTireFlag(ShopListItemDataEx data)
        {
            return data != null && data.Tire;
        }

        private static bool GetRimFlag(ShopListItemDataEx data)
        {
            return data != null && data.Rim;
        }

        private static bool GetLicensePlateFlag(ShopListItemDataEx data)
        {
            return data != null && data.LicensePlate;
        }

        private static int GetSize(ShopListItemDataEx data)
        {
            return data != null ? data.Size : 0;
        }

        private static int GetWidth(ShopListItemDataEx data)
        {
            return data != null ? data.Width : 0;
        }

        private static int GetProfile(ShopListItemDataEx data)
        {
            return data != null ? data.Profile : 0;
        }

        private static int GetEt(ShopListItemDataEx data)
        {
            return data != null ? data.ET : 0;
        }

        private static bool HasDisabledFilters()
        {
            for (int i = 0; i < FilterEnabled.Length; i++) {
                if (!FilterEnabled[i])
                    return true;
            }
            return false;
        }

        private static bool IsEntryVisible(ShopListItemData data)
        {
            int shopMask;
            if (!TryGetShopMask(data, out shopMask))
                return true;

            for (int i = 0; i < FilterOrder.Length; i++) {
                if ((shopMask & (1 << i)) != 0 && FilterEnabled[i])
                    return true;
            }
            return false;
        }

        private static int GetFilterIndex(ShopType shopType)
        {
            for (int i = 0; i < FilterOrder.Length; i++) {
                if (FilterOrder[i] == shopType)
                    return i;
            }
            return -1;
        }

        private static bool TryGetShopMask(ShopListItemData data,
            out int shopMask)
        {
            shopMask = 0;
            if (data == null || string.IsNullOrEmpty(data.ID))
                return false;

            ShopListItemDataEx additional = data.AdditionalData;
            if (additional != null) {
                if (additional.Rim)
                    return TryAddShopType(ShopType.Rims, ref shopMask);
                if (additional.Tire)
                    return TryAddShopType(ShopType.Tire, ref shopMask);
                if (additional.LicensePlate)
                    return TryAddShopType(ShopType.LicensePlate, ref shopMask);
            }

            if (ItemShopMaskCache.TryGetValue(data.ID, out shopMask))
                return shopMask != 0;

            GameInventory inventory = Singleton<GameInventory>.Instance;
            if (inventory == null)
                return false;

            AddItemShopType(inventory, data.ID, ref shopMask);
            if (!data.ID.StartsWith("t_", StringComparison.Ordinal))
                AddItemShopType(inventory, "t_" + data.ID, ref shopMask);

            ItemShopMaskCache[data.ID] = shopMask;
            return shopMask != 0;
        }

        private static void AddItemShopType(GameInventory inventory,
            string itemID, ref int shopMask)
        {
            if (inventory == null || string.IsNullOrEmpty(itemID) ||
                !inventory.ExistsInPartProperty(itemID))
                return;

            PartProperty property = inventory.GetItemProperty(itemID);
            string shopName = GetPartPropertyShopName(property);
            if (string.IsNullOrEmpty(shopName))
                return;

            EnsureShopNameMap();
            ShopType shopType;
            if (ShopNameToType.TryGetValue(shopName, out shopType))
                TryAddShopType(shopType, ref shopMask);
        }

        private static bool TryAddShopType(ShopType shopType,
            ref int shopMask)
        {
            int filterIndex = GetFilterIndex(shopType);
            if (filterIndex < 0)
                return false;

            shopMask |= 1 << filterIndex;
            return true;
        }

        private static void EnsureShopNameMap()
        {
            if (shopNamesInitialized)
                return;

            shopNamesInitialized = true;
            for (int i = 0; i < FilterOrder.Length; i++) {
                string shopName = ShopHelper.ShopToShopName(FilterOrder[i]);
                if (!string.IsNullOrEmpty(shopName))
                    ShopNameToType[shopName] = FilterOrder[i];
            }
        }

        private static string GetPartPropertyShopName(PartProperty property)
        {
            if (property == null)
                return null;

            try {
                object value = PartPropertyShopNameProperty != null
                    ? PartPropertyShopNameProperty.GetValue(property, null)
                    : PartPropertyShopNameField != null
                        ? PartPropertyShopNameField.GetValue(property)
                        : null;
                return value != null ? value.ToString() : null;
            } catch {
                return null;
            }
        }

        private static void RefreshItems(ShopListWindow window)
        {
            if (window != null && FillItemsMethod != null)
                FillItemsMethod.Invoke(window, null);
        }
    }
}
