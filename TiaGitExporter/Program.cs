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
using System.Reflection;
using System.Windows.Forms;

namespace TiaGitExporter {
    /// <summary>
    /// Main application entry point class.
    /// Responsible for initializing runtime settings,
    /// resolving Siemens Openness assemblies,
    /// and launching the main UI form.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Main entry point for the application.
        /// Configures assembly resolution for Siemens TIA Portal Openness,
        /// enables Windows visual styles,
        /// and starts the main application form.
        /// </summary>
        [STAThread]
        static void Main() {
            // -----------------------------------------------------------------
            // Resolve Siemens TIA Portal Openness assemblies dynamically
            // -----------------------------------------------------------------
            // The Openness API (Siemens.Engineering.dll, etc.) is not always
            // located in the application directory. Therefore we attempt to
            // resolve it at runtime using:
            //
            // 1) Environment variable: TIA_OPENNESS_V19
            // 2) Common default installation paths
            // -----------------------------------------------------------------

            /// <summary>
            /// Environment variable pointing to the Openness installation path.
            /// Example:
            /// C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19
            /// </summary>
            var opennessDir = Environment.GetEnvironmentVariable("TIA_OPENNESS_V19");

            // If the environment variable is not set, try known install paths
            if (string.IsNullOrWhiteSpace(opennessDir)) {
                var candidates = new[]
                {
                    @"C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19",
                    @"C:\Program Files (x86)\Siemens\Automation\Portal V19\PublicAPI\V19"
                };

                foreach (var c in candidates) {
                    if (Directory.Exists(c)) {
                        opennessDir = c;
                        break;
                    }
                }
            }

            // -----------------------------------------------------------------
            // Register AssemblyResolve handler if Openness path is found
            // -----------------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(opennessDir) &&
                Directory.Exists(opennessDir)) {
                /// <summary>
                /// Handles runtime resolution of missing Siemens assemblies.
                /// Automatically loads required DLLs from the Openness folder.
                /// </summary>
                AppDomain.CurrentDomain.AssemblyResolve += (_, args) => {
                    var name = new AssemblyName(args.Name).Name + ".dll";
                    var path = Path.Combine(opennessDir, name);

                    return File.Exists(path)
                        ? Assembly.LoadFrom(path)
                        : null;
                };
            }

            // -----------------------------------------------------------------
            // Initialize Windows Forms application
            // -----------------------------------------------------------------

            /// <summary>
            /// Enables Windows visual styles for modern UI appearance.
            /// </summary>
            Application.EnableVisualStyles();

            /// <summary>
            /// Sets text rendering compatibility.
            /// </summary>
            Application.SetCompatibleTextRenderingDefault(false);

            /// <summary>
            /// Launches the main application window.
            /// </summary>
            Application.Run(new Form1());
        }
    }
}