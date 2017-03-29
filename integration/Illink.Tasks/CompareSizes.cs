using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace CompareAssemblySizes
{
	struct AssemblySizes
	{
	public long unlinkedSize;
	public long linkedSize;
	}
	public class CompareAssemblySizes : Task
	{
	[Required]
	public ITaskItem UnlinkedDir { get; set; }

	[Required]
	public ITaskItem LinkedDir { get; set; }

	public override bool Execute()
	{
		string unlinkedDir = UnlinkedDir.ItemSpec;
		string linkedDir = LinkedDir.ItemSpec;

		string[] unlinkedFiles = Directory.GetFiles (unlinkedDir);
		string[] linkedFiles = Directory.GetFiles (linkedDir);

		Dictionary<string, AssemblySizes> sizes = new Dictionary<string, AssemblySizes> ();

		long totalUnlinked = 0;
		foreach (string unlinkedFile in unlinkedFiles) {
		try {
			AssemblyName.GetAssemblyName (unlinkedFile);
		}
		catch (BadImageFormatException) {
			continue;
		}
		string fileName = Path.GetFileName (unlinkedFile);
		AssemblySizes assemblySizes = new AssemblySizes ();
		assemblySizes.unlinkedSize = new System.IO.FileInfo (unlinkedFile).Length;
		totalUnlinked += assemblySizes.unlinkedSize;
		sizes[fileName] = assemblySizes;
		}

		long totalLinked = 0;
		foreach (string linkedFile in linkedFiles) {
		try {
			AssemblyName.GetAssemblyName (linkedFile);
		}
		catch (BadImageFormatException) {
			continue;
		}
		string fileName = Path.GetFileName (linkedFile);
		AssemblySizes assemblySizes = sizes[fileName];
		assemblySizes.linkedSize = new System.IO.FileInfo (linkedFile).Length;
		totalLinked += assemblySizes.linkedSize;
		sizes[fileName] = assemblySizes;
		}

			long unlinkedDirSize = DirSize (new DirectoryInfo (unlinkedDir));
			long linkedDirSize = DirSize (new DirectoryInfo (linkedDir));

			Console.WriteLine ("{0, -60} {1,-20:N0} {2, -20:N0} {3, -10:P}",
				"Total directory size",
				unlinkedDirSize,
				linkedDirSize,
				((double)unlinkedDirSize - (double)linkedDirSize) / (double)unlinkedDirSize);

			Console.WriteLine ("{0, -60} {1,-20:N0} {2, -20:N0} {3, -10:P}",
				"Total size of assemblies",
				totalUnlinked,
				totalLinked,
				((double)totalUnlinked - (double)totalLinked) / (double)totalUnlinked);

			Console.WriteLine ("-----------");
			Console.WriteLine ("Details");
			Console.WriteLine ("-----------");

			foreach (string assembly in sizes.Keys) {
				Console.WriteLine ("{0, -60} {1,-20:N0} {2, -20:N0} {3, -10:P}",
					 assembly,
			sizes[assembly].unlinkedSize,
			sizes[assembly].linkedSize, 
			(double)(sizes[assembly].unlinkedSize - sizes[assembly].linkedSize)/(double)sizes[assembly].unlinkedSize);
		}
		return true;
	}

		public static long DirSize(DirectoryInfo d)
		{
			long size = 0;
			// Add file sizes.
			FileInfo[] fis = d.GetFiles ();
			foreach (FileInfo fi in fis) {
				size += fi.Length;
			}
			// Add subdirectory sizes.
			DirectoryInfo[] dis = d.GetDirectories ();
			foreach (DirectoryInfo di in dis) {
				size += DirSize (di);
			}
			return size;
		}
	}
}
