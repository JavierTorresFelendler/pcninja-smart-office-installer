using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using OfficeSmart.Updates;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == PortableUpdate.HelperArgument) return PortableUpdate.RunHelper();
        string readiness = Environment.GetEnvironmentVariable(UpdateReadiness.EnvironmentName);
        Environment.SetEnvironmentVariable(UpdateReadiness.EnvironmentName, null);
        if (UpdateReadiness.Valid(readiness))
        {
            Application.EnableVisualStyles();
            using var form = new Form { Text = "Application updater VM fixture - no Office operations", Width = 500 };
            form.Shown += async (_, _) => { form.Update(); await UpdateReadiness.SignalAsync(readiness); await System.Threading.Tasks.Task.Delay(1000); form.Close(); };
            Application.Run(form);
            return 0;
        }
        if (!PortableUpdate.IsAdmin || args.Length != 1) return 5;
        try
        {
            string originalPath = PortableUpdate.ExecutablePath;
            using var files = new UpdateFiles(Path.GetDirectoryName(originalPath), Guid.NewGuid().ToString("N"), true);
            File.Copy(args[0], files.Candidate, false);
            File.Copy(originalPath, files.Helper, false);
            UpdateFiles.MakePrivate(files.Helper);
            string originalHash;
            using (var original = new UpdateFiles.LockedFile(originalPath, false)) originalHash = original.Hash();
            using var candidate = new UpdateFiles.LockedFile(files.Candidate, false);
            var offer = new UpdateOffer { Version = FileVersionInfo.GetVersionInfo(files.Candidate).FileVersion,
                Sha256 = candidate.Hash(), MinimumVersion = "3.3.4.0", Signed = false };
            PortableUpdate.ValidateOffer(offer, Version.Parse(FileVersionInfo.GetVersionInfo(originalPath).FileVersion));
            PortableUpdate.VerifyExecutable(candidate, offer.Version, offer.Sha256);
            candidate.Dispose();
            using var parent = Process.GetCurrentProcess();
            var request = new UpdateRequest { OperationId = Path.GetFileName(files.Operation), CreatedTicks = DateTime.UtcNow.Ticks,
                ParentId = parent.Id, ParentStartTicks = parent.StartTime.ToUniversalTime().Ticks,
                ParentPath = originalPath, ParentHash = originalHash, Offer = offer };
            PortableUpdate.StartHelperAsync(files, request, CancellationToken.None).GetAwaiter().GetResult();
            return 0; // Helper must observe this exact parent exit before publishing.
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(Path.GetDirectoryName(PortableUpdate.ExecutablePath), "fixture-error.txt"), ex.ToString()); return 1; }
    }
}
