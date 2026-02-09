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
using System.Text;

namespace TiaGitExporter.Core.Utilities {
    /// <summary>
    /// Minimal logging abstraction used across Core services.
    ///
    /// Design goals:
    /// - Avoid dependency on external logging frameworks.
    /// - Allow silent operation (NullLogger) or file-based logging.
    /// - Keep interface compatible with .NET Framework 4.8.
    /// </summary>
    public interface ILogger {
        /// <summary>
        /// Writes an informational message.
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Writes a warning message (non-fatal condition).
        /// </summary>
        void Warn(string message);

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">Human-readable description.</param>
        /// <param name="ex">Optional exception details.</param>
        void Error(string message, Exception ex = null);
    }

    /// <summary>
    /// No-op logger implementation.
    ///
    /// Useful when logging is optional or undesired
    /// (e.g., unit tests, minimal CLI usage).
    /// </summary>
    public sealed class NullLogger : ILogger {
        /// <summary>
        /// Singleton instance to avoid allocations.
        /// </summary>
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger() { }

        public void Info(string message) { /* intentionally empty */ }

        public void Warn(string message) { /* intentionally empty */ }

        public void Error(string message, Exception ex = null) { /* intentionally empty */ }
    }

    /// <summary>
    /// Thread-safe file logger.
    ///
    /// Characteristics:
    /// - Appends log lines to a single file.
    /// - Uses UTF-8 without BOM.
    /// - Serializes writes via a lock to avoid corruption.
    /// </summary>
    public sealed class FileLogger : ILogger {
        /// <summary>
        /// Absolute log file path.
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// Synchronization gate for concurrent writers.
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>
        /// Creates a file logger.
        /// Ensures the target directory exists.
        /// </summary>
        /// <param name="path">Full log file path.</param>
        public FileLogger(string path) {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            _path = path;

            // Ensure directory exists before first write.
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        }

        /// <summary>
        /// Writes an informational entry.
        /// </summary>
        public void Info(string message) => Write("INFO", message, null);

        /// <summary>
        /// Writes a warning entry.
        /// </summary>
        public void Warn(string message) => Write("WARN", message, null);

        /// <summary>
        /// Writes an error entry (with optional exception).
        /// </summary>
        public void Error(string message, Exception ex = null) => Write("ERROR", message, ex);

        /// <summary>
        /// Core write routine.
        ///
        /// Format:
        /// [timestamp] LEVEL message | ExceptionType: Message
        /// stacktrace...
        /// </summary>
        private void Write(string level, string message, Exception ex) {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}";

            if (ex != null)
                line += $" | {ex.GetType().Name}: {ex.Message}\n{ex}";

            // Serialize file writes to avoid interleaving lines.
            lock (_gate) {
                File.AppendAllText(
                    _path,
                    line + Environment.NewLine,
                    new UTF8Encoding(false) // UTF-8 without BOM
                );
            }
        }
    }
}
