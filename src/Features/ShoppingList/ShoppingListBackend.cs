using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Events;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS;
using Il2CppCMS.Containers;
using Il2CppCMS.Managers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
using Il2CppInterop.Runtime;
#else
using CMS;
using CMS.Containers;
using CMS.Managers;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;
using UnhollowerRuntimeLib;
#endif

namespace Cms21UiPlus
{
    internal sealed class ShoppingListEntrySnapshot
    {
        internal string ID;
        internal string Name;
        internal int Amount;
        internal bool Tire;
        internal bool Rim;
        internal bool LicensePlate;
        internal string LicensePlateName;
        internal int Size;
        internal int Width;
        internal int Profile;
        internal int ET;
        internal bool HasAdditionalData;
    }

    internal sealed class ShoppingListBackendEntry
    {
        internal string ID { get; private set; }
        internal string Name { get; private set; }
        internal int Amount { get; private set; }
        internal long Order { get; private set; }
        internal ShopListItemData Data { get; private set; }

        internal ShopListItemDataEx AdditionalData {
            get { return Data != null ? Data.AdditionalData : null; }
        }

        internal bool IsWheel {
            get {
                ShopListItemDataEx data = AdditionalData;
                return data != null && (data.Tire || data.Rim);
            }
        }

        internal ShoppingListBackendEntry(ShopListItemData data, string name,
            long order)
        {
            Data = CloneData(data);
            ID = Data != null ? Data.ID : null;
            Name = name ?? ID ?? string.Empty;
            Amount = Data != null ? Math.Max(0, Data.Amount) : 0;
            Order = order;
        }

        internal void SetAmount(int amount)
        {
            Amount = Math.Max(0, amount);
            if (Data != null)
                Data.Amount = Amount;
        }

        internal void AddAmount(int amount)
        {
            if (amount <= 0)
                return;
            long value = (long)Amount + amount;
            SetAmount(value > int.MaxValue ? int.MaxValue : (int)value);
        }

        internal ShopListItemData CreateNativeData()
        {
            return CloneData(Data);
        }

        internal ShoppingListEntrySnapshot CreateSnapshot()
        {
            ShopListItemDataEx data = AdditionalData;
            ShoppingListEntrySnapshot snapshot = new ShoppingListEntrySnapshot {
                ID = ID,
                Name = Name,
                Amount = Amount,
                HasAdditionalData = data != null,
            };
            if (data == null)
                return snapshot;

            snapshot.Tire = data.Tire;
            snapshot.Rim = data.Rim;
            if (snapshot.Tire) {
                snapshot.Size = data.Size;
                snapshot.Width = data.Width;
                snapshot.Profile = data.Profile;
                if (snapshot.Rim)
                    snapshot.ET = data.ET;
                return snapshot;
            }
            if (snapshot.Rim) {
                snapshot.Size = data.Size;
                snapshot.ET = data.ET;
                return snapshot;
            }

            snapshot.LicensePlate = data.LicensePlate;
            if (snapshot.LicensePlate)
                snapshot.LicensePlateName = data.LicensePlateName;
            return snapshot;
        }

        private static ShopListItemData CloneData(ShopListItemData source)
        {
            if (source == null)
                return null;

            ShopListItemData copy = new ShopListItemData();
            copy.ID = source.ID;
            copy.Amount = source.Amount;
            copy.AdditionalData = CloneAdditionalData(source.AdditionalData);
            return copy;
        }

        private static ShopListItemDataEx CloneAdditionalData(
            ShopListItemDataEx source)
        {
            if (source == null)
                return null;

            ShopListItemDataEx copy = new ShopListItemDataEx();
            copy.Reset();

            copy.Tire = source.Tire;
            copy.Rim = source.Rim;
            if (copy.Tire) {
                copy.Size = source.Size;
                copy.Width = source.Width;
                copy.Profile = source.Profile;
                if (copy.Rim)
                    copy.ET = source.ET;
                return copy;
            }
            if (copy.Rim) {
                copy.Size = source.Size;
                copy.ET = source.ET;
                return copy;
            }

            copy.LicensePlate = source.LicensePlate;
            if (copy.LicensePlate)
                copy.LicensePlateName = source.LicensePlateName;
            return copy;
        }
    }

