using System;

namespace uLearn.CSharp.CodeDuplicationValidation.TestData
{
	public class Correct1
	{
		public void TestMethod()
		{
			if (true)
				Console.Write(1);
			if (true)
				Console.Write(1);
		}
	}
}