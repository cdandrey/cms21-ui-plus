using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS;
using Il2CppCMS.Containers;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Logic;
using Il2CppCMS.UI.Windows;
using Il2CppCMS.UI.Logic.ChoosePartDown;
using Il2CppCMS.UI.Logic.Scrap;
#else
using CMS;
using CMS.Containers;
using CMS.UI;
using CMS.UI.Logic;
using CMS.UI.Windows;
using CMS.UI.Logic.ChoosePartDown;
using CMS.UI.Logic.Scrap;
#endif

namespace Cms21UiPlus
{
    [HarmonyPatch]
    internal static class PartSelectionFilterPatches
    {
        private struct DownWindowShowState
        {
            internal MountPartSelectionFilterFeature.DownWindowShowState Mount;
            internal SpringClampInventoryFilterFeature.DownWindowShowState Spring;
            internal TireChangerInventoryFilterFeature.DownWindowShowState TireChanger;
        }
        [HarmonyPatch(typeof(ChoosePartUpWindow), nameof(ChoosePartUpWindow.Show),
            new Type[] { typeof(string), typeof(ChoosePartUpWindowType) })]
        [HarmonyPrefix]
        private static void ChoosePartUpWindowShowByModePrefix(
            ChoosePartUpWindow __instance, ChoosePartUpWindowType __1)
        {
            MountPartSelectionFilterFeature.OnUpWindowShowPrefix(
                __instance, __1, "mode");
            SpringClampInventoryFilterFeature.OnUpWindowShowPrefix(
                __instance, __1);
            TireChangerInventoryFilterFeature.OnUpWindowShowPrefix(
                __instance, __1);
        }

        [HarmonyPatch(typeof(ChoosePartUpWindow), nameof(ChoosePartUpWindow.Show),
            new Type[] {
                typeof(Il2CppSystem.Collections.Generic.List<BaseItem>),
                typeof(ChoosePartUpWindowType)
            })]
        [HarmonyPrefix]
        private static void ChoosePartUpWindowShowByItemsPrefix(
            ChoosePartUpWindow __instance, ChoosePartUpWindowType __1)
        {
            MountPartSelectionFilterFeature.OnUpWindowShowPrefix(
                __instance, __1, "items");
            SpringClampInventoryFilterFeature.OnUpWindowShowPrefix(
                __instance, __1);
            TireChangerInventoryFilterFeature.OnUpWindowShowPrefix(
                __instance, __1);
        }

        [HarmonyPatch(typeof(ChoosePartUpWindow), nameof(ChoosePartUpWindow.Show),
            new Type[] { typeof(string), typeof(ChoosePartUpWindowType) })]
        [HarmonyPostfix]
        private static void ChoosePartUpWindowShowByModePostfix(
            ChoosePartUpWindow __instance, ChoosePartUpWindowType __1,
            bool __result)
        {
            MountPartSelectionFilterFeature.OnUpWindowShowPostfix(
                __instance, __1, __result, "mode");
            SpringClampInventoryFilterFeature.OnUpWindowShowPostfix(
                __instance, __1, __result);
            TireChangerInventoryFilterFeature.OnUpWindowShowPostfix(
                __instance, __1, __result);
        }

        [HarmonyPatch(typeof(ChoosePartUpWindow), nameof(ChoosePartUpWindow.Show),
            new Type[] {
                typeof(Il2CppSystem.Collections.Generic.List<BaseItem>),
                typeof(ChoosePartUpWindowType)
            })]
        [HarmonyPostfix]
        private static void ChoosePartUpWindowShowByItemsPostfix(
            ChoosePartUpWindow __instance, ChoosePartUpWindowType __1,
            bool __result)
        {
            MountPartSelectionFilterFeature.OnUpWindowShowPostfix(
                __instance, __1, __result, "items");
            SpringClampInventoryFilterFeature.OnUpWindowShowPostfix(
                __instance, __1, __result);
            TireChangerInventoryFilterFeature.OnUpWindowShowPostfix(
                __instance, __1, __result);
        }

