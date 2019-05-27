using System;

namespace uLearn.CSharp.CodeDuplicationValidation.TestData
{
	public class SameIfBlocks
	{
		public void TestMethod()
		{
			if (true)
			{
				Console.Write(1);
				Console.Write(1);
				Console.Write(1);
			}

			if (true)
			{
				Console.Write(1);
				Console.Write(1);
				Console.Write(1);
			}
		}
	}
}