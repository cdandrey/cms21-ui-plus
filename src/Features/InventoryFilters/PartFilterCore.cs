using System;
using System.Collections.Generic;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
#else
using CMS;
using CMS.Containers;
#endif

namespace Cms21UiPlus
{
    public enum PartFilterContext
    {
        Garage = 0,
        Junkyard = 1,
    }

    public enum TuningQuickFilterMode
    {
        Off = 0,
        TunedOnly = 1,
        StandardOnly = 2,
    }

    public enum ShoppingListQuickFilterMode
    {
        Off = 0,
        ListedOnly = 1,
        UnlistedOnly = 2,
    }

    public sealed class PartFilterCriteria
    {
        public PartFilterContext Context = PartFilterContext.Garage;
        public string SearchText = string.Empty;
        public GarageConditionFilterMode GarageConditionMode =
            GarageConditionFilterMode.Off;
        public JunkyardConditionFilterMode JunkyardConditionMode =
            JunkyardConditionFilterMode.Off;
        public RepairabilityQuickFilterMode RepairabilityMode =
            RepairabilityQuickFilterMode.Off;
        public int RequiredRepairSpecializationMask;
        public int ExcludedRepairSpecializationMask;
        public QualityQuickFilterMode QualityMode = QualityQuickFilterMode.Off;
        public OwnedQuickFilterMode OwnedMode = OwnedQuickFilterMode.Off;
        public TuningQuickFilterMode TuningMode = TuningQuickFilterMode.Off;
        internal ShoppingListQuickFilterMode ShoppingListMode;
        internal HashSet<string> ShoppingListKeys;
        public bool UseJunkyardConditionModes;
        public bool PreserveSpecialInventoryItems;

        public bool UsesConditionFilter {
            get {
                bool useJunkyardModes =
                    Context == PartFilterContext.Junkyard ||
                    UseJunkyardConditionModes;
                return useJunkyardModes
                    ? JunkyardConditionMode != JunkyardConditionFilterMode.Off
                    : GarageConditionMode != GarageConditionFilterMode.Off;
            }
        }

        public bool UsesRepairabilityFilter {
            get { return RepairabilityMode != RepairabilityQuickFilterMode.Off; }
        }

        public bool UsesRepairSpecializationFilter {
            get {
                return RequiredRepairSpecializationMask != 0 ||
                    ExcludedRepairSpecializationMask != 0;
            }
        }

        public bool UsesQualityFilter {
            get { return QualityMode != QualityQuickFilterMode.Off; }
        }

        public bool UsesTuningFilter {
            get { return TuningMode != TuningQuickFilterMode.Off; }
        }

        public bool UsesOwnedFilter {
            get { return OwnedMode != OwnedQuickFilterMode.Off; }
        }

        internal bool UsesShoppingListFilter {
            get { return ShoppingListMode != ShoppingListQuickFilterMode.Off; }
        }

        internal void SetShoppingListFilter(ShoppingListQuickFilterMode mode)
        {
            ShoppingListMode = mode;
            ShoppingListKeys = null;
            if (!UsesShoppingListFilter)
                return;

            ShoppingListKeys = new HashSet<string>(StringComparer.Ordinal);
            if (GlobalState.GameManager == null ||
                GlobalState.GameManager.GameDataManager == null ||
                GlobalState.GameManager.GameDataManager.CurrentProfileData == null)
                return;

            var entries = GlobalState.GameManager.GameDataManager
                .CurrentProfileData.ShopListItemsData;
            if (entries == null)
                return;

            foreach (var entry in entries) {
                if (entry == null || entry.Amount <= 0 ||
                    string.IsNullOrEmpty(entry.ID))
                    continue;
                var data = entry.AdditionalData;
                string key = data != null && (data.Tire || data.Rim)
                    ? PartIdentityComparer.GetKey(entry.ID,
                        data.Rim ? data.ET : 0, data.Tire ? data.Profile : 0,
                        data.Size, data.Tire ? data.Width : 0)
                    : entry.ID;
                ShoppingListKeys.Add(key);
            }
        }

        public bool UsesSearch {
            get { return !string.IsNullOrWhiteSpace(SearchText); }
        }

        public bool HasAnyFilter {
            get {
                return UsesSearch || UsesConditionFilter || UsesRepairabilityFilter ||
                    UsesRepairSpecializationFilter || UsesQualityFilter ||
                    UsesOwnedFilter || UsesTuningFilter || UsesShoppingListFilter;
            }
        }
    }