    [HarmonyPatch]
    internal static class ShoppingListBackend
    {
        private static readonly Dictionary<string, ShoppingListBackendEntry>
            Parts = new Dictionary<string, ShoppingListBackendEntry>(
                StringComparer.Ordinal);
        private static readonly List<ShoppingListBackendEntry> Wheels =
            new List<ShoppingListBackendEntry>();
        private static readonly List<ShoppingListBackendEntry> SourceOrder =
            new List<ShoppingListBackendEntry>();
        private static readonly Dictionary<int, ShoppingListEntrySnapshot>
            RenderedRows = new Dictionary<int, ShoppingListEntrySnapshot>();

        private static ShopListWindow activeWindow;
        private static bool initialized;
        private static long nextOrder;

        internal static event Action DisplayChanged;

        internal static bool IsEnabled {
            get {
                if (Main.SettingsEntry == null || Main.SettingsEntry.Value == null)
                    return false;
                Settings settings = Main.SettingsEntry.Value;
                return settings.addShoppingListSorting ||
                    settings.wheelShopListPurchaseHelper ||
                    settings.removePartsFromShoppingList;
            }
        }

        internal static bool IsOpen(ShopListWindow window)
        {
            return window != null && window == activeWindow;
        }

        internal static int DisplayCount {
            get { return SourceOrder.Count; }
        }

        internal static void Open(ShopListWindow window)
        {
            if (window == null)
                return;

            EnsureInitialized(window);
            RebuildSourceOrder();
            activeWindow = window;
            RenderedRows.Clear();
        }

        internal static void Close(ShopListWindow window)
        {
            if (window == null || window != activeWindow)
                return;

            RebuildSourceOrder();
            WriteAllToWindow(window);
            RenderedRows.Clear();
            SourceOrder.Clear();
            activeWindow = null;
        }

        internal static List<ShoppingListBackendEntry> GetDisplayEntriesSnapshot()
        {
            return new List<ShoppingListBackendEntry>(SourceOrder);
        }

        internal static ShoppingListBackendEntry GetDisplayEntry(int index)
        {
            return index >= 0 && index < SourceOrder.Count
                ? SourceOrder[index] : null;
        }

        internal static void BindRenderedRows(ShopListWindow window)
        {
            RenderedRows.Clear();
            if (window == null || window != activeWindow ||
                window.shopListItems == null)
                return;

            int count = Math.Min(window.shopListItems.Count,
                SourceOrder.Count);
            for (int index = 0; index < count; index++) {
                ShopListItem row = window.shopListItems[index];
                ShoppingListBackendEntry entry = SourceOrder[index];
                if (row == null || entry == null)
                    continue;

                ShoppingListEntrySnapshot key = entry.CreateSnapshot();
                RenderedRows[row.GetInstanceID()] = key;
                BindDeleteButton(window, row, key);
            }
        }

        internal static ShoppingListBackendEntry GetRenderedRowEntry(
            ShopListItem row)
        {
            if (row == null)
                return null;

            ShoppingListEntrySnapshot key;
            return RenderedRows.TryGetValue(row.GetInstanceID(), out key)
                ? FindExact(key) : null;
        }

        internal static ShoppingListBackendEntry GetCurrentSelectedEntry(
            ShopListWindow window)
        {
            if (window == null || window != activeWindow ||
                window.gridNavigationManager == null) {
                return null;
            }

            var selected = window.gridNavigationManager.GetCurrentGridItem();
            ShopListItem row = selected != null
                ? selected.TryCast<ShopListItem>() : null;
            ShoppingListBackendEntry entry = GetRenderedRowEntry(row);
            return entry;
        }

        internal static List<ShoppingListBackendEntry> GetSourceEntriesSnapshot()
        {
            return new List<ShoppingListBackendEntry>(SourceOrder);
        }

