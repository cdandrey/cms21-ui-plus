using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
#else
using CMS;
using CMS.Containers;
#endif

namespace Cms21UiPlus
{
    public sealed class Settings
    {
        [Tomlet.Attributes.TomlInlineComment("Jobs only: unmark a required part after it reaches the job condition")]
        public bool unmarkFinishedParts = true;
        [Tomlet.Attributes.TomlInlineComment("Jobs only: allow marking required body parts")]
        public bool markBodyParts = true;
        [Tomlet.Attributes.TomlInlineComment("Remove or reduce a matching shopping-list entry after purchase")]
        public bool removePartsFromShoppingList = true;
        [Tomlet.Attributes.TomlInlineComment("Prefill purchase amount and wheel dimensions from the shopping list")]
        public bool wheelShopListPurchaseHelper = true;
        [Tomlet.Attributes.TomlInlineComment("Quickly switch between matching mount and unmount modes")]
        public bool quickSwitchMountModes = true;
        [Tomlet.Attributes.TomlInlineComment("Add an exit-game action to the garage pause menu")]
        public bool addExitGameToGaragePauseMenu = true;
        [Tomlet.Attributes.TomlInlineComment("Skip startup intro scenes")]
        public bool skipStartupVideosTotally = true;
        [Tomlet.Attributes.TomlInlineComment("Automatically continue past the startup confirmation prompt")]
        public bool autoContinueStartupPrompt = true;
        [Tomlet.Attributes.TomlInlineComment("Remember inventory and warehouse sorting per profile")]
        public bool rememberInventorySorting = true;
        [Tomlet.Attributes.TomlInlineComment("Show normalized livery file names instead of bare numbers")]
        public bool showLiveryFileNames = true;
        [Tomlet.Attributes.TomlInlineComment("Show vehicle condition and licence plate on map and parking screens")]
        public bool showCarConditionOnMap = true;
        [Tomlet.Attributes.TomlInlineComment("Automatically accept the redundant dyno-start confirmation")]
        public bool autoConfirmDynoStart = true;
        [Tomlet.Attributes.TomlInlineComment("Show repairability indicators in all supported inventories")]
        public bool showPartRepairabilityIndicators = true;
        [Tomlet.Attributes.TomlInlineComment("Show owned-part count indicators in all supported inventories")]
        public bool showOwnedPartCountIndicators = true;
        [Tomlet.Attributes.TomlInlineComment("Hide the vanilla paint-colour badge on part cards")]
        public bool hideBodyPartPaintColorBadges = true;
        [Tomlet.Attributes.TomlInlineComment("Enable inventory, warehouse, barn and junkyard quick filters")]
        public bool addInventoryQuickFilters = true;
        [Tomlet.Attributes.TomlInlineComment("Enable spring-clamp quick filters")]
        public bool addSpringClampInventoryFilters = true;
        [Tomlet.Attributes.TomlInlineComment("Move all filtered parts between inventory and warehouse with the configured shortcut")]
        public bool moveFilteredPartsBetweenInventoryAndWarehouse = true;
        [Tomlet.Attributes.TomlInlineComment("Add search, condition and repairability filters to the scrap inventory")]
        public bool addScrapInventoryFilters = true;
        [Tomlet.Attributes.TomlInlineComment("Hold Space to bulk scrap all or filtered scrap inventory parts")]
        public bool addBulkScrapShortcut = true;
        [Tomlet.Attributes.TomlInlineComment("Add search, condition and repairability filters to part and body repair inventories")]
        public bool addRepairInventoryFilters = true;
    }

