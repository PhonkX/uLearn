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
			// return InspectIfsInsideClass(classDeclarationSyntax)
			// 	.Concat(InspectMethodsInsideClass(classDeclarationSyntax));
			// return InspectMethodsInsideClass(classDeclarationSyntax);
			return InspectSyntaxInsideClass<IfStatementSyntax>(classDeclarationSyntax)
				//.Concat(InspectSyntaxInsideClass<MethodDeclarationSyntax>(classDeclarationSyntax))
				.Concat(InspectSyntaxInsideClass<ForStatementSyntax>(classDeclarationSyntax))
				.Concat(InspectSyntaxInsideClass<WhileStatementSyntax>(classDeclarationSyntax))
				.Concat(InspectSyntaxInsideClass<DoStatementSyntax>(classDeclarationSyntax))
				.Concat(InspectSyntaxInsideClass<ForEachStatementSyntax>(classDeclarationSyntax))
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
				var methodChildNodes = methodStatementSyntax1
					.ChildNodes()
					.ToList();
				var blockSyntax = methodChildNodes
					.OfType<BlockSyntax>()
					.FirstOrDefault();
				if (blockSyntax is null || blockSyntax.ChildNodes().Count() < 3)
					continue;

				var isMethodTest = methodChildNodes.Any(n => n is AttributeListSyntax && n.ToString().Contains("Test"));
				if (isMethodTest)
					continue;
				
				foreach (var methodStatementSyntax2 in methodStatements)
				{
					if (methodStatementSyntax1 == methodStatementSyntax2)
						continue;
					methodChildNodes = methodStatementSyntax2
						.ChildNodes()
						.ToList();
					blockSyntax = methodChildNodes
						.OfType<BlockSyntax>()
						.FirstOrDefault();
					if (blockSyntax is null || blockSyntax.ChildNodes().Count() < 3)
						continue;
					isMethodTest = methodChildNodes.Any(n => n is AttributeListSyntax && n.ToString().Contains("Test"));
					if (isMethodTest)
						continue;
					
					var similarity = 1 - calculateDistance(methodStatementSyntax1, methodStatementSyntax2);
					if (similarity > SimilarityThreshold)
						yield return new SolutionStyleError(StyleErrorType.CodeDuplication01, methodStatementSyntax1, methodStatementSyntax2.GetLocation().GetLineSpan().StartLinePosition.Line);
				}
			}
		}
		
		private IEnumerable<SolutionStyleError> InspectSyntaxInsideClass<TSyntax>(ClassDeclarationSyntax classDeclarationSyntax) where TSyntax: SyntaxNode
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