        [HarmonyPatch(typeof(ChoosePartUpWindow), nameof(ChoosePartUpWindow.Hide),
            new Type[] { typeof(bool) })]
        [HarmonyPostfix]
        private static void ChoosePartUpWindowHidePostfix(
            ChoosePartUpWindow __instance)
        {
            MountPartSelectionFilterFeature.OnUpWindowHidden(__instance);
            SpringClampInventoryFilterFeature.OnUpWindowHidden(__instance);
            TireChangerInventoryFilterFeature.OnUpWindowHidden(__instance);
        }

        [HarmonyPatch(typeof(ChoosePartUpWindow),
            "OnDownWindowItemChange", new Type[] { typeof(ChoosePartDownItem) })]
        [HarmonyPrefix]
        private static bool ChoosePartUpWindowItemChangePrefix(
            ChoosePartUpWindow __instance, ChoosePartDownItem __0)
        {
            return !MountPartSelectionFilterFeature
                    .ShouldSuppressNativeSelection(__instance, __0) &&
                !SpringClampInventoryFilterFeature
                    .ShouldSuppressNativeSelection(__instance, __0) &&
                !TireChangerInventoryFilterFeature
                    .ShouldSuppressNativeSelection(__instance, __0);
        }

        [HarmonyPatch(typeof(ChoosePartUpWindow), "SubmitAction")]
        [HarmonyPrefix]
        private static bool ChoosePartUpWindowSubmitActionPrefix(
            ChoosePartUpWindow __instance)
        {
            return !MountPartSelectionFilterFeature
                    .ShouldSuppressSubmit(__instance) &&
                !SpringClampInventoryFilterFeature
                    .ShouldSuppressSubmit(__instance) &&
                !TireChangerInventoryFilterFeature
                    .ShouldSuppressSubmit(__instance);
        }

        [HarmonyPatch(typeof(ChoosePartDownWindow),
            nameof(ChoosePartDownWindow.Show), new Type[] {
                typeof(Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>),
                typeof(int)
            })]
        [HarmonyPrefix]
        private static void ChoosePartDownWindowShowPrefix(
            ChoosePartDownWindow __instance,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> __0,
            ref int __1, out DownWindowShowState __state)
        {
            __state = new DownWindowShowState();
            __state.Mount = MountPartSelectionFilterFeature
                .PrepareNativeListForShow(__instance, ref __0, ref __1);
            __state.Spring = SpringClampInventoryFilterFeature
                .PrepareNativeListForShow(__instance, ref __0, ref __1);
            __state.TireChanger = TireChangerInventoryFilterFeature
                .PrepareNativeListForShow(__instance, ref __0, ref __1);
        }

        [HarmonyPatch(typeof(ChoosePartDownWindow),
            nameof(ChoosePartDownWindow.Show), new Type[] {
                typeof(Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>),
                typeof(int)
            })]
        [HarmonyPostfix]
        private static void ChoosePartDownWindowShowPostfix(
            ChoosePartDownWindow __instance,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> __0,
            DownWindowShowState __state)
        {
            ScrapInventoryFilterFeature.OnWindowShown(__instance, __0);
            RepairInventoryFilterFeature.OnWindowShown(__instance, __0);
            MountPartSelectionFilterFeature.OnWindowShown(__instance,
                __state.Mount);
            SpringClampInventoryFilterFeature.OnWindowShown(__instance,
                __state.Spring);
            TireChangerInventoryFilterFeature.OnWindowShown(__instance,
                __state.TireChanger);
        }

        [HarmonyPatch(typeof(RepairPartWindow),
            nameof(RepairPartWindow.Show))]
        [HarmonyPostfix]
        private static void RepairPartWindowShowPostfix(
            RepairPartWindow __instance)
        {
            RepairInventoryFilterFeature.OnRepairWindowShown(__instance);
        }

