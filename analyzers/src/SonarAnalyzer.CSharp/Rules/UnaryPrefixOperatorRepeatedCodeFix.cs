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

using Microsoft.CodeAnalysis.Formatting;

namespace SonarAnalyzer.CSharp.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class UnaryPrefixOperatorRepeatedCodeFix : SonarCodeFix
    {
        internal const string Title = "Remove repeated prefix operator(s)";
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UnaryPrefixOperatorRepeated.DiagnosticId);

        protected override Task RegisterCodeFixesAsync(SyntaxNode root, SonarCodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            if (!(root.FindNode(diagnosticSpan, getInnermostNodeForTie: true) is PrefixUnaryExpressionSyntax prefix))
            {
                return Task.CompletedTask;
            }

            context.RegisterCodeFix(
                Title,
                c =>
                {
                    GetExpression(prefix, out var expression, out var count);

                    if (count%2 == 1)
                    {
                        expression = SyntaxFactory.PrefixUnaryExpression(
                            prefix.Kind(),
                            expression);
                    }

                    var newRoot = root.ReplaceNode(prefix, expression
                        .WithAdditionalAnnotations(Formatter.Annotation));
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                context.Diagnostics);

            return Task.CompletedTask;
        }

        private static void GetExpression(PrefixUnaryExpressionSyntax prefix, out ExpressionSyntax expression, out uint count)
        {
            count = 0;
            var currentUnary = prefix;
            do
            {
                count++;
                expression = currentUnary.Operand;
                currentUnary = currentUnary.Operand as PrefixUnaryExpressionSyntax;
            }
            while (currentUnary != null);
        }
    }
}
