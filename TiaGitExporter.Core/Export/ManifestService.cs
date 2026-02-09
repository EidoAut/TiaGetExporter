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
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TiaGitExporter.Core.Export {
    /// <summary>
    /// Reads/writes the manifest file used for incremental exports.
    ///
    /// The manifest stores:
    /// - Relative file path in the repository (key)
    /// - SHA-256 hash of the exported (optionally normalized) content
    /// - Timestamp of the last successful export
    ///
    /// Implementation notes:
    /// - Uses DataContractJsonSerializer to avoid extra dependencies.
    /// - Designed to be compatible with .NET Framework 4.8.
    /// - The manifest location is fixed under: &lt;repoRoot&gt;/meta/manifest.json
    /// </summary>
    public sealed class ManifestService {
        /// <summary>
        /// Default file name for the incremental manifest.
        /// Stored under the "meta" folder inside the repo root.
        /// </summary>
        private const string DefaultManifestName = "manifest.json";

        /// <summary>
        /// Loads the manifest from disk. If it does not exist, returns a new empty manifest.
        /// </summary>
        public Manifest Load(string repoRoot) {
            var path = GetManifestPath(repoRoot);
            if (!File.Exists(path))
                return new Manifest();

            using (var fs = File.OpenRead(path)) {
                var ser = new DataContractJsonSerializer(typeof(Manifest));
                var obj = ser.ReadObject(fs) as Manifest;

                // Defensive: handle corrupt/empty files gracefully by returning a new manifest.
                return obj ?? new Manifest();
            }
        }

        /// <summary>
        /// Saves the given manifest to disk (overwriting any existing manifest).
        /// </summary>
        public void Save(string repoRoot, Manifest manifest) {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            var path = GetManifestPath(repoRoot);

            // Ensure the meta folder exists.
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            using (var fs = File.Create(path)) {
                var ser = new DataContractJsonSerializer(typeof(Manifest));
                ser.WriteObject(fs, manifest);
            }
        }

        /// <summary>
        /// Removes manifest entries that no longer exist on disk.
        /// This keeps incremental state clean when files are deleted/renamed externally.
        /// </summary>
        public void PruneMissingEntries(string repoRoot, Manifest manifest) {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            var toRemove = new List<string>();

            // Keys are stored with forward slashes; convert to OS path separators for File.Exists checks.
            foreach (var kv in manifest.Entries) {
                var rel = (kv.Key ?? "").Replace("/", Path.DirectorySeparatorChar.ToString());
                var full = Path.Combine(repoRoot, rel);

                if (!File.Exists(full))
                    toRemove.Add(kv.Key);
            }

            // Remove after enumeration to avoid modifying the dictionary while iterating.
            foreach (var k in toRemove)
                manifest.Entries.Remove(k);
        }

        /// <summary>
        /// Returns the absolute manifest path for a given repo root:
        /// &lt;repoRoot&gt;/meta/manifest.json
        /// </summary>
        public string GetManifestPath(string repoRoot) {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));

            return Path.Combine(repoRoot, "meta", DefaultManifestName);
        }

        /// <summary>
        /// Manifest data model persisted as JSON.
        /// Entries are keyed by repo-relative file path (using forward slashes).
        /// </summary>
        [DataContract]
        public sealed class Manifest {
            /// <summary>
            /// Tool version that produced this manifest (useful for future migrations).
            /// </summary>
            [DataMember] public string ToolVersion { get; set; } = "0.2";

            /// <summary>
            /// TIA Portal version associated with the export run (used as a compatibility hint).
            /// </summary>
            [DataMember] public string TiaVersion { get; set; } = "V19";

            /// <summary>
            /// Map of relative path -&gt; manifest entry metadata (hash + timestamp).
            /// Uses case-insensitive comparer to reduce issues on Windows file systems.
            /// </summary>
            [DataMember]
            public Dictionary<string, ManifestEntry> Entries { get; set; }
                = new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// One manifest entry describing a single exported file.
        /// </summary>
        [DataContract]
        public sealed class ManifestEntry {
            /// <summary>
            /// SHA-256 hash of the written content (after optional XML normalization).
            /// </summary>
            [DataMember] public string Sha256 { get; set; }

            /// <summary>
            /// ISO-8601 UTC timestamp ("o" format) of the last successful export.
            /// </summary>
            [DataMember] public string LastExportUtc { get; set; }
        }
    }
}
