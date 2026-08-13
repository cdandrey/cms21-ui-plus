using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Managers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Description;
using Il2CppCMS.UI.Logic.Parking;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS;
using CMS.Managers;
using CMS.UI;
using CMS.UI.Description;
using CMS.UI.Logic.Parking;
using CMS.UI.Windows;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    public static class ParkingSortingFeature
    {
        private const string FooterWindowId = "ParkingManagement";
        private const string SortHintId = "Hint_ParkingSorting";

        private sealed class ParkingEntry
        {
            public int OriginalIndex;
            public int ArrivalRank;
            public string ArrivalKey;
            public string DisplayName;
            public float Condition;
        }

        private static ParkingManagementWindow activeWindow;
        private static NativeUiFactory.FooterHintHandle sortHint;
        private static NativeUiFactory.SortingWindowHandle sortingWindow;
        private static SortType sortType = SortType.ByDateAsc;

        private static bool IsEnabled {
            get {
                return GlobalState.IsGarageSceneActive &&
                    Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.addParkingSorting;
            }
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), nameof(ParkingManagementWindow.Show))]
        [HarmonyPostfix]
        public static void ShowPostfix(ParkingManagementWindow __instance,
            bool __result)
        {
            if (!__result || !IsEnabled || __instance == null)
                return;

            activeWindow = __instance;
            if (IsIdle(__instance))
                CreateSortHint();
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), nameof(ParkingManagementWindow.Hide))]
        [HarmonyPrefix]
        public static void HidePrefix(ParkingManagementWindow __instance)
        {
            if (__instance != activeWindow)
                return;

            DestroySortHint();
            DestroySortingWindow();
            activeWindow = null;
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), "HandleInput")]
        [HarmonyPostfix]
        public static void HandleInputPostfix(ParkingManagementWindow __instance)
        {
            if (!Input.GetKeyDown(KeyCode.C))
                return;

            if (!IsEnabled || __instance == null || __instance != activeWindow ||
                !IsIdle(__instance))
                return;

            if (sortingWindow != null && sortingWindow.Root != null) {
                DestroySortingWindow();
                CreateSortHint();
                return;
            }

            OpenSortingWindow();
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), "SetIdleStateDescription")]
        [HarmonyPostfix]
        public static void SetIdleStateDescriptionPostfix(
            ParkingManagementWindow __instance)
        {
            if (!IsEnabled || __instance == null)
                return;

            activeWindow = __instance;
            CreateSortHint();
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), "SetSourceCarInfoDescription")]
        [HarmonyPrefix]
        public static void SetSourceCarInfoDescriptionPrefix()
        {
            DestroySortHint();
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), "SetDestinationCarInfoDescription")]
        [HarmonyPrefix]
        public static void SetDestinationCarInfoDescriptionPrefix()
        {
            DestroySortHint();
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), "SetSwapDescription")]
        [HarmonyPrefix]
        public static void SetSwapDescriptionPrefix()
        {
            DestroySortHint();
        }

        [HarmonyPatch(typeof(ParkingManagementWindow), "SetUnlockAlleyDescription")]
        [HarmonyPrefix]
        public static void SetUnlockAlleyDescriptionPrefix()
        {
            DestroySortHint();
        }

        public static void Reset()
        {
            DestroySortHint();
            DestroySortingWindow();
            activeWindow = null;
            sortType = SortType.ByDateAsc;
        }

        private static void CreateSortHint()
        {
            if (sortHint != null && sortHint.Root != null)
                return;
            if (activeWindow == null || activeWindow.uiDescription == null ||
                !IsIdle(activeWindow))
                return;

            string actionText = ModLocalization.Get("LOC_ParkingSortAction");
            ControlDescription warehouseSortDescription =
                FindWarehouseSortDescription(actionText);
            sortHint = WindowFooterHintController.RequestNativeHint(
                new WindowFooterHintController.NativeHintRequest {
                    WindowId = FooterWindowId,
                    WindowRoot = activeWindow.transform,
                    HintRoot = activeWindow.uiDescription.transform,
                    HintId = SortHintId,
                    Keys = new string[] { "C" },
                    Text = actionText,
                    Action = new Action(OpenSortingWindow),
                    Row = 0,
                    Order = 5,
                    Profile = WindowFooterHintController.NativeFooterProfile.Automatic,
                    VariantSource = warehouseSortDescription,
                });
            if (sortHint != null && sortHint.Description != null &&
                warehouseSortDescription != null &&
                warehouseSortDescription.buttonImage != null &&
                warehouseSortDescription.buttonImage.sprite != null) {
                sortHint.Description.SetMainButton(
                    warehouseSortDescription.buttonImage.sprite);
                sortHint.Description.RefreshLayout();
            }
        }

        private static void DestroySortHint()
        {
            WindowFooterHintController.RemoveHint(FooterWindowId, SortHintId);
            sortHint = null;
        }

        private static ControlDescription FindWarehouseSortDescription(
            string actionText)
        {
            if (string.IsNullOrEmpty(actionText) || WindowManager.Instance == null)
                return null;

            WarehouseWindow warehouse = WindowManager.Instance
                .GetWindowByID<WarehouseWindow>(WindowID.Warehouse);
            if (warehouse == null)
                return null;

            ControlDescription[] descriptions = warehouse
                .GetComponentsInChildren<ControlDescription>(true);
            for (int i = 0; i < descriptions.Length; i++) {
                ControlDescription description = descriptions[i];
                if (description == null || description.texts == null)
                    continue;
                for (int j = 0; j < description.texts.Length; j++) {
                    if (description.texts[j] != null &&
                        string.Equals(description.texts[j].text, actionText,
                            StringComparison.CurrentCultureIgnoreCase))
                        return description;
                }
            }
            return null;
        }

        private static void OpenSortingWindow()
        {
            if (!IsEnabled || activeWindow == null || !IsIdle(activeWindow))
                return;
            if (sortingWindow != null && sortingWindow.Root != null)
                return;

            WindowManager manager = WindowManager.Instance;
            SortingWindow source = manager != null
                ? manager.GetWindowByID<SortingWindow>(WindowID.Sorting)
                : null;
            if (source == null)
                return;

            DestroySortHint();
            string[] captions = {
                ModLocalization.Get("LOC_ParkingSortArrivalDescending"),
                ModLocalization.Get("LOC_ParkingSortArrivalAscending"),
                ModLocalization.Get("LOC_ParkingSortConditionDescending"),
                ModLocalization.Get("LOC_ParkingSortConditionAscending"),
                ModLocalization.Get("LOC_ParkingSortNameDescending"),
                ModLocalization.Get("LOC_ParkingSortNameAscending"),
            };
            sortingWindow = NativeUiFactory.CreateSortingWindow(
                source, "CMS21UIPlus.ParkingSortingWindow",
                ModLocalization.Get("LOC_ParkingSortWindowTitle"), captions,
                new Action<int>(HandleSortSelection));
            if (sortingWindow == null || sortingWindow.Root == null)
                CreateSortHint();
        }

        private static void HandleSortSelection(int index)
        {
            SortType selected;
            switch (index) {
                case 0:
                    selected = SortType.ByDateDesc;
                    break;
                case 1:
                    selected = SortType.ByDateAsc;
                    break;
                case 2:
                    selected = SortType.ByConditionDesc;
                    break;
                case 3:
                    selected = SortType.ByConditionAsc;
                    break;
                case 4:
                    selected = SortType.ByAlphabetDesc;
                    break;
                case 5:
                    selected = SortType.ByAlphabetAsc;
                    break;
                default:
                    return;
            }

            DestroySortingWindow();
            SetParkingSortType(selected);
            CreateSortHint();
        }

        private static void DestroySortingWindow()
        {
            if (sortingWindow == null)
                return;
            NativeUiFactory.DestroySortingWindow(sortingWindow);
            sortingWindow = null;
        }

        private static void SetParkingSortType(SortType newSortType)
        {
            sortType = newSortType;

            if (activeWindow == null)
                return;

            ApplyPhysicalSort(activeWindow);
        }

        private static void ApplyPhysicalSort(ParkingManagementWindow window)
        {
            if (window == null || GlobalState.GameManager == null ||
                GlobalState.GameManager.GameDataManager == null ||
                GlobalState.GameManager.GameDataManager.CurrentProfileData == null)
                return;

            var cars = GlobalState.GameManager.GameDataManager
                .CurrentProfileData.carsOnParking;
            if (cars == null)
                return;

            int capacity = GlobalData.GetMaxParkingPlacesAmount();
            if (capacity <= 0)
                return;
            capacity = Math.Min(capacity, cars.Count);

            List<ParkingEntry> entries = BuildParkingEntries(capacity);
            if (entries.Count <= 1)
                return;

            RefreshArrivalOrder(entries);
            entries.Sort(CompareEntries);

            int[] currentAtSlot = new int[capacity];
            int[] slotOfIdentity = new int[capacity];
            string[] identityKeys = new string[capacity];
            for (int i = 0; i < capacity; i++) {
                currentAtSlot[i] = -1;
                slotOfIdentity[i] = -1;
            }
            for (int i = 0; i < entries.Count; i++) {
                int identity = entries[i].OriginalIndex;
                currentAtSlot[identity] = identity;
                slotOfIdentity[identity] = identity;
                identityKeys[identity] = entries[i].ArrivalKey;
            }

            for (int target = 0; target < entries.Count; target++) {
                int desiredIdentity = entries[target].OriginalIndex;
                int source = slotOfIdentity[desiredIdentity];
                if (source < 0 || source == target)
                    continue;

                int displacedIdentity = currentAtSlot[target];
                try {
                    ParkingCarPlaceManager.MoveCar(source, target);
                } catch {
                    break;
                }

                if (!SlotMatches(target, identityKeys[desiredIdentity]) ||
                    !SlotMatches(source, displacedIdentity >= 0
                        ? identityKeys[displacedIdentity] : null))
                    break;

                currentAtSlot[target] = desiredIdentity;
                slotOfIdentity[desiredIdentity] = target;
                currentAtSlot[source] = displacedIdentity;
                if (displacedIdentity >= 0)
                    slotOfIdentity[displacedIdentity] = source;
            }

            window.PrepareSourceButtons(window.currentSourceParkingLevel);
        }


        private static bool SlotMatches(int index, string expectedKey)
        {
            var cars = GlobalState.GameManager.GameDataManager
                .CurrentProfileData.carsOnParking;
            if (cars == null || index < 0 || index >= cars.Count)
                return false;

            NewCarData car = cars[index];
            if (string.IsNullOrEmpty(expectedKey))
                return car == null || car.IsDefault();
            return car != null && !car.IsDefault() &&
                string.Equals(GetArrivalKey(car), expectedKey,
                    StringComparison.Ordinal);
        }

        private static List<ParkingEntry> BuildParkingEntries(int capacity)
        {
            var cars = GlobalState.GameManager.GameDataManager
                .CurrentProfileData.carsOnParking;
            List<ParkingEntry> entries = new List<ParkingEntry>(capacity);
            bool needsName = sortType == SortType.ByAlphabetAsc ||
                sortType == SortType.ByAlphabetDesc;
            bool needsCondition = needsName ||
                sortType == SortType.ByConditionAsc ||
                sortType == SortType.ByConditionDesc ||
                sortType == SortType.ByConditionTuningFirst;

            for (int i = 0; i < capacity; i++) {
                NewCarData car = cars[i];
                if (car == null || car.IsDefault())
                    continue;

                ParkingEntry entry = new ParkingEntry {
                    OriginalIndex = i,
                    ArrivalKey = GetArrivalKey(car),
                    DisplayName = needsName ? GetDisplayName(car) : string.Empty,
                    Condition = needsCondition
                        ? MapCarInformationFeature.CalculateTotalCondition(car) : 0f,
                };
                entries.Add(entry);
            }
            return entries;
        }

        private static void RefreshArrivalOrder(List<ParkingEntry> entries)
        {
            Types.ProfileState profile = GetCurrentProfileState();
            if (profile == null) {
                for (int i = 0; i < entries.Count; i++)
                    entries[i].ArrivalRank = i;
                return;
            }

            string[] previous = profile.parkingArrivalOrder;
            int orderCapacity = previous != null
                ? Math.Max(previous.Length, entries.Count)
                : entries.Count;
            List<string> order = new List<string>(orderCapacity);
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);

            if (previous != null) {
                for (int i = 0; i < previous.Length; i++) {
                    string key = previous[i];
                    if (!string.IsNullOrEmpty(key) && added.Add(key))
                        order.Add(key);
                }
            }
            for (int i = 0; i < entries.Count; i++) {
                string key = entries[i].ArrivalKey;
                if (added.Add(key))
                    order.Add(key);
            }

            bool changed = previous == null || previous.Length != order.Count;
            if (!changed) {
                for (int i = 0; i < order.Count; i++) {
                    if (!string.Equals(previous[i], order[i],
                        StringComparison.Ordinal)) {
                        changed = true;
                        break;
                    }
                }
            }
            if (changed) {
                profile.parkingArrivalOrder = order.ToArray();
                Main.MarkProfileMemoryDirty();
            }

            Dictionary<string, int> ranks =
                new Dictionary<string, int>(order.Count, StringComparer.Ordinal);
            for (int i = 0; i < order.Count; i++)
                ranks[order[i]] = i;
            for (int i = 0; i < entries.Count; i++) {
                int rank;
                entries[i].ArrivalRank = ranks.TryGetValue(
                    entries[i].ArrivalKey, out rank) ? rank : int.MaxValue;
            }
        }

        private static Types.ProfileState GetCurrentProfileState()
        {
            if (Main.ProfileMemory == null ||
                Main.ProfileMemory.profileStates == null ||
                GlobalState.LoadedProfileId < 0 ||
                GlobalState.LoadedProfileId >= Main.ProfileMemory.profileStates.Length)
                return null;
            return Main.ProfileMemory.profileStates[GlobalState.LoadedProfileId];
        }

        private static string GetArrivalKey(NewCarData car)
        {
            if (car == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(car.UId))
                return "uid:" + car.UId;

            string plate = car.LicensePlatesData != null
                ? car.LicensePlatesData.LicensePlateNumberRear ?? string.Empty
                : string.Empty;
            CarInfoData info = car.CarInfoData;
            return "car:" + (car.carToLoad ?? string.Empty) +
                "|cfg:" + car.configVersion +
                "|plate:" + plate +
                "|price:" + info.BuyPrice +
                "|from:" + info.CarFrom;
        }

        private static string GetDisplayName(NewCarData car)
        {
            if (car == null)
                return string.Empty;

            try {
                if (GlobalState.GameManager != null &&
                    GlobalState.GameManager.CarBundleLoader != null) {
                    string name = GlobalState.GameManager.CarBundleLoader
                        .GetCarNameWithSuffix(car.carToLoad, car.configVersion);
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            } catch {
            }
            return car.carToLoad ?? string.Empty;
        }

        private static int CompareEntries(ParkingEntry left, ParkingEntry right)
        {
            int result;
            switch (sortType) {
                case SortType.ByAlphabetAsc:
                case SortType.ByAlphabetDesc:
                    result = StringComparer.CurrentCultureIgnoreCase.Compare(
                        left.DisplayName, right.DisplayName);
                    if (sortType == SortType.ByAlphabetDesc)
                        result = -result;
                    if (result == 0)
                        result = right.Condition.CompareTo(left.Condition);
                    break;
                case SortType.ByConditionAsc:
                case SortType.ByConditionDesc:
                case SortType.ByConditionTuningFirst:
                    result = left.Condition.CompareTo(right.Condition);
                    if (sortType != SortType.ByConditionAsc)
                        result = -result;
                    break;
                case SortType.ByDateDesc:
                    result = right.ArrivalRank.CompareTo(left.ArrivalRank);
                    break;
                default:
                    result = left.ArrivalRank.CompareTo(right.ArrivalRank);
                    break;
            }
            return result != 0
                ? result
                : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static bool IsIdle(ParkingManagementWindow window)
        {
            return window != null && !window.isCarSwapActive &&
                string.Equals(window.State.ToString(), "Idle",
                    StringComparison.Ordinal);
        }

    }

}
