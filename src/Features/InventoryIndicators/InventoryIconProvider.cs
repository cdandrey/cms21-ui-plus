#if NET6_0_OR_GREATER
using Il2Cpp;
#else
using CMS;
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cms21UiPlus
{
    /// <summary>Loads and caches prepared PNG icons used by inventory indicators and filters.</summary>
    public static class InventoryIconProvider
    {
        private static Sprite redRepairWrenchIcon;
        private static Sprite orangeRepairWrenchIcon;
        private static Sprite yellowRepairWrenchIcon;
        private static Sprite greenRepairWrenchIcon;
        private static Sprite whiteRepairWrenchIcon;
        private static Sprite redWarehouseIcon;
        private static Sprite whiteWarehouseIcon;
        private static Sprite whiteConditionIcon;
        private static Sprite orangeConditionIcon;
        private static Sprite yellowConditionIcon;
        private static Sprite greenConditionIcon;
        private static Sprite greenRingConditionIcon;
        private static Sprite redConditionIcon;
        private static Sprite qualityIcon;
        private static Sprite quality1Icon;
        private static Sprite quality2Icon;
        private static Sprite quality3Icon;
        private static Sprite qualityNonIcon;

        private static readonly HashSet<string> LoggedIcons =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> ExternalIcons =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static Sprite GetRedRepairWrenchIcon()
        {
            return Load(ref redRepairWrenchIcon, "II_RepairabilityRed.png", "repair-red");
        }

        public static Sprite GetOrangeRepairWrenchIcon()
        {
            return Load(ref orangeRepairWrenchIcon, "II_RepairabilityOrange.png", "repair-orange");
        }

        public static Sprite GetYellowRepairWrenchIcon()
        {
            return Load(ref yellowRepairWrenchIcon, "II_RepairabilityYellow.png", "repair-yellow");
        }

        public static Sprite GetGreenRepairWrenchIcon()
        {
            return Load(ref greenRepairWrenchIcon, "II_RepairabilityGreen.png", "repair-green");
        }

        public static Sprite GetWhiteRepairWrenchIcon()
        {
            return Load(ref whiteRepairWrenchIcon, "II_RepairabilityWhite.png", "repair-white");
        }

        public static Sprite GetRepairWrenchIconForCondition(float condition)
        {
            if (condition < GlobalData.JunkCondition)
                return GetRedRepairWrenchIcon();
            if (condition < 0.50f)
                return GetOrangeRepairWrenchIcon();
            if (condition < 0.80f)
                return GetYellowRepairWrenchIcon();
            return GetGreenRepairWrenchIcon();
        }

        public static Sprite GetRedWarehouseIcon()
        {
            return Load(ref redWarehouseIcon, "II_OwnershipRed.png", "warehouse-red");
        }

        public static Sprite GetWhiteWarehouseIcon()
        {
            return Load(ref whiteWarehouseIcon, "II_OwnershipWhite.png", "warehouse-white");
        }


        public static Sprite GetWhiteConditionIcon()
        {
            return Load(ref whiteConditionIcon, "II_ConditionWhite.png", "condition-white");
        }

        public static Sprite GetOrangeConditionIcon()
        {
            return Load(ref orangeConditionIcon, "II_ConditionOrange.png", "condition-orange");
        }

        public static Sprite GetYellowConditionIcon()
        {
            return Load(ref yellowConditionIcon, "II_ConditionYellow.png", "condition-yellow");
        }

        public static Sprite GetGreenConditionIcon()
        {
            return Load(ref greenConditionIcon, "II_ConditionGreen.png", "condition-green");
        }

        public static Sprite GetGreenRingConditionIcon()
        {
            return Load(ref greenRingConditionIcon, "II_ConditionGreenRing.png",
                "condition-green-ring");
        }

        public static Sprite GetRedConditionIcon()
        {
            return Load(ref redConditionIcon, "II_ConditionRed.png", "condition-red");
        }

        public static Sprite GetQualityIcon()
        {
            return Load(ref qualityIcon, "II_Quality.png", "quality");
        }

        public static Sprite GetQuality1Icon()
        {
            return Load(ref quality1Icon, "II_Quality1.png", "quality-1");
        }

        public static Sprite GetQuality2Icon()
        {
            return Load(ref quality2Icon, "II_Quality2.png", "quality-2");
        }

        public static Sprite GetQuality3Icon()
        {
            return Load(ref quality3Icon, "II_Quality3.png", "quality-3");
        }

        public static Sprite GetQualityNonIcon()
        {
            return Load(ref qualityNonIcon, "II_QualityNon.png", "quality-none");
        }

        public static Sprite GetExternalIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            Sprite cached;
            if (ExternalIcons.TryGetValue(filePath, out cached)) {
                if (cached != null && cached.texture != null)
                    return cached;
                ExternalIcons.Remove(filePath);
            }

            string logKey = "external|" + filePath;
            if (!System.IO.File.Exists(filePath)) {
                if (LoggedIcons.Add(logKey))
                    ModLogger.Log("[InventoryIcons] Prepared icon is missing at " +
                        filePath + ".", Types.LoggingLevels.Warning);
                return null;
            }

            try {
                cached = TextureLoader.LoadSpriteFromFile(filePath, false);
            } catch (Exception exception) {
                if (LoggedIcons.Add(logKey))
                    ModLogger.Log("[InventoryIcons] Failed to load icon from " +
                        filePath + "." + Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                return null;
            }

            if (cached == null) {
                if (LoggedIcons.Add(logKey))
                    ModLogger.Log("[InventoryIcons] Could not decode icon at " +
                        filePath + ".", Types.LoggingLevels.Warning);
                return null;
            }

            ExternalIcons[filePath] = cached;
            return cached;
        }

        private static Sprite Load(ref Sprite cache, string fileName, string logicalName)
        {
            if (cache != null && cache.texture != null)
                return cache;
            cache = null;

            string filePath = GlobalConfig.directoryInventoryIndicators + fileName;
            string logKey = logicalName + "|" + filePath;

            if (!System.IO.File.Exists(filePath)) {
                if (LoggedIcons.Add(logKey))
                    ModLogger.Log("[InventoryIcons] Prepared icon is missing: " + logicalName +
                        " at " + filePath + ".", Types.LoggingLevels.Warning);
                return null;
            }

            try {
                cache = TextureLoader.LoadSpriteFromFile(filePath, false);
            } catch (Exception exception) {
                if (LoggedIcons.Add(logKey))
                    ModLogger.Log("[InventoryIcons] Failed to load " + logicalName +
                        " from " + filePath + "." + Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                return null;
            }

            if (cache == null && LoggedIcons.Add(logKey)) {
                ModLogger.Log("[InventoryIcons] Could not decode " + logicalName +
                    " at " + filePath + ".", Types.LoggingLevels.Warning);
            }
            return cache;
        }
    }
}
