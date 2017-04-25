using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILLink.Tests
{
	public class WebApiTest : IntegrationTestBase
	{
		public WebApiTest(ITestOutputHelper output) : base(output) {}

		public string SetupProject()
		{
			string projectRoot = "webapi";

			if (Directory.Exists(projectRoot)) {
				Directory.Delete(projectRoot, true);
			}

			Directory.CreateDirectory(projectRoot);
			int ret = Dotnet("new webapi", projectRoot);
			if (ret != 0) {
				output.WriteLine("dotnet new failed");
				Assert.True(false);
			}

			string csproj = Path.Combine(projectRoot, $"{projectRoot}.csproj");
			return csproj;
		}

		public void FixConfigureCall(string program)
		{
			output.WriteLine("ConfigureConfiguration -> ConfigureAppConfiguration");
			File.WriteAllLines(program,
				File.ReadAllLines(program)
				.Select(l =>
					l.Replace("ConfigureConfiguration",
						"ConfigureAppConfiguration")));
		}

		[Fact]
		public void RunWebApi()
		{
			string csproj = SetupProject();

			// TODO: remove this once the fix from
			// https://github.com/dotnet/templating/commit/95f7b4a3a1a3668a8b8139de7fe7f8f383f3af0c
			// shows up in the installed cli
			string program = Path.Combine(Path.GetDirectoryName(csproj), "Program.cs");
			FixConfigureCall(program);

			AddLinkerReference(csproj);

			BuildAndLink(csproj);
		}
	}
}
