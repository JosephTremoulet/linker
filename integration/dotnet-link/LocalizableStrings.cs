namespace Mono.Linker.Tools.Link
{
	internal class LocalizableStrings
	{
		public const string ProjectArgumentDescription = "The MSBuild project file to build. If a project file is not specified, MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file.";

		public const string FrameworkOptionDescription = "Target framework to link for";

		public const string RuntimeOptionDescription = "Target runtime to link for. The default is to link a portable application.";

		public const string OutputOptionDescription = "Path in which to place the linked app";

		public const string ConfigurationOptionDescription = "Configuration under which to build";

		public const string ModeOptionDescription = "Linker mode to use. Allowed values are sdk";

		public const string VerbosityOptionDescription = "Set the verbosity level of the command. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]";

		public const string ExtraArgumentsDescription = "Extra arguments to pass to msbuild";

	}
}