        internal static void SetFilteredDisplay(
            IList<ShoppingListBackendEntry> filteredEntries)
        {
            SourceOrder.Clear();
            if (filteredEntries != null) {
                HashSet<ShoppingListBackendEntry> seen =
                    new HashSet<ShoppingListBackendEntry>();
                for (int i = 0; i < filteredEntries.Count; i++) {
                    ShoppingListBackendEntry entry = filteredEntries[i];
                    if (entry != null && ContainsStorageEntry(entry) &&
                        seen.Add(entry))
                        SourceOrder.Add(entry);
                }
            }
            RaiseDisplayChanged();
        }

        internal static void SetDisplayOrder(
            IList<ShoppingListBackendEntry> orderedEntries)
        {
            if (orderedEntries == null ||
                orderedEntries.Count != SourceOrder.Count)
                return;

            HashSet<ShoppingListBackendEntry> expected =
                new HashSet<ShoppingListBackendEntry>(SourceOrder);
            if (expected.Count != SourceOrder.Count)
                return;
            for (int i = 0; i < orderedEntries.Count; i++) {
                ShoppingListBackendEntry entry = orderedEntries[i];
                if (entry == null || !expected.Remove(entry))
                    return;
            }
            if (expected.Count != 0)
                return;

            SourceOrder.Clear();
            for (int i = 0; i < orderedEntries.Count; i++)
                SourceOrder.Add(orderedEntries[i]);
            RaiseDisplayChanged();
        }

        internal static bool TryAdjustAmount(ShopListWindow window,
            ShopListItemData data, int delta, out ShopListItemData current)
        {
            current = null;
            if (data == null || delta == 0) {
                return false;
            }

            ShoppingListBackendEntry entry = FindExact(data);
            if (entry == null || entry.Amount < 1) {
                return false;
            }
            if (delta < 0 && entry.Amount <= 1) {
                return false;
            }
            if (delta > 0 && entry.Amount == int.MaxValue) {
                return false;
            }

            entry.SetAmount(entry.Amount + delta);
            current = entry.CreateNativeData();
            Persist(window);
            RaiseDisplayChanged();
            return true;
        }

        internal static bool Remove(ShopListWindow window, ShopListItemData data)
        {
            ShoppingListBackendEntry entry = FindExact(data);
            return RemoveEntry(window, entry, true);
        }

        internal static bool Remove(ShopListWindow window,
            ShoppingListEntrySnapshot snapshot, bool updateUi)
        {
            ShoppingListBackendEntry entry = FindExact(snapshot);
            return RemoveEntry(window, entry, updateUi);
        }

        internal static ShoppingListEntrySnapshot FindForPurchase(
            ShoppingListEntrySnapshot key)
        {
            ShoppingListBackendEntry entry = FindExact(key);
            return entry != null ? entry.CreateSnapshot() : null;
        }

        internal static ShoppingListEntrySnapshot FindForPurchase(
            ShopListWindow window, string itemID, bool wheel)
        {
            EnsureInitialized(window);
            if (string.IsNullOrEmpty(itemID))
                return null;

            ShoppingListBackendEntry entry;
            if (!wheel)
                return Parts.TryGetValue(itemID, out entry) && entry != null
                    ? entry.CreateSnapshot() : null;

            for (int i = 0; i < Wheels.Count; i++) {
                entry = Wheels[i];
                if (entry != null && string.Equals(entry.ID, itemID,
                        StringComparison.Ordinal))
                    return entry.CreateSnapshot();
            }
            return null;
        }

        internal static bool ApplyPurchasedAmount(ShopListWindow window,
            ShoppingListEntrySnapshot purchased, int amount)
        {
            EnsureInitialized(window);
            if (purchased == null || amount <= 0)
                return false;

            ShoppingListBackendEntry entry = FindExact(purchased);
            if (entry == null)
                return false;

            int remaining = entry.Amount - amount;
            if (remaining <= 0)
                return RemoveEntry(window, entry, true);

            entry.SetAmount(remaining);
            Persist(window);
            RaiseDisplayChanged();
            return true;
        }

