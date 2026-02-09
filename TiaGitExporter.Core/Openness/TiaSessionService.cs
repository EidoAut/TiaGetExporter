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

using Siemens.Engineering;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaGitExporter.Core.Utilities;

namespace TiaGitExporter.Core.Openness {

    /// <summary>
    /// Manages the lifecycle of a TIA Portal session and project using the
    /// Openness API.
    ///
    /// Responsibilities:
    /// - Start or attach to a running TIA Portal instance
    /// - Open / close projects
    /// - Archive projects
    /// - Ensure all calls are executed on the STA thread required by Openness
    ///
    /// The service can either own the TIA Portal instance (created by this
    /// process) or attach to an existing one.
    /// </summary>
    public sealed class TiaSessionService : IDisposable {

        /// <summary>
        /// STA worker used to marshal all Openness calls onto the required
        /// Single-Threaded Apartment thread.
        /// </summary>
        private readonly StaWorker _sta;

        /// <summary>
        /// Logger instance used for diagnostic and operational messages.
        /// </summary>
        private readonly ILogger _log;

        /// <summary>
        /// Active TIA Portal instance.
        /// </summary>
        private TiaPortal _tiaPortal;

        /// <summary>
        /// Currently opened project within the TIA Portal session.
        /// </summary>
        private Project _project;

        /// <summary>
        /// Indicates whether this service created (owns) the TIA Portal instance.
        /// If false, disposal will not close the external instance.
        /// </summary>
        private bool _ownsPortal;

        /// <summary>
        /// Disposal state flag.
        /// </summary>
        private bool _disposed;

        public TiaSessionService(StaWorker staWorker)
            : this(staWorker, NullLogger.Instance) { }

        public TiaSessionService(StaWorker staWorker, ILogger logger) {
            _sta = staWorker ?? throw new ArgumentNullException(nameof(staWorker));
            _log = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Indicates whether a TIA Portal instance is currently attached/open.
        /// </summary>
        public bool IsPortalOpen => _tiaPortal != null;

        /// <summary>
        /// Indicates whether a project is currently open.
        /// </summary>
        public bool IsProjectOpen => _project != null;

        // ------------------------------------------------------------------
        // OPEN / ATTACH PORTAL
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens a TIA Portal instance or attaches to an existing running one.
        ///
        /// Strategy:
        /// 1) Attempt to attach to a running instance (preferred).
        /// 2) If none available, start a new instance.
        ///
        /// When attaching, the service will try to adopt an already open
        /// project if found.
        /// </summary>
        public Task OpenPortalAsync(bool withUserInterface) {
            EnsureNotDisposed();

            return _sta.InvokeAsync(() => {
                if (_tiaPortal != null)
                    return true;

                // -----------------------------
                // 1) Try attach to running TIA
                // -----------------------------
                try {
                    var processes = TiaPortal.GetProcesses();

                    if (processes != null && processes.Count > 0) {
                        TiaPortal attached = null;

                        foreach (var p in processes) {
                            try {
                                // Attempt attachment
                                var t = p.Attach();
                                var prj = t.Projects.FirstOrDefault();

                                if (prj != null) {
                                    // Prefer instances with an open project
                                    attached = t;
                                    _project = prj;
                                    break;
                                }

                                // No project → dispose and try next
                                t.Dispose();
                            } catch {
                                // Ignore attach failures (process busy, permissions, etc.)
                            }
                        }

                        // Fallback: attach to first available instance
                        if (attached == null) {
                            attached = processes[0].Attach();
                            _project = attached.Projects.FirstOrDefault();
                        }

                        _tiaPortal = attached;
                        _ownsPortal = false;

                        _log.Info("Attached to existing TIA Portal instance.");

                        if (_project != null)
                            _log.Info("Adopted open project: " + _project.Name);

                        return true;
                    }
                } catch (Exception ex) {
                    _log.Warn("Attach failed. Starting new TIA. " + ex.Message);
                }

                // -----------------------------
                // 2) Start new instance
                // -----------------------------
                _tiaPortal = new TiaPortal(
                    withUserInterface
                        ? TiaPortalMode.WithUserInterface
                        : TiaPortalMode.WithoutUserInterface);

                _ownsPortal = true;

                _log.Info("Started new TIA Portal instance.");
                return true;
            });
        }

        // ------------------------------------------------------------------
        // PROJECT MANAGEMENT
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens a TIA Portal project from disk.
        ///
        /// If another project is already open, it will be closed first.
        /// </summary>
        public Task OpenProjectAsync(string projectPath) {
            EnsureNotDisposed();

            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Project path is required.", nameof(projectPath));

            var fi = new FileInfo(projectPath);

            return _sta.InvokeAsync(() => {
                EnsurePortalOpenOnSta();

                CloseProjectInternal();
                _project = _tiaPortal.Projects.Open(fi);

                _log.Info("Opened project: " + _project.Name);
                return true;
            });
        }

        /// <summary>
        /// Closes the currently open project, if any.
        /// </summary>
        public Task CloseProjectAsync() {
            EnsureNotDisposed();

            return _sta.InvokeAsync(() => {
                CloseProjectInternal();
                return true;
            });
        }

        // ------------------------------------------------------------------
        // ARCHIVE
        // ------------------------------------------------------------------

        /// <summary>
        /// Archives the currently open project to a .zap archive.
        ///
        /// The archive directory will be created if it does not exist.
        /// </summary>
        public Task ArchiveProjectAsync(string archivePath) {
            EnsureNotDisposed();

            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));

