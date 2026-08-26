using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppInterop.Runtime;
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
#else
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using CMS.Containers;
using CMS.UI.Logic;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class ShoppingListQuantityFeature
    {
        private const string MinusButtonName = "QQuantityMinus";
        private const string PlusButtonName = "QQuantityPlus";
        private const string SymbolName = "Symbol";
        private const float ButtonGap = 2f;
        private const float AmountGap = 3.5f;
        private static readonly Color HoverColor =
            new Color(1f, 0.65f, 0.04f, 1f);
        private static ShopListWindow activeWindow;

        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addShoppingListSorting;
            }
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        private static void ShowPostfix(ShopListWindow __instance, bool __result)
        {
            if (__result && IsEnabled)
                activeWindow = __instance;
        }

        [HarmonyPatch(typeof(ShopListWindow), "HandleInput")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool HandleInputPrefix(ShopListWindow __instance)
        {
            bool keypadPlus = Input.GetKeyDown(KeyCode.KeypadPlus);
            bool plus = Input.GetKeyDown(KeyCode.Plus);
            bool equals = Input.GetKeyDown(KeyCode.Equals);
            bool keypadMinus = Input.GetKeyDown(KeyCode.KeypadMinus);
            bool minus = Input.GetKeyDown(KeyCode.Minus);
            bool remove = Input.GetKeyDown(KeyCode.X);
            bool shift = Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
            if (__instance == null || __instance != activeWindow ||
                !ShoppingListBackend.IsOpen(__instance))
                return true;

            int delta = 0;
            if (!remove) {
                if (keypadPlus || plus || (equals && shift))
                    delta = 1;
                else if (keypadMinus || minus)
                    delta = -1;
                else
                    return true;
            }

            ShoppingListBackendEntry entry =
                ShoppingListBackend.GetCurrentSelectedEntry(__instance);

            if (entry != null && entry.Data != null) {
                if (remove)
                    ShoppingListBackend.Remove(__instance, entry.Data);
                else
                    AdjustQuantity(__instance, entry.Data, delta);
            }

            Input.ResetInputAxes();
            return false;
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Hide))]
        [HarmonyPostfix]
        private static void HidePostfix(ShopListWindow __instance)
        {
            if (__instance == activeWindow)
                activeWindow = null;
        }

        private static bool IsPlusPressed()
        {
            if (Input.GetKeyDown(KeyCode.KeypadPlus) ||
                Input.GetKeyDown(KeyCode.Plus))
                return true;

            return Input.GetKeyDown(KeyCode.Equals) &&
                (Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift));
        }

        private static bool IsMinusPressed()
        {
            return Input.GetKeyDown(KeyCode.KeypadMinus) ||
                Input.GetKeyDown(KeyCode.Minus);
        }

        internal static void UpdateRow(ShopListWindow window,
            ShopListItem row, ShopListItemData data)
        {
            if (!IsEnabled) {
                RestoreNativeDelete(row);
                return;
            }
            if (window == null || row == null || data == null ||
                row.trashButton == null)
                return;

            try {
                Button minusButton = EnsureQuantityButton(
                    row, MinusButtonName, "-");
                Button plusButton = EnsureQuantityButton(
                    row, PlusButtonName, "+");
                if (minusButton == null || plusButton == null) {
                    RestoreNativeDelete(row);
                    return;
                }

                BindQuantityButton(minusButton, window, row, data, -1);
                BindQuantityButton(plusButton, window, row, data, 1);
                if (ShoppingListPresentationFeature.IsCardPresentationActive(row)) {
                    ShoppingListPresentationFeature.UpdateQuantityText(row, data);
                    LayoutCardControls(row, minusButton, plusButton);
                } else {
                    UpdateAmountText(row, data.Amount);
                    LayoutControls(row, minusButton, plusButton);
                }
                minusButton.interactable = data.Amount > 1;
                plusButton.interactable = data.Amount < int.MaxValue;

                row.trashButton.gameObject.SetActive(true);
                minusButton.gameObject.SetActive(true);
                plusButton.gameObject.SetActive(true);
            } catch (Exception exception) {
                RestoreNativeDelete(row);
                ModLogger.Log(
                    "[ShoppingList] Failed to prepare quantity controls." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
            }
        }

        internal static void HideRow(ShopListItem row)
        {
            if (row == null)
                return;

            Transform minus = row.transform.Find(MinusButtonName);
            if (minus != null)
                minus.gameObject.SetActive(false);
            Transform plus = row.transform.Find(PlusButtonName);
            if (plus != null)
                plus.gameObject.SetActive(false);
            if (row.trashButton != null)
                row.trashButton.gameObject.SetActive(false);
        }

        private static Button EnsureQuantityButton(ShopListItem row,
            string name, string symbol)
        {
            Transform existing = row.transform.Find(name);
            Button button = existing != null
                ? existing.GetComponent<Button>() : null;
            if (button != null) {
                SetSymbol(button.transform, symbol);
                return button;
            }

#if NET6_0_OR_GREATER
            GameObject buttonObject = new GameObject(name,
                typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> componentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            componentTypes[0] = Il2CppType.Of<RectTransform>();
            GameObject buttonObject = new GameObject(name, componentTypes);
#endif
            buttonObject.transform.SetParent(row.transform, false);
            buttonObject.layer = row.gameObject.layer;
            buttonObject.transform.localScale = Vector3.one;

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text sourceText = row.amount;
            if (sourceText == null) {
                UnityEngine.Object.Destroy(buttonObject);
                return null;
            }

#if NET6_0_OR_GREATER
            GameObject symbolObject = new GameObject(SymbolName,
                typeof(RectTransform));
#else
            Il2CppReferenceArray<Il2CppSystem.Type> symbolComponentTypes =
                new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            symbolComponentTypes[0] = Il2CppType.Of<RectTransform>();
            GameObject symbolObject = new GameObject(
                SymbolName, symbolComponentTypes);
#endif
            symbolObject.transform.SetParent(buttonObject.transform, false);
            symbolObject.layer = buttonObject.layer;
            symbolObject.transform.localScale = Vector3.one;

            Text symbolText = symbolObject.AddComponent<Text>();
            RectTransform symbolRect =
                symbolObject.GetComponent<RectTransform>();
            if (symbolText == null || symbolRect == null) {
                UnityEngine.Object.Destroy(buttonObject);
                return null;
            }

            symbolRect.anchorMin = Vector2.zero;
            symbolRect.anchorMax = Vector2.one;
            symbolRect.pivot = new Vector2(0.5f, 0.5f);
            symbolRect.anchoredPosition = Vector2.zero;
            symbolRect.offsetMin = Vector2.zero;
            symbolRect.offsetMax = Vector2.zero;
            CopyTextStyle(sourceText, symbolText);
            symbolText.text = symbol;
            symbolText.alignment = TextAnchor.MiddleCenter;
            symbolText.fontSize = Math.Max(sourceText.fontSize, 14);
            symbolText.resizeTextForBestFit = false;
            symbolText.horizontalOverflow = HorizontalWrapMode.Overflow;
            symbolText.verticalOverflow = VerticalWrapMode.Overflow;
            symbolText.raycastTarget = false;
            symbolObject.SetActive(true);

            button.targetGraphic = symbolText;
            button.transition = Selectable.Transition.ColorTint;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            ColorBlock colors = button.colors;
            colors.normalColor = sourceText.color;
            colors.highlightedColor = HoverColor;
            colors.pressedColor = HoverColor;
            colors.selectedColor = sourceText.color;
            colors.disabledColor = new Color(sourceText.color.r,
                sourceText.color.g, sourceText.color.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            buttonObject.SetActive(true);
            return button;
        }

        private static void BindQuantityButton(Button button,
            ShopListWindow window, ShopListItem row, ShopListItemData data,
            int delta)
        {
            if (button == null || data == null)
                return;

            button.onClick.RemoveAllListeners();
            Action clickAction = delegate () {
                AdjustQuantity(window, data, delta);
            };
            UnityAction unityAction =
                DelegateSupport.ConvertDelegate<UnityAction>(clickAction);
            button.onClick.AddListener(unityAction);
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
        }

        private static void RestoreNativeDelete(ShopListItem row)
        {
            if (row == null)
                return;
            Transform minus = row.transform.Find(MinusButtonName);
            if (minus != null)
                minus.gameObject.SetActive(false);
            Transform plus = row.transform.Find(PlusButtonName);
            if (plus != null)
                plus.gameObject.SetActive(false);
            if (row.trashButton != null)
                row.trashButton.gameObject.SetActive(true);
        }

        private static void SetSymbol(Transform buttonTransform,
            string symbol)
        {
            if (buttonTransform == null)
                return;
            Text text = buttonTransform.Find(SymbolName)?.GetComponent<Text>();
            if (text != null && text.text != symbol)
                text.text = symbol;
        }

        private static void LayoutCardControls(ShopListItem row,
            Button minusButton, Button plusButton)
        {
            RectTransform rowRect = row.GetComponent<RectTransform>();
            RectTransform trashRect = row.trashButton != null
                ? row.trashButton.GetComponent<RectTransform>() : null;
            RectTransform minusRect = minusButton != null
                ? minusButton.GetComponent<RectTransform>() : null;
            RectTransform plusRect = plusButton != null
                ? plusButton.GetComponent<RectTransform>() : null;
            RectTransform amountRect = row.amount != null
                ? row.amount.GetComponent<RectTransform>() : null;
            if (rowRect == null || trashRect == null || minusRect == null ||
                plusRect == null || amountRect == null || rowRect.rect.width <= 0f)
                return;

            const float margin = 2f;
            const float controlHeight = 18f;
            const float buttonWidth = 16f;
            const float gap = 1.5f;

            ConfigureBottomButton(trashRect, margin, buttonWidth,
                controlHeight);
            ConfigureBottomButton(plusRect,
                margin + buttonWidth + gap, buttonWidth, controlHeight);
            ConfigureBottomButton(minusRect,
                margin + ((buttonWidth + gap) * 2f), buttonWidth,
                controlHeight);

            float controlsWidth = margin + (buttonWidth * 3f) + (gap * 2f);
            amountRect.anchorMin = new Vector2(0f, 0f);
            amountRect.anchorMax = new Vector2(0f, 0f);
            amountRect.pivot = new Vector2(0f, 0f);
            amountRect.anchoredPosition = new Vector2(margin, 0f);
            amountRect.sizeDelta = new Vector2(
                Math.Max(1f, rowRect.rect.width - controlsWidth - margin),
                controlHeight);
            row.amount.alignment = TextAnchor.MiddleLeft;
            row.amount.resizeTextForBestFit = true;
            row.amount.resizeTextMinSize = 6;
            row.amount.resizeTextMaxSize = Math.Max(8, row.amount.fontSize);
            row.amount.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.amount.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void ConfigureBottomButton(RectTransform rect,
            float rightOffset, float width, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-rightOffset, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void LayoutControls(ShopListItem row,
            Button minusButton, Button plusButton)
        {
            RectTransform rowRect = row.GetComponent<RectTransform>();
            RectTransform trashRect = row.trashButton != null
                ? row.trashButton.GetComponent<RectTransform>() : null;
            RectTransform minusRect = minusButton.GetComponent<RectTransform>();
            RectTransform plusRect = plusButton.GetComponent<RectTransform>();
            RectTransform amountRect = row.amount != null
                ? row.amount.GetComponent<RectTransform>() : null;
            if (rowRect == null || trashRect == null || minusRect == null ||
                plusRect == null || amountRect == null ||
                rowRect.rect.width <= 0f)
                return;

            CopyButtonRect(trashRect, plusRect);
            CopyButtonRect(trashRect, minusRect);

            float buttonWidth = Mathf.Max(1f, trashRect.rect.width);
            Vector2 plusPosition = trashRect.anchoredPosition;
            plusPosition.x -= buttonWidth + ButtonGap;
            plusRect.anchoredPosition = plusPosition;

            Vector2 minusPosition = plusPosition;
            minusPosition.x -= buttonWidth + ButtonGap;
            minusRect.anchoredPosition = minusPosition;

            float minusLeft = rowRect.rect.width + minusRect.anchoredPosition.x -
                (minusRect.rect.width * minusRect.pivot.x);
            float amountWidth = amountRect.rect.width;
            Vector2 amountPosition = amountRect.anchoredPosition;
            amountPosition.x = minusLeft - AmountGap - amountWidth;
            amountRect.anchoredPosition = amountPosition;
        }

        private static void CopyButtonRect(RectTransform source,
            RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
        }

        private static void UpdateAmountText(ShopListItem row, int amount)
        {
            if (row == null || row.amount == null)
                return;

            string text = row.amount.text ?? string.Empty;
            int numberStart = -1;
            int numberEnd = -1;
            for (int i = 0; i < text.Length; i++) {
                if (!char.IsDigit(text[i])) {
                    if (numberStart >= 0)
                        break;
                    continue;
                }

                if (numberStart < 0)
                    numberStart = i;
                numberEnd = i + 1;
            }

            string amountText = amount.ToString();
            row.amount.text = numberStart >= 0
                ? text.Substring(0, numberStart) + amountText +
                    text.Substring(numberEnd)
                : amountText;
        }

        private static bool AdjustQuantity(ShopListWindow window,
            ShopListItemData entry, int delta)
        {
            if (window == null || entry == null || delta == 0)
                return false;

            try {
                ShopListItemData current;
                return ShoppingListBackend.TryAdjustAmount(
                    window, entry, delta, out current);
            } catch (Exception exception) {
                ModLogger.Log(
                    "[ShoppingList] Failed to adjust requested quantity." +
                    Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                return false;
            }
        }

    }
}
