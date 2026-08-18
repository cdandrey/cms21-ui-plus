using System;
using System.IO;
using Tomlet;
using UnityEngine;

namespace Cms21UiPlus
{
    /// <summary>User-editable keyboard bindings stored separately from feature switches.</summary>
    public sealed class KeyBindingsSettings
    {
        [Tomlet.Attributes.TomlInlineComment("Primary key for switching mount/unmount modes")]
        public string quickSwitchMountModesPrimary = "LeftAlt";

        [Tomlet.Attributes.TomlInlineComment("Secondary key for switching mount/unmount modes; use None to disable")]
        public string quickSwitchMountModesSecondary = "RightAlt";

        [Tomlet.Attributes.TomlInlineComment("Primary modifier for moving all filtered inventory/warehouse parts")]
        public string filteredWarehouseTransferModifierPrimary = "LeftShift";

        [Tomlet.Attributes.TomlInlineComment("Secondary modifier for moving all filtered inventory/warehouse parts; use None to disable")]
        public string filteredWarehouseTransferModifierSecondary = "RightShift";

        [Tomlet.Attributes.TomlInlineComment("Primary action key for moving all filtered inventory/warehouse parts")]
        public string filteredWarehouseTransferActionPrimary = "Return";

        [Tomlet.Attributes.TomlInlineComment("Secondary action key; use None to disable")]
        public string filteredWarehouseTransferActionSecondary = "KeypadEnter";
    }

    public static class KeyBindingsConfig
    {
        private static readonly KeyBindingsSettings Defaults = new KeyBindingsSettings();

        private static KeyBindingsSettings Settings { get; set; } = new KeyBindingsSettings();
        public static KeyCode QuickSwitchPrimary { get; private set; } = KeyCode.LeftAlt;
        public static KeyCode QuickSwitchSecondary { get; private set; } = KeyCode.RightAlt;
        public static KeyCode FilteredTransferModifierPrimary { get; private set; } = KeyCode.LeftShift;
        public static KeyCode FilteredTransferModifierSecondary { get; private set; } = KeyCode.RightShift;
        public static KeyCode FilteredTransferActionPrimary { get; private set; } = KeyCode.Return;
        public static KeyCode FilteredTransferActionSecondary { get; private set; } = KeyCode.KeypadEnter;

        public static void Load(string filePath)
        {
            Settings = new KeyBindingsSettings();

            try {
                EnsureFileExists(filePath);
                Settings = TomletMain.To<KeyBindingsSettings>(TomlParser.ParseFile(filePath));
                if (Settings == null)
                    Settings = new KeyBindingsSettings();
            } catch (Exception exception) {
                ModLogger.Log("[KeyBindings] Failed to load " + filePath +
                    ". Default bindings will be used." + Environment.NewLine + exception,
                    Types.LoggingLevels.Warning);
                Settings = new KeyBindingsSettings();
            }

            QuickSwitchPrimary = ParseKey(
                Settings.quickSwitchMountModesPrimary,
                KeyCode.LeftAlt,
                nameof(Settings.quickSwitchMountModesPrimary));
            QuickSwitchSecondary = ParseKey(
                Settings.quickSwitchMountModesSecondary,
                KeyCode.RightAlt,
                nameof(Settings.quickSwitchMountModesSecondary));

            FilteredTransferModifierPrimary = ParseKey(
                Settings.filteredWarehouseTransferModifierPrimary,
                KeyCode.LeftShift,
                nameof(Settings.filteredWarehouseTransferModifierPrimary));
            FilteredTransferModifierSecondary = ParseKey(
                Settings.filteredWarehouseTransferModifierSecondary,
                KeyCode.RightShift,
                nameof(Settings.filteredWarehouseTransferModifierSecondary));
            FilteredTransferActionPrimary = ParseKey(
                Settings.filteredWarehouseTransferActionPrimary,
                KeyCode.Return,
                nameof(Settings.filteredWarehouseTransferActionPrimary));
            FilteredTransferActionSecondary = ParseKey(
                Settings.filteredWarehouseTransferActionSecondary,
                KeyCode.KeypadEnter,
                nameof(Settings.filteredWarehouseTransferActionSecondary));
        }

        public static bool IsFilteredTransferModifierPressed()
        {
            return IsKeyPressed(FilteredTransferModifierPrimary) ||
                IsKeyPressed(FilteredTransferModifierSecondary);
        }

        public static bool IsFilteredTransferActionPressed()
        {
            return IsKeyPressedThisFrame(FilteredTransferActionPrimary) ||
                IsKeyPressedThisFrame(FilteredTransferActionSecondary);
        }

        private static bool IsKeyPressed(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKey(key);
        }

        private static bool IsKeyPressedThisFrame(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        private static void EnsureFileExists(string filePath)
        {
            if (File.Exists(filePath))
                return;

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(filePath, TomletMain.TomlStringFrom(Defaults));
        }

        private static KeyCode ParseKey(string value, KeyCode fallback, string settingName)
        {
            KeyCode parsed;
            if (!string.IsNullOrWhiteSpace(value) &&
                Enum.TryParse(value.Trim(), true, out parsed))
                return parsed;

            ModLogger.Log("[KeyBindings] Invalid key '" + value + "' for " +
                settingName + ". Using " + fallback + ".",
                Types.LoggingLevels.Warning);
            return fallback;
        }
    }
}
