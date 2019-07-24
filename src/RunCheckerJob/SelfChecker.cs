using System;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using NUnit.Framework;
using Ulearn.Common.Extensions;
using Ulearn.Core;
using Ulearn.Core.RunCheckerJobApi;

namespace RunCheckerJob
{
	internal class SelfChecker
	{
		private readonly DockerSandboxRunner sandboxRunner;
		private static readonly string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		private static readonly ILog log = LogManager.GetLogger(typeof(SelfChecker));

		public SelfChecker(DockerSandboxRunner sandboxRunner)
		{
			this.sandboxRunner = sandboxRunner;
		}
		
		public RunningResults JsSelfCheck()
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			var exerciseDirectory = new DirectoryInfo(Path.GetFullPath(Path.Combine(baseDirectory, "../../../../../sandboxes/js-sandbox/sample/src")));
			var zipBytes = exerciseDirectory.ToZip(new []{"node_modules", ".idea"});
			var res = sandboxRunner.Run(new CommandRunnerSubmission
			{
				Id = Utils.NewNormalizedGuid(),
				Language = Language.JavaScript,
				ZipFileData = zipBytes,
				DockerImageName = "js-sandbox",
				RunCommand = "node docker-test-runner.js"
			});
			log.Info(res);
			return res;
		}
	}

	[TestFixture, Explicit]
	internal class SelfCheckerTests
	{
		[Test, Explicit]
		public void JsSelfCheckTest()
		{
			var res = new SelfChecker(new DockerSandboxRunner())
				.JsSelfCheck();
			Assert.AreEqual(Verdict.Ok, res.Verdict);
		}
	}
}