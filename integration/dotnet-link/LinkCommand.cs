// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.CommandLine;

namespace Mono.Linker.Tools.Link
{
	class LinkCommand
	{
		private string _projectPath;
		private string _framework = null;
		private string _runtime;
		private string _outputPath;
		private string _configuration;
		private string _verbosity;
		private string _mode;
		private IReadOnlyList<string> _extraArgs;

		private string ProjectPath { get { return _projectPath; } }
		private string Framework { get { return _framework; } }
		private string Runtime { get { return _runtime; } }
		private string OutputPath { get { return _outputPath; } }
		private string Configuration { get { return _configuration; } }
		private string Verbosity { get { return _verbosity; } }
		private string Mode { get { return _mode; } }
		private IReadOnlyList<string> ExtraArgs { get { return _extraArgs; } }

		static int Main(string[] args)
		{
			LinkCommand linkCommand = new LinkCommand();
			return linkCommand.Run(args);
		}

		public int Run(string[] args) 
		{
			ArgumentSyntax argsyntax = ArgumentSyntax.Parse(args, syntax =>
			{
				syntax.DefineOption("f|framework", ref _framework, LocalizableStrings.FrameworkOptionDescription);
				syntax.DefineOption("r|runtime", ref _runtime, LocalizableStrings.RuntimeOptionDescription);
				syntax.DefineOption("o|output", ref _outputPath, LocalizableStrings.OutputOptionDescription);
				syntax.DefineOption("c|configuration", ref _configuration, LocalizableStrings.ConfigurationOptionDescription);
				syntax.DefineOption("m|mode", ref _mode, LocalizableStrings.ModeOptionDescription);
				syntax.DefineOption("v|verbosity", ref _verbosity, LocalizableStrings.VerbosityOptionDescription);
				syntax.DefineParameter("project", ref _projectPath, LocalizableStrings.ProjectArgumentDescription);

				syntax.DefineParameterList("msbuildArguments", ref _extraArgs, LocalizableStrings.ExtraArgumentsDescription);
			});

			return Execute();
		}

		public int Execute()
		{
			List<string> msbuildArgs = new List<string>();

			if (!string.IsNullOrEmpty(ProjectPath))
			{
				msbuildArgs.Add(ProjectPath);
			}

			msbuildArgs.Add("/t:Link");

			if (!string.IsNullOrEmpty(Framework))
			{
				msbuildArgs.Add($"/p:TargetFramework={Framework}");
			}

			if (!string.IsNullOrEmpty(Runtime))
			{
				msbuildArgs.Add($"/p:RuntimeIdentifier={Runtime}");
			}

			if (!string.IsNullOrEmpty(OutputPath))
			{
				msbuildArgs.Add($"/p:PublishDir={OutputPath}");
			}

			if (!string.IsNullOrEmpty(Configuration))
			{
				msbuildArgs.Add($"/p:Configuration={Configuration}");
			}

			if (!string.IsNullOrEmpty(Mode))
			{
				msbuildArgs.Add($"/p:LinkerMode={Mode}");
			}

			if (!string.IsNullOrEmpty(Verbosity))
			{
				msbuildArgs.Add($"/verbosity:{Verbosity}");
			}

			if (ExtraArgs != null)
			{
				foreach (var arg in ExtraArgs)
				{
					msbuildArgs.Add(arg);
				}
			}

			// Is it safe to take dotnet from the command
			// line here? Apparently this is a common
			// convention for our cli tools, so we do the
			// same.
			var psi = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = "msbuild " + String.Join(" ", msbuildArgs),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			var process = new Process
			{
				StartInfo = psi,
			};

			process.Start();
			string output = process.StandardOutput.ReadToEnd();
			Console.WriteLine(output);
			string error = process.StandardError.ReadToEnd();
			Console.WriteLine(error);
			process.WaitForExit();
			return process.ExitCode;
		}
	}
}
