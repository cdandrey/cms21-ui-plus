#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.Containers;
#else
using CMS;
using CMS.Containers;
#endif

namespace Cms21UiPlus
{
    public static class PartRepairabilityRules
    {
        private static bool brakeDrumRepairable;

        public static void SetBrakeDrumRepairable(bool repairable)
        {
            brakeDrumRepairable = repairable;
        }

        internal static int GetRepairLevel(string itemID)
        {
            if (string.IsNullOrEmpty(itemID))
                return 0;

            GameInventory inventory = Singleton<GameInventory>.Instance;
            if (inventory == null || !inventory.ExistsInPartProperty(itemID))
                return 0;
            PartProperty property = inventory.GetItemProperty(itemID);
            return property != null && property.RepairGroup > 0
                ? property.RepairGroup : 0;
        }

        public static bool IsRepairable(Item item)
        {
            return item != null && IsRepairable(item.ID);
        }

        public static bool IsRepairable(string itemID)
        {
            if (string.IsNullOrEmpty(itemID))
                return false;

            GameInventory inventory = Singleton<GameInventory>.Instance;
            if (inventory == null || !inventory.ExistsInPartProperty(itemID))
                return false;

            PartProperty property = inventory.GetItemProperty(itemID);
            if (property != null && property.RepairGroup != 0)
                return true;

            switch (itemID) {
                case "tarczaHamulcowa_1":
                case "tarczaWentylowana_1":
                case "tarczaWentylowana_1B":
                case "tarczaWentylowana_2":
                case "tarczaWentylowana_2B":
                case "tarczaWentylowana_3":
                    return true;
                case "pokrywaBeben_1":
                    return brakeDrumRepairable;
                default:
                    return false;
            }
        }
    }
}
