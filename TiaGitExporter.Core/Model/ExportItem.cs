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

namespace TiaGitExporter.Core.Model {
    /// <summary>
    /// Represents one exportable artifact discovered in a TIA project.
    /// This is a lightweight model object used by the UI and services to avoid
    /// holding onto live Openness objects across operations.
    /// </summary>
    public sealed class ExportItem {
        /// <summary>
        /// Human-friendly path identifying the CPU, e.g. "Device1/CPU_1516".
        /// This is used to resolve the target PlcSoftware by device + CPU name.
        /// </summary>
        public string CpuPath { get; }

        /// <summary>
        /// Logical group path inside PLC software, e.g. "Blocks/Drives/Motion".
        /// Used to build deterministic repo paths and to locate the destination group on import.
        /// </summary>
        public string GroupPath { get; }

        /// <summary>
        /// Object name as shown in TIA (e.g. "FB10", "OB1", "UDT3", "Signals").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Kind of the artifact (FB/FC/OB/UDT/DB/TagTable).
        /// Determines export/import handling and output naming.
        /// </summary>
        public ExportKind Kind { get; }

        /// <summary>
        /// Stable logical key used to re-resolve the object later.
        /// This should remain stable across sessions and scans, unlike in-memory object references.
        /// </summary>
        public string LogicalKey { get; }

        /// <summary>
        /// Creates a new export item descriptor.
        /// </summary>
        /// <param name="cpuPath">CPU path, typically "&lt;DeviceName&gt;/&lt;CpuName&gt;".</param>
        /// <param name="groupPath">Group path inside PLC software (may be empty).</param>
        /// <param name="name">TIA object name.</param>
        /// <param name="kind">Artifact kind.</param>
        /// <param name="logicalKey">Stable unique key for selection persistence.</param>
        public ExportItem(string cpuPath, string groupPath, string name, ExportKind kind, string logicalKey) {
            CpuPath = cpuPath ?? throw new ArgumentNullException(nameof(cpuPath));
            GroupPath = groupPath ?? "";
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            LogicalKey = logicalKey ?? throw new ArgumentNullException(nameof(logicalKey));
        }

        /// <summary>
        /// Returns a compact human-readable representation for logs and UI.
        /// </summary>
        public override string ToString() => $"{CpuPath} | {GroupPath} | {Kind} | {Name}";
    }
}
