using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using OfficeSmart.Updates;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string pipe = Environment.GetEnvironmentVariable(UpdateReadiness.EnvironmentName);
        Environment.SetEnvironmentVariable(UpdateReadiness.EnvironmentName, null);
        if (UpdateReadiness.Valid(pipe))
        {
            Application.EnableVisualStyles();
            using var form = new Form { Text = "Updater readiness fixture", ShowInTaskbar = false };
            form.Shown += async (_, _) => { form.Update(); await UpdateReadiness.SignalAsync(pipe); await System.Threading.Tasks.Task.Delay(500); form.Close(); };
            Application.Run(form);
            return 0;
        }
        try
        {
            OfferChecks();
            StreamChecks();
            DownloadChecks();
            MetadataAndAclChecks();
            foreach (string mode in new[] { "success", "rollback", "collision", "occupied-target", "changed-original" }) Transaction(mode);
            string self = System.Reflection.Assembly.GetExecutingAssembly().Location;
            using var pinned = new UpdateFiles.LockedFile(self, true);
            bool ready = UpdateReadiness.Launch(self, out var launched);
            using (launched) { Check(ready, "Actual GUI readiness failed."); launched.WaitForExit(5000); }
            Console.WriteLine("PASS: Pinned offers, bounded/cancelled transfer, handle-bound moves, OLD collision, rollback, occupied target, changed-original refusal and real child GUI readiness. Disposable fixture files only; no Office or real app replaced.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Refused(Action call)
    {
        try { call(); } catch (IOException) { return; }
        throw new Exception("An unsafe case was accepted.");
    }
    private static void OfferChecks()
    {
        var current = new Version(3, 3, 11, 0);
        var offer = new UpdateOffer { Version = "3.3.12.0", MinimumVersion = "3.3.4.0", Sha256 = new string('A', 64) };
        PortableUpdate.ValidateOffer(offer, current);
        Check(offer.DownloadUrl.EndsWith("/releases/download/v3.3.12/SmartOfficeInstaller.exe"), "URL not pinned.");
        offer.Version = "3.3.11.0"; Refused(() => PortableUpdate.ValidateOffer(offer, current));
        offer.Version = "3.3.12.0"; offer.MinimumVersion = "3.3.12.0"; Refused(() => PortableUpdate.ValidateOffer(offer, current));
        offer.MinimumVersion = "3.3.4.0"; offer.Sha256 = new string('Z', 64); Refused(() => PortableUpdate.ValidateOffer(offer, current));
        offer.Sha256 = new string('A', 64); offer.Signed = true; Refused(() => PortableUpdate.ValidateOffer(offer, current));
        Check(!UpdateReadiness.Valid("anything") && !UpdateReadiness.Valid("PcNinja.OfficeReady.fake"), "Invalid readiness accepted.");
        Refused(() => PortableUpdate.ReadLine(new StringReader(new string('x', 200) + "\n"), 128, 1000));
    }
    private static void StreamChecks()
    {
        using var input = new MemoryStream(new byte[70000]);
        using var output = new MemoryStream();
        Refused(() => PortableUpdate.CopyBounded(input, output, 1024, null, CancellationToken.None).GetAwaiter().GetResult());
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); input.Position = 0;
        try { PortableUpdate.CopyBounded(input, output, 100000, null, cancel.Token).GetAwaiter().GetResult(); throw new Exception("Cancellation ignored."); }
        catch (OperationCanceledException) { }
    }
    private static void Transaction(string mode)
    {
        string folder = Path.Combine(Path.GetTempPath(), "PcNinja-OfficeFixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            byte[] oldBytes = Encoding.UTF8.GetBytes("OLD DISPOSABLE FIXTURE");
            byte[] newBytes = Encoding.UTF8.GetBytes("NEW DISPOSABLE FIXTURE");
            string originalPath = Path.Combine(folder, PortableUpdate.CanonicalName);
            File.WriteAllBytes(originalPath, oldBytes);
            using (var files = new UpdateFiles(folder, Guid.NewGuid().ToString("N"), true, true))
            {
                File.WriteAllBytes(files.Candidate, newBytes);
                string oldArchive = Path.Combine(folder, "OLD-" + PortableUpdate.CanonicalName);
                if (mode == "collision") File.WriteAllText(oldArchive, "PREEXISTING ARCHIVE");
                var request = new UpdateRequest { ParentPath = originalPath, ParentHash = Hash(oldBytes),
                    Offer = new UpdateOffer { Version = "3.3.12.0", Sha256 = Hash(newBytes) } };
                if (mode == "changed-original") File.WriteAllText(originalPath, "CHANGED");
                using var original = new UpdateFiles.LockedFile(originalPath, true);
                using var candidate = new UpdateFiles.LockedFile(files.Candidate, true);
                if (mode == "occupied-target") request.ParentPath = Path.Combine(folder, "source.exe");
                PortableUpdate.Launch launch = (string path, out Process process) =>
                {
                    process = null;
                    Check(candidate.PathName == path && candidate.Hash() == Hash(newBytes), "Launched wrong bytes.");
                    return mode != "rollback";
                };
                if (mode == "rollback" || mode == "changed-original")
                    Refused(() => PortableUpdate.Apply(files, request, original, candidate, launch));
                else if (mode == "occupied-target")
                {
                    // Occupy the target only after the exact original is moved.
                    // A fixture source name differs from the canonical target.
                    original.Move(request.ParentPath);
                    File.WriteAllText(originalPath, "EXISTING TARGET");
                    try { PortableUpdate.Apply(files, request, original, candidate, launch); throw new Exception("Overwrote existing target."); }
                    catch (System.ComponentModel.Win32Exception) { }
                    Check(File.ReadAllText(originalPath) == "EXISTING TARGET", "Existing target changed.");
                    Check(original.Hash() == Hash(oldBytes), "Original bytes lost.");
                }
                else PortableUpdate.Apply(files, request, original, candidate, launch);
                if (mode == "success") Check(File.Exists(oldArchive) && original.Hash() == Hash(oldBytes), "Missing OLD archive.");
                if (mode == "collision") Check(File.ReadAllText(oldArchive) == "PREEXISTING ARCHIVE" &&
                    original.PathName == Path.Combine(folder, "OLD.V1-" + PortableUpdate.CanonicalName), "Archive collision overwrote bytes.");
                if (mode == "rollback") Check(original.PathName == originalPath && original.Hash() == Hash(oldBytes), "Rollback did not restore original.");
            }
        }
        finally
        {
            string verified = Path.GetFullPath(folder);
            if (Path.GetDirectoryName(verified) == Path.GetTempPath().TrimEnd('\\') && Path.GetFileName(verified).StartsWith("PcNinja-OfficeFixture-", StringComparison.Ordinal))
                Directory.Delete(verified, true);
        }
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        internal Func<HttpRequestMessage, HttpResponseMessage> Reply;
        protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation) =>
            System.Threading.Tasks.Task.FromResult(Reply(request));
    }
    private static void DownloadChecks()
    {
        string path = Path.Combine(Path.GetTempPath(), "PcNinja-DownloadFixture-" + Guid.NewGuid().ToString("N"));
        var offer = new UpdateOffer { Version = "3.3.12.0" };
        try
        {
            foreach (string mode in new[] { "bad-status", "bad-redirect", "incomplete", "success" })
            {
                using var handler = new FakeHttp();
                handler.Reply = request =>
                {
                    Check(request.RequestUri == new Uri(offer.DownloadUrl), "Download URL changed.");
                    var response = new HttpResponseMessage(mode == "bad-status" ? HttpStatusCode.NotFound :
                        mode == "bad-redirect" ? HttpStatusCode.Redirect : HttpStatusCode.OK);
                    if (mode == "bad-redirect") response.Headers.Location = new Uri("https://example.com/untrusted.exe");
                    response.Content = new ByteArrayContent(new byte[16]);
                    if (mode == "incomplete") response.Content.Headers.ContentLength = 32;
                    return response;
                };
                using var client = new HttpClient(handler);
                Action run = () => PortableUpdate.DownloadWithClientAsync(client, offer, path, null, CancellationToken.None).GetAwaiter().GetResult();
                if (mode == "success") { run(); Check(new FileInfo(path).Length == 16, "Download not saved."); }
                else Refused(run);
                if (File.Exists(path)) File.Delete(path);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void MetadataAndAclChecks()
    {
        string folder = Path.Combine(Path.GetTempPath(), "PcNinja-OfficeFixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string state = Path.Combine(folder, UpdateFiles.StateFolder);
            Directory.CreateDirectory(state); // Inherited/user-created state is refused, never adopted.
            Refused(() => { using var unsafeState = new UpdateFiles(folder, Guid.NewGuid().ToString("N"), true, true); });
            string built = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../bin/Release/net48/Smart Office Installer.exe"));
            string candidate = Path.Combine(folder, "candidate.exe");
            File.Copy(built, candidate);
            using var file = new UpdateFiles.LockedFile(candidate, false);
            string version = FileVersionInfo.GetVersionInfo(candidate).FileVersion;
            PortableUpdate.VerifyExecutable(file, version, file.Hash());
            Refused(() => PortableUpdate.VerifyExecutable(file, "99.0.0.0", file.Hash()));
            Refused(() => PortableUpdate.VerifyExecutable(file, version, new string('0', 64)));
        }
        finally { Directory.Delete(folder, true); }
    }
}
