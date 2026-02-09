/*
 * -------------------------------------------------------------------------
 *  TiaGitExporter
 * -------------------------------------------------------------------------
 *  Copyright (c) 2026 Eido Automation
 *  Version: v0.1
 *  License: MIT License
 *
 *  Description:
 *  Utility class used to control Windows Forms ProgressBar state colors
 *  through the Win32 API. Enables visual status feedback such as normal,
 *  error, and warning states during export/import operations.
 *
 *  Developed by: Eido Automation
 * -------------------------------------------------------------------------
 */

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TiaGitExporter {
    /// <summary>
    /// Provides helper methods to change the visual state color of a
    /// Windows Forms <see cref="ProgressBar"/> control using the Win32 API.
    /// </summary>
    /// <remarks>
    /// Supported on Windows 7 and later.
    ///
    /// States:
    /// • Normal  → Green
    /// • Error   → Red
    /// • Paused  → Yellow
    ///
    /// This is achieved by sending the PBM_SETSTATE message
    /// to the native progress bar handle.
    /// </remarks>
    internal static class ProgressBarColor {
        #region Win32 Constants

        /// <summary>
        /// Windows message used to set the state of a progress bar.
        /// PBM_SETSTATE = WM_USER (0x0400) + 16.
        /// </summary>
        private const int PBM_SETSTATE = 0x0410 + 16;

        /// <summary>
        /// Normal state (green).
        /// </summary>
        private const int PBST_NORMAL = 0x0001;

        /// <summary>
        /// Error state (red).
        /// </summary>
        private const int PBST_ERROR = 0x0002;

        /// <summary>
        /// Paused/warning state (yellow).
        /// </summary>
        private const int PBST_PAUSED = 0x0003;

        #endregion

        #region Win32 Interop

        /// <summary>
        /// Sends a message to a Windows control handle.
        /// Used here to modify the native progress bar state.
        /// </summary>
        /// <param name="hWnd">Handle of the control.</param>
        /// <param name="msg">Message identifier.</param>
        /// <param name="wParam">Message parameter.</param>
        /// <param name="lParam">Message parameter.</param>
        /// <returns>Result of the message processing.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam);

        #endregion

        #region Public API

        /// <summary>
        /// Sets the progress bar state to Normal (green).
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        public static void SetNormal(ProgressBar bar) {
            if (bar == null || bar.IsDisposed) return;
            TrySet(bar, PBST_NORMAL);
        }

        /// <summary>
        /// Sets the progress bar state to Error (red).
        /// Typically used when an export/import operation fails.
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        public static void SetError(ProgressBar bar) {
            if (bar == null || bar.IsDisposed) return;
            TrySet(bar, PBST_ERROR);
        }

        /// <summary>
        /// Sets the progress bar state to Warning/Paused (yellow).
        /// Useful for non-critical issues or partial completions.
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        public static void SetWarning(ProgressBar bar) {
            if (bar == null || bar.IsDisposed) return;
            TrySet(bar, PBST_PAUSED);
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Attempts to send the PBM_SETSTATE message safely.
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        /// <param name="state">Desired visual state.</param>
        private static void TrySet(ProgressBar bar, int state) {
            try {
                SendMessage(
                    bar.Handle,
                    PBM_SETSTATE,
                    (IntPtr)state,
                    IntPtr.Zero);
            } catch {
                // Silently ignore failures to avoid UI crashes.
                // Typically occurs if handle is not yet created.
            }
        }

        #endregion
    }
}