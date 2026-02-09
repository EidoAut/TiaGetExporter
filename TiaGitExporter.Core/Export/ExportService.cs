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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

using TiaGitExporter.Core.Model;
using TiaGitExporter.Core.Openness;
using TiaGitExporter.Core.Utilities;
using static TiaGitExporter.Core.Export.ManifestService;

namespace TiaGitExporter.Core.Export {
    /// <summary>
    /// Exports selected PLC artifacts to XML files.
    ///
    /// Supported artifacts:
    /// - UDTs (PlcType)
    /// - Blocks (OB/FB/FC/DB)
    /// - PLC tag tables (when exposed by the installed Openness API)
    ///
    /// Design goals:
    /// - Deterministic output paths (Export and Import share the same naming policy)
    /// - Optional XML normalization to improve Git diffs
    /// - Optional incremental export using a manifest with SHA-256 hashes
    ///
    /// IMPORTANT:
    /// - Must be executed on the same STA thread as the TiaPortal instance.
    /// </summary>
    public sealed class ExportService {
        private readonly TiaSessionService _session;
        private readonly XmlNormalizeService _xmlNormalize;
        private readonly ManifestService _manifestService;
        private readonly ILogger _log;

        /// <summary>
        /// Creates an exporter using a null logger (no-op logging).
        /// </summary>
        public ExportService(TiaSessionService session, XmlNormalizeService xmlNormalize, ManifestService manifestService)
            : this(session, xmlNormalize, manifestService, NullLogger.Instance) { }

        /// <summary>
        /// Creates an exporter with an explicit logger.
        /// </summary>
        public ExportService(TiaSessionService session, XmlNormalizeService xmlNormalize, ManifestService manifestService, ILogger logger) {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _xmlNormalize = xmlNormalize ?? throw new ArgumentNullException(nameof(xmlNormalize));
            _manifestService = manifestService ?? throw new ArgumentNullException(nameof(manifestService));
            _log = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Exports the given selection into the repository root.
        /// If <paramref name="incremental"/> is true, unchanged items are skipped using the manifest hashes.
        ///
        /// The optional <paramref name="progress"/> callback receives:
        /// (processedCount, totalCount, currentItem, exceptionIfAny).
        /// </summary>
        public ExportResult Export(
            string repoRoot,
            IReadOnlyCollection<ExportItem> selection,
            bool normalizeXml,
            bool incremental,
            Action<int, int, ExportItem, Exception> progress = null) {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));

            // Output root strategy:
            // - incremental == true  -> stable folder per project (enables real incremental exports)
            // - incremental == false -> timestamped snapshot folder
            var project = _session.GetOpenProjectOnSta();
            var projectName = project != null ? project.Name : "Project";

            string runRoot;
            if (incremental) {
                // Stable run root so repeated exports update the same folder.
                runRoot = Path.Combine(repoRoot, PathHelper.Sanitize(projectName));
            } else {
                // Snapshot-style export: each run gets its own timestamped folder.
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var runRootName = PathHelper.Sanitize(projectName) + "_" + stamp;
                runRoot = Path.Combine(repoRoot, runRootName);
            }

            Directory.CreateDirectory(runRoot);

            // Manifest is only used when incremental is enabled.
            var manifest = incremental ? _manifestService.Load(runRoot) : new Manifest();

            var result = new ExportResult {
                OutputRoot = runRoot
            };

            // Stable order: Types -> Blocks -> TagTables -> DBs
            // (This deterministic ordering improves reproducibility and user perception.)
            var ordered = selection
                .OrderBy(i => OrderKey(i.Kind))
                .ThenBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var total = ordered.Count;
            var processed = 0;

