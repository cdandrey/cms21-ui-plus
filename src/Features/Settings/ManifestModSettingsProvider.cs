using System;
using System.Collections.Generic;

namespace Cms21UiPlus
{
    internal sealed class ManifestModSettingsProvider : IModSettingsProvider
    {
        private sealed class Draft
        {
            public Draft(Dictionary<string, ModSettingValue> values)
            {
                Values = values;
            }

            public Dictionary<string, ModSettingValue> Values;
        }

        private readonly string id;
        private readonly string displayName;
        private readonly string manifestPath;
        private readonly string configPath;
        private readonly string configSection;
        private readonly List<ModSettingsCategory> categories;
        private readonly List<ModSettingOption> options;
        private readonly Dictionary<string, ModSettingOption> optionsByKey;
        private Dictionary<string, ModSettingValue> savedValues;

        public ManifestModSettingsProvider(string id,
            string displayName,
            string manifestPath, string configPath, string configSection,
            List<ModSettingsCategory> categories,
            List<ModSettingOption> options)
        {
            this.id = id;
            this.displayName = string.IsNullOrWhiteSpace(
                displayName) ? id : displayName;
            this.manifestPath = manifestPath;
            this.configPath = configPath;
            this.configSection = configSection;
            this.categories = categories ?? new List<ModSettingsCategory>();
            this.options = options ?? new List<ModSettingOption>();
            optionsByKey = new Dictionary<string, ModSettingOption>(
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < this.options.Count; i++)
                optionsByKey[this.options[i].Key] = this.options[i];
        }

        public string Id
        {
            get { return id; }
        }

        public IList<ModSettingsCategory> Categories
        {
            get { return categories.AsReadOnly(); }
        }

        public string DisplayName
        {
            get { return displayName; }
        }

        public bool TryInitialize(out string error)
        {
            return ReloadSavedValues(out error);
        }

        public object CreateDraft()
        {
            string error;
            if (!ReloadSavedValues(out error)) {
                ModLogger.Log("[ModSettings] Failed to reload " +
                    manifestPath + "." + Environment.NewLine + error,
                    Types.LoggingLevels.Error);
            }
            return new Draft(CloneValues(savedValues));
        }

        public ModSettingValue GetValue(object draft, string key)
        {
            Draft candidate = draft as Draft;
            ModSettingValue value;
            if (candidate != null && candidate.Values != null &&
                candidate.Values.TryGetValue(key, out value))
                return value;

            ModSettingOption option;
            return optionsByKey.TryGetValue(key, out option)
                ? option.DefaultValue : null;
        }

        public void SetValue(object draft, string key,
            ModSettingValue value)
        {
            Draft candidate = draft as Draft;
            ModSettingOption option;
            if (candidate == null || candidate.Values == null ||
                !optionsByKey.TryGetValue(key, out option) ||
                !option.IsValueAllowed(value))
                return;
            candidate.Values[key] = value;
        }

        public void ResetCategory(object draft, string categoryId)
        {
            Draft candidate = draft as Draft;
            if (candidate == null || candidate.Values == null)
                return;

            for (int i = 0; i < options.Count; i++) {
                ModSettingOption option = options[i];
                if (string.Equals(option.CategoryId, categoryId,
                    StringComparison.OrdinalIgnoreCase))
                    candidate.Values[option.Key] = option.DefaultValue;
            }
        }

        public bool HasChanges(object draft)
        {
            Draft candidate = draft as Draft;
            if (candidate == null || candidate.Values == null ||
                savedValues == null)
                return false;

            for (int i = 0; i < options.Count; i++) {
                ModSettingOption option = options[i];
                if (!ValuesEqual(GetDictionaryValue(candidate.Values, option),
                    GetDictionaryValue(savedValues, option)))
                    return true;
            }
            return false;
        }

