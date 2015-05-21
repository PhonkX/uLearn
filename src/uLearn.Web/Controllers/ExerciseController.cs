﻿using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using uLearn.Web.DataContexts;
using uLearn.Web.ExecutionService;
using uLearn.Web.Models;

namespace uLearn.Web.Controllers
{
	public class ExerciseController : Controller
	{
		private readonly CourseManager courseManager;
		private readonly UserSolutionsRepo solutionsRepo = new UserSolutionsRepo();
		private readonly VisitersRepo visitersRepo = new VisitersRepo();

		public ExerciseController()
			: this(WebCourseManager.Instance)
		{
		}

		public ExerciseController(CourseManager courseManager)
		{
			this.courseManager = courseManager;
		}

		[HttpPost]
		[Authorize]
		public async Task<ActionResult> RunSolution(string courseId, int slideIndex = 0)
		{
			var code = GetUserCode(Request.InputStream);
			if (code.Length > TextsRepo.MAX_TEXT_SIZE)
			{
				return Json(new RunSolutionResult
				{
					IsCompileError = true,
					CompilationError = "Слишком большой код."
				});
			}
			var exerciseSlide = (ExerciseSlide)courseManager.GetCourse(courseId).Slides[slideIndex];
			var result = await CheckSolution(exerciseSlide, code, RedundantExecutionService.Default);
			await SaveUserSolution(courseId, exerciseSlide.Id, code, result.CompilationError, result.ActualOutput, result.IsRightAnswer, result.ExecutionServiceName);
			return Json(result);
		}

		private async Task<RunSolutionResult> CheckSolution(ExerciseSlide exerciseSlide, string code, IExecutionService executionService)
		{
			var exerciseBlock = exerciseSlide.Exercise;
			var solution = exerciseBlock.Solution.BuildSolution(code);
			if (solution.HasErrors)
				return new RunSolutionResult { IsCompileError = true, CompilationError = solution.ErrorMessage, ExecutionServiceName = "uLearn" };
			if (solution.HasStyleIssues)
				return new RunSolutionResult { IsStyleViolation = true, CompilationError = solution.StyleMessage, ExecutionServiceName = "uLearn" };
			var submissionDetails = await executionService.Submit(solution.SourceCode, GenerateSubmissionName(exerciseSlide));
			if (submissionDetails == null)
				return new RunSolutionResult
				{
					IsCompillerFailure = true, 
					CompilationError = "Ой-ой, штуковина, которая проверяет решения сломалась (или просто устала). Попробуйте отправить решение позже (когда она немного отдохнет).", 
					ExecutionServiceName = executionService.Name
				};
			var output = submissionDetails.GetOutput();
			var expectedOutput = exerciseBlock.ExpectedOutput.NormalizeEoln();
			var isRightAnswer = submissionDetails.IsSuccess && output.Equals(expectedOutput);
			return new RunSolutionResult
			{
				IsCompileError = submissionDetails.IsCompilationError,
				CompilationError = submissionDetails.CompilationErrorMessage,
				IsRightAnswer = isRightAnswer,
				ExpectedOutput = exerciseBlock.HideExpectedOutputOnError ? null : expectedOutput,
				ActualOutput = output,
				ExecutionServiceName = submissionDetails.ServiceName
			};
		}

		private async Task SaveUserSolution(string courseId, string slideId, string code, string compilationError, string output, bool isRightAnswer, string executionServiceName)
		{
			var userId = User.Identity.GetUserId();
			await solutionsRepo.AddUserSolution(
				courseId, slideId,
				code, isRightAnswer, compilationError, output,
				userId, executionServiceName);
			await visitersRepo.AddSolutionAttempt(slideId, userId, isRightAnswer);
		}


		private static string GetUserCode(Stream inputStream)
		{
			var codeBytes = new MemoryStream();
			inputStream.CopyTo(codeBytes);
			return Encoding.UTF8.GetString(codeBytes.ToArray());
		}

		private string GenerateSubmissionName(Slide exerciseSlide)
		{
			return string.Format("{0}: {1} - {2}", User.Identity.Name, exerciseSlide.Info.UnitName, exerciseSlide.Title);
		}
	}
}
