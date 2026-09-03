using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Cms21UiPlus
{
#pragma warning disable 0649 // Fields are populated by DataContractJsonSerializer.
    [DataContract]
    internal sealed class UiSettingsManifestData
    {
        [DataMember(Name = "modId")] public string modId;
        [DataMember(Name = "displayName")] public string displayName;
        [DataMember(Name = "displayNameKey")] public string displayNameKey;
        [DataMember(Name = "config")] public UiSettingsConfigData config;
        [DataMember(Name = "groups")] public UiSettingsGroupData[] groups;
        [DataMember(Name = "settings")] public UiSettingData[] settings;
        [DataMember(Name = "enums")] public UiSettingsEnumData[] enums;
    }

    [DataContract]
    internal sealed class UiSettingsEnumData
    {
        [DataMember(Name = "id")] public string id;
        [DataMember(Name = "ids")] public string[] ids;
        [DataMember(Name = "en")] public string[] en;
        [DataMember(Name = "ru")] public string[] ru;
    }

    [DataContract]
    internal sealed class UiSettingsInlineEnumData
    {
        [DataMember(Name = "ids")] public string[] ids;
        [DataMember(Name = "en")] public string[] en;
        [DataMember(Name = "ru")] public string[] ru;
    }

    [DataContract]
    internal sealed class UiSettingsConfigData
    {
        [DataMember(Name = "path")] public string path;
        [DataMember(Name = "format")] public string format;
        [DataMember(Name = "section")] public string section;
    }

    [DataContract]
    internal sealed class UiSettingsGroupData
    {
        [DataMember(Name = "id")] public string id;
        [DataMember(Name = "name")] public string name;
        [DataMember(Name = "nameKey")] public string nameKey;
        [DataMember(Name = "order")] public int order;
    }

    [DataContract]
    internal sealed class UiSettingData
    {
        [DataMember(Name = "id")] public string id;
        [DataMember(Name = "key")] public string key;
        [DataMember(Name = "group")] public string group;
        [DataMember(Name = "nameKey")] public string nameKey;
        [DataMember(Name = "descriptionKey")] public string descriptionKey;
        [DataMember(Name = "type")] public string type;
        [DataMember(Name = "default")] public object @default;
        [DataMember(Name = "enum")] public string enumName;
        [DataMember(Name = "enumValues")] public UiSettingsInlineEnumData enumValues;
        [DataMember(Name = "step")] public object step;
        [DataMember(Name = "applyMode")] public string applyMode;
        [DataMember(Name = "dependency")] public string dependency;
        [DataMember(Name = "dependencyWarningKey")] public string dependencyWarningKey;
        [DataMember(Name = "dependencyPartialWarningKey")] public string dependencyPartialWarningKey;
        [DataMember(Name = "dependencyDefaultWarningKey")] public string dependencyDefaultWarningKey;
        [DataMember(Name = "dependencySwitchKey")] public string dependencySwitchKey;
        [DataMember(Name = "dependencyWhenFalse")] public string dependencyWhenFalse;
        [DataMember(Name = "indicatorSwitchKey")] public string indicatorSwitchKey;
        [DataMember(Name = "order")] public int order;
    }

#pragma warning restore 0649

    internal static class UiSettingsManifestLoader
    {
        public const string FileSuffix = ".ui-settings.json";

        private static readonly Regex IdRegex = new Regex(
            @"^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.Compiled);
        private static readonly Regex TomlKeyRegex = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private sealed class OrderedGroup
        {
            public UiSettingsGroupData Data;
            public int SourceIndex;
        }

        private sealed class OrderedSetting
        {
            public UiSettingData Data;
            public int SourceIndex;
        }

        public static bool TryCreateProvider(string dllPath,
            string assemblyName, out IModSettingsProvider provider,
            out bool manifestFound, out string status)
        {
            provider = null;
            manifestFound = false;
            status = string.Empty;

            if (string.IsNullOrEmpty(dllPath) ||
                string.IsNullOrEmpty(assemblyName)) {
                status = "DLL path or assembly name is empty";
                return false;
            }

            string manifestPath = FindManifestPath(dllPath, assemblyName);
            if (string.IsNullOrEmpty(manifestPath)) {
                status = "manifest was not found";
                return false;
            }
            manifestFound = true;

            UiSettingsManifestData manifest;
            string json;
            ModLocalizationCatalog localization;
            ModLocalizationCatalog builtInLocalization =
                ModLocalization.BuiltInCatalog;
            try {
                json = File.ReadAllText(manifestPath);
                if (string.IsNullOrWhiteSpace(json)) {
                    status = "manifest is empty";
                    return false;
                }
                string localizationError;
                if (!ModLocalizationCatalog.TryReadManifest(json,
                    out localization, out localizationError)) {
                    status = "manifest localization could not be read: " +
                        localizationError;
                    return false;
                }
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(
                        typeof(UiSettingsManifestData));
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                using (MemoryStream stream = new MemoryStream(jsonBytes)) {
                    manifest = serializer.ReadObject(stream) as
                        UiSettingsManifestData;
                }
            } catch (Exception exception) {
                status = "manifest JSON could not be read: " +
                    exception.GetType().Name + ": " + exception.Message;
                return false;
            }

            if (manifest == null) {
                status = "manifest JSON did not produce a document";
                return false;
            }

            string error;
            if (!ValidateHeader(manifest, localization, builtInLocalization,
                assemblyName, out error)) {
                status = error;
                return false;
            }

            string configPath;
            if (!TryResolveConfigPath(manifestPath, manifest.config,
                out configPath, out error)) {
                status = error;
                return false;
            }

            List<ModSettingsCategory> categories;
            List<ModSettingOption> options;
            Dictionary<string, List<ModSettingEnumState>> enumDefinitions;
            if (!TryBuildEnumDefinitions(manifest.enums,
                out enumDefinitions, out error)) {
                status = error;
                return false;
            }
            if (!TryBuildModel(manifest, localization,
                builtInLocalization, enumDefinitions,
                out categories, out options,
                out error)) {
                status = error;
                return false;
            }

            ManifestModSettingsProvider created =
                new ManifestModSettingsProvider(manifest.modId,
                    ResolveManifestText(localization, builtInLocalization,
                        manifest.displayNameKey, manifest.displayName),
                    manifestPath, configPath, manifest.config.section,
                    categories, options);
            if (!created.TryInitialize(out error)) {
                status = error;
                return false;
            }

            provider = created;
            status = "loaded " + Path.GetFileName(manifestPath) +
                "; config=" + configPath +
                "; settings=" + options.Count;
            return true;
        }

        private static string FindManifestPath(string dllPath,
            string assemblyName)
        {
            string dllDirectory;
            try {
                dllDirectory = Path.GetDirectoryName(
                    Path.GetFullPath(dllPath));
            } catch {
                return null;
            }
            if (string.IsNullOrEmpty(dllDirectory))
                return null;

            string fileName = assemblyName + FileSuffix;
            string[] candidates = {
                Path.Combine(dllDirectory, fileName),
                Path.Combine(dllDirectory, assemblyName, fileName),
            };

            for (int i = 0; i < candidates.Length; i++) {
                try {
                    if (File.Exists(candidates[i]))
                        return Path.GetFullPath(candidates[i]);
                } catch {
                }
            }
            return null;
        }

        private static bool ValidateHeader(UiSettingsManifestData manifest,
            ModLocalizationCatalog localization,
            ModLocalizationCatalog builtInLocalization, string assemblyName,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(manifest.displayName) &&
                !HasLocalizationKey(localization, manifest.displayNameKey) &&
                !HasLocalizationKey(builtInLocalization,
                    manifest.displayNameKey)) {
                error = "displayName is missing";
                return false;
            }
            if (string.IsNullOrWhiteSpace(manifest.modId) ||
                !IdRegex.IsMatch(manifest.modId)) {
                error = "modId is missing or invalid";
                return false;
            }
            if (!string.Equals(manifest.modId, assemblyName,
                StringComparison.OrdinalIgnoreCase)) {
                error = "modId " + manifest.modId +
                    " does not match assembly " + assemblyName;
                return false;
            }
            if (manifest.config == null) {
                error = "config block is missing";
                return false;
            }
            if (!string.Equals(manifest.config.format, "toml",
                StringComparison.OrdinalIgnoreCase)) {
                error = "unsupported config format: " +
                    (manifest.config.format ?? "<null>") +
                    "; only toml is supported";
                return false;
            }
            if (string.IsNullOrWhiteSpace(manifest.config.section)) {
                error = "config.section is missing";
                return false;
            }
            if (manifest.groups == null || manifest.groups.Length == 0) {
                error = "groups array is empty";
                return false;
            }
            if (manifest.settings == null || manifest.settings.Length == 0) {
                error = "settings array is empty";
                return false;
            }
            return true;
        }

        private static bool TryResolveConfigPath(string manifestPath,
            UiSettingsConfigData config, out string configPath,
            out string error)
        {
            configPath = null;
            error = string.Empty;
            if (config == null || string.IsNullOrWhiteSpace(config.path)) {
                error = "config.path is missing";
                return false;
            }

            try {
                string candidate = config.path.Trim();
                if (!Path.IsPathRooted(candidate)) {
                    string manifestDirectory =
                        Path.GetDirectoryName(manifestPath);
                    candidate = Path.Combine(manifestDirectory, candidate);
                }
                configPath = Path.GetFullPath(candidate);
                if (string.Equals(configPath, manifestPath,
                    StringComparison.OrdinalIgnoreCase)) {
                    error = "config.path points to the manifest itself";
                    configPath = null;
                    return false;
                }
                return true;
            } catch (Exception exception) {
                error = "config.path is invalid: " + exception.Message;
                return false;
            }
        }

        private static bool TryBuildModel(UiSettingsManifestData manifest,
            ModLocalizationCatalog localization,
            ModLocalizationCatalog builtInLocalization,
            IDictionary<string, List<ModSettingEnumState>> enumDefinitions,
            out List<ModSettingsCategory> categories,
            out List<ModSettingOption> options, out string error)
        {
            categories = new List<ModSettingsCategory>();
            options = new List<ModSettingOption>();
            error = string.Empty;

            List<OrderedGroup> orderedGroups = new List<OrderedGroup>();
            HashSet<string> groupIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manifest.groups.Length; i++) {
                UiSettingsGroupData group = manifest.groups[i];
                if (group == null || string.IsNullOrWhiteSpace(group.id) ||
                    !IdRegex.IsMatch(group.id)) {
                    error = "group at index " + i + " has an invalid id";
                    return false;
                }
                if (!groupIds.Add(group.id)) {
                    error = "duplicate group id: " + group.id;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(group.name) &&
                    !HasLocalizationKey(localization, group.nameKey) &&
                    !HasLocalizationKey(builtInLocalization,
                        group.nameKey)) {
                    error = "group " + group.id + " has no name";
                    return false;
                }
                orderedGroups.Add(new OrderedGroup {
                    Data = group,
                    SourceIndex = i,
                });
            }
            orderedGroups.Sort(delegate (OrderedGroup left,
                OrderedGroup right) {
                int order = left.Data.order.CompareTo(right.Data.order);
                return order != 0 ? order :
                    left.SourceIndex.CompareTo(right.SourceIndex);
            });

            Dictionary<string, ModSettingsCategory> categoriesById =
                new Dictionary<string, ModSettingsCategory>(
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> categoryOrderById =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < orderedGroups.Count; i++) {
                UiSettingsGroupData group = orderedGroups[i].Data;
                ModSettingsCategory category = new ModSettingsCategory(
                    group.id, ResolveManifestText(localization,
                        builtInLocalization, group.nameKey, group.name));
                categories.Add(category);
                categoriesById.Add(group.id, category);
                categoryOrderById.Add(group.id, i);
            }

            List<OrderedSetting> orderedSettings =
                new List<OrderedSetting>();
            HashSet<string> settingIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> settingKeys = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manifest.settings.Length; i++) {
                UiSettingData setting = manifest.settings[i];
                if (setting == null || string.IsNullOrWhiteSpace(setting.id) ||
                    !IdRegex.IsMatch(setting.id)) {
                    error = "setting at index " + i + " has an invalid id";
                    return false;
                }
                if (!settingIds.Add(setting.id)) {
                    error = "duplicate setting id: " + setting.id;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(setting.key) ||
                    !TomlKeyRegex.IsMatch(setting.key)) {
                    error = "setting " + setting.id +
                        " has an invalid TOML key";
                    return false;
                }
                if (!settingKeys.Add(setting.key)) {
                    error = "duplicate setting key: " + setting.key;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(setting.group) ||
                    !categoriesById.ContainsKey(setting.group)) {
                    error = "setting " + setting.id +
                        " references an unknown group: " +
                        (setting.group ?? "<null>");
                    return false;
                }
                ModSettingType settingType;
                if (!TryParseSettingType(setting.type, out settingType)) {
                    error = "setting " + setting.id +
                        " uses unsupported type " +
                        (setting.type ?? "<null>") +
                        "; supported types are boolean, number, string, enum";
                    return false;
                }
                if (settingType == ModSettingType.Enum) {
                    bool hasEnumReference =
                        !string.IsNullOrWhiteSpace(setting.enumName);
                    bool hasInlineEnum = setting.enumValues != null;
                    if (hasEnumReference == hasInlineEnum) {
                        error = "setting " + setting.id +
                            " must declare exactly one enum source";
                        return false;
                    }
                    if (hasEnumReference &&
                        (enumDefinitions == null ||
                         !enumDefinitions.ContainsKey(setting.enumName))) {
                        error = "setting " + setting.id +
                            " references an unknown enum: " +
                            setting.enumName;
                        return false;
                    }
                } else if (!string.IsNullOrWhiteSpace(setting.enumName) ||
                    setting.enumValues != null) {
                    error = "setting " + setting.id +
                        " declares enum values but its type is not enum";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(setting.nameKey)) {
                    error = "setting " + setting.id + " has no nameKey";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(setting.descriptionKey)) {
                    error = "setting " + setting.id +
                        " has no descriptionKey";
                    return false;
                }
                bool hasDependency =
                    !string.IsNullOrWhiteSpace(setting.dependency);
                bool hasDependencyWarning =
                    !string.IsNullOrWhiteSpace(setting.dependencyWarningKey);
                bool hasDependencyPartialWarning =
                    !string.IsNullOrWhiteSpace(
                        setting.dependencyPartialWarningKey);
                bool hasDependencyDefaultWarning =
                    !string.IsNullOrWhiteSpace(
                        setting.dependencyDefaultWarningKey);
                bool hasDependencySwitch =
                    !string.IsNullOrWhiteSpace(setting.dependencySwitchKey);
                bool hasDependencyWhenFalse =
                    !string.IsNullOrWhiteSpace(setting.dependencyWhenFalse);
                if (hasDependency != hasDependencyWarning) {
                    error = "setting " + setting.id +
                        " must declare dependency and dependencyWarningKey together";
                    return false;
                }
                if (!hasDependency && (hasDependencyPartialWarning ||
                    hasDependencyDefaultWarning || hasDependencySwitch ||
                    hasDependencyWhenFalse)) {
                    error = "setting " + setting.id +
                        " declares dependency options without dependency";
                    return false;
                }
                if (hasDependencySwitch != hasDependencyWhenFalse) {
                    error = "setting " + setting.id +
                        " must declare dependencySwitchKey and dependencyWhenFalse together";
                    return false;
                }
                if (hasDependency && !IdRegex.IsMatch(setting.dependency)) {
                    error = "setting " + setting.id +
                        " has an invalid dependency id";
                    return false;
                }
                if (hasDependency &&
                    settingType != ModSettingType.Boolean) {
                    error = "setting " + setting.id +
                        " declares dependency but is not boolean";
                    return false;
                }
                if (hasDependencySwitch) {
                    if (!TomlKeyRegex.IsMatch(setting.dependencySwitchKey) ||
                        !IdRegex.IsMatch(setting.dependencyWhenFalse)) {
                        error = "setting " + setting.id +
                            " has an invalid dependency switch";
                        return false;
                    }
                    UiSettingData switchSetting = null;
                    for (int switchIndex = 0;
                        switchIndex < manifest.settings.Length; switchIndex++) {
                        UiSettingData candidate = manifest.settings[switchIndex];
                        if (candidate != null && string.Equals(candidate.key,
                            setting.dependencySwitchKey,
                            StringComparison.OrdinalIgnoreCase)) {
                            switchSetting = candidate;
                            break;
                        }
                    }
                    ModSettingType switchType;
                    if (switchSetting == null ||
                        !TryParseSettingType(switchSetting.type,
                            out switchType) ||
                        switchType != ModSettingType.Boolean) {
                        error = "setting " + setting.id +
                            " dependencySwitchKey must reference a boolean setting";
                        return false;
                    }
                }
                if (!string.IsNullOrWhiteSpace(
                        setting.indicatorSwitchKey)) {
                    if (!TomlKeyRegex.IsMatch(setting.indicatorSwitchKey)) {
                        error = "setting " + setting.id +
                            " has an invalid indicatorSwitchKey";
                        return false;
                    }
                    UiSettingData indicatorSetting = null;
                    for (int switchIndex = 0;
                        switchIndex < manifest.settings.Length; switchIndex++) {
                        UiSettingData candidate = manifest.settings[switchIndex];
                        if (candidate != null && string.Equals(candidate.key,
                            setting.indicatorSwitchKey,
                            StringComparison.OrdinalIgnoreCase)) {
                            indicatorSetting = candidate;
                            break;
                        }
                    }
                    ModSettingType indicatorType;
                    if (indicatorSetting == null ||
                        !TryParseSettingType(indicatorSetting.type,
                            out indicatorType) ||
                        indicatorType != ModSettingType.Boolean) {
                        error = "setting " + setting.id +
                            " indicatorSwitchKey must reference a boolean setting";
                        return false;
                    }
                }
                if ((!HasLocalizationKey(localization, setting.nameKey) &&
                     !HasLocalizationKey(builtInLocalization,
                         setting.nameKey)) ||
                    (!HasLocalizationKey(localization,
                         setting.descriptionKey) &&
                     !HasLocalizationKey(builtInLocalization,
                         setting.descriptionKey)) ||
                    (hasDependency &&
                     !HasLocalizationKey(localization,
                         setting.dependencyWarningKey) &&
                     !HasLocalizationKey(builtInLocalization,
                         setting.dependencyWarningKey)) ||
                    (hasDependencyPartialWarning &&
                     !HasLocalizationKey(localization,
                         setting.dependencyPartialWarningKey) &&
                     !HasLocalizationKey(builtInLocalization,
                         setting.dependencyPartialWarningKey)) ||
                    (hasDependencyDefaultWarning &&
                     !HasLocalizationKey(localization,
                         setting.dependencyDefaultWarningKey) &&
                     !HasLocalizationKey(builtInLocalization,
                         setting.dependencyDefaultWarningKey))) {
                    error = "setting " + setting.id +
                        " localization key is missing from localization.en " +
                        "or localization.ru";
                    return false;
                }

                ModSettingApplyMode applyMode;
                if (!TryParseApplyMode(setting.applyMode, out applyMode)) {
                    error = "setting " + setting.id +
                        " has an invalid applyMode: " +
                        (setting.applyMode ?? "<null>");
                    return false;
                }

                orderedSettings.Add(new OrderedSetting {
                    Data = setting,
                    SourceIndex = i,
                });
            }
            orderedSettings.Sort(delegate (OrderedSetting left,
                OrderedSetting right) {
                int leftGroup = categoryOrderById[left.Data.group];
                int rightGroup = categoryOrderById[right.Data.group];
                int groupOrder = leftGroup.CompareTo(rightGroup);
                if (groupOrder != 0)
                    return groupOrder;
                int order = left.Data.order.CompareTo(right.Data.order);
                return order != 0 ? order :
                    left.SourceIndex.CompareTo(right.SourceIndex);
            });

            for (int i = 0; i < orderedSettings.Count; i++) {
                UiSettingData setting = orderedSettings[i].Data;
                bool hasDependency =
                    !string.IsNullOrWhiteSpace(setting.dependency);
                ModSettingApplyMode applyMode;
                TryParseApplyMode(setting.applyMode, out applyMode);
                ModSettingType settingType;
                TryParseSettingType(setting.type, out settingType);
                IList<ModSettingEnumState> enumStates =
                    new List<ModSettingEnumState>();
                if (settingType == ModSettingType.Enum) {
                    if (setting.enumValues != null) {
                        List<ModSettingEnumState> inlineStates;
                        if (!TryBuildEnumStates(setting.id,
                            setting.enumValues.ids, setting.enumValues.en,
                            setting.enumValues.ru, out inlineStates,
                            out error))
                            return false;
                        enumStates = inlineStates;
                    } else {
                        enumStates = enumDefinitions[setting.enumName];
                    }
                }
                ModSettingValue defaultValue;
                if (!ModSettingValue.TryCreate(setting.@default,
                    out defaultValue)) {
                    error = "setting " + setting.id +
                        " has an invalid default value";
                    return false;
                }
                ModSettingValueType expectedValueType =
                    GetSettingValueType(settingType, enumStates);
                if (defaultValue.Type != expectedValueType) {
                    error = "setting " + setting.id +
                        " default value does not match type " +
                        setting.type;
                    return false;
                }
                double numberStep = 1d;
                if (settingType == ModSettingType.Number &&
                    setting.step != null) {
                    ModSettingValue stepValue;
                    if (!ModSettingValue.TryCreate(setting.step,
                        out stepValue) ||
                        stepValue.Type != ModSettingValueType.Number ||
                        stepValue.NumberValue <= 0d) {
                        error = "setting " + setting.id +
                            " has an invalid number step";
                        return false;
                    }
                    numberStep = stepValue.NumberValue;
                } else if (settingType != ModSettingType.Number &&
                    setting.step != null) {
                    error = "setting " + setting.id +
                        " declares step but its type is not number";
                    return false;
                }
                ModSettingOption option = new ModSettingOption(setting.key,
                    setting.group, settingType, defaultValue, enumStates,
                    numberStep,
                    ResolveSettingText(localization, builtInLocalization,
                        setting.nameKey),
                    ResolveSettingText(localization, builtInLocalization,
                        setting.descriptionKey),
                    ResolveEnglishSettingText(localization,
                        builtInLocalization, setting.descriptionKey),
                    setting.dependency,
                    hasDependency
                        ? ResolveSettingText(localization,
                            builtInLocalization,
                            setting.dependencyWarningKey)
                        : string.Empty,
                    hasDependency &&
                        !string.IsNullOrWhiteSpace(
                            setting.dependencyPartialWarningKey)
                        ? ResolveSettingText(localization,
                            builtInLocalization,
                            setting.dependencyPartialWarningKey)
                        : string.Empty,
                    hasDependency &&
                        !string.IsNullOrWhiteSpace(
                            setting.dependencyDefaultWarningKey)
                        ? ResolveSettingText(localization,
                            builtInLocalization,
                            setting.dependencyDefaultWarningKey)
                        : string.Empty,
                    hasDependency ? setting.dependencySwitchKey : string.Empty,
                    hasDependency ? setting.dependencyWhenFalse : string.Empty,
                    setting.indicatorSwitchKey, applyMode);
                if (!option.IsValueAllowed(defaultValue)) {
                    error = "setting " + setting.id +
                        " default value is not present in enum " +
                        setting.enumName;
                    return false;
                }
                categoriesById[setting.group].Options.Add(option);
                options.Add(option);
            }
            return true;
        }

        private static bool TryParseSettingType(string value,
            out ModSettingType type)
        {
            type = ModSettingType.Boolean;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (string.Equals(value, "boolean",
                StringComparison.OrdinalIgnoreCase)) {
                type = ModSettingType.Boolean;
                return true;
            }
            if (string.Equals(value, "number",
                StringComparison.OrdinalIgnoreCase)) {
                type = ModSettingType.Number;
                return true;
            }
            if (string.Equals(value, "string",
                StringComparison.OrdinalIgnoreCase)) {
                type = ModSettingType.String;
                return true;
            }
            if (string.Equals(value, "enum",
                StringComparison.OrdinalIgnoreCase)) {
                type = ModSettingType.Enum;
                return true;
            }
            return false;
        }

        private static ModSettingValueType GetSettingValueType(
            ModSettingType settingType,
            IList<ModSettingEnumState> enumStates)
        {
            if (settingType == ModSettingType.Boolean)
                return ModSettingValueType.Boolean;
            if (settingType == ModSettingType.String)
                return ModSettingValueType.String;
            if (settingType == ModSettingType.Enum)
                return ModSettingValueType.String;
            return ModSettingValueType.Number;
        }

        private static bool TryBuildEnumDefinitions(
            UiSettingsEnumData[] source,
            out Dictionary<string, List<ModSettingEnumState>> definitions,
            out string error)
        {
            definitions = new Dictionary<string, List<ModSettingEnumState>>(
                StringComparer.OrdinalIgnoreCase);
            error = string.Empty;
            if (source == null)
                return true;

            for (int i = 0; i < source.Length; i++) {
                UiSettingsEnumData enumData = source[i];
                if (enumData == null ||
                    string.IsNullOrWhiteSpace(enumData.id) ||
                    !IdRegex.IsMatch(enumData.id)) {
                    error = "enum at index " + i + " has an invalid id";
                    return false;
                }
                if (definitions.ContainsKey(enumData.id)) {
                    error = "duplicate enum id: " + enumData.id;
                    return false;
                }

                List<ModSettingEnumState> states;
                if (!TryBuildEnumStates(enumData.id, enumData.ids,
                    enumData.en, enumData.ru, out states, out error))
                    return false;
                definitions.Add(enumData.id, states);
            }
            return true;
        }

        private static bool TryBuildEnumStates(string context, string[] ids,
            string[] english, string[] russian,
            out List<ModSettingEnumState> states, out string error)
        {
            states = new List<ModSettingEnumState>();
            error = string.Empty;
            if (ids == null || ids.Length == 0) {
                error = "enum " + context + " has no ids";
                return false;
            }
            if (english == null && russian == null) {
                error = "enum " + context +
                    " must declare en or ru localization";
                return false;
            }
            if (english != null && english.Length != ids.Length) {
                error = "enum " + context +
                    " en localization count does not match ids";
                return false;
            }
            if (russian != null && russian.Length != ids.Length) {
                error = "enum " + context +
                    " ru localization count does not match ids";
                return false;
            }

            HashSet<string> uniqueIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int i = 0; i < ids.Length; i++) {
                string stateId = ids[i];
                if (string.IsNullOrWhiteSpace(stateId) ||
                    !IdRegex.IsMatch(stateId)) {
                    error = "enum " + context +
                        " has an invalid id at index " + i;
                    return false;
                }
                if (!uniqueIds.Add(stateId)) {
                    error = "enum " + context +
                        " has duplicate id: " + stateId;
                    return false;
                }

                string displayName = ResolveEnumDisplayName(stateId, i,
                    english, russian);
                states.Add(new ModSettingEnumState(displayName,
                    ModSettingValue.FromString(stateId)));
            }
            return true;
        }

        private static string ResolveEnumDisplayName(string stateId,
            int index, string[] english, string[] russian)
        {
            string preferred = ModLocalization.IsRussian
                ? GetEnumLocalization(russian, index)
                : GetEnumLocalization(english, index);
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred;

            string fallback = ModLocalization.IsRussian
                ? GetEnumLocalization(english, index)
                : GetEnumLocalization(russian, index);
            return !string.IsNullOrWhiteSpace(fallback)
                ? fallback : stateId;
        }

        private static string GetEnumLocalization(string[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
                return null;
            return values[index];
        }

        private static bool HasLocalizationKey(
            ModLocalizationCatalog localization, string key)
        {
            return localization != null && localization.HasBoth(key);
        }

        private static string ResolveManifestText(
            ModLocalizationCatalog localization,
            ModLocalizationCatalog builtInLocalization, string key,
            string fallback)
        {
            if (HasLocalizationKey(localization, key))
                return localization.Get(key, fallback);
            return builtInLocalization.Get(key, fallback);
        }

        private static string ResolveSettingText(
            ModLocalizationCatalog localization,
            ModLocalizationCatalog builtInLocalization, string key)
        {
            return HasLocalizationKey(localization, key)
                ? localization.Get(key, key)
                : builtInLocalization.Get(key, key);
        }

        private static string ResolveEnglishSettingText(
            ModLocalizationCatalog localization,
            ModLocalizationCatalog builtInLocalization, string key)
        {
            return HasLocalizationKey(localization, key)
                ? localization.GetEnglish(key, key)
                : builtInLocalization.GetEnglish(key, key);
        }

        private static bool TryParseApplyMode(string value,
            out ModSettingApplyMode mode)
        {
            mode = ModSettingApplyMode.RestartGame;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string normalized = value.Trim();
            if (string.Equals(normalized, "immediate",
                StringComparison.OrdinalIgnoreCase)) {
                mode = ModSettingApplyMode.Immediate;
                return true;
            }
            if (string.Equals(normalized, "reopenWindow",
                StringComparison.OrdinalIgnoreCase)) {
                mode = ModSettingApplyMode.ReopenWindow;
                return true;
            }
            if (string.Equals(normalized, "reloadLocation",
                StringComparison.OrdinalIgnoreCase)) {
                mode = ModSettingApplyMode.ReloadLocation;
                return true;
            }
            if (string.Equals(normalized, "restartGame",
                StringComparison.OrdinalIgnoreCase)) {
                mode = ModSettingApplyMode.RestartGame;
                return true;
            }
            return false;
        }
    }
}
