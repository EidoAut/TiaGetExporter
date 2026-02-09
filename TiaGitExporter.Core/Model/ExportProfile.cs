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

namespace TiaGitExporter.Core.Model {
    /// <summary>
    /// Predefined export sets to simplify UI and CLI usage.
    ///
    /// Profiles allow users to quickly select common export scopes
    /// without manually choosing artifact types.
    /// </summary>
    public enum ExportProfile {
        /// <summary>
        /// Core "code-only" export:
        /// - UDTs
        /// - FBs
        /// - FCs
        /// - OBs
        ///
        /// Excludes DBs and tag tables to keep the repository focused on logic.
        /// </summary>
        Core,

        /// <summary>
        /// Core plus PLC tag tables.
        ///
        /// Useful when tag tables act as an IO contract or interface
        /// between PLC logic and external systems.
        /// </summary>
        CorePlusTags,

        /// <summary>
        /// Full export set.
        ///
        /// Includes all typically exportable artifacts:
        /// - UDTs
        /// - FB/FC/OB
        /// - DBs
        /// - PLC tag tables
        ///
        /// Provides the most complete project snapshot for backup or migration.
        /// </summary>
        Full
    }
}
