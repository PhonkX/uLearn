using System;

namespace uLearn.CSharp.CodeDuplicationValidation.TestData.Correct
{
	public class TotallyDifferentIfBlocks
	{
		public void MethodWithIfs()
		{
			if (true)
				Console.WriteLine("1");
			if (false)
			{
				var a = 1;
			}
		}
	}
}