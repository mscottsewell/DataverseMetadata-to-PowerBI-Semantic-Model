namespace DataverseToPowerBI.Configurator.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            
            // Ribbon ToolStrip
            this.toolStripRibbon = new System.Windows.Forms.ToolStrip();
            this.btnChangeEnvironment = new System.Windows.Forms.ToolStripButton();
            this.btnRefreshMetadata = new System.Windows.Forms.ToolStripButton();
            this.lblConnectionStatus = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.btnSelectTables = new System.Windows.Forms.ToolStripButton();
            this.btnBuildSemanticModel = new System.Windows.Forms.ToolStripButton();
            this.btnCalendarTable = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.lblSemanticModel = new System.Windows.Forms.ToolStripLabel();
            this.cboSemanticModels = new System.Windows.Forms.ToolStripComboBox();
            this.btnChangeWorkingFolder = new System.Windows.Forms.ToolStripButton();
            this.btnSettingsFolder = new System.Windows.Forms.ToolStripButton();
            
            // Hidden textbox to hold environment URL
            this.txtEnvironmentUrl = new System.Windows.Forms.TextBox();
            
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            
            this.groupBoxSelectedTables = new System.Windows.Forms.GroupBox();
            this.listViewSelectedTables = new System.Windows.Forms.ListView();
            this.colEdit = new System.Windows.Forms.ColumnHeader();
            this.colTable = new System.Windows.Forms.ColumnHeader();
            this.colForm = new System.Windows.Forms.ColumnHeader();
            this.colView = new System.Windows.Forms.ColumnHeader();
            this.colAttrs = new System.Windows.Forms.ColumnHeader();
            this.panelTableInfo = new System.Windows.Forms.Panel();
            this.lblTableCount = new System.Windows.Forms.Label();
            
            this.groupBoxAttributes = new System.Windows.Forms.GroupBox();
            this.panelAttrFilter = new System.Windows.Forms.Panel();
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtAttrSearch = new System.Windows.Forms.TextBox();
            this.lblShow = new System.Windows.Forms.Label();
            this.radioShowAll = new System.Windows.Forms.RadioButton();
            this.radioShowSelected = new System.Windows.Forms.RadioButton();
            this.listViewAttributes = new System.Windows.Forms.ListView();
            this.colAttrSelected = new System.Windows.Forms.ColumnHeader();
            this.colAttrOnForm = new System.Windows.Forms.ColumnHeader();
            this.colAttrDisplay = new System.Windows.Forms.ColumnHeader();
            this.colAttrLogical = new System.Windows.Forms.ColumnHeader();
            this.colAttrType = new System.Windows.Forms.ColumnHeader();
            this.panelAttrButtons = new System.Windows.Forms.Panel();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.btnDeselectAll = new System.Windows.Forms.Button();
            this.btnSelectFromForm = new System.Windows.Forms.Button();
            
            this.panelStatus = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            
            // Hidden fields for settings storage
            this.txtOutputFolder = new System.Windows.Forms.TextBox();
            this.txtProjectName = new System.Windows.Forms.TextBox();
            
            this.toolStripRibbon.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.groupBoxSelectedTables.SuspendLayout();
            this.panelTableInfo.SuspendLayout();
            this.groupBoxAttributes.SuspendLayout();
            this.panelAttrFilter.SuspendLayout();
            this.panelAttrButtons.SuspendLayout();
            this.panelStatus.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();

            // toolStripRibbon
            this.toolStripRibbon.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStripRibbon.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.btnChangeEnvironment,
                this.btnRefreshMetadata,
                this.lblConnectionStatus,
                this.toolStripSeparator1,
                this.btnSelectTables,
                this.btnBuildSemanticModel,
                this.btnCalendarTable,
                this.toolStripSeparator2,
                this.lblSemanticModel,
                this.cboSemanticModels,
                this.btnChangeWorkingFolder,
                this.btnSettingsFolder
            });
            this.toolStripRibbon.Location = new System.Drawing.Point(0, 0);
            this.toolStripRibbon.Name = "toolStripRibbon";
            this.toolStripRibbon.Padding = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.toolStripRibbon.Size = new System.Drawing.Size(1200, 34);
            this.toolStripRibbon.TabIndex = 1;
            this.toolStripRibbon.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;

            // toolStripSeparator1
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 28);

            // btnChangeEnvironment
            this.btnChangeEnvironment.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            this.btnChangeEnvironment.Image = RibbonIcons.CloudIcon;
            this.btnChangeEnvironment.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnChangeEnvironment.Name = "btnChangeEnvironment";
            this.btnChangeEnvironment.Size = new System.Drawing.Size(130, 25);
            this.btnChangeEnvironment.Text = "Change Environment";
            this.btnChangeEnvironment.ToolTipText = "Configure the Dataverse environment URL";
            this.btnChangeEnvironment.Click += new System.EventHandler(this.BtnChangeEnvironment_Click);

            // btnRefreshMetadata
            this.btnRefreshMetadata.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            this.btnRefreshMetadata.Image = RibbonIcons.RefreshIcon;
            this.btnRefreshMetadata.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRefreshMetadata.Name = "btnRefreshMetadata";
            this.btnRefreshMetadata.Size = new System.Drawing.Size(120, 25);
            this.btnRefreshMetadata.Text = "Refresh Metadata";
            this.btnRefreshMetadata.ToolTipText = "Connect and refresh metadata from the Dataverse environment";
            this.btnRefreshMetadata.Click += new System.EventHandler(this.BtnRefreshMetadata_Click);

            // lblConnectionStatus
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(88, 25);
            this.lblConnectionStatus.Text = "Not connected";
            this.lblConnectionStatus.ForeColor = System.Drawing.Color.Gray;

            // btnSelectTables
            this.btnSelectTables.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            this.btnSelectTables.Image = RibbonIcons.TableIcon;
            this.btnSelectTables.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSelectTables.Name = "btnSelectTables";
            this.btnSelectTables.Size = new System.Drawing.Size(140, 25);
            this.btnSelectTables.Text = "Select Fact && Dimensions";
            this.btnSelectTables.ToolTipText = "Select fact and dimension tables from the solution";
            this.btnSelectTables.Enabled = false;
            this.btnSelectTables.Click += new System.EventHandler(this.BtnSelectTables_Click);

            // btnBuildSemanticModel
            this.btnBuildSemanticModel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            this.btnBuildSemanticModel.Image = RibbonIcons.BuildIcon;
            this.btnBuildSemanticModel.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnBuildSemanticModel.Name = "btnBuildSemanticModel";
            this.btnBuildSemanticModel.Size = new System.Drawing.Size(130, 25);
            this.btnBuildSemanticModel.Text = "Build Semantic Model";
            this.btnBuildSemanticModel.ToolTipText = "Generate the Power BI semantic model";
            this.btnBuildSemanticModel.Click += new System.EventHandler(this.BtnBuildSemanticModel_Click);

            // btnCalendarTable
            this.btnCalendarTable.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            this.btnCalendarTable.Image = RibbonIcons.CalendarIcon;
            this.btnCalendarTable.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnCalendarTable.Name = "btnCalendarTable";
            this.btnCalendarTable.Size = new System.Drawing.Size(100, 25);
            this.btnCalendarTable.Text = "Calendar Table";
            this.btnCalendarTable.ToolTipText = "Configure a Date/Calendar table for the semantic model";
            this.btnCalendarTable.Enabled = false;
            this.btnCalendarTable.Click += new System.EventHandler(this.BtnCalendarTable_Click);

            // toolStripSeparator2
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 28);

            // lblSemanticModel
            this.lblSemanticModel.Name = "lblSemanticModel";
            this.lblSemanticModel.Size = new System.Drawing.Size(95, 25);
            this.lblSemanticModel.Text = "Semantic Model:";

            // cboSemanticModels
            this.cboSemanticModels.Name = "cboSemanticModels";
            this.cboSemanticModels.Size = new System.Drawing.Size(180, 28);
            this.cboSemanticModels.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSemanticModels.ToolTipText = "Select a semantic model from the working folder";
            this.cboSemanticModels.SelectedIndexChanged += new System.EventHandler(this.CboSemanticModels_SelectedIndexChanged);

            // btnChangeWorkingFolder
            this.btnChangeWorkingFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            this.btnChangeWorkingFolder.Image = RibbonIcons.FolderIcon;
            this.btnChangeWorkingFolder.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnChangeWorkingFolder.Name = "btnChangeWorkingFolder";
            this.btnChangeWorkingFolder.Size = new System.Drawing.Size(100, 25);
            this.btnChangeWorkingFolder.Text = "Working Folder";
            this.btnChangeWorkingFolder.ToolTipText = "Browse or open the working folder";
            this.btnChangeWorkingFolder.Click += new System.EventHandler(this.BtnChangeWorkingFolder_Click);

            // btnSettingsFolder
            this.btnSettingsFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            this.btnSettingsFolder.Image = RibbonIcons.FolderIcon;
            this.btnSettingsFolder.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSettingsFolder.Name = "btnSettingsFolder";
            this.btnSettingsFolder.Size = new System.Drawing.Size(100, 25);
            this.btnSettingsFolder.Text = "Settings Folder";
            this.btnSettingsFolder.ToolTipText = "Open the settings/configuration folder";
            this.btnSettingsFolder.Click += new System.EventHandler(this.BtnSettingsFolder_Click);

            // txtEnvironmentUrl (hidden)
            this.txtEnvironmentUrl.Location = new System.Drawing.Point(-100, -100);
            this.txtEnvironmentUrl.Name = "txtEnvironmentUrl";
            this.txtEnvironmentUrl.Size = new System.Drawing.Size(10, 23);
            this.txtEnvironmentUrl.TabIndex = 100;
            this.txtEnvironmentUrl.Visible = false;

            // splitContainerMain
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.Location = new System.Drawing.Point(10, 44);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Panel1.Controls.Add(this.groupBoxSelectedTables);
            this.splitContainerMain.Panel2.Controls.Add(this.groupBoxAttributes);
            this.splitContainerMain.Size = new System.Drawing.Size(1180, 520);
            this.splitContainerMain.SplitterDistance = 590;
            this.splitContainerMain.TabIndex = 2;

            // groupBoxSelectedTables
            this.groupBoxSelectedTables.Controls.Add(this.listViewSelectedTables);
            this.groupBoxSelectedTables.Controls.Add(this.panelTableInfo);
            this.groupBoxSelectedTables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSelectedTables.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSelectedTables.Name = "groupBoxSelectedTables";
            this.groupBoxSelectedTables.Padding = new System.Windows.Forms.Padding(5);
            this.groupBoxSelectedTables.Size = new System.Drawing.Size(590, 520);
            this.groupBoxSelectedTables.TabIndex = 0;
            this.groupBoxSelectedTables.TabStop = false;
            this.groupBoxSelectedTables.Text = "Selected Tables && Forms";

            // panelTableInfo
            this.panelTableInfo.Controls.Add(this.lblTableCount);
            this.panelTableInfo.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelTableInfo.Location = new System.Drawing.Point(5, 490);
            this.panelTableInfo.Name = "panelTableInfo";
            this.panelTableInfo.Size = new System.Drawing.Size(580, 25);
            this.panelTableInfo.TabIndex = 1;

            // lblTableCount
            this.lblTableCount.AutoSize = true;
            this.lblTableCount.Location = new System.Drawing.Point(3, 5);
            this.lblTableCount.Name = "lblTableCount";
            this.lblTableCount.Size = new System.Drawing.Size(112, 15);
            this.lblTableCount.TabIndex = 0;
            this.lblTableCount.Text = "No tables selected";

            // listViewSelectedTables
            this.colRole = new System.Windows.Forms.ColumnHeader();
            this.listViewSelectedTables.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colEdit,
                this.colRole,
                this.colTable,
                this.colForm,
                this.colView,
                this.colAttrs});
            this.listViewSelectedTables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewSelectedTables.FullRowSelect = true;
            this.listViewSelectedTables.HideSelection = false;
            this.listViewSelectedTables.Location = new System.Drawing.Point(5, 21);
            this.listViewSelectedTables.MultiSelect = false;
            this.listViewSelectedTables.Name = "listViewSelectedTables";
            this.listViewSelectedTables.Size = new System.Drawing.Size(580, 464);
            this.listViewSelectedTables.TabIndex = 0;
            this.listViewSelectedTables.UseCompatibleStateImageBehavior = false;
            this.listViewSelectedTables.View = System.Windows.Forms.View.Details;
            this.listViewSelectedTables.SelectedIndexChanged += new System.EventHandler(this.ListViewSelectedTables_SelectedIndexChanged);
            this.listViewSelectedTables.DoubleClick += new System.EventHandler(this.ListViewSelectedTables_DoubleClick);
            this.listViewSelectedTables.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.ListViewSelectedTables_ColumnClick);
            this.listViewSelectedTables.Click += new System.EventHandler(this.ListViewSelectedTables_Click);

            // colEdit
            this.colEdit.Text = "✏️";
            this.colEdit.Width = 30;

            // colRole
            this.colRole.Text = "Role";
            this.colRole.Width = 50;

            // colTable
            this.colTable.Text = "Table";
            this.colTable.Width = 130;

            // colForm
            this.colForm.Text = "Form";
            this.colForm.Width = 100;

            // colView
            this.colView.Text = "Filter";
            this.colView.Width = 160;

            // colAttrs
            this.colAttrs.Text = "Attrs";
            this.colAttrs.Width = 60;

            // groupBoxAttributes
            this.groupBoxAttributes.Controls.Add(this.listViewAttributes);
            this.groupBoxAttributes.Controls.Add(this.panelAttrButtons);
            this.groupBoxAttributes.Controls.Add(this.panelAttrFilter);
            this.groupBoxAttributes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxAttributes.Location = new System.Drawing.Point(0, 0);
            this.groupBoxAttributes.Name = "groupBoxAttributes";
            this.groupBoxAttributes.Padding = new System.Windows.Forms.Padding(5);
            this.groupBoxAttributes.Size = new System.Drawing.Size(586, 520);
            this.groupBoxAttributes.TabIndex = 0;
            this.groupBoxAttributes.TabStop = false;
            this.groupBoxAttributes.Text = "Attributes";

            // panelAttrFilter
            this.panelAttrFilter.Controls.Add(this.lblSearch);
            this.panelAttrFilter.Controls.Add(this.txtAttrSearch);
            this.panelAttrFilter.Controls.Add(this.lblShow);
            this.panelAttrFilter.Controls.Add(this.radioShowAll);
            this.panelAttrFilter.Controls.Add(this.radioShowSelected);
            this.panelAttrFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelAttrFilter.Location = new System.Drawing.Point(5, 21);
            this.panelAttrFilter.Name = "panelAttrFilter";
            this.panelAttrFilter.Size = new System.Drawing.Size(576, 35);
            this.panelAttrFilter.TabIndex = 0;

            // lblSearch
            this.lblSearch.AutoSize = true;
            this.lblSearch.Location = new System.Drawing.Point(3, 10);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Size = new System.Drawing.Size(45, 15);
            this.lblSearch.TabIndex = 0;
            this.lblSearch.Text = "Search:";

            // txtAttrSearch
            this.txtAttrSearch.Location = new System.Drawing.Point(54, 7);
            this.txtAttrSearch.Name = "txtAttrSearch";
            this.txtAttrSearch.Size = new System.Drawing.Size(150, 23);
            this.txtAttrSearch.TabIndex = 1;
            this.txtAttrSearch.TextChanged += new System.EventHandler(this.TxtAttrSearch_TextChanged);

            // lblShow
            this.lblShow.AutoSize = true;
            this.lblShow.Location = new System.Drawing.Point(220, 10);
            this.lblShow.Name = "lblShow";
            this.lblShow.Size = new System.Drawing.Size(39, 15);
            this.lblShow.TabIndex = 2;
            this.lblShow.Text = "Show:";

            // radioShowAll
            this.radioShowAll.AutoSize = true;
            this.radioShowAll.Checked = false;
            this.radioShowAll.Location = new System.Drawing.Point(265, 8);
            this.radioShowAll.Name = "radioShowAll";
            this.radioShowAll.Size = new System.Drawing.Size(39, 19);
            this.radioShowAll.TabIndex = 3;
            this.radioShowAll.TabStop = true;
            this.radioShowAll.Text = "All";
            this.radioShowAll.UseVisualStyleBackColor = true;
            this.radioShowAll.CheckedChanged += new System.EventHandler(this.RadioShowMode_CheckedChanged);

            // radioShowSelected
            this.radioShowSelected.AutoSize = true;
            this.radioShowSelected.Checked = true;
            this.radioShowSelected.Location = new System.Drawing.Point(315, 8);
            this.radioShowSelected.Name = "radioShowSelected";
            this.radioShowSelected.Size = new System.Drawing.Size(71, 19);
            this.radioShowSelected.TabIndex = 4;
            this.radioShowSelected.TabStop = true;
            this.radioShowSelected.Text = "Selected";
            this.radioShowSelected.UseVisualStyleBackColor = true;
            this.radioShowSelected.CheckedChanged += new System.EventHandler(this.RadioShowMode_CheckedChanged);

            // listViewAttributes
            this.listViewAttributes.CheckBoxes = true;
            this.listViewAttributes.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colAttrSelected,
                this.colAttrOnForm,
                this.colAttrDisplay,
                this.colAttrLogical,
                this.colAttrType});
            this.listViewAttributes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewAttributes.FullRowSelect = true;
            this.listViewAttributes.HideSelection = false;
            this.listViewAttributes.Location = new System.Drawing.Point(5, 56);
            this.listViewAttributes.Name = "listViewAttributes";
            this.listViewAttributes.Size = new System.Drawing.Size(576, 424);
            this.listViewAttributes.TabIndex = 1;
            this.listViewAttributes.UseCompatibleStateImageBehavior = false;
            this.listViewAttributes.View = System.Windows.Forms.View.Details;
            this.listViewAttributes.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.ListViewAttributes_ItemChecked);
            this.listViewAttributes.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.ListViewAttributes_ColumnClick);

            // colAttrSelected
            this.colAttrSelected.Text = "Sel";
            this.colAttrSelected.Width = 40;

            // colAttrOnForm
            this.colAttrOnForm.Text = "Form";
            this.colAttrOnForm.Width = 50;

            // colAttrDisplay
            this.colAttrDisplay.Text = "Display Name";
            this.colAttrDisplay.Width = 170;

            // colAttrLogical
            this.colAttrLogical.Text = "Logical Name";
            this.colAttrLogical.Width = 140;

            // colAttrType
            this.colAttrType.Text = "Type";
            this.colAttrType.Width = 120;

            // panelAttrButtons
            this.panelAttrButtons.Controls.Add(this.btnSelectAll);
            this.panelAttrButtons.Controls.Add(this.btnDeselectAll);
            this.panelAttrButtons.Controls.Add(this.btnSelectFromForm);
            this.panelAttrButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelAttrButtons.Location = new System.Drawing.Point(5, 480);
            this.panelAttrButtons.Name = "panelAttrButtons";
            this.panelAttrButtons.Size = new System.Drawing.Size(576, 35);
            this.panelAttrButtons.TabIndex = 2;

            // btnSelectAll
            this.btnSelectAll.Location = new System.Drawing.Point(0, 3);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(90, 28);
            this.btnSelectAll.TabIndex = 0;
            this.btnSelectAll.Text = "Select All";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.BtnSelectAll_Click);

            // btnDeselectAll
            this.btnDeselectAll.Location = new System.Drawing.Point(96, 3);
            this.btnDeselectAll.Name = "btnDeselectAll";
            this.btnDeselectAll.Size = new System.Drawing.Size(100, 28);
            this.btnDeselectAll.TabIndex = 1;
            this.btnDeselectAll.Text = "Deselect All";
            this.btnDeselectAll.UseVisualStyleBackColor = true;
            this.btnDeselectAll.Click += new System.EventHandler(this.BtnDeselectAll_Click);

            // btnSelectFromForm
            this.btnSelectFromForm.Location = new System.Drawing.Point(202, 3);
            this.btnSelectFromForm.Name = "btnSelectFromForm";
            this.btnSelectFromForm.Size = new System.Drawing.Size(130, 28);
            this.btnSelectFromForm.TabIndex = 2;
            this.btnSelectFromForm.Text = "Match Selected Form";
            this.btnSelectFromForm.UseVisualStyleBackColor = true;
            this.btnSelectFromForm.Click += new System.EventHandler(this.BtnSelectFromForm_Click);

            // panelStatus
            this.panelStatus.Controls.Add(this.lblStatus);
            this.panelStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelStatus.Location = new System.Drawing.Point(10, 588);
            this.panelStatus.Name = "panelStatus";
            this.panelStatus.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.panelStatus.Size = new System.Drawing.Size(1180, 30);
            this.panelStatus.TabIndex = 3;

            // lblStatus
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(3, 8);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(39, 15);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Ready";

            // txtOutputFolder (hidden)
            this.txtOutputFolder.Location = new System.Drawing.Point(-100, -100);
            this.txtOutputFolder.Name = "txtOutputFolder";
            this.txtOutputFolder.Size = new System.Drawing.Size(10, 23);
            this.txtOutputFolder.TabIndex = 101;
            this.txtOutputFolder.Visible = false;

            // txtProjectName (hidden)
            this.txtProjectName.Location = new System.Drawing.Point(-100, -100);
            this.txtProjectName.Name = "txtProjectName";
            this.txtProjectName.Size = new System.Drawing.Size(10, 23);
            this.txtProjectName.TabIndex = 102;
            this.txtProjectName.Visible = false;

            // statusStrip
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.progressBar });
            this.statusStrip.Location = new System.Drawing.Point(0, 628);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1200, 22);
            this.statusStrip.TabIndex = 5;

            // progressBar
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(200, 16);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.Visible = false;

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 650);
            this.Controls.Add(this.splitContainerMain);
            this.Controls.Add(this.panelStatus);
            this.Controls.Add(this.toolStripRibbon);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.txtEnvironmentUrl);
            this.Controls.Add(this.txtOutputFolder);
            this.Controls.Add(this.txtProjectName);
            this.MinimumSize = new System.Drawing.Size(1000, 500);
            this.Name = "MainForm";
            this.Padding = new System.Windows.Forms.Padding(10, 10, 10, 10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Dataverse Metadata Extractor for Power BI";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);

            this.toolStripRibbon.ResumeLayout(false);
            this.toolStripRibbon.PerformLayout();
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.groupBoxSelectedTables.ResumeLayout(false);
            this.panelTableInfo.ResumeLayout(false);
            this.panelTableInfo.PerformLayout();
            this.groupBoxAttributes.ResumeLayout(false);
            this.panelAttrFilter.ResumeLayout(false);
            this.panelAttrFilter.PerformLayout();
            this.panelAttrButtons.ResumeLayout(false);
            this.panelStatus.ResumeLayout(false);
            this.panelStatus.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // Ribbon ToolStrip
        private System.Windows.Forms.ToolStrip toolStripRibbon;
        private System.Windows.Forms.ToolStripButton btnChangeEnvironment;
        private System.Windows.Forms.ToolStripButton btnRefreshMetadata;
        private System.Windows.Forms.ToolStripLabel lblConnectionStatus;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton btnSelectTables;
        private System.Windows.Forms.ToolStripButton btnBuildSemanticModel;
        private System.Windows.Forms.ToolStripButton btnCalendarTable;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel lblSemanticModel;
        private System.Windows.Forms.ToolStripComboBox cboSemanticModels;
        private System.Windows.Forms.ToolStripButton btnChangeWorkingFolder;
        private System.Windows.Forms.ToolStripButton btnSettingsFolder;
        
        // Hidden fields for settings storage
        private System.Windows.Forms.TextBox txtEnvironmentUrl;
        private System.Windows.Forms.TextBox txtOutputFolder;
        private System.Windows.Forms.TextBox txtProjectName;
        
        private System.Windows.Forms.SplitContainer splitContainerMain;
        
        private System.Windows.Forms.GroupBox groupBoxSelectedTables;
        private System.Windows.Forms.ListView listViewSelectedTables;
        private System.Windows.Forms.ColumnHeader colEdit;
        private System.Windows.Forms.ColumnHeader colRole;
        private System.Windows.Forms.ColumnHeader colTable;
        private System.Windows.Forms.ColumnHeader colForm;
        private System.Windows.Forms.ColumnHeader colView;
        private System.Windows.Forms.ColumnHeader colAttrs;
        private System.Windows.Forms.Panel panelTableInfo;
        private System.Windows.Forms.Label lblTableCount;
        
        private System.Windows.Forms.GroupBox groupBoxAttributes;
        private System.Windows.Forms.Panel panelAttrFilter;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtAttrSearch;
        private System.Windows.Forms.Label lblShow;
        private System.Windows.Forms.RadioButton radioShowAll;
        private System.Windows.Forms.RadioButton radioShowSelected;
        private System.Windows.Forms.ListView listViewAttributes;
        private System.Windows.Forms.ColumnHeader colAttrSelected;
        private System.Windows.Forms.ColumnHeader colAttrOnForm;
        private System.Windows.Forms.ColumnHeader colAttrDisplay;
        private System.Windows.Forms.ColumnHeader colAttrLogical;
        private System.Windows.Forms.ColumnHeader colAttrType;
        private System.Windows.Forms.Panel panelAttrButtons;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnDeselectAll;
        private System.Windows.Forms.Button btnSelectFromForm;
        
        private System.Windows.Forms.Panel panelStatus;
        private System.Windows.Forms.Label lblStatus;
        
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
    }
}