        [HarmonyPatch(typeof(RepairPartWindow),
            nameof(RepairPartWindow.Hide))]
        [HarmonyPostfix]
        private static void RepairPartWindowHidePostfix()
        {
            RepairInventoryFilterFeature.OnRepairWindowHidden();
        }

        [HarmonyPatch(typeof(ChoosePartPageManager),
            nameof(ChoosePartPageManager.Refresh))]
        [HarmonyPrefix]
        private static void ChoosePartPageManagerRefreshPrefix(
            ChoosePartPageManager __instance,
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> __0)
        {
            ScrapInventoryFilterFeature.FilterNativeListBeforeRefresh(
                __instance, ref __0);
            RepairInventoryFilterFeature.FilterNativeListBeforeRefresh(
                __instance, ref __0);
            MountPartSelectionFilterFeature.FilterNativeListBeforeRefresh(
                __instance, ref __0);
            SpringClampInventoryFilterFeature.FilterNativeListBeforeRefresh(
                __instance, ref __0);
            TireChangerInventoryFilterFeature.FilterNativeListBeforeRefresh(
                __instance, ref __0);
        }

        [HarmonyPatch(typeof(ChoosePartPageManager),
            nameof(ChoosePartPageManager.Refresh))]
        [HarmonyPostfix]
        private static void ChoosePartPageManagerRefreshPostfix(
            ChoosePartPageManager __instance,
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem> __0)
        {
            ScrapInventoryFilterFeature.OnNativeListRefreshed(
                __instance, __0);
            RepairInventoryFilterFeature.OnNativeListRefreshed(__0);
            MountPartSelectionFilterFeature.OnNativeListRefreshed(
                __instance, __0);
            SpringClampInventoryFilterFeature.OnNativeListRefreshed(
                __instance, __0);
            TireChangerInventoryFilterFeature.OnNativeListRefreshed(
                __instance, __0);
        }

        [HarmonyPatch(typeof(InputField), nameof(InputField.KeyPressed))]
        [HarmonyPostfix]
        private static void InputFieldKeyPressedPostfix(Event evt,
            InputField __instance)
        {
            ScrapInventoryFilterFeature.OnInputFieldKeyPressed(__instance);
            RepairInventoryFilterFeature.OnInputFieldKeyPressed(__instance);
            MountPartSelectionFilterFeature.OnInputFieldKeyPressed(__instance);
            SpringClampInventoryFilterFeature.OnInputFieldKeyPressed(__instance);
            TireChangerInventoryFilterFeature.OnInputFieldKeyPressed(__instance);
        }

        [HarmonyPatch(typeof(ScrapUpgrade), "GetItemsForUpgrade")]
        [HarmonyPostfix]
        private static void ScrapUpgradeGetItemsForUpgradePostfix(
            ref Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
                __result)
        {
            ScrapInventoryFilterFeature.FilterUpgradeItemsBeforeNativeSelection(
                ref __result);
        }

        [HarmonyPatch(typeof(ScrapWindow), nameof(ScrapWindow.Hide))]
        [HarmonyPostfix]
        private static void ScrapWindowHidePostfix()
        {
            ScrapInventoryFilterFeature.OnScrapWindowHidden();
        }

        [HarmonyPatch(typeof(ScrapProduction), "GetItemsForScrap")]
        [HarmonyPostfix]
        private static void ScrapProductionGetItemsForScrapPostfix(
            Il2CppSystem.Collections.Generic.List<ChoosePartDownItem>
                __result)
        {
            ScrapInventoryFilterFeature.OnNativeScrapItemsBuilt(__result);
        }

        [HarmonyPatch(typeof(ScrapProduction),
            nameof(ScrapProduction.ProcessGameResult))]
        [HarmonyPostfix]
        private static void ScrapProductionProcessGameResultPostfix(
            ScrapProduction __instance)
        {
            ScrapInventoryFilterFeature.OnScrapProcessed(__instance);
        }

