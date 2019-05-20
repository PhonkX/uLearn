using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ulearn.Core.CSharp.Validators
{
	public class CodeDuplicationValidator: BaseStyleValidator
	{
		private const double SimilarityThreshold = 0.67;
		
		private static Func<SyntaxNode, SyntaxNode, double> calculateDistance;

		static CodeDuplicationValidator()
		{
			InitializeCalculateDistanceMethod();
		}

		public override List<SolutionStyleError> FindErrors(SyntaxTree userSolution, SemanticModel semanticModel)
		{
			if (calculateDistance == null)
				return new List<SolutionStyleError>();
			var classes = userSolution
				.GetRoot()
				.DescendantNodes()
				.OfType<ClassDeclarationSyntax>()
				.ToList();

			return classes.SelectMany(InspectClass).ToList();
		}
		
		private IEnumerable<SolutionStyleError> InspectClass(ClassDeclarationSyntax classDeclarationSyntax)
		{
			return InspectIfsInsideClass(classDeclarationSyntax)
				.Concat(InspectMethodsInsideClass(classDeclarationSyntax));
		}

		private IEnumerable<SolutionStyleError> InspectIfsInsideClass(ClassDeclarationSyntax classDeclarationSyntax)
		{
			var ifStatements = classDeclarationSyntax
				.DescendantNodes()
				.OfType<IfStatementSyntax>()
				.ToList();
			foreach (var ifStatementSyntax1 in ifStatements)
			{
				var ifStatement1ChildNodes = ifStatementSyntax1
					.ChildNodes()
					.ToList();
				var statementUnderIf1 = ifStatement1ChildNodes[1];
				if (!(statementUnderIf1 is BlockSyntax) || statementUnderIf1.ChildNodes().Count() < 3)
					continue;

				foreach (var ifStatementSyntax2 in ifStatements)
				{
					var ifStatement2ChildNodes = ifStatementSyntax2
						.ChildNodes()
						.ToList();
					var statementUnderIf2 = ifStatement2ChildNodes[1];
					if (!(statementUnderIf2 is BlockSyntax) || statementUnderIf2.ChildNodes().Count() < 3)
						continue;

					if (ifStatementSyntax1 == ifStatementSyntax2)
						continue;
					var similarity = 1 - calculateDistance(ifStatementSyntax1, ifStatementSyntax2);
					if (similarity > SimilarityThreshold)
						yield return new SolutionStyleError(StyleErrorType.CodeDuplication01, ifStatementSyntax1, ifStatementSyntax2.GetLocation().GetLineSpan().StartLinePosition.Line);
				}
			}
		}

		private IEnumerable<SolutionStyleError> InspectMethodsInsideClass(ClassDeclarationSyntax classDeclarationSyntax)
		{
			var methodStatements = classDeclarationSyntax
				.DescendantNodes()
				.OfType<MethodDeclarationSyntax>()
				.ToList();
			foreach (var methodStatementSyntax1 in methodStatements)
			{
				foreach (var methodStatementSyntax2 in methodStatements)
				{
					if (methodStatementSyntax1 == methodStatementSyntax2)
						continue;
					var similarity = 1 - calculateDistance(methodStatementSyntax1, methodStatementSyntax2);
					if (similarity > SimilarityThreshold)
						yield return new SolutionStyleError(StyleErrorType.CodeDuplication01, methodStatementSyntax1, methodStatementSyntax2.GetLocation().GetLineSpan().StartLinePosition.Line);
				}
			}
		}

		private static void InitializeCalculateDistanceMethod()
        {
            try
            {
				var assembly = AppDomain.CurrentDomain.Load("Microsoft.CodeAnalysis.CSharp.Features");
				var type = assembly.GetType("Microsoft.CodeAnalysis.CSharp.EditAndContinue.StatementSyntaxComparer");
				var field = type.GetField("Default", BindingFlags.NonPublic | BindingFlags.Static);
				var instance = field?.GetValue(null);
				if (instance == null)
				{
					return;
				}

				var typeOfGetDistanceMethod = typeof(Func<SyntaxNode, SyntaxNode, double>);
				calculateDistance = (Func<SyntaxNode, SyntaxNode, double>)Delegate.CreateDelegate(typeOfGetDistanceMethod, instance, "GetDistance");
			}
			catch (Exception)
			{
			}
		}
	}
}