    internal static class SettingsMigration
    {
        private static readonly Regex LegacyPartIndicatorsRegex = new Regex(
            @"^([ \t]*)showInventoryPartIndicators[ \t]*=[ \t]*(true|false)[^\r\n]*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex RepairabilityIndicatorsRegex = new Regex(
            @"^[ \t]*showPartRepairabilityIndicators[ \t]*=",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex OwnedCountIndicatorsRegex = new Regex(
            @"^[ \t]*showOwnedPartCountIndicators[ \t]*=",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex LegacyDynoRegex = new Regex(
            @"^([ \t]*)streamlinedDyno[ \t]*=[ \t]*(true|false)[^\r\n]*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex DynoConfirmationRegex = new Regex(
            @"^[ \t]*autoConfirmDynoStart[ \t]*=",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public static void MigrateLegacySettings()
        {
            string path = GlobalConfig.cfgFile;
            if (!File.Exists(path))
                return;
            MigrateLegacyPartIndicatorSetting(path);
            MigrateLegacyDynoSetting(path);
        }

        private static void MigrateLegacyPartIndicatorSetting(string path)
        {
            string tempPath = path + ".migration.tmp";
            try {
                string source = File.ReadAllText(path);
                Match legacy = LegacyPartIndicatorsRegex.Match(source);
                if (!legacy.Success)
                    return;

                string indent = legacy.Groups[1].Value;
                string value = legacy.Groups[2].Value.ToLowerInvariant();
                string newLine = source.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                    ? "\r\n" : "\n";
                List<string> replacement = new List<string>(2);
                if (!RepairabilityIndicatorsRegex.IsMatch(source))
                    replacement.Add(indent + "showPartRepairabilityIndicators = " + value + " # Show repairability indicators in all supported inventories");
                if (!OwnedCountIndicatorsRegex.IsMatch(source))
                    replacement.Add(indent + "showOwnedPartCountIndicators = " + value + " # Show owned-part count indicators in all supported inventories");

                string migrated = source.Remove(legacy.Index, legacy.Length)
                    .Insert(legacy.Index, string.Join(newLine, replacement.ToArray()));
                File.WriteAllText(tempPath, migrated);
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            } catch (Exception exception) {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                ModLogger.Log("[Settings] Failed to migrate legacy part-indicator setting." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }

        private static void MigrateLegacyDynoSetting(string path)
        {
            string tempPath = path + ".migration.tmp";
            try {
                string source = File.ReadAllText(path);
                Match legacy = LegacyDynoRegex.Match(source);
                if (!legacy.Success || DynoConfirmationRegex.IsMatch(source))
                    return;

                string newLine = source.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                    ? "\r\n" : "\n";
                string addition = legacy.Groups[1].Value + "autoConfirmDynoStart = " +
                    legacy.Groups[2].Value.ToLowerInvariant() +
                    " # Automatically accept the redundant dyno-start confirmation";
                string migrated = source.Insert(legacy.Index + legacy.Length,
                    newLine + addition);
                File.WriteAllText(tempPath, migrated);
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            } catch (Exception exception) {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                ModLogger.Log("[Settings] Failed to migrate the legacy dyno setting." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }
    }

    public static class GlobalConfig
    {
        public static readonly string cfgFile = @"Mods\CMS21UIPlus\CMS21UIPlus.cfg";
        public static readonly string cfgKeyBindings = @"Mods\CMS21UIPlus\KeyBindings.cfg";
        public static readonly string cfgProfile = @"Mods\CMS21UIPlus\ProfileMemory.dat";
        public static readonly string directoryInventoryIndicators = @"Mods\CMS21UIPlus\InventoryIndicators\";
    }

    public static class GlobalState
    {
        public static bool IsGarageSceneActive;
        public static bool IsMenuSceneActive;
        public static int LoadedProfileId;
        public static GameManager GameManager;
    }

    public static class Types
    {
        public enum LoggingLevels { Normal, NormalClean, Debug, PlayerLog, Warning, Error }

        public sealed class ProfileMemoryData
        {
            [Tomlet.Attributes.TomlInlineComment("There should be no reason to edit this file manually")]
            public string lastCMS21UIPlusVersion = string.Empty;
            public ProfileState[] profileStates;
        }

        public sealed class ProfileState
        {
            public SortType inventorySortType = SortType.ByConditionAsc;
            public SortType warehouseInventorySortType = SortType.ByConditionAsc;
            public SortType warehouseSortType = SortType.ByConditionAsc;
        }
    }
}