            foreach (var item in ordered) {
                try {
                    // Deterministic path & filename so Import can locate exactly what Export produced.
                    var outPath = PathHelper.GetOutputFilePath(runRoot, item);
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");

                    // Export one item via Openness into XML string.
                    var xml = ExportSingleToString(item);

                    // Normalize XML optionally to reduce diffs (attribute order, whitespace, etc.).
                    if (normalizeXml)
                        xml = _xmlNormalize.Normalize(xml);

                    // Compute content hash for incremental comparisons.
                    var sha = HashHelper.Sha256Hex(xml);
                    var rel = MakeRepoRelative(runRoot, outPath);

                    if (incremental) {
                        // Skip writing when file content hasn't changed.
                        ManifestEntry existing;
                        if (manifest.Entries.TryGetValue(rel, out existing) &&
                            string.Equals(existing.Sha256, sha, StringComparison.OrdinalIgnoreCase)) {
                            result.Skipped++;
                            processed++;
                            progress?.Invoke(processed, total, item, null);
                            continue;
                        }
                    }

                    // Write XML without BOM for stable diffs.
                    File.WriteAllText(outPath, xml, new UTF8Encoding(false));

                    // "_sources" generation intentionally disabled.
                    // TryWriteSources(outPath, item, xml);

                    if (incremental) {
                        // Update manifest entry for this item.
                        manifest.Entries[rel] = new ManifestEntry {
                            Sha256 = sha,
                            LastExportUtc = DateTime.UtcNow.ToString("o")
                        };
                    }

                    result.Exported++;
                    processed++;
                    progress?.Invoke(processed, total, item, null);
                } catch (ExportWarningException wx) {
                    // Non-fatal: record as warning and continue.
                    result.Warnings.Add(new ExportWarning(item, wx));
                    _log.Warn("Export warning for " + item + ": " + wx.Message);

                    processed++;
                    progress?.Invoke(processed, total, item, wx);
                } catch (Exception ex) {
                    // Some Openness builds don't expose certain collections (e.g. PLC tag tables).
                    // Treat "warning-like" exceptions as non-fatal so the export can proceed.
                    var w = ex as ExportWarningException;
                    if (w != null) {
                        result.Warnings.Add(new ExportWarning(item, w));
                        _log.Warn("Export skipped (warning) for " + item + ": " + w.Message);

                        processed++;
                        progress?.Invoke(processed, total, item, w);
                    } else {
                        result.Errors.Add(new ExportError(item, ex));
                        _log.Error("Export failed for " + item, ex);

                        processed++;
                        progress?.Invoke(processed, total, item, ex);
                    }
                }
            }

            if (incremental) {
                // Remove manifest entries for files that no longer exist on disk.
                _manifestService.PruneMissingEntries(runRoot, manifest);

                // Persist manifest for next incremental run.
                _manifestService.Save(runRoot, manifest);
            }

            // Write a hint file to help import/compatibility checks.
            WriteTiaVersionHint(runRoot, manifest.TiaVersion);

            return result;
        }

        /// <summary>
        /// Best-effort source generation from XML (SCL/ST extraction or markdown summaries).
        /// NOTE: This method is currently not called (sources are disabled on purpose).
        /// </summary>
        private void TryWriteSources(string xmlPath, ExportItem item, string xml) {
            try {
                var folder = Path.GetDirectoryName(xmlPath) ?? ".";
                var sourcesDir = Path.Combine(folder, "_sources");
                Directory.CreateDirectory(sourcesDir);

                var baseName = Path.GetFileNameWithoutExtension(xmlPath);

                // Prefer structured text when present (SCL/ST). Otherwise generate a lightweight summary.
                var st = SourceExtract.TryExtractStructuredText(xml);

                if (item.Kind == ExportKind.Db) {
                    // Always write a .db file for DBs.
                    var dbText = st ?? SourceExtract.TryExtractDbText(xml) ?? "// No readable DB source found in the exported XML.\r\n";
                    File.WriteAllText(Path.Combine(sourcesDir, baseName + ".db"), dbText, new UTF8Encoding(false));
                    return;
                }

                if (item.Kind == ExportKind.Ob || item.Kind == ExportKind.Fb || item.Kind == ExportKind.Fc) {
                    // If structured text exists, store it as .scl; otherwise store a summary markdown.
                    if (!string.IsNullOrWhiteSpace(st)) {
                        File.WriteAllText(Path.Combine(sourcesDir, baseName + ".scl"), st, new UTF8Encoding(false));
                        return;
                    }

                    var md = SourceExtract.BuildGraphicalSummaryMarkdown(xml, item);
                    File.WriteAllText(Path.Combine(sourcesDir, baseName + ".md"), md, new UTF8Encoding(false));
                    return;
                }

                // Other artifact kinds: do nothing for now.
            } catch (Exception ex) {
                // Source generation is best-effort; never fail the export because of it.
                _log.Warn("Source generation failed for " + item + ": " + ex.Message);
            }
        }

