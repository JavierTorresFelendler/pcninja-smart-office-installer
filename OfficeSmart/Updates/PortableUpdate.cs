using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OfficeSmart.Updates;

[DataContract]
internal sealed class UpdateOffer
{
    [DataMember] public string Version;
    [DataMember] public string Sha256;
    [DataMember] public string MinimumVersion;
    [DataMember] public bool Signed;
    internal string DownloadUrl => "https://github.com/JavierTorresFelendler/pcninja-smart-office-installer/releases/download/v" + System.Version.Parse(Version).ToString(3) + "/SmartOfficeInstaller.exe";
}

[DataContract]
internal sealed class UpdateRequest
{
    [DataMember] public int Protocol = 1;
    [DataMember] public string OperationId;
    [DataMember] public long CreatedTicks;
    [DataMember] public int ParentId;
    [DataMember] public long ParentStartTicks;
    [DataMember] public string ParentPath;
    [DataMember] public string ParentHash;
    [DataMember] public string ParentVersion;
    [DataMember] public UpdateOffer Offer;
    [DataMember] public int HelperId;
    [DataMember] public long HelperStartTicks;
}

internal static class PortableUpdate
{
    internal const string HelperArgument = "--internal-office-app-update";
    internal const string CanonicalName = "Smart Office Installer.exe";
    internal const long MaximumBytes = 32 * 1024 * 1024;
    private const string Ready = "OFFICE_UPDATE_HELPER_READY_1", Go = "OFFICE_UPDATE_GO_1";
    internal static string ExecutablePath => Assembly.GetExecutingAssembly().Location;
    internal static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    internal static void ValidateOffer(UpdateOffer offer, Version current)
    {
        if (offer == null || !Version.TryParse(offer.Version, out var next) || next.Revision != 0 || next <= current ||
            string.IsNullOrEmpty(offer.Sha256) || offer.Sha256.Length != 64)
            throw new IOException("Only a verified newer stable version can be installed.");
        foreach (char c in offer.Sha256) if (!Uri.IsHexDigit(c)) throw new IOException("Invalid update checksum.");
        if (!Version.TryParse(offer.MinimumVersion, out var minimum) || current < minimum)
            throw new IOException("This copy requires a manual update before it can use in-app updates.");
        if (offer.Signed) throw new IOException("This update requires a signed publisher verification policy. Use the official release page.");
    }

