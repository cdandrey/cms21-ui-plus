using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#if NET6_0_OR_GREATER
using Il2Cpp;
#endif

namespace Cms21UiPlus
{
    internal enum ModSettingApplyMode
    {
        Immediate,
        ReopenWindow,
        ReloadLocation,
        RestartGame,
    }

    internal enum ModSettingType
    {
        Boolean,
        Number,
        String,
        Enum,
    }

    internal enum ModSettingValueType
    {
        Boolean,
        Number,
        String,
    }

    internal sealed class ModSettingValue : IEquatable<ModSettingValue>
    {
        private ModSettingValue(ModSettingValueType type, bool boolValue,
            double numberValue, string stringValue)
        {
            Type = type;
            BooleanValue = boolValue;
            NumberValue = numberValue;
            StringValue = stringValue ?? string.Empty;
        }

        public ModSettingValueType Type { get; private set; }
        public bool BooleanValue { get; private set; }
        public double NumberValue { get; private set; }
        public string StringValue { get; private set; }

        public static ModSettingValue FromBoolean(bool value)
        {
            return new ModSettingValue(ModSettingValueType.Boolean,
                value, 0d, null);
        }

        public static ModSettingValue FromNumber(double value)
        {
            return new ModSettingValue(ModSettingValueType.Number,
                false, value, null);
        }

        public static ModSettingValue FromString(string value)
        {
            return new ModSettingValue(ModSettingValueType.String,
                false, 0d, value);
        }

        public static bool TryCreate(object raw, out ModSettingValue value)
        {
            value = null;
            if (raw is bool) {
                value = FromBoolean((bool)raw);
                return true;
            }
            string stringValue = raw as string;
            if (stringValue != null) {
                value = FromString(stringValue);
                return true;
            }
            if (raw == null)
                return false;

            System.Type rawType = raw.GetType();
            TypeCode typeCode = System.Type.GetTypeCode(rawType);
            if (typeCode == TypeCode.Byte || typeCode == TypeCode.SByte ||
                typeCode == TypeCode.Int16 || typeCode == TypeCode.UInt16 ||
                typeCode == TypeCode.Int32 || typeCode == TypeCode.UInt32 ||
                typeCode == TypeCode.Int64 || typeCode == TypeCode.UInt64 ||
                typeCode == TypeCode.Single || typeCode == TypeCode.Double ||
                typeCode == TypeCode.Decimal) {
                try {
                    double number = Convert.ToDouble(raw,
                        CultureInfo.InvariantCulture);
                    if (!double.IsNaN(number) && !double.IsInfinity(number)) {
                        value = FromNumber(number);
                        return true;
                    }
                } catch {
                }
            }
            return false;
        }

        public string ToDisplayString()
        {
            if (Type == ModSettingValueType.Boolean)
                return BooleanValue
                    ? ModLocalization.Get("LOC_Yes")
                    : ModLocalization.Get("LOC_No");
            if (Type == ModSettingValueType.Number)
                return NumberValue.ToString("G15",
                    CultureInfo.InvariantCulture);
            return StringValue;
        }

        public bool Equals(ModSettingValue other)
        {
            if (ReferenceEquals(other, null) || Type != other.Type)
                return false;
            if (Type == ModSettingValueType.Boolean)
                return BooleanValue == other.BooleanValue;
            if (Type == ModSettingValueType.Number)
                return NumberValue.Equals(other.NumberValue);
            return string.Equals(StringValue, other.StringValue,
                StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ModSettingValue);
        }

        public override int GetHashCode()
        {
            if (Type == ModSettingValueType.Boolean)
                return BooleanValue.GetHashCode();
            if (Type == ModSettingValueType.Number)
                return NumberValue.GetHashCode();
            return StringValue.GetHashCode();
        }
    }

    internal sealed class ModSettingEnumState
    {
        public ModSettingEnumState(string name, ModSettingValue value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; private set; }
        public ModSettingValue Value { get; private set; }
    }

    internal sealed class ModSettingOption
    {
        public ModSettingOption(string key, string categoryId,
            ModSettingType type, ModSettingValue defaultValue,
            IList<ModSettingEnumState> enumStates, double numberStep,
            string name, string description, string configDescription,
            string dependencyId, string dependencyWarning,
            string dependencyPartialWarning,
            string dependencyDefaultWarning, string dependencySwitchKey,
            string dependencyWhenFalseId, string indicatorSwitchKey,
            ModSettingApplyMode applyMode)
        {
            Key = key;
            CategoryId = categoryId;
            Type = type;
            DefaultValue = defaultValue;
            EnumStates = enumStates ?? new List<ModSettingEnumState>();
            NumberStep = numberStep > 0d ? numberStep : 1d;
            Name = name;
            Description = description;
            ConfigDescription = configDescription;
            DependencyId = dependencyId ?? string.Empty;
            DependencyWarning = dependencyWarning ?? string.Empty;
            DependencyPartialWarning = dependencyPartialWarning ?? string.Empty;
            DependencyDefaultWarning = dependencyDefaultWarning ?? string.Empty;
            DependencySwitchKey = dependencySwitchKey ?? string.Empty;
            DependencyWhenFalseId = dependencyWhenFalseId ?? string.Empty;
            IndicatorSwitchKey = indicatorSwitchKey ?? string.Empty;
            ApplyMode = applyMode;
        }

