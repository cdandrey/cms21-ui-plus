using System;
using System.Collections.Generic;
using System.Text;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
#else
using CMS;
using CMS.Containers;
#endif

namespace Cms21UiPlus
{
    /// <summary>
    /// Defines one identity rule for inventory, warehouse and shopping-list parts.
    /// Normal parts are identified by ID. Tires and rims additionally use their
    /// dimensional parameters so incompatible wheels are never merged.
    /// </summary>
    public static class PartIdentityComparer
    {
        private static readonly Dictionary<string, bool> WheelParameterCache =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public static bool HasWheelParameters(string itemID)
        {
            if (string.IsNullOrEmpty(itemID))
                return false;

            bool cached;
            if (WheelParameterCache.TryGetValue(itemID, out cached))
                return cached;

            GameInventory gameInventory = Singleton<GameInventory>.Instance;
            if (gameInventory == null)
                return false;

            bool hasWheelParameters = false;
            if (gameInventory.ExistsInPartProperty(itemID)) {
                PartProperty partProperty = gameInventory.GetItemProperty(itemID);
                hasWheelParameters = partProperty != null &&
                    (partProperty.SpecialGroup == SpecialGroup.Rim ||
                     partProperty.SpecialGroup == SpecialGroup.Tire);
            }

            WheelParameterCache[itemID] = hasWheelParameters;
            return hasWheelParameters;
        }

        public static string GetKey(Item item)
        {
            if (item == null)
                return string.Empty;

            return GetKey(item.ID, item.WheelData.ET, item.WheelData.Profile,
                item.WheelData.Size, item.WheelData.Width);
        }

        public static string GetKey(GroupItem group)
        {
            if (group == null || group.ItemList == null ||
                group.ItemList.Count == 0)
                return string.Empty;

            List<string> componentKeys =
                new List<string>(group.ItemList.Count);
            foreach (Item component in group.ItemList) {
                string componentKey = GetKey(component);
                if (!string.IsNullOrEmpty(componentKey))
                    componentKeys.Add(componentKey);
            }

            if (componentKeys.Count == 0)
                return string.Empty;

            componentKeys.Sort(StringComparer.Ordinal);
            StringBuilder key = new StringBuilder(32 + componentKeys.Count * 24);
            key.Append("group|");
            key.Append(componentKeys.Count);
            key.Append('|');
            foreach (string componentKey in componentKeys) {
                key.Append(componentKey.Length);
                key.Append(':');
                key.Append(componentKey);
                key.Append('|');
            }
            return key.ToString();
        }

        public static string GetKey(string itemID, int et, int profile,
            int size, int width)
        {
            if (string.IsNullOrEmpty(itemID))
                return string.Empty;

            if (!HasWheelParameters(itemID))
                return itemID;

            return itemID + "|" + et + "|" + profile + "|" + size + "|" + width;
        }

        public static bool IsCompatibleItemID(string purchasedItemID,
            string requestedItemID, bool allowTuningVariant)
        {
            if (string.Equals(purchasedItemID, requestedItemID,
                StringComparison.Ordinal))
                return true;

            return allowTuningVariant &&
                string.Equals(purchasedItemID, "t_" + requestedItemID,
                    StringComparison.Ordinal);
        }

        public static bool MatchesPurchase(string purchasedItemID,
            int purchasedET, int purchasedProfile, int purchasedSize,
            int purchasedWidth, string requestedItemID, int requestedET,
            int requestedProfile, int requestedSize, int requestedWidth,
            bool allowTuningVariant)
        {
            return IsCompatibleItemID(purchasedItemID, requestedItemID,
                    allowTuningVariant) &&
                (requestedET == 0 || requestedET == purchasedET) &&
                (requestedProfile == 0 || requestedProfile == purchasedProfile) &&
                (requestedSize == 0 || requestedSize == purchasedSize) &&
                (requestedWidth == 0 || requestedWidth == purchasedWidth);
        }
    }
}