        // NOTE: legacy collision-avoidance output naming has been removed in favor of a single
        // deterministic naming policy shared with Import (PathHelper.GetOutputFilePath).

        /// <summary>
        /// Provides a stable ordering across artifact kinds so exports are deterministic.
        /// </summary>
        private static int OrderKey(ExportKind kind) {
            switch (kind) {
                case ExportKind.Udt: return 0;
                case ExportKind.Ob: return 1;
                case ExportKind.Fb: return 2;
                case ExportKind.Fc: return 3;
                case ExportKind.PlcTagTable: return 4;
                case ExportKind.Db: return 5;
                default: return 99;
            }
        }

        /// <summary>
        /// Exports a single item to an XML string by exporting to a temp file first
        /// (Openness export API is file-based).
        /// </summary>
        private string ExportSingleToString(ExportItem item) {
            // Isolate temp files per operation to simplify cleanup.
            var tmpDir = Path.Combine(Path.GetTempPath(), "tia_git_export", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            var tmp = Path.Combine(tmpDir, "export.xml");

            try {
                ExportSingleToFile(item, tmp);

                // Read as UTF-8 (Openness typically produces UTF-8 XML).
                return File.ReadAllText(tmp, Encoding.UTF8);
            } finally {
                // Best-effort cleanup: do not throw if cleanup fails.
                TryDelete(tmp);
                TryDeleteDirectory(tmpDir);
            }
        }

        /// <summary>
        /// Exports a single item to the given file path using the Openness Export() method.
        /// </summary>
        private void ExportSingleToFile(ExportItem item, string filePath) {
            var project = _session.GetOpenProjectOnSta();
            var plc = ResolvePlcSoftware(project, item.CpuPath);
            if (plc == null)
                throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);

            var fi = new FileInfo(filePath);

            // Inventory/UI may include a friendly root label ("Blocks", "PLC data types", "TagTables").
            // In Openness these roots are implicit, so we normalize to a path relative to the real root group.
            var groupPath = NormalizeRootGroupPath(item.Kind, item.GroupPath);

            switch (item.Kind) {
                case ExportKind.Udt:
                    var udt = ResolveUdt(plc, groupPath, item.Name);
                    if (udt == null) throw new InvalidOperationException("UDT not found: " + item);
                    udt.Export(fi, Siemens.Engineering.ExportOptions.WithDefaults);
                    return;

                case ExportKind.Ob:
                case ExportKind.Fb:
                case ExportKind.Fc:
                case ExportKind.Db:
                    var block = ResolveBlock(plc, groupPath, item.Name);
                    if (block == null) throw new InvalidOperationException("Block not found: " + item);
                    block.Export(fi, Siemens.Engineering.ExportOptions.WithDefaults);
                    return;

                case ExportKind.PlcTagTable:
                    var table = ResolvePlcTagTable(plc, groupPath, item.Name);
                    if (table == null)
                        throw new ExportWarningException("PLC tag table not found (or not exposed by this Openness build).", item);
                    table.Export(fi, Siemens.Engineering.ExportOptions.WithDefaults);
                    return;

                default:
                    throw new NotSupportedException("Unsupported kind: " + item.Kind);
            }
        }

