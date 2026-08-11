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
        [DataMember(Name = "default")] public bool @default;
        [DataMember(Name = "applyMode")] public string applyMode;
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
            ModLocalizationCatalog localization;
            ModLocalizationCatalog builtInLocalization =
                ModLocalization.BuiltInCatalog;
            try {
                string json = File.ReadAllText(manifestPath);
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
            if (!TryBuildModel(manifest, localization,
                builtInLocalization, out categories, out options,
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
                if (!string.Equals(setting.type, "boolean",
                    StringComparison.OrdinalIgnoreCase)) {
                    error = "setting " + setting.id +
                        " uses unsupported type " +
                        (setting.type ?? "<null>") +
                        "; only boolean is supported";
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
                if ((!HasLocalizationKey(localization, setting.nameKey) &&
                     !HasLocalizationKey(builtInLocalization,
                         setting.nameKey)) ||
                    (!HasLocalizationKey(localization,
                         setting.descriptionKey) &&
                     !HasLocalizationKey(builtInLocalization,
                         setting.descriptionKey))) {
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
                ModSettingApplyMode applyMode;
                TryParseApplyMode(setting.applyMode, out applyMode);
                ModSettingOption option = new ModSettingOption(setting.key,
                    setting.group, setting.@default,
                    ResolveSettingText(localization, builtInLocalization,
                        setting.nameKey),
                    ResolveSettingText(localization, builtInLocalization,
                        setting.descriptionKey),
                    ResolveEnglishSettingText(localization,
                        builtInLocalization, setting.descriptionKey),
                    applyMode);
                categoriesById[setting.group].Options.Add(option);
                options.Add(option);
            }
            return true;
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
