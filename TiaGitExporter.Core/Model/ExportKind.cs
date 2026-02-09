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
    /// Identifies the exportable object type.
    ///
    /// This enum is used across scanning, export, import, and path generation
    /// to determine how each artifact should be handled.
    /// </summary>
    public enum ExportKind {
        /// <summary>
        /// User-defined data type (UDT / PLC data type).
        /// </summary>
        Udt,

        /// <summary>
        /// Function block (FB).
        /// Typically contains instance data and may reference UDTs.
        /// </summary>
        Fb,

        /// <summary>
        /// Function (FC).
        /// Stateless block without instance DB.
        /// </summary>
        Fc,

        /// <summary>
        /// Organization block (OB).
        /// Entry point blocks executed by the PLC runtime (e.g., OB1, OB100).
        /// </summary>
        Ob,

        /// <summary>
        /// Data block (DB).
        /// Can be global or instance DB.
        /// </summary>
        Db,

        /// <summary>
        /// PLC tag table.
        /// Contains symbolic tags and addresses.
        /// Availability depends on Openness API exposure.
        /// </summary>
        PlcTagTable
    }
}