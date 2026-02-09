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
using System.Linq;
using System.Reflection;

using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

using TiaGitExporter.Core.Model;

namespace TiaGitExporter.Core.Openness {
    /// <summary>
    /// Discovers PLC software artifacts (UDTs, blocks, and optional tag tables) for S7-1200 and S7-1500 CPUs.
    ///
    /// This implementation is intentionally reflection-friendly:
    /// - In Openness V19, the exact runtime types for TypeGroup (user/system) can vary by installation/project.
    /// - Some installations expose different property names for PLC tag table roots.
    ///
    /// Returned objects are plain DTOs suitable for UI binding.
    ///
    /// IMPORTANT:
    /// - Call this on the Openness STA thread (same thread that owns the TiaPortal instance).
    /// </summary>
    public sealed class PlcInventoryService {
        private readonly TiaSessionService _session;

        /// <summary>
        /// Creates a new inventory service bound to a TIA session.
        /// </summary>
        public PlcInventoryService(TiaSessionService session) {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Scans all PLC devices in the opened project and returns exportable items.
        /// </summary>
        /// <param name="profile">Predefined profile controlling what kinds of artifacts are included.</param>
        /// <param name="includeDbs">If false, DBs are excluded even when found.</param>
        public List<ExportItem> ScanExportables(ExportProfile profile, bool includeDbs) {
            var project = _session.GetOpenProjectOnSta();
            var items = new List<ExportItem>();

            foreach (var device in project.Devices) {
                foreach (var cpuItem in FindCpuDeviceItems(device)) {
                    // CpuPath format is used later to re-resolve PlcSoftware on export/import.
                    var cpuPath = device.Name + "/" + cpuItem.Name;

                    var plc = TryGetPlcSoftware(cpuItem);
                    if (plc == null) continue;

                    // Types (UDTs)
                    CollectTypes(plc.TypeGroup, cpuPath, "Types", items);

                    // Blocks (OB/FB/FC/DB)
                    CollectBlocks(plc.BlockGroup, cpuPath, "Blocks", items, includeDbs);

                    // Safety blocks (if present). Some F-CPUs expose a separate safety program with its own block group.
                    var safetyBlockGroup = TryGetSafetyBlockGroup(plc);
                    if (safetyBlockGroup != null)
                        CollectBlocks(safetyBlockGroup, cpuPath, "SafetyBlocks", items, includeDbs);

                    // Tag tables (optional, profile-driven)
                    if (profile == ExportProfile.CorePlusTags || profile == ExportProfile.Full) {
                        var tagRoot = TryGetTagTableRoot(plc);
                        if (tagRoot != null)
                            CollectTagTables(tagRoot, cpuPath, "TagTables", items);
                    }
                }
            }

            // Return a stable ordering for UI and deterministic processing.
            return items
                .OrderBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Kind)
                .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // -------------------------
        // CPU discovery
        // -------------------------

        /// <summary>
        /// Finds all device items that expose a SoftwareContainer (typically CPUs).
        /// Uses a stack-based traversal to avoid recursion.
        /// </summary>
        private static IEnumerable<DeviceItem> FindCpuDeviceItems(Device device) {
            var stack = new Stack<DeviceItem>(device.DeviceItems);
            while (stack.Count > 0) {
                var di = stack.Pop();

                foreach (var child in di.DeviceItems)
                    stack.Push(child);

                // CPU detection heuristic: any device item that has a software container.
                if (di.GetService<SoftwareContainer>() != null)
                    yield return di;
            }
        }

        /// <summary>
        /// Returns PlcSoftware from a CPU device item (or null when not a PLC CPU).
        /// </summary>
        private static PlcSoftware TryGetPlcSoftware(DeviceItem cpuDeviceItem) {
            var swContainer = cpuDeviceItem.GetService<SoftwareContainer>();
            if (swContainer != null && swContainer.Software is PlcSoftware)
                return (PlcSoftware)swContainer.Software;

            return null;
        }

        /// <summary>
        /// Best-effort discovery of a safety program block group via reflection.
        /// On standard CPUs this returns null.
        /// </summary>
        private static PlcBlockGroup TryGetSafetyBlockGroup(PlcSoftware plc) {
            if (plc == null) return null;

            try {
                // Property name may vary across Openness versions.
                // We try common ones and then look for a BlockGroup on the safety program object.
                var spProp = plc.GetType().GetProperty("SafetyProgram", BindingFlags.Instance | BindingFlags.Public)
                             ?? plc.GetType().GetProperty("PlcSafetyProgram", BindingFlags.Instance | BindingFlags.Public);

                var sp = spProp?.GetValue(plc, null);
                if (sp == null) return null;

                var bgProp = sp.GetType().GetProperty("BlockGroup", BindingFlags.Instance | BindingFlags.Public);
                if (bgProp?.GetValue(sp, null) is PlcBlockGroup bg)
                    return bg;
            } catch {
                // Best-effort only: absence/reflection failures should not break inventory.
            }

            return null;
        }

        // -------------------------
        // Types (UDTs) traversal (reflection)
        // -------------------------

        /// <summary>
        /// Collects UDTs (PlcType) from the TypeGroup hierarchy using reflection.
        /// </summary>
        private static void CollectTypes(object typeGroupRoot, string cpuPath, string prefix, List<ExportItem> items) {
            if (typeGroupRoot == null) return;

            TraverseGenericGroup(
                root: typeGroupRoot,
                groupPath: prefix, // Root name is implicit; keep paths relative to root for stable re-resolution.
                getChildren: g => GetChildren(g, "Groups"),
                getItems: g => GetChildren(g, "Types"),
                addItem: (groupPath, objName) => {
                    items.Add(new ExportItem(
                        cpuPath,
                        TrimRootPath(groupPath, "Types"),
                        objName,
                        ExportKind.Udt,
                        BuildLogicalKey(cpuPath, ExportKind.Udt, groupPath, objName)
                    ));
                });
        }

        // -------------------------
        // Blocks traversal (strongly typed)
        // -------------------------

        /// <summary>
        /// Collects blocks from a PlcBlockGroup tree (including nested groups).
        /// </summary>
        private static void CollectBlocks(PlcBlockGroup group, string cpuPath, string prefix, List<ExportItem> items, bool includeDbs) {
            if (group == null) return;

            // IMPORTANT:
            // Do NOT include the root group's display name (e.g. "Program blocks") in stored paths.
            // In Openness, the root group is implicit; including its name makes later resolution fail.
            var basePath = prefix;

            foreach (var b in group.Blocks) {
                var kind = MapBlockKindByRuntimeType(b, includeDbs, out bool include);
                if (!include) continue;

                items.Add(new ExportItem(
                    cpuPath,
                    TrimRootPath(basePath, prefix),
                    b.Name,
                    kind,
                    BuildLogicalKey(cpuPath, kind, basePath, b.Name)
                ));
            }

            foreach (var sub in group.Groups) {
                var subPath = basePath + "/" + sub.Name;
                CollectBlocksRecursive(sub, cpuPath, prefix, subPath, items, includeDbs);
            }
        }

        /// <summary>
        /// Recursive helper to collect blocks from nested groups.
        /// </summary>
        private static void CollectBlocksRecursive(PlcBlockGroup group, string cpuPath, string prefix, string currentPath, List<ExportItem> items, bool includeDbs) {
            if (group == null) return;

            foreach (var b in group.Blocks) {
                var kind = MapBlockKindByRuntimeType(b, includeDbs, out bool include);
                if (!include) continue;

                items.Add(new ExportItem(
                    cpuPath,
                    TrimRootPath(currentPath, prefix),
                    b.Name,
                    kind,
                    BuildLogicalKey(cpuPath, kind, currentPath, b.Name)
                ));
            }

            foreach (var sub in group.Groups) {
                var subPath = currentPath + "/" + sub.Name;
                CollectBlocksRecursive(sub, cpuPath, prefix, subPath, items, includeDbs);
            }
        }

        /// <summary>
        /// Classifies blocks without taking a compile-time dependency on concrete Openness derived types.
        /// Some installations/API variants do not expose all derived classes in referenced assemblies,
        /// so we classify by runtime type name and fall back to common naming conventions.
        /// </summary>
        private static ExportKind MapBlockKindByRuntimeType(PlcBlock block, bool includeDbs, out bool include) {
            include = true;

            // 1) Prefer runtime type name (works across versions even when derived types aren't referenced).
            var typeName = block?.GetType().Name ?? string.Empty;

            // Order matters: FunctionBlock contains "Function".
            if (typeName.IndexOf("OrganizationBlock", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.Equals("OB", StringComparison.OrdinalIgnoreCase))
                return ExportKind.Ob;

            if (typeName.IndexOf("FunctionBlock", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.Equals("FB", StringComparison.OrdinalIgnoreCase))
                return ExportKind.Fb;

            if (typeName.IndexOf("DataBlock", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("InstanceDB", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.Equals("DB", StringComparison.OrdinalIgnoreCase)) {
                if (!includeDbs) {
                    include = false;
                    return ExportKind.Db;
                }
                return ExportKind.Db;
            }

            // Plain function (FC) is often named "Function".
            if (typeName.Equals("Function", StringComparison.OrdinalIgnoreCase)
                || (typeName.IndexOf("Function", StringComparison.OrdinalIgnoreCase) >= 0
                    && typeName.IndexOf("FunctionBlock", StringComparison.OrdinalIgnoreCase) < 0))
                return ExportKind.Fc;

            // 2) Fallback: common Siemens naming convention (OB1/FC10/FB2/DB3...).
            var n = (block?.Name ?? string.Empty).Trim();
            if (n.Length >= 2) {
                var prefix = new string(n.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
                if (prefix == "OB") return ExportKind.Ob;
                if (prefix == "FB") return ExportKind.Fb;
                if (prefix == "FC") return ExportKind.Fc;
                if (prefix == "DB") {
                    if (!includeDbs) { include = false; return ExportKind.Db; }
                    return ExportKind.Db;
                }
            }

            // Unknown/other block kinds (technology/safety variants): include them so they show up.
            include = true;
            return ExportKind.Fc;
        }

        // -------------------------
        // Tag tables traversal (reflection)
        // -------------------------

        /// <summary>
        /// Attempts to locate the tag table root group in a version-tolerant way.
        /// Different Openness versions use different property names.
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
        /// Collects PLC tag tables from the tag table group hierarchy using reflection.
        /// </summary>
        private static void CollectTagTables(object tagRoot, string cpuPath, string prefix, List<ExportItem> items) {
            if (tagRoot == null) return;

            TraverseGenericGroup(
                root: tagRoot,
                groupPath: prefix, // Root name is implicit; keep paths relative to root for stable re-resolution.
                getChildren: g => GetChildren(g, "Groups"),
                getItems: g => GetChildren(g, "TagTables"),
                addItem: (groupPath, objName) => {
                    items.Add(new ExportItem(
                        cpuPath,
                        TrimRootPath(groupPath, "TagTables"),
                        objName,
                        ExportKind.PlcTagTable,
                        BuildLogicalKey(cpuPath, ExportKind.PlcTagTable, groupPath, objName)
                    ));
                });
        }

        // -------------------------
        // Generic traversal helpers
        // -------------------------

        /// <summary>
        /// Traverses an Openness group tree using reflection-friendly accessors.
        /// </summary>
        private static void TraverseGenericGroup(
            object root,
            string groupPath,
            Func<object, IEnumerable> getChildren,
            Func<object, IEnumerable> getItems,
            Action<string, string> addItem) {
            var stack = new Stack<Tuple<object, string>>();
            stack.Push(Tuple.Create(root, groupPath));

            while (stack.Count > 0) {
                var node = stack.Pop();
                var g = node.Item1;
                var path = node.Item2;

                // Items (Types/TagTables/etc.)
                foreach (var it in getItems(g)) {
                    var name = GetName(it);
                    if (!string.IsNullOrWhiteSpace(name))
                        addItem(path, name);
                }

                // Child groups
                foreach (var child in getChildren(g)) {
                    var childName = GetName(child) ?? "Group";
                    stack.Push(Tuple.Create(child, path + "/" + childName));
                }
            }
        }

        /// <summary>
        /// Reads a child collection property from an object using reflection.
        /// Returns an empty enumerable if the property is missing.
        /// </summary>
        private static IEnumerable GetChildren(object obj, string propertyName) {
            if (obj == null) return Array.Empty<object>();

            var p = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (p == null) return Array.Empty<object>();

            var val = p.GetValue(obj, null);
            return val as IEnumerable ?? Array.Empty<object>();
        }

        /// <summary>
        /// Reads the "Name" property from an Openness object via reflection.
        /// </summary>
        private static string GetName(object obj) {
            if (obj == null) return null;

            var p = obj.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
            return p == null ? null : p.GetValue(obj, null) as string;
        }

        /// <summary>
        /// Trims a known root token from a stored group path (root is considered implicit).
        /// </summary>
        private static string TrimRootPath(string full, string root) {
            if (string.IsNullOrWhiteSpace(full)) return "";

            var normalized = full.Replace("\\", "/").Trim('/');

            if (string.IsNullOrWhiteSpace(root))
                return normalized;

            var r = root.Replace("\\", "/").Trim('/');

            // Root group name is implicit in Openness. Treat exact-match as empty.
            if (string.Equals(normalized, r, StringComparison.OrdinalIgnoreCase))
                return "";

            var prefix = r + "/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(prefix.Length);

            return normalized;
        }

        /// <summary>
        /// Builds a stable logical key that can be used to restore checked state in the UI.
        /// </summary>
        private static string BuildLogicalKey(string cpuPath, ExportKind kind, string groupPath, string name) {
            // Use forward slashes to keep keys stable across OS separator differences.
            return cpuPath + "||" + kind + "||" + groupPath.Replace("\\", "/") + "||" + name;
        }
    }
}