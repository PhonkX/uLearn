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
				.OfType<MethodDeclarationSyntax>()
				.ToList();

			return classes.SelectMany(InspectMethod).ToList();
		}
		
		private IEnumerable<SolutionStyleError> InspectMethod(MethodDeclarationSyntax classDeclarationSyntax)
		{
			return InspectSyntaxInsideMethod<IfStatementSyntax>(classDeclarationSyntax)
				.Concat(InspectSyntaxInsideMethod<ForStatementSyntax>(classDeclarationSyntax))
				.Concat(InspectSyntaxInsideMethod<WhileStatementSyntax>(classDeclarationSyntax))
				.Concat(InspectSyntaxInsideMethod<DoStatementSyntax>(classDeclarationSyntax))
				.Concat(InspectSyntaxInsideMethod<ForEachStatementSyntax>(classDeclarationSyntax));
		}

		private IEnumerable<SolutionStyleError> InspectSyntaxInsideMethod<TSyntax>(MethodDeclarationSyntax classDeclarationSyntax) where TSyntax: SyntaxNode
		{
			var syntaxStatements = classDeclarationSyntax
				.DescendantNodes()
				.OfType<TSyntax>()
				.ToList();
			foreach (var statement1 in syntaxStatements)
			{
				var blockSyntax = statement1
					.ChildNodes()
					.OfType<BlockSyntax>()
					.FirstOrDefault();
				if (blockSyntax is null || blockSyntax.ChildNodes().Count() < 3)
					continue;

				foreach (var statement2 in syntaxStatements)
				{
					if (statement1 == statement2)
						continue;
					blockSyntax = statement2
						.ChildNodes()
						.OfType<BlockSyntax>()
						.FirstOrDefault();
					if (blockSyntax is null || blockSyntax.ChildNodes().Count() < 3)
						continue;
					
					var similarity = 1 - calculateDistance(statement1, statement2);
					if (similarity > SimilarityThreshold)
						yield return new SolutionStyleError(StyleErrorType.CodeDuplication01, statement1, statement2.GetLocation().GetLineSpan().StartLinePosition.Line);
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