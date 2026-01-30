using System;
using System.Windows.Forms;

namespace DataverseToPowerBI.Configurator.Forms
{
    public class EnvironmentDialog : Form
    {
        private TextBox txtUrl;
        private Button btnConnect;
        private Button btnSave;
        private Button btnCancel;
        private Label lblUrl;
        private Label lblStatus;

        public string EnvironmentUrl { get; private set; } = "";
        public bool ShouldConnect { get; private set; } = false;

        public EnvironmentDialog(string currentUrl)
        {
            InitializeComponent();
            txtUrl.Text = currentUrl;
        }

        private void InitializeComponent()
        {
            this.Text = "Dataverse Environment";
            this.Size = new System.Drawing.Size(500, 180);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            lblUrl = new Label
            {
                Text = "Dataverse URL:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };

            txtUrl = new TextBox
            {
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(440, 23),
                PlaceholderText = "yourorg.crm.dynamics.com"
            };

            lblStatus = new Label
            {
                Text = "Enter the Dataverse environment URL (without https://)",
                Location = new System.Drawing.Point(20, 75),
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray
            };

            btnConnect = new Button
            {
                Text = "Save && Connect",
                Location = new System.Drawing.Point(170, 105),
                Size = new System.Drawing.Size(110, 30),
                DialogResult = DialogResult.OK
            };
            btnConnect.Click += BtnConnect_Click;

            btnSave = new Button
            {
                Text = "Save",
                Location = new System.Drawing.Point(290, 105),
                Size = new System.Drawing.Size(80, 30),
                DialogResult = DialogResult.OK
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(380, 105),
                Size = new System.Drawing.Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblUrl);
            this.Controls.Add(txtUrl);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnConnect);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnConnect;
            this.CancelButton = btnCancel;
        }

        private void BtnConnect_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                MessageBox.Show("Please enter a URL.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }
            EnvironmentUrl = txtUrl.Text.Trim();
            ShouldConnect = true;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                MessageBox.Show("Please enter a URL.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }
            EnvironmentUrl = txtUrl.Text.Trim();
            ShouldConnect = false;
        }
    }
}
