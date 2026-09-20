using System;
using System.Reflection;

namespace Cms21UiPlus
{
    internal static class GameplayRepairSkillBridge
    {
        private const string RulesTypeName =
            "Cms21GameplayPlus.RepairSkillRules, CMS21GameplayPlus";

        private delegate bool TryGetRepairDisplayDataDelegate(string itemId,
            out string[] indicatorPaths, out bool restorationAvailable);
        private delegate int GetRepairRequirementsMaskDelegate(string itemId);
        private delegate bool GetRepairSpecializationsEnabledDelegate();
        private delegate string GetRepairAvailabilityIndicatorPathDelegate(
            bool available);

        internal static readonly string[] RepairSpecializationIndicatorPaths = {
            @"Mods\CMS21GameplayPlus\SkillIndicators\SI_SkillCompose.png",
            @"Mods\CMS21GameplayPlus\SkillIndicators\SI_SkillTurner.png",
            @"Mods\CMS21GameplayPlus\SkillIndicators\SI_SkillGlazier.png",
            @"Mods\CMS21GameplayPlus\SkillIndicators\SI_SkillElectric.png",
            @"Mods\CMS21GameplayPlus\SkillIndicators\SI_SkillTailor.png",
            @"Mods\CMS21GameplayPlus\SkillIndicators\SI_SkillWelder.png",
        };

        private static readonly int[] GameplaySpecializationMasks = {
            1 << 11,
            1 << 9,
            1 << 12,
            1 << 10,
            1 << 13,
            1 << 8,
        };

        private static readonly Type RulesType =
            Type.GetType(RulesTypeName, false);
        private static readonly TryGetRepairDisplayDataDelegate
            RepairDisplayDataResolver = ResolveRepairDisplayDataResolver();
        private static readonly GetRepairRequirementsMaskDelegate
            RepairRequirementsMaskResolver =
                ResolveRepairRequirementsMaskResolver();
        private static readonly GetRepairSpecializationsEnabledDelegate
            RepairSpecializationsEnabledResolver =
                ResolveRepairSpecializationsEnabledResolver();
        private static readonly GetRepairAvailabilityIndicatorPathDelegate
            RepairAvailabilityIndicatorPathResolver =
                ResolveRepairAvailabilityIndicatorPathResolver();

        internal static bool SupportsRepairSpecializations {
            get {
                return RepairRequirementsMaskResolver != null &&
                    RepairSpecializationsEnabledResolver != null;
            }
        }

        internal static bool RepairSpecializationsEnabled {
            get {
                if (!SupportsRepairSpecializations)
                    return false;
                try {
                    return RepairSpecializationsEnabledResolver();
                } catch (Exception exception) {
                    ModLogger.Log("[RepairSkillIndicator] Gameplay+ repair " +
                        "specialization state query failed." +
                        Environment.NewLine + exception,
                        Types.LoggingLevels.Error);
                    return false;
                }
            }
        }

        internal static int GetRepairSpecializationMask(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) ||
                RepairRequirementsMaskResolver == null)
                return 0;

            try {
                int requirementsMask = RepairRequirementsMaskResolver(itemId);
                int result = 0;
                for (int index = 0;
                        index < GameplaySpecializationMasks.Length; index++) {
                    if ((requirementsMask &
                            GameplaySpecializationMasks[index]) != 0)
                        result |= 1 << index;
                }
                return result;
            } catch (Exception exception) {
                ModLogger.Log("[RepairSkillIndicator] Gameplay+ repair " +
                    "requirements query failed." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                return 0;
            }
        }

        public static bool TryGetRepairDisplayData(string itemId,
            out string[] indicatorPaths, out bool restorationAvailable)
        {
            indicatorPaths = null;
            restorationAvailable = false;
            if (string.IsNullOrEmpty(itemId) || RepairDisplayDataResolver == null)
                return false;

            try {
                return RepairDisplayDataResolver(itemId, out indicatorPaths,
                    out restorationAvailable);
            } catch (Exception exception) {
                ModLogger.Log("[RepairSkillIndicator] Gameplay+ repair display " +
                    "data query failed." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                indicatorPaths = null;
                restorationAvailable = false;
                return false;
            }
        }

        public static string GetRepairAvailabilityIndicatorPath(
            bool available)
        {
            if (RepairAvailabilityIndicatorPathResolver == null)
                return null;

            try {
                return RepairAvailabilityIndicatorPathResolver(available);
            } catch (Exception exception) {
                ModLogger.Log("[RepairSkillIndicator] Gameplay+ availability " +
                    "indicator query failed." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                return null;
            }
        }

        private static TryGetRepairDisplayDataDelegate
            ResolveRepairDisplayDataResolver()
        {
            MethodInfo method = ResolveMethod("TryGetRepairDisplayData",
                typeof(bool), typeof(string), typeof(string[]).MakeByRefType(),
                typeof(bool).MakeByRefType());
            return method != null
                ? (TryGetRepairDisplayDataDelegate)Delegate.CreateDelegate(
                    typeof(TryGetRepairDisplayDataDelegate), method)
                : null;
        }

        private static GetRepairRequirementsMaskDelegate
            ResolveRepairRequirementsMaskResolver()
        {
            MethodInfo method = ResolveMethod("GetRepairRequirementsMask",
                typeof(int), typeof(string));
            return method != null
                ? (GetRepairRequirementsMaskDelegate)Delegate.CreateDelegate(
                    typeof(GetRepairRequirementsMaskDelegate), method)
                : null;
        }

        private static GetRepairSpecializationsEnabledDelegate
            ResolveRepairSpecializationsEnabledResolver()
        {
            try {
                if (RulesType == null)
                    return null;

                PropertyInfo property = RulesType.GetProperty("IsEnabled",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo getter = property != null
                    ? property.GetGetMethod() : null;
                return getter != null && getter.ReturnType == typeof(bool)
                    ? (GetRepairSpecializationsEnabledDelegate)
                        Delegate.CreateDelegate(
                            typeof(GetRepairSpecializationsEnabledDelegate),
                            getter)
                    : null;
            } catch (Exception exception) {
                ModLogger.Log("[RepairSkillIndicator] Gameplay+ integration " +
                    "initialization failed." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                return null;
            }
        }

        private static GetRepairAvailabilityIndicatorPathDelegate
            ResolveRepairAvailabilityIndicatorPathResolver()
        {
            MethodInfo method = ResolveMethod(
                "GetRepairAvailabilityIndicatorPath", typeof(string),
                typeof(bool));
            return method != null
                ? (GetRepairAvailabilityIndicatorPathDelegate)
                    Delegate.CreateDelegate(
                        typeof(GetRepairAvailabilityIndicatorPathDelegate),
                        method)
                : null;
        }

        private static MethodInfo ResolveMethod(string name, Type returnType,
            params Type[] parameterTypes)
        {
            try {
                if (RulesType == null)
                    return null;

                MethodInfo method = RulesType.GetMethod(name,
                    BindingFlags.Public | BindingFlags.Static, null,
                    parameterTypes, null);
                return method != null && method.ReturnType == returnType
                    ? method : null;
            } catch (Exception exception) {
                ModLogger.Log("[RepairSkillIndicator] Gameplay+ integration " +
                    "initialization failed." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                return null;
            }
        }
    }
}
