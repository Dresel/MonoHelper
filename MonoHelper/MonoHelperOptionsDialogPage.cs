namespace MonoHelper
{
	using System;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using Microsoft.VisualStudio.Shell;

	[ClassInterface(ClassInterfaceType.AutoDual)]
	[CLSCompliant(false), ComVisible(true)]
	public class MonoHelperOptionsDialogPage : DialogPage
	{
		[Category("Mono Runtime Settings")]
		[DisplayName("Installation Path")]
		[Description("Installation Path")]
		public string MonoInstallationPath { get; set; }
	}
}