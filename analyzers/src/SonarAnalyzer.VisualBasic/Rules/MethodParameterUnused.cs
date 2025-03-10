﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2014-2025 SonarSource SA
 * mailto:info AT sonarsource DOT com
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the Sonar Source-Available License Version 1, as published by SonarSource SA.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the Sonar Source-Available License for more details.
 *
 * You should have received a copy of the Sonar Source-Available License
 * along with this program; if not, see https://sonarsource.com/license/ssal/
 */

namespace SonarAnalyzer.VisualBasic.Rules
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public sealed class MethodParameterUnused : MethodParameterUnusedBase
    {
        private const string MessageFormat = "Remove this unused procedure parameter '{0}'.";

        private static readonly DiagnosticDescriptor Rule = DescriptorFactory.Create(DiagnosticId, MessageFormat);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void Initialize(SonarAnalysisContext context) =>
            context.RegisterNodeAction(c =>
                {
                    var methodBlock = (MethodBlockSyntax)c.Node;

                    // Bail-out if this is not a method we want to report on (only based on syntax checks)
                    if (methodBlock.SubOrFunctionStatement == null
                        || !HasAnyParameter(methodBlock)
                        || IsEmptyMethod(methodBlock)
                        || IsVirtualOrOverride(methodBlock)
                        || IsInterfaceImplementation(methodBlock)
                        || IsWithEventsHandler(methodBlock)
                        || HasAnyAttribute(methodBlock)
                        || OnlyThrowsNotImplementedException(methodBlock, c.Model))
                    {
                        return;
                    }

                    var unusedParameters = GetUnusedParameters(methodBlock);
                    if (unusedParameters.Count == 0)
                    {
                        return;
                    }

                    // Bail-out if this is not a method we want to report on (only based on symbols checks)
                    var methodSymbol = c.Model.GetDeclaredSymbol(methodBlock);
                    if (methodSymbol == null
                        || methodSymbol.IsMainMethod()
                        || methodSymbol.IsEventHandler()
                        || methodSymbol.GetEffectiveAccessibility() != Accessibility.Private)
                    {
                        return;
                    }

                    foreach (var parameter in unusedParameters)
                    {
                        c.ReportIssue(Rule, parameter, parameter.Identifier.Identifier.ValueText);
                    }
                },
                SyntaxKind.SubBlock,
                SyntaxKind.FunctionBlock);

        private static bool HasAnyParameter(MethodBlockBaseSyntax method) =>
            method.BlockStatement.ParameterList != null
            && method.BlockStatement.ParameterList.Parameters.Any();

        private static bool IsEmptyMethod(MethodBlockBaseSyntax method) =>
            method.Statements.Count == 0;

        private static bool IsVirtualOrOverride(MethodBlockBaseSyntax method) =>
             method.BlockStatement.Modifiers.Any(x => x.Kind() is SyntaxKind.OverridesKeyword or SyntaxKind.OverridableKeyword);

        private static bool IsInterfaceImplementation(MethodBlockSyntax method) =>
            method.SubOrFunctionStatement.ImplementsClause != null;

        private static bool IsWithEventsHandler(MethodBlockSyntax method) =>
            method.SubOrFunctionStatement.HandlesClause != null;

        private static bool HasAnyAttribute(MethodBlockBaseSyntax method) =>
            method.BlockStatement.AttributeLists.Any();

        private static bool OnlyThrowsNotImplementedException(MethodBlockBaseSyntax method, SemanticModel semanticModel) =>
            method.Statements.Count == 1
            && method.Statements
                .OfType<ThrowStatementSyntax>()
                .Select(x => x.Expression)
                .OfType<ObjectCreationExpressionSyntax>()
                .Select(x => semanticModel.GetSymbolInfo(x).Symbol)
                .OfType<IMethodSymbol>()
                .Any(x => x.ContainingType.Is(KnownType.System_NotImplementedException));

        private static List<ParameterSyntax> GetUnusedParameters(MethodBlockBaseSyntax methodBlock)
        {
            var usedIdentifiers = methodBlock.Statements.SelectMany(x => x.DescendantNodes())
                    .Where(node => node.IsKind(SyntaxKind.IdentifierName) && IsVarOrParameter(node))
                    .Cast<IdentifierNameSyntax>()
                    .Select(x => x.Identifier.ValueText)
                    .WhereNotNull()
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            return methodBlock.BlockStatement.ParameterList.Parameters
                .Where(p => !usedIdentifiers.Contains(p.Identifier.Identifier.ValueText))
                .ToList();

            static bool IsVarOrParameter(SyntaxNode node) =>
                node.Parent switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Expression == node,
                    ConditionalAccessExpressionSyntax conditionalAccess => conditionalAccess.Expression == node,
                    _ => true
                };
        }
    }
}
