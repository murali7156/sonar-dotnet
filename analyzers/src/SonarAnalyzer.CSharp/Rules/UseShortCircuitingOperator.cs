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
    public sealed class UseShortCircuitingOperator : UseShortCircuitingOperatorBase<SyntaxKind, BinaryExpressionSyntax>
    {
        protected override ILanguageFacade<SyntaxKind> Language => CSharpFacade.Instance;

        protected override string GetSuggestedOpName(BinaryExpressionSyntax node) =>
            OperatorNames[ShortCircuitingAlternative[node.Kind()]];

        protected override string GetCurrentOpName(BinaryExpressionSyntax node) =>
            OperatorNames[node.Kind()];

        protected override SyntaxToken GetOperator(BinaryExpressionSyntax expression) =>
            expression.OperatorToken;

        internal static readonly IDictionary<SyntaxKind, SyntaxKind> ShortCircuitingAlternative = new Dictionary<SyntaxKind, SyntaxKind>
        {
            { SyntaxKind.BitwiseAndExpression, SyntaxKind.LogicalAndExpression },
            { SyntaxKind.BitwiseOrExpression, SyntaxKind.LogicalOrExpression }
        }.ToImmutableDictionary();

        private static readonly IDictionary<SyntaxKind, string> OperatorNames = new Dictionary<SyntaxKind, string>
        {
            { SyntaxKind.BitwiseAndExpression, "&" },
            { SyntaxKind.BitwiseOrExpression, "|" },
            { SyntaxKind.LogicalAndExpression, "&&" },
            { SyntaxKind.LogicalOrExpression, "||" },
        }.ToImmutableDictionary();

        protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => ImmutableArray.Create<SyntaxKind>(
            SyntaxKind.BitwiseAndExpression,
            SyntaxKind.BitwiseOrExpression);
    }
}
