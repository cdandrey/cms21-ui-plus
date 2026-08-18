using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.UI.Logic.CarInfo;
using Il2CppCMS.UI.Logic.Tabs.CarInfo;
#else
using CMS;
using CMS.UI.Logic.CarInfo;
using CMS.UI.Logic.Tabs.CarInfo;
#endif

namespace Cms21UiPlus
{
    /// <summary>Adds marking and cyan highlighting for body parts in repair jobs.</summary>
    [HarmonyPatch]
    public static class BodyPartMarkingFeature
    {
        private const string Marked = "merkattuBody";
        private const string Unmarked = "unmerkattuBody";

        private sealed class CallbackBinding
        {
            public InteractiveObject Interactive;
            public Il2CppSystem.Action Previous;
        }

        private static readonly Dictionary<int, CallbackBinding> CallbackBindings =
            new Dictionary<int, CallbackBinding>();

        private static bool hasActiveHighlights;
        private static GameObject lastMouseOverObject;
        private static int highlightWorkerGeneration = -1;
        private static int highlightRequestVersion;
        private static int garageGeneration;

        private static bool IsEnabled {
            get {
                return GlobalState.IsGarageSceneActive && Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.markBodyParts;
            }
        }

        public static void OnGarageSceneUnloaded()
        {
            garageGeneration++;
            highlightRequestVersion++;
            hasActiveHighlights = false;
            lastMouseOverObject = null;

            foreach (CallbackBinding binding in CallbackBindings.Values) {
                if (binding.Interactive != null)
                    binding.Interactive.OnTakeOnFinished = binding.Previous;
            }
            CallbackBindings.Clear();
        }

        [HarmonyPatch(typeof(GameScript), nameof(GameScript.SetIOMouseOver))]
        [HarmonyPostfix]
        public static void GameScriptSetIOMouseOverPostfix(GameObject go)
        {
            RefreshWhenMouseTargetChanges(go);
        }

        [HarmonyPatch(typeof(GameScript), nameof(GameScript.SetIOMouseOverNull))]
        [HarmonyPostfix]
        public static void GameScriptSetIOMouseOverNullPostfix()
        {
            RefreshWhenMouseTargetChanges(null);
        }

