using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OfficeSmart.Updates;

internal static class CleanupChecks
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string name, string existing, IntPtr security);

    internal static void Run()
    {
        foreach (string mode in new[] { "success", "historical", "partial", "rollback", "pending", "unknown-file",
            "unknown-directory", "changed-payload", "missing-archive", "wrong-result", "hardlink", "abandoned", "preparing", "empty" })
            Case(mode);
        Console.WriteLine("PASS: Completed/historical/partial/rollback/abandoned cleanup removes staging; pending work, preparation leases, unknown files, changed bytes and hard links are preserved.");
    }

    private static void Case(string mode)
    {
        string name = "PcNinja-OfficeFixture-" + Guid.NewGuid().ToString("N");
        string folder = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(folder);
        try
        {
            string target = Path.Combine(folder, PortableUpdate.CanonicalName);
            string archive = Path.Combine(folder, "OLD-" + PortableUpdate.CanonicalName);
            byte[] old = Encoding.UTF8.GetBytes("OLD"), next = Encoding.UTF8.GetBytes("NEW");
            File.WriteAllBytes(target, mode == "rollback" ? old : mode == "historical" ? Encoding.UTF8.GetBytes("LATER") : next);
            string id = Guid.NewGuid().ToString("N");
            string operation;
            using (var files = new UpdateFiles(folder, id, true, fixture: true))
            {
                operation = files.Operation;
                if (mode != "empty")
                {
                    File.WriteAllBytes(files.Candidate, next);
                    File.WriteAllBytes(files.Helper, old);
                }
                if (mode == "preparing")
                {
                    using (var preparation = files.CreatePrivate(Path.Combine(operation, "preparing.lock"), deleteOnClose: true))
                    {
                        Check(!UpdateCleanup.CleanOnce(target, new Version(3, 3, 13, 0), true), "Deleted a live preparation.");
                        Check(File.Exists(files.Candidate), "Live candidate was deleted.");
                    }
                    Check(!File.Exists(Path.Combine(operation, "preparing.lock")), "Preparation lease was not removed on close.");
                }
                if (mode != "abandoned" && mode != "preparing" && mode != "empty")
                {
                    File.WriteAllBytes(files.Backup, old);
                    File.WriteAllBytes(files.Displaced, old);
                    var request = new UpdateRequest { OperationId = id, ParentPath = target, ParentHash = Hash(old),
                        Offer = new UpdateOffer { Version = "3.3.13.0", Sha256 = Hash(next) } };
                    files.WriteJournal(request);
                    if (mode != "missing-archive" && mode != "rollback") File.WriteAllBytes(archive, old);
                    if (mode != "partial") File.WriteAllText(Path.Combine(operation, "result.txt"), mode == "wrong-result" ? "Update incomplete" :
                        mode == "rollback" ? "Recovered: previous application remains current." : "Updated to 3.3.13.0; previous copy: " + archive);
                    else { File.Delete(files.Helper); File.Delete(files.Displaced); }
                    if (mode == "pending") files.MarkActive(id);
                    if (mode == "unknown-file") File.WriteAllText(Path.Combine(operation, "keep.txt"), "KEEP");
                    if (mode == "unknown-directory") Directory.CreateDirectory(Path.Combine(operation, "keep"));
                    if (mode == "changed-payload") File.WriteAllText(files.Displaced, "UNEXPECTED BYTES");
                    if (mode == "hardlink")
                    {
                        File.Delete(files.Helper);
                        string external = Path.Combine(folder, "external.exe");
                        File.WriteAllBytes(external, old);
                        Check(CreateHardLink(files.Helper, external, IntPtr.Zero), "Hard-link fixture failed.");
                    }
                }
            }
            string currentHash = Hash(File.ReadAllBytes(target));
            bool cleaned = UpdateCleanup.CleanOnce(target, new Version(3, 3, mode == "historical" ? 14 : 13, 0), true);
            bool expected = mode == "success" || mode == "historical" || mode == "partial" || mode == "rollback" ||
                mode == "abandoned" || mode == "preparing" || mode == "empty";
            Check(cleaned == expected, "Unexpected cleanup outcome: " + mode);
            Check(currentHash == Hash(File.ReadAllBytes(target)), "Current executable changed: " + mode);
            if (expected)
            {
                Check(!Directory.Exists(Path.Combine(folder, UpdateFiles.StateFolder)), "Staging remained: " + mode);
                if (mode == "success") Check(Directory.GetFileSystemEntries(folder).Length == 2, "Expected exactly current and OLD executables.");
            }
            else
            {
                Check(File.Exists(Path.Combine(operation, "transaction.json")), "Journal removed in refused cleanup: " + mode);
                Check(File.Exists(Path.Combine(operation, "previous.exe")), "Backup removed in refused cleanup: " + mode);
            }
            if (File.Exists(archive)) Check(Hash(File.ReadAllBytes(archive)) == Hash(old), "OLD archive changed.");
        }
        finally
        {
            string full = Path.GetFullPath(folder);
            if (full != Path.Combine(Path.GetTempPath(), name) || Path.GetDirectoryName(full) != Path.GetTempPath().TrimEnd('\\'))
                throw new Exception("Unexpected fixture cleanup target.");
            Directory.Delete(full, true);
        }
    }

    private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