        public bool ApplySetting(object draft, string key,
            out string status, out ModSettingApplyMode applyMode)
        {
            status = string.Empty;
            applyMode = ModSettingApplyMode.Immediate;
            Draft candidate = draft as Draft;
            ModSettingOption option;
            if (candidate == null || candidate.Values == null) {
                status = ModLocalization.Get("LOC_SettingsDraftIsUnavailable");
                return false;
            }
            if (string.IsNullOrEmpty(key) ||
                !optionsByKey.TryGetValue(key, out option)) {
                status = ModLocalization.Get("LOC_TheSelectedSettingIsUnavailable");
                return false;
            }

            applyMode = option.ApplyMode;
            ModSettingValue value = GetDictionaryValue(candidate.Values, option);
            if (ValuesEqual(value, GetDictionaryValue(savedValues, option))) {
                status = ModLocalization.Get("LOC_NoChangesToApply");
                return true;
            }

            Dictionary<string, ModSettingValue> merged =
                CloneValues(savedValues);
            merged[option.Key] = value;
            string error;
            if (!ModSettingsConfigStore.Save(configPath, configSection,
                options, merged, out error)) {
                status = ModLocalization.Get("LOC_FailedToSaveSettings") + error;
                return false;
            }

            savedValues = merged;
            status = ModLocalization.Get("LOC_SettingSaved") +
                ModLocalization.GetApplyModeStatus(applyMode);
            return true;
        }

        public bool Apply(object draft, out string status,
            out ModSettingApplyMode highestApplyMode)
        {
            status = string.Empty;
            highestApplyMode = ModSettingApplyMode.Immediate;
            Draft candidate = draft as Draft;
            if (candidate == null || candidate.Values == null) {
                status = ModLocalization.Get("LOC_SettingsDraftIsUnavailable");
                return false;
            }

            bool changed = false;
            for (int i = 0; i < options.Count; i++) {
                ModSettingOption option = options[i];
                if (ValuesEqual(GetDictionaryValue(candidate.Values, option),
                    GetDictionaryValue(savedValues, option)))
                    continue;
                changed = true;
                if ((int)option.ApplyMode > (int)highestApplyMode)
                    highestApplyMode = option.ApplyMode;
            }

            if (!changed) {
                status = ModLocalization.Get("LOC_NoChangesToApply");
                return true;
            }

            string error;
            if (!ModSettingsConfigStore.Save(configPath, configSection,
                options, candidate.Values, out error)) {
                status = ModLocalization.Get("LOC_FailedToSaveSettings") + error;
                return false;
            }

            savedValues = CloneValues(candidate.Values);
            status = ModLocalization.Get("LOC_SettingsSaved") +
                ModLocalization.GetApplyModeStatus(highestApplyMode);
            return true;
        }

        private bool ReloadSavedValues(out string error)
        {
            Dictionary<string, ModSettingValue> loaded;
            if (!ModSettingsConfigStore.Load(configPath, configSection,
                options, out loaded, out error))
                return false;
            savedValues = loaded;
            return true;
        }

        private static Dictionary<string, ModSettingValue> CloneValues(
            Dictionary<string, ModSettingValue> source)
        {
            Dictionary<string, ModSettingValue> clone =
                new Dictionary<string, ModSettingValue>(
                    StringComparer.OrdinalIgnoreCase);
            if (source == null)
                return clone;
            foreach (KeyValuePair<string, ModSettingValue> pair in source)
                clone[pair.Key] = pair.Value;
            return clone;
        }

        private static ModSettingValue GetDictionaryValue(
            Dictionary<string, ModSettingValue> values,
            ModSettingOption option)
        {
            ModSettingValue value;
            if (values != null && option != null &&
                values.TryGetValue(option.Key, out value) &&
                option.IsValueAllowed(value))
                return value;
            return option != null ? option.DefaultValue : null;
        }

        private static bool ValuesEqual(ModSettingValue left,
            ModSettingValue right)
        {
            if (ReferenceEquals(left, right))
                return true;
            return left != null && left.Equals(right);
        }
    }
}
