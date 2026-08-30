using System;

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
        public QualityQuickFilterMode QualityMode = QualityQuickFilterMode.Off;
        public OwnedQuickFilterMode OwnedMode = OwnedQuickFilterMode.Off;
        public bool UseJunkyardConditionModes;

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

        public bool UsesQualityFilter {
            get { return QualityMode != QualityQuickFilterMode.Off; }
        }

        public bool UsesOwnedFilter {
            get { return OwnedMode != OwnedQuickFilterMode.Off; }
        }

        public bool UsesSearch {
            get { return !string.IsNullOrWhiteSpace(SearchText); }
        }

        public bool HasAnyFilter {
            get {
                return UsesSearch || UsesConditionFilter || UsesRepairabilityFilter ||
                    UsesQualityFilter || UsesOwnedFilter;
            }
        }
    }

    public static class PartFilterRules
    {
        private const float YellowConditionStart = 0.50f;
        private const float GreenConditionStart = 0.80f;
        private const float PerfectConditionStart = 1.00f;

        private static float RepairConditionThreshold {
            get { return GlobalData.JunkCondition; }
        }

        public static bool Matches(BaseItem baseItem, PartFilterCriteria criteria)
        {
            if (baseItem == null || criteria == null)
                return false;

            if (!MatchesSearch(baseItem, criteria.SearchText))
                return false;

            Item item = baseItem.TryCast<Item>();
            if (item != null)
                return MatchesItem(item, criteria);

            GroupItem group = baseItem.TryCast<GroupItem>();
            if (group == null)
                return false;

            return MatchesGroup(group, criteria);
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
                if (criteria.UsesRepairabilityFilter || criteria.UsesQualityFilter)
                    return false;

                return !criteria.UsesConditionFilter ||
                    MatchesCondition(group.GetCondition(), criteria);
            }

            if (!criteria.UsesConditionFilter &&
                !criteria.UsesRepairabilityFilter &&
                !criteria.UsesQualityFilter)
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

            if (IsSpecialInventoryItem(item)) {
                // Barn and junkyard map/case entries remain visible. In garage-style
                // lists they disappear whenever any filter is active.
                return criteria.Context == PartFilterContext.Junkyard ||
                    !criteria.HasAnyFilter;
            }

            if (criteria.UsesConditionFilter &&
                !MatchesCondition(item.ConditionToShow, criteria))
                return false;

            if (criteria.UsesRepairabilityFilter &&
                !MatchesRepairability(PartRepairabilityRules.IsRepairable(item),
                    criteria.RepairabilityMode))
                return false;

            if (criteria.UsesQualityFilter &&
                !MatchesQuality(item.Quality, criteria.QualityMode))
                return false;

            return !includeOwnedFilter || !criteria.UsesOwnedFilter ||
                MatchesOwned(OwnedPartCache.Has(item), criteria.OwnedMode);
        }

        private static bool MatchesCondition(float condition,
            PartFilterCriteria criteria)
        {
            if (criteria.Context == PartFilterContext.Junkyard ||
                criteria.UseJunkyardConditionModes) {
                switch (criteria.JunkyardConditionMode) {
                    case JunkyardConditionFilterMode.RepairThresholdToPerfect:
                        return condition >= RepairConditionThreshold;
                    case JunkyardConditionFilterMode.Orange:
                        return condition >= RepairConditionThreshold &&
                            condition < YellowConditionStart;
                    case JunkyardConditionFilterMode.Yellow:
                        return condition >= YellowConditionStart &&
                            condition < GreenConditionStart;
                    case JunkyardConditionFilterMode.Green:
                        return condition >= GreenConditionStart &&
                            condition < PerfectConditionStart;
                    case JunkyardConditionFilterMode.Red:
                        return condition < RepairConditionThreshold;
                    default:
                        return true;
                }
            }

            switch (criteria.GarageConditionMode) {
                case GarageConditionFilterMode.RepairThresholdToPerfect:
                    return condition >= RepairConditionThreshold;
                case GarageConditionFilterMode.Red:
                    return condition < RepairConditionThreshold;
                case GarageConditionFilterMode.Orange:
                    return condition >= RepairConditionThreshold &&
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
