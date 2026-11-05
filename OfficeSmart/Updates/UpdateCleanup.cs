using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace OfficeSmart.Updates;

// Cleanup runs from the published EXE, not the staged helper whose image is in
// use. It can finish even if the user immediately closes the new GUI.
internal static class UpdateCleanup
{
    internal const string Argument = "--internal-office-update-cleanup";

    internal static void Start()
    {
        if (!PortableUpdate.IsAdmin || Path.GetFileName(PortableUpdate.ExecutablePath) != PortableUpdate.CanonicalName ||
            !Directory.Exists(Path.Combine(Path.GetDirectoryName(PortableUpdate.ExecutablePath), UpdateFiles.StateFolder))) return;
        try
        {
            using var state = new UpdateFiles(Path.GetDirectoryName(PortableUpdate.ExecutablePath), Guid.Empty.ToString("N"), false, stateOnly: true);
            using var executable = new UpdateFiles.LockedFile(PortableUpdate.ExecutablePath, false);
            string version = typeof(UpdateCleanup).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            PortableUpdate.VerifyExecutable(executable, version, executable.Hash());
            using var child = Process.Start(new ProcessStartInfo
            {
                FileName = PortableUpdate.ExecutablePath, Arguments = Argument,
                WorkingDirectory = Path.GetDirectoryName(PortableUpdate.ExecutablePath),
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex) { Trace.WriteLine("Update cleanup deferred: " + ex.Message); }
    }

    internal static int Run()
    {
        if (!PortableUpdate.IsAdmin || Path.GetFileName(PortableUpdate.ExecutablePath) != PortableUpdate.CanonicalName) return 5;
        string current = PortableUpdate.ExecutablePath;
        var version = Version.Parse(FileVersionInfo.GetVersionInfo(current).FileVersion);
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            try { if (CleanOnce(current, version)) return 0; }
            catch (Exception ex) { Trace.WriteLine("Update cleanup deferred: " + ex.Message); }
            Thread.Sleep(250);
        } while (DateTime.UtcNow < deadline);
        return 1; // Pending/unknown files remain available for recovery; retry next startup.
    }

    internal static bool CleanOnce(string currentPath, Version version, bool fixture = false)
    {
        string folder = UpdateFiles.LocalPath(Path.GetDirectoryName(currentPath));
        string statePath = Path.Combine(folder, UpdateFiles.StateFolder);
        if (!Directory.Exists(statePath)) return true;
        if (Path.GetFileName(currentPath) != PortableUpdate.CanonicalName) throw new IOException("Cleanup requires the canonical executable.");
        using var current = new UpdateFiles.LockedFile(currentPath, false);
        string currentHash = current.Hash();
        if (!fixture) PortableUpdate.VerifyExecutable(current, version.ToString(), currentHash);
        using (var state = new UpdateFiles(folder, Guid.Empty.ToString("N"), false, fixture, stateOnly: true))
        {
            if (File.Exists(state.Active)) return false;
            // Exact GUID children only. No recursive deletion, glob cleanup or
            // following reparse points. Unknown names and files are preserved.
            foreach (string operation in Directory.GetDirectories(statePath))
            {
                string id = Path.GetFileName(operation);
                if (!Guid.TryParseExact(id, "N", out _)) continue;
                try { CleanOperation(folder, id, version, currentHash, fixture); }
                catch (Exception ex) { Trace.WriteLine("Update work files retained: " + ex.Message); }
            }
        }
        try { Directory.Delete(statePath, false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return !Directory.Exists(statePath);
    }

    private static void CleanOperation(string folder, string id, Version currentVersion, string currentHash, bool fixture)
    {
        string operation;
        var handles = new List<UpdateFiles.LockedFile>();
        try
        {
            using var files = new UpdateFiles(folder, id, false, fixture);
            operation = files.Operation;
            if (File.Exists(files.Active) || Directory.GetDirectories(operation).Length != 0) return;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(operation)) names.Add(Path.GetFileName(path));
            if (names.Count == 0) { /* Empty interrupted creation/deletion: remove after releasing anchors. */ }
            else if (!names.Contains("transaction.json"))
            {
                // A cancelled download never reached journal/GO and has no
                // recovery backup. Only its two reserved download/helper names
                // may be discarded while a verified current EXE is retained.
                foreach (string name in names)
                    if (name != "candidate.exe" && name != "helper.exe") return;
                foreach (string name in new[] { "helper.exe", "candidate.exe" })
                {
                    if (!names.Contains(name)) continue;
                    var file = Pin(handles, Path.Combine(operation, name));
                    if (file.Length > PortableUpdate.MaximumBytes) return;
                    if (name == "helper.exe" && !fixture)
                    {
                        string helperVersion = FileVersionInfo.GetVersionInfo(file.PathName).FileVersion;
                        if (!Version.TryParse(helperVersion, out var helper) || helper > currentVersion) return;
                        PortableUpdate.VerifyExecutable(file, helperVersion, file.Hash());
                    }
                }
                DeleteAll(handles);
            }
            else
            {
                var journal = Pin(handles, files.Journal);
                var request = PortableUpdate.Deserialize<UpdateRequest>(journal.ReadText(32768));
                if (request == null || request.Protocol != 1 || request.OperationId != id || request.Offer == null ||
                    !HashValid(request.ParentHash) || !HashValid(request.Offer.Sha256) ||
                    !Version.TryParse(request.Offer.Version, out var offered) ||
                    !string.Equals(Path.GetDirectoryName(UpdateFiles.LocalPath(request.ParentPath)), folder, StringComparison.OrdinalIgnoreCase) ||
                    !request.ParentPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(request.ParentPath).StartsWith("OLD", StringComparison.OrdinalIgnoreCase) ||
                    PortableUpdate.HelperAlive(files, request)) return;
                string originalName = Path.GetFileName(request.ParentPath);
                bool previousIsCurrent = string.Equals(currentHash, request.ParentHash, StringComparison.OrdinalIgnoreCase);
                string archive = null;
                UpdateFiles.LockedFile archiveGuard = null;
                try
                {
                    if (!previousIsCurrent)
                    {
                        if (currentVersion < offered || (currentVersion == offered &&
                            !string.Equals(currentHash, request.Offer.Sha256, StringComparison.OrdinalIgnoreCase))) return;
                        string path = UpdateArchives.Find(files, request);
                        using (var source = new UpdateFiles.LockedFile(path, true)) archive = UpdateArchives.Archive(files, request, source);
                        archiveGuard = new UpdateFiles.LockedFile(archive, false);
                        archiveGuard.RequireHash(request.ParentHash);
                        files.PublishPermissions(archive);
                    }
                    var payloads = new List<UpdateFiles.LockedFile>();
                    UpdateFiles.LockedFile result = null;
                    foreach (string name in names)
                    {
                        if (name == "transaction.json") continue;
                        if (name == "result.txt")
                        {
                            result = Pin(handles, Path.Combine(operation, name));
                            string text = result.ReadText(4096);
                            if (!previousIsCurrent && !MatchesResult(text, archive, request.Offer.Version))
                            {
                                bool legacy = false;
                                foreach (string path in UpdateArchives.LegacyPaths(folder, originalName))
                                    if (MatchesResult(text, path, request.Offer.Version)) { legacy = true; break; }
                                if (!legacy) return;
                            }
                            continue;
                        }
                        string expected = name == "helper.exe" || name == "previous.exe" || name == "displaced.exe" ? request.ParentHash :
                            name == "candidate.exe" || name == "failed.exe" ? request.Offer.Sha256 : null;
                        if (expected == null) return;
                        var payload = Pin(handles, Path.Combine(operation, name));
                        payload.RequireHash(expected);
                        payloads.Add(payload);
                    }
                    // Check every file before deleting any. Keep the journal
                    // last so interrupted cleanup can be verified and resumed.
                    foreach (var payload in payloads) { payload.Delete(); payload.Dispose(); }
                    if (result != null) { result.Delete(); result.Dispose(); }
                    journal.Delete(); journal.Dispose();
                }
                finally { archiveGuard?.Dispose(); }
            }
            foreach (var handle in handles) handle.Dispose();
        }
        finally { foreach (var handle in handles) handle.Dispose(); }
        // Only remove empty directories after releasing our no-delete anchors.
        Directory.Delete(operation, false);
    }

    private static UpdateFiles.LockedFile Pin(List<UpdateFiles.LockedFile> handles, string path)
    {
        var file = new UpdateFiles.LockedFile(path, true);
        handles.Add(file);
        return file;
    }
    private static bool MatchesResult(string text, string archive, string version) =>
        string.Equals(text, "Updated to " + version + "; previous copy: " + archive, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(text, "Recovered new GUI startup; previous copy: " + archive, StringComparison.OrdinalIgnoreCase);
    private static void DeleteAll(List<UpdateFiles.LockedFile> handles)
    {
        foreach (var file in handles) { file.Delete(); file.Dispose(); }
    }
    private static bool HashValid(string hash)
    {
        if (hash == null || hash.Length != 64) return false;
        foreach (char c in hash) if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
