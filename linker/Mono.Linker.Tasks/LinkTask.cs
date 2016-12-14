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

        private const string ExeName = "monolinker.exe";

        public ITaskItem OutputDirectory { get; set; }
        public ITaskItem AssemblyDirectory { get; set; }
        public string CoreAssembliesAction { get; set; }

        [Required]
        public ITaskItem InputAssembly { get; set; }

        // required methods and properties
        protected override string GenerateFullPathToTool()
        {
            // TODO: remove hard-coding of tool path

            // for now, expect monolinker to be on the path
            return "monolinker.exe";
        }

        protected override string ToolName
        {
            get { return "monolinker.exe"; }
        }

        // optional methods and properties
        protected override string GenerateCommandLineCommands()
        {
            StringBuilder sb = new StringBuilder();
            if (InputAssembly != null)
            {
                sb.Append($"-a {InputAssembly.ItemSpec} ");
            }
            if (AssemblyDirectory != null)
            {
                sb.Append($"-d {AssemblyDirectory.ItemSpec}" );
            }
            if (OutputDirectory != null)
            {
                sb.Append($"-out {OutputDirectory.ItemSpec} ");
            }
            if (CoreAssembliesAction != null)
            {
                sb.Append($"-c {CoreAssembliesAction} ");
            }
            return sb.ToString();
        }

    }
}
