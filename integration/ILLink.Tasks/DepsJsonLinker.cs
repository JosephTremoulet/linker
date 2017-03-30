using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace DepsJsonLinker
{
	public class DepsJsonLinker : Task
	{

		[Required]
		public ITaskItem InputDepsFilePath { get; set; }

		[Required]
		public ITaskItem OutputDepsFilePath { get; set; }

		[Required]
		public ITaskItem[] ManagedPublishAssemblies { get; set; }

		[Required]
		public ITaskItem[] KeptAssemblies { get; set; }

		public ITaskItem[] RemovedAssemblies { get; set; }


		public override bool Execute()
		{
			string inputFile = InputDepsFilePath.ItemSpec;
			string outputFile = OutputDepsFilePath.ItemSpec;

			string[] keptAssemblies = KeptAssemblies.Select(a => a.ItemSpec).ToArray();
			string[] allAssemblies = ManagedPublishAssemblies.Select(a => a.ItemSpec).ToArray();
			string[] removedAssemblies = allAssemblies.Except(keptAssemblies).ToArray();

			var removedAssembliesSet = new HashSet<string> (removedAssemblies, StringComparer.InvariantCultureIgnoreCase);

			JObject o = JObject.Parse (File.ReadAllText (inputFile));

			JObject targets = (JObject)o["targets"];

			// Remove targets
			foreach (JProperty target in targets.Children()) {
				JEnumerable<JToken> children = target.Value.Children ();
				for (int i = 0; i < children.Count(); ++i) {
					//foreach (JProperty subtarget in target.Value.Children()) {
					var subtarget = (JProperty) children.ElementAt (i);
					string name = subtarget.Name.Substring (0, subtarget.Name.IndexOf ('/'));
					if (removedAssembliesSet.Contains (name + ".dll")) {
						subtarget.Remove ();
						i--;
						continue;
					}

					// Remove dependencies
					var dependencies = subtarget.Value["dependencies"];
					if (dependencies != null) {
						for (int j = 0; j < dependencies.Count (); ++j) {
							var dependency = ((JProperty)dependencies.ElementAt (j));

							if (removedAssembliesSet.Contains (dependency.Name + ".dll")) {

								dependency.Remove ();
								j--;
								continue;
							}
						}
					}

					// Remove runtimes
					var runtimes = subtarget.Value["runtime"];
					if (runtimes != null) {
						for (int j = 0; j < runtimes.Count (); ++j) {
							var runtime = ((JProperty)runtimes.ElementAt (j));
							string runtimeFileName = runtime.Name.Substring (runtime.Name.LastIndexOf ('/') + 1);

							if (removedAssembliesSet.Contains (runtimeFileName)) {
								runtime.Remove ();
								j--;
								continue;
							}
						}
					}
				}
			}

			// Remove libraries
			JObject libraries = (JObject)o["libraries"];

			JEnumerable<JToken> libraryChildren = libraries.Children ();
			for (int i = 0; i < libraryChildren.Count (); ++i) {
				var library = (JProperty)libraryChildren.ElementAt (i);
				string name = library.Name.Substring (0, library.Name.IndexOf ('/'));
				if (removedAssembliesSet.Contains (name + ".dll")) {
					library.Remove ();
					i--;
					continue;
				}
			}

			File.WriteAllText (outputFile, o.ToString ());

			return true;
		}
	}
}
