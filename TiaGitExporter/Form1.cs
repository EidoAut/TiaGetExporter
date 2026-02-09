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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using TiaGitExporter.Core.Export;
using TiaGitExporter.Core.Import;
using TiaGitExporter.Core.Model;
using TiaGitExporter.Core.Openness;
using TiaGitExporter.Core.Utilities;

namespace TiaGitExporter {
    /// <summary>
    /// Main WinForms UI for scanning, exporting, and importing TIA Portal artifacts
    /// into a Git-friendly repository structure.
    /// </summary>
    public partial class Form1 : Form {
        // NOTE:
        // This form mixes designer-driven controls (created by InitializeComponent)
        // with an optional runtime-built "modern layout" (SetupBottomChrome/SetupModernLayout).

        /// <summary>
        /// Optional main SplitContainer used by the runtime-built layout.
        /// In designer "free move" layout it can remain null.
        /// </summary>
        private SplitContainer _mainSplit;

        // Core services used by the UI layer.
        private readonly StaWorker _sta;
        private readonly TiaSessionService _session;
        private readonly PlcInventoryService _inventory;
        private readonly XmlNormalizeService _xmlNormalize;
        private readonly ManifestService _manifest;
        private readonly ExportService _export;
        private readonly ImportService _import;
        private readonly ILogger _logger;

        /// <summary>
        /// Cached result of the most recent scan (used to render the tree and export all).
        /// </summary>
        private List<ExportItem> _lastScan = new List<ExportItem>();

        /// <summary>
        /// Used to prevent recursive TreeView AfterCheck events while programmatically updating nodes.
        /// </summary>
        private bool _suppressTreeEvents;

        /// <summary>
        /// Icon list for TreeView node types (CPU, folder, UDT, DB, etc.).
        /// </summary>
        private ImageList _treeIcons;

        /// <summary>
        /// Progress bar shown during export/import operations.
        /// May be a designer control or a runtime-created control depending on layout.
        /// </summary>
        private ProgressBar _progress;

        /// <summary>
        /// Root panel used by runtime-built layout (optional).
        /// </summary>
        private Panel _mainPanel;

        // ===== Split safety =====
        // The SplitContainer can throw InvalidOperationException if min sizes and splitter distance
        // are applied before the handle is created or before the container has a valid size.
        private const int DesiredMinLeft = 220;
        private const int DesiredMinRight = 220;
        private const double DefaultSplitRatio = 0.38;
        private bool _splitInitialized;

        /// <summary>
        /// Initializes UI defaults and wires up core services.
        /// </summary>
        public Form1() {
            InitializeComponent();

            // Split safe init (avoid InvalidOperationException by applying after Shown/Resize)
            this.Shown += Form1_Shown;
            this.Resize += Form1_Resize;

            // Corporate-friendly default font and light surface.
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;

            // Tree icons (must be created before first tree render).
            SetupTreeIcons();

            // Make logo feel clickable (credits popup)
            pictureLogo.Cursor = Cursors.Hand;
            new ToolTip().SetToolTip(pictureLogo, "Créditos y licencia");

            // Persistent log file location (per-user, LocalAppData).
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TiaGitExporter", "logs", "app.log");
            _logger = new FileLogger(logPath);

            // Create core services once and reuse across operations.
            _sta = new StaWorker();
            _session = new TiaSessionService(_sta, _logger);
            _inventory = new PlcInventoryService(_session);
            _xmlNormalize = new XmlNormalizeService();
            _manifest = new ManifestService();
            _export = new ExportService(_session, _xmlNormalize, _manifest, _logger);
            _import = new ImportService(_session, _logger);

            // Populate profile dropdown from enum and select a sensible default.
            cmbProfile.DataSource = Enum.GetValues(typeof(ExportProfile));
            cmbProfile.SelectedItem = ExportProfile.Core;

            // Default repo folder under Documents.
            try {
                txtRepo.Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "TiaGitExports");
            } catch {
                // Ignore path failures (rare); user can still browse manually.
            }

            // Designer-driven layout (canvas style):
            // SplitContainer is not used in the "free move" designer layout.
            _mainSplit = null;

            // Use the designer progress bar by default unless runtime layout replaces it.
            _progress = progressExport;

            _mainPanel = null;
        }

        // ===== Designer expects these event handlers =====

