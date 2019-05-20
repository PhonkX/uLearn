using System;

namespace uLearn.CSharp.CodeDuplicationValidation
{
	public class testdata
	{
		public void TestMethod()
		{
			if (true)
			{
				Console.Write(1);
				Console.Write(1);
			}

			if (true)
			{
				Console.WriteLine(2);
				Console.WriteLine(2);
			}
		}
	}
}