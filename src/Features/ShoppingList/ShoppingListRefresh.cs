using System.Reflection;
using HarmonyLib;

#if NET6_0_OR_GREATER
using Il2CppCMS.UI.Windows;
#else
using CMS.UI.Windows;
#endif

namespace Cms21UiPlus
{
    internal static class ShoppingListRefresh
    {
        private static readonly MethodInfo FillItemsMethod =
            AccessTools.Method(typeof(ShopListWindow), "FillItems");

        internal static void RefreshItems(ShopListWindow window)
        {
            if (window == null || FillItemsMethod == null)
                return;

            FillItemsMethod.Invoke(window, null);
            ShoppingListTwoColumnNavigationFeature.RefreshRowsNow(window);
        }
    }
}
