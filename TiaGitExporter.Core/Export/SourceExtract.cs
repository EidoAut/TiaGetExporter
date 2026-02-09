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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TiaGitExporter.Core.Model;

namespace TiaGitExporter.Core.Export {
    /// <summary>
    /// Best-effort extraction of human-friendly sources from TIA export XML.
    ///
    /// These outputs are intended for readability and Git diffs.
    /// The exported XML remains the source of truth for reimport.
    ///
    /// Heuristics:
    /// - Structured text (SCL/ST) is often represented as a sequence of &lt;Text&gt; nodes.
    /// - Graphical blocks (LAD/FBD) don't have a canonical text form; a lightweight Markdown summary is generated.
    /// - DBs commonly contain a declaration represented as &lt;Text&gt; nodes; those are extracted into a .db file.
    ///
    /// Notes:
    /// - This is intentionally heuristic and version-tolerant: TIA export XML varies by version and block type.
    /// - Parsing failures are treated as "no source" (no exceptions bubble up).
    /// </summary>
    internal static class SourceExtract {
        /// <summary>
        /// Attempts to extract Structured Text / SCL source from the exported XML.
        /// Returns null when no sufficiently meaningful text can be detected.
        /// </summary>
        public static string TryExtractStructuredText(string xml) {
            if (string.IsNullOrWhiteSpace(xml)) return null;

            try {
                var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

                // Prefer nodes with names hinting Structured Text (version tolerant).
                // We first search for "Structured" / "SCL" / "ST" containers and then collect their <Text> descendants.
                var candidates = doc
                    .Descendants()
                    .Where(e => NameContains(e, "Structured") || NameContains(e, "SCL") || NameContains(e, "ST"))
                    .SelectMany(e => e.Descendants()
                        .Where(x => x.Name.LocalName.Equals("Text", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (!candidates.Any()) {
                    // Fallback: use any <Text> nodes in the document.
                    candidates = doc.Descendants()
                        .Where(e => e.Name.LocalName.Equals("Text", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Normalize line endings and split into logical lines.
                var lines = candidates
                    .Select(t => (t.Value ?? "").Replace("\r\n", "\n").Replace("\r", "\n"))
                    .SelectMany(v => v.Split(new[] { "\n" }, StringSplitOptions.None))
                    .ToList();

                // Graphical exports can contain many tiny tokens. Require a minimal amount of content.
                var cleaned = lines.Select(l => l).ToList();
                while (cleaned.Count > 0 && string.IsNullOrWhiteSpace(cleaned[0])) cleaned.RemoveAt(0);
                while (cleaned.Count > 0 && string.IsNullOrWhiteSpace(cleaned[cleaned.Count - 1])) cleaned.RemoveAt(cleaned.Count - 1);

                if (cleaned.Count < 3) return null;

                var text = string.Join("\r\n", cleaned);
                if (text.Length < 20) return null;

                return text;
            } catch {
                // Parsing failed; treat as no source.
                return null;
            }
        }

        /// <summary>
        /// Attempts to extract a DB-like declaration/source text from exported XML.
        /// Returns null when no meaningful text is found.
        /// </summary>
        public static string TryExtractDbText(string xml) {
            if (string.IsNullOrWhiteSpace(xml)) return null;

            try {
                var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

                // DB declarations commonly appear under elements named Declaration / Interface / DataBlock.
                // We also include "DB" as a broad token due to differing schemas.
                var declarationScopes = doc.Descendants()
                    .Where(e =>
                        NameContains(e, "Declaration") ||
                        NameContains(e, "Interface") ||
                        NameContains(e, "DataBlock") ||
                        NameContains(e, "DB"))
                    .ToList();

                var textNodes = declarationScopes
                    .SelectMany(e => e.Descendants()
                        .Where(x => x.Name.LocalName.Equals("Text", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (!textNodes.Any()) {
                    // Fallback: use all <Text> nodes.
                    textNodes = doc.Descendants()
                        .Where(e => e.Name.LocalName.Equals("Text", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var lines = textNodes
                    .Select(t => (t.Value ?? "").Replace("\r\n", "\n").Replace("\r", "\n"))
                    .SelectMany(v => v.Split(new[] { "\n" }, StringSplitOptions.None))
                    .ToList();

                var cleaned = lines.ToList();
                while (cleaned.Count > 0 && string.IsNullOrWhiteSpace(cleaned[0])) cleaned.RemoveAt(0);
                while (cleaned.Count > 0 && string.IsNullOrWhiteSpace(cleaned[cleaned.Count - 1])) cleaned.RemoveAt(cleaned.Count - 1);

                if (cleaned.Count == 0) return null;

                var text = string.Join("\r\n", cleaned);
                if (text.Length < 10) return null;

                return text;
            } catch {
                // Parsing failed; treat as no source.
                return null;
            }
        }

        /// <summary>
        /// Builds a readable Markdown summary for graphical blocks (LAD/FBD) from export XML.
        /// This is intended for diffs and quick inspection, not recompilation.
        /// </summary>
        public static string BuildGraphicalSummaryMarkdown(string xml, ExportItem item) {
            var sb = new StringBuilder();

            // Header: identify artifact and name.
            sb.AppendLine($"# {item.Kind}: {item.Name}");
            sb.AppendLine();

            // Keep this message human-friendly but explicit about limitations.
            sb.AppendLine("This file is a readable summary generated from TIA's exported XML.");
            sb.AppendLine("It is not a compilable replacement for the original block.");
            sb.AppendLine();

            if (string.IsNullOrWhiteSpace(xml)) {
                sb.AppendLine("(Empty XML)");
                return sb.ToString();
            }

            try {
                var doc = XDocument.Parse(xml, LoadOptions.None);

                // Networks in LAD/FBD often appear as FlgNet / Network.
                var nets = doc.Descendants()
                    .Where(e => e.Name.LocalName.Equals("FlgNet", StringComparison.OrdinalIgnoreCase) ||
                                e.Name.LocalName.Equals("Network", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (nets.Count == 0) {
                    sb.AppendLine("No typical 'Network' elements (FlgNet/Network) were detected in the XML.");
                    return sb.ToString();
                }

                sb.AppendLine($"Networks detected: {nets.Count}");
                sb.AppendLine();

                int i = 0;
                foreach (var n in nets) {
                    i++;
                    sb.AppendLine($"## Network {i}");

                    // Try to list calls (FB/FC calls) by looking for elements whose names contain "Call".
                    // Some schemas use attributes like Name/Block/Inst; we try the simplest stable one.
                    var calls = n.Descendants()
                        .Where(e => e.Name.LocalName.IndexOf("Call", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(e => e.Attribute("Name")?.Value)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (calls.Count > 0) {
                        sb.AppendLine("- Calls: " + string.Join(", ", calls));
                    }

                    // Variables are often represented as Symbol/Access/Operand nodes.
                    // We capture "name-ish" attribute values as a lightweight sample.
                    var vars = n.Descendants()
                        .Where(e => NameContains(e, "Symbol") || NameContains(e, "Access") || NameContains(e, "Operand"))
                        .SelectMany(e => e.Attributes().Select(a => a.Value))
                        .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length <= 80)
                        .Distinct()
                        .Take(20)
                        .ToList();

                    if (vars.Count > 0) {
                        sb.AppendLine("- Vars (sample): " + string.Join(", ", vars));
                    }

                    if (calls.Count == 0 && vars.Count == 0) {
                        sb.AppendLine("- (No detailed summary could be derived for this network.)");
                    }

                    sb.AppendLine();
                }

                return sb.ToString();
            } catch {
                // If parsing fails, return a minimal stub.
                sb.AppendLine("Unable to parse XML to generate a summary.");
                return sb.ToString();
            }
        }

        /// <summary>
        /// Returns true if the element name contains the given token (case-insensitive).
        /// </summary>
        private static bool NameContains(XElement e, string token) {
            return e != null && e.Name.LocalName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
