using System;

namespace uLearn
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class SingleStatementMethodAttribute : Attribute
	{
	}
}