        /// <summary>
        /// Form load hook kept for designer wiring and initial status text.
        /// </summary>
        private void Form1_Load(object sender, EventArgs e) {
            // Kept for designer wiring
            if (lblStatus != null) lblStatus.Text = "Ready";
        }

        /// <summary>
        /// Applies layout-sensitive SplitContainer settings after the form is shown and sized.
        /// </summary>
        private void Form1_Shown(object sender, EventArgs e) {
            this.PerformLayout();
            EnsureSplitInitialized();
        }

        /// <summary>
        /// Keeps SplitContainer split ratio stable on resize while respecting min sizes.
        /// </summary>
        private void Form1_Resize(object sender, EventArgs e) {
            EnsureSplitInitialized();
            SafeApplySplitRatio(DefaultSplitRatio, true);
        }

        /// <summary>
        /// Browse for a TIA Portal project file (.apXX).
        /// </summary>
        private async void btnBrowseProject_Click(object sender, EventArgs e) {
            using (var dlg = new OpenFileDialog()) {
                dlg.Filter = "TIA Project (*.ap*)|*.ap*|All files (*.*)|*.*";
                dlg.Title = "Select a TIA project file (.apXX)";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtProject.Text = dlg.FileName;
            }

            // Keep async signature without introducing additional work.
            await Task.CompletedTask;
        }

        /// <summary>
        /// Browse for the repository export root folder.
        /// </summary>
        private async void btnBrowseRepo_Click(object sender, EventArgs e) {
            using (var dlg = new FolderBrowserDialog()) {
                dlg.Description = "Select repository export root";
                if (Directory.Exists(txtRepo.Text)) dlg.SelectedPath = txtRepo.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtRepo.Text = dlg.SelectedPath;
            }

            // Keep async signature without introducing additional work.
            await Task.CompletedTask;
        }

        /// <summary>
        /// Opens the TIA Portal instance via Openness (optionally with UI).
        /// </summary>
        private async void btnOpenPortal_Click(object sender, EventArgs e) {
            SetBusy(true);
            try {
                Log("Opening TIA Portal...");
                await _session.OpenPortalAsync(chkPortalUi.Checked);
                Log("TIA Portal opened.");
            } catch (Exception ex) {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Open TIA failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                if (_progress != null) _progress.Visible = true;
                SetBusy(false);
            }
        }

        /// <summary>
        /// Opens the selected TIA project file in the current TIA session.
        /// </summary>
        private async void btnOpenProject_Click(object sender, EventArgs e) {
            SetBusy(true);
            try {
                if (string.IsNullOrWhiteSpace(txtProject.Text) || !File.Exists(txtProject.Text))
                    throw new FileNotFoundException("Project file not found", txtProject.Text);

                Log("Opening project...");
                await _session.OpenProjectAsync(txtProject.Text);
                Log("Project opened.");
            } catch (Exception ex) {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Open project failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                if (_progress != null) _progress.Visible = true;
                SetBusy(false);
            }
        }

        /// <summary>
        /// Scans the currently opened project for exportable artifacts and renders them in the TreeView.
        /// </summary>
        private async void btnScan_Click(object sender, EventArgs e) {
            SetBusy(true);
            try {
                var profile = (ExportProfile)cmbProfile.SelectedItem;
                var includeDbs = chkIncludeDbs.Checked;

                Log("Scanning exportables...");
                _lastScan = await _sta.InvokeAsync(() => _inventory.ScanExportables(profile, includeDbs));

                RenderTree(_lastScan);
                Log(string.Format("Scan complete: {0} items.", _lastScan.Count));
            } catch (Exception ex) {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                if (_progress != null) _progress.Visible = true;
                SetBusy(false);
            }
        }

        /// <summary>
        /// Re-renders the tree based on the current text filter, preserving checked nodes by LogicalKey.
        /// </summary>
        private void txtFilter_TextChanged(object sender, EventArgs e) {
            var checkedKeys = new HashSet<string>(
                GetCheckedItems().Select(i => i.LogicalKey),
                StringComparer.OrdinalIgnoreCase);

            RenderTree(_lastScan ?? new List<ExportItem>());
            RestoreCheckedState(checkedKeys);
        }

