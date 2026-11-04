using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OfficeSmart.Updates;

internal static class UpdateReadiness
{
    internal const string EnvironmentName = "PCNINJA_OFFICE_UPDATE_READY";
    private const string Prefix = "PcNinja.OfficeReady.";
    private const string Ready = "OFFICE_GUI_READY_1";
    internal static bool Valid(string name) => name != null && name.StartsWith(Prefix, StringComparison.Ordinal) &&
        name.Length == Prefix.Length + 32 && Guid.TryParseExact(name.Substring(Prefix.Length), "N", out _);

    internal static bool Launch(string target, out Process launched)
    {
        string name = Prefix + Guid.NewGuid().ToString("N");
        var acl = new PipeSecurity();
        acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 128, 128, acl);
        var start = new ProcessStartInfo { FileName = target, WorkingDirectory = Path.GetDirectoryName(target), UseShellExecute = false };
        start.EnvironmentVariables[EnvironmentName] = name;
        launched = Process.Start(start) ?? throw new IOException("The new application could not be opened.");
        _ = launched.Handle;
        try
        {
            var connected = server.WaitForConnectionAsync();
            if (!connected.Wait(30000)) throw new IOException("No GUI readiness connection; " +
                (launched.HasExited ? "child exit code " + launched.ExitCode : "child is still running"));
            if (!GetNamedPipeClientProcessId(server.SafePipeHandle, out uint pid) || pid != launched.Id)
                throw new IOException("The GUI readiness client did not match the launched process.");
            using var reader = new StreamReader(server, Encoding.UTF8, false, 128, true);
            if (PortableUpdate.ReadLine(reader, 128, 5000) != Ready) return false;
            using var writer = new StreamWriter(server, new UTF8Encoding(false), 128, true) { AutoFlush = true };
            writer.WriteLine("ACK");
            return !launched.HasExited;
        }
        catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is AggregateException)
        { throw new IOException("Application GUI readiness failed: " + ex.Message, ex); }
    }

    internal static async Task SignalAsync(string name)
    {
        if (!Valid(name)) return;
        try
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(3000);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), 128, true) { AutoFlush = true };
            await writer.WriteLineAsync(Ready);
            using var reader = new StreamReader(client, Encoding.UTF8, false, 128, true);
            await Task.Run(() => PortableUpdate.ReadLine(reader, 128, 5000));
        }
        catch (Exception ex) when (ex is IOException || ex is TimeoutException) { }
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
}
