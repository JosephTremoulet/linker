namespace Mono.Linker.Tools.Link
{
    internal class LocalizableStrings
    {
        public const string AppFullName = "link";

        public const string AppDescription = "link for msbuild";

        public const string CmdArgumentProject = "PROJECT";

        public const string ProjectArgumentDescription = "The MSBuild project file to build. If a project file is not specified, MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file.";

        public const string ProjectArgumentValueName = "PROJECT";

        public const string FrameworkOption = "FRAMEWORK";

        public const string FrameworkOptionDescription = "Target framework to publish for";

        public const string RuntimeOption = "RUNTIME_IDENTIFIER";

        public const string RuntimeOptionDescription = "Target runtime to publish for. The default is to publish a portable application.";

        public const string OutputOption = "OUTPUT_DIR";

        public const string OutputOptionDescription = "Path in which to publish the app";

        public const string ConfigurationOption = "CONFIGURATION";

        public const string ConfigurationOptionDescription = "Configuration under which to build";

    }
}
