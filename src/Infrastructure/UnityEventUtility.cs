using UnityEngine.Events;

#if NET6_0_OR_GREATER
using Il2Cpp;
using Il2CppCMS.UI;
using Il2CppCMS.UI.Controls;
#else
using CMS.UI;
using CMS.UI.Controls;
#endif

namespace Cms21UiPlus
{
    /// <summary>Removes runtime and persistent Unity listeners from reused UI controls.</summary>
    public static class UnityEventUtility
    {
        public static void RemoveAllListeners(UnityEngine.UI.Button item)
        {
            Clear(item.onClick);
        }

        public static void RemoveAllListeners(GenericButtonOutline item)
        {
            Clear(item.OnClick);
        }

        private static void Clear(UnityEventBase unityEvent)
        {
            if (unityEvent == null)
                return;

            unityEvent.m_Calls.Clear();
            unityEvent.m_PersistentCalls.Clear();
            unityEvent.DirtyPersistentCalls();
            unityEvent.RemoveAllListeners();
        }
    }
}