        internal static bool ApplyLicensePurchase(ShopListWindow window,
            string itemID, string licensePlateName, int amount)
        {
            EnsureInitialized(window);
            if (string.IsNullOrEmpty(itemID) || amount <= 0)
                return false;

            ShoppingListBackendEntry entry;
            if (!Parts.TryGetValue(itemID, out entry) || entry == null)
                return false;

            ShopListItemDataEx additional = entry.AdditionalData;
            if (additional == null || !additional.LicensePlate ||
                !string.Equals(additional.LicensePlateName, licensePlateName,
                    StringComparison.Ordinal))
                return false;

            int remaining = entry.Amount - amount;
            if (remaining <= 0)
                return RemoveEntry(window, entry, true);

            entry.SetAmount(remaining);
            Persist(window);
            RaiseDisplayChanged();
            return true;
        }

        internal static void PersistState(ShopListWindow window)
        {
            Persist(window);
            RaiseDisplayChanged();
        }

        internal static void ApplyDisplayToWindow(ShopListWindow window)
        {
            if (window == null || window.items == null ||
                window != activeWindow)
                return;

            window.items.Clear();
            for (int i = 0; i < SourceOrder.Count; i++)
                window.items.Add(SourceOrder[i].CreateNativeData());
        }

        internal static void RebuildSourceOrder()
        {
            SourceOrder.Clear();
            foreach (ShoppingListBackendEntry entry in Parts.Values) {
                if (entry != null)
                    SourceOrder.Add(entry);
            }
            for (int i = 0; i < Wheels.Count; i++) {
                ShoppingListBackendEntry entry = Wheels[i];
                if (entry != null)
                    SourceOrder.Add(entry);
            }
            SourceOrder.Sort(delegate(ShoppingListBackendEntry left,
                ShoppingListBackendEntry right) {
                return left.Order.CompareTo(right.Order);
            });
        }

        private static void EnsureInitialized(ShopListWindow window)
        {
            if (initialized || window == null || window.items == null)
                return;

            Parts.Clear();
            Wheels.Clear();
            SourceOrder.Clear();
            RenderedRows.Clear();
            nextOrder = 0;

            HashSet<ShoppingListBackendEntry> seen =
                new HashSet<ShoppingListBackendEntry>();
            for (int i = 0; i < window.items.Count; i++) {
                ShopListItemData data = window.items[i];
                if (data == null || string.IsNullOrEmpty(data.ID) ||
                    data.Amount <= 0)
                    continue;

                ShoppingListBackendEntry entry = FindExact(data);
                if (entry == null) {
                    entry = CreateEntry(data);
                    AddStorageEntry(entry);
                } else if (seen.Contains(entry)) {
                    entry.AddAmount(data.Amount);
                    continue;
                } else {
                    entry.SetAmount(data.Amount);
                }
                seen.Add(entry);
            }
            initialized = true;
        }

        private static ShoppingListBackendEntry AddNativeEntry(
            ShoppingListEntrySnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.ID))
                return null;

