namespace MonoHelper
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using EnvDTE;
	using EnvDTE80;
	using Microsoft.Win32;
	using Mono.Cecil;
	using Mono.Cecil.Mdb;
	using Mono.Cecil.Pdb;

	public static class MonoHelper
	{
		public static void DebugNet(DTE2 dte)
		{
			OutputWindowPane outputWindowPane = PrepareOutputWindowPane(dte);
			outputWindowPane.OutputString("MonoHelper: Debug (.NET Runtime) Start\r\n\r\n");

			System.Diagnostics.Process process = StartNet(dte, outputWindowPane);

			outputWindowPane.OutputString(string.Format("MonoHelper: Attaching to process {0}\r\n", process.Id));

			// Try to attach to the debugger wait a maximum of one minute
			for (int i = 0; i < 600; i++)
			{
				if (!dte.Debugger.LocalProcesses.Cast<EnvDTE.Process>().Any(x => x.ProcessID == process.Id))
				{
					System.Threading.Thread.Sleep(100);
				}
				else
				{
					dte.Debugger.LocalProcesses.Cast<EnvDTE.Process>().Single(x => x.ProcessID == process.Id).Attach();
					break;
				}
			}

			outputWindowPane.OutputString("\r\nMonoHelper: Debug (.NET Runtime) End");
		}

		public static void StartMono(DTE2 dte)
		{
			OutputWindowPane outputWindowPane = PrepareOutputWindowPane(dte);
			outputWindowPane.OutputString("MonoHelper: Start (Mono Runtime) Start\r\n\r\n");

			StartMono(dte, outputWindowPane);

			outputWindowPane.OutputString("\r\nMonoHelper: Start (Mono Runtime) End");
		}

		public static void StartNet(DTE2 dte)
		{
			OutputWindowPane outputWindowPane = PrepareOutputWindowPane(dte);
			outputWindowPane.OutputString("MonoHelper: Start (.NET Runtime) Start\r\n\r\n");

			StartNet(dte, outputWindowPane);

			outputWindowPane.OutputString("\r\nMonoHelper: Start (.NET Runtime) End");
		}

		public static void XBuild(DTE2 dte, bool rebuild = false)
		{
			OutputWindowPane outputWindowPane = PrepareOutputWindowPane(dte);

			if (!rebuild)
			{
				outputWindowPane.OutputString("MonoHelper: XBuild Solution Start\r\n\r\n");
			}
			else
			{
				outputWindowPane.OutputString("MonoHelper: XRebuild Solution Start\r\n\r\n");
			}

			outputWindowPane.OutputString(string.Format("MonoHelper: Saving Documents\r\n"));
			dte.ExecuteCommand("File.SaveAll");

			string monoPath = DetermineMonoPath(dte);

			// Get current configuration
			string configurationName = dte.Solution.SolutionBuild.ActiveConfiguration.Name;
			string platformName = ((SolutionConfiguration2)dte.Solution.SolutionBuild.ActiveConfiguration).PlatformName;
			string fileName = string.Format(@"{0}\bin\xbuild.bat", monoPath);
			string arguments = string.Format(@"""{0}"" /p:Configuration=""{1}"" /p:Platform=""{2}"" {3}", dte.Solution.FileName,
				configurationName, platformName, rebuild ? " /t:Rebuild" : string.Empty);

			// Run XBuild and show in output
			System.Diagnostics.Process proc = new System.Diagnostics.Process
			{
				StartInfo =
					new ProcessStartInfo
					{
						FileName = fileName,
						Arguments = arguments,
						UseShellExecute = false,
						RedirectStandardOutput = true,
						CreateNoWindow = true
					}
			};

			outputWindowPane.OutputString(string.Format("MonoHelper: Running {0} {1}\r\n\r\n", fileName, arguments));

			proc.Start();

			while (!proc.StandardOutput.EndOfStream)
			{
				string line = proc.StandardOutput.ReadLine();

				outputWindowPane.OutputString(line);
				outputWindowPane.OutputString("\r\n");
			}

			// XBuild returned with error, stop processing XBuild Command
			if (proc.ExitCode != 0)
			{
				if (!rebuild)
				{
					outputWindowPane.OutputString("\r\n\r\nMonoHelper: XBuild Solution End");
				}
				else
				{
					outputWindowPane.OutputString("\r\n\r\nMonoHelper: XRebuild Solution End");
				}

				return;
			}

			foreach (Project project in dte.Solution.Projects)
			{
				if (project.ConfigurationManager == null || project.ConfigurationManager.ActiveConfiguration == null)
				{
					continue;
				}

				Property debugSymbolsProperty = GetProperty(project.ConfigurationManager.ActiveConfiguration.Properties,
					"DebugSymbols");

				// If DebugSymbols is true, generate pdb symbols for all assemblies in output folder
				if (debugSymbolsProperty != null && debugSymbolsProperty.Value is bool && (bool)debugSymbolsProperty.Value)
				{
					outputWindowPane.OutputString(
						string.Format("\r\nMonoHelper: Generating DebugSymbols and injecting DebuggableAttributes for project {0}\r\n",
							project.Name));

					// Determine Outputpath, see http://www.mztools.com/articles/2009/MZ2009015.aspx
					string absoluteOutputPath = GetAbsoluteOutputPath(project);

					GenerateDebugSymbols(absoluteOutputPath, outputWindowPane);
				}
			}

			if (!rebuild)
			{
				outputWindowPane.OutputString("\r\nMonoHelper: XBuild Solution End");
			}
			else
			{
				outputWindowPane.OutputString("\r\nMonoHelper: XRebuild Solution End");
			}
		}

		private static string DetermineMonoPath(DTE2 dte)
		{
			OutputWindowPane outputWindowPane = PrepareOutputWindowPane(dte);

			Properties monoHelperProperties = dte.Properties["MonoHelper", "General"];
			string monoPath = (string)monoHelperProperties.Item("MonoInstallationPath").Value;

			if (!string.IsNullOrEmpty(monoPath))
			{
				outputWindowPane.OutputString("\r\nMonoHelper: Mono Installation Path is set.");
			}
			else
			{
				outputWindowPane.OutputString("\r\nMonoHelper: Mono Installation Path is not set. Trying to get it from registry.");

				RegistryKey openSubKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Novell\\Mono");

				if (openSubKey == null)
				{
					openSubKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Novell\\Mono");
				}

				if (openSubKey == null)
				{
					throw new Exception(
						"Mono Runtime not found. Please install Mono and ensure that Mono Installation Path is set via Tools \\ Options \\ Mono Helper or that the necessary registry settings are existing.");
				}

				string value = openSubKey.GetSubKeyNames().OrderByDescending(x => x).First();
				monoPath = (string)openSubKey.OpenSubKey(value).GetValue("SdkInstallRoot");
			}

			return monoPath;
		}

		private static void GenerateDebugSymbols(string absoluteOutputPath, OutputWindowPane outputWindowPane)
		{
			FileInfo[] files = (new DirectoryInfo(absoluteOutputPath)).GetFiles();

			foreach (FileInfo file in files)
			{
				if (file.Name.EndsWith(".dll") || file.Name.EndsWith(".exe"))
				{
					if (files.Any(x => x.Name.EndsWith(".mdb") && x.Name.Substring(0, x.Name.Length - 4) == file.Name))
					{
						outputWindowPane.OutputString(string.Format("MonoHelper: Assembly {0}\r\n", file.Name));

						string assemblyPath = file.FullName;

						AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath,
							new ReaderParameters { SymbolReaderProvider = new MdbReaderProvider(), ReadSymbols = true });

						CustomAttribute debuggableAttribute =
							new CustomAttribute(
								assemblyDefinition.MainModule.Import(
									typeof(DebuggableAttribute).GetConstructor(new[] { typeof(DebuggableAttribute.DebuggingModes) })));

						debuggableAttribute.ConstructorArguments.Add(
							new CustomAttributeArgument(assemblyDefinition.MainModule.Import(typeof(DebuggableAttribute.DebuggingModes)),
								DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints |
									DebuggableAttribute.DebuggingModes.EnableEditAndContinue |
									DebuggableAttribute.DebuggingModes.DisableOptimizations));

						if (assemblyDefinition.CustomAttributes.Any(x => x.AttributeType.Name == typeof(DebuggableAttribute).Name))
						{
							// Replace existing attribute
							int indexOf =
								assemblyDefinition.CustomAttributes.IndexOf(
									assemblyDefinition.CustomAttributes.Single(x => x.AttributeType.Name == typeof(DebuggableAttribute).Name));
							assemblyDefinition.CustomAttributes[indexOf] = debuggableAttribute;
						}
						else
						{
							assemblyDefinition.CustomAttributes.Add(debuggableAttribute);
						}

						assemblyDefinition.Write(assemblyPath,
							new WriterParameters { SymbolWriterProvider = new PdbWriterProvider(), WriteSymbols = true });
					}
				}
			}
		}

		private static string GetAbsoluteOutputPath(Project project)
		{
			Property outputPathProperty = project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath");

			string outputPath = (string)outputPathProperty.Value;
			string absoluteOutputPath = string.Empty;

			if (outputPath.StartsWith(Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString()))
			{
				// This is case 1: "\\server\folder"
				absoluteOutputPath = outputPath;
			}
			else if (outputPath.Length >= 2 && outputPath[1] == Path.VolumeSeparatorChar)
			{
				// This is case 2: "drive:\folder"
				absoluteOutputPath = outputPath;
			}
			else if (outputPath.IndexOf("..\\") != -1)
			{
				// This is case 3: "..\..\folder"
				string projectFolder = Path.GetDirectoryName(project.FullName);

				while (outputPath.StartsWith("..\\"))
				{
					outputPath = outputPath.Substring(3);
					projectFolder = Path.GetDirectoryName(projectFolder);
				}

				absoluteOutputPath = Path.Combine(projectFolder, outputPath);
			}
			else
			{
				// This is case 4: "folder"
				string projectFolder = Path.GetDirectoryName(project.FullName);
				absoluteOutputPath = Path.Combine(projectFolder, outputPath);
			}

			return absoluteOutputPath;
		}

		private static string GetProgramFileName(Project project)
		{
			string fileName;
			int startAction = (int)GetProperty(project.ConfigurationManager.ActiveConfiguration.Properties, "StartAction").Value;

			switch (startAction)
			{
				case 0:
					Property outputFileNameProperty = GetProperty(project.Properties, "OutputFileName");
					fileName = Path.Combine(GetAbsoluteOutputPath(project), (string)outputFileNameProperty.Value);
					break;

				case 1:
					Property startProgram = GetProperty(project.ConfigurationManager.ActiveConfiguration.Properties, "StartProgram");
					fileName = (string)startProgram.Value;
					break;

				case 2:
					throw new InvalidOperationException("StartAction 2 (Start browser with URL) not supported");

				default:
					throw new InvalidOperationException("Unknown StartAction");
			}

			return fileName;
		}

		private static Property GetProperty(Properties properties, string propertyName)
		{
			if (properties != null)
			{
				foreach (Property item in properties)
				{
					if (item != null && item.Name == propertyName)
					{
						return item;
					}
				}
			}

			return null;
		}

		private static OutputWindowPane PrepareOutputWindowPane(DTE2 dte)
		{
			dte.ExecuteCommand("View.Output");

			OutputWindow outputWindow = dte.ToolWindows.OutputWindow;

			OutputWindowPane outputWindowPane = null;

			foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
			{
				if (pane.Name == "MonoHelper")
				{
					outputWindowPane = pane;
					break;
				}
			}

			if (outputWindowPane == null)
			{
				outputWindowPane = outputWindow.OutputWindowPanes.Add("MonoHelper");
			}

			outputWindowPane.Activate();

			outputWindowPane.Clear();
			outputWindowPane.OutputString("MonoHelper, Version 1.2\r\n");
			outputWindowPane.OutputString("Copyright (C) Christopher Dresel 2015\r\n");
			outputWindowPane.OutputString("\r\n");

			return outputWindowPane;
		}

		private static System.Diagnostics.Process StartMono(DTE2 dte, OutputWindowPane outputWindowPane)
		{
			Project startupProject = dte.Solution.Item(((object[])dte.Solution.SolutionBuild.StartupProjects)[0]);

			string fileName = GetProgramFileName(startupProject);
			string arguments = string.Empty;

			Property startArguments = GetProperty(startupProject.ConfigurationManager.ActiveConfiguration.Properties,
				"StartArguments");
			arguments = (string)startArguments.Value;

			string monoPath = DetermineMonoPath(dte);

			outputWindowPane.OutputString(string.Format("MonoHelper: Running {0}\\bin\\mono.exe \"{1}\" {2}\r\n", monoPath,
				fileName, arguments));

			System.Diagnostics.Process process = new System.Diagnostics.Process
			{
				StartInfo =
					new ProcessStartInfo
					{
						FileName = string.Format(@"{0}\bin\mono.exe", monoPath),
						Arguments = string.Format(@"""{0}"" {1}", fileName, arguments),
						UseShellExecute = true,
						WorkingDirectory = Path.GetDirectoryName(fileName)
					}
			};

			process.Start();

			return process;
		}

		private static System.Diagnostics.Process StartNet(DTE2 dte, OutputWindowPane outputWindowPane)
		{
			Project startupProject = dte.Solution.Item(((object[])dte.Solution.SolutionBuild.StartupProjects)[0]);
			Property startArguments = GetProperty(startupProject.ConfigurationManager.ActiveConfiguration.Properties,
				"StartArguments");

			string fileName = GetProgramFileName(startupProject);
			string arguments = (string)startArguments.Value;

			outputWindowPane.OutputString(string.Format("MonoHelper: Running {0} {1}\r\n", fileName, arguments));

			System.Diagnostics.Process process = new System.Diagnostics.Process
			{
				StartInfo =
					new ProcessStartInfo
					{
						FileName = string.Format(@"{0}", fileName),
						Arguments = string.Format(@"{0}", arguments),
						UseShellExecute = true,
						WorkingDirectory = Path.GetDirectoryName(fileName)
					}
			};

			process.Start();

			return process;
		}
	}
}