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
        private delegate string GetRepairAvailabilityIndicatorPathDelegate(
            bool available);

        private static readonly Type RulesType =
            Type.GetType(RulesTypeName, false);
        private static readonly TryGetRepairDisplayDataDelegate
            RepairDisplayDataResolver = ResolveRepairDisplayDataResolver();
        private static readonly GetRepairAvailabilityIndicatorPathDelegate
            RepairAvailabilityIndicatorPathResolver =
                ResolveRepairAvailabilityIndicatorPathResolver();

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