            ShoppingListBackendEntry entry = FindExact(snapshot);
            if (entry != null) {
                entry.AddAmount(1);
            } else {
                ShopListItemData data = CreateNativeData(snapshot);
                entry = CreateEntry(data);
                AddStorageEntry(entry);
            }
            return entry;
        }

        private static ShoppingListEntrySnapshot CaptureNativeEntry(
            string itemID, ShopListItemDataEx additionalData)
        {
            ShoppingListEntrySnapshot snapshot = new ShoppingListEntrySnapshot {
                ID = itemID,
                Amount = 1,
                HasAdditionalData = additionalData != null,
            };
            if (additionalData == null)
                return snapshot;

            snapshot.Tire = additionalData.Tire;
            snapshot.Rim = additionalData.Rim;
            if (snapshot.Tire) {
                snapshot.Size = additionalData.Size;
                snapshot.Width = additionalData.Width;
                snapshot.Profile = additionalData.Profile;
                if (snapshot.Rim) {
                    snapshot.ET = additionalData.ET;
                }
                return snapshot;
            }
            if (snapshot.Rim) {
                snapshot.Size = additionalData.Size;
                snapshot.ET = additionalData.ET;
                return snapshot;
            }

            snapshot.LicensePlate = additionalData.LicensePlate;
            if (snapshot.LicensePlate)
                snapshot.LicensePlateName = additionalData.LicensePlateName;
            return snapshot;
        }

        private static ShoppingListEntrySnapshot CaptureAddToShopListEntry(
            string itemID, string suffix, ShopListItemDataEx additionalData)
        {
            if (!string.IsNullOrEmpty(suffix)) {
                int open = suffix.IndexOf('(');
                int slash = open >= 0 ? suffix.IndexOf('/', open + 1) : -1;
                int r = slash >= 0 ? suffix.IndexOf('R', slash + 1) : -1;
                int close = r >= 0 ? suffix.IndexOf(')', r + 1) : -1;
                int width;
                int profile;
                int size;
                if (open >= 0 && slash > open && r > slash && close > r &&
                    int.TryParse(suffix.Substring(open + 1, slash - open - 1),
                        out width) &&
                    int.TryParse(suffix.Substring(slash + 1, r - slash - 1),
                        out profile) &&
                    int.TryParse(suffix.Substring(r + 1, close - r - 1),
                        out size)) {
                    return new ShoppingListEntrySnapshot {
                        ID = itemID,
                        Amount = 1,
                        Tire = true,
                        Size = size,
                        Width = width,
                        Profile = profile,
                        HasAdditionalData = true,
                    };
                }

                int quote = open >= 0 ? suffix.IndexOf('"', open + 1) : -1;
                int et;
                if (open >= 0 && quote > open &&
                    int.TryParse(suffix.Substring(open + 1, quote - open - 1),
                        out size) && TryParseTrailingInt(suffix, quote + 1, out et)) {
                    return new ShoppingListEntrySnapshot {
                        ID = itemID,
                        Amount = 1,
                        Rim = true,
                        Size = size,
                        ET = et,
                        HasAdditionalData = true,
                    };
                }
            }

            return CaptureNativeEntry(itemID, additionalData);
        }

        private static bool TryParseTrailingInt(
            string value, int start, out int result)
        {
            result = 0;
            int end = value.Length;
            while (end > start && !char.IsDigit(value[end - 1]))
                end--;
            if (end <= start)
                return false;

            int begin = end - 1;
            while (begin > start && char.IsDigit(value[begin - 1]))
                begin--;
            if (begin > start && value[begin - 1] == '-')
                begin--;
            return int.TryParse(value.Substring(begin, end - begin), out result);
        }

        private static ShopListItemData CreateNativeData(
            ShoppingListEntrySnapshot snapshot)
        {
            ShopListItemData data = new ShopListItemData();
            data.ID = snapshot.ID;
            data.Amount = snapshot.Amount;
            if (!snapshot.HasAdditionalData)
                return data;

            ShopListItemDataEx additional = new ShopListItemDataEx();
            additional.Reset();
            additional.Tire = snapshot.Tire;
            additional.Rim = snapshot.Rim;
            if (snapshot.Tire) {
                additional.Size = snapshot.Size;
                additional.Width = snapshot.Width;
                additional.Profile = snapshot.Profile;
                if (snapshot.Rim)
                    additional.ET = snapshot.ET;
            } else if (snapshot.Rim) {
                additional.Size = snapshot.Size;
                additional.ET = snapshot.ET;
            } else {
                additional.LicensePlate = snapshot.LicensePlate;
                if (snapshot.LicensePlate)
                    additional.LicensePlateName = snapshot.LicensePlateName;
            }
            data.AdditionalData = additional;
            return data;
        }

        private static void AddStorageEntry(ShoppingListBackendEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ID))
                return;

            if (entry.IsWheel)
                Wheels.Add(entry);
            else
                Parts[entry.ID] = entry;
        }

        private static ShoppingListBackendEntry CreateEntry(ShopListItemData data)
        {
            return new ShoppingListBackendEntry(data, ResolveName(data),
                nextOrder++);
        }

        private static string ResolveName(ShopListItemData data)
        {
            if (data == null || string.IsNullOrEmpty(data.ID))
                return string.Empty;

            try {
                GameInventory inventory = Singleton<GameInventory>.Instance;
                if (inventory != null) {
                    string name = inventory.GetItemLocalizeName(data.ID);
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            } catch (Exception exception) {
                ModLogger.Log("[ShoppingList] Failed to resolve item name '" +
                    data.ID + "': " + exception.Message,
                    Types.LoggingLevels.Warning);
            }
            return data.ID;
        }

        private static ShoppingListBackendEntry FindExact(ShopListItemData data)
        {
            if (data == null || string.IsNullOrEmpty(data.ID))
                return null;

            if (IsWheelData(data.AdditionalData))
                return FindExactWheel(data);

            ShoppingListBackendEntry entry;
            return Parts.TryGetValue(data.ID, out entry) ? entry : null;
        }

        private static ShoppingListBackendEntry FindExact(
            ShoppingListEntrySnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.ID))
                return null;

            if (!snapshot.Tire && !snapshot.Rim) {
                ShoppingListBackendEntry part;
                return Parts.TryGetValue(snapshot.ID, out part) ? part : null;
            }

            for (int i = 0; i < Wheels.Count; i++) {
                ShoppingListBackendEntry entry = Wheels[i];
                if (EntryMatchesSnapshot(entry, snapshot))
                    return entry;
            }
            return null;
        }

        private static ShoppingListBackendEntry FindExactWheel(
            ShopListItemData data)
        {
            if (data == null)
                return null;

            for (int i = 0; i < Wheels.Count; i++) {
                ShoppingListBackendEntry entry = Wheels[i];
                if (EntryMatchesData(entry, data))
                    return entry;
            }
            return null;
        }

        private static bool EntryMatchesData(ShoppingListBackendEntry entry,
            ShopListItemData data)
        {
            if (entry == null || data == null ||
                !string.Equals(entry.ID, data.ID, StringComparison.Ordinal))
                return false;

            ShopListItemDataEx left = entry.AdditionalData;
            ShopListItemDataEx right = data.AdditionalData;
            if (left == null || right == null || left.Tire != right.Tire ||
                left.Rim != right.Rim)
                return false;
            if (left.Tire) {
                return left.Size == right.Size && left.Width == right.Width &&
                    left.Profile == right.Profile &&
                    (!left.Rim || left.ET == right.ET);
            }
            return left.Rim && left.Size == right.Size && left.ET == right.ET;
        }

        private static bool EntryMatchesSnapshot(ShoppingListBackendEntry entry,
            ShoppingListEntrySnapshot snapshot)
        {
            if (entry == null || snapshot == null ||
                !string.Equals(entry.ID, snapshot.ID, StringComparison.Ordinal))
                return false;

            ShopListItemDataEx data = entry.AdditionalData;
            if (data == null || data.Tire != snapshot.Tire ||
                data.Rim != snapshot.Rim)
                return false;
            if (data.Tire) {
                return data.Size == snapshot.Size &&
                    data.Width == snapshot.Width &&
                    data.Profile == snapshot.Profile &&
                    (!data.Rim || data.ET == snapshot.ET);
            }
            return data.Rim && data.Size == snapshot.Size &&
                data.ET == snapshot.ET;
        }

        private static void BindDeleteButton(ShopListWindow window,
            ShopListItem row, ShoppingListEntrySnapshot key)
        {
            if (window == null || row == null || row.trashButton == null ||
                key == null)
                return;

            row.trashButton.onClick.RemoveAllListeners();
            Action clickAction = delegate () {
                Remove(window, key, true);
            };
            UnityAction unityAction =
                DelegateSupport.ConvertDelegate<UnityAction>(clickAction);
            row.trashButton.onClick.AddListener(unityAction);
        }

        private static bool IsWheelData(ShopListItemDataEx data)
        {
            return data != null && (data.Tire || data.Rim);
        }

        private static bool ContainsStorageEntry(ShoppingListBackendEntry entry)
        {
            if (entry == null)
                return false;
            if (entry.IsWheel)
                return Wheels.Contains(entry);

            ShoppingListBackendEntry current;
            return Parts.TryGetValue(entry.ID, out current) &&
                object.ReferenceEquals(current, entry);
        }

        private static void RemoveStorageEntry(ShoppingListBackendEntry entry)
        {
            if (entry == null)
                return;

            if (entry.IsWheel)
                Wheels.Remove(entry);
            else
                Parts.Remove(entry.ID);
            SourceOrder.Remove(entry);
        }

        private static bool RemoveEntry(ShopListWindow window,
            ShoppingListBackendEntry entry, bool updateUi)
        {
            if (entry == null || !ContainsStorageEntry(entry))
                return false;

            RemoveStorageEntry(entry);
            RenderedRows.Clear();
            if (updateUi) {
                Persist(window);
                RaiseDisplayChanged();
            }
            return true;
        }

        private static void Persist(ShopListWindow window)
        {
            if (window == null)
                window = activeWindow;
            if (window == null)
                return;

            bool restoreDisplay = window == activeWindow;
            List<ShoppingListBackendEntry> displayOrder = restoreDisplay
                ? new List<ShoppingListBackendEntry>(SourceOrder) : null;

            RebuildSourceOrder();
            WriteAllToWindow(window);
            window.Save();

            SourceOrder.Clear();
            if (restoreDisplay) {
                SourceOrder.AddRange(displayOrder);
                ApplyDisplayToWindow(window);
            }
        }

        private static void WriteAllToWindow(ShopListWindow window)
        {
            if (window == null || window.items == null)
                return;

            window.items.Clear();
            for (int i = 0; i < SourceOrder.Count; i++)
                window.items.Add(SourceOrder[i].CreateNativeData());
        }

        private static void RaiseDisplayChanged()
        {
            RenderedRows.Clear();
            Action handler = DisplayChanged;
            if (handler != null)
                handler();
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Show))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void ShowPostfix(ShopListWindow __instance, bool __result)
        {
            if (!__result || !IsEnabled || __instance == null)
                return;

            Open(__instance);
            ShoppingListRefresh.Bind(__instance);
            ApplyDisplayToWindow(__instance);
            BindRenderedRows(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.FillItems))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void FillItemsPostfix(ShopListWindow __instance)
        {
            if (__instance != null && __instance == activeWindow)
                BindRenderedRows(__instance);
        }

        [HarmonyPatch(typeof(ShopListWindow), nameof(ShopListWindow.Hide))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void HidePrefix(ShopListWindow __instance)
        {
            if (__instance == null || __instance != activeWindow)
                return;

            ShoppingListRefresh.Unbind(__instance);
            Close(__instance);
        }

        [HarmonyPatch(typeof(UIManager), "AddToShopList",
            new Type[] { typeof(string), typeof(string),
                typeof(ShopListItemDataEx) })]
        [HarmonyPrefix]
        private static bool AddToShopListPrefix(UIManager __instance,
            string __0, string __1, ShopListItemDataEx __2)
        {
            if (!IsEnabled || __instance == null || string.IsNullOrEmpty(__0))
                return true;

            ShopListWindow window = __instance.ShopListWindow;
            if (window == null || window.items == null)
                return true;

            ShoppingListEntrySnapshot snapshot =
                CaptureAddToShopListEntry(__0, __1, __2);
            EnsureInitialized(window);
            ShoppingListBackendEntry entry = AddNativeEntry(snapshot);
            if (entry == null)
                return true;

            Persist(window);
            if (window == activeWindow) {
                if (!ShoppingListShopFilterFeature.RefreshFromBackend(window)) {
                    RebuildSourceOrder();
                    RaiseDisplayChanged();
                }
            }

            __instance.ShowPopup(
                ModLocalization.Get("LOC_ShoppingListAddedPopupTitle"),
                entry.Name + (__1 ?? string.Empty), PopupType.Normal);
            SoundManager soundManager = SoundManager.Get();
            if (soundManager != null)
                soundManager.PlaySFX("AddItemToList");
            return false;
        }
    }
}
