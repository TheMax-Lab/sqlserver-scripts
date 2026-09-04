using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TheMaxLab.SqlServerDiagnostics.App.Presentation;
using TheMaxLab.SqlServerDiagnostics.Core.Enums;
using TheMaxLab.SqlServerDiagnostics.Core.Interfaces;
using TheMaxLab.SqlServerDiagnostics.Core.Models;

namespace TheMaxLab.SqlServerDiagnostics.App.Forms
{
    public partial class ConnectionForm : Form
    {
        private readonly IConnectionService connectionService;
        private readonly ISqlServerService sqlServerService;
        private readonly ICredentialService credentialService;
        private readonly IConnectionProfileService profileService;
        private readonly ConnectionProfilePresentation profilePresentation = new ConnectionProfilePresentation();
        private ConnectionProfile selectedProfile;
        private string currentCredentialKey;

        public ConnectionForm(IConnectionService connectionService, ISqlServerService sqlServerService, ICredentialService credentialService, IConnectionProfileService profileService, DatabaseConnection initialConnection = null)
        {
            this.connectionService = connectionService ?? throw new ArgumentNullException("connectionService");
            this.sqlServerService = sqlServerService ?? throw new ArgumentNullException("sqlServerService");
            this.credentialService = credentialService ?? throw new ArgumentNullException("credentialService");
            this.profileService = profileService ?? throw new ArgumentNullException("profileService");
            InitializeComponent();
            Shown += async delegate { await LoadProfilesAsync(); };
            authenticationComboBox.SelectedIndex = initialConnection != null && initialConnection.AuthenticationType == AuthenticationType.SqlServer ? 1 : 0;
            if (initialConnection != null)
            {
                currentCredentialKey = initialConnection.CredentialKey; serverTextBox.Text = initialConnection.ServerName; databaseComboBox.Text = initialConnection.DatabaseName; userNameTextBox.Text = initialConnection.UserName; encryptCheckBox.Checked = initialConnection.Encrypt; trustCertificateCheckBox.Checked = initialConnection.TrustServerCertificate; rememberCredentialsCheckBox.Checked = !string.IsNullOrWhiteSpace(currentCredentialKey) && currentCredentialKey.StartsWith("saved:", StringComparison.Ordinal); timeoutNumericUpDown.Value = Math.Max(timeoutNumericUpDown.Minimum, Math.Min(timeoutNumericUpDown.Maximum, initialConnection.ConnectionTimeoutSeconds));
            }
        }

        public DatabaseConnection SelectedConnection { get; private set; }
        public ConnectionTestResult ConnectionResult { get; private set; }

        private async Task LoadProfilesAsync(string selectedId = null)
        {
            try
            {
                var profiles = await profileService.GetAllAsync(CancellationToken.None);
                profileComboBox.BeginUpdate(); profileComboBox.Items.Clear();
                foreach (ConnectionProfile profile in profiles.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)) profileComboBox.Items.Add(new ProfileItem(profile));
                profileComboBox.EndUpdate();
                if (!string.IsNullOrWhiteSpace(selectedId))
                {
                    for (int index = 0; index < profileComboBox.Items.Count; index++)
                    {
                        var item = profileComboBox.Items[index] as ProfileItem;
                        if (item != null && string.Equals(item.Profile.Id, selectedId, StringComparison.OrdinalIgnoreCase)) { profileComboBox.SelectedIndex = index; return; }
                    }
                }
                UpdateProfileButtons();
            }
            catch (Exception) { statusLabel.Text = "Saved profiles could not be loaded. Verify the local profile file."; }
        }

        private void ProfileChanged(object sender, EventArgs e)
        {
            var item = profileComboBox.SelectedItem as ProfileItem;
            selectedProfile = item == null ? null : item.Profile;
            if (selectedProfile != null)
            {
                profileNameTextBox.Text = selectedProfile.Name;
                ApplyConnection(selectedProfile.ToConnection());
                statusLabel.Text = "Saved profile loaded. Connect to validate it.";
            }
            UpdateProfileButtons();
        }

