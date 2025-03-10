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
    public sealed class MagicNumberShouldNotBeUsed : SonarDiagnosticAnalyzer
    {
        private const string DiagnosticId = "S109";
        private const string MessageFormat = "Assign this magic number '{0}' to a well-named variable or constant, and use that instead.";

        private static readonly DiagnosticDescriptor Rule = DescriptorFactory.Create(DiagnosticId, MessageFormat);

        private static readonly ISet<string> NotConsideredAsMagicNumbers = new HashSet<string> { "-1", "0", "1" };
        private static readonly string[] AcceptedCollectionMembersForSingleDigitComparison = { "Size", "Count", "Length" };

        private static readonly HashSet<SyntaxKind> AllowedSingleDigitComparisons =
            [
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression,
                SyntaxKind.LessThanOrEqualExpression,
                SyntaxKind.LessThanExpression,
                SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression
            ];

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        protected override void Initialize(SonarAnalysisContext context) =>
            context.RegisterNodeAction(
                c =>
                {
                    var literalExpression = (LiteralExpressionSyntax)c.Node;

                    if (!IsExceptionToTheRule(literalExpression))
                    {
                        c.ReportIssue(Rule, literalExpression, literalExpression.Token.ValueText);
                    }
                },
                SyntaxKind.NumericLiteralExpression);

        private static bool IsExceptionToTheRule(LiteralExpressionSyntax literalExpression) =>
            NotConsideredAsMagicNumbers.Contains(literalExpression.Token.ValueText)
            || literalExpression.FirstAncestorOrSelf<VariableDeclarationSyntax>() != null
            || literalExpression.FirstAncestorOrSelf<ParameterSyntax>() != null
            || literalExpression.FirstAncestorOrSelf<EnumMemberDeclarationSyntax>() != null
            || literalExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.ValueText == nameof(object.GetHashCode)
            || literalExpression.FirstAncestorOrSelf<PragmaWarningDirectiveTriviaSyntax>() != null
            || IsInsideProperty(literalExpression)
            || IsSingleDigitInToleratedComparisons(literalExpression)
            || IsToleratedArgument(literalExpression);

        // Inside property we consider magic numbers as exceptions in the following cases:
        //   - A {get; set;} = MAGIC_NUMBER
        //   - A { get { return MAGIC_NUMBER; } }
        private static bool IsInsideProperty(SyntaxNode node)
        {
            if (node.FirstAncestorOrSelf<PropertyDeclarationSyntax>() == null)
            {
                return false;
            }
            var parent = node.Parent;
            return parent is ReturnStatementSyntax || parent is EqualsValueClauseSyntax;
        }

        private static bool IsSingleDigitInToleratedComparisons(LiteralExpressionSyntax literalExpression) =>
            literalExpression.Parent is BinaryExpressionSyntax binaryExpression
            && IsSingleDigit(literalExpression.Token.ValueText)
            && binaryExpression.IsAnyKind(AllowedSingleDigitComparisons)
            && IsComparingCollectionSize(binaryExpression);

        private static bool IsToleratedArgument(LiteralExpressionSyntax literalExpression) =>
            IsToleratedMethodArgument(literalExpression)
            || IsSingleOrNamedAttributeArgument(literalExpression);

        // Named argument or constructor argument.
        private static bool IsToleratedMethodArgument(LiteralExpressionSyntax literalExpression) =>
            literalExpression.Parent is ArgumentSyntax arg
            && (arg.NameColon is not null || arg.Parent.Parent is ObjectCreationExpressionSyntax || LooksLikeTimeApi(arg.Parent.Parent));

        private static bool LooksLikeTimeApi(SyntaxNode node) =>
            node is InvocationExpressionSyntax invocationExpression
            && invocationExpression.Expression.GetIdentifier() is { } identifier
            && identifier.ValueText.StartsWith("From");

        private static bool IsSingleOrNamedAttributeArgument(LiteralExpressionSyntax literalExpression) =>
            literalExpression.Parent is AttributeArgumentSyntax arg
            && (arg.NameColon is not null
                || arg.NameEquals is not null
                || (arg.Parent is AttributeArgumentListSyntax argList && argList.Arguments.Count == 1));

        private static bool IsSingleDigit(string text) => byte.TryParse(text, out var result) && result <= 9;

        // We allow single-digit comparisons when checking the size of a collection, which is usually done to access the first elements.
        private static bool IsComparingCollectionSize(BinaryExpressionSyntax binaryComparisonToLiteral)
        {
            var comparedToLiteral = binaryComparisonToLiteral.Left is LiteralExpressionSyntax ? binaryComparisonToLiteral.Right : binaryComparisonToLiteral.Left;
            return GetMemberName(comparedToLiteral) is { } name
                && AcceptedCollectionMembersForSingleDigitComparison.Contains(name);

            // we also allow LINQ Count() - the implementation is kept simple to avoid expensive SemanticModel calls
            static string GetMemberName(SyntaxNode node) =>
                node switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                    InvocationExpressionSyntax invocationExpressionSyntax => GetMemberName(invocationExpressionSyntax.Expression),
                    _ => null
                };
        }
    }
}
