namespace TheMaxLab.SqlServerDiagnostics.App.Forms
{
    partial class ConnectionForm
    {
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.TextBox serverTextBox;
        private System.Windows.Forms.ComboBox databaseComboBox;
        private System.Windows.Forms.ComboBox authenticationComboBox;
        private System.Windows.Forms.TextBox userNameTextBox;
        private System.Windows.Forms.TextBox passwordTextBox;
        private System.Windows.Forms.CheckBox encryptCheckBox;
        private System.Windows.Forms.CheckBox trustCertificateCheckBox;
        private System.Windows.Forms.CheckBox rememberCredentialsCheckBox;
        private System.Windows.Forms.NumericUpDown timeoutNumericUpDown;
        private System.Windows.Forms.Button testButton;
        private System.Windows.Forms.Button connectButton;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.ComboBox profileComboBox;
        private System.Windows.Forms.TextBox profileNameTextBox;
        private System.Windows.Forms.Button newProfileButton;
        private System.Windows.Forms.Button saveProfileButton;
        private System.Windows.Forms.Button deleteProfileButton;

        protected override void Dispose(bool disposing) { if (disposing && components != null) components.Dispose(); base.Dispose(disposing); }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.serverTextBox = new System.Windows.Forms.TextBox(); this.databaseComboBox = new System.Windows.Forms.ComboBox(); this.authenticationComboBox = new System.Windows.Forms.ComboBox(); this.userNameTextBox = new System.Windows.Forms.TextBox(); this.passwordTextBox = new System.Windows.Forms.TextBox();
            this.encryptCheckBox = new System.Windows.Forms.CheckBox(); this.trustCertificateCheckBox = new System.Windows.Forms.CheckBox(); this.rememberCredentialsCheckBox = new System.Windows.Forms.CheckBox(); this.timeoutNumericUpDown = new System.Windows.Forms.NumericUpDown(); this.testButton = new System.Windows.Forms.Button(); this.connectButton = new System.Windows.Forms.Button(); this.statusLabel = new System.Windows.Forms.Label();
            this.profileComboBox = new System.Windows.Forms.ComboBox(); this.profileNameTextBox = new System.Windows.Forms.TextBox(); this.newProfileButton = new System.Windows.Forms.Button(); this.saveProfileButton = new System.Windows.Forms.Button(); this.deleteProfileButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.timeoutNumericUpDown)).BeginInit(); this.SuspendLayout();
            AddLabel("Saved profile", 24, 24); this.profileComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList; this.profileComboBox.Location = new System.Drawing.Point(180, 21); this.profileComboBox.Size = new System.Drawing.Size(330, 23); this.profileComboBox.TabIndex = 0; this.profileComboBox.SelectedIndexChanged += new System.EventHandler(this.ProfileChanged);
            AddLabel("Profile name", 24, 61); this.profileNameTextBox.Location = new System.Drawing.Point(180, 58); this.profileNameTextBox.Size = new System.Drawing.Size(330, 23); this.profileNameTextBox.TabIndex = 1;
            this.newProfileButton.Location = new System.Drawing.Point(180, 90); this.newProfileButton.Size = new System.Drawing.Size(90, 28); this.newProfileButton.Text = "New"; this.newProfileButton.TabIndex = 2; this.newProfileButton.Click += new System.EventHandler(this.NewProfileClick);
            this.saveProfileButton.Location = new System.Drawing.Point(278, 90); this.saveProfileButton.Size = new System.Drawing.Size(130, 28); this.saveProfileButton.Text = "Save profile"; this.saveProfileButton.TabIndex = 3; this.saveProfileButton.Click += new System.EventHandler(this.SaveProfileClick);
            this.deleteProfileButton.Location = new System.Drawing.Point(416, 90); this.deleteProfileButton.Size = new System.Drawing.Size(94, 28); this.deleteProfileButton.Text = "Delete"; this.deleteProfileButton.Enabled = false; this.deleteProfileButton.TabIndex = 4; this.deleteProfileButton.Click += new System.EventHandler(this.DeleteProfileClick);
            AddLabel("Server", 24, 139); this.serverTextBox.Location = new System.Drawing.Point(180, 136); this.serverTextBox.Size = new System.Drawing.Size(330, 23); this.serverTextBox.TabIndex = 5; this.serverTextBox.Text = "(localdb)\\MSSQLLocalDB";
            AddLabel("Database", 24, 176); this.databaseComboBox.Location = new System.Drawing.Point(180, 173); this.databaseComboBox.Size = new System.Drawing.Size(330, 23); this.databaseComboBox.TabIndex = 6; this.databaseComboBox.Text = "master";
            AddLabel("Authentication", 24, 213); this.authenticationComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList; this.authenticationComboBox.Items.AddRange(new object[] { "Windows Authentication", "SQL Server Authentication" }); this.authenticationComboBox.Location = new System.Drawing.Point(180, 210); this.authenticationComboBox.Size = new System.Drawing.Size(330, 23); this.authenticationComboBox.TabIndex = 7; this.authenticationComboBox.SelectedIndexChanged += new System.EventHandler(this.AuthenticationChanged);
            AddLabel("Username", 24, 250); this.userNameTextBox.Enabled = false; this.userNameTextBox.Location = new System.Drawing.Point(180, 247); this.userNameTextBox.Size = new System.Drawing.Size(330, 23); this.userNameTextBox.TabIndex = 8;
            AddLabel("Password", 24, 287); this.passwordTextBox.Enabled = false; this.passwordTextBox.Location = new System.Drawing.Point(180, 284); this.passwordTextBox.Size = new System.Drawing.Size(330, 23); this.passwordTextBox.TabIndex = 9; this.passwordTextBox.UseSystemPasswordChar = true;
            AddLabel("Connection timeout", 24, 324); this.timeoutNumericUpDown.Location = new System.Drawing.Point(180, 321); this.timeoutNumericUpDown.Minimum = 1; this.timeoutNumericUpDown.Maximum = 120; this.timeoutNumericUpDown.Value = 15; this.timeoutNumericUpDown.TabIndex = 10;
            this.encryptCheckBox.Location = new System.Drawing.Point(180, 355); this.encryptCheckBox.Size = new System.Drawing.Size(100, 24); this.encryptCheckBox.Text = "Encrypt"; this.encryptCheckBox.Checked = true; this.encryptCheckBox.TabIndex = 11;
            this.trustCertificateCheckBox.Location = new System.Drawing.Point(290, 355); this.trustCertificateCheckBox.Size = new System.Drawing.Size(160, 24); this.trustCertificateCheckBox.Text = "Trust server certificate"; this.trustCertificateCheckBox.TabIndex = 12;
            this.rememberCredentialsCheckBox.Location = new System.Drawing.Point(180, 385); this.rememberCredentialsCheckBox.Size = new System.Drawing.Size(160, 24); this.rememberCredentialsCheckBox.Text = "Remember credentials"; this.rememberCredentialsCheckBox.Enabled = false; this.rememberCredentialsCheckBox.TabIndex = 13; this.rememberCredentialsCheckBox.CheckedChanged += new System.EventHandler(this.RememberCredentialsChanged);
            this.statusLabel.Location = new System.Drawing.Point(24, 420); this.statusLabel.Size = new System.Drawing.Size(486, 42); this.statusLabel.Text = "Enter connection details."; this.statusLabel.TabIndex = 14;
            this.testButton.Location = new System.Drawing.Point(322, 471); this.testButton.Size = new System.Drawing.Size(90, 32); this.testButton.Text = "Test"; this.testButton.TabIndex = 15; this.testButton.Click += new System.EventHandler(this.TestButtonClick);
            this.connectButton.Location = new System.Drawing.Point(420, 471); this.connectButton.Size = new System.Drawing.Size(90, 32); this.connectButton.Text = "Connect"; this.connectButton.TabIndex = 16; this.connectButton.Click += new System.EventHandler(this.ConnectButtonClick);
            this.AcceptButton = this.connectButton; this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F); this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font; this.ClientSize = new System.Drawing.Size(540, 525); this.Font = new System.Drawing.Font("Segoe UI", 9F); this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog; this.MaximizeBox = false; this.MinimizeBox = false; this.Name = "ConnectionForm"; this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent; this.Text = "Connect to SQL Server";
            this.Controls.AddRange(new System.Windows.Forms.Control[] { this.profileComboBox, this.profileNameTextBox, this.newProfileButton, this.saveProfileButton, this.deleteProfileButton, this.serverTextBox, this.databaseComboBox, this.authenticationComboBox, this.userNameTextBox, this.passwordTextBox, this.timeoutNumericUpDown, this.encryptCheckBox, this.trustCertificateCheckBox, this.rememberCredentialsCheckBox, this.statusLabel, this.testButton, this.connectButton });
            ((System.ComponentModel.ISupportInitialize)(this.timeoutNumericUpDown)).EndInit(); this.ResumeLayout(false); this.PerformLayout();
        }

        private void AddLabel(string text, int x, int y)
        {
            var label = new System.Windows.Forms.Label { AutoSize = true, Location = new System.Drawing.Point(x, y), Text = text };
            this.Controls.Add(label);
        }
    }
}