        [HarmonyPatch(typeof(PartListItem), nameof(PartListItem.SetupPartData))]
        [HarmonyPrefix]
        public static void PartListItemSetupPartDataPrefix(CarInfoPart part)
        {
            if (!IsEnabled || part == null || GlobalState.GameManager == null)
                return;

            try {
                GameScript gameScript = GameScript.Get();
                if (gameScript == null)
                    return;

                CarLoader carLoader = gameScript.GetIOMouseOverCarLoader2();
                if (carLoader == null)
                    return;

                Job job = GlobalState.GameManager.OrderGenerator.GetJobForCarLoader(
                    CarLoaderPlaces.Get().GetCarLoaderId(carLoader));
                if (job == null)
                    return;

                foreach (JobTask task in job.jobTasks) {
                    if (task.type != "Body")
                        continue;

                    foreach (JobPart jobPart in task.Parts) {
                        if (jobPart.Name != part.Name)
                            continue;

                        CarPart carPart = carLoader.GetCarPart(jobPart.ID);
                        if (carPart == null)
                            continue;

                        part.CanBeMarked = true;
                        if (part.MarkAction == null) {
                            part.MarkAction = new Action<bool>(marked => {
                                carPart.AdditionalString = marked ? Marked : Unmarked;
                                if (marked)
                                    hasActiveHighlights = true;
                                RequestHighlightRefresh();
                            });
                        }

                        if (carPart.AdditionalString == Marked) {
                            part.IsMarked = true;
                            hasActiveHighlights = true;
                        }
                    }
                }
            } catch (Exception exception) {
                ModLogger.Log("[Jobs] Failed to configure body-part marking." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
            }
        }

        private static void RefreshWhenMouseTargetChanges(GameObject current)
        {
            if (!IsEnabled || !hasActiveHighlights || lastMouseOverObject == current)
                return;

            lastMouseOverObject = current;
            RequestHighlightRefresh();
        }

        private static void RequestHighlightRefresh()
        {
            highlightRequestVersion++;
            int generation = garageGeneration;
            if (highlightWorkerGeneration == generation)
                return;

            highlightWorkerGeneration = generation;
            MelonCoroutines.Start(UpdateBodyPartHighlights(generation));
        }

        private static IEnumerator UpdateBodyPartHighlights(int generation)
        {
            int processedVersion = -1;
            try {
                while (IsEnabled && generation == garageGeneration &&
                    processedVersion != highlightRequestVersion) {
                    processedVersion = highlightRequestVersion;
                    yield return new WaitForEndOfFrame();
                    if (!IsEnabled || generation != garageGeneration)
                        yield break;

                    RefreshHighlightsOnce();
                }
            } finally {
                if (highlightWorkerGeneration == generation)
                    highlightWorkerGeneration = -1;
                if (IsEnabled && generation == garageGeneration &&
                    processedVersion != highlightRequestVersion)
                    RequestHighlightRefresh();
            }
        }

        private static void RefreshHighlightsOnce()
        {
            GameScript gameScript = GameScript.Get();
            CarLoader carLoader = gameScript != null
                ? gameScript.GetIOMouseOverCarLoader2()
                : null;
            if (carLoader == null || !hasActiveHighlights)
                return;

            bool anyMarked = false;
            foreach (CarPart carPart in carLoader.GetCarParts()) {
                try {
                    if (carPart == null || carPart.handle == null ||
                        string.IsNullOrEmpty(carPart.AdditionalString))
                        continue;

                    InteractiveObject interactive =
                        carPart.handle.GetComponent<InteractiveObject>();
                    if (interactive == null)
                        continue;

                    if (carPart.AdditionalString == Marked) {
                        anyMarked = true;
                        interactive.HighlightAll(Color.cyan);
                        ConfigureFinishedBodyPartUnmark(
                            carLoader, carPart, interactive);
                    } else {
                        interactive.HighlightNone();
                    }
                } catch (Exception exception) {
                    ModLogger.Log("[Jobs] Failed to refresh body-part highlight." +
                        Environment.NewLine + exception,
                        Types.LoggingLevels.Error);
                }
            }
            hasActiveHighlights = anyMarked;
        }

        private static void ConfigureFinishedBodyPartUnmark(CarLoader carLoader,
            CarPart carPart, InteractiveObject interactive)
        {
            if (Main.SettingsEntry == null ||
                !Main.SettingsEntry.Value.unmarkFinishedParts)
                return;

            int id = interactive.GetInstanceID();
            if (CallbackBindings.ContainsKey(id))
                return;

            Il2CppSystem.Action previous = interactive.OnTakeOnFinished;
            CallbackBindings[id] = new CallbackBinding {
                Interactive = interactive,
                Previous = previous
            };

            interactive.OnTakeOnFinished = new Action(delegate {
                if (previous != null)
                    previous.Invoke();

                UnmarkFinishedPart(carLoader, carPart);
            });
        }

        private static void UnmarkFinishedPart(CarLoader carLoader, CarPart carPart)
        {
            if (!IsEnabled || carLoader == null || carPart == null ||
                GlobalState.GameManager == null)
                return;

            Job job = GlobalState.GameManager.OrderGenerator.GetJobForCarLoader(
                CarLoaderPlaces.Get().GetCarLoaderId(carLoader));
            if (job == null)
                return;

            foreach (JobTask task in job.jobTasks) {
                if (task.type != "Body")
                    continue;

                foreach (JobPart jobPart in task.Parts) {
                    if (carPart.name == jobPart.ID && carPart.Condition > 0.99f) {
                        carPart.AdditionalString = Unmarked;
                        RequestHighlightRefresh();
                        return;
                    }
                }
            }
        }
    }
}
