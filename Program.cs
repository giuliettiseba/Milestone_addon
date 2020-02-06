using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.SDK.UI.LoginDialog;

namespace VideoViewer
{
	///
	/// NOTE: This dll requires the application to be in x86 due to the ActiveX
	/// 
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			VideoOS.Platform.SDK.Environment.Initialize();		// Initialize the standalone Environment
			VideoOS.Platform.SDK.UI.Environment.Initialize();
            VideoOS.Platform.SDK.Environment.Properties.ConfigurationRefreshIntervalInMs = 5000;

            EnvironmentManager.Instance.TraceFunctionCalls = true;

			DialogLoginForm loginForm = new DialogLoginForm(SetLoginResult);
			//loginForm.AutoLogin = false;				// Can overrride the tick mark
			//loginForm.LoginLogoImage = someImage;		// Could add my own image here
			Application.Run(loginForm);
			if (Connected)
			{
				Application.Run(new MainForm());
			}

		}

		private static bool Connected = false;
		private static void SetLoginResult(bool connected)
		{
			Connected = connected;
		}
	}
}
