namespace DataverseMetadataExtractor.Forms
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
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.configurationsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.switchConfigurationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newConfigurationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameConfigurationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteConfigurationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            
            this.groupBoxConnection = new System.Windows.Forms.GroupBox();
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.btnConnect = new System.Windows.Forms.Button();
            this.txtEnvironmentUrl = new System.Windows.Forms.TextBox();
            this.labelEnvironmentUrl = new System.Windows.Forms.Label();
            
            this.panelTableSelector = new System.Windows.Forms.Panel();
            this.btnSelectTables = new System.Windows.Forms.Button();
            this.lblTableCount = new System.Windows.Forms.Label();
            
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            
            this.groupBoxSelectedTables = new System.Windows.Forms.GroupBox();
            this.listViewSelectedTables = new System.Windows.Forms.ListView();
            this.colEdit = new System.Windows.Forms.ColumnHeader();
            this.colTable = new System.Windows.Forms.ColumnHeader();
            this.colForm = new System.Windows.Forms.ColumnHeader();
            this.colView = new System.Windows.Forms.ColumnHeader();
            this.colAttrs = new System.Windows.Forms.ColumnHeader();
            this.btnRemoveTable = new System.Windows.Forms.Button();
            
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
            
            this.groupBoxOutput = new System.Windows.Forms.GroupBox();
            this.btnBrowseOutput = new System.Windows.Forms.Button();
            this.txtOutputFolder = new System.Windows.Forms.TextBox();
            this.lblOutputFolder = new System.Windows.Forms.Label();
            this.txtProjectName = new System.Windows.Forms.TextBox();
            this.lblProjectName = new System.Windows.Forms.Label();
            
            this.panelActions = new System.Windows.Forms.Panel();
            this.btnExport = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            
            this.menuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.groupBoxConnection.SuspendLayout();
            this.panelTableSelector.SuspendLayout();
            this.groupBoxSelectedTables.SuspendLayout();
            this.groupBoxAttributes.SuspendLayout();
            this.panelAttrFilter.SuspendLayout();
            this.panelAttrButtons.SuspendLayout();
            this.groupBoxOutput.SuspendLayout();
            this.panelActions.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();

            // groupBoxConnection
            this.groupBoxConnection.Controls.Add(this.lblConnectionStatus);
            this.groupBoxConnection.Controls.Add(this.btnConnect);
            this.groupBoxConnection.Controls.Add(this.txtEnvironmentUrl);
            this.groupBoxConnection.Controls.Add(this.labelEnvironmentUrl);
            this.groupBoxConnection.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxConnection.Location = new System.Drawing.Point(10, 10);
            this.groupBoxConnection.Name = "groupBoxConnection";
            this.groupBoxConnection.Padding = new System.Windows.Forms.Padding(10);
            this.groupBoxConnection.Size = new System.Drawing.Size(1180, 80);
            this.groupBoxConnection.TabIndex = 0;
            this.groupBoxConnection.TabStop = false;
            this.groupBoxConnection.Text = "Connection";

            // menuStrip
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.fileToolStripMenuItem });
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1200, 24);
            this.menuStrip.TabIndex = 0;
            this.menuStrip.Text = "menuStrip";

            // fileToolStripMenuItem
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.configurationsToolStripMenuItem
            });
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";

            // configurationsToolStripMenuItem
            this.configurationsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.switchConfigurationToolStripMenuItem,
                this.newConfigurationToolStripMenuItem,
                this.renameConfigurationToolStripMenuItem,
                this.deleteConfigurationToolStripMenuItem
            });
            this.configurationsToolStripMenuItem.Name = "configurationsToolStripMenuItem";
            this.configurationsToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.configurationsToolStripMenuItem.Text = "&Configurations";
            this.configurationsToolStripMenuItem.DropDownOpening += new System.EventHandler(this.ConfigurationsToolStripMenuItem_DropDownOpening);

            // switchConfigurationToolStripMenuItem
            this.switchConfigurationToolStripMenuItem.Name = "switchConfigurationToolStripMenuItem";
            this.switchConfigurationToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.switchConfigurationToolStripMenuItem.Text = "&Switch to...";
            // Will be populated dynamically with submenu items

            // newConfigurationToolStripMenuItem
            this.newConfigurationToolStripMenuItem.Name = "newConfigurationToolStripMenuItem";
            this.newConfigurationToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.newConfigurationToolStripMenuItem.Text = "&New Configuration...";
            this.newConfigurationToolStripMenuItem.Click += new System.EventHandler(this.NewConfigurationToolStripMenuItem_Click);

            // renameConfigurationToolStripMenuItem
            this.renameConfigurationToolStripMenuItem.Name = "renameConfigurationToolStripMenuItem";
            this.renameConfigurationToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.renameConfigurationToolStripMenuItem.Text = "&Rename Current...";
            this.renameConfigurationToolStripMenuItem.Click += new System.EventHandler(this.RenameConfigurationToolStripMenuItem_Click);

            // deleteConfigurationToolStripMenuItem
            this.deleteConfigurationToolStripMenuItem.Name = "deleteConfigurationToolStripMenuItem";
            this.deleteConfigurationToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.deleteConfigurationToolStripMenuItem.Text = "&Delete Current...";
            this.deleteConfigurationToolStripMenuItem.Click += new System.EventHandler(this.DeleteConfigurationToolStripMenuItem_Click);

            // labelEnvironmentUrl
            this.labelEnvironmentUrl.AutoSize = true;
            this.labelEnvironmentUrl.Location = new System.Drawing.Point(13, 26);
            this.labelEnvironmentUrl.Name = "labelEnvironmentUrl";
            this.labelEnvironmentUrl.Size = new System.Drawing.Size(100, 15);
            this.labelEnvironmentUrl.TabIndex = 0;
            this.labelEnvironmentUrl.Text = "Dataverse URL:";

            // txtEnvironmentUrl
            this.txtEnvironmentUrl.Location = new System.Drawing.Point(13, 44);
            this.txtEnvironmentUrl.Name = "txtEnvironmentUrl";
            this.txtEnvironmentUrl.Size = new System.Drawing.Size(500, 23);
            this.txtEnvironmentUrl.TabIndex = 1;

            // btnConnect
            this.btnConnect.Location = new System.Drawing.Point(525, 43);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(90, 25);
            this.btnConnect.TabIndex = 2;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.BtnConnect_Click);

            // lblConnectionStatus
            this.lblConnectionStatus.AutoSize = true;
            this.lblConnectionStatus.Location = new System.Drawing.Point(630, 48);
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(88, 15);
            this.lblConnectionStatus.TabIndex = 3;
            this.lblConnectionStatus.Text = "Not connected";

            // panelTableSelector
            this.panelTableSelector.Controls.Add(this.btnSelectTables);
            this.panelTableSelector.Controls.Add(this.lblTableCount);
            this.panelTableSelector.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTableSelector.Location = new System.Drawing.Point(10, 90);
            this.panelTableSelector.Name = "panelTableSelector";
            this.panelTableSelector.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.panelTableSelector.Size = new System.Drawing.Size(1180, 40);
            this.panelTableSelector.TabIndex = 1;

            // btnSelectTables
            this.btnSelectTables.Location = new System.Drawing.Point(0, 5);
            this.btnSelectTables.Name = "btnSelectTables";
            this.btnSelectTables.Size = new System.Drawing.Size(180, 30);
            this.btnSelectTables.TabIndex = 0;
            this.btnSelectTables.Text = "Select Fact && Dimensions...";
            this.btnSelectTables.UseVisualStyleBackColor = true;
            this.btnSelectTables.Click += new System.EventHandler(this.BtnSelectTables_Click);

            // lblTableCount
            this.lblTableCount.AutoSize = true;
            this.lblTableCount.Location = new System.Drawing.Point(190, 12);
            this.lblTableCount.Name = "lblTableCount";
            this.lblTableCount.Size = new System.Drawing.Size(112, 15);
            this.lblTableCount.TabIndex = 1;
            this.lblTableCount.Text = "No tables selected";

            // splitContainerMain
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.Location = new System.Drawing.Point(10, 130);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Panel1.Controls.Add(this.groupBoxSelectedTables);
            this.splitContainerMain.Panel2.Controls.Add(this.groupBoxAttributes);
            this.splitContainerMain.Size = new System.Drawing.Size(1180, 450);
            this.splitContainerMain.SplitterDistance = 590;
            this.splitContainerMain.TabIndex = 2;

            // groupBoxSelectedTables
            this.groupBoxSelectedTables.Controls.Add(this.listViewSelectedTables);
            this.groupBoxSelectedTables.Controls.Add(this.btnRemoveTable);
            this.groupBoxSelectedTables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSelectedTables.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSelectedTables.Name = "groupBoxSelectedTables";
            this.groupBoxSelectedTables.Padding = new System.Windows.Forms.Padding(5);
            this.groupBoxSelectedTables.Size = new System.Drawing.Size(590, 450);
            this.groupBoxSelectedTables.TabIndex = 0;
            this.groupBoxSelectedTables.TabStop = false;
            this.groupBoxSelectedTables.Text = "Selected Tables && Forms";

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
            this.listViewSelectedTables.Size = new System.Drawing.Size(580, 389);
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

            // btnRemoveTable
            this.btnRemoveTable.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnRemoveTable.Location = new System.Drawing.Point(5, 410);
            this.btnRemoveTable.Name = "btnRemoveTable";
            this.btnRemoveTable.Size = new System.Drawing.Size(580, 35);
            this.btnRemoveTable.TabIndex = 1;
            this.btnRemoveTable.Text = "Remove Selected Table";
            this.btnRemoveTable.UseVisualStyleBackColor = true;
            this.btnRemoveTable.Click += new System.EventHandler(this.BtnRemoveTable_Click);

            // groupBoxAttributes
            this.groupBoxAttributes.Controls.Add(this.listViewAttributes);
            this.groupBoxAttributes.Controls.Add(this.panelAttrButtons);
            this.groupBoxAttributes.Controls.Add(this.panelAttrFilter);
            this.groupBoxAttributes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxAttributes.Location = new System.Drawing.Point(0, 0);
            this.groupBoxAttributes.Name = "groupBoxAttributes";
            this.groupBoxAttributes.Padding = new System.Windows.Forms.Padding(5);
            this.groupBoxAttributes.Size = new System.Drawing.Size(586, 450);
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
            this.listViewAttributes.Size = new System.Drawing.Size(576, 354);
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
            this.panelAttrButtons.Location = new System.Drawing.Point(5, 410);
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
            this.btnSelectFromForm.Text = "Select From Form";
            this.btnSelectFromForm.UseVisualStyleBackColor = true;
            this.btnSelectFromForm.Click += new System.EventHandler(this.BtnSelectFromForm_Click);

            // groupBoxOutput
            this.groupBoxOutput.Controls.Add(this.btnBrowseOutput);
            this.groupBoxOutput.Controls.Add(this.txtOutputFolder);
            this.groupBoxOutput.Controls.Add(this.lblOutputFolder);
            this.groupBoxOutput.Controls.Add(this.txtProjectName);
            this.groupBoxOutput.Controls.Add(this.lblProjectName);
            this.groupBoxOutput.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxOutput.Location = new System.Drawing.Point(10, 580);
            this.groupBoxOutput.Name = "groupBoxOutput";
            this.groupBoxOutput.Padding = new System.Windows.Forms.Padding(10);
            this.groupBoxOutput.Size = new System.Drawing.Size(1180, 80);
            this.groupBoxOutput.TabIndex = 3;
            this.groupBoxOutput.TabStop = false;
            this.groupBoxOutput.Text = "Output";

            // lblProjectName
            this.lblProjectName.AutoSize = true;
            this.lblProjectName.Location = new System.Drawing.Point(13, 36);
            this.lblProjectName.Name = "lblProjectName";
            this.lblProjectName.Size = new System.Drawing.Size(82, 15);
            this.lblProjectName.TabIndex = 0;
            this.lblProjectName.Text = "Semantic Model Name:";

            // txtProjectName
            this.txtProjectName.Location = new System.Drawing.Point(105, 33);
            this.txtProjectName.Name = "txtProjectName";
            this.txtProjectName.Size = new System.Drawing.Size(250, 23);
            this.txtProjectName.TabIndex = 1;
            this.txtProjectName.TextChanged += new System.EventHandler(this.TxtProjectName_TextChanged);

            // lblOutputFolder
            this.lblOutputFolder.AutoSize = true;
            this.lblOutputFolder.Location = new System.Drawing.Point(375, 36);
            this.lblOutputFolder.Name = "lblOutputFolder";
            this.lblOutputFolder.Size = new System.Drawing.Size(88, 15);
            this.lblOutputFolder.TabIndex = 2;
            this.lblOutputFolder.Text = "Project Folder:";

            // txtOutputFolder
            this.txtOutputFolder.Location = new System.Drawing.Point(469, 33);
            this.txtOutputFolder.Name = "txtOutputFolder";
            this.txtOutputFolder.Size = new System.Drawing.Size(550, 23);
            this.txtOutputFolder.TabIndex = 3;
            this.txtOutputFolder.TextChanged += new System.EventHandler(this.TxtOutputFolder_TextChanged);

            // btnBrowseOutput
            this.btnBrowseOutput.Location = new System.Drawing.Point(1025, 32);
            this.btnBrowseOutput.Name = "btnBrowseOutput";
            this.btnBrowseOutput.Size = new System.Drawing.Size(80, 25);
            this.btnBrowseOutput.TabIndex = 4;
            this.btnBrowseOutput.Text = "Browse...";
            this.btnBrowseOutput.UseVisualStyleBackColor = true;
            this.btnBrowseOutput.Click += new System.EventHandler(this.BtnBrowseOutput_Click);

            // panelActions
            this.panelActions.Controls.Add(this.lblStatus);
            this.panelActions.Controls.Add(this.btnExport);
            this.panelActions.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelActions.Location = new System.Drawing.Point(10, 660);
            this.panelActions.Name = "panelActions";
            this.panelActions.Size = new System.Drawing.Size(1180, 40);
            this.panelActions.TabIndex = 4;

            // lblStatus
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(3, 13);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(39, 15);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Ready";

            // btnExport
            this.btnExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExport.Location = new System.Drawing.Point(1020, 5);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(160, 30);
            this.btnExport.TabIndex = 1;
            this.btnExport.Text = "Export Metadata JSON";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.BtnExport_Click);

            // statusStrip
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.progressBar });
            this.statusStrip.Location = new System.Drawing.Point(0, 708);
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
            this.ClientSize = new System.Drawing.Size(1200, 730);
            this.Controls.Add(this.splitContainerMain);
            this.Controls.Add(this.panelActions);
            this.Controls.Add(this.groupBoxOutput);
            this.Controls.Add(this.panelTableSelector);
            this.Controls.Add(this.groupBoxConnection);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.MinimumSize = new System.Drawing.Size(1000, 600);
            this.Name = "MainForm";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Dataverse Metadata Extractor for Power BI";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);

            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.groupBoxConnection.ResumeLayout(false);
            this.groupBoxConnection.PerformLayout();
            this.panelTableSelector.ResumeLayout(false);
            this.panelTableSelector.PerformLayout();
            this.groupBoxSelectedTables.ResumeLayout(false);
            this.groupBoxAttributes.ResumeLayout(false);
            this.panelAttrFilter.ResumeLayout(false);
            this.panelAttrFilter.PerformLayout();
            this.panelAttrButtons.ResumeLayout(false);
            this.groupBoxOutput.ResumeLayout(false);
            this.groupBoxOutput.PerformLayout();
            this.panelActions.ResumeLayout(false);
            this.panelActions.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem configurationsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem switchConfigurationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newConfigurationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameConfigurationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteConfigurationToolStripMenuItem;
        
        private System.Windows.Forms.GroupBox groupBoxConnection;
        private System.Windows.Forms.Label lblConnectionStatus;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.TextBox txtEnvironmentUrl;
        private System.Windows.Forms.Label labelEnvironmentUrl;
        
        private System.Windows.Forms.Panel panelTableSelector;
        private System.Windows.Forms.Button btnSelectTables;
        private System.Windows.Forms.Label lblTableCount;
        
        private System.Windows.Forms.SplitContainer splitContainerMain;
        
        private System.Windows.Forms.GroupBox groupBoxSelectedTables;
        private System.Windows.Forms.ListView listViewSelectedTables;
        private System.Windows.Forms.ColumnHeader colEdit;
        private System.Windows.Forms.ColumnHeader colRole;
        private System.Windows.Forms.ColumnHeader colTable;
        private System.Windows.Forms.ColumnHeader colForm;
        private System.Windows.Forms.ColumnHeader colView;
        private System.Windows.Forms.ColumnHeader colAttrs;
        private System.Windows.Forms.Button btnRemoveTable;
        
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
        
        private System.Windows.Forms.GroupBox groupBoxOutput;
        private System.Windows.Forms.Button btnBrowseOutput;
        private System.Windows.Forms.TextBox txtOutputFolder;
        private System.Windows.Forms.Label lblOutputFolder;
        private System.Windows.Forms.TextBox txtProjectName;
        private System.Windows.Forms.Label lblProjectName;
        
        private System.Windows.Forms.Panel panelActions;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Label lblStatus;
        
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
    }
}
