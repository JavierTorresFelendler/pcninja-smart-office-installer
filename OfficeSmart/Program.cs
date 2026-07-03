using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace OfficeSmart;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
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
				return;
			}
			catch
			{
				return;
			}
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.Run(new InstallerForm());
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
