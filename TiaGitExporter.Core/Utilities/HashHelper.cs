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
using System.Security.Cryptography;
using System.Text;

namespace TiaGitExporter.Core.Utilities {
    /// <summary>
    /// Cryptographic hashing utilities used for manifest/incremental exports.
    /// </summary>
    public static class HashHelper {
        /// <summary>
        /// Computes a SHA-256 hash for a string (UTF-8) and returns it as a lowercase hex string.
        /// </summary>
        /// <param name="content">Input text to hash. Must not be null.</param>
        /// <returns>Lowercase hex-encoded SHA-256 digest (64 characters).</returns>
        public static string Sha256Hex(string content) {
            if (content == null) throw new ArgumentNullException(nameof(content));

            // SHA256 is disposed via using to release any underlying crypto resources.
            using (var sha = SHA256.Create()) {
                // Hash the UTF-8 bytes of the provided string.
                var bytes = Encoding.UTF8.GetBytes(content);
                var hash = sha.ComputeHash(bytes);

                // Convert hash bytes to hex (two hex chars per byte).
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
        }
    }
}