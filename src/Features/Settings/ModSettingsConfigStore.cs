using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Tomlet;

namespace Cms21UiPlus
{
    internal static class ModSettingsConfigStore
    {
        private static readonly Regex SettingPrefixRegex = new Regex(
            @"^(\s*)([A-Za-z_][A-Za-z0-9_]*)(\s*=\s*)(.*)$",
            RegexOptions.Compiled);
        private static readonly HashSet<string> SessionBackupPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class ParsedSettingLine
        {
            public string Indent;
            public string Key;
            public string Assignment;
            public string Value;
            public string Suffix;
        }

        public static bool Load(string targetPath, string section,
            IList<ModSettingOption> options,
            out Dictionary<string, ModSettingValue> values, out string error)
        {
            values = CreateDefaultValues(options);
            error = string.Empty;

            try {
                if (!File.Exists(targetPath))
                    return true;

                TomlParser.ParseFile(targetPath);
                Encoding encoding;
                string source = ReadTextPreservingEncoding(targetPath,
                    out encoding);
                string normalized = source.Replace("\r\n", "\n")
                    .Replace("\r", "\n");
                string[] lines = normalized.Split('\n');
                string sectionHeader = NormalizeSectionHeader(section);
                bool insideTarget = string.IsNullOrEmpty(sectionHeader);

                for (int i = 0; i < lines.Length; i++) {
                    string trimmed = lines[i].Trim();
                    if (IsSectionHeader(trimmed)) {
                        insideTarget = string.Equals(trimmed, sectionHeader,
                            StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!insideTarget)
                        continue;

                    ParsedSettingLine line;
                    if (!TryParseSettingLine(lines[i], out line))
                        continue;

                    ModSettingOption option = FindOption(options, line.Key);
                    if (option == null)
                        continue;

                    ModSettingValue parsed;
                    if (!TryParseValue(line.Value, option.ValueType,
                        out parsed) || !option.IsValueAllowed(parsed)) {
                        throw new InvalidOperationException(
                            "Setting " + line.Key + " has an invalid " +
                            GetTypeName(option) + " value in " +
                            targetPath + ".");
                    }
                    values[option.Key] = parsed;
                }
                return true;
            } catch (Exception exception) {
                error = exception.Message;
                ModLogger.Log("[ModSettings] Failed to read " + targetPath +
                    "." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                return false;
            }
        }

        public static bool Save(string targetPath, string section,
            IList<ModSettingOption> options,
            IDictionary<string, ModSettingValue> values, out string error)
        {
            error = string.Empty;
            string tempPath = targetPath + ".tmp";
            string backupPath = targetPath + ".bak";

            try {
                string directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                Encoding encoding;
                string source = ReadTextPreservingEncoding(targetPath,
                    out encoding);
                string newLine = source.IndexOf("\r\n",
                    StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
                bool endedWithNewLine = source.EndsWith("\r\n",
                    StringComparison.Ordinal) || source.EndsWith("\n",
                    StringComparison.Ordinal) || source.EndsWith("\r",
                    StringComparison.Ordinal);

                string normalized = source.Replace("\r\n", "\n")
                    .Replace("\r", "\n");
                List<string> lines = new List<string>(
                    normalized.Split('\n'));
                if (source.Length == 0)
                    lines.Clear();
                if (endedWithNewLine && lines.Count > 0 &&
                    lines[lines.Count - 1].Length == 0)
                    lines.RemoveAt(lines.Count - 1);

                Dictionary<string, ModSettingOption> byKey =
                    new Dictionary<string, ModSettingOption>(
                        StringComparer.OrdinalIgnoreCase);
                HashSet<string> written = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < options.Count; i++)
                    byKey[options[i].Key] = options[i];

                string sectionHeader = NormalizeSectionHeader(section);
                bool insideTarget = string.IsNullOrEmpty(sectionHeader);
                bool sectionFound = insideTarget;
                int insertIndex = lines.Count;
                for (int i = 0; i < lines.Count; i++) {
                    string trimmed = lines[i].Trim();
                    if (IsSectionHeader(trimmed)) {
                        if (insideTarget) {
                            insertIndex = i;
                            insideTarget = false;
                        }
                        if (string.Equals(trimmed, sectionHeader,
                            StringComparison.OrdinalIgnoreCase)) {
                            sectionFound = true;
                            insideTarget = true;
                            insertIndex = lines.Count;
                        }
                        continue;
                    }
                    if (!insideTarget)
                        continue;

                    ParsedSettingLine line;
                    if (!TryParseSettingLine(lines[i], out line))
                        continue;

                    ModSettingOption option;
                    if (!byKey.TryGetValue(line.Key, out option))
                        continue;

                    ModSettingValue existing;
                    if (!TryParseValue(line.Value, option.ValueType,
                        out existing) || !option.IsValueAllowed(existing)) {
                        throw new InvalidOperationException(
                            "Setting " + line.Key + " has an invalid " +
                            GetTypeName(option) + " value in " +
                            targetPath + ".");
                    }

                    lines[i] = line.Indent + line.Key + line.Assignment +
                        FormatValue(GetValue(values, option)) + line.Suffix;
                    written.Add(option.Key);
                }

                if (!sectionFound) {
                    if (lines.Count > 0 && lines[lines.Count - 1].Length > 0)
                        lines.Add(string.Empty);
                    if (!string.IsNullOrEmpty(sectionHeader))
                        lines.Add(sectionHeader);
                    insertIndex = lines.Count;
                }

                List<string> missingLines = new List<string>();
                for (int i = 0; i < options.Count; i++) {
                    ModSettingOption option = options[i];
                    if (written.Contains(option.Key))
                        continue;
                    string line = option.Key + " = " +
                        FormatValue(GetValue(values, option));
                    if (!string.IsNullOrWhiteSpace(option.ConfigDescription))
                        line += " # " + option.ConfigDescription;
                    missingLines.Add(line);
                }
                if (missingLines.Count > 0)
                    lines.InsertRange(insertIndex, missingLines);

                string output = string.Join(newLine, lines.ToArray());
                if (endedWithNewLine || output.Length > 0)
                    output += newLine;

                WriteAllText(tempPath, output, encoding);
                TomlParser.ParseFile(tempPath);

                if (File.Exists(targetPath)) {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    try {
                        File.Replace(tempPath, targetPath, backupPath, true);
                    } catch (PlatformNotSupportedException) {
                        ReplaceWithCopy(tempPath, targetPath, backupPath);
                    } catch (IOException) {
                        ReplaceWithCopy(tempPath, targetPath, backupPath);
                    }
                } else {
                    File.Move(tempPath, targetPath);
                }
                if (File.Exists(backupPath))
                    SessionBackupPaths.Add(backupPath);
                return true;
            } catch (Exception exception) {
                error = exception.Message;
                ModLogger.Log("[ModSettings] Failed to save " + targetPath +
                    "." + Environment.NewLine + exception,
                    Types.LoggingLevels.Error);
                try {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                } catch {
                }
                return false;
            }
        }

        public static void DeleteSessionBackups()
        {
            if (SessionBackupPaths.Count == 0)
                return;

            string[] paths = new List<string>(SessionBackupPaths).ToArray();
            for (int i = 0; i < paths.Length; i++) {
                string path = paths[i];
                try {
                    if (File.Exists(path))
                        File.Delete(path);
                } catch (Exception exception) {
                    ModLogger.Log("[ModSettings] Failed to delete backup " +
                        path + "." + Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                }
            }
            SessionBackupPaths.Clear();
        }

        private static Dictionary<string, ModSettingValue> CreateDefaultValues(
            IList<ModSettingOption> options)
        {
            Dictionary<string, ModSettingValue> values =
                new Dictionary<string, ModSettingValue>(
                    StringComparer.OrdinalIgnoreCase);
            if (options == null)
                return values;
            for (int i = 0; i < options.Count; i++)
                values[options[i].Key] = options[i].DefaultValue;
            return values;
        }

        private static ModSettingValue GetValue(
            IDictionary<string, ModSettingValue> values,
            ModSettingOption option)
        {
            ModSettingValue value;
            if (values != null && option != null &&
                values.TryGetValue(option.Key, out value) &&
                option.IsValueAllowed(value))
                return value;
            return option != null ? option.DefaultValue : null;
        }

        private static ModSettingOption FindOption(
            IList<ModSettingOption> options, string key)
        {
            if (options == null || string.IsNullOrEmpty(key))
                return null;
            for (int i = 0; i < options.Count; i++) {
                if (string.Equals(options[i].Key, key,
                    StringComparison.OrdinalIgnoreCase))
                    return options[i];
            }
            return null;
        }

        private static bool TryParseSettingLine(string source,
            out ParsedSettingLine result)
        {
            result = null;
            Match match = SettingPrefixRegex.Match(source ?? string.Empty);
            if (!match.Success)
                return false;

            string remainder = match.Groups[4].Value;
            int commentIndex = FindCommentIndex(remainder);
            string valueAndSpacing = commentIndex >= 0
                ? remainder.Substring(0, commentIndex) : remainder;
            int valueEnd = valueAndSpacing.Length;
            while (valueEnd > 0 && char.IsWhiteSpace(
                valueAndSpacing[valueEnd - 1]))
                valueEnd--;

            string value = valueAndSpacing.Substring(0, valueEnd).TrimStart();
            if (value.Length == 0)
                return false;

            string suffix = valueAndSpacing.Substring(valueEnd);
            if (commentIndex >= 0)
                suffix += remainder.Substring(commentIndex);

            result = new ParsedSettingLine {
                Indent = match.Groups[1].Value,
                Key = match.Groups[2].Value,
                Assignment = match.Groups[3].Value,
                Value = value,
                Suffix = suffix,
            };
            return true;
        }

        private static int FindCommentIndex(string source)
        {
            bool inDouble = false;
            bool inSingle = false;
            bool escaped = false;
            for (int i = 0; i < source.Length; i++) {
                char current = source[i];
                if (inDouble) {
                    if (escaped) {
                        escaped = false;
                        continue;
                    }
                    if (current == '\\') {
                        escaped = true;
                        continue;
                    }
                    if (current == '"')
                        inDouble = false;
                    continue;
                }
                if (inSingle) {
                    if (current == '\'')
                        inSingle = false;
                    continue;
                }
                if (current == '"')
                    inDouble = true;
                else if (current == '\'')
                    inSingle = true;
                else if (current == '#')
                    return i;
            }
            return -1;
        }

        private static bool TryParseValue(string raw,
            ModSettingValueType type, out ModSettingValue value)
        {
            value = null;
            if (type == ModSettingValueType.Boolean) {
                bool parsed;
                if (!bool.TryParse(raw, out parsed))
                    return false;
                value = ModSettingValue.FromBoolean(parsed);
                return true;
            }
            if (type == ModSettingValueType.Number) {
                double parsed;
                string normalized = raw.Replace("_", string.Empty);
                if (!double.TryParse(normalized, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out parsed) ||
                    double.IsNaN(parsed) || double.IsInfinity(parsed))
                    return false;
                value = ModSettingValue.FromNumber(parsed);
                return true;
            }

            string parsedString;
            if (!TryParseString(raw, out parsedString))
                return false;
            value = ModSettingValue.FromString(parsedString);
            return true;
        }

        private static bool TryParseString(string raw, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(raw) || raw.Length < 2)
                return false;
            char quote = raw[0];
            if ((quote != '"' && quote != '\'') ||
                raw[raw.Length - 1] != quote)
                return false;
            string content = raw.Substring(1, raw.Length - 2);
            if (quote == '\'') {
                value = content;
                return true;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < content.Length; i++) {
                char current = content[i];
                if (current != '\\') {
                    builder.Append(current);
                    continue;
                }
                if (++i >= content.Length)
                    return false;
                char escaped = content[i];
                switch (escaped) {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case 'b': builder.Append('\b'); break;
                    case 't': builder.Append('\t'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'r': builder.Append('\r'); break;
                    default: return false;
                }
            }
            value = builder.ToString();
            return true;
        }

        private static string FormatValue(ModSettingValue value)
        {
            if (value == null)
                return "\"\"";
            if (value.Type == ModSettingValueType.Boolean)
                return value.BooleanValue ? "true" : "false";
            if (value.Type == ModSettingValueType.Number)
                return value.NumberValue.ToString("G15",
                    CultureInfo.InvariantCulture);
            return "\"" + EscapeString(value.StringValue) + "\"";
        }

        private static string EscapeString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\b", "\\b")
                .Replace("\t", "\\t")
                .Replace("\n", "\\n")
                .Replace("\f", "\\f")
                .Replace("\r", "\\r");
        }

        private static string GetTypeName(ModSettingOption option)
        {
            if (option == null)
                return "setting";
            if (option.Type == ModSettingType.Boolean)
                return "boolean";
            if (option.Type == ModSettingType.Number)
                return "number";
            if (option.Type == ModSettingType.String)
                return "string";
            return "enum";
        }

        private static string NormalizeSectionHeader(string section)
        {
            if (string.IsNullOrWhiteSpace(section))
                return string.Empty;
            string trimmed = section.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
                trimmed.EndsWith("]", StringComparison.Ordinal))
                return trimmed;
            return "[" + trimmed + "]";
        }

        private static bool IsSectionHeader(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                value.StartsWith("[", StringComparison.Ordinal) &&
                value.EndsWith("]", StringComparison.Ordinal);
        }

        private static string ReadTextPreservingEncoding(string path,
            out Encoding encoding)
        {
            encoding = new UTF8Encoding(false);
            if (!File.Exists(path))
                return string.Empty;

            byte[] bytes = File.ReadAllBytes(path);
            int offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF &&
                bytes[1] == 0xBB && bytes[2] == 0xBF) {
                encoding = new UTF8Encoding(true);
                offset = 3;
            } else if (bytes.Length >= 2 && bytes[0] == 0xFF &&
                bytes[1] == 0xFE) {
                encoding = new UnicodeEncoding(false, true);
                offset = 2;
            } else if (bytes.Length >= 2 && bytes[0] == 0xFE &&
                bytes[1] == 0xFF) {
                encoding = new UnicodeEncoding(true, true);
                offset = 2;
            }
            return encoding.GetString(bytes, offset, bytes.Length - offset);
        }

        private static void WriteAllText(string path, string value,
            Encoding encoding)
        {
            byte[] preamble = encoding.GetPreamble();
            byte[] body = encoding.GetBytes(value);
            using (FileStream stream = new FileStream(path, FileMode.Create,
                FileAccess.Write, FileShare.None)) {
                if (preamble != null && preamble.Length > 0)
                    stream.Write(preamble, 0, preamble.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush(true);
            }
        }

        private static void ReplaceWithCopy(string tempPath,
            string targetPath, string backupPath)
        {
            File.Copy(targetPath, backupPath, true);
            File.Copy(tempPath, targetPath, true);
            File.Delete(tempPath);
        }
    }
}
