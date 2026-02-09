/*
 * -------------------------------------------------------------------------
 *  TiaGitExporter
 * -------------------------------------------------------------------------
 *  Copyright (c) 2026 Eido Automation
 *  Version: v0.1
 *  License: MIT License
 *
 *  Description:
 *  TiaGitExporter is a tool designed to export and import Siemens TIA Portal
 *  PLC artifacts (UDTs, Blocks, Tag Tables, etc.) into a Git-friendly folder
 *  structure using the TIA Portal Openness API.
 *
 *  The application enables version control, change tracking, and automated
 *  backup of PLC software projects by converting TIA objects into structured
 *  XML files suitable for source control systems.
 *
 *  Developed by: Eido Automation
 * -------------------------------------------------------------------------
 */

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TiaGitExporter.Core.Model;

namespace TiaGitExporter.Core.Utilities {
    /// <summary>
    /// Helper methods for building deterministic and filesystem-safe paths.
    ///
    /// Goals:
    /// - Deterministic folder and file naming (stable across runs)
    /// - Safe output on Windows (reserved names, invalid chars, trailing dots/spaces)
    /// - Preserve a structure that mirrors the user's view in TIA (CPU -> category -> groups)
    /// </summary>
    public static class PathHelper {
        /// <summary>
        /// Windows reserved device names (case-insensitive).
        /// These are invalid as file/folder names even when an extension is present.
        /// </summary>
        private static readonly string[] ReservedNames = new[]
        {
            "CON","PRN","AUX","NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        };

        /// <summary>
        /// Regex used to remove trailing whitespace/dots which Windows may normalize away.
        /// </summary>
        private static readonly Regex TrailingDotsOrSpaces =
            new Regex(@"[\s\.]+$", RegexOptions.Compiled);

        /// <summary>
        /// Makes a string safe for filesystem usage.
        ///
        /// Protections:
        /// - Replaces invalid filename characters with '_'
        /// - Trims leading/trailing whitespace
        /// - Removes trailing dots/spaces (Windows collapses/normalizes them)
        /// - Blocks path traversal tokens '.' and '..'
        /// - Avoids Windows reserved device names (CON, PRN, COM1, ...)
        /// </summary>
        /// <param name="name">Input name to sanitize.</param>
        /// <returns>A filesystem-safe string (never null/empty).</returns>
        public static string Sanitize(string name) {
            if (string.IsNullOrWhiteSpace(name))
                return "_";

            // Replace invalid filename characters.
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);

            foreach (var ch in name)
                sb.Append(invalid.Contains(ch) ? '_' : ch);

            // Trim and remove trailing dots/spaces (Windows quirk).
            var s = sb.ToString().Trim();
            s = TrailingDotsOrSpaces.Replace(s, "");
            if (string.IsNullOrWhiteSpace(s)) s = "_";

            // Block traversal tokens.
            if (s == "." || s == "..") s = "_";

            // Reserved names are invalid even with extensions (e.g. "CON.txt").
            var baseName = Path.GetFileNameWithoutExtension(s);
            if (ReservedNames.Any(r => string.Equals(r, baseName, StringComparison.OrdinalIgnoreCase)))
                s = "_" + s;

            return s;
        }

        /// <summary>
        /// Returns a deterministic relative folder for an export item.
        ///
        /// Layout:
        /// &lt;CPU&gt;/&lt;Blocks|Types|TagTables&gt;/&lt;GroupPath&gt;
        ///
        /// Example:
        /// "Device1/CPU_1516/Blocks/Drives/Motion"
        /// </summary>
        public static string GetRelativeFolder(ExportItem item) {
            if (item == null) throw new ArgumentNullException(nameof(item));

            // CPU path is user-facing and may contain separators or characters that are not path-safe.
            // Normalize to forward slashes first, then sanitize each segment via NormalizeGroupPath.
            var cpu = NormalizeGroupPath(item.CpuPath.Replace("\\", "/").Replace(":", "_"));

            // High-level category folder depends on the artifact kind.
            string sub;
            switch (item.Kind) {
                case ExportKind.Udt:
                    sub = "Types";
                    break;

                case ExportKind.PlcTagTable:
                    sub = "TagTables";
                    break;

                case ExportKind.Fb:
                case ExportKind.Fc:
                case ExportKind.Ob:
                case ExportKind.Db:
                    sub = "Blocks";
                    break;

                default:
                    sub = "Other";
                    break;
            }

            // GroupPath comes from the PLC hierarchy and is treated as a sequence of folders.
            var group = NormalizeGroupPath(item.GroupPath);

            // Path.Combine will use OS separators; this is fine since the repo is stored on disk.
            return Path.Combine(cpu, sub, group);
        }

        /// <summary>
        /// Returns a deterministic filename for an export item.
        ///
        /// Strategy:
        /// - Use a sanitized version of the item name for readability
        /// - Append a short hash disambiguator to avoid collisions after sanitization
        /// - Always use ".xml" because TIA exports are XML
        ///
        /// Example:
        /// "FB10__a1b2c3d4.xml"
        /// </summary>
        public static string GetFileName(ExportItem item) {
            if (item == null) throw new ArgumentNullException(nameof(item));

            // Keep file names stable and readable.
            // Ensure deterministic uniqueness in case two names sanitize to the same value.
            var safe = Sanitize(item.Name);

            var disambiguator = HashHelper
                .Sha256Hex(item.CpuPath + "|" + item.Kind + "|" + item.GroupPath + "|" + item.Name)
                .Substring(0, 8);

            return $"{safe}__{disambiguator}.xml";
        }

        /// <summary>
        /// Returns the full output file path for an export item.
        /// This is the canonical location used by both Export and Import.
        /// </summary>
        public static string GetOutputFilePath(string repoRoot, ExportItem item) {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            if (item == null) throw new ArgumentNullException(nameof(item));

            var folder = Path.Combine(repoRoot, GetRelativeFolder(item));
            var file = GetFileName(item);
            return Path.Combine(folder, file);
        }

        /// <summary>
        /// Normalizes a user/group path into OS-specific separators and safe segments.
        /// </summary>
        /// <param name="groupPath">Path using either '\' or '/' separators.</param>
        /// <returns>A sanitized path using the current OS separator.</returns>
        private static string NormalizeGroupPath(string groupPath) {
            if (string.IsNullOrWhiteSpace(groupPath))
                return "";

            // Split into segments and sanitize each one individually.
            var parts = groupPath
                .Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Sanitize);

            return Path.Combine(parts.ToArray());
        }
    }
}