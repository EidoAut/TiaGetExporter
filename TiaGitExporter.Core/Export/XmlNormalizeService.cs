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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TiaGitExporter.Core.Export {
    /// <summary>
    /// Normalizes XML output to be Git-friendly and stable:
    /// - UTF-8 (no BOM)
    /// - LF line endings
    /// - consistent indentation
    /// - stable attribute ordering (by namespace + local name)
    /// - optional scrubbing of volatile fields (regex-based)
    ///
    /// Notes:
    /// - Elements are NOT reordered because element order can be semantically meaningful for TIA exports.
    /// - Scrubbing is intentionally opt-in/configurable to avoid accidentally removing meaningful data.
    /// </summary>
    public sealed class XmlNormalizeService {
        /// <summary>
        /// Normalization options controlling output formatting and optional scrubbing.
        /// </summary>
        public sealed class Options {
            /// <summary>
            /// If true, omits the XML declaration (e.g. &lt;?xml version="1.0" encoding="utf-8"?&gt;).
            /// </summary>
            public bool OmitXmlDeclaration { get; set; } = false;

            /// <summary>
            /// If true, sorts non-namespace attributes deterministically.
            /// Namespace declarations are preserved as-is for readability and familiarity.
            /// </summary>
            public bool SortAttributes { get; set; } = true;

            /// <summary>
            /// Optional list of regex replacements applied after formatting.
            /// Use this to remove volatile fields (timestamps/guids/build IDs) if needed.
            /// </summary>
            public List<RegexReplacement> Scrubbers { get; } = new List<RegexReplacement>();
        }

        /// <summary>
        /// Defines a regex-based replacement used by <see cref="Options.Scrubbers"/>.
        /// </summary>
        public sealed class RegexReplacement {
            /// <summary>
            /// Regex pattern to match. If null/empty, the scrubber is ignored.
            /// </summary>
            public string Pattern { get; set; }

            /// <summary>
            /// Replacement text (defaults to empty string, i.e. remove matches).
            /// </summary>
            public string Replacement { get; set; } = "";

            /// <summary>
            /// Builds a compiled, culture-invariant regex.
            /// </summary>
            public Regex ToRegex() =>
                new Regex(Pattern ?? "", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        private readonly Options _options;

        /// <summary>
        /// Creates a normalizer with default options.
        /// </summary>
        public XmlNormalizeService() : this(new Options()) { }

        /// <summary>
        /// Creates a normalizer with custom options.
        /// </summary>
        public XmlNormalizeService(Options options) {
            _options = options ?? new Options();
        }

        /// <summary>
        /// Normalizes the provided XML string according to the configured options.
        /// </summary>
        public string Normalize(string xmlContent) {
            if (xmlContent == null) throw new ArgumentNullException(nameof(xmlContent));

            // Secure-ish parser settings: prohibit DTDs and disable external entity resolution.
            var readerSettings = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,

                // NOTE: We ignore whitespace so that output is controlled solely by XmlWriter formatting.
                IgnoreWhitespace = true
            };

            XDocument doc;
            using (var sr = new StringReader(xmlContent))
            using (var xr = XmlReader.Create(sr, readerSettings)) {
                doc = XDocument.Load(xr, LoadOptions.None);
            }

            // Stable attribute ordering improves diffs when TIA changes attribute order between exports.
            if (_options.SortAttributes && doc.Root != null)
                SortAttributesRecursively(doc.Root);

            // Output settings: UTF-8 no BOM, LF newlines, 2-space indent for clean diffs.
            var writerSettings = new XmlWriterSettings {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = _options.OmitXmlDeclaration
            };

            string formatted;
            using (var sw = new Utf8StringWriter(writerSettings.Encoding))
            using (var xw = XmlWriter.Create(sw, writerSettings)) {
                doc.Save(xw);
                xw.Flush();
                formatted = sw.ToString();
            }

            // Optional post-format scrubbers (best-effort).
            if (_options.Scrubbers.Count > 0) {
                foreach (var s in _options.Scrubbers) {
                    if (string.IsNullOrWhiteSpace(s?.Pattern)) continue;
                    formatted = s.ToRegex().Replace(formatted, s.Replacement ?? "");
                }
            }

            return formatted;
        }

        /// <summary>
        /// Reads an XML file (UTF-8), normalizes it, and writes it back in place (UTF-8 no BOM).
        /// </summary>
        public void NormalizeFileInPlace(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            var raw = File.ReadAllText(filePath, Encoding.UTF8);
            var norm = Normalize(raw);

            // Ensure stable encoding on disk (no BOM).
            File.WriteAllText(filePath, norm, new UTF8Encoding(false));
        }

        /// <summary>
        /// Sorts attributes recursively for deterministic output:
        /// - Namespace declarations are preserved (kept at the front).
        /// - Other attributes are sorted by namespace URI then local name (ordinal).
        /// </summary>
        private static void SortAttributesRecursively(XElement element) {
            // Preserve namespace declarations (order is not strictly semantic, but keeping them as-is helps readability).
            var attrs = element.Attributes().ToList();
            var nsDecls = attrs.Where(a => a.IsNamespaceDeclaration).ToList();

            var normal = attrs.Where(a => !a.IsNamespaceDeclaration)
                .OrderBy(a => a.Name.NamespaceName, StringComparer.Ordinal)
                .ThenBy(a => a.Name.LocalName, StringComparer.Ordinal)
                .ToList();

            element.ReplaceAttributes(nsDecls.Concat(normal));

            foreach (var child in element.Elements())
                SortAttributesRecursively(child);
        }

        /// <summary>
        /// StringWriter that reports UTF-8 encoding so XmlWriter produces the correct declaration when enabled.
        /// </summary>
        private sealed class Utf8StringWriter : StringWriter {
            public override Encoding Encoding { get; }

            public Utf8StringWriter(Encoding encoding) =>
                Encoding = encoding ?? Encoding.UTF8;
        }
    }
}