            var fi = new FileInfo(archivePath);

            return _sta.InvokeAsync(() => {
                EnsureProjectOpenOnSta();

                var dir = new DirectoryInfo(fi.DirectoryName ?? ".");
                Directory.CreateDirectory(dir.FullName);

                var targetName = Path.GetFileNameWithoutExtension(fi.Name);
                if (string.IsNullOrWhiteSpace(targetName))
                    targetName = _project.Name;

                // ProjectArchivationMode = 0 → default archive behavior
                _project.Archive(dir, targetName, (ProjectArchivationMode)0);

                _log.Info("Project archived: " + targetName);
                return true;
            });
        }

        // ------------------------------------------------------------------
        // INTERNAL ACCESS
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the currently open project (STA thread only).
        ///
        /// Revalidates that the project is still open in case the user
        /// manually closed it from the TIA UI.
        /// </summary>
        internal Project GetOpenProjectOnSta() {
            EnsureProjectOpenOnSta();

            try {
                // Accessing Name forces Openness to validate the handle
                var _ = _project.Name;
            } catch {
                _project = null;
                throw new InvalidOperationException("Project was closed in TIA Portal.");
            }

            return _project;
        }

        // ------------------------------------------------------------------
        // INTERNAL HELPERS
        // ------------------------------------------------------------------

        /// <summary>
        /// Closes the project without thread marshaling.
        /// Must be called from STA context.
        /// </summary>
        private void CloseProjectInternal() {
            if (_project != null) {
                try { _project.Close(); } finally { _project = null; }
            }
        }

        /// <summary>
        /// Ensures a TIA Portal instance is open.
        /// </summary>
        private void EnsurePortalOpenOnSta() {
            if (_tiaPortal == null)
                throw new InvalidOperationException(
                    "TIA Portal is not open. Call OpenPortalAsync() first.");
        }

        /// <summary>
        /// Ensures a project is open.
        /// </summary>
        private void EnsureProjectOpenOnSta() {
            EnsurePortalOpenOnSta();

            if (_project == null)
                throw new InvalidOperationException(
                    "No project is open.");
        }

        /// <summary>
        /// Throws if the service has been disposed.
        /// </summary>
        private void EnsureNotDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TiaSessionService));
        }

        // ------------------------------------------------------------------
        // DISPOSE
        // ------------------------------------------------------------------

        /// <summary>
        /// Disposes the TIA session safely.
        ///
        /// Behavior:
        /// - Closes any open project
        /// - Disposes the TIA Portal instance only if owned
        /// - Detaches otherwise
        /// </summary>
        public void Dispose() {
            if (_disposed) return;
            _disposed = true;

            try {
                _sta.InvokeAsync(() => {
                    CloseProjectInternal();

                    if (_tiaPortal != null) {
                        if (_ownsPortal) {
                            _log.Info("Disposing owned TIA Portal instance.");
                            _tiaPortal.Dispose();
                        } else {
                            _log.Info("Detached from TIA Portal (not owner).");
                        }

                        _tiaPortal = null;
                    }

                    return true;
                }).GetAwaiter().GetResult();
            } catch (Exception ex) {
                _log.Error("Failed to dispose TIA session cleanly.", ex);
            }
        }
    }
}
