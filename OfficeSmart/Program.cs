using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using OfficeSmart.Updates;
using PcNinja.Shared;

namespace OfficeSmart;

internal static class Program
{
	/// <summary>Fixed shared single-instance id. Never version it.</summary>
	internal const string SingleInstanceAppId = "SmartOfficeInstaller";

	[STAThread]
	private static int Main(string[] args)
	{
		if (args.Length != 0)
			return args.Length == 1 && args[0] == PortableUpdate.HelperArgument ? PortableUpdate.RunHelper() : 2;
		string readyPipe = Environment.GetEnvironmentVariable(UpdateReadiness.EnvironmentName);
		Environment.SetEnvironmentVariable(UpdateReadiness.EnvironmentName, null);
		// GUI is single-instance per Windows session. A second launch focuses the
		// running window instead (before UAC, so no needless elevation prompt).
		if (!IsAdmin() && PcNinjaSingleInstance.TryActivateExisting(SingleInstanceAppId)) return 0;
		if (!IsAdmin())
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = Application.ExecutablePath,
					Verb = "runas",
					UseShellExecute = true
				});
				return 0;
			}
			catch
			{
				return 5;
			}
		}
		if (PortableUpdate.IsRecoveryCopy) return PortableUpdate.RecoverFromHelper() ? 0 : 1;
		using var singleInstance = PcNinjaSingleInstance.Acquire(SingleInstanceAppId);
		if (singleInstance == null) return 0;
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		using var form = new InstallerForm();
        form.Shown += delegate { form.BeginInvoke(new Action(async () =>
        {
            form.Update();
            await UpdateReadiness.SignalAsync(readyPipe);
            if (form.Visible) PortableUpdate.RecoverAfterStartup();
        })); };
		Application.Run(form);
		return 0;
	}

	private static bool IsAdmin()
	{
		try
		{
			return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
		}
		catch
		{
			return false;
		}
	}
}
