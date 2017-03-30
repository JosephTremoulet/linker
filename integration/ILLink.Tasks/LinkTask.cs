using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;

namespace Sample.LinkTask
{
	public class Link : ToolTask
	{

		public ITaskItem OutputDirectory { get; set; }
		public ITaskItem AssemblyDirectory { get; set; }
		public string CoreAssembliesAction { get; set; }
		public ITaskItem[] RootDescriptorFiles { get; set; }
		public ITaskItem LinkedAssembliesFile { get; set; }

		[Required]
		public ITaskItem LinkerDllPath { get; set; }

		[Output]
		public ITaskItem[] LinkedAssemblies { get; private set; }

		[Required]
		public ITaskItem[] InputAssemblies { get; set; }

		public string LinkerMode { get; set; }

		// required methods and properties
		protected override string GenerateFullPathToTool()
		{
			return "dotnet";
		}

		protected override string ToolName
		{
			get { return "illink"; }
		}

		// optional methods and properties
		protected override string GenerateCommandLineCommands()
		{
			StringBuilder sb = new StringBuilder();

			sb.Append(LinkerDllPath.ItemSpec + " ");

			foreach (var assemblyItem in InputAssemblies)
			{
				sb.Append($"-a {assemblyItem.ItemSpec} ");
			}

			if (RootDescriptorFiles != null)
			{
				foreach (var rootFile in RootDescriptorFiles)
				{
					sb.Append($"-x {rootFile.ItemSpec} ");
				}
			}

			if (AssemblyDirectory != null)
			{
				sb.Append($"-d {AssemblyDirectory.ItemSpec} " );
			}
			if (OutputDirectory != null)
			{
				sb.Append($"-out {OutputDirectory.ItemSpec} ");
			}
			if (LinkedAssembliesFile != null)
			{
				sb.Append($"-k {LinkedAssembliesFile.ItemSpec} ");
			}
			if (CoreAssembliesAction != null)
			{
				sb.Append($"-c {CoreAssembliesAction} ");
			}

			if (LinkerMode != null)
			{
				if (LinkerMode == "sdk")
				{
					sb.Append("-t -c link -l none");
				}
			}

			return sb.ToString();
		}

	}
}
