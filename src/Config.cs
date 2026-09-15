
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
        [Tomlet.Attributes.TomlInlineComment("Enable the improved shopping list with shop filters, compact card grid, quantity controls, indicators, sorting and bulk purchase")]
        public bool addShoppingListSorting = true;
        [Tomlet.Attributes.TomlInlineComment("Filter the shopping list to the current specialized shop when it opens")]
        public bool filterShoppingListByCurrentShopOnOpen = true;
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
        [Tomlet.Attributes.TomlInlineComment("Physically rearrange cars across all unlocked parking alleys")]
        public bool addParkingSorting = true;
        [Tomlet.Attributes.TomlInlineComment("Automatically accept the redundant dyno-start confirmation")]
        public bool autoConfirmDynoStart = true;
        [Tomlet.Attributes.TomlInlineComment("Show repairability indicators on all supported part cards")]
        public bool showPartRepairabilityIndicators = true;
        [Tomlet.Attributes.TomlInlineComment("Show owned-part count indicators on all supported part cards")]
        public bool showOwnedPartCountIndicators = true;
        [Tomlet.Attributes.TomlInlineComment("Hide the vanilla paint-colour badge on part cards")]
        public bool hideBodyPartPaintColorBadges = true;
        [Tomlet.Attributes.TomlInlineComment("Group identical parts into expandable inventory and warehouse packages")]
        public bool groupInventoryParts = true;
        [Tomlet.Attributes.TomlInlineComment("Enable quick filters in all supported windows")]
        public bool addQuickFilters = true;
        [Tomlet.Attributes.TomlInlineComment("Enable the improved return-to-garage parts window in barn and junkyard")]
        public bool addTakenItemsPresentation = true;
        [Tomlet.Attributes.TomlInlineComment("Reset garage inventory and warehouse quick filters when the window closes")]
        public bool resetInventoryQuickFiltersOnExit = true;
        [Tomlet.Attributes.TomlInlineComment("Reset spring-clamp quick filters when the selection window closes")]
        public bool resetSpringClampInventoryFiltersOnExit = false;
        [Tomlet.Attributes.TomlInlineComment("Reset tire-changer quick filters when the selection window closes")]
        public bool resetTireChangerInventoryFiltersOnExit = false;
        [Tomlet.Attributes.TomlInlineComment("Reset wheel-balancer quick filters when the selection window closes")]
        public bool resetWheelBalancerInventoryFiltersOnExit = false;
        [Tomlet.Attributes.TomlInlineComment("Reset brake-lathe quick filters when the selection window closes")]
        public bool resetBrakeLatheInventoryFiltersOnExit = false;
        [Tomlet.Attributes.TomlInlineComment("Reset mount part-selection quick filters when the selection window closes")]
        public bool resetMountPartSelectionFiltersOnExit = false;
        [Tomlet.Attributes.TomlInlineComment("Move all filtered parts between inventory and warehouse with the configured shortcut")]
        public bool moveFilteredPartsBetweenInventoryAndWarehouse = true;
        [Tomlet.Attributes.TomlInlineComment("Reset scrap inventory quick filters when the scrap window closes")]
        public bool resetScrapInventoryFiltersOnExit = true;
        [Tomlet.Attributes.TomlInlineComment("Hold Space to bulk scrap all or filtered scrap inventory parts")]
        public bool addBulkScrapShortcut = true;
        [Tomlet.Attributes.TomlInlineComment("Reset repair inventory quick filters when the repair window closes")]
        public bool resetRepairInventoryFiltersOnExit = true;
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
            public string[] parkingArrivalOrder;
        }
    }
}