        private void NewProfileClick(object sender, EventArgs e)
        {
            selectedProfile = null; currentCredentialKey = null; profileComboBox.SelectedIndex = -1; profileNameTextBox.Clear(); passwordTextBox.Clear(); rememberCredentialsCheckBox.Checked = false; UpdateProfileButtons(); profileNameTextBox.Focus(); statusLabel.Text = "Enter a profile name and connection settings, then choose Save profile.";
        }

        private async void SaveProfileClick(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(profileNameTextBox.Text)) throw new InvalidOperationException("A profile name is required.");
                if (authenticationComboBox.SelectedIndex == 1 && !rememberCredentialsCheckBox.Checked) throw new InvalidOperationException("Enable Remember credentials before saving a SQL Server authentication profile.");
                DatabaseConnection connection = await CreateConnectionAsync();
                ConnectionProfile profile = profilePresentation.CreateProfile(connection, profileNameTextBox.Text, selectedProfile == null ? null : selectedProfile.Id);
                string previousCredentialKey = selectedProfile == null ? null : selectedProfile.CredentialKey;
                await profileService.SaveAsync(profile, CancellationToken.None);
                if (!string.IsNullOrWhiteSpace(previousCredentialKey) && !string.Equals(previousCredentialKey, profile.CredentialKey, StringComparison.Ordinal)) await credentialService.DeleteAsync(previousCredentialKey, CancellationToken.None);
                selectedProfile = profile;
                await LoadProfilesAsync(profile.Id);
                statusLabel.Text = "Profile saved securely. Passwords are not stored in the profile file.";
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                statusLabel.Text = exception.Message;
            }
            catch (Exception) { statusLabel.Text = "The profile could not be saved."; }
        }

        private async void DeleteProfileClick(object sender, EventArgs e)
        {
            if (selectedProfile == null) return;
            if (MessageBox.Show(this, "Delete the selected profile and its remembered credential?", "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                string deletedCredential = selectedProfile.CredentialKey;
                await profileService.DeleteAsync(selectedProfile.Id, CancellationToken.None);
                if (string.Equals(currentCredentialKey, deletedCredential, StringComparison.Ordinal)) currentCredentialKey = null;
                selectedProfile = null; profileNameTextBox.Clear(); await LoadProfilesAsync(); statusLabel.Text = "Profile deleted.";
            }
            catch (Exception) { statusLabel.Text = "The profile could not be deleted."; }
        }

        private void AuthenticationChanged(object sender, EventArgs e)
        {
            bool sqlAuthentication = authenticationComboBox.SelectedIndex == 1;
            userNameTextBox.Enabled = sqlAuthentication;
            passwordTextBox.Enabled = sqlAuthentication;
            rememberCredentialsCheckBox.Enabled = sqlAuthentication;
        }

        private void RememberCredentialsChanged(object sender, EventArgs e)
        {
            if (!rememberCredentialsCheckBox.Checked && !string.IsNullOrWhiteSpace(currentCredentialKey) && currentCredentialKey.StartsWith("saved:", StringComparison.Ordinal)) currentCredentialKey = null;
        }

        private void ApplyConnection(DatabaseConnection connection)
        {
            currentCredentialKey = connection.CredentialKey;
            serverTextBox.Text = connection.ServerName;
            databaseComboBox.Text = connection.DatabaseName;
            authenticationComboBox.SelectedIndex = connection.AuthenticationType == AuthenticationType.SqlServer ? 1 : 0;
            userNameTextBox.Text = connection.UserName ?? string.Empty;
            passwordTextBox.Clear();
            encryptCheckBox.Checked = connection.Encrypt;
            trustCertificateCheckBox.Checked = connection.TrustServerCertificate;
            rememberCredentialsCheckBox.Checked = !string.IsNullOrWhiteSpace(currentCredentialKey) && currentCredentialKey.StartsWith("saved:", StringComparison.Ordinal);
            timeoutNumericUpDown.Value = Math.Max(timeoutNumericUpDown.Minimum, Math.Min(timeoutNumericUpDown.Maximum, connection.ConnectionTimeoutSeconds));
        }

        private void UpdateProfileButtons() { deleteProfileButton.Enabled = selectedProfile != null; saveProfileButton.Text = selectedProfile == null ? "Save profile" : "Update profile"; }

        private async void TestButtonClick(object sender, EventArgs e) { await TestAndLoadDatabasesAsync(false); }
        private async void ConnectButtonClick(object sender, EventArgs e) { await TestAndLoadDatabasesAsync(true); }

        private async Task TestAndLoadDatabasesAsync(bool closeOnSuccess)
        {
            SetBusy(true);
            try
            {
                DatabaseConnection connection = await CreateConnectionAsync();
                ConnectionTestResult test = await connectionService.TestAsync(connection, CancellationToken.None);
                statusLabel.Text = test.Message;
                if (!test.Success) return;
                var databases = await sqlServerService.GetDatabasesAsync(connection, CancellationToken.None);
                string selected = databaseComboBox.Text;
                databaseComboBox.Items.Clear();
                databaseComboBox.Items.AddRange(databases.Select(item => item.Name).Cast<object>().ToArray());
                if (!string.IsNullOrWhiteSpace(selected)) databaseComboBox.Text = selected;
                if (closeOnSuccess)
                {
                    connection.DatabaseName = databaseComboBox.Text;
                    ConnectionResult = await connectionService.TestAsync(connection, CancellationToken.None);
                    if (!ConnectionResult.Success) { statusLabel.Text = ConnectionResult.Message; return; }
                    SelectedConnection = connection;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (OperationCanceledException) { statusLabel.Text = "Connection test cancelled."; }
            catch (Exception) { statusLabel.Text = "Unable to test the connection. Verify the entered settings."; }
            finally { SetBusy(false); }
        }

        private async Task<DatabaseConnection> CreateConnectionAsync()
        {
            bool sqlAuthentication = authenticationComboBox.SelectedIndex == 1;
            string credentialKey = null;
            if (sqlAuthentication)
            {
                if (!string.IsNullOrEmpty(passwordTextBox.Text))
                {
                    string requiredPrefix = rememberCredentialsCheckBox.Checked ? "saved:" : "session:";
                    if (string.IsNullOrWhiteSpace(currentCredentialKey) || !currentCredentialKey.StartsWith(requiredPrefix, StringComparison.Ordinal)) currentCredentialKey = requiredPrefix + Guid.NewGuid().ToString("N");
                    await credentialService.SaveAsync(currentCredentialKey, userNameTextBox.Text, passwordTextBox.Text, CancellationToken.None);
                }
                if (string.IsNullOrWhiteSpace(currentCredentialKey) || !await credentialService.ExistsAsync(currentCredentialKey, CancellationToken.None)) throw new InvalidOperationException("A SQL Server password is required.");
                credentialKey = currentCredentialKey;
            }
            var connection = new DatabaseConnection
            {
                ServerName = serverTextBox.Text.Trim(), DatabaseName = string.IsNullOrWhiteSpace(databaseComboBox.Text) ? "master" : databaseComboBox.Text.Trim(),
                AuthenticationType = sqlAuthentication ? AuthenticationType.SqlServer : AuthenticationType.Windows,
                UserName = sqlAuthentication ? userNameTextBox.Text.Trim() : null, CredentialKey = credentialKey,
                Encrypt = encryptCheckBox.Checked, TrustServerCertificate = trustCertificateCheckBox.Checked,
                ConnectionTimeoutSeconds = (int)timeoutNumericUpDown.Value
            };
            connection.Validate();
            return connection;
        }

        private void SetBusy(bool busy)
        {
            testButton.Enabled = !busy; connectButton.Enabled = !busy; profileComboBox.Enabled = !busy; newProfileButton.Enabled = !busy; saveProfileButton.Enabled = !busy; deleteProfileButton.Enabled = !busy && selectedProfile != null; statusLabel.Text = busy ? "Connecting..." : statusLabel.Text; UseWaitCursor = busy;
        }

        private sealed class ProfileItem
        {
            public ProfileItem(ConnectionProfile profile) { Profile = profile; }
            public ConnectionProfile Profile { get; private set; }
            public override string ToString() { return Profile.Name; }
        }
    }
}