    public static class PartFilterRules
    {
        private const float RepairConditionStart = 0.15f;
        private const float YellowConditionStart = 0.50f;
        private const float GreenConditionStart = 0.80f;
        private const float PerfectConditionStart = 1.00f;

        public static bool Matches(BaseItem baseItem, PartFilterCriteria criteria)
        {
            if (baseItem == null || criteria == null)
                return false;

            Item item = baseItem.TryCast<Item>();
            if (IsSpecialInventoryItem(item))
                return criteria.PreserveSpecialInventoryItems;

            if (!MatchesSearch(baseItem, criteria.SearchText))
                return false;

            if (item != null) {
                if (criteria.UsesShoppingListFilter &&
                    !MatchesShoppingList(IsListed(item, criteria), criteria))
                    return false;
                return MatchesItem(item, criteria);
            }

            GroupItem group = baseItem.TryCast<GroupItem>();
            if (group == null)
                return false;

            if (criteria.UsesShoppingListFilter) {
                bool listed = false;
                if (group.ItemList != null) {
                    foreach (Item component in group.ItemList) {
                        if (IsListed(component, criteria)) {
                            listed = true;
                            break;
                        }
                    }
                }
                if (!MatchesShoppingList(listed, criteria))
                    return false;
            }

            return MatchesGroup(group, criteria);
        }

        private static bool IsListed(Item item, PartFilterCriteria criteria)
        {
            if (item == null || criteria.ShoppingListKeys == null)
                return false;
            return criteria.ShoppingListKeys.Contains(item.ID) ||
                criteria.ShoppingListKeys.Contains(PartIdentityComparer.GetKey(item));
        }

        private static bool MatchesShoppingList(bool listed, PartFilterCriteria criteria)
        {
            return listed ==
                (criteria.ShoppingListMode == ShoppingListQuickFilterMode.ListedOnly);
        }

