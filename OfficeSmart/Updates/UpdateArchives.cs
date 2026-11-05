using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace OfficeSmart.Updates;

internal static class UpdateArchives
{
    internal static string Name(string originalName, string version, string hash = null)
    {
        if (!Version.TryParse(version, out var parsed) || parsed.Revision < 0 || parsed.ToString(4) != version)
            throw new IOException("The predecessor has no valid four-part file version.");
        return Path.GetFileNameWithoutExtension(originalName) + "-v" + version + (hash == null ? "" : "-" + hash.ToLowerInvariant()) + ".exe";
    }

    internal static string ParentVersion(UpdateFiles files, UpdateRequest request)
    {
        if (!string.IsNullOrEmpty(request.ParentVersion))
        {
            Name(Path.GetFileName(request.ParentPath), request.ParentVersion);
            return request.ParentVersion;
        }
        // v3.3.14 and earlier journals did not record the old file version.
        // Infer it only from exact predecessor bytes, never the current version.
        var paths = new List<string> { files.Backup, files.Helper, files.Displaced };
        paths.AddRange(LegacyPaths(files.Folder, Path.GetFileName(request.ParentPath)));
        string prefix = Path.GetFileNameWithoutExtension(request.ParentPath) + "-v";
        foreach (string path in Directory.GetFiles(files.Folder))
            if (Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;
            using var file = new UpdateFiles.LockedFile(path, false);
            if (!string.Equals(file.Hash(), request.ParentHash, StringComparison.OrdinalIgnoreCase)) continue;
            string version = FileVersionInfo.GetVersionInfo(path).FileVersion;
            if (!Version.TryParse(version, out var parsed) || parsed.Revision < 0) continue;
            PortableUpdate.VerifyExecutable(file, version, request.ParentHash);
            return parsed.ToString(4);
        }
        throw new IOException("The predecessor version cannot be verified; recovery copies were retained.");
    }

    internal static IEnumerable<string> Paths(UpdateFiles files, UpdateRequest request)
    {
        string original = Path.GetFileName(request.ParentPath), version = ParentVersion(files, request);
        yield return Path.Combine(files.Folder, Name(original, version));
        yield return Path.Combine(files.Folder, Name(original, version, request.ParentHash));
        foreach (string path in LegacyPaths(files.Folder, original)) yield return path;
    }

    internal static IEnumerable<string> LegacyPaths(string folder, string original)
    {
        for (int slot = 0; slot < 1000; slot++)
            yield return Path.Combine(folder, (slot == 0 ? "OLD-" : "OLD.V" + slot + "-") + original);
    }

    internal static string Find(UpdateFiles files, UpdateRequest request)
    {
        foreach (string path in Paths(files, request))
        {
            if (!File.Exists(path)) continue;
            using var file = new UpdateFiles.LockedFile(path, false);
            if (string.Equals(file.Hash(), request.ParentHash, StringComparison.OrdinalIgnoreCase)) return path;
        }
        throw new IOException("The verified predecessor archive is unavailable.");
    }

    internal static string Archive(UpdateFiles files, UpdateRequest request, UpdateFiles.LockedFile source)
    {
        source.RequireHash(request.ParentHash);
        string original = Path.GetFileName(request.ParentPath), version = ParentVersion(files, request);
        foreach (string name in new[] { Name(original, version), Name(original, version, request.ParentHash) })
        {
            string destination = Path.Combine(files.Folder, name);
            if (string.Equals(source.PathName, destination, StringComparison.OrdinalIgnoreCase)) return destination;
            try { source.Move(destination); return destination; }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 80 || ex.NativeErrorCode == 183)
            {
                using var existing = new UpdateFiles.LockedFile(destination, false);
                if (!string.Equals(existing.Hash(), request.ParentHash, StringComparison.OrdinalIgnoreCase)) continue;
                // The exact same predecessor is already archived. Remove only
                // the pinned duplicate, while keeping the verified archive open.
                source.Delete(); source.Dispose();
                return destination;
            }
        }
        throw new IOException("Both versioned predecessor names are occupied by different bytes; no file was overwritten.");
    }

    internal static void Publish(UpdateFiles files, UpdateRequest request, string path)
    {
        using var archived = new UpdateFiles.LockedFile(path, false);
        archived.RequireHash(request.ParentHash);
        files.PublishPermissions(path);
    }
}
