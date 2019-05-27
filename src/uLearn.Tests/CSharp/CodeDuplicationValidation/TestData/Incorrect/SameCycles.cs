using System;
using System.Linq;

namespace uLearn.CSharp.CodeDuplicationValidation
{
	public class SameCycles
	{
		public void SameFors()
		{
			for (int i = 0; i < 3; i++)
			{
				Console.WriteLine(2);
				Console.WriteLine(3);
				Console.WriteLine(4);
			}

			for (int i = 0; i < 3; i++)
			{
				Console.WriteLine(2);
				Console.WriteLine(3);
				Console.WriteLine(4);
			}
		}

		public void SameWhiles()
		{
			while (true)
			{
				var a = Math.Pow(2, 2);
				var b = Math.Pow(3, 3);
				var c = Math.Pow(4, 4);
				var d = Math.Pow(5, 5);
			}

			while (true)
			{
				var a = Math.Pow(2, 2);
				var b = Math.Pow(3, 3);
				var c = Math.Pow(4, 4);
				var d = Math.Pow(5, 5);
			}
		}
		
		public void SameDoWhiles()
		{
			do
			{
				var a = 1;
				var b = 2;
				var c = 3;
				var d = 4;
			} while (true);

			do
			{
				var a = 1;
				var b = 2;
				var c = 3;
				var d = 4;
			} while (true);
		}

		public void SameForEaches()
		{
			var sum = 0;
			foreach (var number in Enumerable.Range(0, 3))
			{
				sum += number;
				sum *= 2;
				sum += 2;
			}
			
			foreach (var number in Enumerable.Range(0, 3))
			{
				sum += number;
				sum *= 2;
				sum += 2;
			}
		}
	}
}