    internal static void VerifyExecutable(UpdateFiles.LockedFile file, string version, string hash)
    {
        if (file.Length <= 0 || file.Length > MaximumBytes) throw new IOException("Invalid update size.");
        file.RequireHash(hash);
        var info = FileVersionInfo.GetVersionInfo(file.PathName);
        if (info.FileVersion != version || info.ProductName != "Smart Office Installer" || info.CompanyName != "PcNinja.Pro")
            throw new IOException("The update does not match this product or version.");
        using var stream = File.OpenRead(file.PathName);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt16() != 0x5a4d) throw new IOException("The update is not a Windows executable.");
        stream.Position = 0x3c;
        int offset = reader.ReadInt32();
        if (offset < 64 || offset > stream.Length - 26) throw new IOException("Invalid executable header.");
        stream.Position = offset;
        if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664) throw new IOException("Only x64 application updates are supported.");
        // Preserve publisher continuity by refusing to replace signed copies until
        // an Authenticode policy is implemented. Unsigned hashes are GitHub trust only.
        try { X509Certificate.CreateFromSignedFile(file.PathName); }
        catch (System.Security.Cryptography.CryptographicException) { return; }
        throw new IOException("Signed applications require publisher verification; use the official release page.");
    }

    internal static async Task<string> ReadMetadataAsync(string url)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var client = Client();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = new MemoryStream();
        await CopyBounded(input, output, 128 * 1024, null, timeout.Token).ConfigureAwait(false);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static HttpClient Client()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PcNinja-SmartOfficeInstaller");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    internal static async Task DownloadAsync(UpdateOffer offer, string destination, IProgress<long> progress, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var client = Client();
        await DownloadWithClientAsync(client, offer, destination, progress, timeout.Token).ConfigureAwait(false);
    }

    internal static async Task DownloadWithClientAsync(HttpClient client, UpdateOffer offer, string destination, IProgress<long> progress, CancellationToken cancellation)
    {
        Uri uri = new Uri(offer.DownloadUrl);
        for (int redirects = 0; redirects < 5; redirects++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                (uri.Host != "github.com" && uri.Host != "release-assets.githubusercontent.com" && uri.Host != "objects.githubusercontent.com"))
                throw new IOException("The update redirected outside the approved GitHub download hosts.");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
            {
                if (response.Headers.Location == null) throw new IOException("Missing update redirect.");
                uri = new Uri(uri, response.Headers.Location);
                continue;
            }
            if (status != 200) throw new IOException("The release download returned HTTP " + status + ".");
            long? size = response.Content.Headers.ContentLength;
            if (size.HasValue && (size <= 0 || size > MaximumBytes)) throw new IOException("Invalid release download size.");
            using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            long total = await CopyBounded(input, output, MaximumBytes, progress, cancellation).ConfigureAwait(false);
            await output.FlushAsync(cancellation).ConfigureAwait(false);
            if (total == 0 || (size.HasValue && total != size)) throw new IOException("The update download was incomplete.");
            return;
        }
        throw new IOException("Too many release download redirects.");
    }

    internal static async Task<long> CopyBounded(Stream input, Stream output, long maximum, IProgress<long> progress, CancellationToken cancellation)
    {
        long total = 0;
        byte[] bytes = new byte[65536];
        int count;
        while ((count = await input.ReadAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false)) != 0)
        {
            total += count;
            if (total > maximum) throw new IOException("The update exceeds the download limit.");
            await output.WriteAsync(bytes, 0, count, cancellation).ConfigureAwait(false);
            progress?.Report(total);
        }
        return total;
    }

    internal static async Task PrepareAsync(UpdateOffer offer, IProgress<long> progress, CancellationToken cancellation)
    {
        if (!IsAdmin) throw new IOException("Run the application as administrator to update it.");
        string parentPath = UpdateFiles.LocalPath(ExecutablePath);
        Version current = Version.Parse(FileVersionInfo.GetVersionInfo(parentPath).FileVersion);
        ValidateOffer(offer, current);
        string id = Guid.NewGuid().ToString("N");
        using var files = new UpdateFiles(Path.GetDirectoryName(parentPath), id, true);
        using var preparation = files.CreatePrivate(Path.Combine(files.Operation, "preparing.lock"), deleteOnClose: true);
        if (File.Exists(files.Active)) throw new IOException("A previous app update is pending. Reopen the app to complete recovery first.");
        using var original = new UpdateFiles.LockedFile(parentPath, false);
        string originalHash = original.Hash();
        VerifyExecutable(original, current.ToString(), originalHash);
        string target = Path.Combine(files.Folder, CanonicalName);
        if (!string.Equals(parentPath, target, StringComparison.OrdinalIgnoreCase) && (File.Exists(target) || Directory.Exists(target)))
            throw new IOException("Another Smart Office Installer.exe already exists in this folder. Move this copy to its own folder first.");
        var drive = new DriveInfo(Path.GetPathRoot(files.Folder));
        if (drive.AvailableFreeSpace < MaximumBytes * 3) throw new IOException("Not enough free space for safe application update staging.");
        await DownloadAsync(offer, files.Candidate, progress, cancellation);
        UpdateFiles.MakePrivate(files.Candidate);
        using (var candidate = new UpdateFiles.LockedFile(files.Candidate, false)) VerifyExecutable(candidate, offer.Version, offer.Sha256);
        File.Copy(parentPath, files.Helper, false);
        UpdateFiles.MakePrivate(files.Helper);
        using var helperGuard = new UpdateFiles.LockedFile(files.Helper, false);
        helperGuard.RequireHash(originalHash);
        using var parent = Process.GetCurrentProcess();
        var request = new UpdateRequest { OperationId = id, CreatedTicks = DateTime.UtcNow.Ticks, ParentId = parent.Id,
            ParentStartTicks = parent.StartTime.ToUniversalTime().Ticks, ParentPath = parentPath, ParentHash = originalHash, ParentVersion = current.ToString(4), Offer = offer };
        cancellation.ThrowIfCancellationRequested();
        original.Dispose(); // Helper reacquires and verifies the exact original before READY.
        await StartHelperAsync(files, request, cancellation);
    }

    internal static async Task StartHelperAsync(UpdateFiles files, UpdateRequest request, CancellationToken cancellation)
    {
        using var helperGuard = new UpdateFiles.LockedFile(files.Helper, false);
        helperGuard.RequireHash(request.ParentHash);
        var start = new ProcessStartInfo { FileName = files.Helper, Arguments = HelperArgument, WorkingDirectory = files.Operation,
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var helper = Process.Start(start) ?? throw new IOException("The update helper could not start.");
        helper.StandardInput.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize(request))));
        helper.StandardInput.Flush();
        try
        {
            string ready = await Task.Run(() => ReadLine(helper.StandardOutput, 128, 20000), cancellation);
            if (ready != Ready) throw new IOException("The update helper was not ready. The current app remains open.");
            cancellation.ThrowIfCancellationRequested();
            files.MarkActive(request.OperationId);
            helper.StandardInput.WriteLine(Go);
            helper.StandardInput.Flush();
        }
        catch { helper.StandardInput.Close(); throw; }
        // Caller may close only now. Helper retains the original file and process.
    }

    internal static int RunHelper()
    {
        UpdateFiles files = null;
        bool parentExited = false;
        try
        {
            if (!IsAdmin) return 5;
            string operation = Path.GetDirectoryName(ExecutablePath);
            string id = Path.GetFileName(operation);
            string state = Path.GetDirectoryName(operation);
            if (Path.GetFileName(state) != UpdateFiles.StateFolder || Path.GetFileName(ExecutablePath) != "helper.exe") return 2;
            files = new UpdateFiles(Path.GetDirectoryName(state), id, false);
            var request = Deserialize<UpdateRequest>(Encoding.UTF8.GetString(Convert.FromBase64String(ReadLine(Console.In, 32768, 20000))));
            ValidateRequest(request, files, id);
            using var parent = Process.GetProcessById(request.ParentId);
            _ = parent.Handle;
            if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartTicks ||
                !string.Equals(parent.MainModule.FileName, request.ParentPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The original process identity changed.");
            using (var helper = new UpdateFiles.LockedFile(ExecutablePath, false)) helper.RequireHash(request.ParentHash);
            using var original = new UpdateFiles.LockedFile(request.ParentPath, false);
            original.RequireHash(request.ParentHash);
            using (var verify = new UpdateFiles.LockedFile(files.Candidate, false))
                VerifyExecutable(verify, request.Offer.Version, request.Offer.Sha256);
            using var candidate = new UpdateFiles.LockedFile(files.Candidate, true);
            candidate.RequireHash(request.Offer.Sha256);
            using (var self = Process.GetCurrentProcess())
            { request.HelperId = self.Id; request.HelperStartTicks = self.StartTime.ToUniversalTime().Ticks; }
            files.WriteJournal(request);
            Console.WriteLine(Ready);
            Console.Out.Flush();
            if (ReadLine(Console.In, 128, 20000) != Go) throw new IOException("Update handoff was not committed.");
            if (!parent.WaitForExit(60000)) throw new IOException("The original app did not exit; no files were moved.");
            parentExited = true;
            Apply(files, request, original, candidate, UpdateReadiness.Launch);
            return 0;
        }
        catch (Exception ex)
        {
            try { if (files != null) File.WriteAllText(Path.Combine(files.Operation, "result.txt"), "Update incomplete: " + ex.Message); } catch { }
            if (parentExited)
                System.Windows.Forms.MessageBox.Show("The app update could not complete. Your previous EXE has been kept.\r\n\r\n" +
                    ex.Message + "\r\n\r\nRecovery details: " + files.Operation, "Application update", System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            return 1;
        }
        finally { files?.Dispose(); }
    }

    internal delegate bool Launch(string path, out Process launched);
    internal static void Apply(UpdateFiles files, UpdateRequest request, UpdateFiles.LockedFile original, UpdateFiles.LockedFile candidate, Launch launch)
    {
        string target = Path.Combine(files.Folder, CanonicalName);
        original.RequireHash(request.ParentHash);
        candidate.RequireHash(request.Offer.Sha256);
        if (!File.Exists(files.Journal)) files.WriteJournal(request);
        original.CopyToPrivate(files, files.Backup);
        using (var backup = new UpdateFiles.LockedFile(files.Backup, false)) backup.RequireHash(request.ParentHash);
        bool sameName = string.Equals(target, request.ParentPath, StringComparison.OrdinalIgnoreCase);
        original.Dispose();
        Process launched = null;
        UpdateFiles.LockedFile launchGuard = null;
        bool published = false, acknowledged = false;
        try
        {
            // Same-name publication is one atomic NTFS replacement. Keep a secure
            // independently verified predecessor snapshot, plus every displaced
            // target (even an unexpected raced file); no bytes are discarded.
            candidate.Dispose();
            if (sameName) File.Replace(files.Candidate, target, files.Displaced);
            else File.Move(files.Candidate, target); // An occupied different-name target is never overwritten.
            published = true;
            // CLR startup may refuse a freshly renamed image while a DELETE
            // access handle is held. Reopen with read-only/no-delete sharing and
            // reverify exact bytes before CreateProcess. Any raced substitution
            // is rejected; no unverified path is ever launched.
            launchGuard = new UpdateFiles.LockedFile(target, false);
            launchGuard.RequireHash(request.Offer.Sha256);
            files.PublishPermissions(target); // Permission changes are also bound to the verified, pinned file.
            if (sameName)
            {
                if (!File.Exists(files.Displaced)) throw new IOException("Atomic replacement did not preserve its displaced target.");
                using var displaced = new UpdateFiles.LockedFile(files.Displaced, false);
                displaced.RequireHash(request.ParentHash);
                if (IsAdmin) UpdateFiles.MakePrivate(files.Displaced);
            }
            acknowledged = launch(target, out launched);
            if (!acknowledged) throw new IOException("The new window did not confirm startup.");
        }
        catch
        {
            launchGuard?.Dispose();
            // Never kill a new process: it could already be doing user work.
            // Restore only when it is certainly gone, and only our exact bytes.
            if (launched == null || launched.HasExited)
            {
                if (!published && sameName && !File.Exists(target))
                {
                    // ReplaceFile documents a partial-failure status where the
                    // displaced name exists but the canonical name is absent.
                    using var rescue = new UpdateFiles.LockedFile(files.Backup, true);
                    rescue.RequireHash(request.ParentHash);
                    rescue.Move(target);
                    files.PublishPermissions(target);
                }
                if (published)
                {
                    using (var verify = new UpdateFiles.LockedFile(target, false)) verify.RequireHash(request.Offer.Sha256);
                    if (sameName)
                    {
                        using (var backup = new UpdateFiles.LockedFile(files.Backup, false)) backup.RequireHash(request.ParentHash);
                        File.Replace(files.Backup, target, Path.Combine(files.Operation, "failed.exe"));
                        using var restored = new UpdateFiles.LockedFile(target, false);
                        restored.RequireHash(request.ParentHash);
                        files.PublishPermissions(target);
                    }
                    else
                    {
                        using var failed = new UpdateFiles.LockedFile(target, true);
                        failed.RequireHash(request.Offer.Sha256);
                        failed.Move(Path.Combine(files.Operation, "failed.exe"));
                    }
                }
                files.ClearActive(request.OperationId);
            }
            throw;
        }
        finally { launchGuard?.Dispose(); launched?.Dispose(); }
        if (acknowledged)
        {
            try
            {
                string archived = ArchivePrevious(files, request);
                File.WriteAllText(Path.Combine(files.Operation, "result.txt"), "Updated to " + request.Offer.Version + "; previous copy: " + archived);
                files.ClearActive(request.OperationId);
            }
            catch (Exception ex) { try { File.WriteAllText(Path.Combine(files.Operation, "result.txt"), "Updated; previous copy retained at " + files.Backup + ". " + ex.Message); } catch { } }
        }
    }

    private static string ArchivePrevious(UpdateFiles files, UpdateRequest request)
    {
        bool separateSource = !string.Equals(request.ParentPath, Path.Combine(files.Folder, CanonicalName), StringComparison.OrdinalIgnoreCase);
        if (!File.Exists(files.Backup) || (separateSource && !File.Exists(request.ParentPath)))
        {
            string path = UpdateArchives.Find(files, request);
            using var existingArchive = new UpdateFiles.LockedFile(path, true);
            string recovered = UpdateArchives.Archive(files, request, existingArchive);
            existingArchive.Dispose();
            UpdateArchives.Publish(files, request, recovered);
            return recovered;
        }
        if (!string.Equals(request.ParentPath, Path.Combine(files.Folder, CanonicalName), StringComparison.OrdinalIgnoreCase) && File.Exists(request.ParentPath))
        {
            using var source = new UpdateFiles.LockedFile(request.ParentPath, true);
            source.RequireHash(request.ParentHash);
            string archivedSource = UpdateArchives.Archive(files, request, source);
            source.Dispose();
            UpdateArchives.Publish(files, request, archivedSource);
            return archivedSource;
        }
        using var previous = new UpdateFiles.LockedFile(files.Backup, true);
        previous.RequireHash(request.ParentHash);
        string archived = UpdateArchives.Archive(files, request, previous);
        previous.Dispose();
        UpdateArchives.Publish(files, request, archived);
        return archived;
    }

    internal static void RecoverAfterStartup()
    {
        if (!IsAdmin) return;
        string folder = Path.GetDirectoryName(ExecutablePath);
        string activePath = Path.Combine(folder, UpdateFiles.StateFolder, "active-operation");
        if (!File.Exists(activePath)) return;
        try
        {
            using var state = new UpdateFiles(folder, Guid.Empty.ToString("N"), false, stateOnly: true);
            string id;
            using (var marker = new UpdateFiles.LockedFile(state.Active, false)) id = marker.ReadText(32);
            using var files = new UpdateFiles(folder, id, false);
            UpdateRequest request;
            using (var record = new UpdateFiles.LockedFile(files.Journal, false)) request = Deserialize<UpdateRequest>(record.ReadText(32768));
            if (request == null || request.OperationId != id || request.Protocol != 1 ||
                !string.Equals(Path.GetDirectoryName(UpdateFiles.LocalPath(request.ParentPath)), files.Folder, StringComparison.OrdinalIgnoreCase) ||
                !request.ParentPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid recovery record.");
            if (HelperAlive(files, request)) return;
            if (!File.Exists(activePath)) return; // Helper completed while its process identity was being checked.
            RecoverConfirmed(files, request, ExecutablePath);
        }
        catch (System.ComponentModel.Win32Exception ex) when ((ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3) && !File.Exists(activePath)) { }
        catch (FileNotFoundException) when (!File.Exists(activePath)) { }
        catch (DirectoryNotFoundException) when (!File.Exists(activePath)) { }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show("A previous app update needs attention. All recovery copies have been kept.\r\n\r\n" + ex.Message,
                "App update recovery", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
        }
    }

    internal static bool IsRecoveryCopy => string.Equals(Path.GetFileName(ExecutablePath), "helper.exe", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(ExecutablePath))), UpdateFiles.StateFolder, StringComparison.OrdinalIgnoreCase);

    internal static bool HelperAlive(UpdateFiles files, UpdateRequest request)
    {
        if (request.HelperId <= 0) return false;
        try
        {
            using var helper = Process.GetProcessById(request.HelperId);
            _ = helper.Handle;
            return !helper.HasExited && helper.StartTime.ToUniversalTime().Ticks == request.HelperStartTicks &&
                string.Equals(helper.MainModule.FileName, files.Helper, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; } // Access failure or concurrent process exit: never take over an unconfirmed live helper.
    }

    internal static bool RecoverFromHelper()
    {
        try
        {
            string operation = Path.GetDirectoryName(ExecutablePath);
            string id = Path.GetFileName(operation);
            using var files = new UpdateFiles(Path.GetDirectoryName(Path.GetDirectoryName(operation)), id, false);
            UpdateRequest request;
            using (var record = new UpdateFiles.LockedFile(files.Journal, false)) request = Deserialize<UpdateRequest>(record.ReadText(32768));
            if (request == null || request.OperationId != id || request.Protocol != 1 ||
                !string.Equals(Path.GetDirectoryName(UpdateFiles.LocalPath(request.ParentPath)), files.Folder, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid recovery journal.");
            using (var self = new UpdateFiles.LockedFile(ExecutablePath, false)) self.RequireHash(request.ParentHash);
            using (var active = new UpdateFiles.LockedFile(files.Active, false))
                if (active.ReadText(32) != id) throw new IOException("This operation is no longer pending.");
            if (HelperAlive(files, request)) throw new IOException("The update helper is still applying this update. Recovery did not change any files.");
            try
            {
                using var parent = Process.GetProcessById(request.ParentId);
                if (!parent.HasExited && parent.StartTime.ToUniversalTime().Ticks == request.ParentStartTicks)
                    throw new IOException("The original application is still running. Finish its work before recovering.");
            }
            catch (ArgumentException) { }
            string target = Path.Combine(files.Folder, CanonicalName);
            if (!File.Exists(target) && !File.Exists(request.ParentPath))
            {
                using var backup = new UpdateFiles.LockedFile(files.Backup, true);
                backup.RequireHash(request.ParentHash);
                backup.Move(request.ParentPath); // Exact verified recovery; an occupied path is never overwritten.
                files.PublishPermissions(request.ParentPath);
            }
            string launchPath = File.Exists(target) ? target : request.ParentPath;
            using var guard = new UpdateFiles.LockedFile(launchPath, false);
            string hash = guard.Hash();
            if (!string.Equals(hash, request.ParentHash, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(hash, request.Offer.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Recovery target changed; no file was run.");
            using var recovered = Process.Start(new ProcessStartInfo { FileName = launchPath, WorkingDirectory = files.Folder, UseShellExecute = false });
            return recovered != null;
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show("Recovery kept all copies.\r\n\r\n" + ex.Message, "App update recovery",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            return false;
        }
    }

    internal static void RecoverConfirmed(UpdateFiles files, UpdateRequest request, string currentPath)
    {
        using var current = new UpdateFiles.LockedFile(currentPath, false);
        string hash = current.Hash();
        if (string.Equals(hash, request.ParentHash, StringComparison.OrdinalIgnoreCase))
        {
            files.ClearActive(request.OperationId); // Replacement never committed, or rollback succeeded.
            File.WriteAllText(Path.Combine(files.Operation, "result.txt"), "Recovered: previous application remains current.");
            return;
        }
        if (!string.Equals(hash, request.Offer.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The current file differs from both recorded versions; recovery did not change any file.");
        string archived = ArchivePrevious(files, request); // Called only after this new GUI is shown/painted.
        File.WriteAllText(Path.Combine(files.Operation, "result.txt"), "Recovered new GUI startup; previous copy: " + archived);
        files.ClearActive(request.OperationId);
    }

    internal static void ValidateRequest(UpdateRequest request, UpdateFiles files, string id)
    {
        if (request == null || request.Protocol != 1 || request.OperationId != id || request.CreatedTicks > DateTime.UtcNow.AddMinutes(1).Ticks ||
            request.CreatedTicks < DateTime.UtcNow.AddMinutes(-5).Ticks || request.ParentId <= 0 || string.IsNullOrEmpty(request.ParentPath) ||
            !string.Equals(Path.GetDirectoryName(UpdateFiles.LocalPath(request.ParentPath)), files.Folder, StringComparison.OrdinalIgnoreCase) ||
            !request.ParentPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(request.ParentPath).StartsWith("OLD", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Invalid or expired app-update handoff.");
        string currentVersion = FileVersionInfo.GetVersionInfo(ExecutablePath).FileVersion;
        if (request.ParentVersion != null && request.ParentVersion != currentVersion) throw new IOException("The predecessor version changed.");
        ValidateOffer(request.Offer, Version.Parse(currentVersion));
    }

    internal static string Serialize<T>(T value)
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    internal static T Deserialize<T>(string value)
    {
        if (value == null || value.Length > 32768) throw new IOException("Invalid update handoff size.");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }
    internal static string ReadLine(TextReader reader, int maximum, int milliseconds)
    {
        var read = Task.Run(async () =>
        {
            var line = new StringBuilder();
            var c = new char[1];
            while (await reader.ReadAsync(c, 0, 1).ConfigureAwait(false) != 0)
            {
                if (c[0] == '\n') return line.ToString().TrimEnd('\r');
                if (line.Length >= maximum) throw new IOException("The update handshake exceeds its limit.");
                line.Append(c[0]);
            }
            throw new IOException("The update handshake closed early.");
        });
        if (!Task.WhenAny(read, Task.Delay(milliseconds)).GetAwaiter().GetResult().Equals(read)) throw new TimeoutException("The app update handshake timed out.");
        return read.GetAwaiter().GetResult();
    }
}
