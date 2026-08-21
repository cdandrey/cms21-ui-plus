using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppCMS.UI;
using Il2CppCMS.MainMenu;
using Il2CppCMS.MainMenu.Controls;
using Il2CppCMS.MainMenu.Sections;
using Il2CppCMS.MainMenu.Windows;
using LanguageSettingsTab = Il2CppCMS.MainMenu.Settings.LanguageSettingsTab;
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.Navigation;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS.UI;
using CMS.MainMenu;
using CMS.MainMenu.Controls;
using CMS.MainMenu.Sections;
using CMS.MainMenu.Windows;
using LanguageSettingsTab = CMS.MainMenu.Settings.LanguageSettingsTab;
using CMS.UI.Description;
using CMS.UI.Logic;
using CMS.UI.Logic.Navigation;
#endif

namespace Cms21UiPlus
{
    internal static class ModSettingsMenuFeature
    {
        static ModSettingsMenuFeature()
        {
            ModSettingDependencyRegistry.Changed +=
                RefreshDependencyWarnings;
        }

        private const string LaunchButtonName =
            "CMS21UIPlus.ModSettingsButton";
        private const string WindowName = "CMS21UIPlus.ModsWindow";
        private const string CardsPageName = "CMS21UIPlus.ModsCards";
        private const string SettingsPageName = "CMS21UIPlus.SettingsPage";
        private const float DiscoveryInterval = 0.5f;
        private const int CardColumns = 4;
        private const int CardRows = 4;
        private const int CardsPerPage = CardColumns * CardRows;

        private static readonly Color AccentColor =
            new Color(1f, 0.72f, 0.08f, 1f);
        private static readonly Color SecondaryTextColor =
            new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color ErrorColor =
            new Color(1f, 0.35f, 0.30f, 1f);

        private static MainMenuManager mainMenuManager;
        private static MainSection mainSection;
        private static TutorialsWindow tutorialsStyleSource;
        private static MainMenuButton launchButton;
        private static GameObject launchButtonObject;
        private static int launchButtonListIndex = -1;

        private static GameObject inputShieldObject;
        private static GameObject windowObject;
        private static GameObject cardsPageObject;
        private static RectTransform cardsRoot;
        private static Text pageIndicatorText;
        private static GameObject settingsPageObject;
        private static RectTransform settingsContent;
        private static ScrollRect settingsScroll;
        private static Text settingsHeaderText;
        private static Text settingsStatusText;
        private static RectTransform cardsFooterRoot;
        private static RectTransform footerRoot;
        private static NativeUiFactory.FooterHintHandle cardsSelectHint;
        private static NativeUiFactory.FooterHintHandle cardsCloseHint;
        private static NativeUiFactory.FooterHintHandle settingsEnterHint;
        private static float settingsFooterHintsWidth;
        private static bool discardConfirmationOpen;
        private static CanvasGroup discardOverlayCanvasGroup;
        private static bool discardOverlayInteractable;
        private static bool discardOverlayBlocksRaycasts;
        private static bool discardInputShieldWasActive;
        private static bool discardNativeManagerInputEnabled;
        private static bool discardHintLabelsCustomized;
        private static readonly List<Text> discardHintTexts =
            new List<Text>();
        private static readonly List<string> discardHintOriginalTexts =
            new List<string>();
        private static GameObject discardPreviousSelection;

        private static readonly List<GameObject> hiddenNativeHintObjects =
            new List<GameObject>();
        private static readonly List<bool> hiddenNativeHintStates =
            new List<bool>();
        private static CanvasGroup mainSectionCanvasGroup;
        private static bool mainSectionCanvasGroupAdded;
        private static bool mainSectionCanvasInteractable;
        private static bool mainSectionCanvasBlocksRaycasts;
        private static bool mainSectionCanvasIgnoreParentGroups;
        private static ListNavigationManager suspendedNavigation;
        private static bool suspendedNavigationWasEnabled;
        private static bool mainMenuInteractionSuppressed;
        private static int overlayInputFrame = -1;
        private static AdSection suspendedAdSection;
        private static bool nativeInputStateCaptured;
        private static bool mainSectionInputWasEnabled;
        private static bool adSectionInputWasEnabled;
        private static bool adsWereVisible;
        private static bool mainDescriptionWasVisible;

        private static readonly List<InstalledModEntry> installedMods =
            new List<InstalledModEntry>();
        private static readonly List<NativeUiFactory.ModCardHandle> cards =
            new List<NativeUiFactory.ModCardHandle>();
        private static readonly List<SettingBinding> settingBindings =
            new List<SettingBinding>();
        private static readonly Dictionary<string,
            Dictionary<string, ModSettingValue>> sessionEffectiveValues =
            new Dictionary<string, Dictionary<string, ModSettingValue>>(
                StringComparer.OrdinalIgnoreCase);

        private static IModSettingsProvider activeProvider;
        private static object draft;
        private static SettingBinding editingBinding;
        private static int suppressSettingsRowClickUntilFrame = -1;
        private static Action pendingDiscardAction;
        private static Selectable firstSelectable;
        private static int currentPage;
        private static int selectedVisibleCard = -1;
        private static readonly Dictionary<int, int> cardButtonIndexes =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, SettingBinding>
            settingsButtonBindings =
                new Dictionary<int, SettingBinding>();
        private static readonly Dictionary<int, SettingBinding>
            settingsArrowBindings =
                new Dictionary<int, SettingBinding>();
        private static readonly Dictionary<int, int> settingsArrowDirections =
            new Dictionary<int, int>();
        private static SettingBinding hoveredSettingsBinding;
        private static SettingBinding selectedSettingsBinding;
        private static int hoveredSettingsArrowId;
        private static float nextDiscoveryTime;
        private static bool disabled;

        private sealed class InstalledModEntry
        {
            public InstalledModEntry(string filePath, string assemblyName,
                string displayName, IModSettingsProvider provider)
            {
                FilePath = filePath;
                AssemblyName = assemblyName;
                DisplayName = displayName;
                Provider = provider;
            }

            public string FilePath { get; private set; }
            public string AssemblyName { get; private set; }
            public string DisplayName { get; private set; }
            public IModSettingsProvider Provider { get; private set; }
            public bool SupportsSettings
            {
                get { return Provider != null; }
            }
        }

        private sealed class SettingBinding
        {
            public SettingBinding(NativeUiFactory.SettingsRowHandle row,
                ModSettingOption option, ModSettingValue value,
                ModSettingValue savedValue, ModSettingValue effectiveValue)
            {
                Row = row;
                Option = option;
                Value = value;
                SavedValue = savedValue;
                EffectiveValue = effectiveValue;
            }

            public NativeUiFactory.SettingsRowHandle Row;
            public ModSettingOption Option;
            public ModSettingValue Value;
            public ModSettingValue SavedValue;
            public ModSettingValue EffectiveValue;
            public string Key
            {
                get { return Option != null ? Option.Key : string.Empty; }
            }
        }

        public static bool IsOverlayOpen
        {
            get {
                return windowObject != null && windowObject.activeSelf;
            }
        }

        public static bool RequiresUpdate
        {
            get {
                return !disabled && (mainSection == null ||
                    launchButton == null || IsOverlayOpen);
            }
        }

        public static bool IsDiscardConfirmationOpen
        {
            get { return discardConfirmationOpen; }
        }

        private static bool IsSettingsPageOpen
        {
            get {
                return settingsPageObject != null &&
                    settingsPageObject.activeSelf;
            }
        }

        public static void Update()
        {
            if (disabled)
                return;

            try {
                UpdateUnsafe();
            } catch (Exception exception) {
                DisableAfterError("update", exception);
            }
        }

        private static void UpdateUnsafe()
        {
            if ((mainMenuManager == null || mainSection == null) &&
                Time.unscaledTime >= nextDiscoveryTime) {
                nextDiscoveryTime = Time.unscaledTime + DiscoveryInterval;
                DiscoverMainMenu();
            }

            if (mainSection != null && launchButton == null)
                EnsureLaunchButton();

            if (!IsOverlayOpen)
                return;

            if (discardConfirmationOpen) {
                CustomizeDiscardConfirmationHints();
                return;
            }
            HandleOverlayInput();
        }

        internal static void OnLanguageChanged()
        {
            if (launchButton != null)
                RefreshLaunchButtonPresentation();
        }

        public static bool TryOpenFromMainMenuButton(
            MainMenuButton button)
        {
            if (IsOverlayOpen)
                return true;

            if (IsLaunchButton(button)) {
                OpenModsWindow();
                return true;
            }

            if (launchButton != null) {
                if (EventSystem.current != null &&
                    EventSystem.current.currentSelectedGameObject ==
                        launchButton.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                NativeUiFactory.ResetMainMenuButtonVisual(launchButton);
            }
            return false;
        }

        public static bool HandleOverlayInput()
        {
            if (!IsOverlayOpen)
                return false;
            if (overlayInputFrame == Time.frameCount)
                return true;
            overlayInputFrame = Time.frameCount;

            try {
                if (discardConfirmationOpen)
                    return true;
                if (IsSettingsPageOpen)
                    return HandleSettingsInput();
                return HandleCardsInput();
            } catch (Exception exception) {
                DisableAfterError("input", exception);
                return true;
            }
        }

        private static bool HandleSettingsInput()
        {
            bool editingString = editingBinding != null &&
                editingBinding.Option != null &&
                editingBinding.Option.Type == ModSettingType.String;

            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(KeyCode.JoystickButton1)) {
                RequestBackFromProvider();
                return true;
            }

            if (!editingString && Input.GetKeyDown(KeyCode.Backspace)) {
                RequestBackFromProvider();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter)) {
                HandleSettingsEnter();
                return true;
            }

            if (editingString) {
                HandleStringEditingInput();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.R)) {
                EndSettingsEditing();
                ResetAllCategories();
                return true;
            }

