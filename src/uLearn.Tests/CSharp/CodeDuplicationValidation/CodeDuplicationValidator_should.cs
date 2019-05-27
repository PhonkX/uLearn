using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApprovalTests;
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
using FluentAssertions;
using NUnit.Framework;
using Ulearn.Common.Extensions;
using Ulearn.Core.CSharp.Validators;

namespace uLearn.CSharp.CodeDuplicationValidation
{
	[TestFixture]
	public class CodeDuplicationValidator_should
	{
		private readonly CodeDuplicationValidator validator = new CodeDuplicationValidator();

		private static readonly DirectoryInfo TestDataDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..",
			"..", "CSharp", "CodeDuplicationValidation", "TestData"));

		private static DirectoryInfo IncorrectTestDataDir => TestDataDir.GetDirectories("Incorrect").Single();
		private static DirectoryInfo CorrectTestDataDir => TestDataDir.GetDirectories("Correct").Single();

		private static string[] correctFilenames => CorrectTestDataDir.GetFiles().Select(f => f.Name).ToArray();
		private static string[] incorrectFilenames => IncorrectTestDataDir.GetFiles().Select(f => f.Name).ToArray();
		
		private static DirectoryInfo BasicProgrammingDirectory =>
        			new DirectoryInfo(TestPaths.BasicProgrammingDirectoryPath);
        
        private static IEnumerable<FileInfo> BasicProgrammingFiles =>
        	BasicProgrammingDirectory
        		.EnumerateFiles("*.cs", SearchOption.AllDirectories)
        		.Where(f => !f.Name.Equals("Settings.Designer.cs") &&
        					!f.Name.Equals("Resources.Designer.cs") &&
        					!f.Name.Equals("AssemblyInfo.cs"));

		private static DirectoryInfo ULearnSubmissionsDirectory =>
			new DirectoryInfo(TestPaths.ULearnSubmissionsDirectoryPath);

		private static IEnumerable<FileInfo> SubmissionsFiles => ULearnSubmissionsDirectory
			.EnumerateFiles("*.cs", SearchOption.AllDirectories)
			.Where(f => f.Name.Contains("Accepted"));
		
		[Test]
		[TestCaseSource(nameof(incorrectFilenames))]
		[UseReporter(typeof(DiffReporter))]
		public void FindErrors(string filename)
		{
			var code = IncorrectTestDataDir.GetFiles(filename).Single().ContentAsUtf8();
			var errors = validator.FindErrors(code);
			var errorMessages = errors.Select(e => e.GetMessageWithPositions());
			using (ApprovalResults.ForScenario(filename))
			{
				Approvals.Verify(string.Join(Environment.NewLine, errorMessages));
			}
		}

		[TestCaseSource(nameof(correctFilenames))]
		public void NotFindErrors(string filename)
		{
			var code = CorrectTestDataDir.GetFiles(filename).Single().ContentAsUtf8();
			var errors = validator.FindErrors(code);
			if (errors.Any())
			{
				Console.WriteLine(errors.Select(e => e.GetMessageWithPositions().ToList()));
			}
			errors.Should().BeNullOrEmpty();
		}
		
		[Explicit]
		[TestCaseSource(nameof(BasicProgrammingFiles))]
		public void NotFindErrors_InBasicProgramming(FileInfo file)
		{
			var fileContent = file.ContentAsUtf8();

			var errors = validator.FindErrors(fileContent);

			if (errors != null && errors.Count != 0)
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..",
						"..", "CSharp", "ExampleFiles", "errors", "code_duplication", $"{file.Name}_errors.txt"),
					$@"{fileContent}

{errors.Join(Environment.NewLine)}");

				Assert.Fail();
			}
		}

        [Explicit]
		[TestCaseSource(nameof(SubmissionsFiles))]
		public void NotFindErrors_InCheckAcceptedFiles(FileInfo file)
		{
			var fileContent = file.ContentAsUtf8();

			var errors = validator.FindErrors(fileContent);
			if (errors != null && errors.Count != 0)
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..",
						"..", "CSharp", "ExampleFiles", "submissions_errors", "code_duplication", $"{file.Name}_errors.txt"),
					$@"{fileContent}

{errors.Join(Environment.NewLine)}");

				Assert.Fail();
			}
		}
	}
}