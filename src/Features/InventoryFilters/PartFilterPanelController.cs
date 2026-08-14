using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cms21UiPlus
{
    /// <summary>
    /// Reusable search-and-filter controls for part-list windows that do not wire their
    /// bundled SearchField into native filtering.
    /// </summary>
    public sealed class PartFilterPanelController
    {
        private const float ButtonSize = 20f;
        private const float ButtonSpacing = 28f;
        private const float RightButtonOffset = 18f;

        private static readonly Color32 ActiveButtonColor =
            new Color32(255, 255, 255, 255);
        private static readonly Color32 DisabledButtonColor =
            new Color32(125, 125, 125, 210);

        private enum FilterButtonKind
        {
            Condition,
            Repairability,
            Quality,
        }

        private readonly string buttonPrefix;
        private InputField searchField;
        private Transform conditionButton;
        private Transform repairButton;
        private Transform qualityButton;
        private bool searchFieldOriginalActive;
        private bool searchFieldPositionCaptured;
        private Vector2 searchFieldOriginalPosition;
        private float verticalOffset;
        private string lastSearchText = string.Empty;
        private Action conditionClick;
        private Action repairabilityClick;
        private Action qualityClick;
        private Action conditionReverseClick;
        private Action repairabilityReverseClick;
        private Action qualityReverseClick;
        private Action<string> searchChanged;

        public PartFilterPanelController(string buttonPrefix)
        {
            this.buttonPrefix = buttonPrefix;
        }

        public InputField SearchField {
            get { return searchField; }
        }

        public string SearchText {
            get { return searchField != null ? searchField.text ?? string.Empty : string.Empty; }
        }

        public bool IsSearchFocused {
            get { return searchField != null && searchField.isFocused; }
        }

        public bool Attach(Transform windowRoot, Action onConditionClick,
            Action onRepairabilityClick, Action<string> onSearchChanged,
            bool includeRepairability = true)
        {
            return AttachWithButtons(windowRoot, onConditionClick,
                onRepairabilityClick, null, onSearchChanged, true,
                includeRepairability, false);
        }

        internal bool AttachWithButtons(Transform windowRoot,
            Action onConditionClick, Action onRepairabilityClick,
            Action onQualityClick, Action<string> onSearchChanged,
            bool includeCondition, bool includeRepairability,
            bool includeQuality)
        {
            return AttachWithButtons(windowRoot, onConditionClick,
                onRepairabilityClick, onQualityClick, onSearchChanged,
                includeCondition, includeRepairability, includeQuality,
                null, null, null);
        }

        internal bool AttachWithButtons(Transform windowRoot,
            Action onConditionClick, Action onRepairabilityClick,
            Action onQualityClick, Action<string> onSearchChanged,
            bool includeCondition, bool includeRepairability,
            bool includeQuality, Action onConditionReverseClick,
            Action onRepairabilityReverseClick, Action onQualityReverseClick)
        {
            if (windowRoot == null)
                return false;

            InputField field = FindSearchField(windowRoot);
            if (field == null) {
                ModLogger.Log("[PartFilterPanel] SearchField was not found under " +
                    windowRoot.name + ".", Types.LoggingLevels.Warning);
                return false;
            }

            if (searchField != field) {
                Detach();
                searchField = field;
                searchFieldOriginalActive = field.gameObject.activeSelf;
                RectTransform fieldRect = field.GetComponent<RectTransform>();
                if (fieldRect != null) {
                    searchFieldOriginalPosition = fieldRect.anchoredPosition;
                    searchFieldPositionCaptured = true;
                }
            }

            conditionClick = includeCondition ? onConditionClick : null;
            repairabilityClick = includeRepairability ?
                onRepairabilityClick : null;
            qualityClick = includeQuality ? onQualityClick : null;
            conditionReverseClick = includeCondition ?
                onConditionReverseClick : null;
            repairabilityReverseClick = includeRepairability ?
                onRepairabilityReverseClick : null;
            qualityReverseClick = includeQuality ? onQualityReverseClick : null;
            searchChanged = onSearchChanged;
            lastSearchText = string.Empty;

            searchField.gameObject.SetActive(true);
            searchField.text = string.Empty;
            ApplyVerticalOffset();

            Transform root = searchField.transform.parent != null
                ? searchField.transform.parent
                : windowRoot;
            if (includeCondition) {
                conditionButton = EnsureButton(root,
                    buttonPrefix + "Condition", FilterButtonKind.Condition,
                    conditionButton);
            } else {
                DestroyButton(conditionButton);
                conditionButton = null;
            }
            if (includeRepairability) {
                repairButton = EnsureButton(root,
                    buttonPrefix + "Repairability",
                    FilterButtonKind.Repairability, repairButton);
            } else {
                DestroyButton(repairButton);
                repairButton = null;
            }
            if (includeQuality) {
                qualityButton = EnsureButton(root,
                    buttonPrefix + "Quality", FilterButtonKind.Quality,
                    qualityButton);
            } else {
                DestroyButton(qualityButton);
                qualityButton = null;
            }
            PositionButtons();
            return (!includeCondition || conditionButton != null) &&
                (!includeRepairability || repairButton != null) &&
                (!includeQuality || qualityButton != null);
        }

        public void Detach()
        {
            RestoreSearchFieldPosition();
            if (searchField != null)
                searchField.gameObject.SetActive(searchFieldOriginalActive);

            DestroyButton(conditionButton);
            DestroyButton(repairButton);
            DestroyButton(qualityButton);
            conditionButton = null;
            repairButton = null;
            qualityButton = null;
            searchField = null;
            searchFieldPositionCaptured = false;
            verticalOffset = 0f;
            conditionClick = null;
            repairabilityClick = null;
            qualityClick = null;
            conditionReverseClick = null;
            repairabilityReverseClick = null;
            qualityReverseClick = null;
            searchChanged = null;
            lastSearchText = string.Empty;
        }

        public bool HandleKeyPressed(InputField field)
        {
            if (field == null || searchField == null || field != searchField ||
                !field.isFocused)
                return false;

            string current = field.text ?? string.Empty;
            if (string.Equals(current, lastSearchText, StringComparison.Ordinal))
                return true;

            lastSearchText = current;
            Action<string> callback = searchChanged;
            if (callback != null)
                callback(current);
            return true;
        }

        public void ResetSearch()
        {
            SetSearchText(string.Empty);
        }

        internal void SetSearchText(string text)
        {
            lastSearchText = text ?? string.Empty;
            if (searchField != null)
                searchField.text = lastSearchText;
        }

        internal void SetVerticalOffset(float offset)
        {
            verticalOffset = offset;
            ApplyVerticalOffset();
            PositionButtons();
        }

        internal void EnsureVisible()
        {
            if (searchField != null && !searchField.gameObject.activeSelf)
                searchField.gameObject.SetActive(true);
            if (conditionButton != null && !conditionButton.gameObject.activeSelf)
                conditionButton.gameObject.SetActive(true);
            if (repairButton != null && !repairButton.gameObject.activeSelf)
                repairButton.gameObject.SetActive(true);
            if (qualityButton != null && !qualityButton.gameObject.activeSelf)
                qualityButton.gameObject.SetActive(true);
        }

        internal bool IsUiUnder(Transform root)
        {
            if (root == null)
                return false;

            return IsTransformUnder(root,
                    searchField != null ? searchField.transform : null) ||
                IsTransformUnder(root, conditionButton) ||
                IsTransformUnder(root, repairButton) ||
                IsTransformUnder(root, qualityButton);
        }

        private static bool IsTransformUnder(Transform root, Transform target)
        {
            return target != null &&
                (target == root || target.IsChildOf(root));
        }

        public void UpdateVisuals(GarageConditionFilterMode conditionMode,
            RepairabilityQuickFilterMode repairabilityMode)
        {
            UpdateVisuals(conditionMode, repairabilityMode,
                QualityQuickFilterMode.Off);
        }

        public void UpdateVisuals(GarageConditionFilterMode conditionMode,
            RepairabilityQuickFilterMode repairabilityMode,
            QualityQuickFilterMode qualityMode)
        {
            UpdateConditionVisual(conditionMode);
            UpdateRepairabilityVisual(repairabilityMode);
            UpdateQualityVisual(qualityMode);
        }

        public void UpdateVisuals(JunkyardConditionFilterMode conditionMode,
            RepairabilityQuickFilterMode repairabilityMode)
        {
            UpdateVisuals(conditionMode, repairabilityMode,
                QualityQuickFilterMode.Off);
        }

        public void UpdateVisuals(JunkyardConditionFilterMode conditionMode,
            RepairabilityQuickFilterMode repairabilityMode,
            QualityQuickFilterMode qualityMode)
        {
            UpdateConditionVisual(conditionMode);
            UpdateRepairabilityVisual(repairabilityMode);
            UpdateQualityVisual(qualityMode);
        }

        private void UpdateConditionVisual(GarageConditionFilterMode conditionMode)
        {
            if (conditionButton == null)
                return;

            Image image = conditionButton.GetComponent<Image>();
            if (image == null)
                return;

            switch (conditionMode) {
                case GarageConditionFilterMode.RepairThresholdToPerfect:
                    image.sprite = InventoryIconProvider.GetWhiteConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case GarageConditionFilterMode.Red:
                    image.sprite = InventoryIconProvider.GetRedConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case GarageConditionFilterMode.Orange:
                    image.sprite = InventoryIconProvider.GetOrangeConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case GarageConditionFilterMode.Yellow:
                    image.sprite = InventoryIconProvider.GetYellowConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case GarageConditionFilterMode.GreenRing:
                    image.sprite = InventoryIconProvider.GetGreenRingConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case GarageConditionFilterMode.Perfect:
                    image.sprite = InventoryIconProvider.GetGreenConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                default:
                    image.sprite = InventoryIconProvider.GetWhiteConditionIcon();
                    image.color = DisabledButtonColor;
                    break;
            }
        }

        private void UpdateConditionVisual(JunkyardConditionFilterMode conditionMode)
        {
            if (conditionButton == null)
                return;

            Image image = conditionButton.GetComponent<Image>();
            if (image == null)
                return;

            switch (conditionMode) {
                case JunkyardConditionFilterMode.RepairThresholdToPerfect:
                    image.sprite = InventoryIconProvider.GetWhiteConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case JunkyardConditionFilterMode.Orange:
                    image.sprite = InventoryIconProvider.GetOrangeConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case JunkyardConditionFilterMode.Yellow:
                    image.sprite = InventoryIconProvider.GetYellowConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case JunkyardConditionFilterMode.Green:
                    image.sprite = InventoryIconProvider.GetGreenRingConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                case JunkyardConditionFilterMode.Red:
                    image.sprite = InventoryIconProvider.GetRedConditionIcon();
                    image.color = ActiveButtonColor;
                    break;
                default:
                    image.sprite = InventoryIconProvider.GetWhiteConditionIcon();
                    image.color = DisabledButtonColor;
                    break;
            }
        }

        private void UpdateRepairabilityVisual(
            RepairabilityQuickFilterMode repairabilityMode)
        {
            if (repairButton == null)
                return;

            Image image = repairButton.GetComponent<Image>();
            if (image == null)
                return;

            switch (repairabilityMode) {
                case RepairabilityQuickFilterMode.RepairGroupOnly:
                    image.sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
                    image.color = ActiveButtonColor;
                    break;
                case RepairabilityQuickFilterMode.NonRepairableOnly:
                    image.sprite = InventoryIconProvider.GetRedRepairWrenchIcon();
                    image.color = ActiveButtonColor;
                    break;
                default:
                    image.sprite = InventoryIconProvider.GetWhiteRepairWrenchIcon();
                    image.color = DisabledButtonColor;
                    break;
            }
        }

        private void UpdateQualityVisual(QualityQuickFilterMode qualityMode)
        {
            if (qualityButton == null)
                return;

            Image image = qualityButton.GetComponent<Image>();
            if (image == null)
                return;

            switch (qualityMode) {
                case QualityQuickFilterMode.Improved:
                    image.sprite = InventoryIconProvider.GetQualityIcon();
                    image.color = ActiveButtonColor;
                    break;
                case QualityQuickFilterMode.Quality1:
                    image.sprite = InventoryIconProvider.GetQuality1Icon();
                    image.color = ActiveButtonColor;
                    break;
                case QualityQuickFilterMode.Quality2:
                    image.sprite = InventoryIconProvider.GetQuality2Icon();
                    image.color = ActiveButtonColor;
                    break;
                case QualityQuickFilterMode.Quality3:
                    image.sprite = InventoryIconProvider.GetQuality3Icon();
                    image.color = ActiveButtonColor;
                    break;
                case QualityQuickFilterMode.NonImproved:
                    image.sprite = InventoryIconProvider.GetQualityNonIcon();
                    image.color = ActiveButtonColor;
                    break;
                default:
                    image.sprite = InventoryIconProvider.GetQualityIcon();
                    image.color = DisabledButtonColor;
                    break;
            }
        }

        private Transform EnsureButton(Transform root, string name,
            FilterButtonKind kind, Transform existing)
        {
            Transform buttonTransform = existing;
            if (buttonTransform == null)
                buttonTransform = FindSingleChild(root, name);

            if (buttonTransform == null) {
#if NET6_0_OR_GREATER
                GameObject buttonObject = new GameObject(name, typeof(RectTransform));
#else
                UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                    new UnhollowerBaseLib.Il2CppReferenceArray<Il2CppSystem.Type>(1);
                componentTypes[0] =
                    UnhollowerRuntimeLib.Il2CppType.Of<RectTransform>();
                GameObject buttonObject = new GameObject(name, componentTypes);
#endif
                buttonObject.transform.SetParent(root, false);
                buttonObject.layer = root.gameObject.layer;
                buttonTransform = buttonObject.transform;

                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
                rect.localScale = Vector3.one;

                Image image = buttonObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = true;

                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }

            buttonTransform.gameObject.SetActive(true);
            buttonTransform.SetAsLastSibling();
            ConfigureButton(buttonTransform, kind);
            return buttonTransform;
        }

        private void ConfigureButton(Transform buttonTransform,
            FilterButtonKind kind)
        {
            if (buttonTransform == null)
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

            Action click = kind == FilterButtonKind.Condition
                ? conditionClick
                : kind == FilterButtonKind.Repairability
                    ? repairabilityClick : qualityClick;
            if (click != null)
                button.onClick.AddListener(click);

            Action reverseClick = kind == FilterButtonKind.Condition
                ? conditionReverseClick
                : kind == FilterButtonKind.Repairability
                    ? repairabilityReverseClick : qualityReverseClick;
            InventoryFilterManager.RegisterReverseQuickFilterClick(
                button, reverseClick);
        }

        private void ApplyVerticalOffset()
        {
            if (!searchFieldPositionCaptured || searchField == null)
                return;

            RectTransform rect = searchField.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = new Vector2(
                    searchFieldOriginalPosition.x,
                    searchFieldOriginalPosition.y + verticalOffset);
        }

        private void RestoreSearchFieldPosition()
        {
            if (!searchFieldPositionCaptured || searchField == null)
                return;

            RectTransform rect = searchField.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = searchFieldOriginalPosition;
        }

        private void PositionButtons()
        {
            if (searchField == null || (conditionButton == null &&
                repairButton == null && qualityButton == null))
                return;

            RectTransform searchRect = searchField.GetComponent<RectTransform>();
            if (searchRect == null || searchRect.transform.parent == null)
                return;

            float width = searchRect.rect.width;
            if (width <= 1f)
                width = 220f;

            float left = searchRect.anchoredPosition.x -
                (width * searchRect.pivot.x);
            float y = searchRect.anchoredPosition.y;
            float x = left - RightButtonOffset;

            if (qualityButton != null) {
                PositionButton(qualityButton, searchRect, x, y);
                x -= ButtonSpacing;
            }
            if (repairButton != null) {
                PositionButton(repairButton, searchRect, x, y);
                x -= ButtonSpacing;
            }
            if (conditionButton != null)
                PositionButton(conditionButton, searchRect, x, y);
        }

        private static void PositionButton(Transform buttonTransform,
            RectTransform reference, float x, float y)
        {
            RectTransform rect = buttonTransform != null
                ? buttonTransform.GetComponent<RectTransform>()
                : null;
            if (rect == null || reference == null)
                return;

            rect.SetParent(reference.transform.parent, false);
            rect.anchorMin = reference.anchorMin;
            rect.anchorMax = reference.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
        }

        private static InputField FindSearchField(Transform root)
        {
            InputField fallback = null;
            float fallbackWidth = -1f;

            foreach (InputField field in root.GetComponentsInChildren<InputField>(true)) {
                if (field == null)
                    continue;
                if (string.Equals(field.name, "SearchField",
                    StringComparison.OrdinalIgnoreCase))
                    return field;

                RectTransform rect = field.GetComponent<RectTransform>();
                float width = rect != null ? rect.rect.width : 0f;
                if (width > fallbackWidth) {
                    fallback = field;
                    fallbackWidth = width;
                }
            }

            return fallback;
        }

        private static Transform FindSingleChild(Transform root, string name)
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

                DestroyButton(child);
            }
            return keep;
        }

        private static void DestroyButton(Transform buttonTransform)
        {
            if (buttonTransform == null)
                return;
            InventoryFilterManager.UnregisterReverseQuickFilterClick(
                buttonTransform.GetComponent<Button>());
            buttonTransform.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(buttonTransform.gameObject);
        }

        public static void ClearSelectedControl()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
        }
    }
}
