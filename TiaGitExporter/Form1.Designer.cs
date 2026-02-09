namespace TiaGitExporter
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // Top containers
        private System.Windows.Forms.Panel pnlTop;
        private System.Windows.Forms.GroupBox grpProjectSettings;
        private System.Windows.Forms.GroupBox grpActions;

        // Main area (single canvas container for free move/resize)
        private System.Windows.Forms.Panel pnlMainArea;
        private System.Windows.Forms.Label lblFilter;
        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.TreeView treeItems;
        private System.Windows.Forms.GroupBox grpLog;
        private System.Windows.Forms.RichTextBox txtLog;

        // Project settings controls
        private System.Windows.Forms.Label lblProject;
        private System.Windows.Forms.TextBox txtProject;
        private System.Windows.Forms.Button btnBrowseProject;
        private System.Windows.Forms.Label lblRepo;
        private System.Windows.Forms.TextBox txtRepo;
        private System.Windows.Forms.Button btnBrowseRepo;
        private System.Windows.Forms.CheckBox chkPortalUi;
        private System.Windows.Forms.CheckBox chkNormalizeXml;
        private System.Windows.Forms.CheckBox chkIncremental;
        private System.Windows.Forms.CheckBox chkIncludeDbs;
        private System.Windows.Forms.Label lblProfile;
        private System.Windows.Forms.ComboBox cmbProfile;

        // Action buttons
        private System.Windows.Forms.Button btnOpenPortal;
        private System.Windows.Forms.Button btnOpenProject;
        private System.Windows.Forms.Button btnScan;
        private System.Windows.Forms.Button btnExportSelected;
        private System.Windows.Forms.Button btnExportAll;
        private System.Windows.Forms.Button btnImportSelected;

        // Bottom chrome
        private System.Windows.Forms.ProgressBar progressExport;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.pnlTop = new System.Windows.Forms.Panel();
            this.grpActions = new System.Windows.Forms.GroupBox();
            this.btnOpenPortal = new System.Windows.Forms.Button();
            this.btnOpenProject = new System.Windows.Forms.Button();
            this.btnScan = new System.Windows.Forms.Button();
            this.btnExportSelected = new System.Windows.Forms.Button();
            this.btnExportAll = new System.Windows.Forms.Button();
            this.btnImportSelected = new System.Windows.Forms.Button();
            this.grpProjectSettings = new System.Windows.Forms.GroupBox();
            this.lblProject = new System.Windows.Forms.Label();
            this.txtProject = new System.Windows.Forms.TextBox();
            this.btnBrowseProject = new System.Windows.Forms.Button();
            this.lblRepo = new System.Windows.Forms.Label();
            this.txtRepo = new System.Windows.Forms.TextBox();
            this.btnBrowseRepo = new System.Windows.Forms.Button();
            this.chkPortalUi = new System.Windows.Forms.CheckBox();
            this.chkNormalizeXml = new System.Windows.Forms.CheckBox();
            this.chkIncremental = new System.Windows.Forms.CheckBox();
            this.chkIncludeDbs = new System.Windows.Forms.CheckBox();
            this.lblProfile = new System.Windows.Forms.Label();
            this.cmbProfile = new System.Windows.Forms.ComboBox();
            this.pnlMainArea = new System.Windows.Forms.Panel();
            this.grpLog = new System.Windows.Forms.GroupBox();
            this.pictureLogo = new System.Windows.Forms.PictureBox();
            this.txtLog = new System.Windows.Forms.RichTextBox();
            this.treeItems = new System.Windows.Forms.TreeView();
            this.txtFilter = new System.Windows.Forms.TextBox();
            this.lblFilter = new System.Windows.Forms.Label();
            this.progressExport = new System.Windows.Forms.ProgressBar();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.pnlTop.SuspendLayout();
            this.grpActions.SuspendLayout();
            this.grpProjectSettings.SuspendLayout();
            this.pnlMainArea.SuspendLayout();
            this.grpLog.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureLogo)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlTop
            // 
            this.pnlTop.Controls.Add(this.grpActions);
            this.pnlTop.Controls.Add(this.grpProjectSettings);
            this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTop.Location = new System.Drawing.Point(0, 0);
            this.pnlTop.Margin = new System.Windows.Forms.Padding(2);
            this.pnlTop.Name = "pnlTop";
            this.pnlTop.Padding = new System.Windows.Forms.Padding(9, 8, 9, 6);
            this.pnlTop.Size = new System.Drawing.Size(910, 122);
            this.pnlTop.TabIndex = 0;
            // 
            // grpActions
            // 
            this.grpActions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.grpActions.Controls.Add(this.btnOpenPortal);
            this.grpActions.Controls.Add(this.btnOpenProject);
            this.grpActions.Controls.Add(this.btnScan);
            this.grpActions.Controls.Add(this.btnExportSelected);
            this.grpActions.Controls.Add(this.btnExportAll);
            this.grpActions.Controls.Add(this.btnImportSelected);
            this.grpActions.Location = new System.Drawing.Point(580, 8);
            this.grpActions.Margin = new System.Windows.Forms.Padding(2);
            this.grpActions.Name = "grpActions";
            this.grpActions.Padding = new System.Windows.Forms.Padding(2);
            this.grpActions.Size = new System.Drawing.Size(319, 107);
            this.grpActions.TabIndex = 1;
            this.grpActions.TabStop = false;
            this.grpActions.Text = "Actions";
            // 
            // btnOpenPortal
            // 
            this.btnOpenPortal.Location = new System.Drawing.Point(4, 23);
            this.btnOpenPortal.Margin = new System.Windows.Forms.Padding(2);
            this.btnOpenPortal.Name = "btnOpenPortal";
            this.btnOpenPortal.Size = new System.Drawing.Size(100, 30);
            this.btnOpenPortal.TabIndex = 0;
            this.btnOpenPortal.Text = "Open TIA";
            this.btnOpenPortal.UseVisualStyleBackColor = true;
            this.btnOpenPortal.Click += new System.EventHandler(this.btnOpenPortal_Click);
            // 
            // btnOpenProject
            // 
            this.btnOpenProject.Location = new System.Drawing.Point(108, 23);
            this.btnOpenProject.Margin = new System.Windows.Forms.Padding(2);
            this.btnOpenProject.Name = "btnOpenProject";
            this.btnOpenProject.Size = new System.Drawing.Size(100, 30);
            this.btnOpenProject.TabIndex = 1;
            this.btnOpenProject.Text = "Open Project";
            this.btnOpenProject.UseVisualStyleBackColor = true;
            this.btnOpenProject.Click += new System.EventHandler(this.btnOpenProject_Click);
            // 
            // btnScan
            // 
            this.btnScan.Location = new System.Drawing.Point(212, 23);
            this.btnScan.Margin = new System.Windows.Forms.Padding(2);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(100, 30);
            this.btnScan.TabIndex = 2;
            this.btnScan.Text = "Scan";
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click);
            // 
            // btnExportSelected
            // 
            this.btnExportSelected.Location = new System.Drawing.Point(4, 62);
            this.btnExportSelected.Margin = new System.Windows.Forms.Padding(2);
            this.btnExportSelected.Name = "btnExportSelected";
            this.btnExportSelected.Size = new System.Drawing.Size(100, 30);
            this.btnExportSelected.TabIndex = 3;
            this.btnExportSelected.Text = "Export sel.";
            this.btnExportSelected.UseVisualStyleBackColor = true;
            this.btnExportSelected.Click += new System.EventHandler(this.btnExportSelected_Click);
            // 
            // btnExportAll
            // 
            this.btnExportAll.Location = new System.Drawing.Point(108, 62);
            this.btnExportAll.Margin = new System.Windows.Forms.Padding(2);
            this.btnExportAll.Name = "btnExportAll";
            this.btnExportAll.Size = new System.Drawing.Size(100, 30);
            this.btnExportAll.TabIndex = 4;
            this.btnExportAll.Text = "Export all";
            this.btnExportAll.UseVisualStyleBackColor = true;
            this.btnExportAll.Click += new System.EventHandler(this.btnExportAll_Click);
            // 
            // btnImportSelected
            // 
            this.btnImportSelected.Location = new System.Drawing.Point(212, 62);
            this.btnImportSelected.Margin = new System.Windows.Forms.Padding(2);
            this.btnImportSelected.Name = "btnImportSelected";
            this.btnImportSelected.Size = new System.Drawing.Size(100, 30);
            this.btnImportSelected.TabIndex = 5;
            this.btnImportSelected.Text = "Import sel.";
            this.btnImportSelected.UseVisualStyleBackColor = true;
            this.btnImportSelected.Click += new System.EventHandler(this.btnImportSelected_Click);
            // 
            // grpProjectSettings
            // 
            this.grpProjectSettings.Controls.Add(this.lblProject);
            this.grpProjectSettings.Controls.Add(this.txtProject);
            this.grpProjectSettings.Controls.Add(this.btnBrowseProject);
            this.grpProjectSettings.Controls.Add(this.lblRepo);
            this.grpProjectSettings.Controls.Add(this.txtRepo);
            this.grpProjectSettings.Controls.Add(this.btnBrowseRepo);
            this.grpProjectSettings.Controls.Add(this.chkPortalUi);
            this.grpProjectSettings.Controls.Add(this.chkNormalizeXml);
            this.grpProjectSettings.Controls.Add(this.chkIncremental);
            this.grpProjectSettings.Controls.Add(this.chkIncludeDbs);
            this.grpProjectSettings.Controls.Add(this.lblProfile);
            this.grpProjectSettings.Controls.Add(this.cmbProfile);
            this.grpProjectSettings.Location = new System.Drawing.Point(9, 8);
            this.grpProjectSettings.Margin = new System.Windows.Forms.Padding(2);
            this.grpProjectSettings.Name = "grpProjectSettings";
            this.grpProjectSettings.Padding = new System.Windows.Forms.Padding(2);
            this.grpProjectSettings.Size = new System.Drawing.Size(563, 107);
            this.grpProjectSettings.TabIndex = 0;
            this.grpProjectSettings.TabStop = false;
            this.grpProjectSettings.Text = "Project Settings";
            // 
            // lblProject
            // 
            this.lblProject.AutoSize = true;
            this.lblProject.Location = new System.Drawing.Point(5, 23);
            this.lblProject.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblProject.Name = "lblProject";
            this.lblProject.Size = new System.Drawing.Size(40, 13);
            this.lblProject.TabIndex = 0;
            this.lblProject.Text = "Project";
            // 
            // txtProject
            // 
            this.txtProject.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtProject.Location = new System.Drawing.Point(56, 20);
            this.txtProject.Margin = new System.Windows.Forms.Padding(2);
            this.txtProject.Name = "txtProject";
            this.txtProject.Size = new System.Drawing.Size(414, 20);
            this.txtProject.TabIndex = 1;
            // 
            // btnBrowseProject
            // 
            this.btnBrowseProject.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseProject.Location = new System.Drawing.Point(474, 14);
            this.btnBrowseProject.Margin = new System.Windows.Forms.Padding(2);
            this.btnBrowseProject.Name = "btnBrowseProject";
            this.btnBrowseProject.Size = new System.Drawing.Size(75, 30);
            this.btnBrowseProject.TabIndex = 2;
            this.btnBrowseProject.Text = "Browse...";
            this.btnBrowseProject.UseVisualStyleBackColor = true;
            this.btnBrowseProject.Click += new System.EventHandler(this.btnBrowseProject_Click);
            // 
            // lblRepo
            // 
            this.lblRepo.AutoSize = true;
            this.lblRepo.Location = new System.Drawing.Point(5, 56);
            this.lblRepo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblRepo.Name = "lblRepo";
            this.lblRepo.Size = new System.Drawing.Size(33, 13);
            this.lblRepo.TabIndex = 3;
            this.lblRepo.Text = "Repo";
            // 
            // txtRepo
            // 
            this.txtRepo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtRepo.Location = new System.Drawing.Point(56, 53);
            this.txtRepo.Margin = new System.Windows.Forms.Padding(2);
            this.txtRepo.Name = "txtRepo";
            this.txtRepo.Size = new System.Drawing.Size(414, 20);
            this.txtRepo.TabIndex = 4;
            // 
            // btnBrowseRepo
            // 
            this.btnBrowseRepo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseRepo.Location = new System.Drawing.Point(474, 47);
            this.btnBrowseRepo.Margin = new System.Windows.Forms.Padding(2);
            this.btnBrowseRepo.Name = "btnBrowseRepo";
            this.btnBrowseRepo.Size = new System.Drawing.Size(75, 30);
            this.btnBrowseRepo.TabIndex = 5;
            this.btnBrowseRepo.Text = "Repo...";
            this.btnBrowseRepo.UseVisualStyleBackColor = true;
            this.btnBrowseRepo.Click += new System.EventHandler(this.btnBrowseRepo_Click);
            // 
            // chkPortalUi
            // 
            this.chkPortalUi.AutoSize = true;
            this.chkPortalUi.Checked = true;
            this.chkPortalUi.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkPortalUi.Location = new System.Drawing.Point(31, 83);
            this.chkPortalUi.Margin = new System.Windows.Forms.Padding(2);
            this.chkPortalUi.Name = "chkPortalUi";
            this.chkPortalUi.Size = new System.Drawing.Size(79, 17);
            this.chkPortalUi.TabIndex = 6;
            this.chkPortalUi.Text = "TIA with UI";
            this.chkPortalUi.UseVisualStyleBackColor = true;
            // 
            // chkNormalizeXml
            // 
            this.chkNormalizeXml.AutoSize = true;
            this.chkNormalizeXml.Checked = true;
            this.chkNormalizeXml.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkNormalizeXml.Location = new System.Drawing.Point(114, 83);
            this.chkNormalizeXml.Margin = new System.Windows.Forms.Padding(2);
            this.chkNormalizeXml.Name = "chkNormalizeXml";
            this.chkNormalizeXml.Size = new System.Drawing.Size(97, 17);
            this.chkNormalizeXml.TabIndex = 7;
            this.chkNormalizeXml.Text = "Normalize XML";
            this.chkNormalizeXml.UseVisualStyleBackColor = true;
            // 
            // chkIncremental
            // 
            this.chkIncremental.AutoSize = true;
            this.chkIncremental.Checked = true;
            this.chkIncremental.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkIncremental.Location = new System.Drawing.Point(215, 83);
            this.chkIncremental.Margin = new System.Windows.Forms.Padding(2);
            this.chkIncremental.Name = "chkIncremental";
            this.chkIncremental.Size = new System.Drawing.Size(81, 17);
            this.chkIncremental.TabIndex = 8;
            this.chkIncremental.Text = "Incremental";
            this.chkIncremental.UseVisualStyleBackColor = true;
            // 
            // chkIncludeDbs
            // 
            this.chkIncludeDbs.AutoSize = true;
            this.chkIncludeDbs.Location = new System.Drawing.Point(300, 83);
            this.chkIncludeDbs.Margin = new System.Windows.Forms.Padding(2);
            this.chkIncludeDbs.Name = "chkIncludeDbs";
            this.chkIncludeDbs.Size = new System.Drawing.Size(84, 17);
            this.chkIncludeDbs.TabIndex = 9;
            this.chkIncludeDbs.Text = "Include DBs";
            this.chkIncludeDbs.UseVisualStyleBackColor = true;
            // 
            // lblProfile
            // 
            this.lblProfile.AutoSize = true;
            this.lblProfile.Location = new System.Drawing.Point(388, 84);
            this.lblProfile.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblProfile.Name = "lblProfile";
            this.lblProfile.Size = new System.Drawing.Size(36, 13);
            this.lblProfile.TabIndex = 10;
            this.lblProfile.Text = "Profile";
            // 
            // cmbProfile
            // 
            this.cmbProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbProfile.FormattingEnabled = true;
            this.cmbProfile.Location = new System.Drawing.Point(428, 81);
            this.cmbProfile.Margin = new System.Windows.Forms.Padding(2);
            this.cmbProfile.Name = "cmbProfile";
            this.cmbProfile.Size = new System.Drawing.Size(121, 21);
            this.cmbProfile.TabIndex = 11;
            // 
            // pnlMainArea
            // 
            this.pnlMainArea.Controls.Add(this.grpLog);
            this.pnlMainArea.Controls.Add(this.treeItems);
            this.pnlMainArea.Controls.Add(this.txtFilter);
            this.pnlMainArea.Controls.Add(this.lblFilter);
            this.pnlMainArea.Location = new System.Drawing.Point(0, 122);
            this.pnlMainArea.Margin = new System.Windows.Forms.Padding(2);
            this.pnlMainArea.Name = "pnlMainArea";
            this.pnlMainArea.Padding = new System.Windows.Forms.Padding(9, 6, 9, 6);
            this.pnlMainArea.Size = new System.Drawing.Size(910, 383);
            this.pnlMainArea.TabIndex = 1;
            // 
            // grpLog
            // 
            this.grpLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpLog.Controls.Add(this.pictureLogo);
            this.grpLog.Controls.Add(this.txtLog);
            this.grpLog.Location = new System.Drawing.Point(0, 286);
            this.grpLog.Margin = new System.Windows.Forms.Padding(2);
            this.grpLog.Name = "grpLog";
            this.grpLog.Padding = new System.Windows.Forms.Padding(2);
            this.grpLog.Size = new System.Drawing.Size(910, 89);
            this.grpLog.TabIndex = 0;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "Log";
            // 
            // pictureLogo
            // 
            this.pictureLogo.ErrorImage = null;
            this.pictureLogo.Image = ((System.Drawing.Image)(resources.GetObject("pictureLogo.Image")));
            this.pictureLogo.InitialImage = null;
            this.pictureLogo.Location = new System.Drawing.Point(801, 11);
            this.pictureLogo.Name = "pictureLogo";
            this.pictureLogo.Size = new System.Drawing.Size(98, 73);
            this.pictureLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureLogo.TabIndex = 1;
            this.pictureLogo.TabStop = false;
            this.pictureLogo.Click += new System.EventHandler(this.pictureLogo_Click);
            // 
            // txtLog
            // 
            this.txtLog.Location = new System.Drawing.Point(14, 17);
            this.txtLog.Margin = new System.Windows.Forms.Padding(2);
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(774, 68);
            this.txtLog.TabIndex = 0;
            this.txtLog.Text = "";
            // 
            // treeItems
            // 
            this.treeItems.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.treeItems.CheckBoxes = true;
            this.treeItems.Location = new System.Drawing.Point(17, 31);
            this.treeItems.Margin = new System.Windows.Forms.Padding(2);
            this.treeItems.Name = "treeItems";
            this.treeItems.Size = new System.Drawing.Size(882, 249);
            this.treeItems.TabIndex = 2;
            this.treeItems.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.treeItems_AfterCheck);
            // 
            // txtFilter
            // 
            this.txtFilter.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtFilter.Location = new System.Drawing.Point(65, 7);
            this.txtFilter.Margin = new System.Windows.Forms.Padding(2);
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new System.Drawing.Size(414, 20);
            this.txtFilter.TabIndex = 1;
            this.txtFilter.TextChanged += new System.EventHandler(this.txtFilter_TextChanged);
            // 
            // lblFilter
            // 
            this.lblFilter.AutoSize = true;
            this.lblFilter.Location = new System.Drawing.Point(14, 10);
            this.lblFilter.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new System.Drawing.Size(29, 13);
            this.lblFilter.TabIndex = 0;
            this.lblFilter.Text = "Filter";
            // 
            // progressExport
            // 
            this.progressExport.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.progressExport.Location = new System.Drawing.Point(0, 509);
            this.progressExport.Margin = new System.Windows.Forms.Padding(2);
            this.progressExport.Name = "progressExport";
            this.progressExport.Size = new System.Drawing.Size(910, 13);
            this.progressExport.TabIndex = 2;
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 522);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 10, 0);
            this.statusStrip.Size = new System.Drawing.Size(910, 22);
            this.statusStrip.TabIndex = 3;
            this.statusStrip.Text = "statusStrip";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(39, 17);
            this.lblStatus.Text = "Ready";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(910, 544);
            this.Controls.Add(this.pnlMainArea);
            this.Controls.Add(this.progressExport);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.pnlTop);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MinimumSize = new System.Drawing.Size(694, 511);
            this.Name = "Form1";
            this.Text = "TIA Git Exporter (V19)";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Shown += new System.EventHandler(this.Form1_Shown);
            this.pnlTop.ResumeLayout(false);
            this.grpActions.ResumeLayout(false);
            this.grpProjectSettings.ResumeLayout(false);
            this.grpProjectSettings.PerformLayout();
            this.pnlMainArea.ResumeLayout(false);
            this.pnlMainArea.PerformLayout();
            this.grpLog.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureLogo)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureLogo;
    }
}