        [HarmonyPatch(typeof(ScrapProduction), "StartMiniGameAction")]
        [HarmonyPrefix]
        private static bool ScrapProductionStartMiniGameActionPrefix()
        {
            return !ScrapInventoryFilterFeature.ShouldSuppressNativeScrapStart();
        }

        [HarmonyPatch(typeof(ScrapProduction), "StartMiniGameAction")]
        [HarmonyPostfix]
        private static void ScrapProductionStartMiniGameActionPostfix(
            bool __runOriginal)
        {
            if (__runOriginal)
                ScrapInventoryFilterFeature.OnScrapStarted();
        }

        [HarmonyPatch(typeof(ScrapProduction), "StartMiniGameButton")]
        [HarmonyPrefix]
        private static bool ScrapProductionStartMiniGameButtonPrefix()
        {
            return !ScrapInventoryFilterFeature.ShouldSuppressNativeScrapStart();
        }

        [HarmonyPatch(typeof(ScrapProduction), "StartMiniGameButton")]
        [HarmonyPostfix]
        private static void ScrapProductionStartMiniGameButtonPostfix(
            bool __runOriginal)
        {
            if (__runOriginal)
                ScrapInventoryFilterFeature.OnScrapStarted();
        }

        [HarmonyPatch(typeof(ScrapProduction), "CancelMiniGameAction")]
        [HarmonyPostfix]
        private static void ScrapProductionCancelMiniGameActionPostfix()
        {
            ScrapInventoryFilterFeature.OnScrapCancelled();
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Save))]
        [HarmonyPrefix]
        private static bool InventorySavePrefix(ref bool __result)
        {
            if (!ScrapInventoryFilterFeature.ShouldDeferInventorySave())
                return true;

            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(RepairPartWindow),
            nameof(RepairPartWindow.ProcessGameResult))]
        [HarmonyPostfix]
        private static void RepairPartWindowProcessGameResultPostfix(
            RepairPartWindow __instance)
        {
            RepairInventoryFilterFeature.OnRepairProcessed(__instance);
        }

        [HarmonyPatch(typeof(RepairPartWindow), "StartMiniGameAction")]
        [HarmonyPrefix]
        private static bool RepairPartWindowStartMiniGameActionPrefix()
        {
            return !RepairInventoryFilterFeature.ShouldSuppressRepairAction();
        }

        [HarmonyPatch(typeof(RepairPartWindow), "StartMiniGameAction")]
        [HarmonyPostfix]
        private static void RepairPartWindowStartMiniGameActionPostfix(
            bool __runOriginal)
        {
            if (__runOriginal)
                RepairInventoryFilterFeature.OnRepairStarted();
        }

        [HarmonyPatch(typeof(RepairPartWindow), "StartMiniGameButton")]
        [HarmonyPrefix]
        private static bool RepairPartWindowStartMiniGameButtonPrefix()
        {
            return !RepairInventoryFilterFeature.ShouldSuppressRepairAction();
        }

        [HarmonyPatch(typeof(RepairPartWindow), "StartMiniGameButton")]
        [HarmonyPostfix]
        private static void RepairPartWindowStartMiniGameButtonPostfix(
            bool __runOriginal)
        {
            if (__runOriginal)
                RepairInventoryFilterFeature.OnRepairStarted();
        }

        [HarmonyPatch(typeof(RepairPartWindow), "CancelMiniGameAction")]
        [HarmonyPostfix]
        private static void RepairPartWindowCancelMiniGameActionPostfix()
        {
            RepairInventoryFilterFeature.OnRepairCancelled();
        }

        [HarmonyPatch(typeof(RepairPartWindow), "RepairPartAction")]
        [HarmonyPrefix]
        private static bool RepairPartWindowRepairPartActionPrefix()
        {
            return !RepairInventoryFilterFeature.ShouldSuppressRepairAction();
        }
    }
}
