namespace MonoHelper
{
	using System;
	using System.ComponentModel.Design;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using EnvDTE;
	using EnvDTE80;
	using Microsoft.VisualStudio.Shell;

	/// <summary>
	///     This is the class that implements the package exposed by this assembly.
	///     The minimum requirement for a class to be considered a valid package for Visual Studio
	///     is to implement the IVsPackage interface and register itself with the shell.
	///     This package uses the helper classes defined inside the Managed Package Framework (MPF)
	///     to do it: it derives from the Package class that provides the implementation of the
	///     IVsPackage interface and uses the registration attributes defined in the framework to
	///     register itself and its components with the shell.
	/// </summary>
	// This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
	// a package.
	[PackageRegistration(UseManagedResourcesOnly = true)]
	// This attribute is used to register the informations needed to show the this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	// This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideOptionPage(typeof(MonoHelperOptionsDialogPage), "MonoHelper", "General", 0, 0, true)]
	[Guid(GuidList.GuidMonoHelperPkgString)]
	public sealed class MonoHelperPackage : Package
	{
		/// <summary>
		///     Default constructor of the package.
		///     Inside this method you can place any initialization code that does not require
		///     any Visual Studio service because at this point the package object is created but
		///     not sited yet inside Visual Studio environment. The place to do all the other
		///     initialization is the Initialize method.
		/// </summary>
		public MonoHelperPackage()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", ToString()));
		}

		/// <summary>
		///     Initialization of the package; this method is called right after the package is sited, so this is the place
		///     where you can put all the initilaization code that rely on services provided by VisualStudio.
		/// </summary>
		protected override void Initialize()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", ToString()));
			base.Initialize();

			// Add our command handlers for menu (commands must exist in the .vsct file)
			OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

			if (null != mcs)
			{
				// Create the command for the menu item.
				CommandID xBuildMenuCommandID = new CommandID(GuidList.GuidMonoHelperCmdSet, (int)PkgCmdIDList.XBuildCommandID);
				MenuCommand xBuildMenuItem = new MenuCommand(XBuildMenuItemCallback, xBuildMenuCommandID);
				mcs.AddCommand(xBuildMenuItem);

				CommandID xRebuildMenuCommandID = new CommandID(GuidList.GuidMonoHelperCmdSet, (int)PkgCmdIDList.XRebuildCommandID);
				MenuCommand xRebuildMenuItem = new MenuCommand(XRebuildMenuItemCallback, xRebuildMenuCommandID);
				mcs.AddCommand(xRebuildMenuItem);

				CommandID startNetMenuCommandID = new CommandID(GuidList.GuidMonoHelperCmdSet, (int)PkgCmdIDList.StartNetCommandID);
				MenuCommand startNetMenuItem = new MenuCommand(StartNetMenuItemCallback, startNetMenuCommandID);
				mcs.AddCommand(startNetMenuItem);

				CommandID startMonoMenuCommandID = new CommandID(GuidList.GuidMonoHelperCmdSet, (int)PkgCmdIDList.StartMonoCommandID);
				MenuCommand startMonoMenuItem = new MenuCommand(StartMonoMenuItemCallback, startMonoMenuCommandID);
				mcs.AddCommand(startMonoMenuItem);

				CommandID debugNetMenuCommandID = new CommandID(GuidList.GuidMonoHelperCmdSet, (int)PkgCmdIDList.DebugNetCommandID);
				MenuCommand debugMenuNetItem = new MenuCommand(DebugNetMenuItemCallback, debugNetMenuCommandID);
				mcs.AddCommand(debugMenuNetItem);
			}
		}

		private void DebugNetMenuItemCallback(object sender, EventArgs e)
		{
			MonoHelper.DebugNet(GetDTE());
		}

		private DTE2 GetDTE()
		{
			return GetService(typeof(DTE)) as DTE2;
		}

		private void StartMonoMenuItemCallback(object sender, EventArgs e)
		{
			MonoHelper.StartMono(GetDTE());
		}

		private void StartNetMenuItemCallback(object sender, EventArgs e)
		{
			MonoHelper.StartNet(GetDTE());
		}

		private void XBuildMenuItemCallback(object sender, EventArgs e)
		{
			MonoHelper.XBuild(GetDTE());
		}

		private void XRebuildMenuItemCallback(object sender, EventArgs e)
		{
			MonoHelper.XBuild(GetDTE(), true);
		}
	}
}