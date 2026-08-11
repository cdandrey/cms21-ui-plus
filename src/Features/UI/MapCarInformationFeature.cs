using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS;
using Il2CppCMS.UI.Controls;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Logic.CarInfo;
using Il2CppCMS.UI.Logic.Map.Info;
using Il2CppCMS.UI.Logic.Parking;
using Il2CppCMS.UI.Logic.Tabs.CarInfo;
using Il2CppCMS.UI.Windows;
#else
using CMS;
using CMS.UI.Controls;
using CMS.UI.Logic;
using CMS.UI.Logic.CarInfo;
using CMS.UI.Logic.Map.Info;
using CMS.UI.Logic.Parking;
using CMS.UI.Logic.Tabs.CarInfo;
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    /// <summary>Adds condition and licence-plate information to map and parking car cards.</summary>
    [HarmonyPatch]
    public static class MapCarInformationFeature
    {
        private static bool IsEnabled {
            get {
                return Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.showCarConditionOnMap;
            }
        }

        [HarmonyPatch(typeof(ShowroomCarItem), nameof(ShowroomCarItem.SetupForCarConfigData))]
        [HarmonyPostfix]
        public static void SetupForCarConfigDataPostfix(CarConfigData data,
            ShowroomCarItem __instance)
        {
            if (!IsEnabled || __instance == null || data == null ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Showroom_2")
                return;

            CarRibbon ribbon = __instance.GetComponentInChildren<CarRibbon>();
            if (ribbon == null)
                return;

            Transform versionRibbon = FindChildByNameFragment(
                ribbon.transform, "config", "version");
            if (versionRibbon == null && ribbon.transform.childCount > 2)
                versionRibbon = ribbon.transform.GetChild(2);
            if (versionRibbon == null)
                return;
            versionRibbon.gameObject.SetActive(false);
            if (__instance.VersionsCount > 1) {
                Text text = versionRibbon.GetComponentInChildren<Text>();
                if (text != null)
                    text.text = "<size=8>" +
                        ModLocalization.Get("LOC_ConfigsLabel") + " " +
                        __instance.VersionsCount + "</size>";
                versionRibbon.gameObject.SetActive(true);
            }

            Transform dlcRibbon = FindChildByNameFragment(ribbon.transform, "dlc");
            if (dlcRibbon == null && ribbon.transform.childCount > 0)
                dlcRibbon = ribbon.transform.GetChild(0);
            if (dlcRibbon != null) {
                Text dlcText = dlcRibbon.GetComponentInChildren<Text>();
                if (dlcText != null) {
                    bool installed = data.DLC > 0 && GlobalState.GameManager != null &&
                        GlobalState.GameManager.PlatformManager.IsDLCInstalled(data.DLC - 1);
                    dlcText.text = ModLocalization.Get("LOC_DlcLabel") +
                        (installed ? " *" : string.Empty);
                }
            }
        }

        [HarmonyPatch(typeof(ShowroomCarItem), nameof(ShowroomCarItem.SetupForCarLoader))]
        [HarmonyPostfix]
        public static void SetupForCarLoaderPostfix(CarLoader carLoader,
            ShowroomCarItem __instance)
        {
            if (!IsEnabled || !GlobalState.IsGarageSceneActive ||
                carLoader == null || __instance == null)
                return;

            float totalCondition = (
                Helper.RoundCondition(carLoader.GetPanelsGlobalCondition()) +
                Helper.RoundCondition(carLoader.GetPartsGlobalCondition()) +
                Helper.RoundCondition(carLoader.GetBodyCondition()) +
                Helper.RoundCondition(carLoader.GetInteriorCondition())) / 400f;

            SetupConditionRibbon(__instance.carRibbon, totalCondition);
            string plate = carLoader.LicensePlatesData != null
                ? carLoader.LicensePlatesData.LicensePlateNumberRear
                : null;
            SetupPlateRibbon(__instance, plate);
        }

        [HarmonyPatch(typeof(ParkingCarPanel), nameof(ParkingCarPanel.PrepareForCar))]
        [HarmonyPostfix]
        public static void PrepareForCarPostfix(int saveIndex,
            ParkingCarPanel __instance)
        {
            if (!IsEnabled || !GlobalState.IsGarageSceneActive ||
                __instance == null || GlobalState.GameManager == null)
                return;

            var parkedCars = GlobalState.GameManager.GameDataManager.CurrentProfileData.carsOnParking;
            if (saveIndex < 0 || saveIndex >= parkedCars.Count)
                return;

            NewCarData carData = parkedCars[saveIndex];
            if (carData == null || __instance.showroomCarItem == null)
                return;

            SetupConditionRibbon(__instance.showroomCarItem.carRibbon,
                CalculateTotalCondition(carData));
            string plate = carData.LicensePlatesData != null
                ? carData.LicensePlatesData.LicensePlateNumberRear
                : null;
            SetupPlateRibbon(__instance.showroomCarItem, plate);
        }

        private static void SetupConditionRibbon(CarRibbon ribbon, float condition)
        {
            if (ribbon == null)
                return;

            Condition conditionControl = ribbon.GetComponentInChildren<Condition>(true);
            GameObject target = conditionControl != null
                ? conditionControl.gameObject
                : null;
            if (target == null && ribbon.transform.childCount > 2)
                target = ribbon.transform.GetChild(2).gameObject;
            if (target == null)
                return;

            target.SetActive(true);
            if (conditionControl == null)
                conditionControl = target.GetComponent<Condition>();
            if (conditionControl == null) {
                conditionControl = target.AddComponent<Condition>();
                conditionControl.Color = target.GetComponent<Image>();
                conditionControl.Percentage = target.GetComponentInChildren<Text>();
            }
            conditionControl.Set(Mathf.Clamp01(condition));
        }

        private static void SetupPlateRibbon(ShowroomCarItem item, string plate)
        {
            if (item == null)
                return;

            Transform plateRibbon = FindChildByNameFragment(
                item.transform, "license", "plate");
            if (plateRibbon == null && item.transform.childCount > 10)
                plateRibbon = item.transform.GetChild(10);
            if (plateRibbon == null)
                return;

            GameObject target = plateRibbon.gameObject;
            target.SetActive(false);
            if (string.IsNullOrWhiteSpace(plate))
                return;

            Text text = target.GetComponentInChildren<Text>();
            if (text == null)
                return;

            text.text = "<color=#FFA000>" + EscapeRichText(plate) + "</color>";
            target.SetActive(true);
        }

        private static Transform FindChildByNameFragment(Transform root,
            params string[] fragments)
        {
            if (root == null || fragments == null || fragments.Length == 0)
                return null;

            Transform bestMatch = null;
            int bestScore = 0;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children) {
                if (child == null || child == root)
                    continue;

                string name = child.name ?? string.Empty;
                int score = 0;
                foreach (string fragment in fragments) {
                    if (!string.IsNullOrEmpty(fragment) &&
                        name.IndexOf(fragment,
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                        score++;
                }

                if (score == fragments.Length)
                    return child;
                if (score > bestScore) {
                    bestScore = score;
                    bestMatch = child;
                }
            }
            return bestMatch;
        }

        private static string EscapeRichText(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static float CalculateTotalCondition(NewCarData carData)
        {
            float total =
                Helper.RoundCondition(CalculatePanelsCondition(carData)) +
                Helper.RoundCondition(CalculatePartsCondition(carData)) +
                Helper.RoundCondition(CalculateBodyCondition(carData)) +
                Helper.RoundCondition(CalculateInteriorCondition(carData));
            return total / 400f;
        }

        private static float CalculatePanelsCondition(NewCarData carData)
        {
            if (carData.BodyPartsData == null)
                return 0f;

            float total = 0f;
            int count = 0;
            string frontPlate = GetCarProperty(carData, "licensePlateFront", "9_exterior");
            string rearPlate = GetCarProperty(carData, "licensePlateRear", "9_exterior");

            foreach (BodyPartData part in carData.BodyPartsData) {
                string id = part.Id;
                if (id.StartsWith("bench") || id.StartsWith("seat") ||
                    id.StartsWith("body") || id.StartsWith("details") ||
                    id.StartsWith("steeringWheel") ||
                    (id == "license_plate_front" && frontPlate == "#Dummy") ||
                    (id == "license_plate_rear" && rearPlate == "#Dummy"))
                    continue;

                if (!part.Unmounted)
                    total += part.Condition;
                count++;
            }
            return count > 0 ? total / count : 0f;
        }

        private static float CalculatePartsCondition(NewCarData carData)
        {
            if (carData.PartData == null)
                return 0f;

            float total = 0f;
            int count = 0;
            foreach (PartData part in carData.PartData) {
                if (!part.Unmounted)
                    total += part.Condition;
                count++;
            }
            return count > 0 ? total / count : 0f;
        }

        private static float CalculateBodyCondition(NewCarData carData)
        {
            if (carData.BodyPartsData == null)
                return 0f;

            foreach (BodyPartData part in carData.BodyPartsData) {
                if (part.Id == "body")
                    return part.Condition;
            }
            return 0f;
        }

        private static float CalculateInteriorCondition(NewCarData carData)
        {
            if (carData.BodyPartsData == null)
                return 0f;

            string bench = GetCarProperty(carData, "bench", "5_interior");
            string frontBench = GetCarProperty(carData, "bench_front", "5_interior");
            string leftSeat = GetCarProperty(carData, "seatLeft", "5_interior");
            string rightSeat = GetCarProperty(carData, "seatRight", "5_interior");
            string wheel = GetCarProperty(carData, "wheel", "5_interior");

            float total = 0f;
            int count = 0;
            foreach (BodyPartData part in carData.BodyPartsData) {
                bool isInterior =
                    (part.Id == "bench" && bench != "#Dummy") ||
                    (part.Id == "benchFront" && frontBench != "#Dummy") ||
                    (part.Id == "seatLeft" && leftSeat != "#Dummy") ||
                    (part.Id == "seatRight" && rightSeat != "#Dummy") ||
                    (part.Id == "steeringWheel" && wheel != "#Dummy") ||
                    part.Id.StartsWith("details");
                if (!isInterior)
                    continue;

                if (!part.Unmounted)
                    total += part.Condition;
                count++;
            }
            return count > 0 ? total / count : 0f;
        }

        private static string GetCarProperty(NewCarData carData, string key,
            string section)
        {
            return GlobalState.GameManager.CarBundleLoader.GetCarPropertyString(
                carData.carToLoad, carData.configVersion, key, section, string.Empty);
        }
    }
}