            if (editingBinding != null) {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) {
                    StepBindingValue(editingBinding, -1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow)) {
                    StepBindingValue(editingBinding, 1);
                    return true;
                }
            } else {
                if (Input.GetKeyDown(KeyCode.UpArrow)) {
                    MoveSettingsSelection(-1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.DownArrow)) {
                    MoveSettingsSelection(1);
                    return true;
                }
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && settingsScroll != null) {
                settingsScroll.verticalNormalizedPosition = Mathf.Clamp01(
                    settingsScroll.verticalNormalizedPosition + wheel * 0.08f);
            }
            return true;
        }

        private static bool HandleCardsInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(KeyCode.Backspace) ||
                Input.GetKeyDown(KeyCode.JoystickButton1)) {
                CloseWindow(true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.PageUp)) {
                ChangePage(-1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.PageDown)) {
                ChangePage(1);
                return true;
            }

            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.1f) {
                ChangePage(-1);
                return true;
            }
            if (wheel < -0.1f) {
                ChangePage(1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) {
                MoveCardSelection(-1, 0);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow)) {
                MoveCardSelection(1, 0);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow)) {
                MoveCardSelection(0, -1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow)) {
                MoveCardSelection(0, 1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space)) {
                InvokeSelectedButton();
                return true;
            }
            return true;
        }

        public static void ResetAll()
        {
            CloseWindow(false);
            mainMenuManager = null;
            mainSection = null;
            tutorialsStyleSource = null;
            launchButton = null;
            launchButtonObject = null;
            launchButtonListIndex = -1;
            inputShieldObject = null;
            windowObject = null;
            cardsPageObject = null;
            cardsRoot = null;
            pageIndicatorText = null;
            settingsPageObject = null;
            settingsContent = null;
            settingsScroll = null;
            settingsHeaderText = null;
            settingsStatusText = null;
            cardsFooterRoot = null;
            footerRoot = null;
            WindowFooterHintController.ClearStyledWindow("ModsCards");
            WindowFooterHintController.ClearStyledWindow("ModSettings");
            settingsFooterHintsWidth = 0f;
            cardsSelectHint = null;
            cardsCloseHint = null;
            settingsEnterHint = null;
            discardConfirmationOpen = false;
            discardOverlayCanvasGroup = null;
            discardOverlayInteractable = false;
            discardOverlayBlocksRaycasts = false;
            discardInputShieldWasActive = false;
            discardNativeManagerInputEnabled = false;
            RestoreDiscardConfirmationHints();
            discardPreviousSelection = null;
            hiddenNativeHintObjects.Clear();
            hiddenNativeHintStates.Clear();
            mainSectionCanvasGroup = null;
            mainSectionCanvasGroupAdded = false;
            suspendedNavigation = null;
            mainMenuInteractionSuppressed = false;
            overlayInputFrame = -1;
            suspendedAdSection = null;
            nativeInputStateCaptured = false;
            mainSectionInputWasEnabled = false;
            adSectionInputWasEnabled = false;
            adsWereVisible = false;
            mainDescriptionWasVisible = false;
            activeProvider = null;
            draft = null;
            editingBinding = null;
            suppressSettingsRowClickUntilFrame = -1;
            pendingDiscardAction = null;
            firstSelectable = null;
            installedMods.Clear();
            cards.Clear();
            cardButtonIndexes.Clear();
            settingBindings.Clear();
            ResetSettingsInteractionState();
            currentPage = 0;
            selectedVisibleCard = -1;
            nextDiscoveryTime = 0f;
            disabled = false;
            NativeUiFactory.Reset();
        }

        private static void DiscoverMainMenu()
        {
            Il2CppReferenceArray<UnityEngine.Object> loadedManagers =
                Resources.FindObjectsOfTypeAll(
                    Il2CppType.Of<MainMenuManager>());
            foreach (UnityEngine.Object loaded in loadedManagers) {
                MainMenuManager manager = loaded.TryCast<MainMenuManager>();
                if (manager == null || manager.gameObject == null ||
                    !manager.gameObject.scene.IsValid())
                    continue;

                MainSection section = manager.GetMainSection();
                if (section == null || section.gameObject == null)
                    continue;

                mainMenuManager = manager;
                mainSection = section;
                tutorialsStyleSource = FindTutorialsWindow(manager);
                EnsureLaunchButton();
                return;
            }
        }

        private static TutorialsWindow FindTutorialsWindow(
            MainMenuManager manager)
        {
            Il2CppReferenceArray<UnityEngine.Object> loadedWindows =
                Resources.FindObjectsOfTypeAll(
                    Il2CppType.Of<TutorialsWindow>());
            foreach (UnityEngine.Object loaded in loadedWindows) {
                TutorialsWindow window = loaded.TryCast<TutorialsWindow>();
                if (window == null || window.gameObject == null ||
                    !window.gameObject.scene.IsValid())
                    continue;
                if (window.menuManager == manager ||
                    window.menuManager == null)
                    return window;
            }
            return null;
        }

        private static bool EnsureLaunchButton()
        {
            if (mainSection == null || mainSection.transform == null)
                return false;

            MainMenuButton[] existingButtons =
                mainSection.GetComponentsInChildren<MainMenuButton>(true);
            for (int i = 0; i < existingButtons.Length; i++) {
                MainMenuButton existing = existingButtons[i];
                if (existing == null || existing.gameObject == null ||
                    !string.Equals(existing.gameObject.name,
                        LaunchButtonName, StringComparison.Ordinal))
                    continue;

                launchButton = existing;
                launchButtonObject = existing.gameObject;
                ConfigureLaunchButton();
                return true;
            }

            Il2CppReferenceArray<MainMenuButton> buttons =
                mainSection.buttons;
            if (buttons == null || buttons.Length == 0)
                return false;

            int templateIndex = FindSettingsButtonIndex(buttons);
            if (templateIndex < 0)
                templateIndex = buttons.Length - 1;
            MainMenuButton template = buttons[templateIndex];
            if (template == null || template.gameObject == null)
                return false;

            launchButton = NativeUiFactory.CreateMainMenuButtonFromStyle(
                template, template.transform.parent, LaunchButtonName,
                ModLocalization.Get("LOC_ModsAction"));
            if (launchButton == null)
                return false;
            launchButtonObject = launchButton.gameObject;
            launchButton.transform.SetSiblingIndex(
                template.transform.GetSiblingIndex() + 1);
            ConfigureLaunchButton();
            PositionLaunchButton(buttons, templateIndex, template);
            InsertLaunchButtonIntoNativeLists(buttons, templateIndex,
                template);
            mainSection.UpdateMouseEvents();
            return true;
        }

        private static bool IsLaunchButton(MainMenuButton button)
        {
            if (button == null || button.gameObject == null)
                return false;
            return button.gameObject == launchButtonObject ||
                string.Equals(button.gameObject.name, LaunchButtonName,
                    StringComparison.Ordinal);
        }

        private static void ConfigureLaunchButton()
        {
            if (launchButton == null || launchButton.gameObject == null)
                return;
            UnityAction action = DelegateSupport.ConvertDelegate<UnityAction>(
                new Action(OpenModsWindow));
            launchButton.AssignAction(action);
            launchButton.Type = (MainMenuButtonType)(-1000);
            launchButton.UsedInList = true;
            launchButton.UseAdditionalMouseEvents = false;
            launchButton.useAdditionalMouseEvents = false;
            NativeUiFactory.ResetMainMenuButtonVisual(launchButton);
            RefreshLaunchButtonPresentation();
        }

        internal static void RefreshLaunchButtonPresentation()
        {
            if (launchButton == null || launchButton.gameObject == null)
                return;
            string label = ModLocalization.Get("LOC_ModsAction");
            if (launchButton.text == null ||
                !string.Equals(launchButton.text.text, label,
                    StringComparison.Ordinal)) {
                launchButton.SetText(label);
                if (launchButton.text != null)
                    launchButton.text.text = label;
            }
            if ((int)launchButton.Type != -1000)
                launchButton.Type = (MainMenuButtonType)(-1000);
            NativeUiFactory.UpdateMainMenuButtonVisual(launchButton,
                IsOverlayOpen);
        }

        private static int FindSettingsButtonIndex(
            Il2CppReferenceArray<MainMenuButton> buttons)
        {
            for (int i = 0; i < buttons.Length; i++) {
                MainMenuButton button = buttons[i];
                if (button != null &&
                    button.Type == MainMenuButtonType.Settings)
                    return i;
            }
            return -1;
        }

        private static void PositionLaunchButton(
            Il2CppReferenceArray<MainMenuButton> buttons,
            int templateIndex, MainMenuButton template)
        {
            if (launchButtonObject == null || template == null ||
                buttons == null || buttons.Length == 0)
                return;
            Transform parent = template.transform.parent;
            if (parent == null || parent.GetComponent<LayoutGroup>() != null)
                return;

            List<MainMenuButton> ordered = new List<MainMenuButton>();
            for (int i = 0; i < buttons.Length; i++) {
                MainMenuButton button = buttons[i];
                if (button != null && button.transform.parent == parent)
                    ordered.Add(button);
                if (i == templateIndex)
                    ordered.Add(launchButton);
            }
            if (ordered.Count < 2)
                return;

            RectTransform firstRect =
                ordered[0].GetComponent<RectTransform>();
            RectTransform lastRect = buttons[buttons.Length - 1] != null
                ? buttons[buttons.Length - 1].GetComponent<RectTransform>()
                : null;
            if (firstRect == null || lastRect == null)
                return;

            float firstY = firstRect.anchoredPosition.y;
            float lastY = lastRect.anchoredPosition.y;
            for (int i = 0; i < ordered.Count; i++) {
                RectTransform rect = ordered[i] != null
                    ? ordered[i].GetComponent<RectTransform>() : null;
                if (rect == null)
                    continue;
                float ratio = ordered.Count > 1
                    ? (float)i / (ordered.Count - 1) : 0f;
                Vector2 position = rect.anchoredPosition;
                position.y = Mathf.Lerp(firstY, lastY, ratio);
                rect.anchoredPosition = position;
            }
        }

        private static void InsertLaunchButtonIntoNativeLists(
            Il2CppReferenceArray<MainMenuButton> buttons,
            int templateIndex, MainMenuButton template)
        {
            int buttonInsertIndex = templateIndex + 1;
            Il2CppReferenceArray<MainMenuButton> updatedButtons =
                new Il2CppReferenceArray<MainMenuButton>(
                    buttons.Length + 1);
            for (int source = 0, target = 0;
                target < updatedButtons.Length; target++) {
                if (target == buttonInsertIndex)
                    updatedButtons[target] = launchButton;
                else
                    updatedButtons[target] = buttons[source++];
            }
            mainSection.buttons = updatedButtons;

            ListNavigationManager navigation =
                mainSection.ListNavigationManager;
            if (navigation == null)
                return;
            Il2CppReferenceArray<ListItem> items =
                navigation.GetListItems();
            if (items == null)
                return;

            int templateListIndex = -1;
            for (int i = 0; i < items.Length; i++) {
                if (items[i] != null &&
                    items[i].gameObject == template.gameObject) {
                    templateListIndex = i;
                    break;
                }
            }
            if (templateListIndex < 0)
                templateListIndex = items.Length - 1;

            int listInsertIndex = templateListIndex + 1;
            int launchY = template.Y + 1;
            for (int i = 0; i < items.Length; i++) {
                if (items[i] != null && items[i].Y >= launchY)
                    items[i].Y = items[i].Y + 1;
            }
            launchButton.Y = launchY;

            Il2CppReferenceArray<ListItem> updatedItems =
                new Il2CppReferenceArray<ListItem>(items.Length + 1);
            for (int source = 0, target = 0;
                target < updatedItems.Length; target++) {
                if (target == listInsertIndex)
                    updatedItems[target] = launchButton;
                else
                    updatedItems[target] = items[source++];
            }
            navigation.SetListItems(updatedItems);
            launchButtonListIndex = listInsertIndex;
        }

        private static void OpenModsWindow()
        {
            try {
                if (tutorialsStyleSource == null)
                    tutorialsStyleSource =
                        FindTutorialsWindow(mainMenuManager);
                if (tutorialsStyleSource == null) {
                    ModLogger.Log("[ModSettings] Native UI style sources " +
                        "were not found.", Types.LoggingLevels.Warning);
                    return;
                }

                // Close a currently visible native Tutorials screen, but never
                // modify its items, hierarchy, navigation or state.
                if (tutorialsStyleSource.IsActive)
                    tutorialsStyleSource.Hide(false);

                NativeUiFactory.Initialize(mainMenuManager, launchButton,
                    tutorialsStyleSource);
                ModLocalization.Reset();
                EnsureWindow();
                BuildFooterHints();
                installedMods.Clear();
                installedMods.AddRange(DiscoverInstalledMods());
                currentPage = 0;
                BuildCardsPage();
                settingsPageObject.SetActive(false);
                cardsPageObject.SetActive(true);
                SetCardsFooterVisible(true);
                if (inputShieldObject != null) {
                    inputShieldObject.SetActive(true);
                    inputShieldObject.transform.SetAsLastSibling();
                }
                windowObject.SetActive(true);
                windowObject.transform.SetAsLastSibling();
                RelayoutFooterHints();
                NativeUiFactory.UpdateMainMenuButtonVisual(
                    launchButton, true);
                SuppressMainMenuInteraction();
                ClearCardSelection();
                UpdateCardPointerSelection();
            } catch (Exception exception) {
                DisableAfterError("mods window open", exception);
            }
        }

        private static void EnsureWindow()
        {
            if (windowObject != null)
                return;

            Transform parent = tutorialsStyleSource.transform.parent;
            NativeUiFactory.ModsWindowHandle window =
                NativeUiFactory.CreateModsWindow(parent, WindowName,
                    CardsPageName, SettingsPageName, AccentColor,
                    SecondaryTextColor);
            inputShieldObject = window.InputShield;
            windowObject = window.Root;
            cardsPageObject = window.CardsPage;
            cardsRoot = window.CardsRoot;
            pageIndicatorText = window.PageIndicator;
            settingsPageObject = window.SettingsPage;
            settingsContent = window.SettingsContent;
            settingsScroll = window.SettingsScroll;
            settingsHeaderText = window.SettingsHeader;
            settingsStatusText = window.SettingsStatus;
            cardsFooterRoot = window.CardsFooterRoot;
            footerRoot = window.FooterRoot;
        }

        private static void BuildCardsPage()
        {
            DestroyNamedChildren(cardsRoot, "ModCard");
            cards.Clear();
            cardButtonIndexes.Clear();
            selectedVisibleCard = -1;

            int pageCount = GetPageCount();
            if (currentPage >= pageCount)
                currentPage = Mathf.Max(0, pageCount - 1);
            int start = currentPage * CardsPerPage;
            int end = Mathf.Min(start + CardsPerPage,
                installedMods.Count);

            Vector2 cardSize = NativeUiFactory.NativeCardSize;
            RectTransform rootRect = cardsRoot;
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Abs(rootRect.rect.width);
            float height = Mathf.Abs(rootRect.rect.height);
            Vector2 fallbackArea = NativeUiFactory.NativeCardsAreaSize;
            if (width < cardSize.x)
                width = fallbackArea.x;
            if (height < cardSize.y)
                height = fallbackArea.y;
            float gapX = Mathf.Max(8f,
                (width - CardColumns * cardSize.x) /
                Mathf.Max(1, CardColumns - 1));
            float gapY = Mathf.Max(8f,
                (height - CardRows * cardSize.y) /
                Mathf.Max(1, CardRows - 1));

            for (int global = start; global < end; global++) {
                InstalledModEntry entry = installedMods[global];
                int local = global - start;
                int column = local % CardColumns;
                int row = local / CardColumns;
                IModSettingsProvider provider = entry.Provider;
                NativeUiFactory.ModCardHandle card =
                    NativeUiFactory.CreateModCard(cardsRoot,
                        entry.DisplayName, provider != null,
                        provider != null
                            ? new Action(delegate {
                                OpenProvider(provider);
                            })
                            : null);
                RectTransform rect =
                    card.Root.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(
                    column * (cardSize.x + gapX),
                    -row * (cardSize.y + gapY));
                cards.Add(card);
                if (card.Button != null)
                    cardButtonIndexes[card.Button.GetInstanceID()] = local;
            }

            if (pageIndicatorText != null) {
                pageIndicatorText.text = pageCount > 1
                    ? ModLocalization.Get("LOC_Page") +
                        (currentPage + 1) + "/" + pageCount
                    : string.Empty;
            }
        }

        private static int GetPageCount()
        {
            return Mathf.Max(1,
                (installedMods.Count + CardsPerPage - 1) /
                CardsPerPage);
        }

        private static void ChangePage(int direction)
        {
            int pageCount = GetPageCount();
            int next = Mathf.Clamp(currentPage + direction,
                0, pageCount - 1);
            if (next == currentPage)
                return;
            currentPage = next;
            BuildCardsPage();
            ClearCardSelection();
            UpdateCardPointerSelection();
        }

        private static void SelectCard(int index)
        {
            selectedVisibleCard = index;
            for (int i = 0; i < cards.Count; i++)
                NativeUiFactory.SetModCardSelected(cards[i], i == index);
            if (index >= 0 && index < cards.Count &&
                cards[index] != null) {
                SetSelected(cards[index].Button);
            }
            UpdateCardsSelectHintVisibility();
        }


        private static void UpdateCardPointerSelection()
        {
            Vector3 pointerPosition = Input.mousePosition;
            for (int i = 0; i < cards.Count; i++) {
                NativeUiFactory.ModCardHandle card = cards[i];
                if (card == null || card.Button == null ||
                    !card.Button.interactable || card.Root == null)
                    continue;
                RectTransform rect =
                    card.Root.GetComponent<RectTransform>();
                if (rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        rect, pointerPosition)) {
                    SelectCard(i);
                    return;
                }
            }
            ClearCardSelection();
        }

        internal static void OnCardPointerEnter(Selectable selectable)
        {
            if (!IsOverlayOpen || selectable == null)
                return;
            if (IsSettingsPageOpen) {
                SettingBinding binding;
                if (settingsButtonBindings.TryGetValue(
                        selectable.GetInstanceID(), out binding))
                    SetHoveredSettingsBinding(binding);
                return;
            }
            int index;
            if (cardButtonIndexes.TryGetValue(selectable.GetInstanceID(),
                    out index) && selectedVisibleCard != index)
                SelectCard(index);
        }

        internal static void OnCardPointerExit(Selectable selectable)
        {
            if (!IsOverlayOpen || selectable == null)
                return;
            if (IsSettingsPageOpen) {
                SettingBinding binding;
                if (settingsButtonBindings.TryGetValue(
                        selectable.GetInstanceID(), out binding) &&
                    hoveredSettingsBinding == binding)
                    SetHoveredSettingsBinding(null);
                return;
            }
            int index;
            if (cardButtonIndexes.TryGetValue(selectable.GetInstanceID(),
                    out index) && selectedVisibleCard == index)
                ClearCardSelection();
        }

        internal static void OnSettingsSelectableSelected(
            Selectable selectable)
        {
            if (!IsOverlayOpen || !IsSettingsPageOpen || selectable == null)
                return;
            SettingBinding binding;
            if (!settingsButtonBindings.TryGetValue(
                    selectable.GetInstanceID(), out binding))
                return;
            SettingBinding previous = selectedSettingsBinding;
            selectedSettingsBinding = binding;
            RefreshSettingsRowFocus(previous);
            RefreshSettingsRowFocus(binding);
        }

        internal static void OnSettingsSelectableDeselected(
            Selectable selectable)
        {
            if (selectable == null)
                return;
            SettingBinding binding;
            if (!settingsButtonBindings.TryGetValue(
                    selectable.GetInstanceID(), out binding) ||
                selectedSettingsBinding != binding)
                return;
            selectedSettingsBinding = null;
            RefreshSettingsRowFocus(binding);
        }

        private static void ClearCardSelection()
        {
            selectedVisibleCard = -1;
            for (int i = 0; i < cards.Count; i++)
                NativeUiFactory.SetModCardSelected(cards[i], false);

            if (EventSystem.current != null) {
                GameObject selected =
                    EventSystem.current.currentSelectedGameObject;
                if (selected == launchButtonObject || IsCardObject(selected))
                    EventSystem.current.SetSelectedGameObject(null);
            }
            UpdateCardsSelectHintVisibility();
        }

        private static bool IsCardObject(GameObject candidate)
        {
            if (candidate == null)
                return false;
            for (int i = 0; i < cards.Count; i++) {
                if (cards[i] != null && cards[i].Root == candidate)
                    return true;
            }
            return false;
        }

        private static void ActivateSelectedCard()
        {
            if (selectedVisibleCard < 0 ||
                selectedVisibleCard >= cards.Count)
                return;
            NativeUiFactory.ModCardHandle card =
                cards[selectedVisibleCard];
            if (card == null || card.Button == null ||
                !card.Button.interactable)
                return;
            card.Button.onClick.Invoke();
        }


        private static void SelectFirstSupportedCard()
        {
            for (int i = 0; i < cards.Count; i++) {
                if (cards[i] != null && cards[i].Button != null &&
                    cards[i].Button.interactable) {
                    SelectCard(i);
                    return;
                }
            }
            selectedVisibleCard = -1;
            for (int i = 0; i < cards.Count; i++)
                NativeUiFactory.SetModCardSelected(cards[i], false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            UpdateCardsSelectHintVisibility();
        }

        private static void MoveCardSelection(int dx, int dy)
        {
            if (cards.Count == 0)
                return;
            if (selectedVisibleCard < 0 ||
                selectedVisibleCard >= cards.Count) {
                SelectFirstSupportedCard();
                return;
            }

            int column = selectedVisibleCard % CardColumns;
            int row = selectedVisibleCard / CardColumns;
            for (int step = 0; step < cards.Count; step++) {
                column += dx;
                row += dy;
                if (column < 0 || column >= CardColumns ||
                    row < 0 || row >= CardRows)
                    break;
                int candidate = row * CardColumns + column;
                if (candidate < 0 || candidate >= cards.Count)
                    continue;
                NativeUiFactory.ModCardHandle card = cards[candidate];
                if (card != null && card.Button != null &&
                    card.Button.interactable) {
                    SelectCard(candidate);
                    return;
                }
            }
        }

        private static void SetCardsFooterVisible(bool visible)
        {
            if (cardsFooterRoot == null ||
                cardsFooterRoot.gameObject == null)
                return;
            cardsFooterRoot.gameObject.SetActive(visible);
            if (visible)
                cardsFooterRoot.transform.SetAsLastSibling();
            UpdateCardsSelectHintVisibility();
            if (visible)
                RelayoutFooterHints();
        }

        private static void UpdateCardsSelectHintVisibility()
        {
            if (cardsSelectHint == null ||
                cardsSelectHint.Root == null)
                return;
            bool visible = cardsFooterRoot != null &&
                cardsFooterRoot.gameObject.activeSelf &&
                selectedVisibleCard >= 0 &&
                selectedVisibleCard < cards.Count &&
                cards[selectedVisibleCard] != null &&
                cards[selectedVisibleCard].Button != null &&
                cards[selectedVisibleCard].Button.interactable;
            NativeUiFactory.SetFooterHintActive(cardsSelectHint, visible);
            RelayoutFooterHints();
        }


        private static void BuildFooterHints()
        {
            WindowFooterHintController.ClearStyledWindow("ModsCards");
            WindowFooterHintController.ClearStyledWindow("ModSettings");
            DestroyNamedChildren(cardsFooterRoot, "Hint_");
            DestroyNamedChildren(footerRoot, "Hint_");

            cardsSelectHint = WindowFooterHintController.RequestStyledHint(
                "ModsCards", cardsFooterRoot, "Select",
                new string[] { "Enter" },
                ModLocalization.Get("LOC_SelectAction"),
                new Action(ActivateSelectedCard), 0);

            cardsCloseHint = WindowFooterHintController.RequestStyledHint(
                "ModsCards", cardsFooterRoot, "Close",
                new string[] { "Esc" },
                ModLocalization.Get("LOC_CloseAction"),
                new Action(delegate { CloseWindow(true); }), 10);

            settingsEnterHint = WindowFooterHintController.RequestStyledHint(
                "ModSettings", footerRoot, "Enter",
                new string[] { "Enter" },
                ModLocalization.Get("LOC_EditAction"),
                new Action(HandleSettingsEnter), 0);

            WindowFooterHintController.RequestStyledHint(
                "ModSettings", footerRoot, "Default",
                new string[] { "R" }, ModLocalization.Get("LOC_DefaultAction"),
                new Action(ResetAllCategories), 10);

            WindowFooterHintController.RequestStyledHint(
                "ModSettings", footerRoot, "Back",
                new string[] { "Esc" },
                ModLocalization.Get("LOC_BackAction"),
                new Action(RequestBackFromProvider), 20);

            UpdateCardsSelectHintVisibility();
            RelayoutFooterHints();
        }

        private static void RelayoutFooterHints()
        {
            WindowFooterHintController.UpdateLayouts();
            settingsFooterHintsWidth = WindowFooterHintController
                .GetStyledUsedWidth("ModSettings");
            UpdateSettingsStatusLayout();
        }

        private static void UpdateSettingsStatusLayout()
        {
            if (settingsStatusText == null)
                return;
            RectTransform statusRect = settingsStatusText.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            float left = settingsFooterHintsWidth > 0f
                ? settingsFooterHintsWidth + 14f : 0f;
            statusRect.offsetMin = new Vector2(left, 0f);
            statusRect.offsetMax = new Vector2(-8f, 0f);
            settingsStatusText.alignment = TextAnchor.MiddleLeft;
        }

        private static void OpenProvider(IModSettingsProvider provider)
        {
            if (provider == null)
                return;
            activeProvider = provider;
            draft = provider.CreateDraft();
            cardsPageObject.SetActive(false);
            settingsPageObject.SetActive(true);
            SetCardsFooterVisible(false);
            settingsHeaderText.text = provider.DisplayName;
            BuildProviderSettings();
            SelectFirstSettingsRow();
            UpdateSettingsPointerSelection();
            RelayoutFooterHints();
        }

        private static void BuildProviderSettings()
        {
            ResetSettingsInteractionState();
            DestroyChildren(settingsContent);
            settingBindings.Clear();
            editingBinding = null;
            firstSelectable = null;
            RefreshSettingsEnterHint();
            if (activeProvider == null || draft == null)
                return;

            object currentValues = activeProvider.CreateDraft();
            Dictionary<string, ModSettingValue> effectiveValues =
                GetOrCreateEffectiveValues(activeProvider, currentValues);
            for (int categoryIndex = 0;
                categoryIndex < activeProvider.Categories.Count;
                categoryIndex++) {
                ModSettingsCategory category =
                    activeProvider.Categories[categoryIndex];
                NativeUiFactory.CreateSectionHeader(settingsContent,
                    category.Name);

                for (int optionIndex = 0;
                    optionIndex < category.Options.Count;
                    optionIndex++) {
                    ModSettingOption option =
                        category.Options[optionIndex];
                    string key = option.Key;
                    ModSettingValue value = activeProvider.GetValue(
                        draft, key);
                    ModSettingValue savedValue = currentValues != null
                        ? activeProvider.GetValue(currentValues, key) : value;
                    NativeUiFactory.SettingsRowHandle row =
                        NativeUiFactory.CreateSettingsRow(
                            settingsContent, option.Name,
                            option.GetDisplayValue(value),
                            option.Type == ModSettingType.Boolean,
                            value != null && value.Type ==
                                ModSettingValueType.Boolean &&
                                value.BooleanValue,
                            new Action(delegate {
                                OnSettingsRowClicked(key);
                            }));
                    ModSettingValue effectiveValue;
                    if (effectiveValues == null ||
                        !effectiveValues.TryGetValue(key,
                            out effectiveValue))
                        effectiveValue = savedValue;
                    SettingBinding created = new SettingBinding(row, option,
                        value, savedValue, effectiveValue);
                    settingBindings.Add(created);
                    RegisterSettingsRowInteraction(created);
                    UpdateBindingWarning(created);
                    if (firstSelectable == null && row != null)
                        firstSelectable = row.Button;
                }
            }

            settingsScroll.verticalNormalizedPosition = 1f;
            UpdateDirtyStatus();
        }

        private static Dictionary<string, ModSettingValue>
            GetOrCreateEffectiveValues(IModSettingsProvider provider,
            object currentValues)
        {
            if (provider == null)
                return null;

            string providerId = provider.Id ?? string.Empty;
            Dictionary<string, ModSettingValue> values;
            if (sessionEffectiveValues.TryGetValue(providerId, out values))
                return values;

            values = new Dictionary<string, ModSettingValue>(
                StringComparer.OrdinalIgnoreCase);
            for (int categoryIndex = 0;
                categoryIndex < provider.Categories.Count;
                categoryIndex++) {
                ModSettingsCategory category =
                    provider.Categories[categoryIndex];
                for (int optionIndex = 0;
                    optionIndex < category.Options.Count;
                    optionIndex++) {
                    ModSettingOption option = category.Options[optionIndex];
                    values[option.Key] = currentValues != null
                        ? provider.GetValue(currentValues, option.Key)
                        : option.DefaultValue;
                }
            }
            sessionEffectiveValues[providerId] = values;
            return values;
        }

        private static bool RequiresRestart(SettingBinding binding)
        {
            return binding != null && binding.Option != null &&
                binding.Option.ApplyMode != ModSettingApplyMode.Immediate &&
                !SettingValuesEqual(binding.Value,
                    binding.EffectiveValue);
        }

        private static void UpdateBindingWarning(SettingBinding binding)
        {
            if (binding == null || binding.Option == null)
                return;

            if (binding.Value != null &&
                binding.Value.Type == ModSettingValueType.Boolean &&
                binding.Value.BooleanValue &&
                !string.IsNullOrEmpty(binding.Option.DependencyId) &&
                activeProvider != null) {
                string dependencyId = binding.Option.DependencyId;
                if (!string.IsNullOrEmpty(
                    binding.Option.DependencySwitchKey) &&
                    !string.IsNullOrEmpty(
                        binding.Option.DependencyWhenFalseId)) {
                    SettingBinding switchBinding = FindBinding(
                        binding.Option.DependencySwitchKey);
                    if (switchBinding != null &&
                        switchBinding.Value != null &&
                        switchBinding.Value.Type ==
                            ModSettingValueType.Boolean &&
                        !switchBinding.Value.BooleanValue) {
                        dependencyId =
                            binding.Option.DependencyWhenFalseId;
                    }
                }
                string dependencyStatus =
                    ModSettingDependencyRegistry.GetStatus(
                        activeProvider.Id, dependencyId);
                string dependencyWarning = string.Empty;
                if (string.Equals(dependencyStatus,
                    ModSettingDependencyRegistry.Partial,
                    StringComparison.OrdinalIgnoreCase))
                    dependencyWarning =
                        binding.Option.DependencyPartialWarning;
                else if (string.Equals(dependencyStatus,
                    ModSettingDependencyRegistry.UnavailableByDefault,
                    StringComparison.OrdinalIgnoreCase))
                    dependencyWarning =
                        !string.IsNullOrEmpty(
                            binding.Option.DependencyDefaultWarning)
                            ? binding.Option.DependencyDefaultWarning
                            : binding.Option.DependencyWarning;
                else if (string.Equals(dependencyStatus,
                    ModSettingDependencyRegistry.Unavailable,
                    StringComparison.OrdinalIgnoreCase))
                    dependencyWarning = binding.Option.DependencyWarning;

                if (!string.IsNullOrEmpty(dependencyWarning)) {
                    NativeUiFactory.SetSettingsRowWarning(binding.Row,
                        dependencyWarning, string.Equals(dependencyStatus,
                            ModSettingDependencyRegistry.Partial,
                            StringComparison.OrdinalIgnoreCase));
                    return;
                }
            }

            NativeUiFactory.SetSettingsRowWarning(binding.Row,
                RequiresRestart(binding)
                    ? ModLocalization.Get("LOC_RestartRequired")
                    : string.Empty);
        }

        private static void RefreshDependencyWarnings()
        {
            for (int i = 0; i < settingBindings.Count; i++)
                UpdateBindingWarning(settingBindings[i]);
        }

        private static void RegisterSettingsRowInteraction(
            SettingBinding binding)
        {
            if (binding == null || binding.Row == null)
                return;
            if (binding.Row.Button != null) {
                settingsButtonBindings[
                    binding.Row.Button.GetInstanceID()] = binding;
            }
            RegisterSettingsArrowInteraction(binding,
                binding.Row.LeftArrow, -1);
            RegisterSettingsArrowInteraction(binding,
                binding.Row.RightArrow, 1);
        }

        private static void RegisterSettingsArrowInteraction(
            SettingBinding binding, GameObject arrow, int direction)
        {
            if (binding == null || arrow == null)
                return;
            Image hitArea = arrow.GetComponent<Image>();
            if (hitArea == null)
                hitArea = arrow.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            EventTrigger trigger = arrow.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = arrow.AddComponent<EventTrigger>();
            if (trigger == null)
                return;
            int id = trigger.GetInstanceID();
            settingsArrowBindings[id] = binding;
            settingsArrowDirections[id] = direction;
        }

        private static void ResetSettingsInteractionState()
        {
            settingsButtonBindings.Clear();
            settingsArrowBindings.Clear();
            settingsArrowDirections.Clear();
            hoveredSettingsBinding = null;
            selectedSettingsBinding = null;
            hoveredSettingsArrowId = 0;
        }

        private static void SetHoveredSettingsBinding(
            SettingBinding binding)
        {
            if (hoveredSettingsBinding == binding)
                return;
            SettingBinding previous = hoveredSettingsBinding;
            hoveredSettingsBinding = binding;
            RefreshSettingsRowFocus(previous);
            RefreshSettingsRowFocus(binding);
        }

        private static void RefreshSettingsRowFocus(SettingBinding binding)
        {
            if (binding == null || binding.Row == null)
                return;
            NativeUiFactory.SetSettingsRowVisualState(binding.Row,
                hoveredSettingsBinding == binding,
                selectedSettingsBinding == binding);
        }

        private static void RefreshEditingBindingVisual()
        {
            SettingBinding binding = editingBinding;
            if (binding == null || binding.Row == null)
                return;
            bool showLeft = binding.Option != null &&
                binding.Option.CanMoveValue(binding.Value, -1);
            bool showRight = binding.Option != null &&
                binding.Option.CanMoveValue(binding.Value, 1);
            EventTrigger leftTrigger = binding.Row.LeftArrow != null
                ? binding.Row.LeftArrow.GetComponent<EventTrigger>() : null;
            EventTrigger rightTrigger = binding.Row.RightArrow != null
                ? binding.Row.RightArrow.GetComponent<EventTrigger>() : null;
            if ((!showLeft && leftTrigger != null &&
                    leftTrigger.GetInstanceID() == hoveredSettingsArrowId) ||
                (!showRight && rightTrigger != null &&
                    rightTrigger.GetInstanceID() == hoveredSettingsArrowId))
                hoveredSettingsArrowId = 0;
            NativeUiFactory.SetSettingsRowEditing(binding.Row, true,
                showLeft, showRight,
                showLeft && leftTrigger != null &&
                    leftTrigger.GetInstanceID() == hoveredSettingsArrowId,
                showRight && rightTrigger != null &&
                    rightTrigger.GetInstanceID() == hoveredSettingsArrowId);
        }

        private static void UpdateSettingsPointerSelection()
        {
            Vector3 pointerPosition = Input.mousePosition;
            for (int i = 0; i < settingBindings.Count; i++) {
                SettingBinding binding = settingBindings[i];
                if (binding == null || binding.Row == null ||
                    binding.Row.Root == null)
                    continue;
                RectTransform rect =
                    binding.Row.Root.GetComponent<RectTransform>();
                if (rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        rect, pointerPosition)) {
                    SetHoveredSettingsBinding(binding);
                    return;
                }
            }
            SetHoveredSettingsBinding(null);
        }

        internal static void OnSettingsArrowPointerEnter(EventTrigger trigger)
        {
            if (!IsOverlayOpen || !IsSettingsPageOpen || trigger == null)
                return;
            SettingBinding binding;
            if (!settingsArrowBindings.TryGetValue(trigger.GetInstanceID(),
                    out binding) || binding != editingBinding)
                return;
            hoveredSettingsArrowId = trigger.GetInstanceID();
            RefreshEditingBindingVisual();
        }

        internal static void OnSettingsArrowPointerExit(EventTrigger trigger)
        {
            if (trigger == null ||
                hoveredSettingsArrowId != trigger.GetInstanceID())
                return;
            hoveredSettingsArrowId = 0;
            RefreshEditingBindingVisual();
        }

        internal static void OnSettingsArrowPointerClick(EventTrigger trigger)
        {
            if (!IsOverlayOpen || !IsSettingsPageOpen || trigger == null)
                return;
            int id = trigger.GetInstanceID();
            SettingBinding binding;
            int direction;
            if (!settingsArrowBindings.TryGetValue(id, out binding) ||
                !settingsArrowDirections.TryGetValue(id, out direction) ||
                binding != editingBinding)
                return;
            StepBindingValue(binding, direction);
        }

        private static SettingBinding FindBinding(string key)
        {
            for (int i = 0; i < settingBindings.Count; i++) {
                if (string.Equals(settingBindings[i].Key, key,
                    StringComparison.Ordinal))
                    return settingBindings[i];
            }
            return null;
        }

        private static void SetBindingValue(SettingBinding binding,
            ModSettingValue value)
        {
            if (binding == null || binding.Option == null ||
                activeProvider == null || draft == null ||
                !binding.Option.IsValueAllowed(value))
                return;
            binding.Value = value;
            activeProvider.SetValue(draft, binding.Key, value);
            UpdateBindingRow(binding);
            RefreshDependencyWarnings();
            UpdateDirtyStatus();
        }

        private static void UpdateBindingRow(SettingBinding binding)
        {
            if (binding == null || binding.Option == null)
                return;
            bool isBoolean = binding.Option.Type == ModSettingType.Boolean;
            bool booleanValue = isBoolean && binding.Value != null &&
                binding.Value.Type == ModSettingValueType.Boolean &&
                binding.Value.BooleanValue;
            NativeUiFactory.UpdateSettingsRow(binding.Row,
                binding.Option.GetDisplayValue(binding.Value),
                isBoolean, booleanValue);
        }

        private static bool StepBindingValue(SettingBinding binding,
            int direction)
        {
            if (binding == null || binding.Option == null)
                return false;
            ModSettingValue next;
            if (!binding.Option.TryMoveValue(binding.Value, direction,
                out next))
                return false;
            SetBindingValue(binding, next);
            RefreshEditingBindingVisual();
            return true;
        }

        private static void HandleStringEditingInput()
        {
            SettingBinding binding = editingBinding;
            if (binding == null || binding.Option == null ||
                binding.Option.Type != ModSettingType.String)
                return;

            string value = binding.Value != null
                ? binding.Value.StringValue : string.Empty;
            bool changed = false;
            if (Input.GetKeyDown(KeyCode.Backspace) && value.Length > 0) {
                value = value.Substring(0, value.Length - 1);
                changed = true;
            }

            string input = Input.inputString;
            for (int i = 0; i < input.Length; i++) {
                char character = input[i];
                if (character == '\b' || character == '\n' ||
                    character == '\r' || char.IsControl(character))
                    continue;
                value += character;
                changed = true;
            }
            if (changed)
                SetBindingValue(binding,
                    ModSettingValue.FromString(value));
        }

        private static void OnSettingsRowClicked(string key)
        {
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Time.frameCount <= suppressSettingsRowClickUntilFrame)
                return;
            BeginSettingsEditing(FindBinding(key));
        }

        private static void HandleSettingsEnter()
        {
            suppressSettingsRowClickUntilFrame = Time.frameCount + 1;
            if (editingBinding == null) {
                BeginSettingsEditing(FindSelectedBinding());
                return;
            }
            ApplyEditingBinding();
        }

        private static void BeginSettingsEditing(SettingBinding binding)
        {
            if (binding == null || binding.Row == null)
                return;
            editingBinding = binding;
            hoveredSettingsArrowId = 0;
            SetSelected(binding.Row.Button);
            RefreshSettingsEnterHint();
            RefreshEditingBindingVisual();
        }

        private static void EndSettingsEditing()
        {
            SettingBinding previous = editingBinding;
            editingBinding = null;
            hoveredSettingsArrowId = 0;
            if (previous != null && previous.Row != null) {
                NativeUiFactory.SetSettingsRowEditing(previous.Row, false,
                    false, false, false, false);
            }
            RefreshSettingsEnterHint();
        }

        private static SettingBinding FindSelectedBinding()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < settingBindings.Count; i++) {
                SettingBinding binding = settingBindings[i];
                if (binding.Row != null && binding.Row.Root == selected)
                    return binding;
            }
            return null;
        }

        private static void RefreshSettingsEnterHint()
        {
            NativeUiFactory.UpdateFooterHint(settingsEnterHint,
                editingBinding != null
                    ? ModLocalization.Get("LOC_SaveAction")
                    : ModLocalization.Get("LOC_EditAction"),
                true);
            UpdateSettingsStatusLayout();
        }

        private static void ApplyEditingBinding()
        {
            SettingBinding binding = editingBinding;
            if (binding == null || activeProvider == null || draft == null)
                return;
            if (SettingValuesEqual(binding.Value, binding.SavedValue)) {
                EndSettingsEditing();
                UpdateDirtyStatus();
                return;
            }
            ApplyDraft();
        }

        private static bool SettingValuesEqual(ModSettingValue left,
            ModSettingValue right)
        {
            if (ReferenceEquals(left, right))
                return true;
            return left != null && left.Equals(right);
        }

        private static void MoveSettingsSelection(int direction)
        {
            if (settingBindings.Count == 0)
                return;
            int current = -1;
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < settingBindings.Count; i++) {
                if (settingBindings[i].Row != null &&
                    settingBindings[i].Row.Root == selected) {
                    current = i;
                    break;
                }
            }
            if (current < 0)
                current = 0;
            else
                current = Mathf.Clamp(current + direction, 0,
                    settingBindings.Count - 1);
            SetSelected(settingBindings[current].Row.Button);
            if (settingsScroll != null && settingBindings.Count > 1) {
                float ratio = (float)current /
                    (settingBindings.Count - 1);
                settingsScroll.verticalNormalizedPosition =
                    Mathf.Clamp01(1f - ratio);
            }
        }

        private static bool ApplyDraft()
        {
            if (activeProvider == null || draft == null)
                return false;
            string status;
            ModSettingApplyMode mode;
            bool success = activeProvider.Apply(draft, out status,
                out mode);
            settingsStatusText.text = status;
            settingsStatusText.color = success
                ? SecondaryTextColor : ErrorColor;
            if (success) {
                object refreshedDraft = activeProvider.CreateDraft();
                if (refreshedDraft != null)
                    draft = refreshedDraft;
                for (int i = 0; i < settingBindings.Count; i++) {
                    SettingBinding binding = settingBindings[i];
                    if (binding == null)
                        continue;
                    ModSettingValue value = activeProvider.GetValue(draft,
                        binding.Key);
                    binding.Value = value;
                    binding.SavedValue = value;
                    UpdateBindingRow(binding);
                }
                RefreshDependencyWarnings();
                EndSettingsEditing();
                ShowNativeSavePopup();
            }
            return success;
        }

        private static void ShowNativeSavePopup()
        {
            Il2CppReferenceArray<UnityEngine.Object> loadedWindows =
                Resources.FindObjectsOfTypeAll(
                    Il2CppType.Of<SettingsWindow>());
            SettingsWindow fallback = null;
            foreach (UnityEngine.Object loaded in loadedWindows) {
                SettingsWindow window = loaded.TryCast<SettingsWindow>();
                if (window == null || window.gameObject == null ||
                    !window.gameObject.scene.IsValid())
                    continue;
                if (window.gameObject.activeInHierarchy) {
                    window.ShowSavePopup();
                    return;
                }
                if (fallback == null)
                    fallback = window;
            }
            if (fallback != null)
                fallback.ShowSavePopup();
        }

        private static void ResetAllCategories()
        {
            if (activeProvider == null || draft == null)
                return;
            for (int i = 0; i < activeProvider.Categories.Count; i++) {
                activeProvider.ResetCategory(draft,
                    activeProvider.Categories[i].Id);
            }
            BuildProviderSettings();
            settingsStatusText.text = ModLocalization.Get("LOC_DefaultValuesLoadedPressEnterToSave");
            settingsStatusText.color = SecondaryTextColor;
            SelectFirstSettingsRow();
            UpdateSettingsPointerSelection();
        }

        private static void UpdateDirtyStatus()
        {
            if (settingsStatusText == null || activeProvider == null ||
                draft == null)
                return;
            if (!string.IsNullOrEmpty(settingsStatusText.text) &&
                settingsStatusText.color == ErrorColor)
                return;
            bool dirty = activeProvider.HasChanges(draft);
            settingsStatusText.text = dirty
                ? ModLocalization.Get("LOC_UnsavedChanges")
                : string.Empty;
            settingsStatusText.color = dirty
                ? AccentColor : SecondaryTextColor;
        }

        private static void RequestBackFromProvider()
        {
            if (activeProvider != null && draft != null &&
                activeProvider.HasChanges(draft)) {
                ShowDiscardModal(ReturnToCardsPage);
                return;
            }
            ReturnToCardsPage();
        }

        private static void ReturnToCardsPage()
        {
            HideModal();
            activeProvider = null;
            draft = null;
            editingBinding = null;
            settingBindings.Clear();
            ResetSettingsInteractionState();
            RefreshSettingsEnterHint();
            settingsPageObject.SetActive(false);
            cardsPageObject.SetActive(true);
            SetCardsFooterVisible(true);
            ClearCardSelection();
            UpdateCardPointerSelection();
        }

        private static void ShowDiscardModal(Action action)
        {
            HideModal();
            pendingDiscardAction = action;
            UIManager uiManager = UIManager.Get();
            if (uiManager == null) {
                pendingDiscardAction = null;
                if (settingsStatusText != null) {
                    settingsStatusText.text =
                        ModLocalization.Get("LOC_ConfirmationWindowIsUnavailable");
                    settingsStatusText.color = ErrorColor;
                }
                SelectFirstSettingsRow();
                return;
            }

            try {
                SuspendOverlayForDiscardConfirmation();
                discardConfirmationOpen = true;
                discardHintLabelsCustomized = false;
                uiManager.ShowAskWindow(
                    ModLocalization.Get("LOC_UnsavedChangesTitle"),
                    ModLocalization.Get("LOC_ApplyChanges"),
                    new Action<bool>(OnDiscardConfirmationResult), true);
                CustomizeDiscardConfirmationHints();
                EnableNativeDiscardConfirmationInput();
                Input.ResetInputAxes();
            } catch (Exception exception) {
                discardConfirmationOpen = false;
                RestoreDiscardConfirmationHints();
                pendingDiscardAction = null;
                RestoreOverlayAfterDiscardConfirmation();
                ModLogger.Log("[ModSettings] Failed to open the native " +
                    "discard confirmation." + Environment.NewLine +
                    exception, Types.LoggingLevels.Warning);
                if (settingsStatusText != null) {
                    settingsStatusText.text =
                        ModLocalization.Get("LOC_FailedToOpenConfirmation");
                    settingsStatusText.color = ErrorColor;
                }
                RestoreDiscardSelection();
            }
        }

        private static void OnDiscardConfirmationResult(bool accepted)
        {
            Action action = pendingDiscardAction;
            discardConfirmationOpen = false;
            RestoreDiscardConfirmationHints();
            pendingDiscardAction = null;
            RestoreOverlayAfterDiscardConfirmation();
            Input.ResetInputAxes();
            bool canLeave = !accepted || ApplyDraft();
            if (canLeave) {
                discardPreviousSelection = null;
                if (action != null)
                    action();
                return;
            }
            RestoreDiscardSelection();
        }

        private static void CustomizeDiscardConfirmationHints()
        {
            if (discardHintLabelsCustomized)
                return;

            string acceptText = ModLocalization.Get("LOC_ApplyAction");
            string cancelText = ModLocalization.Get("LOC_ExitAction");
            bool acceptUpdated = false;
            bool cancelUpdated = false;
            Il2CppReferenceArray<UnityEngine.Object> loaded =
                Resources.FindObjectsOfTypeAll(
                    Il2CppType.Of<ControlDescription>());
            foreach (UnityEngine.Object item in loaded) {
                ControlDescription description =
                    item.TryCast<ControlDescription>();
                if (description == null || description.gameObject == null ||
                    !description.gameObject.activeInHierarchy ||
                    description.texts == null ||
                    description.texts.Length == 0)
                    continue;

                Text label = description.texts[0];
                if (label == null)
                    continue;
                string current = (label.text ?? string.Empty).Trim();
                string spriteName = description.buttonImage != null &&
                    description.buttonImage.sprite != null
                        ? description.buttonImage.sprite.name
                        : string.Empty;

                if (IsDiscardAcceptHint(current, spriteName)) {
                    CaptureDiscardHintOriginal(label);
                    label.text = acceptText;
                    acceptUpdated = true;
                } else if (IsDiscardCancelHint(current, spriteName)) {
                    CaptureDiscardHintOriginal(label);
                    label.text = cancelText;
                    cancelUpdated = true;
                }
            }

            discardHintLabelsCustomized = acceptUpdated && cancelUpdated;
        }

        private static void CaptureDiscardHintOriginal(Text text)
        {
            for (int i = 0; i < discardHintTexts.Count; i++) {
                if (discardHintTexts[i] == text)
                    return;
            }
            discardHintTexts.Add(text);
            discardHintOriginalTexts.Add(text.text);
        }

        private static void RestoreDiscardConfirmationHints()
        {
            for (int i = 0; i < discardHintTexts.Count; i++) {
                Text text = discardHintTexts[i];
                if (text != null)
                    text.text = discardHintOriginalTexts[i];
            }
            discardHintTexts.Clear();
            discardHintOriginalTexts.Clear();
            discardHintLabelsCustomized = false;
        }

        private static bool IsDiscardAcceptHint(string text,
            string spriteName)
        {
            return string.Equals(text, "ACCEPT",
                       StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "ПРИНЯТЬ",
                    StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(spriteName) &&
                    (spriteName.IndexOf("return",
                         StringComparison.OrdinalIgnoreCase) >= 0 ||
                     spriteName.IndexOf("enter",
                         StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static bool IsDiscardCancelHint(string text,
            string spriteName)
        {
            return string.Equals(text, "CANCEL",
                       StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "ОТМЕНА",
                    StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(spriteName) &&
                    spriteName.IndexOf("esc",
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void SuspendOverlayForDiscardConfirmation()
        {
            discardPreviousSelection = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject : null;
            discardOverlayCanvasGroup = windowObject != null
                ? windowObject.GetComponent<CanvasGroup>() : null;
            if (discardOverlayCanvasGroup != null) {
                discardOverlayInteractable =
                    discardOverlayCanvasGroup.interactable;
                discardOverlayBlocksRaycasts =
                    discardOverlayCanvasGroup.blocksRaycasts;
                discardOverlayCanvasGroup.interactable = false;
                discardOverlayCanvasGroup.blocksRaycasts = false;
            }

            discardInputShieldWasActive = inputShieldObject != null &&
                inputShieldObject.activeSelf;
            if (inputShieldObject != null)
                inputShieldObject.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private static void EnableNativeDiscardConfirmationInput()
        {
            discardNativeManagerInputEnabled =
                mainMenuManager != null &&
                mainMenuInteractionSuppressed;
            if (discardNativeManagerInputEnabled)
                mainMenuManager.EnableInput(true);
        }

        private static void RestoreOverlayAfterDiscardConfirmation()
        {
            if (discardNativeManagerInputEnabled &&
                mainMenuManager != null &&
                mainMenuInteractionSuppressed)
                mainMenuManager.EnableInput(false);
            discardNativeManagerInputEnabled = false;
            KeepNativeMainHintsHidden();

            if (discardOverlayCanvasGroup != null) {
                discardOverlayCanvasGroup.interactable =
                    discardOverlayInteractable;
                discardOverlayCanvasGroup.blocksRaycasts =
                    discardOverlayBlocksRaycasts;
            }
            discardOverlayCanvasGroup = null;
            discardOverlayInteractable = false;
            discardOverlayBlocksRaycasts = false;

            if (inputShieldObject != null && IsOverlayOpen &&
                discardInputShieldWasActive)
                inputShieldObject.SetActive(true);
            if (windowObject != null && IsOverlayOpen)
                windowObject.transform.SetAsLastSibling();
            discardInputShieldWasActive = false;
        }

        private static void RestoreDiscardSelection()
        {
            GameObject previous = discardPreviousSelection;
            discardPreviousSelection = null;
            if (EventSystem.current != null && previous != null &&
                previous.activeInHierarchy) {
                EventSystem.current.SetSelectedGameObject(previous);
                return;
            }
            SelectFirstSettingsRow();
        }

        private static void HideModal()
        {
            discardConfirmationOpen = false;
            RestoreDiscardConfirmationHints();
            pendingDiscardAction = null;
            RestoreOverlayAfterDiscardConfirmation();
            discardPreviousSelection = null;
        }

        private static void CloseWindow(bool restoreSelection)
        {
            HideModal();
            ModSettingsConfigStore.DeleteSessionBackups();
            activeProvider = null;
            draft = null;
            editingBinding = null;
            settingBindings.Clear();
            ResetSettingsInteractionState();
            RefreshSettingsEnterHint();
            SetCardsFooterVisible(false);
            if (windowObject != null)
                windowObject.SetActive(false);
            if (inputShieldObject != null)
                inputShieldObject.SetActive(false);
            RestoreMainMenuInteraction();
            if (launchButton != null)
                NativeUiFactory.ResetMainMenuButtonVisual(launchButton);

            if (restoreSelection && mainSection != null &&
                launchButton != null) {
                ListNavigationManager navigation =
                    mainSection.ListNavigationManager;
                if (navigation != null && launchButtonListIndex >= 0)
                    navigation.SetSelected(launchButtonListIndex);
                else
                    launchButton.Select();
            }
        }

        private static List<InstalledModEntry> DiscoverInstalledMods()
        {
            List<InstalledModEntry> result =
                new List<InstalledModEntry>();
            string modsDirectory = null;
            try {
                modsDirectory = Path.GetDirectoryName(
                    typeof(Main).Assembly.Location);
            } catch (Exception exception) {
                ModLogger.Log("[ModSettings] Failed to resolve the Mods " +
                    "directory." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }

            if (string.IsNullOrEmpty(modsDirectory) ||
                !Directory.Exists(modsDirectory)) {
                ModLogger.Log("[ModSettings] Mods directory is unavailable: " +
                    (modsDirectory ?? "<null>"),
                    Types.LoggingLevels.Warning);
                return result;
            }

            Dictionary<string, Assembly> loadedAssemblies =
                new Dictionary<string, Assembly>(
                    StringComparer.OrdinalIgnoreCase);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++) {
                Assembly assembly = assemblies[i];
                try {
                    string name = assembly.GetName().Name;
                    if (!string.IsNullOrEmpty(name) &&
                        !loadedAssemblies.ContainsKey(name))
                        loadedAssemblies.Add(name, assembly);
                } catch (Exception exception) {
                    ModLogger.Log("[ModSettings] Failed to inspect a " +
                        "loaded assembly." + Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                }
            }

            string[] dllFiles;
            try {
                dllFiles = Directory.GetFiles(modsDirectory, "*.dll",
                    SearchOption.TopDirectoryOnly);
            } catch (Exception exception) {
                ModLogger.Log("[ModSettings] Failed to enumerate Mods " +
                    "directory." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
                return result;
            }

            Array.Sort(dllFiles, StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenAssemblies = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < dllFiles.Length; i++) {
                string filePath = dllFiles[i];
                string fileName = Path.GetFileName(filePath);
                string assemblyName;
                try {
                    AssemblyName metadata =
                        AssemblyName.GetAssemblyName(filePath);
                    assemblyName = metadata != null
                        ? metadata.Name : null;
                    if (string.IsNullOrEmpty(assemblyName)) {
                        ModLogger.Log("[ModSettings] DLL metadata did not " +
                            "contain an assembly name: " + fileName + ".",
                            Types.LoggingLevels.Warning);
                        continue;
                    }
                } catch (Exception exception) {
                    ModLogger.Log("[ModSettings] Failed to read DLL " +
                        "metadata: " + fileName + "." +
                        Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                    continue;
                }

                if (!seenAssemblies.Add(assemblyName))
                    continue;

                Assembly loadedAssembly;
                loadedAssemblies.TryGetValue(assemblyName,
                    out loadedAssembly);

                IModSettingsProvider provider;
                bool manifestFound;
                string manifestStatus;
                bool manifestLoaded =
                    UiSettingsManifestLoader.TryCreateProvider(filePath,
                        assemblyName, out provider, out manifestFound,
                        out manifestStatus);
                if (manifestFound && !manifestLoaded) {
                    ModLogger.Log("[ModSettings] UI settings manifest is " +
                        "invalid: assembly=" + assemblyName + "; " +
                        manifestStatus + ".",
                        Types.LoggingLevels.Warning);
                }

                string displayName = provider != null
                    ? provider.DisplayName
                    : TryGetMelonDisplayName(loadedAssembly);
                if (loadedAssembly == typeof(Main).Assembly &&
                    provider == null)
                    displayName = BuildInfo.Name;

                if (string.IsNullOrEmpty(displayName) && provider == null) {
                    continue;
                }

                if (string.IsNullOrEmpty(displayName))
                    displayName = Path.GetFileNameWithoutExtension(filePath);

                result.Add(new InstalledModEntry(filePath, assemblyName,
                    displayName, provider));
            }

            result.Sort(delegate (InstalledModEntry left,
                InstalledModEntry right) {
                return string.Compare(left.DisplayName,
                    right.DisplayName,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        private static string TryGetMelonDisplayName(Assembly assembly)
        {
            if (assembly == null)
                return null;

            try {
                IList<CustomAttributeData> attributes =
                    assembly.GetCustomAttributesData();
                for (int i = 0; i < attributes.Count; i++) {
                    CustomAttributeData attribute = attributes[i];
                    Type attributeType = attribute.Constructor != null
                        ? attribute.Constructor.DeclaringType : null;
                    if (attributeType == null ||
                        !string.Equals(attributeType.FullName,
                            "MelonLoader.MelonInfoAttribute",
                            StringComparison.Ordinal))
                        continue;

                    IList<CustomAttributeTypedArgument> arguments =
                        attribute.ConstructorArguments;
                    if (arguments.Count < 2)
                        return null;
                    string displayName = arguments[1].Value as string;
                    return string.IsNullOrEmpty(displayName)
                        ? null : displayName;
                }
            } catch (Exception exception) {
                ModLogger.Log("[ModSettings] MelonInfo metadata read failed." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
            return null;
        }

        private static void SelectFirstSettingsRow()
        {
            if (firstSelectable != null)
                SetSelected(firstSelectable);
        }

        private static void SetSelected(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null)
                return;
            EventSystem.current.SetSelectedGameObject(
                selectable.gameObject);
        }

        private static void InvokeSelectedButton()
        {
            if (EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject == null)
                return;
            Button button = EventSystem.current.currentSelectedGameObject
                .GetComponent<Button>();
            if (button != null && button.interactable)
                button.onClick.Invoke();
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null)
                return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(
                    parent.GetChild(i).gameObject);
        }

        private static void DestroyNamedChildren(Transform parent,
            string prefix)
        {
            if (parent == null)
                return;
            for (int i = parent.childCount - 1; i >= 0; i--) {
                Transform child = parent.GetChild(i);
                if (child != null && child.gameObject != null &&
                    child.gameObject.name.StartsWith(prefix,
                        StringComparison.Ordinal))
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }


        private static void SuppressMainMenuInteraction()
        {
            if (mainMenuInteractionSuppressed || mainSection == null ||
                mainMenuManager == null)
                return;

            mainMenuInteractionSuppressed = true;
            overlayInputFrame = -1;

            suspendedAdSection = mainMenuManager.GetAdSection();
            mainSectionInputWasEnabled = mainSection.inputIsEnabled;
            adSectionInputWasEnabled = suspendedAdSection != null &&
                suspendedAdSection.inputIsEnabled;
            adsWereVisible = mainMenuManager.adsContainer != null &&
                mainMenuManager.adsContainer.activeSelf;
            mainDescriptionWasVisible = mainMenuManager.uiDescription != null &&
                mainMenuManager.uiDescription.gameObject != null &&
                mainMenuManager.uiDescription.gameObject.activeSelf;
            nativeInputStateCaptured = true;

            // Use the same public main-menu API that native windows use.
            // This disables keyboard/gamepad handling and the section event
            // subscriptions instead of only blocking individual controls.
            mainMenuManager.EnableInput(false);
            mainMenuManager.DisableMainSectionInput(true, true);
            mainMenuManager.DisableAdSectionInput(true, true);
            mainMenuManager.HideAds();

            suspendedNavigation = mainSection.ListNavigationManager;
            if (suspendedNavigation != null) {
                suspendedNavigationWasEnabled = suspendedNavigation.enabled;
                suspendedNavigation.enabled = false;
            }

            mainSectionCanvasGroup =
                mainSection.GetComponent<CanvasGroup>();
            if (mainSectionCanvasGroup == null) {
                mainSectionCanvasGroup =
                    mainSection.gameObject.AddComponent<CanvasGroup>();
                mainSectionCanvasGroupAdded = true;
                mainSectionCanvasInteractable = true;
                mainSectionCanvasBlocksRaycasts = true;
                mainSectionCanvasIgnoreParentGroups = false;
            } else {
                mainSectionCanvasGroupAdded = false;
                mainSectionCanvasInteractable =
                    mainSectionCanvasGroup.interactable;
                mainSectionCanvasBlocksRaycasts =
                    mainSectionCanvasGroup.blocksRaycasts;
                mainSectionCanvasIgnoreParentGroups =
                    mainSectionCanvasGroup.ignoreParentGroups;
            }
            mainSectionCanvasGroup.interactable = false;
            mainSectionCanvasGroup.blocksRaycasts = false;

            CaptureAndHideNativeMainHints();
            if (launchButton != null)
                NativeUiFactory.UpdateMainMenuButtonVisual(
                    launchButton, true);

        }

        private static void RestoreMainMenuInteraction()
        {
            if (!mainMenuInteractionSuppressed)
                return;

            mainMenuInteractionSuppressed = false;
            overlayInputFrame = -1;

            if (suspendedNavigation != null)
                suspendedNavigation.enabled = suspendedNavigationWasEnabled;
            suspendedNavigation = null;

            if (mainSectionCanvasGroup != null) {
                if (mainSectionCanvasGroupAdded) {
                    mainSectionCanvasGroup.interactable = true;
                    mainSectionCanvasGroup.blocksRaycasts = true;
                    mainSectionCanvasGroup.ignoreParentGroups = false;
                    UnityEngine.Object.Destroy(mainSectionCanvasGroup);
                } else {
                    mainSectionCanvasGroup.interactable =
                        mainSectionCanvasInteractable;
                    mainSectionCanvasGroup.blocksRaycasts =
                        mainSectionCanvasBlocksRaycasts;
                    mainSectionCanvasGroup.ignoreParentGroups =
                        mainSectionCanvasIgnoreParentGroups;
                }
            }
            mainSectionCanvasGroup = null;
            mainSectionCanvasGroupAdded = false;

            for (int i = 0; i < hiddenNativeHintObjects.Count; i++) {
                GameObject item = hiddenNativeHintObjects[i];
                if (item != null)
                    item.SetActive(hiddenNativeHintStates[i]);
            }
            hiddenNativeHintObjects.Clear();
            hiddenNativeHintStates.Clear();

            if (mainMenuManager != null && nativeInputStateCaptured) {
                mainMenuManager.EnableInput(true);
                mainMenuManager.DisableMainSectionInput(
                    !mainSectionInputWasEnabled, true);
                mainMenuManager.DisableAdSectionInput(
                    !adSectionInputWasEnabled, true);
                if (adsWereVisible)
                    mainMenuManager.ShowAds();
                else
                    mainMenuManager.HideAds();
                if (mainMenuManager.uiDescription != null &&
                    mainMenuManager.uiDescription.gameObject != null) {
                    mainMenuManager.uiDescription.gameObject.SetActive(
                        mainDescriptionWasVisible);
                }
            }

            suspendedAdSection = null;
            nativeInputStateCaptured = false;
            if (mainSection != null)
                mainSection.UpdateMouseEvents();
        }

        private static void CaptureAndHideNativeMainHints()
        {
            hiddenNativeHintObjects.Clear();
            hiddenNativeHintStates.Clear();
            if (mainMenuManager != null &&
                mainMenuManager.uiDescription != null &&
                mainMenuManager.uiDescription.gameObject != null) {
                GameObject descriptionRoot =
                    mainMenuManager.uiDescription.gameObject;
                hiddenNativeHintObjects.Add(descriptionRoot);
                hiddenNativeHintStates.Add(descriptionRoot.activeSelf);
                descriptionRoot.SetActive(false);
            }
            Transform searchRoot = mainSection.transform.parent != null
                ? mainSection.transform.parent : mainSection.transform;
            ControlDescription[] descriptions =
                searchRoot.GetComponentsInChildren<ControlDescription>(true);
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription description = descriptions[i];
                if (description == null || description.gameObject == null ||
                    !description.gameObject.activeInHierarchy)
                    continue;
                GameObject item = description.gameObject;
                if (hiddenNativeHintObjects.Contains(item))
                    continue;
                hiddenNativeHintObjects.Add(item);
                hiddenNativeHintStates.Add(item.activeSelf);
                item.SetActive(false);
            }
        }

        private static void KeepNativeMainHintsHidden()
        {
            if (!IsOverlayOpen || mainSection == null)
                return;
            if (mainMenuManager != null &&
                mainMenuManager.uiDescription != null &&
                mainMenuManager.uiDescription.gameObject != null) {
                TrackAndHideNativeHint(
                    mainMenuManager.uiDescription.gameObject, false);
            }

            Transform searchRoot = mainSection.transform.parent != null
                ? mainSection.transform.parent : mainSection.transform;
            ControlDescription[] descriptions =
                searchRoot.GetComponentsInChildren<ControlDescription>(true);
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription description = descriptions[i];
                if (description == null || description.gameObject == null ||
                    !description.gameObject.activeSelf)
                    continue;
                TrackAndHideNativeHint(description.gameObject, false);
            }
        }

        private static void TrackAndHideNativeHint(GameObject item,
            bool restoreActive)
        {
            if (item == null)
                return;
            int index = hiddenNativeHintObjects.IndexOf(item);
            if (index < 0) {
                hiddenNativeHintObjects.Add(item);
                hiddenNativeHintStates.Add(restoreActive);
            }
            item.SetActive(false);
        }

        private static void DisableAfterError(string stage,
            Exception exception)
        {
            disabled = true;
            RestoreMainMenuInteraction();
            if (windowObject != null)
                windowObject.SetActive(false);
            if (inputShieldObject != null)
                inputShieldObject.SetActive(false);
            if (launchButtonObject != null)
                launchButtonObject.SetActive(false);
            ModLogger.Log("[ModSettings] In-game settings UI disabled after " +
                stage + " failure." + Environment.NewLine + exception,
                Types.LoggingLevels.Warning);
        }
    }

    [HarmonyPatch]
    internal static class ModSettingsPointerSelectionPatch
    {
        [HarmonyPatch(typeof(Selectable), nameof(Selectable.OnPointerEnter))]
        [HarmonyPostfix]
        private static void OnPointerEnterPostfix(Selectable __instance)
        {
            ModSettingsMenuFeature.OnCardPointerEnter(__instance);
        }

        [HarmonyPatch(typeof(Selectable), nameof(Selectable.OnPointerExit))]
        [HarmonyPostfix]
        private static void OnPointerExitPostfix(Selectable __instance)
        {
            ModSettingsMenuFeature.OnCardPointerExit(__instance);
        }

        [HarmonyPatch(typeof(Selectable), nameof(Selectable.OnSelect))]
        [HarmonyPostfix]
        private static void OnSelectPostfix(Selectable __instance)
        {
            ModSettingsMenuFeature.OnSettingsSelectableSelected(__instance);
        }

        [HarmonyPatch(typeof(Selectable), nameof(Selectable.OnDeselect))]
        [HarmonyPostfix]
        private static void OnDeselectPostfix(Selectable __instance)
        {
            ModSettingsMenuFeature.OnSettingsSelectableDeselected(__instance);
        }
    }

    [HarmonyPatch]
    internal static class ModSettingsArrowPointerPatch
    {
        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerEnter))]
        [HarmonyPostfix]
        private static void OnPointerEnterPostfix(EventTrigger __instance)
        {
            ModSettingsMenuFeature.OnSettingsArrowPointerEnter(__instance);
        }

        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerExit))]
        [HarmonyPostfix]
        private static void OnPointerExitPostfix(EventTrigger __instance)
        {
            ModSettingsMenuFeature.OnSettingsArrowPointerExit(__instance);
        }

        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerClick))]
        [HarmonyPostfix]
        private static void OnPointerClickPostfix(EventTrigger __instance)
        {
            ModSettingsMenuFeature.OnSettingsArrowPointerClick(__instance);
        }
    }

    [HarmonyPatch(typeof(LanguageSettingsTab), "OnItemClick")]
    internal static class ModSettingsLaunchButtonLanguageSelectionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(string __1)
        {
            ModLocalization.SetGameLanguage(__1);
            ModSettingsMenuFeature.RefreshLaunchButtonPresentation();
        }
    }

    [HarmonyPatch(typeof(MainSection), "HandleInput")]
    internal static class ModSettingsMainMenuInputPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (!ModSettingsMenuFeature.IsOverlayOpen ||
                ModSettingsMenuFeature.IsDiscardConfirmationOpen)
                return true;
            __result = ModSettingsMenuFeature.HandleOverlayInput();
            return false;
        }
    }

    [HarmonyPatch(typeof(MainMenuButton), "OnPointerClick")]
    internal static class ModSettingsMainMenuPointerPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MainMenuButton __instance)
        {
            return !ModSettingsMenuFeature.TryOpenFromMainMenuButton(
                __instance);
        }
    }

    [HarmonyPatch(typeof(MainMenuButton), "InvokeAction")]
    internal static class ModSettingsMainMenuButtonPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MainMenuButton __instance)
        {
            return !ModSettingsMenuFeature.TryOpenFromMainMenuButton(
                __instance);
        }
    }
}
