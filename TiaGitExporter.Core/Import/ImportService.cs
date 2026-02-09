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

using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;

using TiaGitExporter.Core.Model;
using TiaGitExporter.Core.Openness;
using TiaGitExporter.Core.Utilities;

namespace TiaGitExporter.Core.Import {
    /// <summary>
    /// Imports XML artifacts from the Git-friendly export folder back into the open TIA project.
    ///
    /// Notes:
    /// - This is a pragmatic "sync" importer, not a full project reconstruction tool.
    /// - It assumes the target PLC devices already exist in the project (it will not create devices).
    /// - Must run on the same STA thread as the Openness TiaPortal instance.
    /// - Import uses the same deterministic path policy as Export (PathHelper.GetOutputFilePath).
    /// </summary>
    public sealed class ImportService {
        private readonly TiaSessionService _session;
        private readonly ILogger _log;

        /// <summary>
        /// Creates an importer using a null logger (no-op logging).
        /// </summary>
        public ImportService(TiaSessionService session) : this(session, NullLogger.Instance) { }

        /// <summary>
        /// Creates an importer with an explicit logger.
        /// </summary>
        public ImportService(TiaSessionService session, ILogger logger) {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _log = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Imports the selected items from <paramref name="repoRoot"/> into the current project.
        ///
        /// Import order:
        /// 1) UDTs
        /// 2) Blocks (OB/FB/FC/DB)
        /// 3) PLC tag tables
        ///
        /// This ordering reduces dependency issues (blocks may reference UDTs).
        /// </summary>
        public ImportResult Import(string repoRoot, IReadOnlyCollection<ExportItem> selection) {
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));

            var project = _session.GetOpenProjectOnSta();
            var result = new ImportResult();

