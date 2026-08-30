using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppCMS.MainMenu;
using Il2CppCMS.MainMenu.Controls;
using Il2CppCMS.MainMenu.Logic;
using Il2CppCMS.MainMenu.Windows;
using GenericButtonOutline = Il2CppCMS.UI.Controls.GenericButtonOutline;
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS.MainMenu;
using CMS.MainMenu.Controls;
using CMS.MainMenu.Logic;
using CMS.MainMenu.Windows;
using GenericButtonOutline = CMS.UI.Controls.GenericButtonOutline;
using CMS.UI.Description;
using CMS.UI.Logic;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Creates mod UI objects from live native UI templates. Visual hierarchy
    /// is cloned where possible; native behaviour and events are disabled.
    /// </summary>
    internal static class NativeUiFactory
    {
        internal sealed class ModCardHandle
        {
            public GameObject Root;
            public Button Button;
            public Image Background;
            public GameObject FocusFrame;
            public Image Icon;
            public Text Title;
            public Text UnsupportedText;
        }

        internal sealed class SettingsRowHandle
        {
            public GameObject Root;
            public Button Button;
            public Image Background;
            public GameObject HoverVisual;
            public GameObject SelectedVisual;
            public GameObject ValueIndicator;
            public Image ValueIndicatorImage;
            public GameObject LeftArrow;
            public RectTransform LeftArrowRect;
            public Graphic[] LeftArrowGraphics;
            public GameObject RightArrow;
            public RectTransform RightArrowRect;
            public Graphic[] RightArrowGraphics;
            public Vector2 StateAnchorMin;
            public Vector2 StateAnchorMax;
            public Vector2 StatePivot;
            public Vector2 StateAnchoredPosition;
            public Vector2 StateSizeDelta;
            public TextAnchor StateAlignment;
            public Text Label;
            public Text State;
            public Text ApplyWarning;
        }

        internal sealed class FooterHintHandle
        {
            public GameObject Root;
            public RectTransform Rect;
            public RectTransform ContentRect;
            public Vector2 NormalizedSize;
            public Button Button;
            public CanvasGroup CanvasGroup;
            public readonly List<Image> KeyImages = new List<Image>();
            public Vector2 KeySize;
            public Color NormalColor;
            public ControlDescription Description;
            public ControlDescription Source;
            public UIDescription InputOwner;
            public UnityAction Action;
            public string[] Keys;
            public string Text;
            public Text Label;
            public float Width;
            public float Height;
            public bool Enabled = true;
            public bool BlockKeyboardInput = true;
            public bool UsesNativeHoldSuffix;
            public bool HoldSuffixHovered;
            public EventTrigger HoldHoverTrigger;
            public string HoldBaseText;
            public string HoldSuffixText;
            public Color HoldBaseColor;
            public int HoldSuffixFontSize;
            public bool NormalizeRect;
            public bool IsStyledFooter;
            public ControlHintRowHandle Row;
        }

        internal sealed class ControlHintOptions
        {
            public ControlDescription Source;
            public ControlDescription Template;
            public ControlDescription VariantSource;
            public Transform Parent;
            public string Name;
            public string[] Keys;
            public string Label;
            public Action Action;
            public DescriptionInputHandlingMethod InputHandlingMethod =
                DescriptionInputHandlingMethod.ButtonDown;
            public bool CanHold;
            public float TimeToHold;
            public bool OnlyHandleMouseClickInput = true;
            public bool NormalizeRect = true;
            public bool SetAsLastSibling;
        }

        internal sealed class ControlHintSpec
        {
            public ControlDescription Source;
            public ControlDescription Template;
            public ControlDescription VariantSource;
            public string Name;
            public string[] Keys;
            public string Label;
            public Action Action;
            public DescriptionInputHandlingMethod InputHandlingMethod =
                DescriptionInputHandlingMethod.ButtonDown;
            public bool CanHold;
            public float TimeToHold;
            public bool OnlyHandleMouseClickInput = true;
            public bool Enabled = true;
        }

        internal sealed class ControlHintRowOptions
        {
            public Transform Parent;
            public ControlDescription Source;
            public string Name = "NativeControlHintRow";
            public ControlHintSpec[] Hints;
            public float Spacing = 10f;
            public bool NormalizeItems = true;
            public bool LayoutItems = true;
            public bool WrapItems = false;
            public float LineSpacing = 2f;
            public bool SetAsLastSibling;
        }

        internal sealed class ControlHintRowHandle
        {
            public Transform Parent;
            public readonly List<FooterHintHandle> Hints =
                new List<FooterHintHandle>();
            public float Spacing;
            public float Width;
            public float Height;
            public bool LayoutItems;
            public bool WrapItems;
            public float LineSpacing;
        }

        private sealed class HintStyleProfile
        {
            public readonly ImageStyle KeyImage = new ImageStyle();
            public readonly TextStyle Text = new TextStyle();
            public Vector2 KeySize = new Vector2(14.4f, 14.4f);
            public float Height = 15f;
            public int FontSize = 10;
            public float Spacing = 4f;
            public Color TextColor = new Color(0.74f, 0.74f, 0.74f, 1f);
        }

        private sealed class MainMenuVisualHandle
        {
            public MainMenuButton Button;
            public Image BaseImage;
            public Image StateSink;
            public Text Text;
            public Color NormalTextRendererColor;
        }

        internal sealed class SortingWindowHandle
        {
            public GameObject Root;
            public readonly List<GenericButtonOutline> Buttons =
                new List<GenericButtonOutline>();
            public Il2CppSystem.Action<int> MouseHoverAction;
        }

        internal sealed class ModsWindowHandle
        {
            public GameObject InputShield;
            public GameObject Root;
            public GameObject CardsPage;
            public RectTransform CardsRoot;
            public Text PageIndicator;
            public GameObject SettingsPage;
            public RectTransform SettingsContent;
            public ScrollRect SettingsScroll;
            public Text SettingsHeader;
            public Text SettingsStatus;
            public RectTransform CardsFooterRoot;
            public RectTransform FooterRoot;
        }

        private sealed class RectStyle
        {
            public Vector2 AnchorMin = Vector2.zero;
            public Vector2 AnchorMax = Vector2.one;
            public Vector2 Pivot = new Vector2(0.5f, 0.5f);
            public Vector2 AnchoredPosition = Vector2.zero;
            public Vector2 SizeDelta = Vector2.zero;
            public Vector3 LocalScale = Vector3.one;

            public void Capture(RectTransform source)
            {
                if (source == null)
                    return;
                AnchorMin = source.anchorMin;
                AnchorMax = source.anchorMax;
                Pivot = source.pivot;
                AnchoredPosition = source.anchoredPosition;
                SizeDelta = source.sizeDelta;
                LocalScale = source.localScale;
            }

            public void Apply(RectTransform target)
            {
                if (target == null)
                    return;
                target.anchorMin = AnchorMin;
                target.anchorMax = AnchorMax;
                target.pivot = Pivot;
                target.anchoredPosition = AnchoredPosition;
                target.sizeDelta = SizeDelta;
                target.localScale = LocalScale;
            }
        }

        private sealed class ImageStyle
        {
            public Sprite Sprite;
            public Material Material;
            public Image.Type Type = Image.Type.Simple;
            public Color Color = Color.white;
            public bool PreserveAspect;
            public bool FillCenter = true;
            public Image.FillMethod FillMethod = Image.FillMethod.Radial360;
            public float FillAmount = 1f;
            public float PixelsPerUnitMultiplier = 1f;

            public void Capture(Image source)
            {
                if (source == null)
                    return;
                Sprite = source.sprite;
                Material = source.material;
                Type = source.type;
                Color = source.color;
                PreserveAspect = source.preserveAspect;
                FillCenter = source.fillCenter;
                FillMethod = source.fillMethod;
                FillAmount = source.fillAmount;
                PixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            }

            public void Apply(Image target)
            {
                if (target == null)
                    return;
                target.sprite = Sprite;
                target.material = Material;
                target.type = Type;
                target.color = Color;
                target.preserveAspect = PreserveAspect;
                target.fillCenter = FillCenter;
                target.fillMethod = FillMethod;
                target.fillAmount = FillAmount;
                target.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;
            }
        }

        private sealed class SettingsArrowStyle
        {
            public readonly ImageStyle Image = new ImageStyle();
            public Quaternion LocalRotation = Quaternion.identity;
            public Vector3 LocalScale = Vector3.one;
            public bool IsValid;

            public void Capture(Image source)
            {
                if (source == null || source.sprite == null)
                    return;
                Image.Capture(source);
                LocalRotation = source.rectTransform.localRotation;
                Vector3 scale = source.rectTransform.localScale;
                LocalScale = new Vector3(
                    scale.x < 0f ? -1f : 1f,
                    scale.y < 0f ? -1f : 1f, 1f);
                IsValid = true;
            }

            public void Capture(Sprite sprite)
            {
                if (sprite == null)
                    return;
                Image.Sprite = sprite;
                Image.Material = null;
                Image.Type = UnityEngine.UI.Image.Type.Simple;
                Image.Color = Color.white;
                Image.PreserveAspect = true;
                Image.FillCenter = true;
                Image.FillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                Image.FillAmount = 1f;
                Image.PixelsPerUnitMultiplier = 1f;
                LocalRotation = Quaternion.identity;
                LocalScale = Vector3.one;
                IsValid = true;
            }

            public void Reset()
            {
                IsValid = false;
                LocalRotation = Quaternion.identity;
                LocalScale = Vector3.one;
            }
        }

        private sealed class TextStyle
        {
            public Font Font;
            public int FontSize = 20;
            public FontStyle FontStyle = FontStyle.Normal;
            public Color Color = Color.white;
            public TextAnchor Alignment = TextAnchor.MiddleLeft;
            public HorizontalWrapMode HorizontalOverflow =
                HorizontalWrapMode.Wrap;
            public VerticalWrapMode VerticalOverflow =
                VerticalWrapMode.Truncate;
            public bool ResizeTextForBestFit;
            public int ResizeTextMinSize = 10;
            public int ResizeTextMaxSize = 24;
            public float LineSpacing = 1f;

            public void Capture(Text source)
            {
                if (source == null)
                    return;
                Font = source.font;
                FontSize = source.fontSize;
                FontStyle = source.fontStyle;
                Color = source.color;
                Alignment = source.alignment;
                HorizontalOverflow = source.horizontalOverflow;
                VerticalOverflow = source.verticalOverflow;
                ResizeTextForBestFit = source.resizeTextForBestFit;
                ResizeTextMinSize = source.resizeTextMinSize;
                ResizeTextMaxSize = source.resizeTextMaxSize;
                LineSpacing = source.lineSpacing;
            }

            public void Apply(Text target)
            {
                if (target == null)
                    return;
                if (Font != null)
                    target.font = Font;
                target.fontSize = FontSize;
                target.fontStyle = FontStyle;
                target.color = Color;
                target.alignment = Alignment;
                target.horizontalOverflow = HorizontalOverflow;
                target.verticalOverflow = VerticalOverflow;
                target.resizeTextForBestFit = ResizeTextForBestFit;
                target.resizeTextMinSize = ResizeTextMinSize;
                target.resizeTextMaxSize = ResizeTextMaxSize;
                target.lineSpacing = LineSpacing;
            }
        }

        private static readonly RectStyle WindowRect = new RectStyle();
        private static readonly RectStyle CardsRect = new RectStyle();
        private static readonly RectStyle CardRect = new RectStyle();
        private static readonly RectStyle CardSelectedRect = new RectStyle();
        private static readonly RectStyle CardIconRect = new RectStyle();
        private static readonly RectStyle CardTitleRect = new RectStyle();
        private static readonly RectStyle SettingsRowRect = new RectStyle();
        private static readonly RectStyle DescriptionRect = new RectStyle();
        private static readonly RectStyle HintRect = new RectStyle();
        private static readonly RectStyle HintKeyRect = new RectStyle();

        private static readonly ImageStyle WindowBackground =
            new ImageStyle();
        private static readonly ImageStyle CardBackground =
            new ImageStyle();
        private static readonly ImageStyle CardSelected =
            new ImageStyle();
        private static readonly ImageStyle CardIcon =
            new ImageStyle();
        private static readonly ImageStyle SettingsBackground =
            new ImageStyle();
        private static readonly ImageStyle SettingsHover =
            new ImageStyle();
        private static readonly ImageStyle SettingsSelected =
            new ImageStyle();
        private static readonly SettingsArrowStyle SettingsLeftArrow =
            new SettingsArrowStyle();
        private static readonly SettingsArrowStyle SettingsRightArrow =
            new SettingsArrowStyle();
        private static readonly ImageStyle HintKeyImage =
            new ImageStyle();

        private static readonly TextStyle BaseText = new TextStyle();
        private static readonly TextStyle CardText = new TextStyle();
        private static readonly TextStyle SettingsText = new TextStyle();
        private static readonly TextStyle HintText = new TextStyle();

        private static readonly Dictionary<int, FooterHintHandle>
            NativeHoldHoverHints = new Dictionary<int, FooterHintHandle>();
        private const string ModifierKeyObjectPrefix =
            "CMS21UIPlus.ModifierKey_";

        private static bool initialized;
        private static TutorialsWindow tutorialsSource;
        private static SettingsButton settingsSource;
        private static ControlDescription hintSource;
        private static MainMenuVisualHandle mainMenuVisual;
        private static Sprite modCardIcon;
        private static int modCardIconTutorialIndex = -1;
        private static Color mainMenuNormalGraphicColor = Color.white;
        private static Color mainMenuNormalTextColor = Color.white;
        private static Color mainMenuNormalTextComponentColor = Color.white;
        private static Vector2 nativeCardSize = new Vector2(133f, 96f);
        private static Vector2 nativeCardsAreaSize =
            new Vector2(571f, 425f);
        private static Vector2 nativeSettingsRowSize =
            new Vector2(600f, 48f);
        private static Color settingsNormalColor =
            new Color(0.17f, 0.18f, 0.20f, 0.96f);
        private static Color settingsHoverColor =
            new Color(0.68f, 0.68f, 0.68f, 0.96f);
        private static Color settingsPressedColor =
            new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color SettingsEnabledColor =
            new Color(0.10f, 0.86f, 0.18f, 1f);
        private static readonly Color SettingsDisabledColor =
            new Color(0.88f, 0.12f, 0.10f, 1f);
        private static Color cardNormalColor =
            new Color(0.50f, 0.50f, 0.50f, 0.88f);
        private static Color hintNormalColor =
            new Color(0.74f, 0.74f, 0.74f, 1f);
        private static readonly Color HintHoverColor =
            new Color(1f, 153f / 255f, 0f, 1f);
        private static Vector2 nativeHintKeySize = new Vector2(14.4f, 14.4f);
        private static float nativeHintHeight = 15f;
        private static int nativeHintFontSize = 10;
        private static float hintSpacing = 4f;

        public static Font Font
        {
            get { return BaseText.Font; }
        }

        public static int BaseFontSize
        {
            get { return Mathf.Clamp(BaseText.FontSize, 14, 28); }
        }

        public static Vector2 NativeCardSize
        {
            get { return nativeCardSize; }
        }

        public static Vector2 NativeCardsAreaSize
        {
            get { return nativeCardsAreaSize; }
        }

        public static void Reset()
        {
            initialized = false;
            tutorialsSource = null;
            settingsSource = null;
            hintSource = null;
            mainMenuVisual = null;
            modCardIcon = null;
            SettingsLeftArrow.Reset();
            SettingsRightArrow.Reset();
        }

        public static void Initialize(MainMenuManager manager,
            MainMenuButton launchButton, TutorialsWindow tutorialsWindow)
        {
            if (initialized && tutorialsSource != null)
                return;

            tutorialsSource = tutorialsWindow;
            CaptureTutorialStyles(tutorialsWindow);
            CaptureSettingsStyles();
            CaptureHintStyles(tutorialsWindow);
            EnsureFallbacks(launchButton);
            initialized = true;

        }


        public static MainMenuButton CreateMainMenuButtonFromStyle(
            MainMenuButton source, Transform parent, string name,
            string label)
        {
            if (source == null || source.gameObject == null)
                return null;

            GameObject root = CreateUiObject(name, parent);
            root.SetActive(false);
            CopyRect(source.GetComponent<RectTransform>(),
                root.GetComponent<RectTransform>());

            Image sourceImage = source.graphic != null
                ? source.graphic.TryCast<Image>() : null;
            Image image = root.AddComponent<Image>();
            ImageStyle imageStyle = new ImageStyle();
            imageStyle.Capture(sourceImage);
            imageStyle.Apply(image);
            image.raycastTarget = true;
            if (sourceImage != null && sourceImage.canvasRenderer != null)
                mainMenuNormalGraphicColor =
                    sourceImage.canvasRenderer.GetColor();
            if (source.text != null) {
                mainMenuNormalTextComponentColor = source.text.color;
                if (source.text.canvasRenderer != null) {
                    mainMenuNormalTextColor =
                        source.text.canvasRenderer.GetColor();
                }
            }

            int copiedFrames = CopyMainMenuButtonDecorations(
                source, sourceImage, root.transform);
            if (copiedFrames == 0) {
                CreateFallbackMainMenuFrame(root.transform,
                    source.text != null ? source.text.color :
                    new Color(0.72f, 0.72f, 0.72f, 0.9f));
            }

            GameObject textObject = CreateUiObject("Text", root.transform);
            Text text = textObject.AddComponent<Text>();
            TextStyle textStyle = new TextStyle();
            textStyle.Capture(source.text);
            textStyle.Apply(text);
            text.text = label;
            text.raycastTarget = false;
            if (source.text != null)
                CopyRect(source.text.rectTransform, text.rectTransform);
            else
                Stretch(text.rectTransform, 18f, 0f, -18f, 0f);
            textObject.transform.SetAsLastSibling();

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.ignoreParentGroups = false;

            MainMenuButton button = root.AddComponent(
                Il2CppType.Of<MainMenuButton>()).TryCast<MainMenuButton>();
            if (button == null) {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            button.text = text;
            // MainMenuButton already calculates the native renderer tint for
            // its Graphic. Point it at the visible image instead of routing
            // the state through a transparent proxy.
            button.graphic = image;
            button.canvasGroup = canvasGroup;
            button.Type = (MainMenuButtonType)(-1000);
            button.UsedInList = true;
            button.UseAdditionalMouseEvents = false;
            button.useAdditionalMouseEvents = false;

            mainMenuVisual = new MainMenuVisualHandle {
                Button = button,
                BaseImage = image,
                StateSink = null,
                Text = text,
                NormalTextRendererColor = mainMenuNormalTextColor,
            };
            root.SetActive(true);
            image.canvasRenderer.SetColor(mainMenuNormalGraphicColor);
            text.canvasRenderer.SetColor(mainMenuNormalTextColor);
            UpdateMainMenuButtonVisual(button, false);
            return button;
        }


        public static void UpdateMainMenuButtonVisual(
            MainMenuButton button, bool active)
        {
            if (button == null || button.gameObject == null)
                return;

            if (button.canvasGroup != null)
                button.canvasGroup.alpha = 1f;

            // The visible image and text are assigned directly to the native
            // MainMenuButton. Its state machine controls normal, hover,
            // selected, active and disabled renderer colours. Do not compete
            // with it by writing colours every frame.
        }


        public static void ResetMainMenuButtonVisual(
            MainMenuButton button)
        {
            if (button == null || button.gameObject == null)
                return;

            MainMenuVisualHandle visual = mainMenuVisual;
            if (visual != null && visual.Button == button) {
                if (visual.BaseImage != null &&
                    visual.BaseImage.canvasRenderer != null) {
                    visual.BaseImage.canvasRenderer.SetColor(
                        mainMenuNormalGraphicColor);
                }
                if (visual.Text != null) {
                    visual.Text.color = mainMenuNormalTextComponentColor;
                    if (visual.Text.canvasRenderer != null) {
                        visual.Text.canvasRenderer.SetColor(
                            visual.NormalTextRendererColor);
                    }
                }
                return;
            }

            Image image = button.graphic != null
                ? button.graphic.TryCast<Image>() : null;
            if (image != null && image.canvasRenderer != null)
                image.canvasRenderer.SetColor(mainMenuNormalGraphicColor);
            if (button.text != null) {
                button.text.color = mainMenuNormalTextComponentColor;
                if (button.text.canvasRenderer != null) {
                    button.text.canvasRenderer.SetColor(
                        mainMenuNormalTextColor);
                }
            }
        }


        private static int CopyMainMenuButtonDecorations(
            MainMenuButton source, Image sourceImage, Transform targetRoot)
        {
            if (source == null || source.transform == null ||
                targetRoot == null)
                return 0;

            int copied = 0;
            Image[] images = source.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++) {
                Image candidate = images[i];
                if (candidate == null || candidate == sourceImage ||
                    candidate.gameObject == source.gameObject ||
                    candidate.transform.parent != source.transform)
                    continue;
                if (source.text != null &&
                    candidate.transform.IsChildOf(source.text.transform))
                    continue;

                GameObject decoration = CreateUiObject(
                    "Style_" + candidate.gameObject.name, targetRoot);
                CopyRect(candidate.GetComponent<RectTransform>(),
                    decoration.GetComponent<RectTransform>());
                Image imageDecoration = decoration.AddComponent<Image>();
                ImageStyle decorationStyle = new ImageStyle();
                decorationStyle.Capture(candidate);
                decorationStyle.Apply(imageDecoration);
                imageDecoration.raycastTarget = false;
                decoration.SetActive(candidate.gameObject.activeSelf);
                copied++;
            }
            return copied;
        }


        private static void CreateFallbackMainMenuFrame(
            Transform parent, Color color)
        {
            Color frameColor = color;
            frameColor.a = Mathf.Clamp(frameColor.a, 0.72f, 0.95f);
            const float thickness = 1f;

            CreateFrameLine(parent, "Style_FrameTop",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -thickness), Vector2.zero,
                frameColor);
            CreateFrameLine(parent, "Style_FrameBottom",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, thickness),
                frameColor);
            CreateFrameLine(parent, "Style_FrameLeft",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(thickness, 0f),
                frameColor);
            CreateFrameLine(parent, "Style_FrameRight",
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-thickness, 0f), Vector2.zero,
                frameColor);
        }


        private static void CreateFrameLine(Transform parent,
            string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject line = CreateUiObject(name, parent);
            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = line.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }


        public static RectTransform CreateMaskedViewport(Transform parent,
            string name)
        {
            GameObject root = CreateUiObject(name, parent);
            Image image = root.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.001f);
            image.raycastTarget = true;
            root.AddComponent<RectMask2D>();
            return root.GetComponent<RectTransform>();
        }

        public static RectTransform CreateVerticalContent(Transform parent,
            string name, float spacing)
        {
            GameObject root = CreateUiObject(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout =
                root.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = spacing;
            RectOffset padding = new RectOffset();
            padding.left = 0;
            padding.right = 0;
            padding.top = 2;
            padding.bottom = 2;
            layout.padding = padding;

            ContentSizeFitter fitter =
                root.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return rect;
        }

        public static ScrollRect CreateVerticalScroll(GameObject host,
            RectTransform viewport, RectTransform content)
        {
            ScrollRect scroll = host.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26f;
            return scroll;
        }

        public static GameObject CreateModalBackdrop(Transform parent,
            string name)
        {
            GameObject root = CreateUiObject(name, parent);
            Stretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image image = root.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.75f);
            image.raycastTarget = true;
            return root;
        }

        public static GameObject CreatePanel(Transform parent, string name,
            Color color, Color borderColor)
        {
            GameObject root = CreateUiObject(name, parent);
            Image image = root.AddComponent<Image>();
            SettingsBackground.Apply(image);
            image.color = color;
            image.raycastTarget = true;
            AddBorder(root, borderColor, 1.2f);
            return root;
        }

        public static Button CreateActionButton(Transform parent,
            string name, string label, Action action)
        {
            GameObject root = CreateUiObject(name, parent);
            Image image = root.AddComponent<Image>();
            SettingsBackground.Apply(image);
            image.color = settingsNormalColor;
            image.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = settingsNormalColor;
            colors.highlightedColor = settingsHoverColor;
            colors.pressedColor = settingsPressedColor;
            colors.selectedColor = settingsHoverColor;
            colors.disabledColor = new Color(settingsNormalColor.r,
                settingsNormalColor.g, settingsNormalColor.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            if (action != null) {
                button.onClick.AddListener(
                    DelegateSupport.ConvertDelegate<UnityAction>(
                        new Action(action)));
            }

            Text text = CreateText(root.transform, "Text", label,
                Mathf.Max(12, SettingsText.FontSize - 2),
                TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform, 4f, 2f, -4f, -2f);
            return button;
        }

        public static GameObject CreateWindow(string name, Transform parent)
        {
            GameObject root = CreateUiObject(name, parent);
            WindowRect.Apply(root.GetComponent<RectTransform>());
            Image background = root.AddComponent<Image>();
            WindowBackground.Apply(background);
            background.raycastTarget = true;
            return root;
        }

        public static SortingWindowHandle CreateSortingWindow(
            SortingWindow source, string name, string title,
            string[] captions, Action<int> onSelected)
        {
            if (source == null || source.gameObject == null ||
                source.transform == null || source.transform.parent == null)
                return null;

            Transform sourceBackground = source.transform.Find("BG");
            Transform sourceWindow = source.transform.Find("Window");
            if (sourceBackground == null || sourceWindow == null)
                return null;

            GameObject root = CreateUiObject(name, source.transform.parent);
            root.SetActive(false);
            CopyRect(source.GetComponent<RectTransform>(),
                root.GetComponent<RectTransform>());

            GameObject background = GameObject.Instantiate(
                sourceBackground.gameObject, root.transform);
            background.name = "BG";
            GameObject window = GameObject.Instantiate(
                sourceWindow.gameObject, root.transform);
            window.name = "Window";

            Transform titleTransform = window.transform.Find("Top/Text");
            Text titleText = titleTransform != null
                ? titleTransform.GetComponent<Text>()
                : null;
            Transform bottom = window.transform.Find("Bottom");
            if (titleText == null || bottom == null || captions == null ||
                captions.Length == 0 || captions.Length > 8) {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            SortingWindowHandle handle = new SortingWindowHandle {
                Root = root,
            };
            RectTransform previousButtonRect = null;
            RectTransform lastButtonRect = null;
            GameObject lastButtonObject = null;
            for (int index = 0; index < 6; index++) {
                string buttonName = index == 0
                    ? "GenericButtonOutline"
                    : "GenericButtonOutline (" + index + ")";
                Transform buttonTransform = bottom.Find(buttonName);
                GenericButtonOutline button = buttonTransform != null
                    ? buttonTransform.GetComponent<GenericButtonOutline>()
                    : null;
                RectTransform buttonRect = buttonTransform != null
                    ? buttonTransform.GetComponent<RectTransform>()
                    : null;
                if (button == null || buttonRect == null) {
                    DestroySortingWindow(handle);
                    return null;
                }

                bool active = index < captions.Length;
                buttonTransform.gameObject.SetActive(active);
                if (active)
                    handle.Buttons.Add(button);
                if (index == 4)
                    previousButtonRect = buttonRect;
                else if (index == 5) {
                    lastButtonRect = buttonRect;
                    lastButtonObject = buttonTransform.gameObject;
                }
            }

            if (captions.Length > 6) {
                if (previousButtonRect == null || lastButtonRect == null ||
                    lastButtonObject == null) {
                    DestroySortingWindow(handle);
                    return null;
                }

                Vector2 step = lastButtonRect.anchoredPosition -
                    previousButtonRect.anchoredPosition;
                if (Mathf.Abs(step.x) < 0.1f &&
                    Mathf.Abs(step.y) < 0.1f)
                    step = new Vector2(0f,
                        -Mathf.Max(1f, lastButtonRect.rect.height));

                for (int index = 6; index < captions.Length; index++) {
                    GameObject clone = GameObject.Instantiate(
                        lastButtonObject, bottom);
                    clone.name = "GenericButtonOutline (" + index + ")";
                    RectTransform cloneRect =
                        clone.GetComponent<RectTransform>();
                    GenericButtonOutline button =
                        clone.GetComponent<GenericButtonOutline>();
                    if (cloneRect == null || button == null) {
                        UnityEngine.Object.Destroy(clone);
                        DestroySortingWindow(handle);
                        return null;
                    }
                    cloneRect.anchoredPosition =
                        lastButtonRect.anchoredPosition +
                        step * (index - 5);
                    clone.SetActive(true);
                    handle.Buttons.Add(button);
                }
            }

            handle.MouseHoverAction =
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>(
                    new Action<int>(hoveredIndex =>
                        SelectSortingButton(handle, hoveredIndex)));

            root.transform.SetAsLastSibling();
            root.SetActive(true);
            titleText.text = title ?? string.Empty;
            for (int index = 0; index < handle.Buttons.Count; index++) {
                GenericButtonOutline button = handle.Buttons[index];
                int selectedIndex = index;
                UnityEventUtility.RemoveAllListeners(button);
                UnityAction action = onSelected != null
                    ? DelegateSupport.ConvertDelegate<UnityAction>(
                        new Action(() => onSelected(selectedIndex)))
                    : null;
                button.Y = index;
                button.PlaySounds = true;
                button.Setup(action, captions[index] ?? string.Empty, false, false);
                button.OnMouseHover = handle.MouseHoverAction;
                button.SetDisabled(false, true);
                button.Deselect();
            }
            SelectSortingButton(handle, 0);
            return handle;
        }

        private static void SelectSortingButton(SortingWindowHandle handle,
            int selectedIndex)
        {
            if (handle == null || selectedIndex < 0 ||
                selectedIndex >= handle.Buttons.Count)
                return;

            for (int index = 0; index < handle.Buttons.Count; index++) {
                GenericButtonOutline button = handle.Buttons[index];
                if (button == null)
                    continue;
                if (index == selectedIndex)
                    button.Select();
                else
                    button.Deselect();
            }
        }

        public static void DestroySortingWindow(SortingWindowHandle handle)
        {
            if (handle == null)
                return;

            for (int index = 0; index < handle.Buttons.Count; index++) {
                GenericButtonOutline button = handle.Buttons[index];
                if (button == null)
                    continue;
                button.OnMouseHover = null;
                UnityEventUtility.RemoveAllListeners(button);
            }
            handle.MouseHoverAction = null;
            handle.Buttons.Clear();
            if (handle.Root != null) {
                handle.Root.SetActive(false);
                UnityEngine.Object.Destroy(handle.Root);
            }
            handle.Root = null;
        }

        public static ModsWindowHandle CreateModsWindow(Transform parent,
            string windowName, string cardsPageName,
            string settingsPageName, Color accentColor,
            Color secondaryTextColor)
        {
            GameObject inputShield = CreateUiObject(
                windowName + ".InputShield", parent);
            Stretch(inputShield.GetComponent<RectTransform>(),
                0f, 0f, 0f, 0f);
            Image shieldImage = inputShield.AddComponent<Image>();
            shieldImage.color = new Color(0f, 0f, 0f, 0.001f);
            shieldImage.raycastTarget = true;
            CanvasGroup shieldGroup = inputShield.AddComponent<CanvasGroup>();
            shieldGroup.alpha = 1f;
            shieldGroup.interactable = true;
            shieldGroup.blocksRaycasts = true;
            shieldGroup.ignoreParentGroups = true;
            inputShield.SetActive(false);

            GameObject root = CreateWindow(windowName, parent);
            CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
            rootGroup.ignoreParentGroups = true;
            root.SetActive(false);

            float hintLineHeight = Mathf.Max(1f, nativeHintHeight);
            float hintLineSpacing = 2f;
            float hintFooterHeight = hintLineHeight * 2f +
                hintLineSpacing;

            GameObject cardsPage = CreateUiObject(cardsPageName,
                root.transform);
            Stretch(cardsPage.GetComponent<RectTransform>(),
                0f, 0f, 0f, 0f);
            RectTransform cardsRoot = CreateCardsRoot(
                cardsPage.transform, "CardsRoot");

            Text pageIndicator = CreateText(cardsPage.transform,
                "PageIndicator", string.Empty,
                Mathf.Max(11, BaseFontSize - 5),
                TextAnchor.MiddleRight, secondaryTextColor);
            RectTransform pageRect = pageIndicator.rectTransform;
            pageRect.anchorMin = new Vector2(1f, 0f);
            pageRect.anchorMax = new Vector2(1f, 0f);
            pageRect.pivot = new Vector2(1f, 1f);
            pageRect.anchoredPosition = new Vector2(0f, -8f);
            pageRect.sizeDelta = new Vector2(180f, 24f);

            RectTransform cardsFooterRect = CreateWindowHintFooter(
                cardsPage.transform, "CMS21UIPlus.CardsFooter",
                12f, pageRect.sizeDelta.x + 22f, 12f,
                hintFooterHeight);
            cardsFooterRect.gameObject.SetActive(false);

            GameObject settingsPage = CreateUiObject(settingsPageName,
                root.transform);
            Stretch(settingsPage.GetComponent<RectTransform>(),
                0f, 0f, 0f, 0f);

            GameObject frame = CreatePanel(settingsPage.transform,
                "SettingsFrame",
                new Color(0.055f, 0.058f, 0.064f, 0.94f),
                new Color(0.72f, 0.72f, 0.72f, 0.92f));
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            // Keep the native tutorial-detail proportions, but lower the
            // upper edge so the window remains below the profile name.
            frameRect.anchorMin = new Vector2(0.285f, 0.115f);
            frameRect.anchorMax = new Vector2(0.985f, 0.83f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            Text header = CreateText(frame.transform, "Header",
                string.Empty, BaseFontSize + 2,
                TextAnchor.MiddleCenter, accentColor);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 52f);

            RectTransform viewport = CreateMaskedViewport(
                frame.transform, "Viewport");
            Stretch(viewport, 12f, 16f, -12f, -58f);
            RectTransform content = CreateVerticalContent(
                viewport, "Content", 4f);
            ScrollRect scroll = CreateVerticalScroll(
                frame, viewport, content);

            RectTransform footerRect = CreateWindowHintFooterBelowFrame(
                settingsPage.transform, frameRect, "Footer",
                0f, 0f, 6f, hintLineHeight);

            Text status = CreateText(footerRect, "Status",
                string.Empty, Mathf.Max(10, BaseFontSize - 6),
                TextAnchor.MiddleLeft, secondaryTextColor);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            settingsPage.SetActive(false);
            return new ModsWindowHandle {
                InputShield = inputShield,
                Root = root,
                CardsPage = cardsPage,
                CardsRoot = cardsRoot,
                PageIndicator = pageIndicator,
                SettingsPage = settingsPage,
                SettingsContent = content,
                SettingsScroll = scroll,
                SettingsHeader = header,
                SettingsStatus = status,
                CardsFooterRoot = cardsFooterRect,
                FooterRoot = footerRect,
            };
        }

        private static RectTransform CreateWindowHintFooter(
            Transform parent, string name, float left, float right,
            float bottom, float height)
        {
            GameObject footer = CreateUiObject(name, parent);
            RectTransform rect = footer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom +
                Mathf.Max(1f, height));
            rect.localScale = Vector3.one;
            return rect;
        }

        private static RectTransform CreateWindowHintFooterBelowFrame(
            Transform parent, RectTransform frame, string name,
            float left, float right, float gap, float height)
        {
            if (parent == null || frame == null)
                return null;

            GameObject footer = CreateUiObject(name, parent);
            RectTransform rect = footer.GetComponent<RectTransform>();
            float bottomAnchor = frame.anchorMin.y;
            rect.anchorMin = new Vector2(frame.anchorMin.x, bottomAnchor);
            rect.anchorMax = new Vector2(frame.anchorMax.x, bottomAnchor);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left,
                -Mathf.Max(0f, gap) - Mathf.Max(1f, height));
            rect.offsetMax = new Vector2(-right, -Mathf.Max(0f, gap));
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return rect;
        }


        public static RectTransform CreateCardsRoot(Transform parent,
            string name)
        {
            GameObject root = CreateUiObject(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            CardsRect.Apply(rect);
            return rect;
        }

        public static ModCardHandle CreateModCard(Transform parent,
            string title, bool supported, Action onClick)
        {
            GameObject root = CreateUiObject("ModCard", parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = nativeCardSize;

            Image background = root.AddComponent<Image>();
            CardBackground.Apply(background);
            background.color = cardNormalColor;
            background.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.interactable = supported;
            if (supported && onClick != null) {
                button.onClick.AddListener(
                    DelegateSupport.ConvertDelegate<UnityAction>(
                        new Action(onClick)));
            }

            GameObject iconObject = CreateUiObject("Icon", root.transform);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            CardIconRect.Apply(iconRect);
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -6f);
            iconRect.sizeDelta = new Vector2(46f, 40f);
            Image icon = iconObject.AddComponent<Image>();
            CardIcon.Apply(icon);
            if (modCardIcon != null)
                icon.sprite = modCardIcon;
            icon.raycastTarget = false;

            GameObject titleObject = CreateUiObject("Title", root.transform);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            CardTitleRect.Apply(titleRect);
            Text titleText = titleObject.AddComponent<Text>();
            CardText.Apply(titleText);
            titleText.text = title;
            titleText.fontSize = CardText.FontSize;
            titleText.resizeTextForBestFit = false;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.raycastTarget = false;

            Text unsupportedText = null;
            if (!supported) {
                // Native cards reserve the full lower band for the title.
                // Compact unsupported titles to fit one status line.
                titleRect.anchorMin = new Vector2(0.04f, 0.18f);
                titleRect.anchorMax = new Vector2(0.96f, 0.42f);
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
                titleText.fontSize = Mathf.Min(titleText.fontSize, 11);

                GameObject warningObject = CreateUiObject(
                    "UnsupportedSettings", root.transform);
                RectTransform warningRect =
                    warningObject.GetComponent<RectTransform>();
                warningRect.anchorMin = new Vector2(0.04f, 0.02f);
                warningRect.anchorMax = new Vector2(0.96f, 0.20f);
                warningRect.offsetMin = Vector2.zero;
                warningRect.offsetMax = Vector2.zero;
                unsupportedText = warningObject.AddComponent<Text>();
                CardText.Apply(unsupportedText);
                unsupportedText.text = ModLocalization.Get(
                    "LOC_UiSettingsNotSupported");
                unsupportedText.fontSize = Mathf.Max(7,
                    titleText.fontSize / 2);
                unsupportedText.resizeTextForBestFit = true;
                unsupportedText.resizeTextMinSize = 6;
                unsupportedText.resizeTextMaxSize =
                    Mathf.Max(7, titleText.fontSize / 2);
                unsupportedText.alignment = TextAnchor.MiddleCenter;
                unsupportedText.color = new Color(1f, 0.73f, 0.08f, 1f);
                unsupportedText.raycastTarget = false;
            }

            GameObject selected = CreateUiObject(
                "Selected", root.transform);
            RectTransform selectedRect =
                selected.GetComponent<RectTransform>();
            CardSelectedRect.Apply(selectedRect);
            Image selectedImage = selected.AddComponent<Image>();
            CardSelected.Apply(selectedImage);
            selectedImage.raycastTarget = false;
            selected.SetActive(false);
            selected.transform.SetAsLastSibling();

            return new ModCardHandle {
                Root = root,
                Button = button,
                Background = background,
                FocusFrame = selected,
                Icon = icon,
                Title = titleText,
                UnsupportedText = unsupportedText,
            };
        }

        public static void SetModCardSelected(
            ModCardHandle card, bool selected)
        {
            if (card == null || card.FocusFrame == null)
                return;
            card.FocusFrame.SetActive(selected);
        }


        public static SettingsRowHandle CreateSettingsRow(Transform parent,
            string label, string valueText, bool showValueIndicator,
            bool indicatorValue, Action onClick)
        {
            SettingsRowHandle native = CreateNativeSettingsRow(parent,
                label, valueText, showValueIndicator, indicatorValue,
                onClick);
            if (native != null)
                return native;
            return CreateFallbackSettingsRow(parent, label, valueText,
                showValueIndicator, indicatorValue, onClick);
        }

        private static SettingsRowHandle CreateNativeSettingsRow(
            Transform parent, string label, string valueText,
            bool showValueIndicator, bool indicatorValue, Action onClick)
        {
            if (settingsSource == null ||
                settingsSource.gameObject == null || parent == null)
                return null;

            GameObject root = GameObject.Instantiate(
                settingsSource.gameObject, parent);
            root.name = "SettingRow";
            root.SetActive(false);

            RectTransform rect = root.GetComponent<RectTransform>();
            if (rect == null) {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(nativeSettingsRowSize.x,
                Mathf.Max(32f, nativeSettingsRowSize.y));

            SettingsButton nativeButton = root.GetComponent<SettingsButton>();
            Text labelText = nativeButton != null
                ? nativeButton.text : FindFirstText(root.transform, null);
            Transform hoverTransform = nativeButton != null &&
                nativeButton.hover != null
                    ? nativeButton.hover.transform : null;
            Transform selectedTransform = nativeButton != null &&
                nativeButton.selected != null
                    ? nativeButton.selected.transform : null;
            GameObject hoverVisual = hoverTransform != null
                ? hoverTransform.gameObject : null;
            GameObject selectedVisual = selectedTransform != null
                ? selectedTransform.gameObject : null;
            Text stateText = FindSettingsValueText(root.transform,
                labelText, hoverTransform, selectedTransform);

            if (nativeButton != null)
                nativeButton.enabled = false;

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }

            Image background = root.GetComponent<Image>();
            if (background == null) {
                background = FindFirstImage(root.transform, null,
                    new string[] { "hover", "selected" });
            }
            if (background == null) {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            background.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            if (button == null)
                button = root.AddComponent<Button>();
            else
                button.onClick.RemoveAllListeners();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
            if (onClick != null) {
                button.onClick.AddListener(
                    DelegateSupport.ConvertDelegate<UnityAction>(
                        new Action(onClick)));
            }

            if (labelText != null) {
                labelText.text = label;
                labelText.raycastTarget = false;
            }
            if (stateText == null)
                stateText = CloneSettingsStateText(root.transform,
                    labelText);
            HideUnusedSettingsTexts(root.transform, labelText, stateText,
                hoverTransform, selectedTransform);

            Image valueIndicatorImage;
            GameObject valueIndicator = CloneSettingsValueIndicator(
                root.transform, selectedVisual, out valueIndicatorImage);
            RectTransform leftArrowRect;
            Graphic[] leftArrowGraphics;
            GameObject leftArrow = CreateSettingsArrow(root.transform,
                "LeftArrow", true, -126f, out leftArrowRect,
                out leftArrowGraphics);
            RectTransform rightArrowRect;
            Graphic[] rightArrowGraphics;
            GameObject rightArrow = CreateSettingsArrow(root.transform,
                "RightArrow", false, -18f, out rightArrowRect,
                out rightArrowGraphics);
            Text applyWarning = CreateSettingsApplyWarning(
                root.transform, labelText);

            SettingsRowHandle handle = new SettingsRowHandle {
                Root = root,
                Button = button,
                Background = background,
                HoverVisual = hoverVisual,
                SelectedVisual = selectedVisual,
                ValueIndicator = valueIndicator,
                ValueIndicatorImage = valueIndicatorImage,
                LeftArrow = leftArrow,
                LeftArrowRect = leftArrowRect,
                LeftArrowGraphics = leftArrowGraphics,
                RightArrow = rightArrow,
                RightArrowRect = rightArrowRect,
                RightArrowGraphics = rightArrowGraphics,
                Label = labelText,
                State = stateText,
                ApplyWarning = applyWarning,
            };
            CaptureSettingsStateLayout(handle);
            UpdateSettingsRow(handle, valueText, showValueIndicator,
                indicatorValue);
            SetSettingsRowVisualState(handle, false, false);
            root.SetActive(true);
            return handle;
        }

        private static SettingsRowHandle CreateFallbackSettingsRow(
            Transform parent, string label, string valueText,
            bool showValueIndicator, bool indicatorValue, Action onClick)
        {
            GameObject root = CreateUiObject("SettingRow", parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(nativeSettingsRowSize.x,
                Mathf.Max(32f, nativeSettingsRowSize.y));

            Image background = root.AddComponent<Image>();
            SettingsBackground.Apply(background);
            background.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            if (onClick != null) {
                button.onClick.AddListener(
                    DelegateSupport.ConvertDelegate<UnityAction>(
                        new Action(onClick)));
            }

            GameObject labelObject = CreateUiObject("Label", root.transform);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, 12f, 2f, -120f, -2f);
            Text labelText = labelObject.AddComponent<Text>();
            SettingsText.Apply(labelText);
            labelText.text = label;
            labelText.fontSize = Mathf.Max(12, SettingsText.FontSize);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.raycastTarget = false;

            Text stateText = CloneSettingsStateText(root.transform,
                labelText);
            Image valueIndicatorImage;
            GameObject valueIndicator = CloneSettingsValueIndicator(
                root.transform, null, out valueIndicatorImage);
            RectTransform leftArrowRect;
            Graphic[] leftArrowGraphics;
            GameObject leftArrow = CreateSettingsArrow(root.transform,
                "LeftArrow", true, -126f, out leftArrowRect,
                out leftArrowGraphics);
            RectTransform rightArrowRect;
            Graphic[] rightArrowGraphics;
            GameObject rightArrow = CreateSettingsArrow(root.transform,
                "RightArrow", false, -18f, out rightArrowRect,
                out rightArrowGraphics);
            Text applyWarning = CreateSettingsApplyWarning(
                root.transform, labelText);
            SettingsRowHandle handle = new SettingsRowHandle {
                Root = root,
                Button = button,
                Background = background,
                ValueIndicator = valueIndicator,
                ValueIndicatorImage = valueIndicatorImage,
                LeftArrow = leftArrow,
                LeftArrowRect = leftArrowRect,
                LeftArrowGraphics = leftArrowGraphics,
                RightArrow = rightArrow,
                RightArrowRect = rightArrowRect,
                RightArrowGraphics = rightArrowGraphics,
                Label = labelText,
                State = stateText,
                ApplyWarning = applyWarning,
            };
            CaptureSettingsStateLayout(handle);
            UpdateSettingsRow(handle, valueText, showValueIndicator,
                indicatorValue);
            return handle;
        }

        public static void UpdateSettingsRow(SettingsRowHandle handle,
            string valueText, bool showValueIndicator, bool indicatorValue)
        {
            if (handle == null)
                return;
            if (handle.ValueIndicator != null)
                handle.ValueIndicator.SetActive(showValueIndicator);
            if (handle.ValueIndicatorImage != null) {
                Color indicatorColor = indicatorValue
                    ? SettingsEnabledColor : SettingsDisabledColor;
                handle.ValueIndicatorImage.color = indicatorColor;
                if (handle.ValueIndicatorImage.canvasRenderer != null)
                    handle.ValueIndicatorImage.canvasRenderer.SetColor(
                        indicatorColor);
                handle.ValueIndicatorImage.raycastTarget = false;
            }
            if (handle.State != null) {
                handle.State.text = valueText ?? string.Empty;
                handle.State.color = Color.white;
                if (handle.State.canvasRenderer != null)
                    handle.State.canvasRenderer.SetColor(Color.white);
                handle.State.raycastTarget = false;
            }
        }

        public static void SetSettingsRowVisualState(
            SettingsRowHandle handle, bool hovered, bool selected)
        {
            if (handle == null)
                return;
            bool focused = hovered || selected;
            if (handle.HoverVisual != null)
                handle.HoverVisual.SetActive(focused);
            else if (handle.Background != null)
                handle.Background.color = focused
                    ? settingsHoverColor : settingsNormalColor;
            // Selection is represented by the native hover background.
            // The native orange marker is reserved as the template for the
            // persistent true/false value indicator.
            if (handle.SelectedVisual != null)
                handle.SelectedVisual.SetActive(false);
        }

        public static void SetSettingsRowEditing(SettingsRowHandle handle,
            bool editing, bool showLeftArrow, bool showRightArrow,
            bool leftHovered, bool rightHovered)
        {
            if (handle == null)
                return;

            if (handle.LeftArrow != null)
                handle.LeftArrow.SetActive(editing && showLeftArrow);
            if (handle.RightArrow != null)
                handle.RightArrow.SetActive(editing && showRightArrow);

            SetSettingsArrowColor(handle.LeftArrowGraphics,
                leftHovered ? GetSettingsAccentColor() : Color.white);
            SetSettingsArrowColor(handle.RightArrowGraphics,
                rightHovered ? GetSettingsAccentColor() : Color.white);

            if (handle.State == null)
                return;
            RectTransform stateRect = handle.State.rectTransform;
            if (editing && (showLeftArrow || showRightArrow)) {
                stateRect.anchorMin = new Vector2(1f, 0.5f);
                stateRect.anchorMax = new Vector2(1f, 0.5f);
                stateRect.pivot = new Vector2(0.5f, 0.5f);
                stateRect.anchoredPosition = new Vector2(-63f, 0f);
                stateRect.sizeDelta = new Vector2(96f,
                    Mathf.Max(0f, handle.StateSizeDelta.y));
                handle.State.alignment = TextAnchor.MiddleCenter;
            } else {
                stateRect.anchorMin = handle.StateAnchorMin;
                stateRect.anchorMax = handle.StateAnchorMax;
                stateRect.pivot = handle.StatePivot;
                stateRect.anchoredPosition = handle.StateAnchoredPosition;
                stateRect.sizeDelta = handle.StateSizeDelta;
                handle.State.alignment = handle.StateAlignment;
            }
        }

        public static void SetSettingsRowWarning(
            SettingsRowHandle handle, string text, bool yellow = false)
        {
            if (handle == null || handle.ApplyWarning == null)
                return;

            Text warning = handle.ApplyWarning;
            warning.text = text ?? string.Empty;
            Color color = yellow ? Color.yellow : SettingsSelected.Color;
            color.a = 1f;
            warning.color = color;
            if (warning.canvasRenderer != null)
                warning.canvasRenderer.SetColor(color);
            warning.gameObject.SetActive(!string.IsNullOrEmpty(warning.text));
        }

        private static Text CreateSettingsApplyWarning(Transform parent,
            Text labelSource)
        {
            GameObject warningObject = CreateUiObject(
                "ApplyWarning", parent);
            RectTransform warningRect =
                warningObject.GetComponent<RectTransform>();
            warningRect.anchorMin = new Vector2(0.48f, 0f);
            warningRect.anchorMax = new Vector2(1f, 1f);
            warningRect.pivot = new Vector2(1f, 0.5f);
            warningRect.offsetMin = Vector2.zero;
            warningRect.offsetMax = new Vector2(-122f, 0f);
            warningRect.localScale = Vector3.one;

            Text warning = warningObject.AddComponent<Text>();
            SettingsText.Apply(warning);
            int sourceFontSize = labelSource != null
                ? labelSource.fontSize : SettingsText.FontSize;
            warning.fontSize = Mathf.Max(8,
                Mathf.RoundToInt(sourceFontSize / 1.5f));
            warning.fontStyle = FontStyle.Normal;
            warning.alignment = TextAnchor.MiddleRight;
            warning.resizeTextForBestFit = false;
            warning.horizontalOverflow = HorizontalWrapMode.Wrap;
            warning.verticalOverflow = VerticalWrapMode.Truncate;
            warning.raycastTarget = false;
            warningObject.SetActive(false);
            return warning;
        }

        private static void CaptureSettingsStateLayout(
            SettingsRowHandle handle)
        {
            if (handle == null || handle.State == null)
                return;
            RectTransform rect = handle.State.rectTransform;
            handle.StateAnchorMin = rect.anchorMin;
            handle.StateAnchorMax = rect.anchorMax;
            handle.StatePivot = rect.pivot;
            handle.StateAnchoredPosition = rect.anchoredPosition;
            handle.StateSizeDelta = rect.sizeDelta;
            handle.StateAlignment = handle.State.alignment;
        }

        private static GameObject CreateSettingsArrow(Transform rowRoot,
            string name, bool pointsLeft, float anchoredX,
            out RectTransform rect, out Graphic[] graphics)
        {
            GameObject arrow = CreateUiObject(name, rowRoot);
            rect = arrow.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(anchoredX, 0f);
            // Keep a comfortable pointer hit area while the visual itself
            // matches the x-height of the native settings text.
            rect.sizeDelta = new Vector2(24f, 28f);
            rect.localScale = Vector3.one;

            Image nativeArrow = CreateNativeSettingsArrowImage(
                arrow.transform, pointsLeft);
            if (nativeArrow != null) {
                graphics = new Graphic[] { nativeArrow };
            } else {
                Image upper = CreateSettingsArrowBar(arrow.transform,
                    "Upper", new Vector2(0f, 3f),
                    pointsLeft ? -45f : 45f);
                Image lower = CreateSettingsArrowBar(arrow.transform,
                    "Lower", new Vector2(0f, -3f),
                    pointsLeft ? 45f : -45f);
                graphics = new Graphic[] { upper, lower };
            }
            arrow.SetActive(false);
            return arrow;
        }

        private static Image CreateNativeSettingsArrowImage(
            Transform parent, bool pointsLeft)
        {
            SettingsArrowStyle style = pointsLeft
                ? SettingsLeftArrow : SettingsRightArrow;
            bool mirrored = false;
            if (!style.IsValid) {
                style = pointsLeft
                    ? SettingsRightArrow : SettingsLeftArrow;
                mirrored = style.IsValid;
            }
            if (!style.IsValid || style.Image.Sprite == null)
                return null;

            GameObject visual = CreateUiObject("Visual", parent);
            RectTransform rect = visual.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            float height = Mathf.Clamp(SettingsText.FontSize * 0.72f,
                10f, 14f);
            Sprite sprite = style.Image.Sprite;
            float aspect = sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height : 1f;
            float width = Mathf.Clamp(height * aspect, height * 0.55f,
                height * 1.35f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = style.LocalRotation;
            Vector3 scale = style.LocalScale;
            if (mirrored)
                scale.x *= -1f;
            rect.localScale = scale;

            Image image = visual.AddComponent<Image>();
            style.Image.Apply(image);
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateSettingsArrowBar(Transform parent,
            string name, Vector2 position, float rotation)
        {
            GameObject bar = CreateUiObject(name, parent);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(2.5f, 10f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = bar.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static void SetSettingsArrowColor(Graphic[] graphics,
            Color color)
        {
            if (graphics == null)
                return;
            for (int i = 0; i < graphics.Length; i++) {
                Graphic graphic = graphics[i];
                if (graphic == null)
                    continue;
                graphic.color = color;
                if (graphic.canvasRenderer != null)
                    graphic.canvasRenderer.SetColor(color);
            }
        }

        private static Color GetSettingsAccentColor()
        {
            Color color = SettingsSelected.Color;
            color.a = 1f;
            return color;
        }

        private static GameObject CloneSettingsValueIndicator(
            Transform rowRoot, GameObject source, out Image image)
        {
            image = null;
            GameObject indicator = null;
            if (source != null && source.transform != null) {
                Transform indicatorParent = source.transform.parent != null
                    ? source.transform.parent : rowRoot;
                indicator = GameObject.Instantiate(source, indicatorParent);
                indicator.name = "ValueIndicator";
                indicator.transform.SetSiblingIndex(
                    source.transform.GetSiblingIndex());
                Text[] clonedTexts =
                    indicator.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < clonedTexts.Length; i++) {
                    if (clonedTexts[i] != null)
                        clonedTexts[i].gameObject.SetActive(false);
                }
                image = FindFirstImage(indicator.transform, null, null);
            }

            if (indicator == null || image == null) {
                if (indicator != null)
                    UnityEngine.Object.Destroy(indicator);
                indicator = CreateUiObject("ValueIndicator", rowRoot);
                RectTransform indicatorRect =
                    indicator.GetComponent<RectTransform>();
                indicatorRect.anchorMin = new Vector2(0f, 0f);
                indicatorRect.anchorMax = new Vector2(0f, 1f);
                indicatorRect.pivot = new Vector2(0f, 0.5f);
                indicatorRect.anchoredPosition = Vector2.zero;
                indicatorRect.sizeDelta = new Vector2(5f, 0f);
                indicatorRect.localScale = Vector3.one;
                image = indicator.AddComponent<Image>();
                SettingsSelected.Apply(image);
            }

            Graphic[] graphics =
                indicator.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
            indicator.SetActive(true);
            return indicator;
        }

        private static Text FindSettingsValueText(Transform root,
            Text label, Transform hover, Transform selected)
        {
            if (root == null)
                return null;

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            Text best = null;
            float bestScore = float.MinValue;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            for (int i = 0; i < texts.Length; i++) {
                Text candidate = texts[i];
                if (candidate == null || candidate == label ||
                    IsInside(candidate.transform, hover) ||
                    IsInside(candidate.transform, selected))
                    continue;

                float score = 0f;
                bool likelyValue = false;
                string name = candidate.gameObject.name ?? string.Empty;
                if (name.IndexOf("value",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("state",
                    StringComparison.OrdinalIgnoreCase) >= 0) {
                    score += 200f;
                    likelyValue = true;
                }
                if (candidate.alignment == TextAnchor.UpperRight ||
                    candidate.alignment == TextAnchor.MiddleRight ||
                    candidate.alignment == TextAnchor.LowerRight) {
                    score += 100f;
                    likelyValue = true;
                }

                RectTransform candidateRect = candidate.rectTransform;
                if (candidateRect != null && rootRect != null) {
                    Vector3 center = candidateRect.TransformPoint(
                        candidateRect.rect.center);
                    float localX = rootRect.InverseTransformPoint(center).x;
                    score += localX;
                    if (localX > rootRect.rect.width * 0.12f)
                        likelyValue = true;
                }
                if (!likelyValue)
                    continue;
                if (!string.IsNullOrEmpty(candidate.text))
                    score += 10f;

                if (score > bestScore) {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private static void HideUnusedSettingsTexts(Transform root,
            Text label, Text state, Transform hover, Transform selected)
        {
            if (root == null)
                return;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) {
                Text candidate = texts[i];
                if (candidate == null || candidate == label ||
                    candidate == state ||
                    IsInside(candidate.transform, hover) ||
                    IsInside(candidate.transform, selected))
                    continue;
                candidate.gameObject.SetActive(false);
            }
        }

        private static bool IsInside(Transform candidate,
            Transform possibleAncestor)
        {
            return candidate != null && possibleAncestor != null &&
                (candidate == possibleAncestor ||
                    candidate.IsChildOf(possibleAncestor));
        }

        private static Text CloneSettingsStateText(Transform parent,
            Text source)
        {
            GameObject stateObject;
            Text stateText;
            if (source != null) {
                stateObject = GameObject.Instantiate(
                    source.gameObject, parent);
                stateObject.name = "State";
                stateText = stateObject.GetComponent<Text>();
            }
            else {
                stateObject = CreateUiObject("State", parent);
                stateText = stateObject.AddComponent<Text>();
                SettingsText.Apply(stateText);
            }

            RectTransform stateRect = stateObject.GetComponent<RectTransform>();
            stateRect.anchorMin = new Vector2(1f, 0f);
            stateRect.anchorMax = new Vector2(1f, 1f);
            stateRect.pivot = new Vector2(1f, 0.5f);
            stateRect.anchoredPosition = new Vector2(-14f, 0f);
            stateRect.sizeDelta = new Vector2(96f, 0f);
            stateRect.localScale = Vector3.one;
            stateText.alignment = TextAnchor.MiddleRight;
            stateText.raycastTarget = false;
            return stateText;
        }

        public static Text CreateSectionHeader(Transform parent,
            string text)
        {
            GameObject root = CreateUiObject("SectionHeader", parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(nativeSettingsRowSize.x, 32f);

            GameObject textObject = CreateUiObject("Text", root.transform);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            Stretch(textRect, 12f, 0f, -12f, 0f);
            Text label = textObject.AddComponent<Text>();
            SettingsText.Apply(label);
            label.text = text;
            label.fontSize = Mathf.Max(12, SettingsText.FontSize - 1);
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.76f, 0.10f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        public static FooterHintHandle CreateFooterHint(Transform parent,
            string key, string label, Action action)
        {
            return CreateFooterHint(parent, new string[] { key }, label,
                action);
        }

        public static FooterHintHandle CreateFooterHint(Transform parent,
            string[] keys, string label, Action action)
        {
            if (parent == null)
                return null;

            HintStyleProfile style = ResolveHintStyleProfile(parent);
            GameObject root = CreateUiObject("Hint_" +
                (keys != null && keys.Length > 0 ? keys[0] : "Action"),
                parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            Image hitTarget = root.AddComponent<Image>();
            hitTarget.color = new Color(0f, 0f, 0f, 0.001f);
            hitTarget.raycastTarget = true;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.ignoreParentGroups = true;

            HorizontalLayoutGroup layout =
                root.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = style.Spacing;
            layout.padding = new RectOffset();

            ContentSizeFitter fitter = root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            FooterHintHandle handle = new FooterHintHandle {
                Root = root,
                Rect = rect,
                ContentRect = rect,
                CanvasGroup = canvasGroup,
                Height = style.Height,
                KeySize = style.KeySize,
                NormalColor = style.TextColor,
                Keys = keys,
                Text = label ?? string.Empty,
                Enabled = true,
                IsStyledFooter = true,
            };

            if (keys == null || keys.Length == 0)
                keys = new string[] { string.Empty };
            for (int i = 0; i < keys.Length; i++) {
                GameObject keyObject = CreateUiObject(
                    "Key" + i, root.transform);
                RectTransform keyRect =
                    keyObject.GetComponent<RectTransform>();
                Sprite keySprite = FindPcButtonSprite(keys[i]);
                Vector2 keySize = GetHintKeySize(style, keySprite);
                keyRect.sizeDelta = keySize;
                Image keyImage = keyObject.AddComponent<Image>();
                style.KeyImage.Apply(keyImage);
                if (keySprite != null)
                    keyImage.sprite = keySprite;
                keyImage.color = style.KeyImage.Color.a > 0.01f
                    ? style.KeyImage.Color : style.TextColor;
                keyImage.preserveAspect = true;
                keyImage.raycastTarget = false;
                handle.KeyImages.Add(keyImage);
            }

            GameObject labelObject = CreateUiObject("Label", root.transform);
            RectTransform labelRect =
                labelObject.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(40f, style.Height);
            Text labelText = labelObject.AddComponent<Text>();
            style.Text.Apply(labelText);
            labelText.fontSize = style.FontSize;
            labelText.resizeTextForBestFit = false;
            labelText.text = handle.Text;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.raycastTarget = false;
            labelRect.sizeDelta = new Vector2(
                Mathf.Max(40f, labelText.preferredWidth + 2f),
                style.Height);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = labelText;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            ColorBlock colors = button.colors;
            colors.normalColor = style.TextColor;
            colors.highlightedColor = HintHoverColor;
            colors.pressedColor = HintHoverColor;
            colors.selectedColor = style.TextColor;
            colors.disabledColor = new Color(style.TextColor.r,
                style.TextColor.g, style.TextColor.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            if (labelText.canvasRenderer != null)
                labelText.canvasRenderer.SetColor(style.TextColor);
            if (action != null) {
                UnityAction unityAction =
                    DelegateSupport.ConvertDelegate<UnityAction>(
                        new Action(action));
                button.onClick.AddListener(unityAction);
                handle.Action = unityAction;
            }

            handle.Button = button;
            handle.Label = labelText;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float width = LayoutUtility.GetPreferredWidth(rect);
            if (width < 30f) {
                width = labelRect.sizeDelta.x;
                for (int i = 0; i < handle.KeyImages.Count; i++)
                    width += handle.KeyImages[i].rectTransform.sizeDelta.x +
                        style.Spacing;
            }
            rect.sizeDelta = new Vector2(width, style.Height);
            handle.Width = width;
            handle.Height = style.Height;
            return handle;
        }

        public static FooterHintHandle CreateFooterHint(Transform parent,
            ControlDescription source, string[] keys, string label,
            Action action)
        {
            ControlHintRowHandle row = CreateControlHintRow(
                new ControlHintRowOptions {
                    Parent = parent,
                    Source = source,
                    Name = "Hint_Row",
                    Spacing = 0f,
                    WrapItems = false,
                    Hints = new ControlHintSpec[] {
                        new ControlHintSpec {
                            Name = "Hint_" +
                                (keys != null && keys.Length > 0
                                    ? keys[0] : "Action"),
                            Keys = keys,
                            Label = label,
                            Action = action,
                        },
                    },
                });
            return row != null && row.Hints.Count > 0
                ? row.Hints[0] : null;
        }

        internal static ControlHintRowHandle CreateControlHintRow(
            ControlHintRowOptions options)
        {
            if (options == null || options.Parent == null ||
                options.Hints == null || options.Hints.Length == 0)
                return null;

            ControlDescription defaultSource = options.Source;
            if (defaultSource == null) {
                string selectionReason;
                defaultSource = FindBestHintSource(options.Parent,
                    out selectionReason);
                if (defaultSource == null) {
                    if (hintSource == null)
                        CaptureHintStyles(null);
                    defaultSource = hintSource;
                }
            }
            if (defaultSource == null)
                return null;

            ControlHintRowHandle row = new ControlHintRowHandle {
                Parent = options.Parent,
                Spacing = options.Spacing >= 0f
                    ? options.Spacing
                    : ResolveControlHintRowSpacing(defaultSource),
                LayoutItems = options.LayoutItems,
                WrapItems = options.WrapItems,
                LineSpacing = Mathf.Max(0f, options.LineSpacing),
            };

            for (int i = 0; i < options.Hints.Length; i++) {
                ControlHintSpec spec = options.Hints[i];
                if (spec == null)
                    continue;

                ControlDescription source = spec.Source != null
                    ? spec.Source : defaultSource;
                if (source == null || source.gameObject == null)
                    continue;

                ControlDescription template = spec.Template;
                ControlDescription variant = spec.VariantSource;
                if (template == null) {
                    ControlDescription standard =
                        FindStandardActionDescription(source);
                    template = standard != null ? standard : source;
                    if (variant == null)
                        variant = standard;
                }

                FooterHintHandle handle = CreateControlHint(
                    new ControlHintOptions {
                        Source = source,
                        Template = template,
                        VariantSource = variant,
                        Parent = options.Parent,
                        Name = !string.IsNullOrEmpty(spec.Name)
                            ? spec.Name : options.Name + "_" + i,
                        Keys = spec.Keys,
                        Label = spec.Label,
                        Action = spec.Action,
                        InputHandlingMethod = spec.InputHandlingMethod,
                        CanHold = spec.CanHold,
                        TimeToHold = spec.TimeToHold,
                        OnlyHandleMouseClickInput =
                            spec.OnlyHandleMouseClickInput,
                        NormalizeRect = options.NormalizeItems,
                        SetAsLastSibling = options.SetAsLastSibling,
                    });
                if (handle == null)
                    continue;

                handle.Row = row;
                row.Hints.Add(handle);
                UpdateFooterHint(handle, spec.Label, spec.Enabled);
            }

            RelayoutControlHintRow(row);
            return row;
        }

        internal static ControlHintRowHandle CreateNativeFooterHint(
            ControlDescription source, string name, string[] keys,
            string text, Action action,
            ControlDescription variantSource = null,
            DescriptionInputHandlingMethod inputHandlingMethod =
                DescriptionInputHandlingMethod.ButtonDown,
            bool canHold = false, float timeToHold = 0f,
            bool onlyHandleMouseClickInput = true)
        {
            if (source == null || source.transform == null ||
                source.transform.parent == null)
                return null;

            return CreateControlHintRow(new ControlHintRowOptions {
                Parent = source.transform.parent,
                Source = source,
                Name = name,
                NormalizeItems = false,
                LayoutItems = false,
                WrapItems = false,
                SetAsLastSibling = true,
                Hints = new ControlHintSpec[] {
                    new ControlHintSpec {
                        Source = source,
                        Template = source,
                        VariantSource = variantSource,
                        Name = name,
                        Keys = keys,
                        Label = text,
                        Action = action,
                        InputHandlingMethod = inputHandlingMethod,
                        CanHold = canHold,
                        TimeToHold = timeToHold,
                        OnlyHandleMouseClickInput =
                            onlyHandleMouseClickInput,
                    },
                },
            });
        }

        internal static void RelayoutControlHintRow(
            ControlHintRowHandle row)
        {
            if (row == null || row.Parent == null || !row.LayoutItems)
                return;

            Canvas.ForceUpdateCanvases();
            RectTransform parentRect =
                row.Parent.GetComponent<RectTransform>();
            float originX = parentRect != null
                ? parentRect.rect.xMin : 0f;
            float originY = parentRect != null
                ? parentRect.rect.yMin : 0f;
            float availableWidth = parentRect != null
                ? Mathf.Abs(parentRect.rect.width) : 0f;
            float maximumX = availableWidth > 0f
                ? originX + availableWidth : float.MaxValue;

            float cursorX = originX;
            float cursorY = originY;
            float lineHeight = 0f;
            float furthestRight = originX;
            float furthestTop = originY;
            bool placedInLine = false;

            for (int i = 0; i < row.Hints.Count; i++) {
                FooterHintHandle handle = row.Hints[i];
                if (handle == null || handle.Root == null ||
                    handle.Rect == null || !handle.Root.activeSelf)
                    continue;

                if (handle.NormalizeRect)
                    PrepareNormalizedControlHint(handle, false);

                handle.Rect.anchoredPosition = Vector2.zero;
                LayoutRebuilder.ForceRebuildLayoutImmediate(handle.Rect);

                Bounds bounds;
                bool hasBounds = TryGetControlHintVisualBounds(
                    handle, row.Parent, out bounds);
                float itemWidth = hasBounds
                    ? Mathf.Max(1f, bounds.size.x)
                    : CalculateControlHintWidth(handle.Rect);
                float itemHeight = hasBounds
                    ? Mathf.Max(1f, bounds.size.y)
                    : Mathf.Max(1f, handle.Height);

                if (row.WrapItems && placedInLine &&
                    cursorX + itemWidth > maximumX) {
                    cursorX = originX;
                    cursorY += lineHeight + row.LineSpacing;
                    lineHeight = 0f;
                    placedInLine = false;
                }

                Vector2 position = handle.Rect.anchoredPosition;
                if (hasBounds) {
                    position.x += cursorX - bounds.min.x;
                    position.y += cursorY - bounds.min.y;
                } else if (parentRect != null) {
                    position.x = cursorX - parentRect.rect.xMin;
                    position.y = cursorY - parentRect.rect.yMin;
                } else {
                    position.x = cursorX;
                    position.y = cursorY;
                }
                handle.Rect.anchoredPosition = position;
                LayoutRebuilder.ForceRebuildLayoutImmediate(handle.Rect);

                if (TryGetControlHintVisualBounds(handle, row.Parent,
                        out bounds)) {
                    itemWidth = Mathf.Max(1f, bounds.size.x);
                    itemHeight = Mathf.Max(1f, bounds.size.y);
                    cursorX = bounds.max.x + row.Spacing;
                    lineHeight = Mathf.Max(lineHeight, itemHeight);
                    furthestRight = Mathf.Max(furthestRight,
                        bounds.max.x);
                    furthestTop = Mathf.Max(furthestTop,
                        bounds.max.y);
                } else {
                    cursorX += itemWidth + row.Spacing;
                    lineHeight = Mathf.Max(lineHeight, itemHeight);
                    furthestRight = Mathf.Max(furthestRight,
                        cursorX - row.Spacing);
                    furthestTop = Mathf.Max(furthestTop,
                        cursorY + itemHeight);
                }

                handle.Width = itemWidth;
                handle.Height = itemHeight;
                placedInLine = true;
            }

            row.Width = Mathf.Max(0f, furthestRight - originX);
            row.Height = Mathf.Max(0f, furthestTop - originY);
        }

        private static float ResolveControlHintRowSpacing(
            ControlDescription source)
        {
            Transform parent = source != null
                ? source.transform.parent : null;
            if (parent == null)
                return 10f;

            ControlDescription[] descriptions =
                parent.GetComponentsInChildren<ControlDescription>(true);
            float smallestGap = float.MaxValue;
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription left = descriptions[i];
                if (left == null || left.transform.parent != parent ||
                    left.gameObject == null || !left.gameObject.activeSelf)
                    continue;

                Bounds leftBounds;
                if (!TryGetControlDescriptionVisualBounds(left, parent,
                        out leftBounds))
                    continue;

                for (int j = 0; j < descriptions.Length; j++) {
                    if (i == j)
                        continue;
                    ControlDescription right = descriptions[j];
                    if (right == null || right.transform.parent != parent ||
                        right.gameObject == null ||
                        !right.gameObject.activeSelf)
                        continue;

                    Bounds rightBounds;
                    if (!TryGetControlDescriptionVisualBounds(right, parent,
                            out rightBounds) ||
                        rightBounds.min.x < leftBounds.max.x)
                        continue;

                    float gap = rightBounds.min.x - leftBounds.max.x;
                    if (gap > 0.1f && gap < smallestGap)
                        smallestGap = gap;
                }
            }

            float fallback = Mathf.Max(6f, hintSpacing * 2.5f);
            return smallestGap < float.MaxValue &&
                smallestGap <= fallback * 3f
                    ? smallestGap : fallback;
        }

        internal static void DestroyControlHintRow(
            ControlHintRowHandle row)
        {
            if (row == null)
                return;
            for (int i = row.Hints.Count - 1; i >= 0; i--)
                DestroyFooterHint(row.Hints[i]);
            row.Hints.Clear();
            row.Parent = null;
            row.Width = 0f;
            row.Height = 0f;
        }

        internal static FooterHintHandle CreateControlHint(
            ControlHintOptions options)
        {
            if (options == null || options.Source == null ||
                options.Source.gameObject == null || options.Parent == null)
                return null;

            ControlDescription template = options.Template != null
                ? options.Template : options.Source;
            if (template.gameObject == null)
                template = options.Source;

            string objectName = !string.IsNullOrEmpty(options.Name)
                ? options.Name : "NativeControlHint";
            RectTransform templateRect =
                template.GetComponent<RectTransform>();
            Vector2 normalizedSize = options.NormalizeRect
                ? GetSizeRelativeToTarget(templateRect, options.Parent)
                : Vector2.zero;
            if (normalizedSize.x < 1f)
                normalizedSize.x = templateRect != null
                    ? Mathf.Abs(templateRect.rect.width) : nativeHintHeight;
            if (normalizedSize.y < 1f)
                normalizedSize.y = templateRect != null
                    ? Mathf.Abs(templateRect.rect.height) : nativeHintHeight;

            GameObject root = GameObject.Instantiate(
                template.gameObject, options.Parent);
            if (root == null)
                return null;
            root.name = objectName;
            RectTransform rect = root.GetComponent<RectTransform>();
            ControlDescription description =
                root.GetComponent<ControlDescription>();
            if (description == null || rect == null) {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            root.SetActive(false);

            if (description.OnAction != null)
                description.OnAction.RemoveAllListeners();
            if (description.OnFill != null)
                description.OnFill.RemoveAllListeners();

            description.actionName = options.Source.actionName;
            description.descriptionVariant = options.VariantSource != null
                ? options.VariantSource.descriptionVariant
                : options.Source.descriptionVariant;
            description.inputHandlingMethod = options.InputHandlingMethod;
            description.isUIDescription = true;
            description.canHold = options.CanHold;
            description.timeToHold = options.TimeToHold;
            description.timeToWaitBeforeInvokingAction =
                options.Source.timeToWaitBeforeInvokingAction;
            description.OnlyHandleMouseClickInput =
                options.OnlyHandleMouseClickInput;
            description.blockKeyboardInput =
                options.OnlyHandleMouseClickInput;
            description.blockMouseInput = false;
            description.blockInput = false;
            description.blockBothSoft = false;
            description.hasAction = true;
            ResetControlHintInput(description);

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            if (options.SetAsLastSibling)
                root.transform.SetAsLastSibling();
            root.SetActive(true);
            description.Show();
            description.inputHandlingMethod = options.InputHandlingMethod;
            description.isUIDescription = true;
            description.canHold = options.CanHold;
            description.timeToHold = options.TimeToHold;
            description.OnlyHandleMouseClickInput =
                options.OnlyHandleMouseClickInput;
            description.blockKeyboardInput =
                options.OnlyHandleMouseClickInput;
            description.blockMouseInput = false;
            description.blockInput = false;
            description.blockBothSoft = false;
            description.hasAction = true;
            description.forceNormalColor = false;
            description.canRunUpdate = true;
            ResetControlHintInput(description);
            ApplyControlHintContent(description, options.Source,
                options.Keys, options.Label);
            if (!options.CanHold)
                HideNonActionControlHintTexts(description, root);
            description.RefreshLayout();
            ArrangeModifierKeyImages(description, options.Keys);

            if (description.OnAction != null) {
                description.OnAction.RemoveAllListeners();
                if (options.Action != null) {
                    Action action = delegate {
                        options.Action();
                    };
                    UnityAction unityAction =
                        DelegateSupport.ConvertDelegate<UnityAction>(action);
                    description.OnAction.AddListener(unityAction);
                }
            }

            FooterHintHandle handle = new FooterHintHandle {
                Root = root,
                Rect = rect,
                ContentRect = rect,
                NormalizedSize = normalizedSize,
                CanvasGroup = canvasGroup,
                Description = description,
                Source = options.Source,
                Keys = options.Keys,
                Text = options.Label ?? string.Empty,
                Label = description.texts != null &&
                    description.texts.Length > 0
                        ? description.texts[0] : null,
                Height = Mathf.Max(1f, Mathf.Abs(rect.rect.height)),
                Width = CalculateControlHintWidth(rect),
                Enabled = true,
                BlockKeyboardInput = options.OnlyHandleMouseClickInput,
                NormalizeRect = options.NormalizeRect,
            };

            if (options.NormalizeRect)
                PrepareNormalizedControlHint(handle, true);
            else
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            Bounds bounds;
            if (TryGetControlHintVisualBounds(handle, options.Parent,
                    out bounds)) {
                handle.Width = Mathf.Max(1f, bounds.size.x);
                handle.Height = Mathf.Max(1f, bounds.size.y);
            }

            if (!RegisterControlHintInputOwner(handle)) {
                DestroyFooterHint(handle);
                return null;
            }
            return handle;
        }

        private static bool RegisterControlHintInputOwner(
            FooterHintHandle handle)
        {
            if (handle == null || handle.Description == null ||
                handle.Source == null || handle.BlockKeyboardInput ||
                !handle.Description.canHold)
                return true;

            UIDescription owner =
                handle.Source.GetComponentInParent<UIDescription>();
            if (owner == null || owner.descriptions == null)
                return false;

            ControlDescription[] descriptions = owner.descriptions;
            for (int i = 0; i < descriptions.Length; i++) {
                if (descriptions[i] != handle.Description)
                    continue;
                handle.InputOwner = owner;
                return true;
            }

            ControlDescription[] expanded =
                new ControlDescription[descriptions.Length + 1];
            Array.Copy(descriptions, expanded, descriptions.Length);
            expanded[descriptions.Length] = handle.Description;
            owner.descriptions = expanded;
            handle.InputOwner = owner;
            return true;
        }

        private static void UnregisterControlHintInputOwner(
            FooterHintHandle handle)
        {
            if (handle == null)
                return;

            UIDescription owner = handle.InputOwner;
            handle.InputOwner = null;
            if (owner == null || owner.descriptions == null ||
                handle.Description == null)
                return;

            ControlDescription[] descriptions = owner.descriptions;
            int index = -1;
            for (int i = 0; i < descriptions.Length; i++) {
                if (descriptions[i] != handle.Description)
                    continue;
                index = i;
                break;
            }
            if (index < 0)
                return;

            ControlDescription[] reduced =
                new ControlDescription[descriptions.Length - 1];
            if (index > 0)
                Array.Copy(descriptions, 0, reduced, 0, index);
            if (index < descriptions.Length - 1)
                Array.Copy(descriptions, index + 1, reduced, index,
                    descriptions.Length - index - 1);
            owner.descriptions = reduced;
        }

        internal static void ApplyNativeHoldSuffix(FooterHintHandle handle,
            string baseText, string holdText)
        {
            if (handle == null || handle.Description == null ||
                handle.Label == null)
                return;

            Text sourceLabel = handle.Source != null &&
                handle.Source.texts != null &&
                handle.Source.texts.Length > 0
                    ? handle.Source.texts[0] : null;
            if (!handle.UsesNativeHoldSuffix) {
                handle.HoldBaseColor = sourceLabel != null
                    ? GetRenderedTextColor(sourceLabel) : hintNormalColor;
                handle.HoldSuffixFontSize =
                    Mathf.Max(8, handle.Label.fontSize - 2);
                Text nativeHold = FindNativeHoldText(handle.Source);
                if (nativeHold != null)
                    handle.HoldSuffixFontSize = nativeHold.fontSize;
            }

            handle.Description.forceNormalColor = true;
            handle.UsesNativeHoldSuffix = true;
            handle.HoldBaseText = baseText ?? string.Empty;
            handle.HoldSuffixText = holdText ?? string.Empty;
            handle.HoldSuffixHovered = handle.Description.mouseOver;
            EnsureNativeHoldHoverEvents(handle);
            RenderNativeHoldSuffix(handle, handle.HoldSuffixHovered, true);
        }

        private static void EnsureNativeHoldHoverEvents(
            FooterHintHandle handle)
        {
            if (handle == null || handle.Root == null ||
                handle.HoldHoverTrigger != null)
                return;

            EventTrigger trigger = handle.Root.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = handle.Root.AddComponent<EventTrigger>();
            if (trigger == null)
                return;

            handle.HoldHoverTrigger = trigger;
            NativeHoldHoverHints[trigger.GetInstanceID()] = handle;
        }

        internal static void OnNativeHoldPointerEnter(EventTrigger trigger)
        {
            SetNativeHoldHoverState(trigger, true);
        }

        internal static void OnNativeHoldPointerExit(EventTrigger trigger)
        {
            SetNativeHoldHoverState(trigger, false);
        }

        private static void SetNativeHoldHoverState(EventTrigger trigger,
            bool hovered)
        {
            if (trigger == null)
                return;

            FooterHintHandle handle;
            if (!NativeHoldHoverHints.TryGetValue(trigger.GetInstanceID(),
                    out handle) || handle == null || handle.Root == null ||
                handle.Label == null || !handle.UsesNativeHoldSuffix ||
                handle.HoldSuffixHovered == hovered)
                return;

            handle.HoldSuffixHovered = hovered;
            RenderNativeHoldSuffix(handle, hovered, false);
        }

        private static void RenderNativeHoldSuffix(FooterHintHandle handle,
            bool hovered, bool refreshLayout)
        {
            if (handle == null || handle.Label == null)
                return;

            Color baseColor = hovered ? HintHoverColor : handle.HoldBaseColor;
            Text label = handle.Label;
            label.supportRichText = true;
            label.color = Color.white;
            label.CrossFadeColor(Color.white, 0f, true, true);
            string displayText =
                "<color=#" + ColorToHtmlRgba(baseColor) + ">" +
                (handle.HoldBaseText ?? string.Empty) + "</color> " +
                "<size=" + handle.HoldSuffixFontSize + "><color=#" +
                ColorToHtmlRgba(HintHoverColor) + ">[" +
                (handle.HoldSuffixText ?? string.Empty) +
                "]</color></size>";
            if (string.Equals(label.text, displayText,
                    StringComparison.Ordinal)) {
                handle.Text = displayText;
                return;
            }

            label.text = displayText;
            handle.Text = displayText;
            if (refreshLayout && handle.Description != null)
                handle.Description.RefreshLayout();
        }

        private static string ColorToHtmlRgba(Color color)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
            int a = Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f);
            return r.ToString("X2") + g.ToString("X2") +
                b.ToString("X2") + a.ToString("X2");
        }

        private static Color GetRenderedTextColor(Text text)
        {
            return text != null && text.canvasRenderer != null
                ? text.canvasRenderer.GetColor() : Color.clear;
        }

        private static Text FindNativeHoldText(ControlDescription source)
        {
            if (source == null)
                return null;

            Transform direct = source.transform.Find("hold");
            Text directText = direct != null
                ? direct.GetComponent<Text>() : null;
            if (directText != null)
                return directText;

            UIDescription owner = source.GetComponentInParent<UIDescription>();
            if (owner == null)
                return null;

            ControlDescription[] descriptions =
                owner.GetComponentsInChildren<ControlDescription>(true);
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription candidate = descriptions[i];
                if (candidate == null || candidate.transform == null)
                    continue;

                Transform hold = candidate.transform.Find("hold");
                Text holdText = hold != null
                    ? hold.GetComponent<Text>() : null;
                if (holdText != null)
                    return holdText;
            }
            return null;
        }

        private static void NormalizeControlHintRect(
            RectTransform rect, Vector2 size, bool resetPosition)
        {
            if (rect == null)
                return;

            Vector2 position = resetPosition
                ? Vector2.zero : rect.anchoredPosition;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            if (size.x > 0f && size.y > 0f)
                rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static void PrepareNormalizedControlHint(
            FooterHintHandle handle, bool resetPosition)
        {
            if (handle == null || handle.Rect == null ||
                handle.Description == null || !handle.NormalizeRect)
                return;

            NormalizeControlHintRect(handle.Rect,
                handle.NormalizedSize, resetPosition);
            LayoutRebuilder.ForceRebuildLayoutImmediate(handle.Rect);
        }

        private static void NormalizeWrappedControlHint(
            FooterHintHandle handle)
        {
            if (handle == null || !handle.NormalizeRect)
                return;

            PrepareNormalizedControlHint(handle, false);
            Transform relativeTo = handle.Rect != null
                ? handle.Rect.parent : null;
            Bounds bounds;
            if (relativeTo != null && TryGetControlHintVisualBounds(
                    handle, relativeTo, out bounds)) {
                handle.Width = Mathf.Max(1f, bounds.size.x);
                handle.Height = Mathf.Max(1f, bounds.size.y);
            }
        }

        private static bool ApplyControlHintContent(
            ControlDescription description, ControlDescription source,
            string[] keys, string label)
        {
            if (description == null || source == null)
                return false;

            Sprite mainSprite = null;
            if (keys == null || keys.Length == 0) {
                if (source.buttonImage != null)
                    mainSprite = source.buttonImage.sprite;
            } else {
                mainSprite = FindPcButtonSprite(keys[keys.Length - 1]);
            }

            bool changed = false;
            if (mainSprite != null &&
                (description.buttonImage == null ||
                    description.buttonImage.sprite != mainSprite)) {
                description.SetMainButton(mainSprite);
                changed = true;
            }
            description.DisableAdditionalButtons();

            string desiredText = label ?? string.Empty;
            Text currentText = description.texts != null &&
                description.texts.Length > 0
                    ? description.texts[0] : null;
            if (currentText == null || currentText.text != desiredText) {
                description.SetText(desiredText);
                changed = true;
            }
            return changed;
        }

        private static void ArrangeModifierKeyImages(
            ControlDescription description, string[] keys)
        {
            if (description == null || description.buttonImage == null)
                return;

            RectTransform mainRect = description.buttonImage.rectTransform;
            Transform parent = mainRect != null ? mainRect.parent : null;
            if (mainRect == null || parent == null)
                return;

            int modifierCount = keys != null
                ? Mathf.Max(0, keys.Length - 1) : 0;
            SetModifierKeyImagesActive(parent, modifierCount);
            if (modifierCount == 0)
                return;

            float keyHeight = Mathf.Abs(mainRect.rect.height);
            if (keyHeight < 1f)
                keyHeight = Mathf.Abs(mainRect.sizeDelta.y);
            if (keyHeight < 1f)
                keyHeight = nativeHintKeySize.y;

            float scaleX = Mathf.Abs(mainRect.localScale.x);
            float scaleY = Mathf.Abs(mainRect.localScale.y);
            if (scaleX < 0.0001f)
                scaleX = 1f;
            if (scaleY < 0.0001f)
                scaleY = 1f;

            float mainWidth = Mathf.Abs(mainRect.rect.width);
            if (mainWidth < 1f)
                mainWidth = Mathf.Abs(mainRect.sizeDelta.x);
            float mainLeft = mainRect.anchoredPosition.x -
                mainRect.pivot.x * mainWidth * scaleX;
            float gap = Mathf.Max(1f, keyHeight * scaleY * 0.10f);
            float cursorRight = mainLeft - gap;
            Image[] modifierImages = new Image[modifierCount];

            for (int i = modifierCount - 1; i >= 0; i--) {
                Sprite sprite = FindPcButtonSprite(keys[i]);
                Image image = GetOrCreateModifierKeyImage(parent, i,
                    description.buttonImage);
                modifierImages[i] = image;
                if (image == null)
                    continue;

                RectTransform rect = image.rectTransform;
                float width = keyHeight;
                if (sprite != null && sprite.rect.height > 0f) {
                    width = keyHeight * sprite.rect.width /
                        sprite.rect.height;
                    width = Mathf.Clamp(width, keyHeight * 0.72f,
                        keyHeight * 3.5f);
                }

                image.sprite = sprite;
                image.gameObject.SetActive(sprite != null);
                rect.anchorMin = mainRect.anchorMin;
                rect.anchorMax = mainRect.anchorMax;
                rect.pivot = new Vector2(1f, mainRect.pivot.y);
                rect.sizeDelta = new Vector2(width, keyHeight);
                rect.anchoredPosition = new Vector2(cursorRight,
                    mainRect.anchoredPosition.y);
                rect.localRotation = mainRect.localRotation;
                rect.localScale = mainRect.localScale;
                cursorRight -= width * scaleX + gap;
            }

            SyncModifierKeyImageColors(description);
        }

        private static Image GetOrCreateModifierKeyImage(
            Transform parent, int index, Image source)
        {
            if (parent == null || source == null)
                return null;

            string name = ModifierKeyObjectPrefix + index;
            Transform existing = parent.Find(name);
            Image image = existing != null
                ? existing.GetComponent<Image>() : null;
            if (image == null) {
                GameObject root = CreateUiObject(name, parent);
                image = root.AddComponent<Image>();
            }

            image.material = source.material;
            image.type = source.type;
            image.preserveAspect = source.preserveAspect;
            image.fillCenter = source.fillCenter;
            image.fillMethod = source.fillMethod;
            image.fillAmount = source.fillAmount;
            image.pixelsPerUnitMultiplier =
                source.pixelsPerUnitMultiplier;
            image.raycastTarget = false;
            LayoutElement layout = image.GetComponent<LayoutElement>();
            if (layout == null)
                layout = image.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            return image;
        }

        private static void SetModifierKeyImagesActive(
            Transform parent, int activeCount)
        {
            if (parent == null)
                return;

            for (int i = 0; i < parent.childCount; i++) {
                Transform child = parent.GetChild(i);
                if (child == null || child.gameObject == null ||
                    !child.gameObject.name.StartsWith(
                        ModifierKeyObjectPrefix,
                        StringComparison.Ordinal))
                    continue;

                int index;
                string suffix = child.gameObject.name.Substring(
                    ModifierKeyObjectPrefix.Length);
                bool valid = int.TryParse(suffix, out index) &&
                    index >= 0 && index < activeCount;
                child.gameObject.SetActive(valid);
            }
        }

        private static void SyncModifierKeyImageColors(
            ControlDescription description)
        {
            if (description == null || description.buttonImage == null ||
                description.buttonImage.rectTransform == null)
                return;

            Transform parent =
                description.buttonImage.rectTransform.parent;
            if (parent == null)
                return;

            Color componentColor = description.buttonImage.color;
            Color rendererColor = description.buttonImage.canvasRenderer != null
                ? description.buttonImage.canvasRenderer.GetColor()
                : componentColor;
            for (int i = 0; i < parent.childCount; i++) {
                Transform child = parent.GetChild(i);
                if (child == null || child.gameObject == null ||
                    !child.gameObject.name.StartsWith(
                        ModifierKeyObjectPrefix,
                        StringComparison.Ordinal))
                    continue;

                Image image = child.GetComponent<Image>();
                if (image == null)
                    continue;
                image.color = componentColor;
                if (image.canvasRenderer != null)
                    image.canvasRenderer.SetColor(rendererColor);
            }
        }

        private static void HideNonActionControlHintTexts(
            ControlDescription description, GameObject root)
        {
            if (description == null || root == null)
                return;

            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < allTexts.Length; i++) {
                Text candidate = allTexts[i];
                if (candidate == null || candidate.gameObject == null)
                    continue;

                bool isActionText = false;
                if (description.texts != null) {
                    for (int textIndex = 0;
                        textIndex < description.texts.Length; textIndex++) {
                        if (description.texts[textIndex] == candidate) {
                            isActionText = true;
                            break;
                        }
                    }
                }

                if (!isActionText)
                    candidate.gameObject.SetActive(false);
            }
        }

        private static float CalculateControlHintWidth(RectTransform rect)
        {
            if (rect == null)
                return 0f;
            float width = Mathf.Abs(rect.rect.width);
            float preferred = LayoutUtility.GetPreferredWidth(rect);
            if (preferred > width)
                width = preferred;
            if (width < 1f)
                width = Mathf.Abs(rect.sizeDelta.x);
            return Mathf.Max(1f, width);
        }

        internal static bool TryGetControlHintVisualBounds(
            FooterHintHandle handle, Transform relativeTo, out Bounds bounds)
        {
            if (handle == null || handle.Root == null ||
                handle.Description == null) {
                bounds = new Bounds();
                return false;
            }

            return TryGetControlDescriptionVisualBounds(
                handle.Description, handle.Root.transform, handle.Keys,
                relativeTo, out bounds);
        }

        internal static bool TryGetRectTransformBounds(RectTransform rect,
            Transform relativeTo, out Bounds bounds)
        {
            bounds = new Bounds();
            if (rect == null || relativeTo == null)
                return false;
            Il2CppStructArray<Vector3> corners =
                new Il2CppStructArray<Vector3>(4);
            rect.GetWorldCorners(corners);
            Vector3 minimum = relativeTo.InverseTransformPoint(corners[0]);
            Vector3 maximum = minimum;
            for (int i = 1; i < corners.Length; i++) {
                Vector3 point = relativeTo.InverseTransformPoint(corners[i]);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
            bounds.SetMinMax(minimum, maximum);
            return true;
        }

        internal static bool TryGetControlDescriptionVisualBounds(
            ControlDescription description, Transform relativeTo,
            out Bounds bounds)
        {
            Transform root = description != null
                ? description.transform : null;
            return TryGetControlDescriptionVisualBounds(description, root,
                null, relativeTo, out bounds);
        }

        private static bool TryGetControlDescriptionVisualBounds(
            ControlDescription description, Transform hintRoot,
            string[] keys, Transform relativeTo, out Bounds bounds)
        {
            bounds = new Bounds();
            if (description == null || hintRoot == null ||
                relativeTo == null)
                return false;

            bool found = false;
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;
            Graphic[] graphics =
                hintRoot.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) {
                Graphic graphic = graphics[i];
                if (!IsControlHintContentGraphic(description, hintRoot,
                        keys, graphic))
                    continue;

                Vector3 graphicMinimum;
                Vector3 graphicMaximum;
                if (!TryGetGraphicBounds(graphic, relativeTo,
                        out graphicMinimum, out graphicMaximum))
                    continue;

                if (!found) {
                    minimum = graphicMinimum;
                    maximum = graphicMaximum;
                    found = true;
                } else {
                    minimum = Vector3.Min(minimum, graphicMinimum);
                    maximum = Vector3.Max(maximum, graphicMaximum);
                }
            }

            if (!found)
                return false;
            bounds.SetMinMax(minimum, maximum);
            return true;
        }

        private static bool IsControlHintContentGraphic(
            ControlDescription description, Transform hintRoot,
            string[] keys, Graphic graphic)
        {
            if (description == null || hintRoot == null ||
                graphic == null || !graphic.enabled ||
                graphic.gameObject == null ||
                !IsActiveInsideHint(graphic.transform, hintRoot))
                return false;

            Text text = graphic.TryCast<Text>();
            if (text != null) {
                if (description.texts == null)
                    return false;
                for (int i = 0; i < description.texts.Length; i++) {
                    if (description.texts[i] == text)
                        return true;
                }
                return false;
            }

            Image image = graphic.TryCast<Image>();
            if (image == null || image.sprite == null)
                return false;
            if (image == description.buttonImage ||
                image == description.buttonFill)
                return true;

            if (keys == null)
                return false;
            for (int i = 0; i < keys.Length; i++) {
                Sprite expected = FindPcButtonSprite(keys[i]);
                if (expected == null)
                    continue;
                if (image.sprite == expected || string.Equals(
                        image.sprite.name, expected.name,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsActiveInsideHint(Transform item,
            Transform hintRoot)
        {
            Transform current = item;
            while (current != null) {
                if (current.gameObject == null ||
                    !current.gameObject.activeSelf)
                    return false;
                if (current == hintRoot)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool TryGetGraphicBounds(Graphic graphic,
            Transform relativeTo, out Vector3 minimum, out Vector3 maximum)
        {
            minimum = Vector3.zero;
            maximum = Vector3.zero;
            RectTransform rect = graphic != null
                ? graphic.rectTransform : null;
            if (rect == null || relativeTo == null)
                return false;

            Il2CppStructArray<Vector3> corners =
                new Il2CppStructArray<Vector3>(4);
            rect.GetWorldCorners(corners);
            minimum = relativeTo.InverseTransformPoint(corners[0]);
            maximum = minimum;
            for (int i = 1; i < corners.Length; i++) {
                Vector3 point = relativeTo.InverseTransformPoint(corners[i]);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }

            Text text = graphic.TryCast<Text>();
            if (text == null)
                return true;

            float rectWidth = Mathf.Abs(rect.rect.width);
            float renderedWidth = Mathf.Abs(maximum.x - minimum.x);
            if (rectWidth < 0.001f || renderedWidth < 0.001f)
                return true;

            float preferredWidth = Mathf.Max(0f, text.preferredWidth) *
                renderedWidth / rectWidth;
            if (preferredWidth < 0.001f)
                return true;

            float center = (minimum.x + maximum.x) * 0.5f;
            TextAnchor alignment = text.alignment;
            if (alignment == TextAnchor.UpperRight ||
                alignment == TextAnchor.MiddleRight ||
                alignment == TextAnchor.LowerRight) {
                minimum.x = maximum.x - preferredWidth;
            } else if (alignment == TextAnchor.UpperCenter ||
                alignment == TextAnchor.MiddleCenter ||
                alignment == TextAnchor.LowerCenter) {
                minimum.x = center - preferredWidth * 0.5f;
                maximum.x = center + preferredWidth * 0.5f;
            } else {
                maximum.x = minimum.x + preferredWidth;
            }
            return true;
        }

        private static ControlDescription FindStandardActionDescription(
            ControlDescription source)
        {
            if (source == null)
                return null;

            UIDescription owner = source.GetComponentInParent<UIDescription>();
            ControlDescription fallback = null;
            if (owner != null && owner.descriptions != null) {
                for (int i = 0; i < owner.descriptions.Length; i++) {
                    ControlDescription candidate = owner.descriptions[i];
                    if (!IsUsableStandardActionDescription(candidate))
                        continue;
                    if (string.Equals(candidate.descriptionVariant.ToString(),
                            "Action", StringComparison.Ordinal))
                        return candidate;
                    if (fallback == null)
                        fallback = candidate;
                }
            }
            if (fallback != null)
                return fallback;

            Transform root = owner != null
                ? owner.transform : source.transform.root;
            if (root == null)
                return null;
            ControlDescription[] descriptions =
                root.GetComponentsInChildren<ControlDescription>(true);
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription candidate = descriptions[i];
                if (!IsUsableStandardActionDescription(candidate))
                    continue;
                if (string.Equals(candidate.descriptionVariant.ToString(),
                        "Action", StringComparison.Ordinal))
                    return candidate;
                if (fallback == null)
                    fallback = candidate;
            }
            return fallback;
        }

        private static bool IsUsableStandardActionDescription(
            ControlDescription description)
        {
            return description != null && description.gameObject != null &&
                description.buttonImage != null &&
                !IsAlternativeActionDescription(description);
        }

        private static ControlDescription FindAlternativeActionDescription(
            ControlDescription source)
        {
            if (source == null)
                return null;

            UIDescription owner = source.GetComponentInParent<UIDescription>();
            if (owner != null && owner.descriptions != null) {
                for (int i = 0; i < owner.descriptions.Length; i++) {
                    ControlDescription candidate = owner.descriptions[i];
                    if (IsAlternativeActionDescription(candidate))
                        return candidate;
                }
            }

            Transform root = owner != null ? owner.transform : source.transform.root;
            if (root != null) {
                ControlDescription[] descriptions =
                    root.GetComponentsInChildren<ControlDescription>(true);
                for (int i = 0; i < descriptions.Length; i++) {
                    if (IsAlternativeActionDescription(descriptions[i]))
                        return descriptions[i];
                }
            }

            return null;
        }

        private static bool IsAlternativeActionDescription(
            ControlDescription description)
        {
            return description != null && string.Equals(
                description.descriptionVariant.ToString(),
                "AlternativeAction", StringComparison.Ordinal);
        }

        private static void ResetControlHintInput(
            ControlDescription description)
        {
            if (description == null)
                return;
            description.FillEventRegistered = false;
            description.holdTime = 0f;
            description.eventInvoked = false;
            description.eventInvoking = false;
            description.mouseDown = false;
            description.mouseOver = false;
            if (description.buttonFill != null)
                description.buttonFill.fillAmount = 0f;
        }

        public static void UpdateFooterHint(FooterHintHandle handle,
            string label, bool enabled)
        {
            if (handle == null || handle.Root == null)
                return;

            handle.Text = label ?? string.Empty;
            handle.Enabled = enabled;

            if (handle.IsStyledFooter) {
                if (handle.Label != null &&
                    handle.Label.text != handle.Text) {
                    handle.Label.text = handle.Text;
                    RectTransform labelRect =
                        handle.Label.rectTransform;
                    labelRect.sizeDelta = new Vector2(
                        Mathf.Max(40f,
                            handle.Label.preferredWidth + 2f),
                        handle.Height);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(
                        handle.Rect);
                    float width =
                        LayoutUtility.GetPreferredWidth(handle.Rect);
                    if (width > 0f) {
                        handle.Width = width;
                        handle.Rect.sizeDelta = new Vector2(
                            width, handle.Height);
                    }
                }
                if (handle.Button != null)
                    handle.Button.interactable = enabled;
                if (handle.CanvasGroup != null)
                    handle.CanvasGroup.alpha = enabled ? 1f : 0.55f;
                return;
            }

            if (handle.Description == null)
                return;

            if (handle.Label == null && handle.Description.texts != null &&
                handle.Description.texts.Length > 0)
                handle.Label = handle.Description.texts[0];
            if (handle.Label == null ||
                handle.Label.text != handle.Text) {
                handle.Description.SetText(handle.Text);
                handle.Description.RefreshLayout();
                ArrangeModifierKeyImages(handle.Description,
                    handle.Keys);
                if (handle.NormalizeRect)
                    NormalizeWrappedControlHint(handle);
                else if (handle.ContentRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(
                        handle.ContentRect);
                handle.Width = Mathf.Max(1f,
                    Mathf.Abs(handle.Rect.rect.width));
                handle.Height = Mathf.Max(1f,
                    Mathf.Abs(handle.Rect.rect.height));
                if (handle.Description.texts != null &&
                    handle.Description.texts.Length > 0)
                    handle.Label = handle.Description.texts[0];
            }

            handle.Description.blockInput = !enabled;
            handle.Description.blockMouseInput = !enabled;
            handle.Description.blockKeyboardInput =
                handle.BlockKeyboardInput || !enabled;
            if (!enabled)
                ResetControlHintInput(handle.Description);
            if (handle.CanvasGroup != null)
                handle.CanvasGroup.alpha = enabled ? 1f : 0.55f;
            if (handle.Row != null)
                RelayoutControlHintRow(handle.Row);
        }

        public static void SetFooterHintActive(FooterHintHandle handle,
            bool active)
        {
            if (handle == null || handle.Root == null)
                return;
            if (handle.Root.activeSelf == active)
                return;

            if (!active && !handle.IsStyledFooter &&
                handle.Description != null)
                handle.Description.Hide();
            handle.Root.SetActive(active);
            if (handle.IsStyledFooter) {
                if (handle.Row != null)
                    RelayoutControlHintRow(handle.Row);
                return;
            }

            if (handle.Description == null)
                return;
            if (!active) {
                if (handle.Row != null)
                    RelayoutControlHintRow(handle.Row);
                return;
            }

            handle.Description.Show();
            handle.Description.blockInput = !handle.Enabled;
            handle.Description.blockMouseInput = !handle.Enabled;
            handle.Description.blockKeyboardInput =
                handle.BlockKeyboardInput || !handle.Enabled;
            handle.Description.hasAction = true;
            ResetControlHintInput(handle.Description);
            ApplyControlHintContent(handle.Description, handle.Source,
                handle.Keys, handle.Text);
            handle.Description.RefreshLayout();
            ArrangeModifierKeyImages(handle.Description, handle.Keys);
            if (handle.NormalizeRect)
                NormalizeWrappedControlHint(handle);
            else if (handle.ContentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    handle.ContentRect);
            handle.Width = Mathf.Max(1f,
                Mathf.Abs(handle.Rect.rect.width));
            handle.Height = Mathf.Max(1f,
                Mathf.Abs(handle.Rect.rect.height));
            if (handle.Row != null)
                RelayoutControlHintRow(handle.Row);
        }

        public static void DestroyFooterHint(FooterHintHandle handle)
        {
            if (handle == null)
                return;
            ControlHintRowHandle row = handle.Row;
            handle.Row = null;
            if (row != null)
                row.Hints.Remove(handle);
            UnregisterControlHintInputOwner(handle);
            if (handle.HoldHoverTrigger != null)
                NativeHoldHoverHints.Remove(
                    handle.HoldHoverTrigger.GetInstanceID());
            if (handle.Description != null &&
                handle.Description.OnAction != null)
                handle.Description.OnAction.RemoveAllListeners();
            if (handle.Description != null &&
                handle.Description.OnFill != null)
                handle.Description.OnFill.RemoveAllListeners();
            if (handle.Button != null)
                handle.Button.onClick.RemoveAllListeners();
            if (handle.Root != null) {
                handle.Root.SetActive(false);
                UnityEngine.Object.Destroy(handle.Root);
            }
            handle.Root = null;
            handle.Rect = null;
            handle.ContentRect = null;
            handle.Button = null;
            handle.KeyImages.Clear();
            handle.HoldHoverTrigger = null;
            handle.Description = null;
            handle.Source = null;
            handle.InputOwner = null;
            handle.Action = null;
            handle.Keys = null;
            handle.Text = null;
            if (row != null)
                RelayoutControlHintRow(row);
        }

        public static void EnsureHintStyles(Transform preferredRoot)
        {
            if (hintSource == null)
                CaptureHintStyles(null);
            EnsureFallbacks(null);
        }

        private static HintStyleProfile ResolveHintStyleProfile(
            Transform preferredRoot)
        {
            string selectionReason;
            ControlDescription source = FindBestHintSource(preferredRoot,
                out selectionReason);
            if (source == null) {
                if (hintSource == null)
                    CaptureHintStyles(null);
                source = hintSource;
                selectionReason = source != null
                    ? "fallback to cached global source after recapture"
                    : "no ControlDescription source found";
            }

            HintStyleProfile profile = new HintStyleProfile();
            profile.KeySize = nativeHintKeySize;
            profile.Height = nativeHintHeight;
            profile.FontSize = nativeHintFontSize;
            profile.Spacing = hintSpacing;
            profile.TextColor = hintNormalColor;
            if (source == null) {
                profile.KeyImage.Capture(null);
                profile.Text.Font = HintText.Font;
                profile.Text.FontSize = HintText.FontSize;
                profile.Text.Color = HintText.Color;
                profile.FontSize = Mathf.Clamp(HintText.FontSize, 10, 18);
                profile.TextColor = hintNormalColor;
                return profile;
            }

            RectTransform rootRect = source.GetComponent<RectTransform>();
            Vector2 renderedRootSize = GetSizeRelativeToTarget(
                rootRect, preferredRoot);
            if (renderedRootSize.y > 4f)
                profile.Height = renderedRootSize.y;

            profile.KeyImage.Capture(source.buttonImage);
            RectTransform keyRect = source.buttonImage != null
                ? source.buttonImage.rectTransform : null;
            if (keyRect != null) {
                Vector2 size = GetSizeRelativeToTarget(
                    keyRect, preferredRoot);
                if (size.x >= 8f && size.y >= 8f)
                    profile.KeySize = size;
            }

            Text sourceText = null;
            if (source.texts != null && source.texts.Length > 0)
                sourceText = source.texts[0];
            profile.Text.Capture(sourceText);
            if (sourceText != null) {
                profile.FontSize = sourceText.fontSize;
                if (sourceText.canvasRenderer != null)
                    profile.TextColor =
                        sourceText.canvasRenderer.GetColor();
                else
                    profile.TextColor = sourceText.color;
            }
            profile.Height = Mathf.Max(profile.Height, profile.KeySize.y);
            profile.Spacing = ResolveHintSpacingRelativeToTarget(
                keyRect, sourceText, preferredRoot);
            return profile;
        }

        private static ControlDescription FindBestHintSource(
            Transform preferredRoot, out string reason)
        {
            ControlDescription best = null;
            int bestScore = int.MinValue;
            int bestLevel = -1;
            if (preferredRoot != null) {
                Transform searchRoot = preferredRoot;
                for (int level = 0; level < 3 && searchRoot != null; level++) {
                    ControlDescription[] local = searchRoot
                        .GetComponentsInChildren<ControlDescription>(true);
                    for (int i = 0; i < local.Length; i++) {
                        int score = ScoreHint(local[i], true) - level * 20;
                        if (score > bestScore) {
                            best = local[i];
                            bestScore = score;
                            bestLevel = level;
                        }
                    }
                    searchRoot = searchRoot.parent;
                }
            }
            if (best != null) {
                reason = "local candidate; ancestor level=" + bestLevel +
                    "; score=" + bestScore;
                return best;
            }
            reason = hintSource != null
                ? "cached global ControlDescription; no local candidate"
                : "no local or cached ControlDescription";
            return hintSource;
        }

        private static Vector2 GetHintKeySize(HintStyleProfile style,
            Sprite sprite)
        {
            Vector2 size = style.KeySize;
            if (sprite == null || sprite.rect.height <= 0f || size.y <= 0f)
                return size;
            float aspect = sprite.rect.width / sprite.rect.height;
            float width = size.y * aspect;
            width = Mathf.Clamp(width, size.y * 0.72f, size.y * 3.5f);
            return new Vector2(width, size.y);
        }

        private static Vector2 GetSizeRelativeToTarget(
            RectTransform source, Transform target)
        {
            if (source == null)
                return Vector2.zero;
            Transform reference = target != null ? target : source.parent;
            float targetScaleX = reference != null
                ? Mathf.Abs(reference.lossyScale.x) : 1f;
            float targetScaleY = reference != null
                ? Mathf.Abs(reference.lossyScale.y) : 1f;
            if (targetScaleX < 0.0001f)
                targetScaleX = 1f;
            if (targetScaleY < 0.0001f)
                targetScaleY = 1f;
            return new Vector2(
                Mathf.Abs(source.rect.width) *
                    Mathf.Abs(source.lossyScale.x) / targetScaleX,
                Mathf.Abs(source.rect.height) *
                    Mathf.Abs(source.lossyScale.y) / targetScaleY);
        }

        private static float ResolveHintSpacingRelativeToTarget(
            RectTransform keyRect, Text text, Transform target)
        {
            if (keyRect == null || text == null)
                return 4f;
            RectTransform textRect = text.rectTransform;
            if (textRect == null || keyRect.parent != textRect.parent)
                return 4f;

            float keyWidth = Mathf.Abs(keyRect.rect.width) *
                Mathf.Abs(keyRect.localScale.x);
            float textWidth = Mathf.Abs(textRect.rect.width) *
                Mathf.Abs(textRect.localScale.x);
            float keyRight = keyRect.anchoredPosition.x +
                (1f - keyRect.pivot.x) * keyWidth;
            float textLeft = textRect.anchoredPosition.x -
                textRect.pivot.x * textWidth;
            float gap = textLeft - keyRight;

            Transform sourceParent = keyRect.parent;
            float sourceScale = sourceParent != null
                ? Mathf.Abs(sourceParent.lossyScale.x) : 1f;
            float targetScale = target != null
                ? Mathf.Abs(target.lossyScale.x) : sourceScale;
            if (targetScale < 0.0001f)
                targetScale = 1f;
            gap = gap * sourceScale / targetScale;
            if (gap < 1f || gap > 24f)
                gap = 4f;
            return gap;
        }



        public static Image CreateImage(Transform parent, string name,
            Sprite sprite, Color color, bool raycastTarget)
        {
            GameObject root = CreateUiObject(name, parent);
            Image image = root.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CloneText(Transform parent, string name, Text source)
        {
            if (parent == null || source == null || source.gameObject == null)
                return null;

            GameObject root = GameObject.Instantiate(source.gameObject, parent);
            root.name = name;
            root.transform.localScale = Vector3.one;
            return root.GetComponent<Text>();
        }

        public static Text CreateText(Transform parent, string name,
            string value, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject root = CreateUiObject(name, parent);
            Text text = root.AddComponent<Text>();
            BaseText.Apply(text);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public static GameObject CreateNativeNoItemsPage(Transform parent)
        {
            if (parent == null)
                return null;

            Transform template = FindNativeNoItemsPage(parent.root);
            if (template == null || template.gameObject == null)
                return null;

            GameObject root = GameObject.Instantiate(
                template.gameObject, parent);
            root.name = "QNativeNoItemsPage";
            root.transform.localScale = Vector3.one;
            root.SetActive(true);
            return root;
        }

        private static Transform FindNativeNoItemsPage(Transform root)
        {
            if (root == null)
                return null;

            RectTransform[] transforms =
                root.GetComponentsInChildren<RectTransform>(true);
            for (int index = 0; index < transforms.Length; index++) {
                RectTransform candidate = transforms[index];
                if (candidate == null || candidate.gameObject == null)
                    continue;
                if (string.Equals(candidate.gameObject.name, "NoItemsPage",
                        StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        public static GameObject CreateUiObject(string name,
            Transform parent)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.layer = parent != null
                ? parent.gameObject.layer : 5;
            RectTransform rect = gameObject.AddComponent<RectTransform>();
            if (parent != null)
                rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return gameObject;
        }

        public static void Stretch(RectTransform rect, float left,
            float bottom, float right, float top)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        public static void CopyRect(RectTransform source,
            RectTransform target)
        {
            if (source == null || target == null)
                return;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
        }

        public static void AddBorder(GameObject host, Color color,
            float thickness)
        {
            if (host == null)
                return;
            CreateBorderEdge(host.transform, "Top", color,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, thickness));
            CreateBorderEdge(host.transform, "Bottom", color,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, thickness));
            CreateBorderEdge(host.transform, "Left", color,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(thickness, 0f));
            CreateBorderEdge(host.transform, "Right", color,
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0f), new Vector2(thickness, 0f));
        }

        private static void CreateBorderEdge(Transform parent, string name,
            Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 sizeDelta)
        {
            GameObject edge = CreateUiObject("Border" + name, parent);
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;
            Image image = edge.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CaptureTutorialStyles(
            TutorialsWindow tutorialsWindow)
        {
            if (tutorialsWindow == null)
                return;

            WindowRect.Capture(
                tutorialsWindow.GetComponent<RectTransform>());
            if (tutorialsWindow.uiDescription != null)
                DescriptionRect.Capture(tutorialsWindow.uiDescription
                    .GetComponent<RectTransform>());
            Image windowImage = FindFirstImage(
                tutorialsWindow.background != null
                    ? tutorialsWindow.background.transform : null,
                null, null);
            WindowBackground.Capture(windowImage);

            TutorialItem template = FindTutorialCard(tutorialsWindow,
                false);
            TutorialItem workbench = FindTutorialCard(tutorialsWindow,
                true);
            if (template == null)
                template = workbench;
            if (template == null)
                return;

            RectTransform cardsArea = template.transform.parent != null
                ? template.transform.parent.GetComponent<RectTransform>()
                : null;
            if (cardsArea == null &&
                tutorialsWindow.gridNavigationManager != null) {
                cardsArea = tutorialsWindow.gridNavigationManager
                    .GetComponent<RectTransform>();
            }
            CardsRect.Capture(cardsArea);
            if (cardsArea != null) {
                Vector2 areaSize = cardsArea.rect.size;
                nativeCardsAreaSize = new Vector2(
                    Mathf.Abs(areaSize.x), Mathf.Abs(areaSize.y));
            }

            RectTransform cardRect = template.GetComponent<RectTransform>();
            CardRect.Capture(cardRect);
            if (cardRect != null) {
                Vector2 size = cardRect.rect.size;
                nativeCardSize = new Vector2(Mathf.Abs(size.x),
                    Mathf.Abs(size.y));
            }

            Image background = FindFirstImage(template.transform,
                "BGSolid", null);
            if (background == null)
                background = template.GetComponent<Image>();
            CardBackground.Capture(background);
            if (background != null)
                cardNormalColor = background.color;

            Image selected = FindFirstImage(template.transform,
                "Selected", null);
            CardSelected.Capture(selected);
            CardSelectedRect.Capture(selected != null
                ? selected.rectTransform : null);

            Text title = FindFirstText(template.transform, "Text");
            CardText.Capture(title);
            CardTitleRect.Capture(title != null
                ? title.rectTransform : null);
            BaseText.Capture(title);

            TutorialItem iconSource = ResolveModCardIconSource(
                tutorialsWindow, workbench, template);
            Image icon = iconSource != null
                ? FindFirstImage(iconSource.transform, "Icon", null)
                : null;
            CardIcon.Capture(icon);
            CardIconRect.Capture(icon != null
                ? icon.rectTransform : null);
            if (icon != null)
                modCardIcon = icon.sprite;
        }

        private static void CaptureSettingsStyles()
        {
            Il2CppReferenceArray<UnityEngine.Object> loaded =
                Resources.FindObjectsOfTypeAll(
                    Il2CppType.Of<SettingsButton>());
            int bestScore = int.MinValue;
            foreach (UnityEngine.Object item in loaded) {
                SettingsButton candidate = item.TryCast<SettingsButton>();
                if (candidate == null || candidate.gameObject == null ||
                    candidate.text == null)
                    continue;
                int score = ScoreSettingsSource(candidate);
                if (score > bestScore) {
                    settingsSource = candidate;
                    bestScore = score;
                }
            }

            if (settingsSource == null)
                return;

            RectTransform rect =
                settingsSource.GetComponent<RectTransform>();
            SettingsRowRect.Capture(rect);
            if (rect != null) {
                Vector2 size = rect.rect.size;
                nativeSettingsRowSize = new Vector2(
                    Mathf.Abs(size.x), Mathf.Abs(size.y));
            }

            Image background = settingsSource.GetComponent<Image>();
            if (background == null)
                background = FindFirstImage(settingsSource.transform,
                    null, new string[] { "hover", "selected" });
            SettingsBackground.Capture(background);
            if (background != null)
                settingsNormalColor = background.color;

            Image hover = FindFirstImage(
                settingsSource.hover != null
                    ? settingsSource.hover.transform : null,
                null, null);
            SettingsHover.Capture(hover);
            if (hover != null)
                settingsHoverColor = hover.color;

            Image selected = FindFirstImage(
                settingsSource.selected != null
                    ? settingsSource.selected.transform : null,
                null, null);
            SettingsSelected.Capture(selected);
            if (selected != null)
                settingsPressedColor = selected.color;

            SettingsText.Capture(settingsSource.text);
            if (BaseText.Font == null)
                BaseText.Capture(settingsSource.text);
            CaptureSettingsArrowStyles(settingsSource);
        }

        private static void CaptureSettingsArrowStyles(
            SettingsButton source)
        {
            SettingsLeftArrow.Reset();
            SettingsRightArrow.Reset();
            if (source == null || source.gameObject == null)
                return;

            Transform hover = source.hover != null
                ? source.hover.transform : null;
            Transform selected = source.selected != null
                ? source.selected.transform : null;
            Text value = FindSettingsValueText(source.transform,
                source.text, hover, selected);
            Image background = source.GetComponent<Image>();
            Image[] images = source.GetComponentsInChildren<Image>(true);
            int bestLeft = int.MinValue;
            int bestRight = int.MinValue;
            for (int i = 0; i < images.Length; i++) {
                Image candidate = images[i];
                if (candidate == null || candidate == background ||
                    candidate.sprite == null ||
                    IsInside(candidate.transform, hover) ||
                    IsInside(candidate.transform, selected))
                    continue;

                int leftScore = ScoreSettingsArrowCandidate(candidate,
                    value, true);
                if (leftScore >= 150 && leftScore > bestLeft) {
                    bestLeft = leftScore;
                    SettingsLeftArrow.Capture(candidate);
                }
                int rightScore = ScoreSettingsArrowCandidate(candidate,
                    value, false);
                if (rightScore >= 150 && rightScore > bestRight) {
                    bestRight = rightScore;
                    SettingsRightArrow.Capture(candidate);
                }
            }

            CaptureGlobalSettingsArrowSprites();
        }

        private static int ScoreSettingsArrowCandidate(Image candidate,
            Text value, bool pointsLeft)
        {
            string objectName = candidate.gameObject.name ?? string.Empty;
            string spriteName = candidate.sprite != null
                ? candidate.sprite.name : string.Empty;
            string descriptor = (objectName + " " + spriteName)
                .ToLowerInvariant();
            bool mentionsArrow = descriptor.IndexOf("arrow",
                StringComparison.Ordinal) >= 0 ||
                descriptor.IndexOf("chevron",
                    StringComparison.Ordinal) >= 0;
            bool mentionsLeft = descriptor.IndexOf("left",
                StringComparison.Ordinal) >= 0 ||
                descriptor.IndexOf("prev",
                    StringComparison.Ordinal) >= 0;
            bool mentionsRight = descriptor.IndexOf("right",
                StringComparison.Ordinal) >= 0 ||
                descriptor.IndexOf("next",
                    StringComparison.Ordinal) >= 0;
            bool mentionsDirection = pointsLeft
                ? mentionsLeft : mentionsRight;
            bool mentionsOpposite = pointsLeft
                ? mentionsRight : mentionsLeft;
            if (!mentionsArrow && !mentionsDirection)
                return int.MinValue;

            int score = mentionsArrow ? 100 : 0;
            if (mentionsDirection)
                score += 220;
            if (mentionsOpposite)
                score -= 280;

            RectTransform rect = candidate.rectTransform;
            if (rect != null) {
                Vector2 size = rect.rect.size;
                if (Mathf.Abs(size.x) <= nativeSettingsRowSize.y * 1.5f &&
                    Mathf.Abs(size.y) <= nativeSettingsRowSize.y * 1.5f)
                    score += 25;
                if (value != null) {
                    float delta = rect.position.x -
                        value.rectTransform.position.x;
                    if ((pointsLeft && delta < 0f) ||
                        (!pointsLeft && delta > 0f))
                        score += 35;
                }
            }
            return score;
        }

        private static void CaptureGlobalSettingsArrowSprites()
        {
            if (SettingsLeftArrow.IsValid &&
                SettingsRightArrow.IsValid)
                return;

            Il2CppReferenceArray<UnityEngine.Object> loaded =
                Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>());
            bool needLeft = !SettingsLeftArrow.IsValid;
            bool needRight = !SettingsRightArrow.IsValid;
            int bestLeft = int.MinValue;
            int bestRight = int.MinValue;
            foreach (UnityEngine.Object item in loaded) {
                Sprite sprite = item.TryCast<Sprite>();
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                    continue;
                string name = sprite.name.ToLowerInvariant();
                if (name.IndexOf("arrow",
                    StringComparison.Ordinal) < 0 &&
                    name.IndexOf("chevron",
                    StringComparison.Ordinal) < 0)
                    continue;

                int leftScore = ScoreSettingsArrowSprite(name, true);
                if (needLeft && leftScore > bestLeft) {
                    bestLeft = leftScore;
                    SettingsLeftArrow.Capture(sprite);
                }
                int rightScore = ScoreSettingsArrowSprite(name, false);
                if (needRight && rightScore > bestRight) {
                    bestRight = rightScore;
                    SettingsRightArrow.Capture(sprite);
                }
            }
        }

        private static int ScoreSettingsArrowSprite(string name,
            bool pointsLeft)
        {
            bool left = name.IndexOf("left",
                StringComparison.Ordinal) >= 0 ||
                name.IndexOf("prev",
                    StringComparison.Ordinal) >= 0;
            bool right = name.IndexOf("right",
                StringComparison.Ordinal) >= 0 ||
                name.IndexOf("next",
                    StringComparison.Ordinal) >= 0;
            bool direction = pointsLeft ? left : right;
            bool opposite = pointsLeft ? right : left;
            if (!direction)
                return int.MinValue;
            int score = 200;
            if (name.IndexOf("settings",
                StringComparison.Ordinal) >= 0)
                score += 60;
            if (opposite)
                score -= 300;
            return score;
        }

        private static int ScoreSettingsSource(SettingsButton candidate)
        {
            if (candidate == null || candidate.gameObject == null ||
                candidate.text == null)
                return int.MinValue;

            int score = 0;
            if (candidate.gameObject.scene.IsValid())
                score += 50;
            if (candidate.gameObject.activeInHierarchy)
                score += 40;
            if (candidate.hover != null)
                score += 30;
            if (candidate.selected != null)
                score += 30;

            Transform hover = candidate.hover != null
                ? candidate.hover.transform : null;
            Transform selected = candidate.selected != null
                ? candidate.selected.transform : null;
            Text value = FindSettingsValueText(candidate.transform,
                candidate.text, hover, selected);
            if (value != null) {
                score += 120;
                if (IsBooleanSettingsValue(value.text))
                    score += 180;
            }

            Text[] texts = candidate.GetComponentsInChildren<Text>(true);
            int mainTexts = 0;
            for (int i = 0; i < texts.Length; i++) {
                if (texts[i] != null &&
                    !IsInside(texts[i].transform, hover) &&
                    !IsInside(texts[i].transform, selected))
                    mainTexts++;
            }
            if (mainTexts == 2)
                score += 80;
            else if (mainTexts > 2)
                score -= (mainTexts - 2) * 20;

            string path = GetHierarchyPath(candidate.transform);
            if (path.IndexOf("Settings",
                StringComparison.OrdinalIgnoreCase) >= 0)
                score += 40;
            return score;
        }

        private static bool IsBooleanSettingsValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            string normalized = value.Trim().ToLowerInvariant();
            return normalized == "yes" || normalized == "no" ||
                normalized == "on" || normalized == "off" ||
                normalized == "да" || normalized == "нет" ||
                normalized == "вкл" || normalized == "выкл";
        }

        private static string GetHierarchyPath(Transform item)
        {
            if (item == null)
                return string.Empty;
            StringBuilder builder = new StringBuilder();
            Transform current = item;
            while (current != null) {
                if (builder.Length > 0)
                    builder.Insert(0, '/');
                builder.Insert(0, current.gameObject.name);
                current = current.parent;
            }
            return builder.ToString();
        }

        private static void CaptureHintStyles(
            TutorialsWindow tutorialsWindow)
        {
            int bestScore = int.MinValue;
            if (tutorialsWindow != null &&
                tutorialsWindow.uiDescription != null) {
                ControlDescription[] local = tutorialsWindow.uiDescription
                    .GetComponentsInChildren<ControlDescription>(true);
                for (int i = 0; i < local.Length; i++) {
                    int score = ScoreHint(local[i], true);
                    if (score > bestScore) {
                        bestScore = score;
                        hintSource = local[i];
                    }
                }
            }

            Il2CppReferenceArray<UnityEngine.Object> loaded =
                Resources.FindObjectsOfTypeAll(
                    Il2CppType.Of<ControlDescription>());
            foreach (UnityEngine.Object item in loaded) {
                ControlDescription candidate =
                    item.TryCast<ControlDescription>();
                int score = ScoreHint(candidate, false);
                if (score > bestScore) {
                    bestScore = score;
                    hintSource = candidate;
                }
            }

            CaptureResolvedHintStyle();
        }

        private static void CaptureResolvedHintStyle()
        {
            if (hintSource == null)
                return;

            RectTransform hintRect =
                hintSource.GetComponent<RectTransform>();
            HintRect.Capture(hintRect);
            if (hintRect != null) {
                Vector2 rendered = GetSizeRelativeToTarget(
                    hintRect, hintRect.parent);
                if (rendered.y > 4f)
                    nativeHintHeight = rendered.y;
            }

            HintKeyImage.Capture(hintSource.buttonImage);
            RectTransform sourceKeyRect = hintSource.buttonImage != null
                ? hintSource.buttonImage.rectTransform : null;
            HintKeyRect.Capture(sourceKeyRect);
            if (sourceKeyRect != null) {
                Vector2 size = GetSizeRelativeToTarget(
                    sourceKeyRect, hintSource.transform.parent);
                if (size.x >= 8f && size.y >= 8f)
                    nativeHintKeySize = size;
            }

            Text hintText = null;
            if (hintSource.texts != null && hintSource.texts.Length > 0)
                hintText = hintSource.texts[0];
            HintText.Capture(hintText);
            if (hintText != null) {
                nativeHintFontSize = hintText.fontSize;
                hintNormalColor = hintText.canvasRenderer != null
                    ? hintText.canvasRenderer.GetColor()
                    : hintText.color;
            }
            if (sourceKeyRect != null && hintText != null)
                hintSpacing = ResolveHintSpacingRelativeToTarget(
                    sourceKeyRect, hintText, hintSource.transform.parent);
            if (BaseText.Font == null)
                BaseText.Capture(hintText);
        }

        private static int ScoreHint(ControlDescription candidate,
            bool local)
        {
            if (candidate == null || candidate.gameObject == null ||
                candidate.buttonImage == null)
                return int.MinValue;
            int score = local ? 200 : 0;
            if (candidate.gameObject.scene.IsValid())
                score += 50;
            if (candidate.gameObject.activeInHierarchy)
                score += 50;
            if (candidate.texts != null && candidate.texts.Length > 0)
                score += 30;
            return score;
        }

        private static void EnsureFallbacks(MainMenuButton launchButton)
        {
            Text fallbackText = launchButton != null
                ? launchButton.text : null;
            if (BaseText.Font == null && fallbackText != null)
                BaseText.Capture(fallbackText);
            if (CardText.Font == null)
                CardText.Capture(fallbackText);
            if (SettingsText.Font == null)
                SettingsText.Capture(fallbackText);
            if (HintText.Font == null)
                HintText.Capture(fallbackText);

            if (BaseText.Font == null)
                BaseText.Font = Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");
            if (CardText.Font == null)
                CardText.Font = BaseText.Font;
            if (SettingsText.Font == null)
                SettingsText.Font = BaseText.Font;
            if (HintText.Font == null)
                HintText.Font = BaseText.Font;

            if (WindowBackground.Color.a <= 0f)
                WindowBackground.Color = new Color(0.04f, 0.04f,
                    0.04f, 0.82f);
            if (CardBackground.Color.a <= 0f)
                CardBackground.Color = new Color(0.5f, 0.5f,
                    0.5f, 0.82f);
            if (SettingsBackground.Color.a <= 0f)
                SettingsBackground.Color = settingsNormalColor;
            if (HintKeyImage.Color.a <= 0f)
                HintKeyImage.Color = Color.white;

            if (nativeCardSize.x < 40f || nativeCardSize.y < 40f)
                nativeCardSize = new Vector2(133f, 96f);
            if (nativeCardsAreaSize.x < nativeCardSize.x ||
                nativeCardsAreaSize.y < nativeCardSize.y)
                nativeCardsAreaSize = new Vector2(571f, 425f);
            if (nativeSettingsRowSize.y < 24f ||
                nativeSettingsRowSize.y > 100f)
                nativeSettingsRowSize.y = 48f;
            if (nativeHintKeySize.x < 12f ||
                nativeHintKeySize.y < 12f)
                nativeHintKeySize = new Vector2(14.4f, 14.4f);
            if (nativeHintHeight < nativeHintKeySize.y)
                nativeHintHeight = nativeHintKeySize.y;
            if (nativeHintFontSize < 8 || nativeHintFontSize > 28)
                nativeHintFontSize = Mathf.Clamp(
                    HintText.FontSize, 10, 18);
        }

        private static TutorialItem ResolveModCardIconSource(
            TutorialsWindow window, TutorialItem preferred,
            TutorialItem fallback)
        {
            if (window == null || window.tutorialItems == null)
                return preferred ?? fallback;

            if (modCardIconTutorialIndex >= 0 &&
                modCardIconTutorialIndex < window.tutorialItems.Length) {
                TutorialItem cached =
                    window.tutorialItems[modCardIconTutorialIndex];
                if (cached != null && cached.gameObject != null)
                    return cached;
            }

            TutorialItem source = preferred ?? fallback;
            if (source == null)
                return null;
            for (int i = 0; i < window.tutorialItems.Length; i++) {
                TutorialItem item = window.tutorialItems[i];
                if (item != null && item.gameObject == source.gameObject) {
                    modCardIconTutorialIndex = i;
                    break;
                }
            }
            return source;
        }

        private static TutorialItem FindTutorialCard(
            TutorialsWindow window, bool workbenchOnly)
        {
            if (window == null || window.tutorialItems == null)
                return null;
            TutorialItem fallback = null;
            for (int i = 0; i < window.tutorialItems.Length; i++) {
                TutorialItem item = window.tutorialItems[i];
                if (item == null || item.gameObject == null)
                    continue;
                if (fallback == null)
                    fallback = item;
                if (!workbenchOnly)
                    return item;
                Text[] texts = item.GetComponentsInChildren<Text>(true);
                for (int j = 0; j < texts.Length; j++) {
                    string value = texts[j] != null
                        ? texts[j].text : null;
                    if (string.IsNullOrEmpty(value))
                        continue;
                    if (value.IndexOf("ВЕРСТАК",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("WORKBENCH",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        return item;
                }
            }
            return workbenchOnly ? null : fallback;
        }

        private static Image FindFirstImage(Transform root,
            string exactName, string[] excludedNames)
        {
            if (root == null)
                return null;
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++) {
                Image image = images[i];
                if (image == null || image.gameObject == null)
                    continue;
                string name = image.gameObject.name;
                if (!string.IsNullOrEmpty(exactName) &&
                    !string.Equals(name, exactName,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                bool excluded = false;
                if (excludedNames != null) {
                    for (int j = 0; j < excludedNames.Length; j++) {
                        if (name.IndexOf(excludedNames[j],
                                StringComparison.OrdinalIgnoreCase) >= 0) {
                            excluded = true;
                            break;
                        }
                    }
                }
                if (!excluded)
                    return image;
            }
            return null;
        }

        private static Text FindFirstText(Transform root,
            string exactName)
        {
            if (root == null)
                return null;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) {
                Text text = texts[i];
                if (text == null || text.gameObject == null)
                    continue;
                if (string.IsNullOrEmpty(exactName) ||
                    string.Equals(text.gameObject.name, exactName,
                        StringComparison.OrdinalIgnoreCase))
                    return text;
            }
            return null;
        }

        private static Sprite FindPcButtonSprite(string key)
        {
            string[] names;
            if (string.Equals(key, "Enter",
                StringComparison.OrdinalIgnoreCase))
                names = new string[] { "enter", "return" };
            else if (string.Equals(key, "Esc",
                StringComparison.OrdinalIgnoreCase))
                names = new string[] { "esc", "escape" };
            else if (string.Equals(key, "R",
                StringComparison.OrdinalIgnoreCase))
                names = new string[] { "r", "keyr", "keyboardr",
                    "rkey" };
            else if (string.Equals(key, "Shift",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Shift L",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "LeftShift",
                    StringComparison.OrdinalIgnoreCase))
                names = new string[] { "leftshift", "shiftleft", "shift" };
            else if (string.Equals(key, "Shift R",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "RightShift",
                    StringComparison.OrdinalIgnoreCase))
                names = new string[] { "rightshift", "shiftright", "shift" };
            else if (string.Equals(key, "Alt L",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "LeftAlt",
                    StringComparison.OrdinalIgnoreCase))
                names = new string[] { "leftalt", "altleft", "alt" };
            else if (string.Equals(key, "Space",
                StringComparison.OrdinalIgnoreCase))
                names = new string[] { "spacebar", "space" };
            else if (string.Equals(key, "MouseRight",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "RMB",
                    StringComparison.OrdinalIgnoreCase))
                names = new string[] { "mouseright", "rightmouse",
                    "mouserightbutton", "rightmousebutton",
                    "mousebuttonright", "mouserightclick", "rightclick",
                    "rmb" };
            else
                names = new string[] { key };

            if (GameInventory.Instance == null ||
                GameInventory.Instance.buttonSpritesProvider == null ||
                GameInventory.Instance.buttonSpritesProvider.PCButtons == null)
                return null;

            foreach (SpriteContainer container in
                GameInventory.Instance.buttonSpritesProvider.PCButtons) {
                if (container.sprite == null)
                    continue;
                string containerName = Normalize(container.name);
                string spriteName = Normalize(container.sprite.name);
                for (int i = 0; i < names.Length; i++) {
                    string wanted = Normalize(names[i]);
                    if (containerName == wanted || spriteName == wanted)
                        return container.sprite;
                }
            }

            foreach (SpriteContainer container in
                GameInventory.Instance.buttonSpritesProvider.PCButtons) {
                if (container.sprite == null)
                    continue;
                string containerName = Normalize(container.name);
                string spriteName = Normalize(container.sprite.name);
                for (int i = 0; i < names.Length; i++) {
                    string wanted = Normalize(names[i]);
                    if (wanted.Length <= 1)
                        continue;
                    if (containerName.IndexOf(wanted,
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        spriteName.IndexOf(wanted,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        return container.sprite;
                }
            }
            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++) {
                char character = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }
            return builder.ToString();
        }

    }

    [HarmonyPatch]
    internal static class NativeHoldFooterPointerPatch
    {
        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerEnter))]
        [HarmonyPostfix]
        private static void OnPointerEnterPostfix(EventTrigger __instance)
        {
            NativeUiFactory.OnNativeHoldPointerEnter(__instance);
        }

        [HarmonyPatch(typeof(EventTrigger), nameof(EventTrigger.OnPointerExit))]
        [HarmonyPostfix]
        private static void OnPointerExitPostfix(EventTrigger __instance)
        {
            NativeUiFactory.OnNativeHoldPointerExit(__instance);
        }
    }
}
