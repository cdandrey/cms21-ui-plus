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
        private static ShopListWindow activeWindow;
        private static bool subscribed;

        internal static void Bind(ShopListWindow window)
        {
            activeWindow = window;
            if (subscribed)
                return;

            ShoppingListBackend.DisplayChanged += OnDisplayChanged;
            subscribed = true;
        }

        internal static void Unbind(ShopListWindow window)
        {
            if (window != activeWindow)
                return;

            activeWindow = null;
            if (!subscribed)
                return;

            ShoppingListBackend.DisplayChanged -= OnDisplayChanged;
            subscribed = false;
        }

        internal static void RefreshItems(ShopListWindow window)
        {
            if (window == null || FillItemsMethod == null)
                return;

            if (ShoppingListBackend.IsOpen(window))
                ShoppingListBackend.ApplyDisplayToWindow(window);
            FillItemsMethod.Invoke(window, null);
        }

        private static void OnDisplayChanged()
        {
            if (activeWindow != null)
                RefreshItems(activeWindow);
        }
    }
}
