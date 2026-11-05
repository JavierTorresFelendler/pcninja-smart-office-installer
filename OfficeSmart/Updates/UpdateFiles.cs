using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OfficeSmart.Updates;

// Native handle locks bind verification and no-overwrite moves to the same file.
internal sealed class UpdateFiles : IDisposable
{
    internal const string StateFolder = ".pcninja-office-updates";
    private readonly List<SafeFileHandle> directories = new List<SafeFileHandle>();
    internal string Folder { get; }
    internal string Operation { get; }
    internal string Candidate => Path.Combine(Operation, "candidate.exe");
    internal string Helper => Path.Combine(Operation, "helper.exe");
    internal string Backup => Path.Combine(Operation, "previous.exe");
    internal string Displaced => Path.Combine(Operation, "displaced.exe");
    internal string Journal => Path.Combine(Operation, "transaction.json");
    internal string Active => Path.Combine(Folder, StateFolder, "active-operation");
    private const string PrivateAcl = "O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)";
    private readonly SecurityIdentifier fixtureUser;

    internal UpdateFiles(string folder, string id, bool create, bool fixture = false, bool stateOnly = false)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new IOException("Invalid update operation.");
        Folder = LocalPath(folder);
        if (fixture)
        {
            string name = Path.GetFileName(Folder);
            if (!string.Equals(Path.GetDirectoryName(Folder), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith("PcNinja-OfficeFixture-", StringComparison.Ordinal) || !Guid.TryParseExact(name.Substring(22), "N", out _))
                throw new IOException("Fixtures require an exact random child of the current temporary directory.");
            fixtureUser = WindowsIdentity.GetCurrent().User;
        }
        string root = Path.GetPathRoot(Folder);
        var drive = new DriveInfo(root);
        if (drive.DriveType != DriveType.Fixed || drive.DriveFormat != "NTFS")
            throw new IOException("Move the app to a writable local NTFS disk before updating.");
        try
        {
            Anchor(root);
            string current = root;
            foreach (string part in Folder.Substring(root.Length).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                Anchor(current);
            }
            string state = Path.Combine(Folder, StateFolder);
            OpenPrivateDirectory(state, create, false);
            Operation = stateOnly ? state : Path.Combine(state, id);
            if (!stateOnly) OpenPrivateDirectory(Operation, create, create);
        }
        catch { Dispose(); throw; }
    }