            // Deterministic import order, matching export ordering policy.
            var ordered = selection
                .OrderBy(i => OrderKey(i.Kind))
                .ThenBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in ordered) {
                try {
                    // Resolve where Export wrote this artifact.
                    var xmlPath = PathHelper.GetOutputFilePath(repoRoot, item);
                    if (!File.Exists(xmlPath)) {
                        result.Errors.Add(new ImportError(item, new FileNotFoundException("Export XML not found", xmlPath)));
                        continue;
                    }

                    // Resolve the target PLC software instance.
                    var plc = ResolvePlcSoftware(project, item.CpuPath);
                    if (plc == null)
                        throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);

                    // Dispatch by kind and import via Openness.
                    ImportSingle(plc, item, xmlPath);
                    result.Imported++;
                } catch (Exception ex) {
                    // Import is best-effort across multiple items; record and continue.
                    result.Errors.Add(new ImportError(item, ex));
                    _log.Error("Import failed for " + item, ex);
                }
            }

            return result;
        }

        /// <summary>
        /// Provides deterministic ordering to reduce dependency-related import failures.
        /// </summary>
        private static int OrderKey(ExportKind kind) {
            switch (kind) {
                case ExportKind.Udt: return 0;
                case ExportKind.Ob: return 1;
                case ExportKind.Fb: return 2;
                case ExportKind.Fc: return 3;
                case ExportKind.Db: return 4;
                case ExportKind.PlcTagTable: return 5;
                default: return 99;
            }
        }

        /// <summary>
        /// Imports a single item into the provided PLC software instance.
        /// </summary>
        private static void ImportSingle(PlcSoftware plc, ExportItem item, string xmlPath) {
            var fi = new FileInfo(xmlPath);

            switch (item.Kind) {
                case ExportKind.Udt:
                    ImportUdt(plc, item, fi);
                    return;

                case ExportKind.Ob:
                case ExportKind.Fb:
                case ExportKind.Fc:
                case ExportKind.Db:
                    ImportBlock(plc, item, fi);
                    return;

                case ExportKind.PlcTagTable:
                    ImportTagTable(plc, item, fi);
                    return;

                default:
                    throw new NotSupportedException("Unsupported kind: " + item.Kind);
            }
        }

        /// <summary>
        /// Imports a UDT XML into the appropriate type group.
        /// Uses reflection because the TypeGroup structure varies across Openness versions/installations.
        /// </summary>
        private static void ImportUdt(PlcSoftware plc, ExportItem item, FileInfo xml) {
            object root = plc.TypeGroup;
            if (root == null)
                throw new InvalidOperationException("PLC TypeGroup is not available.");

            // NOTE: GroupPath may include UI-only roots; if needed, normalize before calling this.
            var group = ResolveGenericGroupByPath(root, item.GroupPath);
            if (group == null)
                throw new InvalidOperationException("Could not resolve type group for path: " + item.GroupPath);

            // group.Types.Import(FileInfo, ImportOptions) or group.Types.Import(FileInfo)
            var typesCollection = GetPropertyValue(group, "Types");
            InvokeImport(typesCollection, xml);
        }

        /// <summary>
        /// Imports a program block (OB/FB/FC/DB) XML into the target block group.
        /// </summary>
        private static void ImportBlock(PlcSoftware plc, ExportItem item, FileInfo xml) {
            var group = ResolveBlockGroup(plc.BlockGroup, item.GroupPath);
            if (group == null)
                throw new InvalidOperationException("Could not resolve block group for path: " + item.GroupPath);

            // group.Blocks.Import(FileInfo, ImportOptions) or group.Blocks.Import(FileInfo)
            var blocksCollection = group.Blocks;
            InvokeImport(blocksCollection, xml);
        }

        /// <summary>
        /// Imports a PLC tag table XML into the appropriate tag table group.
        /// </summary>
        private static void ImportTagTable(PlcSoftware plc, ExportItem item, FileInfo xml) {
            var root = TryGetTagTableRoot(plc);
            if (root == null)
                throw new InvalidOperationException("PLC tag tables are not exposed by this Openness build.");

            var group = ResolveGenericGroupByPath(root, item.GroupPath);
            if (group == null)
                throw new InvalidOperationException("Could not resolve tag table group for path: " + item.GroupPath);

            var tablesCollection = GetPropertyValue(group, "TagTables");
            InvokeImport(tablesCollection, xml);
        }

        /// <summary>
        /// Invokes Import(...) on an Openness collection via reflection.
        /// This avoids taking a hard dependency on specific ImportOptions enum members.
        /// </summary>
        private static void InvokeImport(object opennessCollection, FileInfo xml) {
            if (opennessCollection == null)
                throw new InvalidOperationException("Target collection is null (cannot import).");

            // Find method Import(FileInfo, ImportOptions) or Import(FileInfo)
            var t = opennessCollection.GetType();
            var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => string.Equals(m.Name, "Import", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Prefer Import(FileInfo, ImportOptions) to mimic the typical Openness API shape.
            foreach (var m in methods) {
                var p = m.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(FileInfo) && p[1].ParameterType.IsEnum) {
                    // Use enum value 0 as a safe default (typically "WithDefaults" / "Override", varies by version).
                    var enumType = p[1].ParameterType;
                    var defaultOpt = Enum.ToObject(enumType, 0);

                    m.Invoke(opennessCollection, new object[] { xml, defaultOpt });
                    return;
                }
            }

            // Fallback: Import(FileInfo)
            foreach (var m in methods) {
                var p = m.GetParameters();
                if (p.Length == 1 && p[0].ParameterType == typeof(FileInfo)) {
                    m.Invoke(opennessCollection, new object[] { xml });
                    return;
                }
            }

            throw new MissingMethodException(t.FullName, "Import");
        }

        // -------------------------
        // Resolution helpers
        // -------------------------

        /// <summary>
        /// Resolves PlcSoftware based on cpuPath in the form "&lt;DeviceName&gt;/&lt;CpuItemName&gt;".
        /// Splits at the last '/' because device names may contain '/'.
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

            var cpu = FindAllDeviceItems(device).FirstOrDefault(di =>
                string.Equals(di.Name, cpuName, StringComparison.OrdinalIgnoreCase));
            if (cpu == null) return null;

            var swContainer = cpu.GetService<SoftwareContainer>();
            if (swContainer == null) return null;

            return swContainer.Software as PlcSoftware;
        }

        /// <summary>
        /// Enumerates all device items under a device (non-recursive traversal).
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

        /// <summary>
        /// Tries to get the root group for PLC tag tables using common property names across Openness versions.
        /// </summary>
        private static object TryGetTagTableRoot(PlcSoftware plc) {
            var prop = plc.GetType().GetProperty("PlcTagTableGroup", BindingFlags.Instance | BindingFlags.Public);
            if (prop != null) {
                var val = prop.GetValue(plc, null);
                if (val != null) return val;
            }

            prop = plc.GetType().GetProperty("TagTableGroup", BindingFlags.Instance | BindingFlags.Public);
            if (prop != null) {
                var val = prop.GetValue(plc, null);
                if (val != null) return val;
            }

            return null;
        }

        /// <summary>
        /// Resolves a generic Openness group hierarchy by walking "Groups" collections using reflection.
        /// This is used for type groups and tag table groups.
        /// </summary>
        private static object ResolveGenericGroupByPath(object rootGroup, string groupPath) {
            if (rootGroup == null) return null;
            if (string.IsNullOrWhiteSpace(groupPath)) return rootGroup;

            var parts = groupPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            object current = rootGroup;
            foreach (var part in parts) {
                var groups = GetEnumerableProperty(current, "Groups");
                object next = null;

                foreach (var g in groups) {
                    var name = GetStringProperty(g, "Name");
                    if (string.Equals(name, part, StringComparison.OrdinalIgnoreCase)) {
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
        /// Gets an object's property value using the shared reflection cache.
        /// </summary>
        private static object GetPropertyValue(object obj, string propertyName) {
            if (obj == null) return null;

            var p = ReflectionCache.GetProperty(obj.GetType(), propertyName);
            return p == null ? null : p.GetValue(obj, null);
        }

        /// <summary>
        /// Gets an IEnumerable property value (returns empty enumerable when missing/null).
        /// </summary>
        private static IEnumerable GetEnumerableProperty(object obj, string propertyName) {
            var v = GetPropertyValue(obj, propertyName);
            return v as IEnumerable ?? Array.Empty<object>();
        }

        /// <summary>
        /// Gets a string property value (returns null when missing/non-string).
        /// </summary>
        private static string GetStringProperty(object obj, string propertyName) {
            var v = GetPropertyValue(obj, propertyName);
            return v as string;
        }
    }

    /// <summary>
    /// Result summary for an import run.
    /// </summary>
    public sealed class ImportResult {
        /// <summary>Total number of items successfully imported.</summary>
        public int Imported { get; set; }

        /// <summary>
        /// Non-fatal issues encountered during import.
        /// The importer currently records mostly hard errors, but the UI expects warnings for parity with export.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Fatal errors that prevented one or more items from being imported.</summary>
        public List<ImportError> Errors { get; } = new List<ImportError>();

        /// <summary>True when at least one error occurred.</summary>
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// Represents one failed import operation.
    /// </summary>
    public sealed class ImportError {
        /// <summary>The item that failed to import.</summary>
        public ExportItem Item { get; }

        /// <summary>The exception describing the failure.</summary>
        public Exception Exception { get; }

        public ImportError(ExportItem item, Exception exception) {
            Item = item;
            Exception = exception;
        }

        public override string ToString() =>
            Item + " -> " + Exception.GetType().Name + ": " + Exception.Message;
    }
}
