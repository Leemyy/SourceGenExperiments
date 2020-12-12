using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGeneratorSamples
{
	[Generator]
	public class InheritanceGenerator : ISourceGenerator
	{
		private static readonly DiagnosticDescriptor _DiagnosticIncorrectNamespace =
			new("EG0001", "Types derived from Parent must be in the namespace 'Experiments.Children'", "The Type '{0}' must be defined in the namespace 'Experiments.Children'; otherwise code generation will not function properly", "Experiments", DiagnosticSeverity.Error, true);


		public void Initialize(GeneratorInitializationContext context) {
			// Register a syntax receiver that will be created for each generation pass
			context.RegisterForSyntaxNotifications(() => new Receiver());
		}

		public void Execute(GeneratorExecutionContext context) {
			if (context.SyntaxReceiver is not Receiver receiver)
				return;

			if (context.Compilation is not CSharpCompilation compilation)
				return;

			INamedTypeSymbol? parent = compilation.GetTypeByMetadataName("Experiments.Parent");
			if (parent is null) {
				return;
			}

			var source = new StringBuilder(@"
using System;

namespace Experiments
{
	public class Stats
	{
");

			//used to assemble the full namespace in the correct order
			List<string> stack = new();
			//loop over the candidate classes, and keep the ones that derive from Parent
			List<INamedTypeSymbol> childTypes = new();
			foreach (var candidate in receiver.CandidateTypes) {
				var model = compilation.GetSemanticModel(candidate.SyntaxTree);
				var declaredType = model.GetDeclaredSymbol(candidate)!;

				#region Emit debug members
				source = source.Append(@"		public static string ")
					.Append(declaredType.Name)
					.Append(@" = """);
				var nameSpc = declaredType.ContainingNamespace;
				while (nameSpc is not null && !nameSpc.IsGlobalNamespace) {
					stack.Add(nameSpc.Name);
					nameSpc = nameSpc.ContainingNamespace;
				}
				for (int i = stack.Count - 1; i >= 0; --i) {
					source = source.Append(stack[i]);
					if (i > 0)
						source = source.Append('.');
				}
				stack.Clear();
				source = source.AppendLine(@""";");
				#endregion

				bool isChild = false;
				var baseType = declaredType.BaseType;
				while (baseType is not null) {
					if (SymbolEqualityComparer.Default.Equals(baseType, parent)) {
						isChild = true;
						break;
					}
					//Move to next parent in the hierarchy
					baseType = baseType.BaseType;
				}
				if (!isChild)
					continue;

				source = source.Append(@"		public static bool ")
					.Append(candidate.Identifier.ToString());

				if (IsInChildNamespace(declaredType)) {
					childTypes.Add(declaredType);
					source = source.AppendLine(@"_IsChild = true;");
				}
				else {
					//Add error message
					context.ReportDiagnostic(Diagnostic.Create(_DiagnosticIncorrectNamespace, Location.Create(candidate.SyntaxTree, candidate.Identifier.Span), declaredType.Name));
					source = source.AppendLine(@"_IsChild = false;");
				}
			}

			source = source.AppendLine(@"
	}
}");
			context.AddSource("GeneratedCode", SourceText.From(source.ToString(), Encoding.UTF8));
		}

		private static bool IsInChildNamespace(INamedTypeSymbol type) {
			var parentNamespace = type.ContainingNamespace;
			if (parentNamespace is null)
				return false;
			if (parentNamespace.Name != "Child")
				return false;
			parentNamespace = parentNamespace.ContainingNamespace;
			if (parentNamespace.Name != "Experiments")
				return false;
			parentNamespace = parentNamespace.ContainingNamespace;
			if (!parentNamespace.IsGlobalNamespace)
				return false;
			return true;
		}

		/// <summary>
		/// Created on demand before each generation pass
		/// </summary>
		class Receiver : ISyntaxReceiver
		{
			public List<RecordDeclarationSyntax> CandidateTypes { get; } = new();

			/// <summary>
			/// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
			/// </summary>
			public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
				// any type with a base list is a potential candidate
				if (syntaxNode is RecordDeclarationSyntax decl
					&& decl.BaseList is not null) {
					CandidateTypes.Add(decl);
				}
			}
		}
	}
}
