using System;
using System.Collections.Generic;

namespace Cms21UiPlus
{
    public static class ModSettingDependencyRegistry
    {
        public const string Available = "available";
        public const string Partial = "partial";
        public const string Unavailable = "unavailable";
        public const string UnavailableByDefault = "unavailableByDefault";

        private static readonly Dictionary<string, string> statuses =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static event Action Changed;

        public static void SetAvailable(string providerId, string dependencyId,
            bool available)
        {
            SetStatus(providerId, dependencyId,
                available ? Available : Unavailable);
        }

        public static void SetStatus(string providerId, string dependencyId,
            string status)
        {
            if (string.IsNullOrWhiteSpace(providerId) ||
                string.IsNullOrWhiteSpace(dependencyId))
                return;

            string normalized;
            if (!TryNormalizeStatus(status, out normalized))
                return;

            string key = BuildKey(providerId, dependencyId);
            string current;
            if (statuses.TryGetValue(key, out current) &&
                string.Equals(current, normalized,
                    StringComparison.OrdinalIgnoreCase))
                return;

            statuses[key] = normalized;
            Action changed = Changed;
            if (changed != null)
                changed();
        }

        internal static string GetStatus(string providerId,
            string dependencyId)
        {
            if (string.IsNullOrWhiteSpace(providerId) ||
                string.IsNullOrWhiteSpace(dependencyId))
                return Available;

            string value;
            return statuses.TryGetValue(
                BuildKey(providerId, dependencyId), out value)
                ? value : Available;
        }

        private static bool TryNormalizeStatus(string status,
            out string normalized)
        {
            normalized = null;
            if (string.Equals(status, Available,
                StringComparison.OrdinalIgnoreCase))
                normalized = Available;
            else if (string.Equals(status, Partial,
                StringComparison.OrdinalIgnoreCase))
                normalized = Partial;
            else if (string.Equals(status, Unavailable,
                StringComparison.OrdinalIgnoreCase))
                normalized = Unavailable;
            else if (string.Equals(status, UnavailableByDefault,
                StringComparison.OrdinalIgnoreCase))
                normalized = UnavailableByDefault;
            return normalized != null;
        }

        private static string BuildKey(string providerId, string dependencyId)
        {
            return providerId + "\n" + dependencyId;
        }
    }
}
