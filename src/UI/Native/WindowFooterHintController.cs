using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppInterop.Runtime;
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.Paging;
#else
using UnhollowerRuntimeLib;
using CMS.UI.Description;
using CMS.UI.Logic;
using CMS.UI.Logic.Paging;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Owns mod footer hints per native window. Features register intent;
    /// this controller creates, orders, positions and destroys the UI objects.
    /// </summary>
    internal static class WindowFooterHintController
    {
        private const float SecondRowVerticalPadding = 5f;
        private const float FallbackHorizontalSpacing = 15f;

        internal enum NativeFooterProfile
        {
            Automatic,
            InventoryPopulated,
            InventoryEmpty,
            WarehouseInventoryPopulated,
            WarehouseInventoryEmpty,
            WarehouseStoragePopulated,
            WarehouseStorageEmpty,
            TravelPopulated,
            TravelEmpty,
            RepairPopulated,
            RepairEmpty,
            ScrapProductionPopulated,
            ScrapProductionEmpty,
            ScrapUpgradePopulated,
            ScrapUpgradeEmpty,
        }

        internal sealed class NativeHintRequest
        {
            public string WindowId;
            public Transform WindowRoot;
            public Transform HintRoot;
            public string HintId;
            public string[] Keys;
            public string Text;
            public Action Action;
            public ControlDescription Source;
            public int Row;
            public int Order;
            public NativeFooterProfile Profile;
            public int ItemCount = -1;
            public ControlDescription VariantSource;
            public DescriptionInputHandlingMethod InputHandlingMethod =
                DescriptionInputHandlingMethod.ButtonDown;
            public bool CanHold;
            public float TimeToHold;
            public bool OnlyHandleMouseClickInput = true;
            public bool AllowAutomaticRowWrap = true;
        }

        private sealed class HintEntry
        {
            public string Id;
            public int Row;
            public int Order;
            public NativeUiFactory.ControlHintRowHandle FactoryRow;
            public NativeUiFactory.FooterHintHandle Hint;
            public bool AllowAutomaticRowWrap;
        }

        private sealed class WindowState
        {
            public string Id;
            public Transform Root;
            public Transform HintRoot;
            public ControlDescription Source;
            public RectTransform Parent;
            public readonly List<HintEntry> Entries = new List<HintEntry>();
            public bool Suspended;
            public NativeFooterProfile Profile;
            public int ItemCount = -1;
            public bool PendingRenderAudit;
            public int EarliestLayoutFrame;
            public bool NativeLayoutChangedWhileSuspended;
        }

        private sealed class NativeRowMetrics
        {
            public float StartX;
            public float BottomY;
            public float Height;
            public float NativeLength;
            public float Spacing;
            public float WindowRight;
            public float ReservedLeft;
            public float ReservedWidth;
            public int VisibleCount;
        }

        private sealed class FooterLayoutInput
        {
            public float NativeStartX;
            public float NativeBottomY;
            public float NativeLength;
            public float PaginationStartX;
            public float PaginationLength;
            public float Spacing;
        }

        private sealed class FooterHintLayoutInput
        {
            public HintEntry Entry;
            public float Width;
            public float Height;
            public int RequestedRow;
            public bool AllowAutomaticRowWrap;
        }

        private sealed class FooterHintPlacement
        {
            public FooterHintLayoutInput Hint;
            public float Left;
            public float Bottom;
            public int Row;
        }

        private sealed class StyledEntry
        {
            public string Id;
            public int Order;
            public NativeUiFactory.FooterHintHandle Hint;
        }

        private sealed class StyledState
        {
            public RectTransform Parent;
            public float UsedWidth;
            public readonly List<StyledEntry> Entries =
                new List<StyledEntry>();
        }

        private static readonly Dictionary<string, WindowState> Windows =
            new Dictionary<string, WindowState>(StringComparer.Ordinal);
        private static readonly Dictionary<string, StyledState> StyledRows =
            new Dictionary<string, StyledState>(StringComparer.Ordinal);
        private static bool renderAuditSubscribed;
        private static bool renderAuditPass;
        private static bool creatingManagedHint;
        private static readonly Canvas.WillRenderCanvases RenderAuditHandler =
            DelegateSupport.ConvertDelegate<Canvas.WillRenderCanvases>(
                new Action(OnWillRenderCanvases));
        private static void EnsureRenderAuditSubscribed()
        {
            if (renderAuditSubscribed)
                return;
            Canvas.add_willRenderCanvases(RenderAuditHandler);
            renderAuditSubscribed = true;
        }

        internal static NativeUiFactory.FooterHintHandle RequestStyledHint(
            string windowId, RectTransform parent, string hintId,
            string[] keys, string text, Action action, int order)
        {
            if (string.IsNullOrEmpty(windowId) || parent == null ||
                string.IsNullOrEmpty(hintId))
                return null;
            StyledState state;
            if (!StyledRows.TryGetValue(windowId, out state) ||
                state.Parent != parent) {
                ClearStyledWindow(windowId);
                state = new StyledState { Parent = parent };
                StyledRows[windowId] = state;
            }
            StyledEntry entry = null;
            bool layoutChanged = false;
            for (int i = 0; i < state.Entries.Count; i++) {
                if (state.Entries[i].Id == hintId) {
                    entry = state.Entries[i];
                    break;
                }
            }
            if (entry == null) {
                NativeUiFactory.FooterHintHandle hint =
                    NativeUiFactory.CreateFooterHint(parent, keys, text,
                        action);
                if (hint == null)
                    return null;
                entry = new StyledEntry {
                    Id = hintId,
                    Hint = hint,
                };
                state.Entries.Add(entry);
                layoutChanged = true;
            } else {
                layoutChanged = !string.Equals(entry.Hint.Text, text,
                    StringComparison.Ordinal);
                NativeUiFactory.UpdateFooterHint(entry.Hint, text, true);
            }
            layoutChanged |= entry.Order != order;
            entry.Order = order;
            if (layoutChanged)
                LayoutStyledRow(state);
            return entry.Hint;
        }

        internal static void ClearStyledWindow(string windowId)
        {
            StyledState state;
            if (!StyledRows.TryGetValue(windowId, out state))
                return;
            for (int i = state.Entries.Count - 1; i >= 0; i--)
                NativeUiFactory.DestroyFooterHint(state.Entries[i].Hint);
            state.Entries.Clear();
            StyledRows.Remove(windowId);
        }

        internal static float GetStyledUsedWidth(string windowId)
        {
            StyledState state;
            return StyledRows.TryGetValue(windowId, out state)
                ? state.UsedWidth : 0f;
        }

        internal static NativeUiFactory.FooterHintHandle RequestNativeHint(
            NativeHintRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.WindowId) ||
                string.IsNullOrEmpty(request.HintId) ||
                request.WindowRoot == null)
                return null;

            EnsureRenderAuditSubscribed();

            WindowState state;
            if (!Windows.TryGetValue(request.WindowId, out state) ||
                state.Root != request.WindowRoot) {
                ClearWindow(request.WindowId);
                state = new WindowState {
                    Id = request.WindowId,
                    Root = request.WindowRoot,
                    HintRoot = request.HintRoot,
                };
                Windows[request.WindowId] = state;
            }
            if (state.HintRoot != request.HintRoot) {
                ClearWindow(request.WindowId);
                state = new WindowState {
                    Id = request.WindowId,
                    Root = request.WindowRoot,
                    HintRoot = request.HintRoot,
                };
                Windows[request.WindowId] = state;
            }

            if (!ResolveNativeHost(state))
                return null;

            HintEntry entry = FindEntry(state, request.HintId);
            bool layoutChanged = false;
            if (entry == null) {
                NativeUiFactory.ControlHintRowHandle factoryRow;
                creatingManagedHint = true;
                try {
                    factoryRow = NativeUiFactory.CreateNativeFooterHint(
                            request.Source != null
                                ? request.Source : state.Source,
                            request.HintId, request.Keys, request.Text,
                            request.Action, request.VariantSource,
                            request.InputHandlingMethod, request.CanHold,
                            request.TimeToHold,
                            request.OnlyHandleMouseClickInput);
                } finally {
                    creatingManagedHint = false;
                }
                NativeUiFactory.FooterHintHandle hint = factoryRow != null &&
                    factoryRow.Hints.Count > 0 ? factoryRow.Hints[0] : null;
                if (hint == null) {
                    if (factoryRow != null)
                        NativeUiFactory.DestroyControlHintRow(factoryRow);
                    return null;
                }

                LayoutElement layout = hint.Root.GetComponent<LayoutElement>();
                if (layout == null)
                    layout = hint.Root.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                entry = new HintEntry {
                    Id = request.HintId,
                    FactoryRow = factoryRow,
                    Hint = hint,
                };
                state.Entries.Add(entry);
                layoutChanged = true;
            } else {
                layoutChanged = !string.Equals(entry.Hint.Text,
                    request.Text, StringComparison.Ordinal);
                NativeUiFactory.UpdateFooterHint(entry.Hint, request.Text,
                    true);
            }

            if (request.Profile != NativeFooterProfile.Automatic)
                state.Profile = request.Profile;
            if (request.ItemCount >= 0)
                state.ItemCount = request.ItemCount;

            layoutChanged |= entry.Row != request.Row ||
                entry.Order != request.Order ||
                entry.AllowAutomaticRowWrap != request.AllowAutomaticRowWrap;
            entry.Row = request.Row;
            entry.Order = request.Order;
            entry.AllowAutomaticRowWrap = request.AllowAutomaticRowWrap;
            if (layoutChanged)
                ScheduleLayout(state);
            return entry.Hint;
        }

        internal static void RemoveHint(string windowId, string hintId)
        {
            WindowState state;
            if (!Windows.TryGetValue(windowId, out state))
                return;
            HintEntry entry = FindEntry(state, hintId);
            if (entry == null)
                return;
            NativeUiFactory.DestroyControlHintRow(entry.FactoryRow);
            state.Entries.Remove(entry);
            if (state.Entries.Count == 0)
                Windows.Remove(windowId);
            else
                ScheduleLayout(state);
        }

        internal static void OnNativeDescriptionLayoutChanged(
            ControlDescription description)
        {
            if (description == null || description.gameObject == null ||
                creatingManagedHint ||
                description.transform == null)
                return;
            foreach (WindowState state in Windows.Values) {
                if (state == null || state.HintRoot == null ||
                    !description.transform.IsChildOf(state.HintRoot))
                    continue;
                RequestNativeLayout(state);
            }
        }

        internal static void OnPaginationVisibilityChanged(
            PagedWindowBase pagedWindow)
        {
            if (pagedWindow == null || pagedWindow.transform == null)
                return;

            foreach (WindowState state in Windows.Values) {
                if (state == null || state.Root == null ||
                    (pagedWindow.transform != state.Root &&
                        !pagedWindow.transform.IsChildOf(state.Root)))
                    continue;
                RequestNativeLayout(state);
            }
        }

        private static void RequestNativeLayout(WindowState state)
        {
            if (state == null)
                return;
            if (state.Suspended) {
                state.NativeLayoutChangedWhileSuspended = true;
                return;
            }
            ScheduleLayout(state);
        }

        internal static void SetNativeProfile(string windowId,
            NativeFooterProfile profile, int itemCount = -1)
        {
            WindowState state;
            if (string.IsNullOrEmpty(windowId) ||
                !Windows.TryGetValue(windowId, out state))
                return;
            state.Profile = profile;
            if (itemCount >= 0)
                state.ItemCount = itemCount;
        }

        internal static void SuspendWindow(string windowId)
        {
            WindowState state;
            if (string.IsNullOrEmpty(windowId) ||
                !Windows.TryGetValue(windowId, out state))
                return;
            state.Suspended = true;
            SetHintsVisible(state, false);
        }

        internal static void ResumeWindow(string windowId)
        {
            WindowState state;
            if (string.IsNullOrEmpty(windowId) ||
                !Windows.TryGetValue(windowId, out state))
                return;
            state.Suspended = false;
            if (state.NativeLayoutChangedWhileSuspended) {
                state.NativeLayoutChangedWhileSuspended = false;
                ScheduleLayout(state);
            } else {
                SetHintsVisible(state, true);
            }
        }

        internal static void ClearWindow(string windowId)
        {
            WindowState state;
            if (!Windows.TryGetValue(windowId, out state))
                return;
            for (int i = state.Entries.Count - 1; i >= 0; i--)
                NativeUiFactory.DestroyControlHintRow(
                    state.Entries[i].FactoryRow);
            state.Entries.Clear();
            Windows.Remove(windowId);
        }

        internal static void UpdateLayouts()
        {
            foreach (StyledState state in StyledRows.Values)
                LayoutStyledRow(state);
        }

        private static void ScheduleLayout(WindowState state)
        {
            if (state == null)
                return;
            if (state.Suspended || state.Root == null ||
                !state.Root.gameObject.activeInHierarchy)
                return;
            int earliestFrame = Time.frameCount + 1;
            if (state.PendingRenderAudit &&
                state.EarliestLayoutFrame >= earliestFrame)
                return;
            state.PendingRenderAudit = true;
            state.EarliestLayoutFrame = Mathf.Max(
                state.EarliestLayoutFrame, earliestFrame);
            SetHintsVisible(state, false);
        }

        private static void OnWillRenderCanvases()
        {
            if (renderAuditPass)
                return;
            renderAuditPass = true;
            try {
                foreach (WindowState state in Windows.Values) {
                    if (state == null || !state.PendingRenderAudit ||
                        state.Suspended || state.Root == null ||
                        !state.Root.gameObject.activeInHierarchy ||
                        Time.frameCount < state.EarliestLayoutFrame)
                        continue;
                    state.PendingRenderAudit = false;
                    LayoutWindow(state);
                    SetHintsVisible(state, true);
                }
            } finally {
                renderAuditPass = false;
            }
        }

        private static void SetHintsVisible(WindowState state, bool visible)
        {
            if (state == null)
                return;
            for (int i = 0; i < state.Entries.Count; i++) {
                NativeUiFactory.FooterHintHandle hint =
                    state.Entries[i].Hint;
                if (hint != null && hint.CanvasGroup != null)
                    hint.CanvasGroup.alpha = visible ? 1f : 0f;
            }
        }

        private static HintEntry FindEntry(WindowState state, string id)
        {
            for (int i = 0; i < state.Entries.Count; i++) {
                if (string.Equals(state.Entries[i].Id, id,
                    StringComparison.Ordinal))
                    return state.Entries[i];
            }
            return null;
        }

        private static bool ResolveNativeHost(WindowState state)
        {
            if (state == null || state.Root == null ||
                state.HintRoot == null)
                return false;
            if (state.Source != null && state.Parent != null &&
                state.Source.gameObject.activeInHierarchy &&
                state.Parent.gameObject.activeInHierarchy)
                return true;

            state.Source = null;
            state.Parent = null;
            float leftmost = float.MaxValue;
            ControlDescription[] descriptions =
                state.HintRoot.GetComponentsInChildren<ControlDescription>(true);
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription candidate = descriptions[i];
                if (candidate == null || candidate.gameObject == null ||
                    !candidate.gameObject.activeInHierarchy ||
                    candidate.gameObject.name.StartsWith("Hint_",
                        StringComparison.Ordinal))
                    continue;
                RectTransform rect = candidate.GetComponent<RectTransform>();
                RectTransform parent = rect != null && rect.parent != null
                    ? rect.parent.GetComponent<RectTransform>() : null;
                if (rect == null || parent == null)
                    continue;
                Vector3 left = state.Root.InverseTransformPoint(
                    rect.TransformPoint(new Vector3(rect.rect.xMin,
                        rect.rect.center.y, 0f)));
                if (left.x >= leftmost)
                    continue;
                leftmost = left.x;
                state.Source = candidate;
                state.Parent = parent;
            }
            return state.Source != null && state.Parent != null;
        }

        private static void LayoutWindow(WindowState state)
        {
            if (!ResolveNativeHost(state))
                return;
            RectTransform sourceRect =
                state.Source.GetComponent<RectTransform>();
            if (sourceRect == null)
                return;

            state.Entries.Sort(delegate(HintEntry left, HintEntry right) {
                return left.Order.CompareTo(right.Order);
            });

            Bounds sourceBounds;
            bool hasSourceBounds = NativeUiFactory
                .TryGetControlDescriptionVisualBounds(state.Source,
                    state.Parent, out sourceBounds);
            NativeRowMetrics metrics = MeasureNativeRow(state, sourceRect,
                sourceBounds, hasSourceBounds);

            List<FooterHintLayoutInput> layoutHints =
                new List<FooterHintLayoutInput>();
            for (int i = 0; i < state.Entries.Count; i++) {
                HintEntry entry = state.Entries[i];
                RectTransform rect = entry.Hint != null
                    ? entry.Hint.Rect : null;
                if (rect == null || rect.parent != state.Parent)
                    continue;
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                entry.Hint.Root.transform.SetAsLastSibling();

                Bounds visualBounds;
                bool hasVisualBounds = NativeUiFactory
                    .TryGetControlHintVisualBounds(entry.Hint,
                        state.Parent, out visualBounds);
                float width = hasVisualBounds
                    ? Mathf.Max(1f, visualBounds.size.x)
                    : Mathf.Max(1f, Mathf.Abs(rect.rect.width));
                float height = hasVisualBounds
                    ? Mathf.Max(1f, visualBounds.size.y)
                    : Mathf.Max(1f, Mathf.Abs(rect.rect.height));
                Bounds buttonBounds = new Bounds();
                bool hasButtonBounds = entry.Hint.Description != null &&
                    entry.Hint.Description.buttonImage != null &&
                    NativeUiFactory.TryGetRectTransformBounds(
                        entry.Hint.Description.buttonImage.rectTransform,
                        state.Parent, out buttonBounds);
                float buttonLeft = hasVisualBounds
                    ? visualBounds.min.x
                    : hasButtonBounds ? buttonBounds.min.x : 0f;
                float visibleRight = hasVisualBounds
                    ? visualBounds.max.x : buttonLeft + width;
                float occupiedWidth = Mathf.Max(1f,
                    visibleRight - buttonLeft);
                layoutHints.Add(new FooterHintLayoutInput {
                    Entry = entry,
                    Width = occupiedWidth,
                    Height = height,
                    RequestedRow = entry.Row,
                    AllowAutomaticRowWrap = entry.AllowAutomaticRowWrap,
                });
            }

            FooterLayoutInput layoutInput = new FooterLayoutInput {
                NativeStartX = metrics.StartX,
                NativeBottomY = metrics.BottomY,
                NativeLength = metrics.NativeLength,
                PaginationStartX = metrics.ReservedLeft,
                PaginationLength = metrics.ReservedWidth,
                Spacing = metrics.Spacing,
            };
            List<FooterHintPlacement> placements = CalculateHintPositions(
                layoutInput, layoutHints);
            for (int i = 0; i < placements.Count; i++) {
                FooterHintPlacement placement = placements[i];
                HintEntry entry = placement.Hint.Entry;
                RectTransform rect = entry.Hint != null
                    ? entry.Hint.Rect : null;
                if (rect == null || rect.parent != state.Parent)
                    continue;
                Bounds visualBounds;
                bool hasVisualBounds = NativeUiFactory
                    .TryGetControlHintVisualBounds(entry.Hint,
                        state.Parent, out visualBounds);
                Bounds buttonBounds = new Bounds();
                bool hasButtonBounds = entry.Hint.Description != null &&
                    entry.Hint.Description.buttonImage != null &&
                    NativeUiFactory.TryGetRectTransformBounds(
                        entry.Hint.Description.buttonImage.rectTransform,
                        state.Parent, out buttonBounds);
                float buttonLeft = hasVisualBounds
                    ? visualBounds.min.x
                    : hasButtonBounds ? buttonBounds.min.x : 0f;
                if (hasVisualBounds) {
                    rect.anchoredPosition += new Vector2(
                        placement.Left - buttonLeft,
                        placement.Bottom - visualBounds.min.y);
                } else {
                    Vector3 rectBottomLeft = state.Parent
                        .InverseTransformPoint(rect.TransformPoint(
                            new Vector3(rect.rect.xMin, rect.rect.yMin,
                                0f)));
                    rect.anchoredPosition += new Vector2(
                        placement.Left - rectBottomLeft.x,
                        placement.Bottom - rectBottomLeft.y);
                }
            }
        }

        private static List<FooterHintPlacement> CalculateHintPositions(
            FooterLayoutInput input, List<FooterHintLayoutInput> hints)
        {
            List<FooterHintPlacement> result =
                new List<FooterHintPlacement>();
            if (input == null || hints == null)
                return result;

            float availableRight = input.PaginationStartX;
            float firstRowCursor = input.NativeStartX +
                input.NativeLength + (input.NativeLength > 0f
                    ? input.Spacing : 0f);
            float secondRowCursor = input.NativeStartX;
            bool secondRowStarted = false;
            for (int i = 0; i < hints.Count; i++) {
                FooterHintLayoutInput hint = hints[i];
                if (hint == null)
                    continue;
                bool useSecondRow = hint.RequestedRow > 0 ||
                    (hint.AllowAutomaticRowWrap && (secondRowStarted ||
                        firstRowCursor + hint.Width > availableRight));
                if (useSecondRow)
                    secondRowStarted = true;
                FooterHintPlacement placement = new FooterHintPlacement {
                    Hint = hint,
                    Row = useSecondRow ? 2 : 1,
                    Left = useSecondRow
                        ? secondRowCursor : firstRowCursor,
                    Bottom = useSecondRow
                        ? input.NativeBottomY - SecondRowVerticalPadding -
                            hint.Height
                        : input.NativeBottomY,
                };
                result.Add(placement);
                if (useSecondRow)
                    secondRowCursor += hint.Width + input.Spacing;
                else
                    firstRowCursor += hint.Width + input.Spacing;
            }
            return result;
        }

        private static NativeRowMetrics MeasureNativeRow(WindowState state,
            RectTransform sourceRect, Bounds sourceBounds,
            bool hasSourceBounds)
        {
            NativeRowMetrics metrics = new NativeRowMetrics {
                StartX = hasSourceBounds ? sourceBounds.min.x : 0f,
                BottomY = hasSourceBounds ? sourceBounds.min.y : 0f,
                Height = hasSourceBounds
                    ? Mathf.Max(1f, sourceBounds.size.y) : 15f,
                Spacing = FallbackHorizontalSpacing,
            };
            HorizontalLayoutGroup layout = state.Parent
                .GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
                metrics.Spacing = Mathf.Max(0f, layout.spacing);

            Bounds sourceButtonBounds = new Bounds();
            bool hasSourceButton = state.Source.buttonImage != null &&
                NativeUiFactory.TryGetRectTransformBounds(
                    state.Source.buttonImage.rectTransform, state.Parent,
                    out sourceButtonBounds);
            float rowMinY = hasSourceButton
                ? sourceButtonBounds.min.y : metrics.BottomY;
            float rowMaxY = hasSourceButton
                ? sourceButtonBounds.max.y : metrics.BottomY + metrics.Height;

            ControlDescription[] descriptions = state.HintRoot
                .GetComponentsInChildren<ControlDescription>(true);
            List<ControlDescription> visible =
                new List<ControlDescription>();
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription description = descriptions[i];
                bool isVisible = IsVisibleNativeHint(state, description,
                    rowMinY, rowMaxY);
                if (!isVisible)
                    continue;
                visible.Add(description);
            }
            visible.Sort(delegate(ControlDescription left,
                ControlDescription right) {
                Bounds leftBounds = new Bounds();
                Bounds rightBounds = new Bounds();
                NativeUiFactory.TryGetRectTransformBounds(
                    left.buttonImage.rectTransform, state.Parent,
                    out leftBounds);
                NativeUiFactory.TryGetRectTransformBounds(
                    right.buttonImage.rectTransform, state.Parent,
                    out rightBounds);
                return leftBounds.min.x.CompareTo(rightBounds.min.x);
            });

            float nativeLeft = float.MaxValue;
            float nativeRight = float.MinValue;
            for (int i = 0; i < visible.Count; i++) {
                ControlDescription description = visible[i];
                Bounds buttonBounds = new Bounds();
                Bounds visualBounds = new Bounds();
                if (!NativeUiFactory.TryGetRectTransformBounds(
                        description.buttonImage.rectTransform, state.Parent,
                        out buttonBounds) ||
                    !NativeUiFactory.TryGetControlDescriptionVisualBounds(
                        description, state.Parent, out visualBounds))
                    continue;
                if (metrics.VisibleCount == 0) {
                    metrics.StartX = visualBounds.min.x;
                    metrics.BottomY = visualBounds.min.y;
                    metrics.Height = Mathf.Max(1f, visualBounds.size.y);
                }
                nativeLeft = Mathf.Min(nativeLeft, visualBounds.min.x);
                nativeRight = Mathf.Max(nativeRight, visualBounds.max.x);
                metrics.VisibleCount++;
            }

            if (metrics.VisibleCount > 0) {
                metrics.StartX = nativeLeft;
                metrics.NativeLength = Mathf.Max(0f,
                    nativeRight - nativeLeft);
            }

            MeasureReservedFooterArea(state, metrics, rowMinY, rowMaxY,
                null);
            return metrics;
        }

        private static bool IsVisibleNativeHint(WindowState state,
            ControlDescription description, float rowMinY, float rowMaxY)
        {
            if (description == null || description.gameObject == null ||
                !description.gameObject.activeInHierarchy ||
                state == null || state.HintRoot == null ||
                !description.transform.IsChildOf(state.HintRoot) ||
                IsManagedHint(state, description) ||
                description.buttonImage == null ||
                !description.buttonImage.enabled ||
                !description.buttonImage.gameObject.activeInHierarchy ||
                  description.buttonImage.color.a <= 0.01f)
                return false;
            if (GetEffectiveCanvasGroupAlpha(description.transform) <=
                    0.01f)
                return false;
            Bounds buttonBounds = new Bounds();
            if (!NativeUiFactory.TryGetRectTransformBounds(
                    description.buttonImage.rectTransform, state.Parent,
                    out buttonBounds) || buttonBounds.size.x < 0.1f ||
                buttonBounds.size.y < 0.1f ||
                buttonBounds.max.y < rowMinY - 2f ||
                buttonBounds.min.y > rowMaxY + 2f)
                return false;
            if (description.texts == null)
                return false;
            for (int i = 0; i < description.texts.Length; i++) {
                Text text = description.texts[i];
                if (text != null && text.enabled &&
                    text.gameObject.activeInHierarchy &&
                    text.color.a > 0.01f && !string.IsNullOrEmpty(text.text))
                    return true;
            }
            return false;
        }

        private static void MeasureReservedFooterArea(WindowState state,
            NativeRowMetrics metrics, float rowMinY, float rowMaxY,
            bool? paginationVisible)
        {
            float reservedLeft = float.MaxValue;
            float reservedRight = float.MinValue;
            float structuralRight = float.MinValue;
            Text[] texts = state.Root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) {
                Text text = texts[i];
                if (text == null || !IsPageCounter(text.transform,
                        text.text))
                    continue;
                IncludeStructuralRight(text.rectTransform, state.Parent,
                    rowMinY, rowMaxY, metrics.Height,
                    ref structuralRight);
                if (paginationVisible == false ||
                    (paginationVisible == null && !IsActuallyVisible(text)))
                    continue;
                IncludeReservedBounds(text.rectTransform, state.Parent,
                    rowMinY, rowMaxY, metrics.Height,
                    ref reservedLeft, ref reservedRight);
                Text[] childTexts = text.GetComponentsInChildren<Text>(true);
                for (int childIndex = 0; childIndex < childTexts.Length;
                        childIndex++) {
                    if (childTexts[childIndex] != null &&
                        IsActuallyVisible(childTexts[childIndex]))
                        IncludeReservedBounds(childTexts[childIndex]
                            .rectTransform, state.Parent, rowMinY, rowMaxY,
                            metrics.Height,
                            ref reservedLeft, ref reservedRight);
                }
            }
            ToggleGroup[] groups = state.Root
                .GetComponentsInChildren<ToggleGroup>(true);
            for (int i = 0; i < groups.Length; i++) {
                if (groups[i] != null)
                    IncludeStructuralRight(groups[i]
                        .GetComponent<RectTransform>(), state.Parent,
                        rowMinY, rowMaxY, metrics.Height,
                        ref structuralRight);
                if (groups[i] != null && paginationVisible != false &&
                    (paginationVisible == true ||
                        IsActuallyVisible(groups[i]) &&
                        HasVisibleGraphic(groups[i].transform)))
                    IncludeReservedBounds(groups[i]
                        .GetComponent<RectTransform>(), state.Parent,
                        rowMinY, rowMaxY, metrics.Height,
                        ref reservedLeft, ref reservedRight);
            }
            Slider[] sliders = state.Root.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++) {
                if (sliders[i] != null)
                    IncludeStructuralRight(sliders[i]
                        .GetComponent<RectTransform>(), state.Parent,
                        rowMinY, rowMaxY, metrics.Height,
                        ref structuralRight);
                if (sliders[i] != null && paginationVisible != false &&
                    (paginationVisible == true ||
                        IsActuallyVisible(sliders[i])))
                    IncludeReservedBounds(sliders[i]
                        .GetComponent<RectTransform>(), state.Parent,
                        rowMinY, rowMaxY, metrics.Height,
                        ref reservedLeft, ref reservedRight);
            }
            Graphic[] graphics = state.Root
                .GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) {
                Graphic graphic = graphics[i];
                if (graphic == null || graphic.rectTransform == null ||
                    GetPath(graphic.transform).IndexOf("PagesDots",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                IncludeStructuralRight(graphic.rectTransform, state.Parent,
                    rowMinY, rowMaxY, metrics.Height,
                    ref structuralRight);
                if (paginationVisible == false ||
                    (paginationVisible == null &&
                        !IsActuallyVisible(graphic)))
                    continue;
                IncludeReservedBounds(graphic.rectTransform, state.Parent,
                    rowMinY, rowMaxY, metrics.Height,
                    ref reservedLeft, ref reservedRight);
            }
            float windowRight = structuralRight > float.MinValue
                ? structuralRight : GetWindowRight(state);
            if (reservedLeft == float.MaxValue) {
                metrics.WindowRight = windowRight;
                metrics.ReservedLeft = metrics.WindowRight;
                metrics.ReservedWidth = 0f;
                return;
            }
            metrics.WindowRight = Mathf.Max(windowRight, reservedRight);
            metrics.ReservedLeft = reservedLeft;
            metrics.ReservedWidth = Mathf.Max(0f,
                reservedRight - reservedLeft);
        }

        private static void IncludeStructuralRight(RectTransform rect,
            Transform relativeTo, float rowMinY, float rowMaxY,
            float rowHeight, ref float right)
        {
            Bounds bounds;
            if (!NativeUiFactory.TryGetRectTransformBounds(rect,
                    relativeTo, out bounds) ||
                bounds.max.y < rowMinY - 2f ||
                bounds.min.y > rowMaxY + 2f ||
                bounds.size.y > Mathf.Max(1f, rowHeight) * 3f)
                return;
            right = Mathf.Max(right, bounds.max.x);
        }

        private static bool IsActuallyVisible(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled ||
                !behaviour.gameObject.activeInHierarchy)
                return false;

            Graphic graphic = behaviour as Graphic;
            if (graphic != null && graphic.color.a <= 0.01f)
                return false;

            return GetEffectiveCanvasGroupAlpha(behaviour.transform) >
                0.01f;
        }

        private static float GetEffectiveCanvasGroupAlpha(
            Transform transform)
        {
            float alpha = 1f;
            Transform current = transform;
            while (current != null) {
                CanvasGroup[] groups = current
                    .GetComponents<CanvasGroup>();
                bool ignoreParents = false;
                for (int i = 0; i < groups.Length; i++) {
                    CanvasGroup group = groups[i];
                    if (group == null || !group.enabled)
                        continue;
                    alpha *= group.alpha;
                    ignoreParents |= group.ignoreParentGroups;
                }
                if (ignoreParents)
                    break;
                current = current.parent;
            }
            return alpha;
        }

        private static bool HasVisibleGraphic(Transform root)
        {
            if (root == null)
                return false;
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) {
                if (IsActuallyVisible(graphics[i]))
                    return true;
            }
            return false;
        }

        private static bool IsPageCounter(Transform transform, string text)
        {
            if ((text ?? string.Empty).IndexOf('/') >= 0)
                return true;
            Transform current = transform;
            while (current != null) {
                if (current.name.IndexOf("PageCounter",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static void IncludeReservedBounds(RectTransform rect,
            Transform relativeTo, float rowMinY, float rowMaxY,
            float rowHeight,
            ref float left, ref float right)
        {
            Bounds bounds = new Bounds();
            if (!NativeUiFactory.TryGetRectTransformBounds(rect,
                    relativeTo, out bounds))
                return;
            bool overlapsRow = bounds.max.y >= rowMinY - 2f &&
                bounds.min.y <= rowMaxY + 2f;
            bool compact = bounds.size.y <= Mathf.Max(1f, rowHeight) * 3f;
            if (!overlapsRow || !compact)
                return;
            left = Mathf.Min(left, bounds.min.x);
            right = Mathf.Max(right, bounds.max.x);
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
                return "none";
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null) {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
        private static float GetNativeHintsRight(WindowState state,
            RectTransform sourceRect, float availableRight)
        {
            HorizontalLayoutGroup layout = state.Parent
                .GetComponent<HorizontalLayoutGroup>();
            if (layout != null) {
                float cursor = state.Parent.rect.xMin + layout.padding.left;
                int count = 0;
                ControlDescription[] layoutDescriptions = state.HintRoot
                    .GetComponentsInChildren<ControlDescription>(true);
                for (int i = 0; i < layoutDescriptions.Length; i++) {
                    ControlDescription description = layoutDescriptions[i];
                    if (description == null || description.gameObject == null ||
                        !description.gameObject.activeInHierarchy ||
                        IsManagedHint(state, description) ||
                        description.gameObject.name.StartsWith("Hint_",
                            StringComparison.Ordinal))
                        continue;
                    RectTransform rect = description
                        .GetComponent<RectTransform>();
                    if (rect == null || rect.parent != state.Parent)
                        continue;
                    float width = LayoutUtility.GetPreferredWidth(rect);
                    if (width < 1f) {
                        Bounds bounds;
                        width = NativeUiFactory
                            .TryGetControlDescriptionVisualBounds(description,
                                state.Parent, out bounds)
                            ? Mathf.Max(1f, bounds.size.x)
                            : Mathf.Max(1f, Mathf.Abs(rect.rect.width));
                    }
                    if (count > 0)
                        cursor += layout.spacing;
                    cursor += width;
                    count++;
                }
                if (count > 0)
                    return cursor;
            }

            Bounds sourceBounds;
            bool hasSourceBounds = NativeUiFactory
                .TryGetControlDescriptionVisualBounds(state.Source,
                    state.Parent, out sourceBounds);
            float right = hasSourceBounds
                ? sourceBounds.max.x
                : state.Parent.InverseTransformPoint(
                    sourceRect.TransformPoint(new Vector3(
                        sourceRect.rect.xMax,
                        sourceRect.rect.center.y, 0f))).x;
            ControlDescription[] descriptions =
                state.HintRoot.GetComponentsInChildren<ControlDescription>(true);
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription description = descriptions[i];
                if (description == null || description.gameObject == null ||
                    !description.gameObject.activeInHierarchy ||
                    IsManagedHint(state, description) ||
                    description.gameObject.name.StartsWith("Hint_",
                        StringComparison.Ordinal))
                    continue;
                RectTransform rect = description.GetComponent<RectTransform>();
                if (rect == null || rect.parent != state.Parent)
                    continue;
                Bounds candidateBounds;
                bool hasCandidateBounds = NativeUiFactory
                    .TryGetControlDescriptionVisualBounds(description,
                        state.Parent, out candidateBounds);
                Vector3 candidateMin = hasCandidateBounds
                    ? candidateBounds.min
                    : state.Parent.InverseTransformPoint(
                        rect.TransformPoint(new Vector3(rect.rect.xMin,
                            rect.rect.yMin, 0f)));
                Vector3 candidateMax = hasCandidateBounds
                    ? candidateBounds.max
                    : state.Parent.InverseTransformPoint(
                        rect.TransformPoint(new Vector3(rect.rect.xMax,
                            rect.rect.yMax, 0f)));
                float sourceMinY = hasSourceBounds
                    ? sourceBounds.min.y
                    : state.Parent.InverseTransformPoint(
                        sourceRect.TransformPoint(new Vector3(
                            sourceRect.rect.xMin,
                            sourceRect.rect.yMin, 0f))).y;
                float sourceMaxY = hasSourceBounds
                    ? sourceBounds.max.y
                    : state.Parent.InverseTransformPoint(
                        sourceRect.TransformPoint(new Vector3(
                            sourceRect.rect.xMax,
                            sourceRect.rect.yMax, 0f))).y;
                bool overlapsFirstRow = candidateMax.y >= sourceMinY &&
                    candidateMin.y <= sourceMaxY;
                if (!overlapsFirstRow || candidateMin.x >= availableRight)
                    continue;
                float candidate = candidateMax.x;
                if (candidate <= availableRight)
                    right = Mathf.Max(right, candidate);
            }
            return right;
        }

        private static bool IsManagedHint(WindowState state,
            ControlDescription description)
        {
            if (state == null || description == null)
                return false;
            for (int i = 0; i < state.Entries.Count; i++) {
                NativeUiFactory.FooterHintHandle hint =
                    state.Entries[i].Hint;
                if (hint != null && hint.Description == description)
                    return true;
            }
            return false;
        }

        private static float GetFirstRowRightBoundary(WindowState state,
            RectTransform sourceRect, float nativeRight)
        {
            float boundary = GetWindowRight(state);
            Text[] texts = state.Root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) {
                Text text = texts[i];
                if (text == null || text.gameObject == null ||
                    !text.gameObject.activeInHierarchy ||
                    (text.text ?? string.Empty).IndexOf('/') < 0)
                    continue;
                ReduceBoundaryForObstacle(state, sourceRect,
                    text.rectTransform, nativeRight, ref boundary);
            }
            Slider[] sliders = state.Root.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++) {
                if (sliders[i] != null)
                    ReduceBoundaryForObstacle(state, sourceRect,
                        sliders[i].GetComponent<RectTransform>(), nativeRight,
                        ref boundary);
            }
            ToggleGroup[] groups =
                state.Root.GetComponentsInChildren<ToggleGroup>(true);
            for (int i = 0; i < groups.Length; i++) {
                if (groups[i] != null)
                    ReduceBoundaryForObstacle(state, sourceRect,
                        groups[i].GetComponent<RectTransform>(), nativeRight,
                        ref boundary);
            }
            RectTransform[] rects =
                state.Root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++) {
                RectTransform rect = rects[i];
                if (rect == null || rect.gameObject == null)
                    continue;
                string name = rect.gameObject.name ?? string.Empty;
                if (name.IndexOf("slider",
                        StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("page",
                        StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("pagination",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                ReduceBoundaryForObstacle(state, sourceRect, rect,
                    nativeRight, ref boundary);
            }
            return boundary - 10f;
        }

        private static float GetWindowRight(WindowState state)
        {
            float right = state.Parent.rect.xMax;
            Transform current = state.Parent.parent;
            while (current != null) {
                RectTransform rect = current.GetComponent<RectTransform>();
                if (rect != null) {
                    float candidate = state.Parent.InverseTransformPoint(
                        rect.TransformPoint(new Vector3(rect.rect.xMax,
                            rect.rect.center.y, 0f))).x;
                    right = Mathf.Max(right, candidate);
                }
                if (current == state.Root)
                    break;
                current = current.parent;
            }
            return right;
        }

        private static void ReduceBoundaryForObstacle(WindowState state,
            RectTransform sourceRect, RectTransform obstacle,
            float nativeRight, ref float boundary)
        {
            if (obstacle == null || !obstacle.gameObject.activeInHierarchy)
                return;
            Vector3 sourceMin = state.Parent.InverseTransformPoint(
                sourceRect.TransformPoint(new Vector3(sourceRect.rect.xMin,
                    sourceRect.rect.yMin, 0f)));
            Vector3 sourceMax = state.Parent.InverseTransformPoint(
                sourceRect.TransformPoint(new Vector3(sourceRect.rect.xMax,
                    sourceRect.rect.yMax, 0f)));
            Vector3 obstacleMin = state.Parent.InverseTransformPoint(
                obstacle.TransformPoint(new Vector3(obstacle.rect.xMin,
                    obstacle.rect.yMin, 0f)));
            Vector3 obstacleMax = state.Parent.InverseTransformPoint(
                obstacle.TransformPoint(new Vector3(obstacle.rect.xMax,
                    obstacle.rect.yMax, 0f)));
            bool overlapsRow = obstacleMax.y >= sourceMin.y &&
                obstacleMin.y <= sourceMax.y;
            if (overlapsRow && obstacleMin.x > nativeRight)
                boundary = Mathf.Min(boundary, obstacleMin.x);
        }

        private static void LayoutStyledRow(StyledState state)
        {
            state.UsedWidth = 0f;
            if (state.Parent == null)
                return;
            state.Entries.Sort(delegate(StyledEntry left, StyledEntry right) {
                return left.Order.CompareTo(right.Order);
            });
            float availableWidth = Mathf.Abs(state.Parent.rect.width);
            float cursorX = 0f;
            float cursorY = 0f;
            float lineHeight = 0f;
            const float spacing = 10f;
            const float lineSpacing = 2f;
            for (int i = 0; i < state.Entries.Count; i++) {
                NativeUiFactory.FooterHintHandle hint =
                    state.Entries[i].Hint;
                if (hint == null || hint.Root == null || hint.Rect == null ||
                    !hint.Root.activeSelf)
                    continue;
                float width = Mathf.Max(1f, hint.Width);
                float height = Mathf.Max(1f, hint.Height);
                if (cursorX > 0f && availableWidth > 0f &&
                    cursorX + width > availableWidth) {
                    cursorX = 0f;
                    cursorY += lineHeight + lineSpacing;
                    lineHeight = 0f;
                }
                RectTransform rect = hint.Rect;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(cursorX, cursorY);
                rect.sizeDelta = new Vector2(width, height);
                rect.localScale = Vector3.one;
                cursorX += width + spacing;
                lineHeight = Mathf.Max(lineHeight, height);
                state.UsedWidth = Mathf.Max(state.UsedWidth,
                    cursorX - spacing);
            }
        }

    }

    [HarmonyPatch]
    internal static class NativePaginationVisibilityPatch
    {
        [HarmonyPatch(typeof(PagedWindowBase), "UpdatePageCounter")]
        [HarmonyPrefix]
        private static void UpdatePageCounterPrefix(PagedWindowBase __instance,
            out bool __state)
        {
            __state = IsPageCounterVisible(__instance);
        }

        [HarmonyPatch(typeof(PagedWindowBase), "UpdatePageCounter")]
        [HarmonyPostfix]
        private static void UpdatePageCounterPostfix(PagedWindowBase __instance,
            bool __state)
        {
            if (__state == IsPageCounterVisible(__instance))
                return;
            WindowFooterHintController.OnPaginationVisibilityChanged(
                __instance);
        }

        private static bool IsPageCounterVisible(PagedWindowBase pagedWindow)
        {
            return pagedWindow != null && pagedWindow.pageCount != null &&
                pagedWindow.pageCount.gameObject != null &&
                pagedWindow.pageCount.gameObject.activeSelf;
        }
    }

    [HarmonyPatch(typeof(ControlDescription),
        nameof(ControlDescription.Show))]
    internal static class NativeFooterShowPatch
    {
        [HarmonyPostfix]
        private static void ShowPostfix(ControlDescription __instance)
        {
            WindowFooterHintController.OnNativeDescriptionLayoutChanged(
                __instance);
        }
    }

    [HarmonyPatch(typeof(ControlDescription),
        nameof(ControlDescription.Hide))]
    internal static class NativeFooterHidePatch
    {
        [HarmonyPostfix]
        private static void HidePostfix(ControlDescription __instance)
        {
            WindowFooterHintController.OnNativeDescriptionLayoutChanged(
                __instance);
        }
    }

    [HarmonyPatch(typeof(ControlDescription),
        nameof(ControlDescription.RefreshLayout))]
    internal static class NativeFooterRefreshLayoutPatch
    {
        [HarmonyPostfix]
        private static void RefreshLayoutPostfix(ControlDescription __instance)
        {
            WindowFooterHintController.OnNativeDescriptionLayoutChanged(
                __instance);
        }
    }
}
