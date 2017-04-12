using System;
using System.IO;
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
			int ret = RunCommand("dotnet", "new webapi", projectRoot);
			if (ret != 0) {
				output.WriteLine("dotnet new failed");
				Assert.True(false);
			}

			string csproj = Path.Combine(projectRoot, $"{projectRoot}.csproj");
			return csproj;
		}

		[Fact]
		public void RunWebApi()
		{
			string csproj = SetupProject();

			AddLinkerReference(csproj);

			BuildAndLink(csproj);
		}
	}
}