    internal static string LocalPath(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd('\\');
        if (full.StartsWith("\\", StringComparison.Ordinal) || full.Length < 3 || full[1] != ':' ||
            full.IndexOf(':', 2) >= 0 || full.IndexOfAny(new[] { '*', '?', '"' }) >= 0)
            throw new IOException("Only ordinary local paths are supported for app updates.");
        foreach (string part in full.Substring(3).Split('\\'))
            if (part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal))
                throw new IOException("Ambiguous update path.");
        return full;
    }

    private void Anchor(string path)
    {
        var handle = CreateFileW(path, 0x80, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw Error("Cannot lock the update directory."); }
        if (!GetFileInformationByHandle(handle, out var info) || (info.Attributes & 0x410) != 0x10)
        { handle.Dispose(); throw new IOException("Reparse points are not supported for app updates."); }
        directories.Add(handle); // No delete sharing: ancestor names cannot be swapped.
    }

    private void OpenPrivateDirectory(string path, bool create, bool requireNew)
    {
        if (create)
        {
            string sddl = fixtureUser == null ? PrivateAcl : "O:" + fixtureUser.Value + "G:" + fixtureUser.Value + "D:P(A;OICI;FA;;;" + fixtureUser.Value + ")";
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out var descriptor, out _))
                throw Error("Cannot prepare update permissions.");
            try
            {
                var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = descriptor };
                if (!CreateDirectoryW(path, ref attributes))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 183 || requireNew) throw new Win32Exception(error, "Cannot create private update staging.");
                }
            }
            finally { LocalFree(descriptor); }
        }
        Anchor(path);
        var acl = Directory.GetAccessControl(path, AccessControlSections.Access | AccessControlSections.Owner);
        if (!acl.AreAccessRulesProtected || !AllowedSid((SecurityIdentifier)acl.GetOwner(typeof(SecurityIdentifier))))
            throw new IOException("Update staging has unsafe ownership or inherited permissions.");
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !AllowedSid((SecurityIdentifier)rule.IdentityReference))
                throw new IOException("Update staging is accessible to another account.");
    }

    private static bool TrustedSid(SecurityIdentifier sid) => sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
        sid.IsWellKnown(WellKnownSidType.LocalSystemSid);
    private bool AllowedSid(SecurityIdentifier sid) => TrustedSid(sid) || (fixtureUser != null && sid == fixtureUser);

    internal FileStream CreatePrivate(string path, bool deleteOnClose = false)
    {
        string sddl = fixtureUser == null ? PrivateAcl : "O:" + fixtureUser.Value + "G:" + fixtureUser.Value + "D:P(A;OICI;FA;;;" + fixtureUser.Value + ")";
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out var descriptor, out _)) throw Error("Cannot prepare file permissions.");
        try
        {
            var security = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = descriptor };
            var handle = CreateFileSecure(path, 0x40000000 | (deleteOnClose ? 0x10000u : 0), 0, ref security, 1,
                0x00200000 | (deleteOnClose ? 0x04000000u : 0), IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); throw Error("Cannot create a private update record without overwriting a file."); }
            return new FileStream(handle, FileAccess.Write);
        }
        finally { LocalFree(descriptor); }
    }
    internal void WriteJournal(UpdateRequest request)
    {
        using var output = CreatePrivate(Journal);
        byte[] bytes = Encoding.UTF8.GetBytes(PortableUpdate.Serialize(request));
        output.Write(bytes, 0, bytes.Length);
        output.Flush(true);
    }
    internal void MarkActive(string id)
    {
        string pending = Path.Combine(Operation, "active.new");
        using (var output = CreatePrivate(pending))
        {
            byte[] bytes = Encoding.ASCII.GetBytes(id);
            output.Write(bytes, 0, bytes.Length); output.Flush(true);
        }
        File.Move(pending, Active); // Atomic pointer publication; never an incomplete marker.
    }
    internal void ClearActive(string id)
    {
        if (!File.Exists(Active)) return;
        using (var marker = new LockedFile(Active, false))
            if (marker.ReadText(32) != id) throw new IOException("Another update owns the recovery marker.");
        File.Delete(Active); // Protected state directory; only this exact operation is cleared.
    }

    internal static void MakePublicExecutable(string path)
    {
        var security = new FileSecurity();
        security.SetSecurityDescriptorSddlForm("O:BAG:BAD:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;0x1200a9;;;BU)");
        File.SetAccessControl(path, security);
    }
    internal void PublishPermissions(string path)
    {
        if (fixtureUser == null) MakePublicExecutable(path);
    }

    internal static void MakePrivate(string path)
    {
        var security = new FileSecurity();
        security.SetSecurityDescriptorSddlForm("O:BAG:BAD:P(A;;FA;;;SY)(A;;FA;;;BA)");
        File.SetAccessControl(path, security);
    }

    internal sealed class LockedFile : IDisposable
    {
        private readonly FileStream stream;
        internal string PathName { get; private set; }
        internal LockedFile(string path, bool movable)
        {
            PathName = LocalPath(path);
            var file = CreateFileW(PathName, 0x80000000 | (movable ? 0x10000u : 0), 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
            if (file.IsInvalid) { file.Dispose(); throw Error("Cannot lock the update executable."); }
            if (!GetFileInformationByHandle(file, out var info) || (info.Attributes & 0x410) != 0 || info.Links != 1)
            { file.Dispose(); throw new IOException("Updates require an ordinary EXE with one file link."); }
            var finalName = new StringBuilder(32768);
            uint length = GetFinalPathNameByHandleW(file, finalName, (uint)finalName.Capacity, 0);
            if (length == 0 || length >= finalName.Capacity ||
                !string.Equals(finalName.ToString(), "\\\\?\\" + PathName, StringComparison.OrdinalIgnoreCase))
            { file.Dispose(); throw new IOException("The executable path resolved to another file."); }
            stream = new FileStream(file, FileAccess.Read);
        }
        internal string Hash()
        {
            stream.Position = 0;
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        internal long Length => stream.Length;
        internal string ReadText(int maximum)
        {
            if (stream.Length > maximum) throw new IOException("Update record exceeds its bound.");
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 128, true);
            return reader.ReadToEnd();
        }
        internal void CopyToPrivate(UpdateFiles files, string destination)
        {
            stream.Position = 0;
            using var output = files.CreatePrivate(destination);
            stream.CopyTo(output); output.Flush(true);
        }
        internal void RequireHash(string expected)
        {
            if (!string.Equals(Hash(), expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("The executable changed or failed its checksum.");
        }
        internal void Move(string destination)
        {
            destination = LocalPath(destination);
            byte[] name = Encoding.Unicode.GetBytes(destination + '\0');
            int lengthOffset = (IntPtr.Size == 8 ? 8 : 4) + IntPtr.Size;
            int nameOffset = lengthOffset + 4;
            IntPtr buffer = Marshal.AllocHGlobal(nameOffset + name.Length);
            try
            {
                Marshal.Copy(new byte[nameOffset], 0, buffer, nameOffset);
                Marshal.WriteInt32(buffer, lengthOffset, name.Length - 2);
                Marshal.Copy(name, 0, IntPtr.Add(buffer, nameOffset), name.Length);
                if (!SetFileInformationByHandle(stream.SafeFileHandle, 3, buffer, (uint)(nameOffset + name.Length)))
                    throw Error("Cannot move the update executable without overwriting another file.");
                PathName = destination;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        internal void Delete()
        {
            // Delete the exact ordinary file we verified, never a subsequently
            // substituted pathname. The caller must open it with movable=true.
            IntPtr disposition = Marshal.AllocHGlobal(1);
            try
            {
                Marshal.WriteByte(disposition, 1);
                if (!SetFileInformationByHandle(stream.SafeFileHandle, 4, disposition, 1))
                    throw Error("Cannot remove the verified update work file.");
            }
            finally { Marshal.FreeHGlobal(disposition); }
        }
        public void Dispose() => stream.Dispose();
    }

    public void Dispose()
    {
        for (int i = directories.Count - 1; i >= 0; i--) directories[i].Dispose();
        directories.Clear();
    }
    private static Win32Exception Error(string message) => new Win32Exception(Marshal.GetLastWin32Error(), message);
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation
    {
        public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] private static extern SafeFileHandle CreateFileSecure(string path, uint access, uint share, ref SecurityAttributes security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, IntPtr information, uint length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateDirectoryW(string path, ref SecurityAttributes security);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
