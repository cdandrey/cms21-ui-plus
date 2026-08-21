using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Cms21UiPlus
{
    internal sealed class ModLocalizationCatalog
    {
        private readonly Dictionary<string, string> englishValues;
        private readonly Dictionary<string, string> russianValues;
        private readonly Dictionary<string, string> activeValues;

        private ModLocalizationCatalog(Dictionary<string, string> english,
            Dictionary<string, string> russian)
        {
            englishValues = english ?? NewMap();
            russianValues = russian ?? NewMap();
            activeValues = ModLocalization.IsRussian
                ? russianValues : englishValues;
        }

        private const string EnglishResourceName =
            "Cms21UiPlus.Localization.en.json";
        private const string RussianResourceName =
            "Cms21UiPlus.Localization.ru.json";

        public static ModLocalizationCatalog LoadEmbedded()
        {
            return new ModLocalizationCatalog(
                ReadEmbeddedMap(EnglishResourceName),
                ReadEmbeddedMap(RussianResourceName));
        }

        private static Dictionary<string, string> ReadEmbeddedMap(
            string resourceName)
        {
            using (Stream stream = typeof(ModLocalizationCatalog).Assembly
                .GetManifestResourceStream(resourceName)) {
                if (stream == null)
                    throw new InvalidOperationException(
                        "Embedded localization resource was not found: " +
                        resourceName);

                using (StreamReader reader = new StreamReader(stream,
                    Encoding.UTF8, true)) {
                    Dictionary<string, string> values;
                    string error;
                    if (!LocalizationJsonReader.TryReadStringMap(
                        reader.ReadToEnd(), out values, out error))
                        throw new InvalidOperationException(
                            "Embedded localization resource could not be read: " +
                            resourceName + ": " + error);
                    return values;
                }
            }
        }

        public static bool TryReadManifest(string json,
            out ModLocalizationCatalog catalog, out string error)
        {
            catalog = new ModLocalizationCatalog(NewMap(), NewMap());
            error = string.Empty;
            if (string.IsNullOrEmpty(json) || json.IndexOf(
                "\"localization\"", StringComparison.Ordinal) < 0)
                return true;

            Dictionary<string, string> english;
            Dictionary<string, string> russian;
            if (!LocalizationJsonReader.TryReadLocalization(json,
                out english, out russian, out error))
                return false;
            catalog = new ModLocalizationCatalog(english, russian);
            return true;
        }

        public string Get(string key, string fallback)
        {
            string value;
            if (string.IsNullOrEmpty(key))
                return fallback;
            return activeValues.TryGetValue(key, out value)
                ? value : fallback;
        }

        public string GetEnglish(string key, string fallback)
        {
            string value;
            return !string.IsNullOrEmpty(key) &&
                englishValues.TryGetValue(key, out value) ? value : fallback;
        }

        public bool HasBoth(string key)
        {
            string english;
            string russian;
            return !string.IsNullOrWhiteSpace(key) &&
                englishValues.TryGetValue(key, out english) &&
                russianValues.TryGetValue(key, out russian) &&
                !string.IsNullOrWhiteSpace(english) &&
                !string.IsNullOrWhiteSpace(russian);
        }

        private static Dictionary<string, string> NewMap()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

    }

    internal static class LocalizationJsonReader
    {
        public static bool TryReadLocalization(string json,
            out Dictionary<string, string> english,
            out Dictionary<string, string> russian, out string error)
        {
            english = null;
            russian = null;
            error = string.Empty;
            int localizationStart;
            int localizationEnd;
            if (!TryFindObjectProperty(json, 0, "localization",
                out localizationStart, out localizationEnd, out error))
                return false;
            int englishStart;
            int englishEnd;
            if (!TryFindObjectProperty(json, localizationStart, "en",
                out englishStart, out englishEnd, out error))
                return false;
            int russianStart;
            int russianEnd;
            if (!TryFindObjectProperty(json, localizationStart, "ru",
                out russianStart, out russianEnd, out error))
                return false;
            if (!TryReadStringMap(json.Substring(englishStart,
                englishEnd - englishStart), out english, out error))
                return false;
            return TryReadStringMap(json.Substring(russianStart,
                russianEnd - russianStart), out russian, out error);
        }

        public static bool TryReadStringMap(string json,
            out Dictionary<string, string> result, out string error)
        {
            result = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;
            int index = 0;
            SkipWhitespace(json, ref index);
            if (!Consume(json, ref index, '{')) {
                error = "Expected a JSON object.";
                return false;
            }
            SkipWhitespace(json, ref index);
            while (index < json.Length && json[index] != '}') {
                string key;
                if (!TryReadString(json, ref index, out key, out error))
                    return false;
                SkipWhitespace(json, ref index);
                if (!Consume(json, ref index, ':')) {
                    error = "Expected ':' after localization key.";
                    return false;
                }
                SkipWhitespace(json, ref index);
                string value;
                if (!TryReadString(json, ref index, out value, out error))
                    return false;
                result[key] = value;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') {
                    index++;
                    SkipWhitespace(json, ref index);
                } else {
                    break;
                }
            }
            if (!Consume(json, ref index, '}')) {
                error = "Expected the end of a localization object.";
                return false;
            }
            return true;
        }

        private static bool TryFindObjectProperty(string json,
            int objectStart, string propertyName, out int valueStart,
            out int valueEnd, out string error)
        {
            valueStart = 0;
            valueEnd = 0;
            error = string.Empty;
            int index = objectStart;
            SkipWhitespace(json, ref index);
            if (!Consume(json, ref index, '{')) {
                error = "Expected an object containing '" + propertyName +
                    "'.";
                return false;
            }
            SkipWhitespace(json, ref index);
            while (index < json.Length && json[index] != '}') {
                string key;
                if (!TryReadString(json, ref index, out key, out error))
                    return false;
                SkipWhitespace(json, ref index);
                if (!Consume(json, ref index, ':')) {
                    error = "Expected ':' after '" + key + "'.";
                    return false;
                }
                SkipWhitespace(json, ref index);
                int start = index;
                if (!TrySkipValue(json, ref index, out error))
                    return false;
                if (string.Equals(key, propertyName,
                    StringComparison.Ordinal)) {
                    if (start >= json.Length || json[start] != '{') {
                        error = "Property '" + propertyName +
                            "' must be an object.";
                        return false;
                    }
                    valueStart = start;
                    valueEnd = index;
                    return true;
                }
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') {
                    index++;
                    SkipWhitespace(json, ref index);
                } else {
                    break;
                }
            }
            error = "Required object '" + propertyName +
                "' was not found.";
            return false;
        }

        private static bool TrySkipValue(string json, ref int index,
            out string error)
        {
            error = string.Empty;
            SkipWhitespace(json, ref index);
            if (index >= json.Length) {
                error = "Unexpected end of JSON.";
                return false;
            }
            char current = json[index];
            if (current == '"') {
                string ignored;
                return TryReadString(json, ref index, out ignored, out error);
            }
            if (current == '{' || current == '[') {
                char open = current;
                char close = open == '{' ? '}' : ']';
                int depth = 0;
                while (index < json.Length) {
                    current = json[index];
                    if (current == '"') {
                        string ignored;
                        if (!TryReadString(json, ref index, out ignored,
                            out error))
                            return false;
                        continue;
                    }
                    index++;
                    if (current == open)
                        depth++;
                    else if (current == close && --depth == 0)
                        return true;
                }
                error = "Unterminated JSON container.";
                return false;
            }
            while (index < json.Length && json[index] != ',' &&
                json[index] != '}' && json[index] != ']')
                index++;
            return true;
        }

        private static bool TryReadString(string json, ref int index,
            out string value, out string error)
        {
            value = null;
            error = string.Empty;
            SkipWhitespace(json, ref index);
            if (!Consume(json, ref index, '"')) {
                error = "Expected a JSON string.";
                return false;
            }
            StringBuilder builder = new StringBuilder();
            while (index < json.Length) {
                char current = json[index++];
                if (current == '"') {
                    value = builder.ToString();
                    return true;
                }
                if (current != '\\') {
                    builder.Append(current);
                    continue;
                }
                if (index >= json.Length) {
                    error = "Invalid JSON escape sequence.";
                    return false;
                }
                char escaped = json[index++];
                switch (escaped) {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) {
                            error = "Invalid Unicode escape sequence.";
                            return false;
                        }
                        int code;
                        if (!int.TryParse(json.Substring(index, 4),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out code)) {
                            error = "Invalid Unicode escape sequence.";
                            return false;
                        }
                        builder.Append((char)code);
                        index += 4;
                        break;
                    default:
                        error = "Unsupported JSON escape sequence.";
                        return false;
                }
            }
            error = "Unterminated JSON string.";
            return false;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
        }

        private static bool Consume(string json, ref int index,
            char expected)
        {
            if (index >= json.Length || json[index] != expected)
                return false;
            index++;
            return true;
        }
    }

    internal static class ModLocalization
    {
        private static ModLocalizationCatalog catalog;
        private static bool? isRussian;

        public static bool IsRussian
        {
            get {
                if (!isRussian.HasValue)
                    isRussian = DetectRussianLanguage();
                return isRussian.Value;
            }
        }

        internal static ModLocalizationCatalog BuiltInCatalog
        {
            get {
                if (catalog == null)
                    catalog = ModLocalizationCatalog.LoadEmbedded();
                return catalog;
            }
        }

        private static bool DetectRussianLanguage()
        {
            try {
                string language = GameSettings.LanguageSettings;
                if (!string.IsNullOrWhiteSpace(language))
                    return IsRussianLanguageName(language);
            } catch {
            }
            return Application.systemLanguage == SystemLanguage.Russian;
        }

        private static bool IsRussianLanguageName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            return value.IndexOf("russian",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("рус",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(value, "ru",
                    StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("ru-",
                    StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("ru_",
                    StringComparison.OrdinalIgnoreCase);
        }

        public static string Get(string key)
        {
            return BuiltInCatalog.Get(key, key);
        }

        public static string FormatCount(int count, string oneKey,
            string fewKey, string manyKey)
        {
            string key = manyKey;
            if (IsRussian) {
                int lastTwo = count % 100;
                int last = count % 10;
                if (lastTwo < 11 || lastTwo > 14) {
                    if (last == 1)
                        key = oneKey;
                    else if (last >= 2 && last <= 4)
                        key = fewKey;
                }
            } else if (count == 1) {
                key = oneKey;
            }
            return string.Format(Get(key), count);
        }

        public static void Reset()
        {
            catalog = null;
            isRussian = null;
        }

        internal static void SetGameLanguage(string language)
        {
            catalog = null;
            isRussian = string.IsNullOrWhiteSpace(language)
                ? (bool?)null
                : IsRussianLanguageName(language);
        }

        public static string GetApplyModeText(ModSettingApplyMode mode)
        {
            switch (mode) {
                case ModSettingApplyMode.ReopenWindow:
                    return Get("LOC_ApplyModeReopenWindow");
                case ModSettingApplyMode.ReloadLocation:
                    return Get("LOC_ApplyModeReloadLocation");
                case ModSettingApplyMode.RestartGame:
                    return Get("LOC_ApplyModeRestartGame");
                default:
                    return Get("LOC_ApplyModeImmediate");
            }
        }

        public static string GetApplyModeStatus(ModSettingApplyMode mode)
        {
            switch (mode) {
                case ModSettingApplyMode.ReopenWindow:
                    return Get("LOC_ApplyStatusReopenWindow");
                case ModSettingApplyMode.ReloadLocation:
                    return Get("LOC_ApplyStatusReloadLocation");
                case ModSettingApplyMode.RestartGame:
                    return Get("LOC_ApplyStatusRestartGame");
                default:
                    return Get("LOC_ApplyStatusImmediate");
            }
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.SetLanguage))]
    internal static class ModLocalizationLanguagePatch
    {
        [HarmonyPostfix]
        private static void Postfix(string __0)
        {
            ModLocalization.SetGameLanguage(__0);
            ModSettingsMenuFeature.OnLanguageChanged();
        }
    }
}
