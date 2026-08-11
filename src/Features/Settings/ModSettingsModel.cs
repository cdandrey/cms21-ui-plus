using System;
using System.Collections.Generic;
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

    internal sealed class ModSettingOption
    {
        public ModSettingOption(string key, string categoryId, bool defaultValue,
            string name, string description, string configDescription,
            ModSettingApplyMode applyMode)
        {
            Key = key;
            CategoryId = categoryId;
            Name = name;
            DefaultValue = defaultValue;
            Description = description;
            ConfigDescription = configDescription;
            ApplyMode = applyMode;
        }

        public string Key { get; private set; }
        public string CategoryId { get; private set; }
        public bool DefaultValue { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string ConfigDescription { get; private set; }
        public ModSettingApplyMode ApplyMode { get; private set; }

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
        bool GetValue(object draft, string key);
        void SetValue(object draft, string key, bool value);
        void ResetCategory(object draft, string categoryId);
        bool HasChanges(object draft);
        bool ApplySetting(object draft, string key, out string status,
            out ModSettingApplyMode applyMode);
        bool Apply(object draft, out string status,
            out ModSettingApplyMode highestApplyMode);
    }

}
