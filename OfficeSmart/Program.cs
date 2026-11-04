using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using OfficeSmart.Updates;

namespace OfficeSmart;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		if (args.Length != 0)
			return args.Length == 1 && args[0] == PortableUpdate.HelperArgument ? PortableUpdate.RunHelper() : 2;
		string readyPipe = Environment.GetEnvironmentVariable(UpdateReadiness.EnvironmentName);
		Environment.SetEnvironmentVariable(UpdateReadiness.EnvironmentName, null);
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
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		using var form = new InstallerForm();
		if (UpdateReadiness.Valid(readyPipe))
			form.Shown += delegate { form.BeginInvoke(new Action(async () => { form.Update(); await UpdateReadiness.SignalAsync(readyPipe); })); };
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
