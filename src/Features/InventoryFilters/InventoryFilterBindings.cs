using System;
using System.Collections.Generic;
using System.Reflection;

#if NET6_0_OR_GREATER
using Il2CppCMS.Containers;
using Il2CppCMS.UI.Windows.Base;
#else
using CMS.Containers;
using CMS.UI.Windows.Base;
#endif

namespace Cms21UiPlus
{
    public static partial class InventoryFilterManager
    {
        private static readonly Dictionary<int, DrawSnapshot> DrawSnapshots =
            new Dictionary<int, DrawSnapshot>();
        private static readonly Dictionary<Type, ItemsBinding> ItemsBindings =
            new Dictionary<Type, ItemsBinding>();
        private static readonly HashSet<Type> MissingItemsBindings = new HashSet<Type>();
        private sealed class ItemsBinding
        {
            public FieldInfo Field;
            public PropertyInfo Property;

            public string Name {
                get {
                    if (Field != null)
                        return Field.DeclaringType.Name + "." + Field.Name;
                    return Property.DeclaringType.Name + "." + Property.Name;
                }
            }

            public Il2CppSystem.Collections.Generic.List<BaseItem> Get(BaseInventory inventory)
            {
                object value = Field != null
                    ? Field.GetValue(inventory)
                    : Property.GetValue(inventory, null);
                return value as Il2CppSystem.Collections.Generic.List<BaseItem>;
            }

            public void Set(BaseInventory inventory,
                Il2CppSystem.Collections.Generic.List<BaseItem> value)
            {
                if (Field != null)
                    Field.SetValue(inventory, value);
                else
                    Property.SetValue(inventory, value, null);
            }
        }

        private sealed class DrawSnapshot
        {
            public BaseInventory Inventory;
            public ItemsBinding Binding;
            public Il2CppSystem.Collections.Generic.List<BaseItem> Original;
            public int Depth;

            public void Restore()
            {
                if (Inventory != null && Binding != null)
                    Binding.Set(Inventory, Original);
            }
        }

        private static ItemsBinding GetItemsBinding(BaseInventory inventory)
        {
            Type runtimeType = inventory.GetType();
            ItemsBinding cached;
            if (ItemsBindings.TryGetValue(runtimeType, out cached))
                return cached;
            if (MissingItemsBindings.Contains(runtimeType))
                return null;

            Type listType = typeof(Il2CppSystem.Collections.Generic.List<BaseItem>);
            Type currentType = runtimeType;

            while (currentType != null && currentType != typeof(object)) {
                FieldInfo field = currentType.GetField("items", BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                    BindingFlags.IgnoreCase);
                if (field != null && !field.IsInitOnly && field.FieldType == listType) {
                    ItemsBinding result = new ItemsBinding();
                    result.Field = field;
                    ItemsBindings[runtimeType] = result;
                    return result;
                }

                PropertyInfo property = currentType.GetProperty("items", BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                    BindingFlags.IgnoreCase);
                if (property != null && property.CanRead && property.CanWrite &&
                    property.GetIndexParameters().Length == 0 && property.PropertyType == listType) {
                    ItemsBinding result = new ItemsBinding();
                    result.Property = property;
                    ItemsBindings[runtimeType] = result;
                    return result;
                }

                currentType = currentType.BaseType;
            }

            MissingItemsBindings.Add(runtimeType);
            ModLogger.Log("[InventoryFilter] Exact PagedWindow.items binding was not found for " +
                (inventory.GetType().FullName ?? inventory.GetType().Name) + ".",
                Types.LoggingLevels.Warning);
            return null;
        }

        private static void ResetCurrentPage(BaseInventory inventory)
        {
            if (inventory == null)
                return;

            SetIntMember(inventory, "currentPage", 0);

            // Advanced page dots keep a separate page-group index. Reset it together
            // with currentPage when a quick-filter mode changes.
            SetIntMember(inventory, "currentDotsPage", 0);
        }

        private static void UpdatePaginationCount(BaseInventory inventory, int itemCount)
        {
            if (inventory == null)
                return;

            try {
                int itemsPerPage;
                if (!TryGetIntMember(inventory, "ItemsPerPage", out itemsPerPage) ||
                    itemsPerPage <= 0)
                    return;

                int pages = itemCount <= 0
                    ? 0
                    : ((itemCount - 1) / itemsPerPage) + 1;

                SetIntMember(inventory, "pagesCount", pages);

                int currentPage;
                if (TryGetIntMember(inventory, "currentPage", out currentPage)) {
                    int maximumPage = pages > 0 ? pages - 1 : 0;
                    if (currentPage < 0 || currentPage > maximumPage)
                        SetIntMember(inventory, "currentPage", maximumPage);
                }
            } catch (Exception exception) {
                ModLogger.Log("[InventoryFilter] Failed to update filtered pagination count." +
                    Environment.NewLine + exception, Types.LoggingLevels.Warning);
            }
        }

        private static bool TryGetIntMember(object target, string memberName,
            out int value)
        {
            value = 0;
            if (target == null)
                return false;

            Type currentType = target.GetType();
            while (currentType != null && currentType != typeof(object)) {
                FieldInfo field = currentType.GetField(memberName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                    BindingFlags.IgnoreCase);
                if (field != null && field.FieldType == typeof(int)) {
                    value = (int)field.GetValue(target);
                    return true;
                }

                PropertyInfo property = currentType.GetProperty(memberName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                    BindingFlags.IgnoreCase);
                if (property != null && property.CanRead &&
                    property.GetIndexParameters().Length == 0 &&
                    property.PropertyType == typeof(int)) {
                    value = (int)property.GetValue(target, null);
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        private static bool SetIntMember(object target, string memberName, int value)
        {
            if (target == null)
                return false;

            Type currentType = target.GetType();
            while (currentType != null && currentType != typeof(object)) {
                FieldInfo field = currentType.GetField(memberName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                    BindingFlags.IgnoreCase);
                if (field != null && !field.IsInitOnly &&
                    field.FieldType == typeof(int)) {
                    field.SetValue(target, value);
                    return true;
                }

                PropertyInfo property = currentType.GetProperty(memberName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                    BindingFlags.IgnoreCase);
                if (property != null && property.CanWrite &&
                    property.GetIndexParameters().Length == 0 &&
                    property.PropertyType == typeof(int)) {
                    property.SetValue(target, value, null);
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }
    }
}