        public static bool MatchesSearch(BaseItem baseItem, string searchText)
        {
            if (baseItem == null)
                return false;
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            string localizedName = baseItem.GetLocalizedName() ?? string.Empty;
            return localizedName.IndexOf(searchText.Trim(),
                StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static bool MatchesGroup(GroupItem group, PartFilterCriteria criteria)
        {
            if (criteria.UsesOwnedFilter &&
                !MatchesOwned(OwnedPartCache.Has(group), criteria.OwnedMode))
                return false;

            if (criteria.Context == PartFilterContext.Garage) {
                // Complete wheels, assembled shock absorbers and other GroupItems have
                // aggregate condition but no repairability or quality value of their own.
                if (criteria.UsesRepairabilityFilter ||
                    criteria.UsesRepairSpecializationFilter ||
                    criteria.UsesQualityFilter || criteria.UsesTuningFilter) {
                    if (!criteria.UsesTuningFilter || group.ItemList == null)
                        return false;
                    foreach (Item groupItem in group.ItemList) {
                        if (MatchesItem(groupItem, criteria, false))
                            return true;
                    }
                    return false;
                }

                return !criteria.UsesConditionFilter ||
                    MatchesCondition(group.GetCondition(), criteria);
            }

            if (!criteria.UsesConditionFilter &&
                !criteria.UsesRepairabilityFilter &&
                !criteria.UsesQualityFilter && !criteria.UsesTuningFilter)
                return true;
            if (group.ItemList == null)
                return false;

            foreach (Item groupItem in group.ItemList) {
                if (MatchesItem(groupItem, criteria, false))
                    return true;
            }

            return false;
        }

        private static bool MatchesItem(Item item, PartFilterCriteria criteria,
            bool includeOwnedFilter = true)
        {
            if (item == null)
                return false;

            if (criteria.UsesConditionFilter &&
                !MatchesCondition(item.ConditionToShow, criteria))
                return false;

            if (criteria.UsesRepairabilityFilter &&
                !MatchesRepairability(PartRepairabilityRules.IsRepairable(item),
                    criteria.RepairabilityMode))
                return false;

            if (criteria.UsesRepairSpecializationFilter) {
                int specializationMask =
                    GameplayRepairSkillBridge.GetRepairSpecializationMask(item.ID);
                if ((specializationMask & criteria.RequiredRepairSpecializationMask) !=
                        criteria.RequiredRepairSpecializationMask ||
                    (specializationMask & criteria.ExcludedRepairSpecializationMask) != 0)
                    return false;
            }

            if (criteria.UsesQualityFilter &&
                !MatchesQuality(item.Quality, criteria.QualityMode))
                return false;

            if (criteria.UsesTuningFilter &&
                (IsTuned(item) !=
                    (criteria.TuningMode == TuningQuickFilterMode.TunedOnly)))
                return false;

            return !includeOwnedFilter || !criteria.UsesOwnedFilter ||
                MatchesOwned(OwnedPartCache.Has(item), criteria.OwnedMode);
        }

        internal static bool IsTuned(Item item)
        {
            if (item == null || string.IsNullOrEmpty(item.ID))
                return false;
            if (item.ID.StartsWith("t_", StringComparison.OrdinalIgnoreCase))
                return true;

            switch (item.ID) {
                case "tarczaWentylowana_2":
                case "tarczaWentylowana_2B":
                case "tarczaWentylowana_3":
                case "tarczaWentylowana_3B":
                case "tarczaWentylowana_4":
                case "tarczaWentylowana_4B":
                case "tarczaWentylowana_5":
                    return true;
            }
            GameInventory catalog = Singleton<GameInventory>.Instance;
            if (catalog == null)
                return false;
            var tuning = catalog.GetTuningProperty(item.ID);
            var property = catalog.GetItemProperty(item.ID);
            return (tuning != null && tuning.Power > 0f) ||
                (property != null && property.TuningValue > 0f);
        }

        private static bool MatchesCondition(float condition,
            PartFilterCriteria criteria)
        {
            if (criteria.Context == PartFilterContext.Junkyard ||
                criteria.UseJunkyardConditionModes) {
                switch (criteria.JunkyardConditionMode) {
                    case JunkyardConditionFilterMode.RepairThresholdToPerfect:
                        return condition >= RepairConditionStart;
                    case JunkyardConditionFilterMode.Orange:
                        return condition >= RepairConditionStart &&
                            condition < YellowConditionStart;
                    case JunkyardConditionFilterMode.Yellow:
                        return condition >= YellowConditionStart &&
                            condition < GreenConditionStart;
                    case JunkyardConditionFilterMode.Green:
                        return condition >= GreenConditionStart &&
                            condition < PerfectConditionStart;
                    case JunkyardConditionFilterMode.Perfect:
                        return condition >= PerfectConditionStart;
                    case JunkyardConditionFilterMode.Red:
                        return condition < RepairConditionStart;
                    default:
                        return true;
                }
            }

            switch (criteria.GarageConditionMode) {
                case GarageConditionFilterMode.RepairThresholdToPerfect:
                    return condition >= RepairConditionStart;
                case GarageConditionFilterMode.Red:
                    return condition < RepairConditionStart;
                case GarageConditionFilterMode.Orange:
                    return condition >= RepairConditionStart &&
                        condition < YellowConditionStart;
                case GarageConditionFilterMode.Yellow:
                    return condition >= YellowConditionStart &&
                        condition < GreenConditionStart;
                case GarageConditionFilterMode.GreenRing:
                    return condition >= GreenConditionStart &&
                        condition < PerfectConditionStart;
                case GarageConditionFilterMode.Perfect:
                    return condition >= PerfectConditionStart;
                default:
                    return true;
            }
        }

        private static bool MatchesRepairability(bool repairable,
            RepairabilityQuickFilterMode mode)
        {
            switch (mode) {
                case RepairabilityQuickFilterMode.RepairGroupOnly:
                    return repairable;
                case RepairabilityQuickFilterMode.NonRepairableOnly:
                    return !repairable;
                default:
                    return true;
            }
        }

        private static bool MatchesQuality(int quality,
            QualityQuickFilterMode mode)
        {
            switch (mode) {
                case QualityQuickFilterMode.Improved:
                    return quality >= 1 && quality <= 3;
                case QualityQuickFilterMode.Quality1:
                    return quality == 1;
                case QualityQuickFilterMode.Quality2:
                    return quality == 2;
                case QualityQuickFilterMode.Quality3:
                    return quality == 3;
                case QualityQuickFilterMode.NonImproved:
                    return quality == 0;
                default:
                    return true;
            }
        }

        private static bool MatchesOwned(bool owned, OwnedQuickFilterMode mode)
        {
            switch (mode) {
                case OwnedQuickFilterMode.Owned:
                    return owned;
                case OwnedQuickFilterMode.Missing:
                    return !owned;
                default:
                    return true;
            }
        }

        internal static bool IsSpecialInventoryItem(Item item)
        {
            return item != null &&
                (string.Equals(item.ID, "specialMap", StringComparison.Ordinal) ||
                 string.Equals(item.ID, "specialCase", StringComparison.Ordinal));
        }
    }
}
