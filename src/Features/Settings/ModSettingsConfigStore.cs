using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Tomlet;

namespace Cms21UiPlus
{
    internal static class ModSettingsConfigStore
    {
        private static readonly Regex SettingLineRegex = new Regex(
            @"^(\s*)([A-Za-z_][A-Za-z0-9_]*)(\s*=\s*)(true|false)(.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AnySettingLineRegex = new Regex(
            @"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=",
            RegexOptions.Compiled);
        private static readonly HashSet<string> SessionBackupPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool Load(string targetPath, string section,
            IList<ModSettingOption> options,
            out Dictionary<string, bool> values, out string error)
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

                    Match match = SettingLineRegex.Match(lines[i]);
                    if (match.Success) {
                        string key = match.Groups[2].Value;
                        if (!values.ContainsKey(key))
                            continue;
                        bool parsed;
                        if (!bool.TryParse(match.Groups[4].Value, out parsed))
                            continue;
                        values[key] = parsed;
                        continue;
                    }

                    Match anySetting = AnySettingLineRegex.Match(lines[i]);
                    if (anySetting.Success &&
                        values.ContainsKey(anySetting.Groups[1].Value)) {
                        throw new InvalidOperationException(
                            "Setting " + anySetting.Groups[1].Value +
                            " is not a boolean value in " + targetPath + ".");
                    }
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
            IDictionary<string, bool> values, out string error)
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

                    Match match = SettingLineRegex.Match(lines[i]);
                    if (match.Success) {
                        string key = match.Groups[2].Value;
                        ModSettingOption option;
                        if (!byKey.TryGetValue(key, out option))
                            continue;
                        lines[i] = match.Groups[1].Value + key +
                            match.Groups[3].Value +
                            (GetValue(values, option) ? "true" : "false") +
                            match.Groups[5].Value;
                        written.Add(key);
                        continue;
                    }

                    Match anySetting = AnySettingLineRegex.Match(lines[i]);
                    if (anySetting.Success &&
                        byKey.ContainsKey(anySetting.Groups[1].Value)) {
                        throw new InvalidOperationException(
                            "Setting " + anySetting.Groups[1].Value +
                            " is not a boolean value in " + targetPath + ".");
                    }
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
                        (GetValue(values, option) ? "true" : "false");
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
                    SessionBackupPaths.Remove(path);
                } catch (Exception exception) {
                    ModLogger.Log("[ModSettings] Failed to remove backup " +
                        path + "." + Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                }
            }
        }

        private static Dictionary<string, bool> CreateDefaultValues(
            IList<ModSettingOption> options)
        {
            Dictionary<string, bool> values = new Dictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);
            if (options == null)
                return values;
            for (int i = 0; i < options.Count; i++)
                values[options[i].Key] = options[i].DefaultValue;
            return values;
        }

        private static bool GetValue(IDictionary<string, bool> values,
            ModSettingOption option)
        {
            bool value;
            if (values != null && option != null &&
                values.TryGetValue(option.Key, out value))
                return value;
            return option != null && option.DefaultValue;
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
