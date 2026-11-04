using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OfficeSmart.Updates;

namespace OfficeSmart;

public partial class InstallerForm
{
    private bool appUpdateInProgress;
    private bool closingForAppUpdate;
    private Func<UpdateOffer, IProgress<long>, CancellationToken, Task> prepareAppUpdate = PortableUpdate.PrepareAsync;
    private Func<string, bool> confirmAppUpdate = null;
    private Action<string> reportAppUpdateFailure = null;

    private void InstallAppUpdate(UpdateManifest manifest, Version version)
    {
        if (appUpdateInProgress || worker.IsBusy || IsDisposed) return;
        try
        {
            if (!IsValidUpdateManifest(manifest)) throw new IOException("Invalid release metadata.");
            var offer = new UpdateOffer { Version = manifest.Version, Sha256 = manifest.Portable.Sha256,
                MinimumVersion = manifest.MinimumSupportedVersion, Signed = manifest.Signing != null && manifest.Signing.Required };
            PortableUpdate.ValidateOffer(offer, GetCurrentFileVersion());
            string message = "Update Smart Office Installer to " + version + "?\r\n\r\n" +
                "The app will download and verify the new version, then close and reopen. " +
                "The new copy keeps the name Smart Office Installer.exe. " +
                "Your previous EXE is kept as OLD- (or OLD.V1-, OLD.V2-) after the new window opens.\r\n\r\n" +
                "This updates the app only. It does not install or remove Office or restart Windows.";
            bool accepted = confirmAppUpdate?.Invoke(message) ?? MessageBox.Show(this, message, "Update available",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            if (!accepted) return;
            bool handedOff = false;
            appUpdateInProgress = true;
            try
            {
                using var cancellation = new CancellationTokenSource();
                using var dialog = new Form { Text = "Updating Smart Office Installer", ClientSize = new Size(440, 155),
                    StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false, MinimizeBox = false, BackColor = BG, ForeColor = TXT, Font = Font };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 3 };
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                var status = new Label { Text = "Downloading and verifying the application update...", Dock = DockStyle.Fill, AutoSize = false };
                var bar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee };
                var cancel = new Button { Text = "Cancel", AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Color.Black };
                layout.Controls.Add(status, 0, 0); layout.Controls.Add(bar, 0, 1); layout.Controls.Add(cancel, 0, 2);
                dialog.Controls.Add(layout);
                bool finished = false;
                Exception failure = null;
                cancel.Click += (_, _) => { cancellation.Cancel(); cancel.Enabled = false; status.Text = "Cancelling..."; };
                dialog.FormClosing += (_, e) =>
                {
                    if (!finished) { cancellation.Cancel(); cancel.Enabled = false; status.Text = "Cancelling..."; e.Cancel = true; }
                };
                dialog.Shown += async (_, _) =>
                {
                    try
                    {
                        var progress = new Progress<long>(bytes =>
                        {
                            if (!finished && !cancellation.IsCancellationRequested && !dialog.IsDisposed)
                                status.Text = "Downloaded " + (bytes / 1024) + " KB. Verifying before reopening...";
                        });
                        await prepareAppUpdate(offer, progress, cancellation.Token);
                        handedOff = true;
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { failure = ex; }
                    finally { finished = true; dialog.Close(); }
                };
                dialog.ShowDialog(this);
                if (failure != null) throw failure;
            }
            finally { appUpdateInProgress = false; }
            if (handedOff) { closingForAppUpdate = true; Close(); }
            else SetUpdateButtonAvailable(manifest, version);
        }
        catch (Exception ex)
        {
            string message = "The app update could not be started. Your current app remains available.\r\n\r\n" + ex.Message;
            if (reportAppUpdateFailure != null) reportAppUpdateFailure(message);
            else MessageBox.Show(this, message, "Application update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