        public string Key { get; private set; }
        public string CategoryId { get; private set; }
        public ModSettingType Type { get; private set; }
        public ModSettingValue DefaultValue { get; private set; }
        public IList<ModSettingEnumState> EnumStates { get; private set; }
        public double NumberStep { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string ConfigDescription { get; private set; }
        public string DependencyId { get; private set; }
        public string DependencyWarning { get; private set; }
        public string DependencyPartialWarning { get; private set; }
        public string DependencyDefaultWarning { get; private set; }
        public string DependencySwitchKey { get; private set; }
        public string DependencyWhenFalseId { get; private set; }
        public string IndicatorSwitchKey { get; private set; }
        public ModSettingApplyMode ApplyMode { get; private set; }

        public ModSettingValueType ValueType
        {
            get {
                if (Type == ModSettingType.Boolean)
                    return ModSettingValueType.Boolean;
                if (Type == ModSettingType.String)
                    return ModSettingValueType.String;
                if (Type == ModSettingType.Enum && EnumStates.Count > 0)
                    return EnumStates[0].Value.Type;
                return ModSettingValueType.Number;
            }
        }

        public bool IsValueAllowed(ModSettingValue value)
        {
            if (value == null || value.Type != ValueType)
                return false;
            if (Type != ModSettingType.Enum)
                return true;
            for (int i = 0; i < EnumStates.Count; i++) {
                if (EnumStates[i].Value.Equals(value))
                    return true;
            }
            return false;
        }

        public string GetDisplayValue(ModSettingValue value)
        {
            ModSettingValue actual = IsValueAllowed(value)
                ? value : DefaultValue;
            if (Type == ModSettingType.Enum) {
                for (int i = 0; i < EnumStates.Count; i++) {
                    if (EnumStates[i].Value.Equals(actual))
                        return EnumStates[i].Name;
                }
            }
            return actual != null ? actual.ToDisplayString() : string.Empty;
        }

        public bool CanMoveValue(ModSettingValue value, int direction)
        {
            ModSettingValue ignored;
            return TryMoveValue(value, direction, out ignored);
        }

        public bool TryMoveValue(ModSettingValue value, int direction,
            out ModSettingValue next)
        {
            next = value;
            if (direction == 0 || !IsValueAllowed(value))
                return false;

            if (Type == ModSettingType.Boolean) {
                if (direction < 0 && value.BooleanValue) {
                    next = ModSettingValue.FromBoolean(false);
                    return true;
                }
                if (direction > 0 && !value.BooleanValue) {
                    next = ModSettingValue.FromBoolean(true);
                    return true;
                }
                return false;
            }

            if (Type == ModSettingType.Number) {
                next = ModSettingValue.FromNumber(value.NumberValue +
                    NumberStep * (direction < 0 ? -1d : 1d));
                return true;
            }

            if (Type == ModSettingType.Enum) {
                int current = -1;
                for (int i = 0; i < EnumStates.Count; i++) {
                    if (EnumStates[i].Value.Equals(value)) {
                        current = i;
                        break;
                    }
                }
                int target = current + (direction < 0 ? -1 : 1);
                if (current < 0 || target < 0 || target >= EnumStates.Count)
                    return false;
                next = EnumStates[target].Value;
                return true;
            }

            return false;
        }
    }

    internal sealed class ModSettingsCategory
    {
        public ModSettingsCategory(string id, string name)
        {
            Id = id;
            Name = name;
            Options = new List<ModSettingOption>();
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public IList<ModSettingOption> Options { get; private set; }

    }

    internal interface IModSettingsProvider
    {
        string Id { get; }
        string DisplayName { get; }
        IList<ModSettingsCategory> Categories { get; }
        object CreateDraft();
        ModSettingValue GetValue(object draft, string key);
        void SetValue(object draft, string key, ModSettingValue value);
        void ResetCategory(object draft, string categoryId);
        bool HasChanges(object draft);
        bool ApplySetting(object draft, string key, out string status,
            out ModSettingApplyMode applyMode);
        bool Apply(object draft, out string status,
            out ModSettingApplyMode highestApplyMode);
    }

}