        /// <summary>
        /// Removes UI-level root group labels from a GroupPath so it matches Openness group roots.
        /// </summary>
        private static string NormalizeRootGroupPath(ExportKind kind, string groupPath) {
            var p = (groupPath ?? "").Trim().Replace("\\", "/");
            if (p.Length == 0) return "";

            // Root group names are UI conveniences; Openness roots are implicit.
            // We therefore trim optional root names if present.
            string[] roots;
            switch (kind) {
                case ExportKind.Udt:
                    roots = new[] { "PLC data types", "PLC data types".ToLowerInvariant(), "Types", "UDTs" };
                    break;
                case ExportKind.PlcTagTable:
                    roots = new[] { "PLC tags", "TagTables", "PLC tag tables", "PLC tag tables".ToLowerInvariant() };
                    break;
                case ExportKind.Ob:
                case ExportKind.Fb:
                case ExportKind.Fc:
                case ExportKind.Db:
                    roots = new[] { "Blocks", "Program blocks", "Program Blocks" };
                    break;
                default:
                    roots = new string[0];
                    break;
            }

            foreach (var r in roots) {
                if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase))
                    return "";
                if (p.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase))
                    return p.Substring(r.Length + 1);
            }
            return p;
        }

        // -------------------------
        // PLC resolution
        // -------------------------

        /// <summary>
        /// Resolves the PlcSoftware instance from a CPU path string.
        /// Expected cpuPath format: "&lt;DeviceName&gt;/&lt;CpuItemName&gt;".
        ///
        /// IMPORTANT: Device names in TIA can contain '/', so we split at the last '/'.
        /// </summary>
        private static PlcSoftware ResolvePlcSoftware(Project project, string cpuPath) {
            var p = (cpuPath ?? "").Trim();
            var lastSlash = p.LastIndexOf('/');
            if (lastSlash <= 0 || lastSlash >= p.Length - 1) return null;

            var deviceName = p.Substring(0, lastSlash);
            var cpuName = p.Substring(lastSlash + 1);

            var device = project.Devices.FirstOrDefault(d =>
                string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
            if (device == null) return null;

            // CPU can be nested anywhere under device items (rack, module, etc.).
            var cpu = FindAllDeviceItems(device).FirstOrDefault(di =>
                string.Equals(di.Name, cpuName, StringComparison.OrdinalIgnoreCase));
            if (cpu == null) return null;

            var swContainer = cpu.GetService<SoftwareContainer>();
            if (swContainer == null) return null;

            return swContainer.Software as PlcSoftware;
        }

        /// <summary>
        /// Iterates all DeviceItems in a device tree using a stack (non-recursive).
        /// </summary>
        private static IEnumerable<DeviceItem> FindAllDeviceItems(Device device) {
            var stack = new Stack<DeviceItem>(device.DeviceItems);
            while (stack.Count > 0) {
                var di = stack.Pop();
                yield return di;

                foreach (var child in di.DeviceItems)
                    stack.Push(child);
            }
        }

        // -------------------------
        // Types (UDTs) resolution (reflection-based, robust)
        // -------------------------

        /// <summary>
        /// Resolves a UDT (PlcType) within the TypeGroup hierarchy using reflection.
        /// Reflection is used to tolerate differences across Openness versions.
        /// </summary>
        private static PlcType ResolveUdt(PlcSoftware plc, string groupPath, string name) {
            object root = plc.TypeGroup;
            if (root == null) return null;

            var group = ResolveGroupByPath(root, groupPath);
            if (group == null) return null;

            // Many Openness versions expose types under a "Types" property.
            foreach (var t in GetChildren(group, "Types").OfType<object>()) {
                var tName = GetName(t);
                if (string.Equals(tName, name, StringComparison.OrdinalIgnoreCase))
                    return t as PlcType;
            }
            return null;
        }

        /// <summary>
        /// Resolves a nested group by walking a '/'-delimited path under the given root group.
        /// </summary>
        private static object ResolveGroupByPath(object rootGroup, string groupPath) {
            if (rootGroup == null) return null;
            if (string.IsNullOrWhiteSpace(groupPath)) return rootGroup;

            var parts = groupPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            object current = rootGroup;
            foreach (var part in parts) {
                // Groups are typically exposed under a "Groups" property.
                var groups = GetChildren(current, "Groups");
                object next = null;

                foreach (var g in groups) {
                    if (string.Equals(GetName(g), part, StringComparison.OrdinalIgnoreCase)) {
                        next = g;
                        break;
                    }
                }

                if (next == null) return null;
                current = next;
            }
            return current;
        }

        /// <summary>
        /// Reads an IEnumerable child collection from an object property by name (reflection-based).
        /// Returns an empty enumerable if the property doesn't exist.
        /// </summary>
        private static IEnumerable GetChildren(object obj, string propertyName) {
            if (obj == null) return Array.Empty<object>();

            var p = ReflectionCache.GetProperty(obj.GetType(), propertyName);
            if (p == null) return Array.Empty<object>();

            var val = p.GetValue(obj, null);
            return val as IEnumerable ?? Array.Empty<object>();
        }

        /// <summary>
        /// Returns the "Name" property of an Openness object via reflection.
        /// </summary>
        private static string GetName(object obj) {
            if (obj == null) return null;

            var p = ReflectionCache.GetProperty(obj.GetType(), "Name");
            return p == null ? null : p.GetValue(obj, null) as string;
        }

        // -------------------------
        // Blocks resolution
        // -------------------------

        /// <summary>
        /// Resolves a program block (OB/FB/FC/DB) from the block group hierarchy.
        /// </summary>
        private static PlcBlock ResolveBlock(PlcSoftware plc, string groupPath, string name) {
            var group = ResolveBlockGroup(plc.BlockGroup, groupPath);
            if (group == null) return null;

            return group.Blocks.FirstOrDefault(b =>
                string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves a PlcBlockGroup by walking a '/'-delimited group path.
        /// </summary>
        private static PlcBlockGroup ResolveBlockGroup(PlcBlockGroup root, string groupPath) {
            if (root == null) return null;
            if (string.IsNullOrWhiteSpace(groupPath)) return root;

            var parts = groupPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            foreach (var part in parts) {
                var next = current.Groups.FirstOrDefault(g =>
                    string.Equals(g.Name, part, StringComparison.OrdinalIgnoreCase));

                if (next == null) return null;
                current = next;
            }
            return current;
        }

        // -------------------------
        // PLC Tag Tables resolution
        // -------------------------

        /// <summary>
        /// Resolves a PLC Tag Table by name within the tag table group hierarchy.
        /// Returns null when tag tables are not exposed by the installed Openness API.
        /// </summary>
        private static PlcTagTable ResolvePlcTagTable(PlcSoftware plc, string groupPath, string name) {
            var root = TryGetTagTableRoot(plc);
            if (root == null) return null;

            var group = ResolveTagTableGroup(root, groupPath);
            if (group == null) return null;

            return group.TagTables.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Attempts to locate the tag table root group in a version-tolerant way
        /// (different Openness versions use different property names).
        /// </summary>
        private static PlcTagTableGroup TryGetTagTableRoot(PlcSoftware plc) {
            // Openness variants expose different property names.
            var prop = plc.GetType().GetProperty("PlcTagTableGroup");
            if (prop != null) {
                var val = prop.GetValue(plc, null) as PlcTagTableGroup;
                if (val != null) return val;
            }

            prop = plc.GetType().GetProperty("TagTableGroup");
            if (prop != null) {
                var val = prop.GetValue(plc, null) as PlcTagTableGroup;
                if (val != null) return val;
            }

            return null;
        }

        /// <summary>
        /// Resolves a PlcTagTableGroup by walking a '/'-delimited group path.
        /// </summary>
        private static PlcTagTableGroup ResolveTagTableGroup(PlcTagTableGroup root, string groupPath) {
            if (root == null) return null;
            if (string.IsNullOrWhiteSpace(groupPath)) return root;

            var parts = groupPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            foreach (var part in parts) {
                var next = current.Groups.FirstOrDefault(g =>
                    string.Equals(g.Name, part, StringComparison.OrdinalIgnoreCase));

                if (next == null) return null;
                current = next;
            }
            return current;
        }

        // -------------------------
        // Helpers
        // -------------------------

        /// <summary>
        /// Converts an absolute path into a repo-root relative path using forward slashes.
        /// </summary>
        private static string MakeRepoRelative(string repoRoot, string fullPath) {
            var root = Path.GetFullPath(repoRoot).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(fullPath);

            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full.Substring(root.Length).Replace("\\", "/");

            // Fallback: normalize separators.
            return full.Replace("\\", "/");
        }

        /// <summary>
        /// Writes a "meta/tia.txt" hint file with the TIA version (or a default).
        /// This helps consumers understand which TIA version produced the export.
        /// </summary>
        private static void WriteTiaVersionHint(string repoRoot, string tiaVersion) {
            var metaDir = Path.Combine(repoRoot, "meta");
            Directory.CreateDirectory(metaDir);

            File.WriteAllText(
                Path.Combine(metaDir, "tia.txt"),
                tiaVersion ?? "V19",
                new UTF8Encoding(false));
        }

        /// <summary>
        /// Best-effort file delete (swallows IO errors).
        /// </summary>
        private static void TryDelete(string path) {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        /// <summary>
        /// Best-effort directory delete (swallows IO errors).
        /// </summary>
        private static void TryDeleteDirectory(string path) {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Result summary for an export run.
    /// </summary>
    public sealed class ExportResult {
        /// <summary>
        /// Physical folder where this export run was written.
        /// </summary>
        public string OutputRoot { get; set; }

        /// <summary>Total number of items written to disk.</summary>
        public int Exported { get; set; }

        /// <summary>Total number of items skipped due to unchanged content (incremental mode only).</summary>
        public int Skipped { get; set; }

        /// <summary>Fatal errors that prevented an item from exporting.</summary>
        public List<ExportError> Errors { get; } = new List<ExportError>();

        /// <summary>Non-fatal issues encountered during export.</summary>
        public List<ExportWarning> Warnings { get; } = new List<ExportWarning>();

        /// <summary>True when at least one fatal error occurred.</summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>True when at least one warning occurred.</summary>
        public bool HasWarnings => Warnings.Count > 0;
    }

    /// <summary>
    /// Represents one non-fatal export issue.
    /// </summary>
    public sealed class ExportWarning {
        /// <summary>The item that triggered the warning.</summary>
        public ExportItem Item { get; }

        /// <summary>The exception describing the warning condition.</summary>
        public Exception Exception { get; }

        public ExportWarning(ExportItem item, Exception exception) {
            Item = item;
            Exception = exception;
        }

        public override string ToString() =>
            Item + " -> " + Exception.GetType().Name + ": " + Exception.Message;
    }

    /// <summary>
    /// Represents one failed export operation.
    /// </summary>
    public sealed class ExportError {
        /// <summary>The item that failed to export.</summary>
        public ExportItem Item { get; }

        /// <summary>The exception describing the failure.</summary>
        public Exception Exception { get; }

        public ExportError(ExportItem item, Exception exception) {
            Item = item;
            Exception = exception;
        }

        public override string ToString() =>
            Item + " -> " + Exception.GetType().Name + ": " + Exception.Message;
    }

    /// <summary>
    /// Used to signal a non-fatal condition during export.
    /// Throw this when an item can't be exported due to environment/version limitations
    /// (e.g., a missing Openness collection), but the overall export should continue.
    /// </summary>
    internal sealed class ExportWarningException : Exception {
        /// <summary>The export item that caused this warning.</summary>
        public ExportItem Item { get; }

        public ExportWarningException(string message, ExportItem item)
            : base(message) {
            Item = item;
        }
    }
}