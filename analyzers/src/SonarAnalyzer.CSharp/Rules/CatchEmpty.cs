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

namespace SonarAnalyzer.CSharp.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CatchEmpty : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2486";
        private const string MessageFormat = "Handle the exception or explain in a comment why it can be ignored.";

        private static readonly DiagnosticDescriptor rule =
            DescriptorFactory.Create(DiagnosticId, MessageFormat);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(rule);

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterNodeAction(
                c =>
                {
                    var catchClause = (CatchClauseSyntax)c.Node;

                    if (!HasStatements(catchClause) &&
                        !HasComments(catchClause) &&
                        IsGenericCatch(catchClause, c.Model))
                    {
                        c.ReportIssue(rule, c.Node);
                    }
                },
                SyntaxKind.CatchClause);
        }

        private static bool IsGenericCatch(CatchClauseSyntax catchClause, SemanticModel semanticModel)
        {
            if (catchClause.Declaration == null)
            {
                return true;
            }

            if (catchClause.Filter != null)
            {
                return false;
            }

            var type = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type;
            return type.Is(KnownType.System_Exception);
        }

        private static bool HasComments(CatchClauseSyntax catchClause)
        {
            return catchClause.Block.OpenBraceToken.TrailingTrivia.Any(IsCommentTrivia) ||
                catchClause.Block.CloseBraceToken.LeadingTrivia.Any(IsCommentTrivia);
        }

        private static bool IsCommentTrivia(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia);
        }

        private static bool HasStatements(CatchClauseSyntax catchClause)
        {
            return catchClause.Block.Statements.Any();
        }
    }
}