        /// <summary>
        /// Handles check/uncheck propagation for TreeView nodes:
        /// - checking a node checks all children
        /// - unchecking a node unchecks all children
        /// - parent nodes reflect whether any child is checked
        /// </summary>
        private void treeItems_AfterCheck(object sender, TreeViewEventArgs e) {
            if (_suppressTreeEvents) return;

            _suppressTreeEvents = true;
            try {
                // Children
                SetChildrenChecked(e.Node, e.Node.Checked);

                // Parents
                UpdateParentsChecked(e.Node);
            } finally {
                _suppressTreeEvents = false;
            }
        }

        /// <summary>
        /// Exports only the items currently checked in the TreeView.
        /// </summary>
        private async void btnExportSelected_Click(object sender, EventArgs e) {
            await DoExportAsync(GetCheckedItems());
        }

        /// <summary>
        /// Exports all items from the latest scan.
        /// </summary>
        private async void btnExportAll_Click(object sender, EventArgs e) {
            if (_lastScan == null || _lastScan.Count == 0) {
                MessageBox.Show(this, "Scan first.", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            await DoExportAsync(_lastScan);
        }

        /// <summary>
        /// Imports selected items from the repository folder into the currently opened TIA project.
        /// </summary>
        private async void btnImportSelected_Click(object sender, EventArgs e) {
            SetBusy(true);
            try {
                var items = GetCheckedItems();
                if (items.Count == 0) {
                    MessageBox.Show(this, "Select items to import (check the tree).", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtRepo.Text))
                    throw new ArgumentException("Repo folder is required.");

                Log(string.Format("Importing {0} items from repo...", items.Count));
                var res = await _sta.InvokeAsync(() => _import.Import(txtRepo.Text, items));
                Log(string.Format("Import done. Imported: {0}, errors: {1}", res.Imported, res.Errors.Count));

                foreach (var w in res.Warnings)
                    Log("WARN: " + w);

                foreach (var err in res.Errors)
                    Log("ERROR: " + err.ToString());
            } catch (Exception ex) {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                if (_progress != null) _progress.Visible = true;
                SetBusy(false);
            }
        }

        // NOTE:
        // Designer errors mention btnArchive_Click, but your method name in code is btnArchive_Click above.
        // If your designer expects btnArchive_Click, this matches.

        // =================== EXPORT ===================

        /// <summary>
        /// Executes an export operation for the provided items list.
        /// Updates progress bar and status label via callback.
        /// </summary>
        private async Task DoExportAsync(List<ExportItem> items) {
            SetBusy(true);
            try {
                if (string.IsNullOrWhiteSpace(txtRepo.Text))
                    throw new ArgumentException("Repo folder is required.");

                // Ensure the output directory exists.
                Directory.CreateDirectory(txtRepo.Text);

                Log(string.Format("Exporting {0} items...", items.Count));

                // Initialize progress bar.
                _progress.Visible = true;
                _progress.Minimum = 0;
                _progress.Maximum = Math.Max(1, items.Count);
                _progress.Value = 0;

                // Reset to neutral color at the start of a run.
                ProgressBarColor.SetNormal(_progress);

                bool normalize = chkNormalizeXml.Checked;
                bool incremental = chkIncremental.Checked;

                // Run export in STA context (TIA Openness requires STA).
                var res = await _sta.InvokeAsync(() => _export.Export(
                    txtRepo.Text,
                    items,
                    normalize,
                    incremental,
                    (done, total, current, ex) => {
                        try {
                            // Marshal progress updates onto the UI thread.
                            BeginInvoke((Action)(() => {
                                _progress.Maximum = Math.Max(1, total);
                                _progress.Value = Math.Min(done, _progress.Maximum);
                                lblStatus.Text = string.Format("{0}/{1}  {2}",
                                    done,
                                    total,
                                    current != null ? current.ToString() : "");

                                // Color the bar based on current warning/error state.
                                if (ex != null && string.Equals(ex.GetType().Name, "ExportWarningException", StringComparison.Ordinal))
                                    ProgressBarColor.SetWarning(_progress);
                                else if (ex != null)
                                    ProgressBarColor.SetError(_progress);
                            }));
                        } catch {
                            // Ignore UI update failures (e.g., during shutdown).
                        }
                    }));

                // Summarize results.
                Log(string.Format("Export done. Exported: {0}, skipped: {1}, warnings: {2}, errors: {3}",
                    res.Exported, res.Skipped, res.Warnings.Count, res.Errors.Count));

                if (!string.IsNullOrWhiteSpace(res.OutputRoot))
                    Log("OK: Output folder: " + res.OutputRoot);

                foreach (var w in res.Warnings)
                    Log("WARN: " + w.ToString());

                foreach (var err in res.Errors)
                    Log("ERROR: " + err.ToString());

                // Final progress color based on overall outcome.
                if (res.Errors.Count > 0) ProgressBarColor.SetError(_progress);
                else if (res.Warnings.Count > 0) ProgressBarColor.SetWarning(_progress);
                else ProgressBarColor.SetNormal(_progress);

                lblStatus.Text = string.Format("Done. Exported: {0}, skipped: {1}, warnings: {2}, errors: {3}",
                    res.Exported, res.Skipped, res.Warnings.Count, res.Errors.Count);
            } catch (Exception ex) {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                _progress.Visible = true;
                SetBusy(false);
            }
        }

        // =================== TREE ===================

        /// <summary>
        /// Renders exportable items into a TreeView grouped by CPU path and group folders.
        /// Applies the UI filter if present.
        /// </summary>
        private void RenderTree(List<ExportItem> items) {
            var filter = (txtFilter != null ? txtFilter.Text : "").Trim();
            if (!string.IsNullOrWhiteSpace(filter)) {
                // Filter by common searchable fields.
                items = items.Where(i =>
                        (i.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                        || (i.GroupPath ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                        || (i.CpuPath ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                        || i.Kind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            treeItems.BeginUpdate();
            try {
                treeItems.Nodes.Clear();

                // Top-level grouping: CPU path.
                var cpuGroups = items.GroupBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var cpu in cpuGroups) {
                    var cpuNode = new TreeNode(cpu.Key) {
                        Tag = null,
                        ImageKey = "cpu",
                        SelectedImageKey = "cpu"
                    };

                    // Within each CPU, build folder paths (GroupPath) and leaf items.
                    foreach (var item in cpu.OrderBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                                 .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)) {
                        var rel = (item.GroupPath ?? "").Trim();
                        var groupNode = EnsurePathNode(cpuNode, rel);

                        var leafIcon = GetKindIconKey(item.Kind);
                        var leaf = new TreeNode(item.Name) {
                            Tag = item,
                            ImageKey = leafIcon,
                            SelectedImageKey = leafIcon
                        };

                        groupNode.Nodes.Add(leaf);
                    }

                    cpuNode.Expand();
                    treeItems.Nodes.Add(cpuNode);
                }
            } finally {
                treeItems.EndUpdate();
            }
        }

        /// <summary>
        /// Ensures folder nodes exist for a given path under a root node, returning the deepest node.
        /// </summary>
        private TreeNode EnsurePathNode(TreeNode root, string path) {
            var parts = path.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            TreeNode current = root;

            foreach (var p in parts) {
                // Find existing folder node with matching text.
                TreeNode next = null;
                foreach (TreeNode n in current.Nodes) {
                    if (n.Tag == null && string.Equals(n.Text, p, StringComparison.OrdinalIgnoreCase)) {
                        next = n;
                        break;
                    }
                }

                // Create folder node if missing.
                if (next == null) {
                    next = new TreeNode(p) {
                        Tag = null,
                        ImageKey = "folder",
                        SelectedImageKey = "folder_open"
                    };
                    current.Nodes.Add(next);
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Maps export kind to TreeView icon key.
        /// </summary>
        private static string GetKindIconKey(ExportKind kind) {
            switch (kind) {
                case ExportKind.Udt: return "udt";
                case ExportKind.PlcTagTable: return "tag";
                case ExportKind.Ob: return "ob";
                case ExportKind.Fb: return "fb";
                case ExportKind.Fc: return "fc";
                case ExportKind.Db: return "db";
                default: return "file";
            }
        }

        /// <summary>
        /// Returns a flat list of all checked ExportItem nodes in the TreeView.
        /// </summary>
        private List<ExportItem> GetCheckedItems() {
            var result = new List<ExportItem>();

            Action<TreeNode> walk = null;
            walk = (node) => {
                if (node.Checked && node.Tag is ExportItem)
                    result.Add((ExportItem)node.Tag);

                foreach (TreeNode c in node.Nodes)
                    walk(c);
            };

            foreach (TreeNode n in treeItems.Nodes)
                walk(n);

            return result;
        }

        /// <summary>
        /// Reapplies checked state after re-rendering the TreeView, based on item LogicalKey.
        /// </summary>
        private void RestoreCheckedState(HashSet<string> checkedKeys) {
            if (checkedKeys == null || checkedKeys.Count == 0) return;

            _suppressTreeEvents = true;
            try {
                Action<TreeNode> walk = null;
                walk = (n) => {
                    if (n.Tag is ExportItem) {
                        var it = (ExportItem)n.Tag;
                        n.Checked = checkedKeys.Contains(it.LogicalKey);
                    }
                    foreach (TreeNode c in n.Nodes) walk(c);
                };

                foreach (TreeNode n in treeItems.Nodes)
                    walk(n);
            } finally {
                _suppressTreeEvents = false;
            }
        }

        /// <summary>
        /// Recursively sets checked state for a node's descendants.
        /// </summary>
        private void SetChildrenChecked(TreeNode n, bool value) {
            foreach (TreeNode c in n.Nodes) {
                c.Checked = value;
                SetChildrenChecked(c, value);
            }
        }

        /// <summary>
        /// Updates parent nodes so they become checked if any child is checked.
        /// </summary>
        private void UpdateParentsChecked(TreeNode n) {
            var p = n.Parent;
            if (p == null) return;

            bool anyChecked = false;
            foreach (TreeNode c in p.Nodes)
                anyChecked |= c.Checked;

            p.Checked = anyChecked;
            UpdateParentsChecked(p);
        }

        // =================== UI HELPERS ===================

        /// <summary>
        /// Enables/disables UI buttons to prevent concurrent operations.
        /// </summary>
        private void SetBusy(bool busy) {
            btnOpenPortal.Enabled = !busy;
            btnOpenProject.Enabled = !busy;
            btnScan.Enabled = !busy;
            btnExportSelected.Enabled = !busy;
            btnExportAll.Enabled = !busy;
            btnImportSelected.Enabled = !busy;
        }

        /// <summary>
        /// Writes a timestamped line to the log RichTextBox with basic severity coloring,
        /// and mirrors the last message to the status label.
        /// </summary>
        private void Log(string message) {
            if (InvokeRequired) {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }

            // Simple, convention-based coloring:
            // - "ERROR:" -> red
            // - "WARN:" / "WARNING:" -> orange
            // - "OK:" / "SUCCESS:" -> green
            // - default -> control foreground
            var prefix = (message ?? "").TrimStart();
            Color? color = null;
            if (prefix.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                color = Color.Firebrick;
            else if (prefix.StartsWith("WARN:", StringComparison.OrdinalIgnoreCase) ||
                     prefix.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                color = Color.DarkOrange;
            else if (prefix.StartsWith("OK:", StringComparison.OrdinalIgnoreCase) ||
                     prefix.StartsWith("SUCCESS:", StringComparison.OrdinalIgnoreCase))
                color = Color.SeaGreen;

            var line = string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine);

            // RichTextBox supports colored appends.
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = color ?? txtLog.ForeColor;
            txtLog.AppendText(line);
            txtLog.SelectionColor = txtLog.ForeColor;
            txtLog.ScrollToCaret();

            // Keep status label synchronized with the most recent message.
            if (lblStatus != null) lblStatus.Text = message;
        }

        /// <summary>
        /// Displays a minimal "About" dialog for credits and license.
        /// </summary>
        private void ShowAbout() {
            MessageBox.Show(
                "Eido Automation\r\n\r\nTIA Git Exporter\r\n\r\nLicensed under the MIT License.",
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // =================== SPLIT SAFE ===================

        /// <summary>
        /// Initializes SplitContainer min sizes and initial splitter ratio safely.
        /// Must be called only after the control has a handle and a valid size.
        /// </summary>
        private void EnsureSplitInitialized() {
            if (_mainSplit == null || _splitInitialized) return;
            if (!_mainSplit.IsHandleCreated) return;

            int total = _mainSplit.ClientSize.Width;
            if (total <= 0) return;

            // The minimum sizes must never exceed half the available width,
            // otherwise SplitContainer can throw or behave unexpectedly.
            int available = Math.Max(0, total - _mainSplit.SplitterWidth);
            int safeMin = Math.Max(0, available / 2);

            int minLeft = Math.Min(DesiredMinLeft, safeMin);
            int minRight = Math.Min(DesiredMinRight, safeMin);

            _mainSplit.Panel1MinSize = minLeft;
            _mainSplit.Panel2MinSize = minRight;

            SafeApplySplitRatio(DefaultSplitRatio, false);
            _splitInitialized = true;
        }

        /// <summary>
        /// Applies or clamps SplitContainer SplitterDistance within min/max bounds.
        /// </summary>
        private void SafeApplySplitRatio(double ratio, bool clampOnly) {
            if (_mainSplit == null) return;

            int total = _mainSplit.ClientSize.Width;
            if (total <= 0) return;

            int min = _mainSplit.Panel1MinSize;
            int max = total - _mainSplit.Panel2MinSize;
            if (max < min) return;

            int desired = clampOnly
                ? _mainSplit.SplitterDistance
                : (int)Math.Round(total * ratio);

            int clamped = Math.Max(min, Math.Min(desired, max));

            if (_mainSplit.SplitterDistance != clamped)
                _mainSplit.SplitterDistance = clamped;
        }

        // =================== LAYOUT BUILD ===================

        /// <summary>
        /// Builds a runtime "modern" layout using TableLayoutPanel and SplitContainer.
        /// This is optional and only applied if _mainPanel is present.
        /// </summary>
        private void SetupModernLayout() {
            if (_mainPanel == null) return;

            // Keep references to designer controls (do not recreate them).
            var projectPath = txtProject;
            var browseProject = btnBrowseProject;
            var tiaWithUi = chkPortalUi;
            var openTia = btnOpenPortal;
            var openProject = btnOpenProject;
            var scan = btnScan;

            var profile = cmbProfile;
            var includeDbs = chkIncludeDbs;
            var normalizeXml = chkNormalizeXml;
            var incremental = chkIncremental;

            var repoPath = txtRepo;
            var browseRepo = btnBrowseRepo;

            var filterLabel = lblFilter;
            var filterBox = txtFilter;
            var tree = treeItems;

            var exportSelected = btnExportSelected;
            var exportAll = btnExportAll;
            var importSelected = btnImportSelected;

            var log = txtLog;

            _mainPanel.Controls.Clear();

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Header container: project and settings.
            var header = new GroupBox {
                Text = "Project & Settings",
                Dock = DockStyle.Top,
                Padding = new Padding(12)
            };

            var headerGrid = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                AutoSize = true
            };
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblProject = new Label { Text = "Project", AutoSize = true, Anchor = AnchorStyles.Left };
            var lblRepo = new Label { Text = "Repo", AutoSize = true, Anchor = AnchorStyles.Left };
            var lblProfile = new Label { Text = "Profile", AutoSize = true, Anchor = AnchorStyles.Left };

            projectPath.Dock = DockStyle.Fill;
            browseProject.Dock = DockStyle.Fill;
            repoPath.Dock = DockStyle.Fill;
            browseRepo.Dock = DockStyle.Fill;

            headerGrid.Controls.Add(lblProject, 0, 0);
            headerGrid.Controls.Add(projectPath, 1, 0);
            headerGrid.Controls.Add(browseProject, 2, 0);

            var about = new Button { Text = "About", AutoSize = true, Dock = DockStyle.Fill };
            about.Click += (s, e) => ShowAbout();
            headerGrid.Controls.Add(about, 3, 0);

            headerGrid.Controls.Add(lblRepo, 0, 1);
            headerGrid.Controls.Add(repoPath, 1, 1);
            headerGrid.Controls.Add(browseRepo, 2, 1);

            // Settings row (checkboxes + profile selector).
            var settingsFlow = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 6, 0, 0)
            };

            tiaWithUi.AutoSize = true;
            normalizeXml.AutoSize = true;
            incremental.AutoSize = true;
            includeDbs.AutoSize = true;

            profile.Width = 160;

            settingsFlow.Controls.Add(tiaWithUi);
            settingsFlow.Controls.Add(normalizeXml);
            settingsFlow.Controls.Add(incremental);
            settingsFlow.Controls.Add(includeDbs);
            settingsFlow.Controls.Add(new Label { Text = "   ", AutoSize = true });
            settingsFlow.Controls.Add(lblProfile);
            settingsFlow.Controls.Add(profile);

            // Action buttons row (open TIA, open project, scan).
            var actionsFlow = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 6, 0, 0)
            };
            actionsFlow.Controls.Add(openTia);
            actionsFlow.Controls.Add(openProject);
            actionsFlow.Controls.Add(scan);

            headerGrid.Controls.Add(settingsFlow, 0, 2);
            headerGrid.SetColumnSpan(settingsFlow, 2);
            headerGrid.Controls.Add(actionsFlow, 2, 2);
            headerGrid.SetColumnSpan(actionsFlow, 2);

            header.Controls.Add(headerGrid);

            // Body Split (IMPORTANT: do NOT set Panel1MinSize/Panel2MinSize here)
            // These are set later in EnsureSplitInitialized once the control is sized.
            _mainSplit = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };

            // Left side: filter + tree.
            var left = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            filterLabel.Margin = new Padding(0, 6, 8, 6);
            filterBox.Dock = DockStyle.Fill;
            filterBox.Margin = new Padding(0, 4, 0, 4);
            tree.Dock = DockStyle.Fill;
            tree.BorderStyle = BorderStyle.FixedSingle;

            left.Controls.Add(filterLabel, 0, 0);
            left.Controls.Add(filterBox, 1, 0);
            left.Controls.Add(tree, 0, 1);
            left.SetColumnSpan(tree, 2);

            _mainSplit.Panel1.Padding = new Padding(0, 0, 8, 0);
            _mainSplit.Panel1.Controls.Add(left);

            // Right side: actions + archive placeholder + log.
            var right = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _mainSplit.Panel2.Padding = new Padding(8, 0, 0, 0);

            var grpActions = new GroupBox { Text = "Actions", Dock = DockStyle.Top, Padding = new Padding(12) };
            var actionsStack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoSize = true };
            actionsStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionsStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            exportSelected.Dock = DockStyle.Top;
            exportAll.Dock = DockStyle.Top;
            importSelected.Dock = DockStyle.Top;

            actionsStack.Controls.Add(exportSelected, 0, 0);
            actionsStack.Controls.Add(exportAll, 0, 1);
            actionsStack.Controls.Add(importSelected, 0, 2);
            grpActions.Controls.Add(actionsStack);

            // Archive group is currently a placeholder in this snippet.
            var grpArchive = new GroupBox { Text = "Archive", Dock = DockStyle.Top, Padding = new Padding(12) };
            var archiveGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, AutoSize = true };
            archiveGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            archiveGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grpArchive.Controls.Add(archiveGrid);

            var grpLog = new GroupBox { Text = "Log", Dock = DockStyle.Fill, Padding = new Padding(12) };
            log.Dock = DockStyle.Fill;
            log.BorderStyle = BorderStyle.FixedSingle;
            grpLog.Controls.Add(log);

            right.Controls.Add(grpActions, 0, 0);
            right.Controls.Add(grpArchive, 0, 1);
            right.Controls.Add(grpLog, 0, 2);

            _mainSplit.Panel2.Controls.Add(right);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_mainSplit, 0, 1);
            _mainPanel.Controls.Add(root);

            // Provide a reasonable minimum window size for this layout.
            MinimumSize = new Size(980, 680);
        }

        /// <summary>
        /// Wraps the existing form content with a bottom "chrome" area:
        /// progress bar + status strip, and moves the prior controls into _mainPanel.
        /// </summary>
        private void SetupBottomChrome() {
            var existing = Controls.Cast<Control>().ToList();
            Controls.Clear();

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _mainPanel = new Panel { Dock = DockStyle.Fill };

            var bottomPanel = new Panel {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 2, 6, 2)
            };

            // Runtime-created progress bar used by export/import operations.
            _progress = new ProgressBar {
                Dock = DockStyle.Top,
                Height = 14,
                Visible = true,
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            // Configure status strip if present.
            if (statusStrip != null) {
                statusStrip.Dock = DockStyle.Bottom;
                statusStrip.SizingGrip = false;
                statusStrip.AutoSize = false;
                statusStrip.Height = 32;
                statusStrip.Padding = new Padding(6, 0, 6, 0);
            }

            // Move existing controls into the main panel (except status strip).
            foreach (var c in existing) {
                if (c == statusStrip) continue;
                _mainPanel.Controls.Add(c);
            }

            bottomPanel.Controls.Add(_progress);
            if (statusStrip != null)
                bottomPanel.Controls.Add(statusStrip);

            root.Controls.Add(_mainPanel, 0, 0);
            root.Controls.Add(bottomPanel, 0, 1);
            Controls.Add(root);
        }

        /// <summary>
        /// Ensures session resources are released on close (TIA portal and STA worker).
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e) {
            base.OnFormClosing(e);

            // Dispose defensively: shutdown may already be in progress.
            try { _session.Dispose(); } catch { }
            try { _sta.Dispose(); } catch { }
        }

        // ================= ICONS =================

        /// <summary>
        /// Creates and assigns the TreeView ImageList, including custom badge icons for artifact kinds.
        /// </summary>
        private void SetupTreeIcons() {
            _treeIcons = new ImageList {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit
            };

            // Base icons.
            _treeIcons.Images.Add("cpu", SystemIcons.Application.ToBitmap());
            _treeIcons.Images.Add("folder", CreateFolderIcon(false));
            _treeIcons.Images.Add("folder_open", CreateFolderIcon(true));
            _treeIcons.Images.Add("file", CreateDocIcon());

            // Artifact kind icons.
            _treeIcons.Images.Add("udt", CreateBadgeIcon("UDT"));
            _treeIcons.Images.Add("tag", CreateBadgeIcon("TAG"));
            _treeIcons.Images.Add("ob", CreateBadgeIcon("OB"));
            _treeIcons.Images.Add("fb", CreateBadgeIcon("FB"));
            _treeIcons.Images.Add("fc", CreateBadgeIcon("FC"));
            _treeIcons.Images.Add("db", CreateBadgeIcon("DB"));

            treeItems.ImageList = _treeIcons;
        }

        /// <summary>
        /// Creates a small 16x16 badge icon with a short label (e.g., "UDT", "DB").
        /// </summary>
        private static Bitmap CreateBadgeIcon(string text) {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent);

                using (var bg = new SolidBrush(Color.FromArgb(235, 235, 235)))
                using (var border = new Pen(Color.FromArgb(120, 120, 120))) {
                    g.FillRectangle(bg, 1, 2, 14, 12);
                    g.DrawRectangle(border, 1, 2, 14, 12);
                }

                using (var f = new Font("Segoe UI", 6.5f, FontStyle.Bold, GraphicsUnit.Point))
                using (var br = new SolidBrush(Color.Black)) {
                    var sf = new StringFormat {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.DrawString(text, f, br, new RectangleF(0, 1, 16, 14), sf);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Creates a minimal folder icon; open==true draws an "open" folder variant.
        /// </summary>
        private static Bitmap CreateFolderIcon(bool open) {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent);
                using (var fill = new SolidBrush(Color.FromArgb(245, 221, 144)))
                using (var fill2 = new SolidBrush(Color.FromArgb(238, 205, 110)))
                using (var border = new Pen(Color.FromArgb(160, 130, 60))) {
                    if (open) {
                        g.FillRectangle(fill2, 2, 6, 12, 8);
                        g.DrawRectangle(border, 2, 6, 12, 8);
                        g.FillRectangle(fill, 2, 4, 8, 4);
                        g.DrawRectangle(border, 2, 4, 8, 4);
                    } else {
                        g.FillRectangle(fill2, 2, 5, 12, 9);
                        g.DrawRectangle(border, 2, 5, 12, 9);
                        g.FillRectangle(fill, 2, 3, 7, 3);
                        g.DrawRectangle(border, 2, 3, 7, 3);
                    }
                }
            }
            return bmp;
        }

        /// <summary>
        /// Creates a minimal "document" icon (white page with grey lines).
        /// </summary>
        private static Bitmap CreateDocIcon() {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent);
                using (var fill = new SolidBrush(Color.White))
                using (var border = new Pen(Color.FromArgb(120, 120, 120)))
                using (var line = new Pen(Color.FromArgb(180, 180, 180))) {
                    g.FillRectangle(fill, 3, 2, 10, 12);
                    g.DrawRectangle(border, 3, 2, 10, 12);
                    g.DrawLine(line, 5, 6, 11, 6);
                    g.DrawLine(line, 5, 8, 11, 8);
                    g.DrawLine(line, 5, 10, 11, 10);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Logo click handler: shows license/credits dialog.
        /// </summary>
        private void pictureLogo_Click(object sender, EventArgs e) {
            MessageBox.Show(
                "2026 Eido Automation\n" +
                "MIT License.",
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }
}
