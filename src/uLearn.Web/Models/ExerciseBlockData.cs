namespace uLearn.Web.Models
{
	public class ExerciseBlockData
	{
		public ExerciseBlockData(bool showControls = true, bool isSkipped = true, string latestAcceptedSolution = null)
		{
			ShowControls = showControls;
			LatestAcceptedSolution = latestAcceptedSolution;
			CanSkip = !isSkipped && LatestAcceptedSolution == null;
		}

		public bool ShowControls { get; private set; }
		public bool CanSkip { get; private set; }
		public string LatestAcceptedSolution { get; private set; }
		public string RunSolutionUrl { get; set; } // Url.Action("RunSolution", "Exercise", new {courseId, slideIndex})
		public string AcceptedSolutionUrl { get; set; } // Url.Action("AcceptedSolutions", "Course", new {courseId, slideIndex});
		public string GetHintUrl { get; set; } // Url.Action("UseHint", "Hint")
		
		public bool IsLti { get; set; }
		public bool IsSkippedOrPassed { get; set; }
		public string CourseId { get; set; }
		public int SlideIndex { get; set; }
	}
}