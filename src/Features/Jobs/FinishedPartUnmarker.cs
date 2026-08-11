using System;
using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.PartModules;
#else
using CMS;
using CMS.PartModules;
#endif

namespace Cms21UiPlus
{
    /// <summary>Clears job-part marks once a mounted part satisfies the job condition.</summary>
    [HarmonyPatch]
    public static class FinishedPartUnmarker
    {
        private static bool IsEnabled {
            get {
                return GlobalState.IsGarageSceneActive && Main.SettingsEntry != null &&
                    Main.SettingsEntry.Value.unmarkFinishedParts;
            }
        }

        [HarmonyPatch(typeof(PartScript), nameof(PartScript.ShowMounted))]
        [HarmonyPrefix]
        public static void PartScriptShowMountedPrefix(PartScript __instance)
        {
            if (IsEnabled)
                TryUnmarkMountedPart(__instance, false);
        }

        [HarmonyPatch(typeof(PartScript), nameof(PartScript.SetCondition))]
        [HarmonyPostfix]
        public static void PartScriptSetConditionPostfix(PartScript __instance)
        {
            if (!IsEnabled || __instance.IsUnmounted ||
                GameMode.Get().GetCurrentMode() != gameMode.PartSelectMount)
                return;

            TryUnmarkMountedPart(__instance, true);
        }

        private static void TryUnmarkMountedPart(PartScript part, bool instantMount)
        {
            try {
                CarLoader carLoader = GameScript.Get().GetIOMouseOverCarLoader2();
                if (carLoader == null)
                    return;

                Job job = GlobalState.GameManager.OrderGenerator.GetJobForCarLoader(
                    CarLoaderPlaces.Get().GetCarLoaderId(carLoader));
                if (job == null)
                    return;

                UnmarkIfFinished(part, job.globalCondition,
                    instantMount ? "unmarked done B" : "unmarked done");
                foreach (PartScript linkedPart in part.GetUnmountWith()) {
                    UnmarkIfFinished(linkedPart, job.globalCondition,
                        instantMount ? "unmarked done B with" : "unmarked done with");
                }
            } catch (Exception exception) {
                ModLogger.Log("[Jobs] Failed to update finished-part marking." +
                    Environment.NewLine + exception, Types.LoggingLevels.Error);
            }
        }

        private static void UnmarkIfFinished(PartScript part, float requiredCondition,
            string logSuffix)
        {
            if (part == null || part.Condition < requiredCondition ||
                !part.markImportantPart)
                return;

            part.markImportantPart = false;
            ModLogger.Log(part.name + " " + logSuffix, Types.LoggingLevels.Debug);
        }
    }
}
